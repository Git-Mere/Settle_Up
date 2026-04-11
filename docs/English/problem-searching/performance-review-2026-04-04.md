# Performance Review 2026-04-04

This document summarizes the results of reviewing possible bottlenecks and memory-growth risks in the current implementations of `discord-api` and `receipt-parser`.

The review focused on two questions:

- latency between the moment a user uploads a receipt and the moment the public check message appears
- whether the service structure accumulates unnecessary calls or in-memory state over long runtimes

## Scope

Reviewed areas:

- `services/discord-api`
- `services/receipt-parser`

The following flow received the most attention:

1. Discord `/settle-up` upload start
2. Blob upload
3. Event Grid -> `receipt-parser`
4. Document Intelligence parsing
5. parser Cosmos save
6. `discord-api` callback
7. public check message post or refresh

## Summary

The first-upload latency currently felt by users appears to come from multiple cold-path costs stacking together rather than from one single cause.

The main contributors were:

- `discord-api` checks Blob container existence on every upload
- `receipt-parser` pays Blob download, Document Intelligence analysis, and Cosmos container initialization on the first parsing path
- `discord-api` looks up the uploader display name again through the Discord REST API while processing the parser callback

In addition, `discord-api` currently keeps confirmed sessions and per-session locks in memory, which creates a long-running memory-growth risk.

## Findings

### 1. Discord user information is looked up again right before draft publish

Severity:

- High

Description:

`discord-api` receives the parser callback and calls `ResolveUploadedByDisplayNameAsync` before creating or refreshing the public check message.

That call sits on the critical path immediately before the public check message is published. Even when the uploader display name is already known from pending-session creation, the service looks it up again, adding another unnecessary network call.

This also explains why the first execution feels especially slow. The first Discord REST call can pay DNS, TLS, and connection-setup costs at the same time.

Relevant code:

- [ReceiptDraftSessionService.cs#L165](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptDraftSessionService.cs#L165)
- [ReceiptDraftSessionService.cs#L178](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptDraftSessionService.cs#L178)
- [ReceiptDraftSessionService.cs#L281](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptDraftSessionService.cs#L281)

Impact:

- longer time from parser callback to public check message visibility
- worse perceived latency during cold start

Recommended direction:

- reuse the display name already stored in the pending session whenever possible
- only fall back to Discord REST when it is truly needed

### 2. Blob container existence check is executed for every upload

Severity:

- High

Description:

The Blob uploader in `discord-api` calls `CreateIfNotExistsAsync` on every upload.

That call requires a network round trip and still runs even when the container already exists in production. On the first upload, it can also overlap with Azure authentication and first-connection initialization, making the delay more noticeable.

Relevant code:

- [BlobImageUploader.cs#L61](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Storage/BlobImageUploader.cs#L61)
- [BlobImageUploader.cs#L68](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Storage/BlobImageUploader.cs#L68)

Impact:

- unnecessary delay at the `/settle-up` upload start stage
- overlaps with cold authentication costs on the first upload

Recommended direction:

- move it to startup or a lazy one-time initialization path
- avoid checking container existence on every request path

### 3. The first `receipt-parser` processing path has a long cold path

Severity:

- High

Description:

The first receipt processed by `receipt-parser` currently pays the following costs in sequence:

- Blob download
- Document Intelligence call
- parser Cosmos container initialization

`AnalyzeDocumentAsync(WaitUntil.Completed, ...)` waits until receipt analysis completes, and the parser Cosmos repository calls `CreateContainerIfNotExistsAsync` on the first save.

Relevant code:

- [DocumentIntelligenceReceiptParser.cs#L39](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/DocumentIntelligenceReceiptParser.cs#L39)
- [DocumentIntelligenceReceiptParser.cs#L71](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/DocumentIntelligenceReceiptParser.cs#L71)
- [CosmosReceiptRepository.cs#L46](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/CosmosReceiptRepository.cs#L46)
- [CosmosReceiptRepository.cs#L90](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/CosmosReceiptRepository.cs#L90)

Impact:

- the first receipt takes noticeably longer than later ones
- matches the symptom that users feel the very first receipt is especially slow

Recommended direction:

- consider parser startup warm-up
- move Cosmos container initialization out of the request path or prewarm it once
- review deployment settings that can reduce cold starts in production

### 4. Confirmed sessions are not removed from memory

Severity:

- Medium

Description:

`discord-api` currently removes sessions on cancel, but on confirm it only changes the session to `IsConfirmed = true` and does not remove it from the store.

That means confirmed sessions remain in `_sessions`. If the number of sessions keeps growing, this can affect both memory usage and any functionality that uses linear scans across all sessions.

Relevant code:

- [ReceiptInteractionService.cs#L561](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptInteractionService.cs#L561)
- [ReceiptInteractionService.cs#L664](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptInteractionService.cs#L664)
- [ReceiptSessionStore.cs#L5](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptSessionStore.cs#L5)
- [ReceiptSessionStore.cs#L76](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptSessionStore.cs#L76)

Impact:

- memory growth over long runtimes
- higher linear-scan cost in some code paths

Recommended direction:

- remove sessions after confirm or introduce TTL-based cleanup
- if confirmed UI must remain accessible, consider shrinking retained state to minimal metadata only

### 5. Per-session locks are never cleaned up

Severity:

- Medium

Description:

`ReceiptSessionLockManager` stores one `SemaphoreSlim` per session in a `ConcurrentDictionary`, but it does not have any removal logic.

As the session count increases, the lock dictionary grows with it. Combined with the current behavior of keeping confirmed sessions in the store, this accumulation becomes more meaningful.

Relevant code:

- [ReceiptSessionLockManager.cs#L5](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptSessionLockManager.cs#L5)

Impact:

- memory growth over long runtimes

Recommended direction:

- remove the lock entry when the session ends
- or introduce reference counting / TTL-based cleanup

### 6. `/language` scans all sessions linearly

Severity:

- Low

Description:

After changing a user's public language, `/language` iterates over all sessions through `ReceiptSessionStore.GetAll()` and refreshes the sessions owned by that user.

This is not currently a hot path, so it is not an urgent bottleneck. However, if confirmed sessions continue to remain in memory, the cost can slowly grow over time.

Relevant code:

- [LanguageCommandHandler.cs#L52](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Commands/LanguageCommandHandler.cs#L52)
- [ReceiptSessionStore.cs#L76](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptSessionStore.cs#L76)

Recommended direction:

- consider a secondary index by owner id
- or restrict the scan to active sessions only

### 7. User language settings also accumulate in memory

Severity:

- Low

Description:

`UserLanguagePreferenceStore` uses only an in-memory dictionary and has no removal or expiration policy.

At the current scale this is not a major issue, but if the service lives a long time and the number of users keeps growing, the dictionary will continue to grow as well.

Relevant code:

- [UserLanguagePreferenceStore.cs#L5](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Localization/UserLanguagePreferenceStore.cs#L5)

Recommended direction:

- acceptable at the current stage
- reconsider persistence or cleanup policy if operational scale grows

## Non-Issues for This Symptom

Items that do not appear to be directly related to the perceived latency in this review:

- 1-second public message debounce

Reason:

- after the draft callback, public check message creation/refresh uses direct `SendToChannelAsync` or `RefreshAsync` paths rather than the debounced path

Relevant code:

- [ReceiptDraftSessionService.cs#L202](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptDraftSessionService.cs#L202)
- [ReceiptDraftSessionService.cs#L222](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptDraftSessionService.cs#L222)

## Why The First Upload Feels Especially Slow

The current cold path for the first upload can be interpreted roughly like this:

1. `discord-api` performs a Blob container existence check
2. Blob upload may pay Azure authentication and first-connection costs
3. `receipt-parser` downloads the Blob
4. `receipt-parser` makes the first Document Intelligence analysis call
5. `receipt-parser` initializes the Cosmos container and performs the upsert
6. `discord-api` looks up the uploader name again through Discord REST during callback handling
7. the public check message is posted or refreshed

In other words, the symptom that “the first receipt is especially slow” is fully consistent with the current code and operating characteristics.

## Recommended Next Steps

Suggested priority order:

1. remove the Discord user name re-lookup from the `discord-api` draft publish path
2. move Blob container existence checks out of the request path
3. move parser Cosmos container initialization to one-time prewarm
4. add session and lock cleanup after confirm
5. add startup warm-up if needed

## Notes

This document is a diagnostic review based on the current code.

- actual latency proportions can vary by Azure environment, cold start frequency, network conditions, and Document Intelligence response time
- if quantitative latency profiling is needed, a follow-up effort should collect OpenTelemetry span duration together with structured logs

# Performance Review 2026-04-07

This document summarizes the results of re-reviewing the current code state as of 2026-04-07.

Several major items identified in the previous review had already been addressed.

- `discord-api` Blob uploader warm-up added
- `receipt-parser` parser/Cosmos warm-up added
- uploader display-name reuse path added during parser callback
- receipt session / session lock cleanup added after confirm

Accordingly, this document focuses on inefficiency candidates that still remain after the previous review.

## Scope

- `services/discord-api`
- `services/receipt-parser`

Flows reviewed most closely:

1. Discord receipt interaction mutation
2. rendering and history generation immediately before and after confirm
3. parser save/send hot path
4. stale-object accumulation risk over long runtimes

## Summary

The two most noticeable remaining inefficiencies are:

- the confirm/history path still recomputes the same allocation-style data multiple times
- in-memory objects can remain for a long time when users abandon the upload flow or when a downstream callback never arrives

On the parser side, the success path still performs two Cosmos upserts for the same document, which is worth reevaluating from a cost-benefit perspective.

## Findings

### 1. Participant item-share calculation is repeated once per participant during confirm

Severity:

- High

Description:

`ConfirmedSettlementHistoryDocument.FromSession(...)` first calls `ReceiptAllocationService.Calculate(session)` once.
It then calls `BuildParticipantItems(session, participant.UserId)` for each participant, and that method calls `ReceiptAllocationService.CalculateParticipantItemShares(session)` internally.

The problem is that `CalculateParticipantItemShares(...)` iterates over all session items again for each participant.
If there are 5 participants, the same share map is recalculated 5 times.

Relevant code:

- [ConfirmedSettlementHistoryDocument.cs#L78](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ConfirmedSettlementHistoryDocument.cs#L78)
- [ConfirmedSettlementHistoryDocument.cs#L105](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ConfirmedSettlementHistoryDocument.cs#L105)
- [ConfirmedSettlementHistoryDocument.cs#L137](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ConfirmedSettlementHistoryDocument.cs#L137)
- [ReceiptAllocationService.cs#L66](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptAllocationService.cs#L66)

Impact:

- increased CPU usage on the confirm hot path
- wasted computation grows quickly as both item count and participant count increase
- repeated allocations and dictionary creation immediately before history persistence

Recommended direction:

- calculate the participant item-share map once at the start of confirm/history generation
- pass the precomputed share map into `BuildParticipantItems(...)`
- if possible, combine the result of `ReceiptAllocationService.Calculate(...)` and the share map into one immutable result object

### 2. Allocation/render helpers rescan `UserSelections` for every item

Severity:

- Medium

Description:

`ReceiptAllocationService.Calculate(...)` and `CalculateParticipantItemShares(...)` both call `ReceiptSessionStateService.GetUsersForItem(session, item.Id)` for each item.
But `GetUsersForItem(...)` scans the entire `session.UserSelections` structure each time to find the users for that item.

The same pattern appears in `GetUnassignedItems(...)`, which iterates items and then calls `GetUsersForItem(...)` again internally.

In other words, the current structure lacks an `item -> users` reverse mapping, so render/confirm/alcohol/tax calculation paths repeatedly rediscover the same relationship.

Relevant code:

- [ReceiptAllocationService.cs#L7](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptAllocationService.cs#L7)
- [ReceiptAllocationService.cs#L72](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptAllocationService.cs#L72)
- [ReceiptSessionStateService.cs#L340](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptSessionStateService.cs#L340)
- [ReceiptSessionStateService.cs#L369](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptSessionStateService.cs#L369)
- [ReceiptMessageRenderer.cs#L16](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptMessageRenderer.cs#L16)

Impact:

- fixed-cost overhead in rendering and confirm calculations becomes more visible as receipt item count and participant count grow
- unnecessary array creation (`ToArray`) and sorting work repeat
- acceptable for small receipts, but more likely to be noticeable on larger grocery or group-meal receipts

Recommended direction:

- maintain an `itemId -> assignedUsers` reverse index on mutation
- or reuse the map built by `ReceiptRenderContext.Create(...)` in allocation and history paths as well
- even if `GetUsersForItem(...)` remains as a general helper, hot paths should use only precomputed maps

### 3. Interaction/session objects can remain for a long time when the upload flow is abandoned

Severity:

- Medium

Description:

In the `/settle-up` flow, clicking the button stores the full `SocketMessageComponent` in `_uploadPromptInteractions`.
That value is removed only when modal submission succeeds and `TryDeleteUploadPromptAsync(...)` is called.

If a user clicks the button and then closes the modal, disconnects the client, or abandons the flow before submitting, the dictionary entry remains indefinitely.

Likewise, a pending upload session is added to the store in `CreatePendingUploadSessionAndReturnAsync(...)` and is naturally cleaned up only on upload failure or parser-callback success. A TTL/cleanup path for stalled cases where the parser callback never arrives is not currently visible.

Relevant code:

- [SettleUpCommandHandler.cs#L18](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Commands/SettleUpCommandHandler.cs#L18)
- [SettleUpCommandHandler.cs#L108](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Commands/SettleUpCommandHandler.cs#L108)
- [SettleUpCommandHandler.cs#L214](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Commands/SettleUpCommandHandler.cs#L214)
- [ReceiptDraftSessionService.cs#L32](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptDraftSessionService.cs#L32)
- [ReceiptSessionState.cs#L26](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L26)
- [ReceiptSessionStore.cs#L5](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptSessionStore.cs#L5)

Impact:

- abandoned flows can increase memory usage over long runtimes
- retained objects are heavier than a light leak because they include Discord interaction/message/channel references
- stale pending UI can make operational tracking harder

Recommended direction:

- add TTL-based sweeping to `_uploadPromptInteractions`
- add stale cleanup for pending receipt sessions based on creation timestamp
- narrow cleanup targets to `IsDraftReady == false` or "no callback after N minutes"

### 4. The parser success path upserts the same document to Cosmos twice

Severity:

- Medium

Description:

`ReceiptProcessingService.SaveAndSendDraftAsync(...)` first stores a document with `NotificationStatus=Pending`, and after HTTP delivery succeeds it upserts almost the same body again with `NotificationStatus=Sent`.

That double-write may have been intentional for failure recovery and reprocessing design. However, under the current code, the success path is likely the more common one, which means the service serializes and writes a large document containing the item list and metadata twice.

Relevant code:

- [ReceiptProcessingService.cs#L87](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/ReceiptProcessingService.cs#L87)
- [ReceiptProcessingService.cs#L93](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/ReceiptProcessingService.cs#L93)
- [ReceiptProcessingService.cs#L100](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/ReceiptProcessingService.cs#L100)
- [ReceiptProcessingService.cs#L101](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/ReceiptProcessingService.cs#L101)
- [CosmosReceiptRepository.cs#L40](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/CosmosReceiptRepository.cs#L40)

Impact:

- higher RU cost on the success path
- double the serialization/allocation work
- more Cosmos write pressure during Event Grid bursts

Recommended direction:

- reevaluate whether the initial Pending write is truly necessary for crash recovery
- if delivery tracking must remain, consider a separate status-only document or lightweight delivery-tracking record
- otherwise, consider writing once on the success path and persisting retry/reprocessing metadata only on failure

### 5. Uncached render still performs a lot of section-level LINQ materialization

Severity:

- Low

Description:

Every time render cache is invalidated, `ReceiptMessageRenderer` recreates `ReceiptRenderContext.Create(...)` and each section builder uses `Select/Where/OrderBy/ToArray` multiple times.

At the moment this is not an urgent bottleneck thanks to debounce and rendered-cache support, but repeated mutations over a receipt with many items will keep allocating intermediate arrays and strings.

Relevant code:

- [ReceiptMessageRenderer.cs#L5](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptMessageRenderer.cs#L5)
- [ReceiptMessageRenderer.cs#L149](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptMessageRenderer.cs#L149)
- [ReceiptMessageRenderer.cs#L168](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptMessageRenderer.cs#L168)
- [ReceiptMessageRenderer.cs#L362](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptMessageRenderer.cs#L362)

Impact:

- possible GC pressure during hot mutation periods
- section-string construction cost grows as item count increases

Recommended direction:

- not urgent; remove duplicated allocation-helper work first
- then consider rewriting section builders with imperative loops
- adjust builders to reuse maps/totals already present in the render context more aggressively

## Non-Findings

Items that were concerns before but appear resolved in this review:

- missing session/lock cleanup after confirm
- repeated Blob uploader container-readiness work on every request
- missing parser startup credential/container warm-up
- mandatory Discord user display-name re-lookup right before draft callback publish

## Recommended Next Steps

Suggested priority order:

1. reduce duplicated computation in `ConfirmedSettlementHistoryDocument` and `ReceiptAllocationService`
2. add TTL cleanup for pending uploads / abandoned interactions
3. reevaluate whether the parser success path really needs double Cosmos upserts
4. after that, reduce LINQ allocations in the render path

## Suggested First Refactor

The best first target is to expand the allocation result object.

For example, if the following data were calculated once and returned together, a large portion of the current duplication would disappear:

- `itemId -> assignedUsers`
- `userId -> itemShares`
- `participant breakdown`
- `tax lines`
- `tip lines`

That would allow the following to reuse the same precomputed graph:

- confirm embed
- history document creation
- unassigned/shared/individual sections
- tax/tip sections

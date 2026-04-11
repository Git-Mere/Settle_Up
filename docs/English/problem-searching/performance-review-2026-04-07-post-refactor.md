# Performance Review 2026-04-07 Post Refactor

This document summarizes the results of re-reviewing the current code state after the recent structural refactor, as of 2026-04-07.

Several major items identified in the previous performance/memory review had already been addressed.

- `discord-api` Blob uploader warm-up added
- `receipt-parser` startup warm-up added
- receipt session / session lock cleanup added after confirm
- existing public receipt message refresh removed from `/language`
- `discord-api` responsibilities split more clearly across interaction / session / history services

Accordingly, this document focuses on bottleneck candidates, memory-retention cost, and unnecessary calls that still remain after the refactor.

## Scope

- `services/discord-api`
- `services/receipt-parser`

Flows reviewed most closely:

1. draft-session upsert during parser callback
2. Discord public main message refresh / confirm
3. session TTL cleanup
4. selection panel open/update
5. render-context creation and settlement calculation

## Summary

At the current state, no critical issue requiring immediate correction was found.

However, several residual risks remain:

- the draft-callback critical path still contains a Discord REST lookup
- a single active session can retain `IDiscordInteraction`, edit tokens, render cache, and similar objects for a long time
- debounced refresh still creates small task/CTS allocation churn during rapid interaction bursts
- some repeated computation remains in the selection-panel and render paths

In other words, the current state is better described as “per-session memory retention cost and some repeated hot-path computation remain” rather than “a leaking system that is actively breaking.”

## Findings

### 1. Uploader display-name lookup still remains on the critical path before draft callback publish

Severity:

- Medium

Description:

`ReceiptDraftSessionService` finds an existing session through `FindExistingSession(...)` before applying the draft payload to the session, then calls `ResolveUploadedByDisplayNameAsync(...)` based on that result.

If the existing pending session still has the display name, it is reused, and a recent change also moved display-name verification to run again inside the session lock.
As a result, the earlier problem of duplicate lookups outside the lock during overlapping callback retries for the same receipt has been reduced.

However, on a cache miss, the Discord REST fallback still remains on the draft-publish critical path.

Relevant code:

- [ReceiptDraftSessionService.cs#L172](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptDraftSessionService.cs#L172)
- [ReceiptDraftSessionService.cs#L177](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptDraftSessionService.cs#L177)
- [ReceiptDraftSessionService.cs#L177](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptDraftSessionService.cs#L177)
- [ReceiptDraftSessionService.cs#L282](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptDraftSessionService.cs#L282)
- [ReceiptDraftSessionService.cs#L301](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptDraftSessionService.cs#L301)
- [ReceiptDraftSessionService.cs#L311](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptDraftSessionService.cs#L311)

Impact:

- the critical path still contains a network call before the public check message appears after the parser callback
- on cache misses, first draft publish still depends on Discord REST latency

Recommended direction:

- the recent change already added a re-check inside the lock
- if another step is needed, trust the display name captured during pending-session creation more strongly and leave REST only as a fallback

### 2. Active receipt sessions retain interaction/token references for the duration of the TTL

Severity:

- Medium

Description:

`ReceiptSessionState` currently stores `PendingEditItemIds`, `ActivePrivatePanelInteractions`, `MainMessage`, and `CachedRenderedMessage` directly in the session object.

Confirm/cancel/expiry cleanup significantly reduced the risk of global leaks, but a long-lived active session can still hold onto relatively heavy objects.
If a user opens panels repeatedly, creates multiple edit modal tokens, and leaves the session unconfirmed for a long time, the memory footprint of that single session can grow.

Relevant code:

- [ReceiptSessionState.cs#L23](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L23)
- [ReceiptSessionState.cs#L24](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L24)
- [ReceiptSessionState.cs#L25](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L25)
- [ReceiptSessionState.cs#L26](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L26)
- [ReceiptSessionState.cs#L27](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L27)
- [ReceiptSessionState.cs#L28](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L28)
- [ReceiptSessionState.cs#L38](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L38)
- [ReceiptSessionState.cs#L39](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L39)

Impact:

- larger memory usage for long-lived active sessions
- abandoned interaction references can remain until TTL cleanup

Recommended direction:

- consider TTL or bounded size for `PendingEditItemIds`
- consider storing only minimal identifiers instead of the full interaction object in `ActivePrivatePanelInteractions`
- if `CachedRenderedMessage` grows large, consider size- or age-based invalidation

### 3. Debounced refresh still creates task/CTS allocation churn during rapid mutations

Severity:

- Medium

Description:

`ReceiptMainMessageDebounceService.ScheduleRefresh(...)` creates a new `CancellationTokenSource` and starts a fire-and-forget task on each call.
Previous pending refreshes are cancelled and disposed, so the logical leak risk is small, but rapid repeated clicks can still create many short-lived CTS objects and tasks.

Relevant code:

- [ReceiptMainMessageDebounceService.cs#L25](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptMainMessageDebounceService.cs#L25)
- [ReceiptMainMessageDebounceService.cs#L29](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptMainMessageDebounceService.cs#L29)
- [ReceiptMainMessageDebounceService.cs#L38](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptMainMessageDebounceService.cs#L38)
- [ReceiptMainMessageDebounceService.cs#L55](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptMainMessageDebounceService.cs#L55)

Impact:

- more allocations during rapid click/mutation bursts
- not a major issue yet, but long interaction bursts could increase GC pressure

Recommended direction:

- consider a per-session single-timer model
- or consider a coalescing worker based on “next refresh at” semantics

### 4. TTL cleanup copies all sessions into an array and scans them periodically

Severity:

- Low

Description:

Every minute, `ReceiptSessionExpiryService` calls `ReceiptSessionStore.GetAll()`, copies all sessions into an array, and scans them.
If the number of active sessions remains small, the cost is minor, but periodic allocation and linear-scan cost will grow with session count.

Relevant code:

- [ReceiptSessionStore.cs#L76](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptSessionStore.cs#L76)
- [ReceiptSessionStore.cs#L78](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptSessionStore.cs#L78)
- [ReceiptSessionExpiryService.cs#L56](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptSessionExpiryService.cs#L56)
- [ReceiptSessionExpiryService.cs#L62](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptSessionExpiryService.cs#L62)

Impact:

- background sweep cost grows with active session count
- periodic array allocation

Recommended direction:

- acceptable at the current scale
- if the scale grows, consider a min-heap/expiry-bucket structure or additional owner/session indexes

### 5. Selection-panel item display-name calculation becomes O(n^2) when duplicate items are frequent

Severity:

- Low

Description:

`ReceiptSelectionPanelService` calls `GetSelectionDisplayName(...)` for each item when building option labels.
That helper calls `session.Items.Count(...)` and `GetInstanceIndex(...)` internally, so large receipts with many duplicate groups trigger repeated scans.

Relevant code:

- [ReceiptSelectionPanelService.cs#L65](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptSelectionPanelService.cs#L65)
- [ReceiptSelectionPanelService.cs#L67](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptSelectionPanelService.cs#L67)
- [ReceiptSelectionPanelService.cs#L130](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptSelectionPanelService.cs#L130)
- [ReceiptSelectionPanelService.cs#L139](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptSelectionPanelService.cs#L139)

Impact:

- panel open/update cost can grow for sessions with many items, such as `/custom`

Recommended direction:

- build a duplicate-index map once and reuse it, similar to the render context
- precompute `itemId -> displayName` during panel build

### 6. The renderer still splits allocation-style computation into two passes during one uncached render

Severity:

- Low

Description:

`ReceiptMessageRenderer.ReceiptRenderContext.Create(...)` calls both `ReceiptAllocationService.Calculate(session)` and `CalculateParticipantItemShares(...)`.
Both derive maps/dictionaries from the same assignment relationships, so part of the computation graph overlaps.

Render cache means this is not an urgent bottleneck, but repeated cache invalidation during active mutation still leaves duplicated work.

Relevant code:

- [ReceiptMessageRenderer.cs#L412](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Rendering/ReceiptMessageRenderer.cs#L412)
- [ReceiptMessageRenderer.cs#L415](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Rendering/ReceiptMessageRenderer.cs#L415)
- [ReceiptMessageRenderer.cs#L460](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Rendering/ReceiptMessageRenderer.cs#L460)
- [ReceiptMessageRenderer.cs#L461](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Rendering/ReceiptMessageRenderer.cs#L461)

Impact:

- more CPU/allocation work during repeated mutations
- render-cache misses on large receipts can become more expensive

Recommended direction:

- combine allocation results and participant item shares into one immutable result object
- make both rendering and history generation reuse that same result

## Non-Findings

Items that were previous issues but appear resolved in this re-review:

- missing session cleanup after confirm
- missing session-lock cleanup
- `/language` refreshing all sessions
- request-path container readiness work in the Blob uploader

## Recommended Next Steps

Suggested priority order:

1. make `ReceiptDraftSessionService` even more cache-first around display-name fallback
2. reduce CTS/task churn in `ReceiptMainMessageDebounceService` with a simpler coalescing structure
3. reuse precomputed display names and allocation results in the selection-panel and renderer paths
4. reduce active-session memory footprint (`IDiscordInteraction`, edit tokens, render-cache lifetime)

## Suggested First Change

The best first target is `ReceiptDraftSessionService`.

Why:

- it still contains a remaining network-call path, so the payoff is immediate
- the scope of structural change is small
- it can reduce duplicate REST lookups during callback retry / duplicate-delivery situations

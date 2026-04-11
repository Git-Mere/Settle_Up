# Refactor Summary 2026-04-07

This document summarizes the structural-simplification refactor performed during the 2026-04-07 session.

The goal was not to change behavior. The goal was to keep current behavior intact while making responsibility boundaries clearer and separating duplicated cleanup, rendering, and persistence logic.

## Scope

- `services/discord-api`
- `services/receipt-parser`

## Summary

This refactor introduced the following changes.

- `discord-api` reorganized `Services` into subfolders by interaction, session, history, rendering, blob, testing, and calculations.
- session-end cleanup, private-panel cleanup, selection-panel rendering, and history-persistence retry were each split into dedicated services.
- `ReceiptInteractionService` was reduced to orchestration-focused responsibility.
- `receipt-parser` moved draft-document / outbound-payload creation into `ReceiptDraftFactory`, reducing `ReceiptProcessingService` to orchestration-focused logic.

## Discord API Changes

### 1. Reorganized the Services folder into role-based subdirectories

`services/discord-api/src/Services` now follows this structure.

- `Blob/`
- `Calculations/`
- `History/`
- `Interaction/`
- `Rendering/`
- `Session/`
- `Testing/`

The purpose of this change is to reduce the cost of locating files as the number of services grows and to make the interaction/session/history boundaries more explicit.

Related documentation updates:

- [services/discord-api/README.md](/home/aero-mere/CS397/Settle_Up/services/discord-api/README.md)
- [services/discord-api/codex.md](/home/aero-mere/CS397/Settle_Up/services/discord-api/codex.md)

### 2. Unified session-cleanup paths

Previously, confirm, cancel, pending delete, and TTL expiry each carried their own versions of debounce cancellation, private-panel cleanup, main-message deletion, session removal, and lock cleanup.

That shared cleanup responsibility now lives in `ReceiptSessionLifetimeService`.

Relevant code:

- [ReceiptSessionLifetimeService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptSessionLifetimeService.cs)
- [ReceiptInteractionService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptInteractionService.cs)
- [ReceiptSessionExpiryService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptSessionExpiryService.cs)
- [ReceiptDraftSessionService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptDraftSessionService.cs)

Effect:

- less duplication in cleanup logic
- lower risk of behavior drift between cleanup paths
- easier reasoning about confirm/cancel/expiry handling

### 3. Split private-panel management

Previously, `ReceiptInteractionService` directly handled active private-panel replacement and bulk panel cleanup.

Now `ReceiptPrivatePanelService` owns panel replacement, registration, and bulk cleanup.

Relevant code:

- [ReceiptPrivatePanelService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptPrivatePanelService.cs)

Effect:

- Discord private-panel lifecycle logic is removed from interaction orchestration code
- the connection between session cleanup paths and panel cleanup paths is clearer

### 4. Split selection-panel rendering and response behavior

Selection-panel content/component construction and the respond-vs-update branching were moved into `ReceiptSelectionPanelService`.

Relevant code:

- [ReceiptSelectionPanelService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptSelectionPanelService.cs)
- [ReceiptInteractionService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptInteractionService.cs)

Effect:

- `ReceiptInteractionService` no longer carries helper responsibility for UI panel building
- selection-panel logic is easier to work with in one place

### 5. Split background persistence for settlement history

History-save retry and failure follow-up response handling were moved into `SettlementHistoryPersistenceService`.

Relevant code:

- [SettlementHistoryPersistenceService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/History/SettlementHistoryPersistenceService.cs)

Effect:

- confirm flow no longer contains detailed history-retry implementation logic
- confirm orchestration is shorter and easier to read

### 6. Reduced `ReceiptInteractionService`

The largest change before and after the refactor is the role cleanup in `ReceiptInteractionService`.

- before: interaction routing + panel rendering + panel cleanup + history retry + part of session cleanup
- now: interaction routing + validation + mutation orchestration

This reduced the file size and made the service focus more directly on the actual receipt-mutation flow.

## Receipt Parser Changes

### 1. Split draft-document / notification-payload factory responsibility

The following responsibilities were moved from `ReceiptProcessingService` into `ReceiptDraftFactory`.

- `ReceiptDocument` creation
- `DiscordDraftNotificationPayload` creation
- `uploadedByUserId` extraction from the Blob path

Relevant code:

- [ReceiptDraftFactory.cs](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/ReceiptDraftFactory.cs)
- [ReceiptProcessingService.cs](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/ReceiptProcessingService.cs)

Effect:

- `ReceiptProcessingService` is more focused on parse -> save -> send orchestration
- rules for building the draft contract are easier to locate in one file
- service-boundary responsibilities are clearer on the parser side as well

## Verification

After the refactor, the following builds were run to verify compilation state.

- `dotnet build services/discord-api/src/DiscordApi.csproj -c Release`
- `dotnet build services/receipt-parser/receipt-parser.csproj -c Release`

Results:

- `discord-api`: 0 warnings, 0 errors
- `receipt-parser`: 0 warnings, 0 errors

Manual testing with a single user also confirmed that the expected major behaviors were preserved.

## Remaining Work

This refactor focused on structural simplification. The next stage is optimization and reducing residual risk.

Current follow-up candidates:

1. minimize display-name fallback REST calls in `ReceiptDraftSessionService`
2. reduce task/CTS churn in `ReceiptMainMessageDebounceService`
3. reuse precomputed display names in the selection-panel / renderer paths
4. reduce active-session memory footprint

In other words, this change is better understood as establishing a cleaner baseline for later optimization and problem analysis than as “the refactor is fully finished.”

## Follow-up Adjustments After Refactor

Small follow-up adjustments applied immediately after the refactor:

- `ReceiptDraftSessionService` was adjusted to re-check uploader display-name resolution inside the draft-session lock.
- active check receipt session TTL was reduced from `6 hours` to `3 hours`.

The goal of these adjustments was to reduce the chance of duplicate Discord REST lookups during callback retry/duplication scenarios and to shorten the memory/message retention window for abandoned check sessions.

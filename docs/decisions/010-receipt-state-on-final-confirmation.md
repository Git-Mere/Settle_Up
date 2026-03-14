# 010 - Persist Receipt State Only on Final Confirmation

## Status
Accepted

## Context

After removing the separate `settlement-service`, the `discord-api` service became responsible for handling the buyer-selection workflow and final settlement confirmation.

A design concern remained around concurrent updates to Cosmos DB.

One possible approach was to persist every buyer-selection change immediately to the database as users interact through Discord. However, this would introduce more write operations and increase the likelihood of concurrent modification issues, especially if multiple users make changes around the same time.

At the current stage of the project, the settlement interaction is driven entirely through Discord and does not require every intermediate selection state to be permanently stored.

The workflow is expected to behave as follows:

1. `receipt-parser` creates the initial draft receipt document
2. `discord-api` displays the receipt and manages user selections within Discord
3. users interact with the Discord message to indicate purchased items
4. selection changes are managed in the active Discord interaction state rather than being written to Cosmos DB immediately
5. the user who initiated the command is responsible for pressing the final `Confirm` button
6. only when `Confirm` is pressed will the final receipt/settlement state be written to Cosmos DB

This design reduces the number of database writes and narrows the write timing to a single final confirmation step.

## Decision

We will persist receipt settlement data to Cosmos DB only when the initiating user presses the final `Confirm` button.

During the buyer-selection phase:

- intermediate user selections will be managed by `discord-api`
- intermediate selection changes will not be persisted to Cosmos DB
- the receipt document created by `receipt-parser` will not be continuously updated for each selection change

The `Confirm` action will be restricted as follows:

- only the user who initiated the receipt-selection flow may confirm
- confirmation is allowed only when all required receipt items have been assigned
- when `Confirm` is pressed, `discord-api` must perform server-side validation before writing to Cosmos DB

Server-side validation must verify at least:

- the requesting user is the allowed confirmer
- the receipt is not already finalized
- all required items have been assigned
- applicable automatic rules such as alcohol-related tax assignment have been resolved

User-interface state such as enabling or disabling the `Confirm` button may be used to improve UX, but the final source of truth for confirmation eligibility will be server-side validation.

## Consequences

### Positive

- fewer Cosmos DB writes
- simpler implementation during the early project stage
- reduced chance of concurrent document updates during item selection
- clearer write timing and ownership of final receipt state
- easier to reason about the lifecycle of a receipt during Discord interaction

### Negative

- intermediate selection state is not durable during the selection phase
- if the Discord API process restarts or loses in-memory/session state, ongoing selection progress may be lost
- final confirmation still requires proper validation to prevent invalid or duplicate writes

## Follow-up Notes

This decision assumes that the project currently prioritizes simplicity over durable intermediate workflow recovery.

The receipt lifecycle should still be modeled clearly, for example:

- `Draft`
- `SelectionInProgress`
- `Finalized`

Even though intermediate selection changes are not persisted, the final persisted document should reflect the fully resolved result of the selection workflow.

If later project requirements demand recovery of in-progress buyer selections across restarts or deployments, persisting intermediate selection state can be reconsidered.
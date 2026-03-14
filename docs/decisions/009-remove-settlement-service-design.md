# 009 - Remove the Separate Settlement Service and Handle Settlement in Discord API

## Status
Accepted

## Context

In the initial Settle Up architecture, a separate `settlement-service` was planned after the buyer-selection step.

The original flow was roughly:

1. User uploads a receipt through Discord
2. Receipt image is stored in Blob Storage
3. OCR / receipt parsing is triggered
4. `receipt-parser` creates and stores a draft receipt
5. Users interact through Discord to mark which items they purchased
6. `settlement-service` calculates the final settlement
7. `settlement-service` stores the result

During design refinement, the settlement workflow was reconsidered.

The buyer-selection process is highly tied to Discord interactions:

- users select and update items through Discord UI components
- the bot needs to show current selection state in messages
- the bot needs to react to button/select interactions
- the bot needs to update the message dynamically as users make changes
- the final settlement is triggered directly from the Discord-side workflow

Because of this, the settlement process is not an isolated backend calculation step.  
It is part of a stateful Discord interaction flow.

A separate `settlement-service` would introduce additional complexity:

- an extra service boundary between `discord-api` and settlement logic
- additional API contracts between services
- more state synchronization concerns
- more deployment and debugging overhead
- ambiguity around which service owns the receipt state during user selection

Also, the extra Cosmos DB attached to the original `settlement-service` design does not provide much value at the current stage of the project.

## Decision

We will remove the separate `settlement-service`.

Instead, the `discord-api` service will handle:

- displaying the parsed draft receipt to users in Discord
- collecting user item selections
- dynamically updating the Discord message to reflect current selections
- applying settlement rules such as shared item ownership
- applying related rules such as alcohol-tax assignment
- calculating final settlement amounts
- updating the receipt document state in Cosmos DB

The `receipt-parser` service will remain responsible for:

- receiving OCR/parsing results
- normalizing receipt data
- creating the draft receipt document
- storing the initial draft receipt in Cosmos DB
- notifying `discord-api` that the draft is ready

The final settlement result will **not** be sent back to `receipt-parser` for modification.  
`receipt-parser` is responsible only for receipt parsing and draft creation, not for user-driven settlement state.

## Consequences

### Positive

- simpler architecture with fewer services
- less inter-service communication
- easier implementation and debugging during the early project stage
- clearer ownership of Discord interaction state
- easier to support dynamic message updates during item selection
- no need for a second Cosmos DB persistence flow for settlement data

### Negative

- `discord-api` now owns both Discord interaction logic and settlement logic
- the service becomes somewhat broader in responsibility
- if settlement rules become much more complex in the future, the logic may need to be extracted later

## Follow-up Notes

The system should treat the receipt document in Cosmos DB as the main source of truth and move it through state transitions such as:

- `Draft`
- `SelectionInProgress`
- `Finalized`

At this stage, both `receipt-parser` and `discord-api` may operate on the same receipt document lifecycle, with each service owning different phases of that lifecycle.

If the project later expands to support other clients beyond Discord, or if settlement logic becomes significantly more complex, introducing a dedicated settlement service can be reconsidered.
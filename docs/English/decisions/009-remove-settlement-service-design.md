# 009 - Remove the Separate Settlement Service

## Status
Accepted

## Context

An earlier architecture planned a separate `settlement-service` after the buyer-selection step.

That flow was roughly:

1. user uploads a receipt through Discord
2. the image is stored in Blob Storage
3. parsing is triggered
4. `receipt-parser` creates a draft receipt
5. users interact through Discord to mark item ownership
6. `settlement-service` calculates the final settlement
7. `settlement-service` stores the result

During refinement, it became clear that settlement behavior was tightly coupled to Discord interaction state:

- users select and update items through Discord UI components
- the bot must show current state in Discord messages
- the bot must react directly to button, select, and modal interactions
- final settlement is triggered from the Discord interaction flow itself

This raised the question of whether a separate service still provided enough value at the current stage.

## Options Considered

### Option A - Keep a Separate `settlement-service`

Advantages:

- stronger separation of settlement logic from Discord interaction code
- clearer future extraction path if settlement logic grows large

Disadvantages:

- adds another service boundary
- introduces more state synchronization complexity
- increases deployment and debugging overhead
- creates ambiguity around which service owns interaction-phase receipt state

### Option B - Handle Settlement Inside `discord-api`

Advantages:

- simpler architecture for the current phase
- Discord interaction state and settlement logic stay close together
- easier implementation of dynamic message-driven workflow
- avoids a second persistence flow that currently adds little value

Disadvantages:

- `discord-api` takes on broader responsibility
- long-term extraction may be needed if rules become much more complex

## Decision

We will remove the separate `settlement-service` for now and handle settlement workflow logic inside `discord-api`.

`receipt-parser` remains responsible for parsing and creating the initial draft receipt.

## Consequences

### Positive

- simpler architecture with fewer moving parts
- easier implementation and debugging
- clearer ownership of the Discord interaction workflow
- better support for dynamic receipt-selection UI

### Negative

- `discord-api` now owns both interaction and settlement concerns
- future extraction may be needed if settlement rules become significantly more complex

## Follow-up Notes

The receipt lifecycle should still be modeled clearly through states such as:

- `Draft`
- `SelectionInProgress`
- `Finalized`

If the system later expands beyond Discord or settlement logic grows substantially, a dedicated settlement service can be reconsidered.

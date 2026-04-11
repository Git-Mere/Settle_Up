# 007 - Use HTTP Between `receipt-parser` and `discord-api`

## Status
Accepted

## Context

The `receipt-parser` service processes receipt images and produces structured draft receipt data.

Once parsing is complete, the system needs to notify `discord-api` so that the draft can drive the next Discord-side workflow.

At this stage of the project, a concrete implementation choice was required for the parser-to-Discord integration:

1. send the parsed draft directly to `discord-api` over HTTP
2. publish an event and have `discord-api` consume it asynchronously

This decision affects complexity, coupling, debugging workflow, and delivery speed.

## Options Considered

### Option A - Direct HTTP Request

Flow:

1. user uploads a receipt through Discord
2. the image is stored in Blob Storage
3. Event Grid triggers `receipt-parser`
4. `receipt-parser` extracts structured receipt data
5. `receipt-parser` sends the result to `discord-api` over HTTP
6. `discord-api` continues the Discord interaction flow

Advantages:

- simpler end-to-end implementation for the current project stage
- easier local debugging and request tracing
- easier to reason about request/response behavior during development
- fewer moving parts than adding another eventing hop immediately

Disadvantages:

- tighter coupling between the two services
- downstream availability affects delivery
- retry and validation behavior must be handled explicitly

### Option B - Event-Driven Handoff

Advantages:

- looser service coupling
- better alignment with the broader event-driven architectural direction
- easier to add more downstream consumers later

Disadvantages:

- more infrastructure and operational complexity in the short term
- slower to implement during the current phase
- harder local debugging for this specific integration path

## Decision

We will use HTTP for communication from `receipt-parser` to `discord-api` for parsed draft delivery.

## Consequences

### Positive

- faster implementation of the parser-to-Discord handoff
- simpler end-to-end debugging in the current phase
- clearer short-term service contract for draft delivery

### Negative

- stronger direct dependency between the two services
- retry, error handling, and validation must be handled carefully
- this path is less aligned with the longer-term event-driven ideal

## Follow-up Notes

This is a pragmatic implementation decision for the current phase, not a rejection of event-driven design as a general architectural direction.

The HTTP endpoint must therefore be treated as an explicit service contract and should be hardened with:

- request validation
- clear logging
- retry behavior
- future authentication or service verification as needed

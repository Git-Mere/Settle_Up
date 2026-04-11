# 024 - Store Parser Draft Documents With A Single Cosmos Write

## Status
Accepted

## Context

`receipt-parser` stores parsed receipt drafts in Cosmos DB before sending them to `discord-api`.

Previously the service wrote the same draft document twice in the common success path:

1. save a large draft document with delivery status `Pending`
2. send the draft to `discord-api`
3. save the same large draft document again with delivery status `Sent`

The practical difference between the two writes was limited to delivery tracking fields such as notification status, attempt count, timestamps, and last error.

This meant the success path paid for:

- two Cosmos upserts
- two full document serializations
- repeated writes of the same item list and parse metadata

The service already performs immediate in-process retry on transient HTTP failures when sending to `discord-api`.
That retry behavior is triggered by HTTP failures and exceptions, not by reading pending delivery status back from Cosmos.

## Options Considered

### 1. Keep the current two-write status-tracking model

Pros:

- explicit delivery state persisted in Cosmos
- easier to inspect failed delivery attempts from stored documents
- future reprocessing design can build on stored status fields

Cons:

- success path pays for two writes of a large document
- Cosmos cost is higher than necessary
- most of the second upsert rewrites unchanged receipt content

### 2. Split delivery status into a separate lightweight document or container

Pros:

- keeps delivery observability
- avoids rewriting the large draft body on every status change
- allows independent retention policy for delivery tracking

Cons:

- introduces extra document and schema complexity
- still requires additional writes
- adds another model/query surface before the project has a concrete reprocessing workflow

### 3. Store the parsed draft document once and rely on retry + logs for delivery outcome

Pros:

- lowest Cosmos write cost in the success path
- simplest storage model
- keeps retry behavior without extra persistence churn

Cons:

- no persisted delivery status in Cosmos
- failed delivery investigation relies on logs rather than stored metadata
- a later reprocessing workflow will need a new persistence design if it becomes necessary

## Decision

Store the parsed draft document in Cosmos DB exactly once.

The stored draft document should contain only the parsed receipt content and metadata required by downstream consumers.
Delivery tracking fields such as notification status, attempt count, sent timestamp, and last error are removed from the draft document schema.

`receipt-parser` continues to:

- save the parsed draft document once
- attempt delivery to `discord-api`
- retry immediately in-process on transient failures
- log final delivery failure and throw

It no longer performs a second Cosmos upsert to record `Pending` or `Sent` delivery status.

## Consequences

Positive:

- success path cost is reduced to one large Cosmos write
- document schema is simpler
- write amplification from delivery bookkeeping is removed
- retry behavior remains unchanged for transient HTTP failures

Negative:

- Cosmos no longer contains persisted delivery status
- final failure analysis depends more on logs
- future delivery reprocessing, if needed, must introduce a new explicit persistence design

## Follow-up Notes

- If durable reprocessing becomes necessary later, prefer a dedicated lightweight delivery-tracking model instead of reintroducing large-document double writes.
- This decision is informed by the performance review in `docs/problem-searching/performance-review-2026-04-07.md`.
- This decision changes the practical role of Cosmos for `receipt-parser`: it stores parsed draft content, not delivery lifecycle state.

# 010 - Persist Receipt State Only on Final Confirmation

## Status
Accepted

## Context

After removing the separate `settlement-service`, `discord-api` became responsible for buyer selection and final settlement confirmation.

A design concern remained around how often receipt interaction state should be persisted to Cosmos DB.

Two approaches were considered:

1. persist every buyer-selection change immediately
2. keep intermediate selection state in memory and persist only on final confirmation

This decision affects write volume, concurrency risk, and workflow complexity.

## Options Considered

### Option A - Persist Every Intermediate Change

Advantages:

- interaction state becomes durable earlier
- recovery after process restarts may be easier

Disadvantages:

- many more database writes
- higher chance of concurrent modification issues
- more complexity during rapid multi-user interaction

### Option B - Persist Only on Final Confirmation

Advantages:

- fewer writes to Cosmos DB
- simpler interaction-phase implementation
- lower chance of concurrent document updates during item selection
- clearer final write timing

Disadvantages:

- intermediate state is not durable
- in-memory progress can be lost if the process restarts

## Decision

We will persist receipt settlement data only when the initiating user presses the final `Confirm` button.

During the buyer-selection phase:

- intermediate user selections are managed by `discord-api`
- intermediate changes are not persisted continuously to Cosmos DB
- final confirmation requires server-side validation before persistence

## Consequences

### Positive

- fewer Cosmos DB writes
- simpler implementation in the current project stage
- reduced risk of concurrent document updates during active selection
- clearer ownership of final persistence timing

### Negative

- in-progress selection state is not durable
- process restarts can lose ongoing interaction progress
- final confirmation still requires robust validation

## Follow-up Notes

Confirmation validation should verify at least:

- the requester is allowed to confirm
- the receipt is not already finalized
- all required items have been assigned
- applicable automatic business rules have been resolved

If future requirements demand recovery of in-progress interaction state across restarts or deployments, intermediate persistence can be reconsidered.

# 005 - Prefer Event-Driven Communication Between Services

## Status
Accepted

## Context

Settle Up is designed as a multi-service system. Services need to coordinate to complete the full workflow of receipt processing and settlement.

An early design question was how services should communicate:

1. direct synchronous HTTP between services
2. event-driven communication through an event bus

This decision affects coupling, resilience, extensibility, and operational complexity.

## Options Considered

### Option A - Direct Service-to-Service HTTP

Advantages:

- simpler initial implementation
- easier to debug in very early development
- immediate response from downstream services

Disadvantages:

- stronger coupling between services
- upstream behavior depends directly on downstream availability
- harder to add additional consumers later
- runtime dependencies become tighter

### Option B - Event-Driven Communication

Advantages:

- looser coupling between services
- multiple services can react to the same event
- improved resilience through asynchronous processing
- better alignment with cloud-native event-driven architecture

Disadvantages:

- infrastructure is more complex than pure direct calls
- eventual consistency replaces immediate synchronous completion
- debugging event flows can be harder

## Decision

The preferred architectural direction is event-driven communication between services.

## Consequences

### Positive

- reduced direct coupling between services
- improved extensibility for future consumers
- better alignment with the cloud eventing model already used for Blob events
- clearer domain-event-oriented architecture

### Negative

- event infrastructure and operational tracing become more important
- eventual consistency must be accepted
- debugging can require more tooling and discipline

## Follow-up Notes

This decision expresses the architectural preference, not a rule that every integration must be event-driven immediately.

Later decisions may choose a more pragmatic short-term mechanism for specific flows while the project is still evolving. Those exceptions should be documented explicitly rather than treated as a reversal of this overall direction.

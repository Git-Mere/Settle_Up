# 003 - Use Service-Owned Databases

## Status
Accepted

## Context

Settle Up is designed as a multi-service system with distinct service responsibilities, including parsing, Discord interaction, and future settlement-related capabilities.

An architectural decision was required around persistent storage ownership:

1. all services share one database
2. each service owns its own database and data model

This decision affects service boundaries, coupling, and long-term schema evolution.

## Options Considered

### Option A - Shared Database Across Services

Advantages:

- simpler initial infrastructure
- easier direct querying across domains
- less configuration during early development

Disadvantages:

- strong coupling between services
- services can become dependent on each other’s internal schemas
- schema changes in one domain can break another
- weak alignment with common microservice design practices

### Option B - Service-Owned Databases

Advantages:

- clear data ownership per service
- reduced coupling between services
- each service can evolve its schema independently
- better alignment with service-oriented architecture principles

Disadvantages:

- requires additional infrastructure setup
- data sharing must happen through APIs or events rather than direct queries

## Decision

We will adopt a service-owned database model where each service manages its own database.

## Consequences

### Positive

- clearer service boundaries
- reduced schema coupling between services
- easier long-term evolution of service internals
- better support for independent deployment and change management

### Negative

- cross-service data access becomes more explicit and more complex
- infrastructure setup can increase as more services are added

## Follow-up Notes

Under this model, the parser service owns parsed receipt draft data, while other services should own their own persistence needs rather than relying on direct access to parser-owned storage.

Inter-service communication should happen through contracts such as HTTP or events, not through shared database access.

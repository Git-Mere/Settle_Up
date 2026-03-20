# 001 - Adopt a Monorepo Structure

## Status
Accepted

## Context

Settle Up is being built as a multi-service system rather than a single application.

The repository structure therefore needed to support:

- multiple services evolving together
- shared documentation and design decisions
- shared infrastructure patterns such as Docker, CI, and observability
- future extraction of service-specific deployment and runtime concerns

Two repository organization options were considered:

1. separate repositories per service
2. a single monorepo containing all services

## Options Considered

### Option A - Separate Repositories per Service

Advantages:

- services can be isolated at the repository level
- CI/CD can be fully separated per service
- team ownership boundaries can be strict if teams are organized by service

Disadvantages:

- repository management becomes more complex across services
- shared contracts and common code are harder to evolve together
- early-stage coordination overhead increases

### Option B - Monorepo

Advantages:

- simpler overall repository management
- easier coordination across services during early development
- easier to share infrastructure patterns, documentation, and contracts
- good fit when services are still evolving together

Disadvantages:

- repository history and change scope are shared across services
- stronger discipline is needed to maintain clean service boundaries

## Decision

We will use a monorepo for Settle Up.

## Consequences

### Positive

- simpler project setup and repository management
- easier cross-service coordination during the current project stage
- shared documentation and infrastructure changes are easier to keep aligned
- better support for common code and repository-wide conventions

### Negative

- service boundaries must be maintained intentionally inside one repository
- unrelated changes may coexist in the same working tree
- CI and repository hygiene require discipline as the project grows

## Follow-up Notes

This decision is based on the current project stage, where the services are still being designed and iterated together.

If the project later reaches a scale where service ownership, release cadence, or access control strongly diverge, repository separation can be reconsidered. For now, the monorepo structure provides the best balance of simplicity and coordination.

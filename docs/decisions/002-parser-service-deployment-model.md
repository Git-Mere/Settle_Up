# 002 - Deploy the Parser as a Containerized Service

## Status
Accepted

## Context

The receipt parsing component of Settle Up is responsible for:

- receiving blob creation events from Azure Blob Storage via Event Grid
- invoking Azure Document Intelligence to extract structured data from receipt images
- processing the extracted data
- persisting parsed results

Although the current functionality is primarily event-driven, the parser is expected to grow over time with additional processing logic and operational endpoints.

Two deployment approaches were considered:

1. Azure Functions as a serverless event-driven component
2. a containerized microservice deployed on Azure Container Apps

## Options Considered

### Option A - Azure Function

Advantages:

- native integration with Event Grid triggers
- minimal infrastructure management
- automatic scaling with event volume
- fast to implement for simple event-driven workloads

Disadvantages:

- less suitable if the component grows into a broader service
- harder to expose additional APIs for debugging or manual reprocessing
- more constrained runtime and hosting model
- weaker alignment with the rest of the planned service architecture

### Option B - Containerized Microservice

Advantages:

- full control over runtime and application structure
- easy to expose additional HTTP endpoints
- aligns with the broader multi-service architecture
- easier to extend with more processing flows or operational features
- consistent deployment model with other services such as `discord-api`

Disadvantages:

- requires building and maintaining container images
- slightly more infrastructure setup than a pure serverless function

## Decision

We will implement the parser as a containerized service deployed on Azure Container Apps.

## Consequences

### Positive

- stronger alignment with the project’s service-oriented direction
- easier future expansion for debugging, reprocessing, and operational endpoints
- more control over runtime behavior
- better consistency with other services in the repository

### Negative

- more operational setup than a simple serverless trigger
- container build and deployment must be maintained

## Follow-up Notes

This decision does not remove the event-driven nature of receipt processing. The parser may still be triggered by Event Grid, but it will run as a long-lived service rather than a pure function.

This keeps the architecture flexible while preserving compatibility with future service growth.

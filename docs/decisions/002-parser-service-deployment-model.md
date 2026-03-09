# 002 - Parser Service Deployment Model

## Status
Accepted

## Context
The receipt parsing component of Settle Up is responsible for:

- Receiving blob creation events from Azure Blob Storage via Event Grid
- Invoking Azure Document Intelligence to extract structured data from receipt images
- Processing the extracted data
- Persisting the parsed result to Cosmos DB

There were two possible approaches for implementing this component:

- Using Azure Functions as an event-driven serverless function
- Implementing a containerized microservice deployed on Azure Container Apps

While the current functionality is primarily event-driven, the parser service is expected to grow over time with additional processing logic and operational capabilities.

## Option A: Azure Function

### Advantages
- Native integration with Event Grid triggers
- Minimal infrastructure management
- Automatic scaling with event volume
- Fast to implement for simple event-driven workloads

### Disadvantages
- Less suitable for evolving into a full microservice
- Harder to expose additional APIs for debugging or manual reprocessing
- More limited control over runtime environment
- Architectural mismatch if the system grows into multiple long-running services

## Option B: Container App Microservice

### Advantages
- Full control over the runtime and application architecture
- Easy to expose additional HTTP endpoints (e.g., manual parsing, health checks, debugging)
- Aligns with the microservice architecture used by other services in the system
- Simplifies future expansion such as additional processing pipelines or background workers
- Consistent deployment model with other services (e.g., Discord API service)

### Disadvantages
- Requires building and maintaining container images
- Slightly more infrastructure setup compared to serverless functions

## Decision
We chose to implement the parser as a containerized service deployed on Azure Container Apps.

## Rationale
Although the parser currently operates as an event-driven component, it is expected to evolve into a more complex service that may include additional processing logic, operational endpoints, and integration points.

Deploying the parser as a containerized microservice provides greater architectural flexibility and maintains consistency with the overall service-oriented design of the system.

This approach also allows the service to scale independently while keeping the infrastructure model consistent across services.
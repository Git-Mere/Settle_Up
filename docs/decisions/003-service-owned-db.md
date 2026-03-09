# 003 - Service-Owned Databases

## Status
Accepted

## Context
Settle Up is designed as a multi-service system consisting of several independent services such as:

- Discord Bot Service
- Receipt Parser Service
- Settlement Service

Each service has a clearly defined responsibility. The Receipt Parser Service processes uploaded receipt images, invokes Azure Document Intelligence to extract structured data, and produces a parsed receipt draft. The Settlement Service calculates final payment balances after users confirm item ownership.

During the design process, we considered how these services should interact with persistent storage.

The main architectural question was whether all services should share a single database or whether each service should manage its own database.

## Option A: Shared Database Across Services

### Advantages
- Simpler infrastructure with a single database instance
- Easier to implement queries across different domains
- Less configuration required during early development

### Disadvantages
- Strong coupling between services through shared data structures
- Services may accidentally depend on each other's internal schemas
- Changes in one service’s data model may break another service
- Violates common microservice design practices where services own their data

## Option B: Service-Owned Databases

### Advantages
- Clear data ownership boundaries for each service
- Reduces coupling between services
- Each service can evolve its schema independently
- Aligns with common microservice architecture practices

### Disadvantages
- Requires additional infrastructure configuration
- Data sharing between services must be done through APIs or events instead of direct queries

## Decision
We chose to adopt a service-owned database model where each service manages its own database.

## Rationale
This approach aligns with the architectural goal of maintaining clear service boundaries and minimizing coupling between services.

The Receipt Parser Service will store parsed receipt drafts and intermediate receipt states in its own database. The Settlement Service will maintain its own database to store final settlement results and calculation history.

Services communicate through APIs or events rather than direct database access, ensuring that each service remains responsible for its own data model and persistence layer.
# 005 - Event-Driven Service Communication

## Status
Accepted

## Context
Settle Up is designed as a multi-service system consisting of several independent services, including:

- Discord Bot Service
- Receipt Parser Service
- Settlement Service

These services must communicate in order to complete the full workflow of receipt processing and bill settlement.

Initially, we considered allowing services to communicate directly through synchronous HTTP requests. However, this approach would introduce tight coupling between services and create dependencies on service availability during runtime.

Since the system already uses Azure Event Grid to react to Blob Storage events, we considered extending the architecture to use event-driven communication between internal services as well.

## Option A: Direct Service-to-Service Communication (HTTP)

### Advantages
- Simpler implementation
- Easier to debug during early development
- Immediate response from downstream services

### Disadvantages
- Strong coupling between services
- Service failures propagate upstream
- Reduced flexibility for adding additional consumers
- Harder to scale or extend the system in the future

## Option B: Event-Driven Communication via Event Bus

### Advantages
- Loose coupling between services
- Services communicate through events rather than direct dependencies
- Multiple services can subscribe to the same event
- Improved resilience since services process events independently
- Aligns well with cloud-native event-driven architectures

### Disadvantages
- Slightly more complex infrastructure setup
- Eventual consistency instead of immediate synchronous responses
- Debugging event flows can be more complex

## Decision
We chose to adopt an event-driven communication model between services.

## Rationale
The system already uses Azure Event Grid to trigger the Receipt Parser Service when a receipt image is uploaded to Blob Storage. Extending this event-driven pattern to internal service communication creates a more consistent architecture.

After the parser service successfully processes a receipt, it will publish a domain event indicating that the receipt has been parsed. Other services, such as the Discord Bot Service or Settlement Service, can subscribe to these events and react accordingly.

This approach keeps services loosely coupled and allows the system to evolve without introducing direct dependencies between services.

For example, the Receipt Parser Service may emit an event such as `ReceiptParsed`. The Discord Bot Service can subscribe to this event to notify users, while the Settlement Service may begin preparing the settlement workflow.

Using events rather than direct service calls ensures that each service remains responsible for its own domain while still enabling coordination across the system.
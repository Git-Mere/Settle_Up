# 008 - Validate Payloads at the `/getting_draft` Boundary

## Status
Accepted

## Context

The `discord-api` service exposes the `/getting_draft` endpoint to receive structured draft receipt data from internal components.

During the current project stage, the request payload structure is still evolving. This increases the risk of malformed, incomplete, or inconsistent requests during development and integration.

A decision was required on how strictly the endpoint should validate incoming data:

1. accept payloads with minimal validation and fail later if needed
2. validate payloads at the API boundary before processing

## Options Considered

### Option A - Minimal Validation

Advantages:

- simpler initial implementation
- faster early iteration

Disadvantages:

- malformed requests fail deeper in the system
- debugging becomes harder
- inconsistent data can propagate into downstream processing or storage

### Option B - Validate at the API Boundary

Advantages:

- invalid requests are rejected immediately
- debugging becomes easier during integration
- service contracts become clearer
- malformed data is prevented from moving deeper into the system

Disadvantages:

- validation logic requires maintenance as the schema evolves
- implementation is slightly more complex

## Decision

We will validate `/getting_draft` payloads at the API boundary before processing them.

If validation fails:

- the endpoint will return `400 Bad Request`
- the request will not continue through the workflow
- a validation failure will be logged

Authentication is not part of this decision and may be added later.

## Consequences

### Positive

- invalid requests are rejected early
- integration debugging becomes easier
- malformed data is less likely to reach storage or downstream logic
- the endpoint contract becomes clearer

### Negative

- validation rules must evolve with the payload schema
- additional implementation complexity is introduced

## Follow-up Notes

Future improvements may include:

- authentication or service identity verification
- formal schema definition through OpenAPI or JSON Schema
- shared validation helpers across service boundaries

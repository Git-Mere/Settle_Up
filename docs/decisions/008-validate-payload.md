# 008 - Validate Payload for the `/getting_draft` Endpoint

## Status
Accepted

## Context

The `discord-api` service exposes the `/getting_draft` endpoint, which is used to receive structured data from other internal components of the Settle Up system.

At this stage of development, the system architecture is still evolving, and the request payload structure exchanged between services is not yet fully stabilized. Because of this, malformed or incomplete requests may occur during development or integration between services.

We therefore needed to decide how strictly incoming requests should be validated.

Two main options were considered:

1. Accept incoming payloads without strict validation and rely on downstream services to handle errors
2. Validate the payload at the API boundary before processing the request

This decision affects system reliability, debugging efficiency, and the clarity of service contracts between components.

## Options Considered

### Option 1 — No Payload Validation

The API would accept incoming JSON payloads without checking whether required fields exist or whether field types are correct.

Processing would continue until a failure occurs deeper in the system (for example, during database operations or message formatting).

Advantages:

- Simpler initial implementation
- Faster early development

Disadvantages:

- Errors surface later in the processing pipeline
- Harder debugging when malformed requests propagate through the system
- Increased risk of inconsistent data reaching downstream services or storage

---

### Option 2 — Validate Payload at the API Boundary

The `/getting_draft` endpoint validates incoming payloads before processing them.

Validation includes checks such as:

- Required fields must exist
- Field types must match the expected schema
- Strings must not be empty
- URLs or identifiers must follow a basic valid format
- Arrays and objects must have valid structure

If validation fails, the API returns a **400 Bad Request** response and the request is not processed further.

Advantages:

- Invalid requests are rejected immediately
- Easier debugging during development
- Clearer service contract between components
- Prevents malformed data from reaching databases or other services

Disadvantages:

- Slightly more implementation work
- Validation rules must be updated if the payload schema changes

---

## Decision

We decided to **implement payload validation for the `/getting_draft` endpoint**.

Incoming requests will be validated at the API boundary to ensure that the payload structure matches the expected schema before any further processing occurs.

If validation fails:

- The request will return **HTTP 400 Bad Request**
- A validation error will be logged for debugging purposes

At the current stage of development, **authentication will not be added yet**, as the endpoint is primarily used for internal service communication and development simplicity is preferred.

---

## Consequences

### Positive

- Invalid requests are rejected early in the request lifecycle
- Debugging becomes easier when integration issues occur
- Prevents malformed or incomplete data from propagating through the system
- Establishes a clearer API contract between services

### Negative

- Payload validation rules must be maintained as the schema evolves
- Some additional implementation complexity is introduced
- Overly strict validation could temporarily slow development if the schema changes frequently

---

## Future Considerations

In future iterations of the system, we may:

- Add authentication or service identity verification between services
- Introduce a formal schema definition (e.g., OpenAPI or JSON Schema)
- Apply validation middleware or shared DTO validation logic across services
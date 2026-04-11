# 004 - Limit Parser Data to Parsed Receipt Content

## Status
Accepted

## Context

The receipt parser is responsible for analyzing uploaded receipts and producing structured receipt data.

A design question arose around whether the parser-owned data should also include user-driven assignment information, such as:

- who purchased an item
- who shared an item
- payer or participant details

This decision affects service responsibility boundaries and the mutability of parser-owned data.

## Options Considered

### Option A - Store User Assignment Data in the Parser Database

Advantages:

- all receipt-related information can live in one document
- fewer services are involved in the overall receipt flow
- some queries may become simpler

Disadvantages:

- parser ownership becomes mixed with settlement and interaction concerns
- user-driven workflow state enters parser-owned data
- parser documents become more mutable and less stable
- service boundaries become blurred

### Option B - Keep Parser Data Focused on Parsed Receipt Content

Advantages:

- clearer separation between parsing and settlement responsibilities
- parser data remains focused on objective document interpretation
- parser documents remain more stable and easier to reason about
- better alignment with service-owned data principles

Disadvantages:

- downstream services must carry user interaction and settlement state
- coordination is needed through identifiers and contracts

## Decision

The parser database will store parsed receipt data only. User assignment and settlement interaction state will be handled outside the parser domain.

## Consequences

### Positive

- clearer service responsibility boundaries
- parser documents remain closer to immutable parsed facts
- user-driven workflow logic stays out of the parser service

### Negative

- downstream services must manage more interaction-specific state
- cross-service coordination remains necessary

## Follow-up Notes

The parser should continue to focus on extracted receipt fields and normalized receipt structure.

If future requirements introduce richer workflow recovery or audit needs, downstream documents may reference parser-owned receipt identifiers rather than expanding parser ownership to include settlement interaction state.

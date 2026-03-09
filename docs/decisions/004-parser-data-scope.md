# 004 - Parser Data Scope and Settlement Responsibility

## Status
Accepted

## Context
The Settle Up system processes receipt images uploaded by users and extracts structured data using Azure Document Intelligence. The Receipt Parser Service is responsible for analyzing the receipt image and producing a parsed representation of the receipt.

During the design process, we considered whether the parsed receipt draft stored in the parser database should also include user-assigned ownership information such as which user purchased or consumed each item.

This raised an architectural question regarding service responsibilities and data ownership: should the parser service store user assignment data (e.g., buyer or participants), or should that responsibility belong to another service?

## Option A: Store Buyer and Participant Information in Parser Database

### Advantages
- All receipt-related information is stored in a single document
- Simplifies queries if both parsing data and assignment data are needed together
- Fewer services involved in managing receipt data

### Disadvantages
- Blurs the responsibility boundary between parsing and settlement logic
- Introduces user-driven state changes into parser-owned data
- Parser documents become mutable and tied to interaction workflows
- Violates the principle of keeping service domains focused

## Option B: Keep Parser Data Immutable and Move Assignment Logic to Settlement Service

### Advantages
- Maintains a clear separation of concerns between services
- Parser service stores only objective data extracted from the receipt
- User-driven interactions and assignment logic remain in the settlement domain
- Parser documents remain mostly immutable and easier to reason about
- Aligns with service-owned database principles

### Disadvantages
- Requires coordination between parser and settlement services
- Settlement service must reference receipt data through receipt identifiers

## Decision
We chose to keep the parser database limited to storing parsed receipt data only. User assignment information such as item ownership, participants, or payer details will be managed by the Settlement Service.

## Rationale
The parser service is responsible solely for extracting structured data from receipt images. The information it produces represents the system’s interpretation of the document and should remain stable after parsing.

User interactions, such as selecting who purchased or shared specific items, belong to the settlement domain rather than the parsing domain. By moving this logic to the Settlement Service, we maintain clear service boundaries and keep parser documents focused on parsed receipt data.

This design also ensures that the parser database remains mostly immutable, while the settlement service manages dynamic user-driven state changes.
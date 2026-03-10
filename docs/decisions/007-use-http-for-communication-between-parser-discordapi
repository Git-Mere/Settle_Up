# 007 - Use HTTP for Communication Between Receipt Parser and Discord API

## Status
Accepted

## Context

In the Settle Up system, the `receipt-parser` service processes receipt images uploaded by users.  
Once a receipt is parsed successfully, the system needs to notify the `discord-api` service so that the parsed receipt information can be sent back to Discord.

At this stage of the project, we need to choose how the `receipt-parser` service communicates the parsing result to the `discord-api` service.

Two main options were considered:

1. Direct HTTP communication between services
2. Event-driven communication (e.g., publishing an event and letting `discord-api` subscribe)

The decision impacts system complexity, coupling, debugging workflow, and future extensibility.

## Options Considered

### Option 1 — HTTP Request (Synchronous Service Call)

Flow:

1. User uploads receipt through Discord bot
2. Image is stored in Blob Storage
3. Event Grid triggers `receipt-parser`
4. `receipt-parser` extracts structured data from the receipt
5. `receipt-parser` sends the result to `discord-api` via HTTP
6. `discord-api` sends the formatted message back to Discord

Example interaction:

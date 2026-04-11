# 016 - Define Confirmed Settlement History Document And History Query Policy

## Status
Accepted

## Context
`discord-api` plans to persist settlement results at confirm time and later expose them through the `/history` command.

This data serves a different purpose from the draft parse documents stored by `receipt-parser`.

- parser documents are internal service data for OCR, normalization, and delivery state
- history documents are user-facing snapshots used to show final settlement results again after confirm

That means the history document should not mirror parser-internal raw structure. It should instead be a compact read model tailored to `discord-api` confirm UI and history lookup needs.

The first intended `/history` scenario is “show the settlement results this user confirmed in the past.” Because of that, the storage structure and partition key also needed to match that lookup pattern.

## Options Considered
1. store a detailed history document close to the parser draft structure
- some fields might be easy to reuse
- but it stores much more than `/history` actually needs
- it remains overly tied to parser-internal representation instead of confirmed user-facing results

2. store a snapshot document with only the key fields needed for confirm results
- history stores only what `/history` actually needs
- the read model can stay very close to the confirm UI shape
- it can evolve independently from the parser draft schema

3. design `/history` primarily around guild-wide browsing and use `guildId` as the main partition key
- may help for server-level exploration later
- but it is less direct for the current first requirement of “show the results this user created”

4. design `/history` around uploader-centric lookup and use `uploadedByUserId` as the partition key
- directly matches the current requirement
- makes it easy to retrieve one user's past results in recency order
- if guild-wide browsing is needed later, that can be handled through a separate query/index strategy

## Decision
Confirmed settlement history will be stored as a **snapshot document containing the key settlement information at confirm time**.

The document shape follows these principles.

- store only the key result of the confirm state
- do not copy parser raw results or parser-internal draft representation as-is
- store the values that need to be shown to users again in a shape similar to the current confirm public message
- include participant-level final amounts and participant item summaries

The recommended document shape is as follows.

```json
{
  "id": "history_01JXYZ...",
  "type": "confirmed_settlement",
  "receiptId": "receipt_abc123",
  "guildId": "123456789012345678",
  "channelId": "123456789012345679",
  "messageId": "123456789012345680",

  "uploadedByUserId": "111111111111111111",
  "uploadedByDisplayName": "mere",
  "merchantName": "Sunset Diner",
  "transactionDate": "2026-03-12",
  "currency": "USD",

  "subtotal": 42.00,
  "tax": 3.36,
  "sst": null,
  "slt": null,
  "tip": 8.40,
  "total": 53.76,
  "tipSplitMode": "Proportional",

  "paymentContact": "zelle@example.com",

  "participants": [
    {
      "userId": "111111111111111111",
      "displayName": "mere",
      "amount": 18.20,
      "taxAmount": 1.44,
      "sstAmount": 0.00,
      "sltAmount": 0.00,
      "tipAmount": 3.20,
      "items": [
        { "name": "Burger", "quantity": 1, "amount": 14.00, "isAlcohol": false },
        { "name": "Fries", "quantity": 1, "amount": 5.00, "isAlcohol": false }
      ]
    }
  ],

  "confirmedAtUtc": "2026-03-12T21:15:00Z",
  "createdAtUtc": "2026-03-12T21:15:00Z",
  "updatedAtUtc": "2026-03-12T21:15:00Z"
}
```

The UI/query policy is defined as follows.

- at the current stage, `/history` retrieves only settlement results confirmed by the current user
- the default lookup filter is therefore `uploadedByUserId == currentUserId`
- history rendering should use a structure similar to the current confirm public message
- `SST`, `SLT`, and `Tip` should be included only when those values exist
- when those values are absent, they should be hidden just like in the current confirm UI

The partition key is **`/uploadedByUserId`**.

This best matches the current primary `/history` usage pattern: “show my previously confirmed settlement results.”

## Consequences
Positive outcomes:

- history rendering is simpler because the document shape is close to the confirm UI
- history is less affected by parser-internal schema changes because it is stored as a separate snapshot
- the partition key directly matches the first user experience for `/history`
- the current UI rule of showing `SST`, `SLT`, and `Tip` only when present remains consistent across storage and retrieval

Negative outcomes and costs:

- guild-wide history browsing is not the first optimization target in the current structure
- additional code is needed to create and persist a confirm-time snapshot
- if confirm UI and history UI evolve together, snapshot fields and rendering policy must be kept in sync

## Follow-up Notes
- this decision should be read together with `015-store-confirmed-settlement-history-in-a-discord-api-owned-cosmos-container.md`
- it assumes that the same Cosmos account and database are reused, while the history container itself is separately owned by `discord-api`
- when storing history, `discord-api` must generate the participant item summaries needed for confirm-style rendering
- the initial `/history` implementation should start with uploader-based list lookup plus single-result detail rendering
- if guild-wide history, search, or filters are needed later, a new query/index decision can be added separately

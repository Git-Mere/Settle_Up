# 015 - Store Confirmed Settlement History In A Discord-Api-Owned Cosmos Container

## Status
Accepted

## Context
`receipt-parser` currently stores parsed receipt drafts in Cosmos DB. That data is internal parser-owned data used to manage OCR and normalization output and to deliver drafts to `discord-api`.

`discord-api` now needs to persist the key settlement result at confirm time. That data will later be used by the `/history` command to retrieve past settlement results.

This persisted data should be much smaller and more focused on confirmed settlement results than the draft parse documents stored by `receipt-parser`.

In other words, the data to be stored now has the following characteristics.

- it is not an intermediate parse artifact
- it is a user-facing settlement snapshot after confirm
- it is a read model for Discord UI and history lookup
- the owning service is `discord-api`, not `receipt-parser`

Earlier decision [003-service-owned-db.md](/home/aero-mere/CS397/Settle_Up/docs/decisions/003-service-owned-db.md) already adopted the following principle.

- `We will adopt a service-owned database model where each service manages its own database.`
- `the parser service owns parsed receipt draft data, while other services should own their own persistence needs rather than relying on direct access to parser-owned storage.`

That means the storage location for confirmed history also needed to be evaluated through the same service-ownership lens.

## Options Considered
1. store confirmed history in the same container currently used by `receipt-parser`
- likely the fastest initial implementation
- but draft parser data and confirmed settlement history would be mixed in one container
- data ownership and schema responsibility would be mixed, weakening the service boundary
- `/history` lookup would risk becoming dependent on parser-internal document shape

2. reuse the same Cosmos account and same database, but add a `discord-api`-owned container
- separates ownership without significantly increasing infrastructure
- parser and discord-api can share the account and database while still keeping container-level boundaries
- history lookup can be modeled independently from parser draft documents
- at the current stage, this is the best balance between operational simplicity and service-separation principles

3. add both a separate database and a separate container for `discord-api`
- database-level separation gives a clearer boundary
- but at the current stage, a separate container inside the same database is sufficient
- adds somewhat more operational and configuration complexity

4. add a completely separate Cosmos account for `discord-api`
- useful if security, cost, networking, or backup policies must be strongly isolated
- but infrastructure and operational cost are too high for the current stage
- the history snapshot is important, but not yet large enough or isolated enough to justify a separate account

## Decision
After confirm, settlement history will be stored in a **new `discord-api`-owned container while reusing the existing Cosmos account and existing database**.

The concrete direction is as follows.

- the existing draft container continues to be owned by `receipt-parser`
- `discord-api` uses its own history container within the same Cosmos account and database
- confirmed settlement history is not stored in the parser container
- `/history` runs against documents owned by `discord-api`

This choice is intended to balance the following.

- keep initial operational complexity low by not adding a whole new account or database
- avoid breaking the service boundary by reusing the parser container
- design the history lookup model specifically for `discord-api` needs

## Consequences
Positive outcomes:

- ownership is separated between `receipt-parser` draft data and `discord-api` confirmed history data
- the `/history` feature does not depend on parser-internal schema
- reusing the same account and database keeps initial deployment and operations relatively small
- indexing, TTL, partition key, and RU strategy can be tuned per container for history usage

Negative outcomes and costs:

- a new container still has to be managed even inside the same database
- `discord-api` needs its own Cosmos configuration and persistence code
- because the account is shared, the infrastructure boundary is not fully isolated

## Follow-up Notes
- history documents should be simpler confirm snapshots than parser draft documents
- expected stored fields include `receiptId`, owner/user identifiers, merchant name, confirm timestamp, money summary, participant totals, and participant item summaries needed for `/history`
- partition key and query strategy can be refined further around the actual `/history` UX
- even while reusing the same database, code and documentation should remain explicit that the history container is owned by `discord-api`
- if security, cost, or operational requirements grow later, a separate Cosmos account can be revisited in a new decision

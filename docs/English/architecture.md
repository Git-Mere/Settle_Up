# Architecture

Settle Up is a multi-service mono-repo focused on receipt-based expense settlement.

The two services that currently form the working core of the system are:

- `discord-api`
- `receipt-parser`

This document summarizes the current system structure, responsibility boundaries, runtime flows, and the design direction currently in use.

## Architecture Summary

The current system is centered on the following flow.

1. a user uploads a receipt through Discord
2. `discord-api` stores the image in Blob Storage
3. the Blob-created event is delivered to `receipt-parser`
4. `receipt-parser` parses the image with Document Intelligence
5. `receipt-parser` stores the draft in Cosmos DB
6. `receipt-parser` sends an HTTP callback to `discord-api`
7. `discord-api` creates or refreshes the public settlement UI
8. users assign items and confirm
9. `discord-api` stores confirmed history in Cosmos DB

## Repository Structure

Current main folders:

```text
/
├─ docs/
│  ├─ decisions/
│  ├─ api.md
│  ├─ architecture.md
│  └─ ci-cd.md
├─ shared/
│  └─ SettleUp.Observability/
└─ services/
   ├─ discord-api/
   └─ receipt-parser/
```

The following services may be added later.

- `settlement-service`
- `user-service`
- `export-service`

These are still future design candidates. The current user-facing core flow is handled by `discord-api` and `receipt-parser`.

## Service Responsibilities

### `discord-api`

Primary responsibilities:

- connect to the Discord bot gateway
- handle slash commands, buttons, and modals
- provide the receipt upload entrypoint
- receive parser callbacks
- render the public receipt UI
- handle item selection, add, remove, edit, and confirm actions
- persist and query history

Current characteristics:

- runs the Discord worker and the HTTP receiver in one process
- uses a single public main message that gets updated in place
- combines private panels with a public message
- uses session-scoped in-memory state

### `receipt-parser`

Primary responsibilities:

- receive the Event Grid webhook
- download Blob images
- parse receipts with Document Intelligence
- store normalized draft documents
- send draft callbacks to `discord-api`

Current characteristics:

- stores parsing results and delivery-related state in Cosmos
- includes a retryable callback policy
- treats `uploadedByUserId` extraction as a critical contract point

### `shared/SettleUp.Observability`

Primary responsibilities:

- common logging bootstrap
- OpenTelemetry configuration
- Azure Monitor / Application Insights exporter setup

Current direction:

- reuse shared observability bootstrap instead of duplicating setup code in each service

## Runtime Architecture

### Receipt Upload and Parsing Flow

```text
Discord User
  -> discord-api (/settle-up)
  -> Azure Blob Storage
  -> Event Grid
  -> receipt-parser
  -> Azure Document Intelligence
  -> Cosmos DB (draft receipt)
  -> discord-api (/getting_draft)
  -> Discord public settlement UI
```

### Manual Settlement Flow

```text
Discord User
  -> discord-api (/custom)
  -> in-memory blank receipt session
  -> Discord public settlement UI
  -> owner adds items manually
  -> confirm
  -> Cosmos DB (confirmed history)
```

### History Flow

```text
Discord User
  -> discord-api (/history or /history index:n)
  -> Cosmos DB (confirmed history container)
  -> ephemeral history response
```

## State Model

### In-Progress Receipt State

In-progress receipt state currently lives in the `discord-api` in-memory session layer.

It includes:

- receipt header data
- parsed items
- user selections
- public message metadata
- render cache
- pending edit tokens
- owner language for the public UI

This state is not preserved across process restarts.

### Confirmed History State

After confirm, a snapshot is saved to Cosmos DB.

Policy:

- update the confirm UI first
- persist history in the background with retry
- show an ephemeral failure notice if the save ultimately fails

Related decision:

- `docs/decisions/017`

## Discord UI Architecture

The current receipt UI follows these principles.

- keep one public main message as the center of the flow
- perform routine interactions through private panels
- refresh the public message after debounce
- switch to the confirmed embed immediately on confirm

Main buttons:

- `Select Item`
- `Add Item`
- `Remove Item`
- `Edit Item`
- `Mark Alcohol`
- `Confirm`
- `Cancel`

Permission policy:

- `Select Item` is available to all participants
- `Add/Remove/Edit/Mark Alcohol/Confirm/Cancel` are owner-only

Important constraints:

- Discord public messages cannot present different disabled button states to different users
- therefore non-owners can still click owner-only buttons, and the server blocks them
- the public message language is also single-owner-based, not user-specific

## Tax and Money Model

The current money model includes:

- `Subtotal`
- `Tax`
- `SST`
- `SLT`
- `Tip`
- `Total`

Additional policies:

- discounts are handled as item-level discounts when possible
- unmatched discounts are not applied automatically
- `KRW` receipts treat general tax as tax-included and exclude it from calculation and display

Related decisions:

- `docs/decisions/014`
- `docs/decisions/019`
- `docs/decisions/022`

## Localization Model

Localization is currently handled inside `discord-api`.

Policy:

- user language preference is memory-based
- supported languages are English and Korean
- private, ephemeral, and history responses use the caller's language
- the public receipt main message uses the owner's language
- slash command descriptions remain in simple English
- logs and exception messages remain in English

Related decision:

- `docs/decisions/020`

## Integration and Trust Boundaries

The current main boundaries are:

- Discord <-> `discord-api`
- `discord-api` <-> Azure Blob Storage
- Blob Storage/Event Grid <-> `receipt-parser`
- `receipt-parser` <-> Document Intelligence
- `receipt-parser` <-> Cosmos DB
- `receipt-parser` <-> `discord-api`
- `discord-api` <-> Cosmos DB

Security-sensitive points include:

- Event Grid payload validation
- Blob URL parsing
- `receipt-parser -> discord-api` callback validation
- Azure secret and configuration management

Secrets are currently environment-variable-based. In Azure, Container App environment variables combined with Key Vault references can be used.

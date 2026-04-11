# API

Settle Up currently operates around two core service APIs.

- `discord-api`
- `receipt-parser`

This document summarizes the HTTP endpoints that are actually implemented today, the Discord interaction entrypoints, and the current service-to-service contract.

## Scope

This document currently covers the following areas.

- `discord-api` HTTP callback endpoint
- `receipt-parser` Event Grid webhook endpoint
- `receipt-parser` local test endpoint
- Discord slash command entrypoints
- `receipt-parser -> discord-api` draft payload contract

Potential future APIs for user services, a settlement service, or an export service are not yet part of this document.

## `discord-api`

### HTTP Endpoint

#### `POST /getting_draft`

This is the internal callback endpoint through which `receipt-parser` sends a parsed draft receipt after parsing completes.

Primary responsibilities:

- payload validation
- owner verification through `uploadedByUserId`
- pending session creation or update of an existing session
- public receipt message creation or refresh

Success response:

```json
{
  "message": "draft received"
}
```

Current default listen address:

- `http://0.0.0.0:5000`

Environment variable:

- `ASPNETCORE_URLS`

### Discord Slash Commands

The current main commands are as follows.

#### `/settle-up`

Starts receipt image upload.

Flow:

1. execute slash command
2. return an ephemeral button response
3. open an upload modal when the button is clicked
4. upload a file
5. store the file in Blob Storage
6. create a public pending message
7. switch to the public check message after the parser callback arrives

#### `/custom`

Creates a blank receipt session immediately without using the parser.

Main characteristics:

- `Seller Name = Custom`
- `Purchase Date = date at command execution time`
- `Buyer Name = command invoker`
- all money fields start at `0`
- the owner fills in items later through `Add item` and related actions

Optional option:

- `payment_contact`

#### `/history`

Retrieves settlement history previously confirmed by the current user as owner.

Behavior:

- `/history`: retrieves the recent history list
- `/history index:<number>`: retrieves the details of the `n`th most recent history entry at the current point in time

Policy:

- `index:1` means the most recent confirmed result at the current point in time
- the service does not keep a list cache and re-queries using the current sort order

#### `/language`

Sets the user's UI language.

Supported languages:

- `English`
- `Korean`

Policy:

- private, ephemeral, and history responses use the caller's language
- the public receipt main message uses the owner's language
- the setting is kept in memory and resets on restart

#### Debug Commands

These are registered only in the Development environment.

- `/pingtest`
- `/test`

Example `/test` scenarios:

- general draft
- liquor tax draft
- restaurant tip draft
- discount draft
- stacked discount draft

## `receipt-parser`

### HTTP Endpoints

#### `POST /api/events/blob-created`

This is the production endpoint through which Azure Event Grid delivers Blob-created events.

Primary responsibilities:

- process Event Grid subscription validation
- parse Blob-created events
- extract `uploadedByUserId` from the Blob URL
- execute Document Intelligence `prebuilt-receipt`
- upsert the Cosmos draft document
- call back to `discord-api /getting_draft`

#### `POST /api/tests/local-upload-parse`

This is a local test helper endpoint.

Activation condition:

- `ReceiptParser__EnableLocalUploadTestEndpoint=true`

Primary responsibilities:

- receive an uploaded file
- call Document Intelligence
- save to Cosmos
- send a local callback to `discord-api`

## Service-to-Service Contract

### Draft Notification Payload

The current `receipt-parser -> discord-api` payload includes the following categories of fields.

Core identifiers:

- `id`
- `status`
- `blobUrl`
- `uploadedByUserId`

Receipt header:

- `merchantName`
- `transactionDate`
- `currency`
- `subtotal`
- `tax`
- `sst`
- `slt`
- `tip`
- `total`

Items:

- `id`
- `description`
- `quantity`
- `unitPrice`
- `totalPrice`
- `originalUnitPrice`
- `originalTotalPrice`
- `discountAmount`

Metadata:

- `parseMetadata.modelId`
- `parseMetadata.merchantConfidence`
- `parseMetadata.totalConfidence`
- `createdAtUtc`
- `updatedAtUtc`

### Contract Notes

- `uploadedByUserId` is effectively required.
- The current owner permission model and draft session creation both depend on this value.
- If the Blob URL pattern changes, the parser extraction rule and `discord-api` validation must be reviewed together.

## Current Data Handling Rules

### Discount Handling

Current discount-line policy:

- negative amount lines are not sent as normal items in the draft
- the parser first attributes them to the immediately preceding normal item as a discount
- successfully matched discounts are sent as item-level discounts
- unmatched discounts are not applied automatically

Related decision:

- `docs/decisions/019`

### KRW Tax Handling

Current `discord-api` policy:

- when `Currency == KRW`, general `Tax` is treated as tax-included and normalized to `0`
- as a result, general tax is not added again to the final amount for Korean receipts
- the tax header and tax section are also hidden

Related decision:

- `docs/decisions/022`

## Authentication and Trust Boundary

In the current implementation:

- Discord interactions are received through the Discord gateway/session model
- the `receipt-parser -> discord-api` callback is currently HTTP-based
- callback authentication and signature validation are still a hardening target

In other words, the production flow works today, but service-to-service trust hardening remains follow-up work.

## Related Documents

- `docs/architecture.md`
- `docs/ci-cd.md`
- `docs/decisions/007-use-http-for-communication-between-parser-discordapi.md`
- `docs/decisions/019-attribute-negative-receipt-lines-to-the-previous-item-and-ignore-unmatched-discounts.md`
- `docs/decisions/020-use-in-memory-user-language-preferences-with-owner-language-for-public-receipt-messages.md`
- `docs/decisions/021-add-a-custom-manual-settlement-entrypoint-with-a-blank-receipt-session.md`
- `docs/decisions/022-treat-general-tax-on-krw-receipts-as-tax-included.md`

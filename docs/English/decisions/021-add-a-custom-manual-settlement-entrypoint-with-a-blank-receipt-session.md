# 021 - Add A Custom Manual Settlement Entrypoint With A Blank Receipt Session

## Status
Accepted

## Context
The default settlement entrypoint in `discord-api` currently starts from receipt image upload and the parser callback.

In real usage, however, there are also cases such as:

- wanting to start settlement immediately without a receipt image
- wanting to add items manually without waiting for OCR results
- wanting to begin a manual settlement flow regardless of parser quality

Because the existing UI already supports add/remove/edit/select/confirm, the project could reuse the same settlement UX without the parser if it could create a blank receipt session.

The core feature needed here was a new entrypoint that can produce a public check message without receipt parsing and let the owner fill it in manually.

## Options Considered
1. keep only `/settle-up` and do not support manual settlement
- keeps the flow simpler
- but cannot support receipt-less settlement, OCR bypass, or simple shared-cost scenarios

2. add `/custom` to create a blank draft and reuse the existing receipt UI
- reuses add/remove/edit/select/confirm as-is
- provides the same settlement UX without the parser
- remains simple as long as initial values and confirm conditions are clearly defined

3. build a completely separate UI specifically for `/custom`
- could tailor the UX for manual settlement in more detail
- but duplicates the existing receipt UI and increases maintenance cost

## Decision
Add the `/custom` slash command so that a blank receipt session can be created immediately without the parser.

Initial session values:

- `Seller Name`: `Custom`
- `Purchase Date`: the date at command execution time
- `Buyer Name`: the command invoker
- `Item Total Price`, `Tax`, `Tip`, `SST`, `SLT`, `Total Price`: all start at `0`
- item list: empty

Command-option policy:

- `payment_contact` is accepted as an optional string option
- if present, it is shown later in the confirm UI as `Pay to`
- if absent, it stays empty

Behavior policy:

- create the public check message immediately after `/custom`
- the owner then fills the session using existing receipt actions such as `Add item`
- confirm is not allowed while the session is empty
- confirm becomes available only when at least one item exists and all items are assigned under the existing rules
- the current `/language` policy applies unchanged

## Consequences
Positive outcomes:

- settlement can begin immediately without the parser
- supports receipt-less cost sharing and OCR bypass scenarios
- reuses the current receipt UI and interaction logic, keeping implementation scope smaller

Negative outcomes and costs:

- until confirm, the state exists only in memory, just like a normal receipt session
- in-progress `/custom` sessions are lost if the bot restarts
- unlike parser-based drafts, the owner must input items manually

## Follow-up Notes
- confirmed `/custom` results use the same history persistence path as normal receipts
- the current decision adds only the manual-settlement entrypoint, not a separate persistence path for custom drafts
- seller name, default currency, and initial tax/tip editing UX can be adjusted later if needed

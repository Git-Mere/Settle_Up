# 022 - Treat General Tax On KRW Receipts As Tax Included

## Status
Accepted

## Context
After receiving a draft receipt, `discord-api` recalculates final settlement amounts from the sum of item amounts and tax/tip values.

The current general policy is:

- use item amounts as subtotal
- allocate general tax proportionally by item purchase share
- treat tip and special taxes such as `SST` and `SLT` as separate values

That policy works for receipts such as U.S. receipts where tax is shown separately from item prices, but it does not fit Korean receipts well.

In Korean receipts, product prices often already include VAT. When OCR still extracts a separate `Tax` or `TaxDetails` value, the current UI path creates the following issue.

- item amounts already include tax
- `discord-api` adds general tax again when computing the total
- participants end up paying tax twice

This problem cannot be solved by hiding the tax UI alone. If tax is still included in the calculation path, settlement amounts remain distorted.

## Options Considered
1. keep one general-tax policy for all countries
- simplest implementation
- but causes double taxation on Korean receipts

2. hide the tax UI for Korean receipts but keep the calculation
- may look less strange visually
- but the actual settlement amount is still wrong

3. have the parser send `CountryRegion` or another explicit tax-policy field and decide from that
- the clearest long-term structure
- but too large a payload and contract change for the immediate fix needed now

4. treat `KRW` receipts as tax-included and normalize `Tax=0` in `discord-api`
- can be applied immediately using the current parser payload
- fixes both calculation and UI together
- simplifies tax policy by relying on currency as a proxy

## Decision
For now, treat general `Tax` as tax-included when `Currency == KRW`.

Application method:

- normalize general `Tax` to `0` when creating a session from a draft payload
- apply the same normalization when reapplying a draft payload to an existing pending session
- do not automatically change `SST`, `SLT`, or `Tip` under this decision

As a result:

- general tax is not added again to KRW receipt total calculations
- tax header and tax section are hidden for KRW receipts
- item amounts remain closer to the real paid amount for Korean receipts

## Consequences
Positive outcomes:

- prevents general-tax double charging on Korean receipts
- fixes both the displayed UI and the real settlement calculation consistently
- solves the issue quickly without changing the parser payload contract immediately

Negative outcomes and costs:

- tax-included behavior is inferred from currency alone
- exceptional KRW documents that really should add tax separately are hard to express under the current rule
- in the long term, tax treatment may need to become more explicit at the payload level

## Follow-up Notes
- this is a priority policy introduced to prevent a real double-tax issue observed in Korean receipts
- in the long term, the parser may need to send an explicit `CountryRegion` or `TaxTreatment` field
- this decision applies only to general tax and does not automatically alter `Tip`, `SST`, or `SLT` handling

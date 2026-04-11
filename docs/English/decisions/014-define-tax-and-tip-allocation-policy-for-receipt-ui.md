# 014 - Define Tax And Tip Allocation Policy For Receipt UI

## Status
Accepted

## Context
The `discord-api` receipt check UI lets users select items and then shows the final settlement amounts based on those selections.

At this stage, tax and tip had to be included in actual settlement calculations, and a policy was needed in particular for how to divide alcohol-related special taxes such as `SST` and `SLT`.

A policy was needed for the following reasons.

- general sales tax and special alcohol taxes are not the same kind of charge
- `SST` and `SLT` are directly tied to alcohol items, so splitting them across people who did not choose alcohol would be unfair
- alcohol already carries extra taxes, so spreading those costs to the full group would not match actual consumption
- general tax is also better allocated by the price ratio of the items each user selected than by a flat equal split
- tip can reasonably be expected to work either as proportional split or equal split depending on the receipt, venue, or user expectation, so it should be owner-selectable rather than fixed

The policy also had to be explainable in Discord UI, so the public check message needed to make it visible why each person was paying a given tax or tip amount.

## Options Considered
1. split all tax and tip equally across all participants
- simplest to implement
- but would also spread alcohol-specific taxes across everyone, which mismatches consumption
- fairness is poor because people who did not choose alcohol would still pay `SST` and `SLT`

2. split all tax and tip proportionally across all participants
- somewhat natural for general tax
- but still spreads `SST` and `SLT` to non-alcohol buyers
- the calculation still ignores which items actually caused the special taxes

3. split general tax proportionally across all selected items, split `SST` and `SLT` proportionally only among alcohol-item selectors, and let the owner choose tip mode
- general tax is allocated according to overall consumption share, which is comparatively fair
- alcohol taxes are assigned only to people connected to alcohol items, making the policy easier to explain
- tip can support both proportional and equal modes depending on the situation
- implementation and UI become more complex, but this best matches real receipt settlement expectations

## Decision
Adopt the following policy.

- General `Tax` is distributed in proportion to the total price of all items selected by each user.
- Alcohol-related special taxes such as `SST` and `SLT` are distributed only among users who selected alcohol items.
- The internal distribution of `SST` and `SLT` is also proportional to the alcohol-item price selected by each user.
- Tip is treated as a receipt-level value, and the owner can choose one of two modes during the check stage.
- Tip proportional mode:
  distribute in proportion to the total value of all items selected by each user.
- Tip equal mode:
  split equally among participants who selected at least one item.

Reflect this policy in the UI as well.

- The public check message includes an owner-only `Mark Alcohol` button.
- The owner can mark multiple items as alcohol in a private selection panel.
- Items marked as alcohol are visibly distinguishable in the public message.
- The public check message includes both a `Tax` section and a `Tip` section.
- The `Tax` section shows each user's share broken down into general tax, `SST`, and `SLT`.
- The `Tip` section shows each user's tip amount.
- The header exposes `SST`, `SLT`, and `Tip` only when those values actually exist on the receipt.

## Consequences
Positive outcomes:

- alcohol-related special taxes are assigned only to actual alcohol buyers, improving fairness
- general tax is also divided by consumption share, making it easier to explain why a person pays a certain amount
- the system does not force one single tip policy and therefore better supports real usage scenarios
- separating `Tax` and `Tip` in the public message makes the settlement basis more transparent

Negative outcomes and costs:

- implementation scope is larger because parser contract, session state, settlement calculation, and Discord UI all change together
- alcohol-item identification may not be reliable enough from parser output alone, so manual owner tagging UI is needed
- if the owner does not mark alcohol items, `SST` and `SLT` settlement cannot be validated correctly and extra checks are required
- tip-mode toggling increases interaction count and state complexity during the check phase

## Follow-up Notes
- This decision operates on top of the public receipt check-message UI flow.
- For related UI direction, also refer to `011-discord-selcetion-ui.md`.
- If parser alcohol-item detection improves later, the dependency on owner-side manual alcohol tagging can be reduced.
- If tip editing later needs to go beyond parser payload values, an owner-editable tip policy can be split into a separate decision.

# 019 - Attribute Negative Receipt Lines To The Previous Item And Ignore Unmatched Discounts

## Status
Accepted

## Context
When `receipt-parser` sent OCR output to `discord-api` as a draft payload, discount lines on receipts were sometimes coming through as ordinary items.

Typical examples looked like this.

- a discount line such as `-6.00` existed under a product line
- OCR extracted that line as a separate item
- `discord-api` recalculated subtotal and total based on the item sum

If a draft was built that way, the discount line entered the settlement UI as a “negative item.”

As a result:

- the public check UI could show the discount line as if it were a normal item
- it could appear as a selectable participant target
- total recalculation could become distorted after add/remove/edit operations

In the current `discord-api` user flow, what matters is understanding which product received the discount. Showing only a receipt-level total discount would hide which item the discount belonged to.

At the same time, OCR quality cannot guarantee that every discount line can always be attributed to the correct product.

## Options Considered
1. keep negative-value lines as ordinary items
- simplest implementation
- but mixes discount lines into settlement items and breaks subtotal/total recalculation
- gives a poor user experience

2. treat all negative-value lines only as a receipt-level aggregate discount
- prevents total distortion
- but removes item-level discount context from the UI
- loses too much information for item-level settlement

3. attribute negative-value lines to the previous ordinary item first, and for unmatched discounts expose an `Unattributed Discount` section and apply it
- works well for many grocery-style receipts
- supports item-level discount display
- but needs extra unmatched-discount UX and calculation policy when OCR order is wrong or the discount is global

4. attribute negative-value lines to the previous ordinary item first, and ignore unmatched discounts
- handles the most common discount pattern simply
- avoids subtracting potentially wrong global discounts
- unmatched discounts are not applied automatically, but users can correct them manually through `Edit item`

## Decision
Negative receipt lines are handled using the following policy.

- do not include them as normal items in the draft
- first try to attribute them to the immediately preceding ordinary item as a discount
- if attribution succeeds, accumulate them on that item
- if multiple discount lines appear consecutively below the same item, keep accumulating them onto that same item
- if attribution fails, do not apply the discount in `discord-api` calculation or UI

UI policy:

- items with successful discount attribution are rendered as one-line summaries
- example: `Protein Bar - $3.50 (discount -$1.00)`
- no separate `Unattributed Discount` section is shown for unmatched discounts

Calculation policy:

- item-level settlement amount uses the net amount after discount
- `Item Total Price` reflects the discounted item total
- unmatched discounts do not further reduce the overall total automatically

## Consequences
Positive outcomes:

- discount lines no longer appear as ordinary settlement items
- the common “discount belongs to the item immediately above it” pattern is preserved for users
- public UI can show discount information in the context of the item itself
- the risk of subtracting the total incorrectly due to bad OCR order or wrong matching is reduced

Negative outcomes and costs:

- the previous-item rule cannot accurately represent every discount layout
- name matching, position matching, and global coupon handling are not addressed yet
- when attribution fails, users may need to correct the item manually through `Edit item`

## Follow-up Notes
- both `receipt-parser` and `discord-api` now use item-level discount fields together
- discount scenarios can be reproduced with `/test`, including single-discount and stacked-discount cases
- if attribution failures become common because of OCR quality, product-name matching or a global-discount policy can be revisited in a separate decision
- this decision assumes the current public receipt UI remains item-centric

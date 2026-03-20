# 011 - Use a Merged Item UI with Equal Split and Pagination

## Status
Accepted

## Context

The Discord-based receipt settlement workflow needs a user interface that allows multiple users to select which receipt items they participated in.

Several constraints shaped the design:

- Discord select menus have option limits
- real-world receipts often contain repeated item lines
- a single item may be shared by multiple users
- the UI needs to be readable in a Discord message format
- settlement behavior should remain understandable for users

An early design that displayed every OCR line item individually introduced too much UI complexity and ambiguity.

## Options Considered

### Option A - Display Every OCR Line Item Individually

Advantages:

- closer to the raw receipt structure
- could support more granular assignment semantics

Disadvantages:

- repeated items create noisy UI
- message readability becomes poor
- user interaction becomes harder
- Discord component limits become more restrictive

### Option B - Merge Identical Items and Use Simplified Selection Rules

Advantages:

- cleaner and more readable UI
- easier user understanding
- better fit for Discord component constraints
- simpler settlement calculation model

Disadvantages:

- loses some per-line-item granularity
- assumes simpler split behavior

## Decision

The Discord receipt selection UI will follow these principles:

1. identical items are merged into a normalized representation with quantity information
2. the UI primarily displays selections grouped by user
3. items selected by multiple users appear in a shared section
4. cost is split equally among all users who selected an item
5. users select items through a Discord string select menu
6. pagination is used when receipts exceed Discord component limits

## Consequences

### Positive

- significantly cleaner and more readable Discord UI
- less confusion from repeated OCR items
- simple and predictable settlement calculation behavior
- compatibility with Discord select menu limits
- better support for shared item representation

### Negative

- item quantities do not directly model per-user consumption
- the system assumes equal-split behavior for shared items
- more advanced split modes are not supported in this version

## Follow-up Notes

Future iterations may consider richer split modes such as:

- quantity-based assignment
- percentage splits
- more advanced settlement rule variants

For the current project scope, the merged-item equal-split model provides the best balance between usability, implementation simplicity, and Discord UI constraints.

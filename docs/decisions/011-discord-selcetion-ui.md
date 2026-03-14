
# 011 - Adopt Merged Item UI with Equal Split and Paginated Selection

## Status
Accepted

## Context

During the design of the Discord-based user interface for the receipt settlement workflow, several UI and interaction design decisions needed to be finalized.

The system allows multiple users in a Discord channel to select which receipt items they participated in purchasing. Based on those selections, the system calculates the final settlement.

Several constraints and design considerations influenced the final decision:

- Discord message components have limitations (e.g., select menu options are limited to 25 items).
- Real-world receipts often contain repeated items (e.g., multiple identical items listed separately).
- A single item may be shared by multiple users.
- A single user may select multiple items.
- The UI must clearly communicate shared items and individual items.
- The calculation logic should remain simple and predictable for users.

Initial approaches considered displaying every OCR item line individually and computing settlement based on item quantity assignment. However, this approach introduced UI complexity and ambiguity for users.

Therefore, a simplified and more user-friendly model was adopted.

---

## Decision

The system will use the following UI and settlement design principles.

### 1. Merge Identical Receipt Items

Items extracted from OCR that have the same normalized description will be merged into a single item with a quantity value.

Example:

OCR result:

```
Pizza Slice
Pizza Slice
Pizza Slice
Coke
Coke
Beer
```

Normalized representation:

```
Pizza Slice (3)
Coke (2)
Beer (1)
```

The merged item becomes the unit displayed in the UI and selectable by users.

---

### 2. User → Items Display Structure

The Discord message will primarily display selections grouped by user rather than listing users under each item.

Example:

```
Sam
• Pizza Slice
• Coke

Joy
• Pizza Slice
• Beer
```

This structure is easier for users to read and makes it clearer what each person selected.

---

### 3. Shared Item Representation

Items selected by more than one user will be grouped under a shared section in the message.

Example:

```
Shared
• Pizza Slice (3) — Sam, Joy

Individual

Sam
• Coke

Joy
• Beer
```

This helps clearly communicate which items are being split between multiple users.

---

### 4. Equal Cost Distribution

Selecting an item means the cost of that item is shared equally among the users who selected it.

Calculation rule:

```
item_cost / number_of_selected_users
```

Example:

```
Pizza Slice (3)
Total price: $9
Selected users: Sam, Joy, Alex

Each pays: $3
```

The item quantity is treated primarily as informational display data rather than directly determining how many units each user consumed.

---

### 5. Select Menu for Item Selection

Users will choose items they participated in through a Discord **String Select Menu**.

Example:

```
Choose items you shared

☑ Pizza Slice (3)
☑ Coke (2)
☑ Beer (1)
```

Selections will update the shared receipt message dynamically.

---

### 6. Pagination for Large Receipts

Discord select menus support a maximum of **25 options**.

To handle receipts with more items than this limit, item selection will support pagination.

Example:

```
Choose items (Page 1/2)

☑ Pizza Slice (3)
☑ Coke (2)
☑ Beer (1)
...
[Next Page]
```

Pagination ensures the UI remains compatible with Discord component limits.

---

## Consequences

### Positive

- significantly cleaner and more readable Discord UI
- avoids confusion caused by repeated identical items
- simple and predictable settlement calculation logic
- compatible with Discord select menu limits
- supports shared items naturally
- reduces message length and UI clutter

### Negative

- item quantities do not directly represent per-user consumption
- the system assumes equal cost splitting when multiple users select the same item
- more complex settlement rules (e.g., per-unit assignment) are not supported in this version

---

## Follow-up Notes

Future iterations may consider supporting more advanced split modes (e.g., quantity-based assignment or percentage splits). However, the current equal-split model provides the best balance between simplicity, usability, and implementation complexity for the scope of this project.

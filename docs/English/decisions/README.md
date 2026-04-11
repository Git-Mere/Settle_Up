# Decision Records

This directory stores the project's architecture and design decision records in a lightweight ADR style.

Each file documents one decision that affects system structure, service boundaries, runtime behavior, delivery flow, or long-term implementation direction.

## Purpose

Use this folder to record decisions that:

- change the architecture of the system
- define service boundaries or ownership
- lock in an important implementation direction
- replace an earlier architectural assumption
- explain why a non-obvious technical tradeoff was chosen

These records are meant to preserve reasoning, not just outcomes.

## Standard Format

Every decision document in this folder should use the same section structure:

```md
# NNN - Decision Title

## Status
Accepted

## Context
...

## Options Considered
...

## Decision
...

## Consequences
...

## Follow-up Notes
...
```

Section expectations:

- `Status`
  - Current state of the decision.
  - Preferred values: `Proposed`, `Accepted`, `Superseded`, `Deprecated`.

- `Context`
  - Why the decision was needed.
  - Include technical constraints, current system state, and the practical problem being solved.

- `Options Considered`
  - The realistic options that were evaluated.
  - Include tradeoffs, not strawman options.

- `Decision`
  - The chosen direction.
  - State it clearly and directly.

- `Consequences`
  - Positive and negative outcomes of the decision.
  - Prefer explicit tradeoffs over vague summaries.

- `Follow-up Notes`
  - Implementation notes, exceptions, future reconsideration points, or links to related decisions.

## Numbering Rules

Decision files use a fixed numeric prefix:

- format: `NNN-kebab-case-title.md`
- example: `012-serialize-receipt-session-updates-and-debounce-public-message-publishing.md`

Rules:

- numbering is sequential and zero-padded to 3 digits
- assign the next available number when adding a new decision
- never renumber older decisions just to reorder them
- if a decision is replaced later, keep the old file and mark its status accordingly rather than recycling its number

This preserves stable references in commits, discussions, and other documents.

## File Naming Rules

Use short but descriptive kebab-case titles.

Good:

- `013-use-single-public-receipt-message.md`
- `014-add-callback-authentication.md`

Avoid:

- vague names such as `misc-update.md`
- very long sentence-style filenames
- filenames without the numeric prefix

## Writing Rules

When adding or updating a decision:

- write from the perspective of the repository, not a personal journal
- describe the real technical problem that triggered the decision
- include meaningful alternatives and tradeoffs
- keep the language direct and implementation-oriented
- prefer concrete statements over generic architectural slogans

Avoid:

- mixing multiple unrelated decisions into one file
- recording ordinary implementation details that do not represent a real decision
- rewriting history without marking prior decisions as superseded or deprecated

## Change Rules

When a previous direction changes:

1. do not delete the earlier decision record
2. update the old document status if needed, for example `Superseded`
3. create a new decision record with a new number
4. reference the older decision in `Follow-up Notes` when useful

Decision records are historical artifacts as well as documentation.

## Scope Guidance

Good candidates for a decision record:

- monorepo vs multi-repo structure
- service-to-service communication model
- where receipt state is persisted
- Discord UI interaction model
- delivery retry model
- concurrency handling strategy

Poor candidates for a decision record:

- routine refactoring
- variable naming changes
- one-off bug fixes
- code formatting changes

## Current Convention

The records in this folder are now standardized to the ADR structure defined in this README.

Future additions should follow the same format unless the repository intentionally adopts a new documented standard.

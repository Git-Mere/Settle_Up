# 012 - Serialize Receipt Session Updates and Debounce Public Message Publishing

## Status
Accepted

## Context

The `discord-api` service currently manages receipt interaction state in memory through `ReceiptSessionState`.

A single receipt session may be accessed by multiple Discord users at nearly the same time through:

- item selection
- add item
- remove item
- edit item
- final confirmation

At the current stage of the project, these interaction handlers update the same in-memory session object directly and may also publish a new public Discord message immediately after each change.

This created two related problems:

1. concurrency risk within a single receipt session
2. excessive public message creation when multiple users interact in a short period

The first problem was recognized after reviewing the current implementation:

- the session store uses a concurrent dictionary for top-level lookup
- however, each stored `ReceiptSessionState` still contains mutable in-memory collections such as item lists, user selection maps, and pending edit tokens
- multiple interaction requests can therefore read and mutate the same receipt session concurrently

This means the current structure can lead to race conditions such as:

- one user selection overwriting or interleaving with another update
- item add/remove/edit operations colliding with selection updates
- public message metadata being updated out of order
- final confirmation being evaluated against a moving in-memory state

The second problem comes from the current public-message strategy:

- a new public message may be published after each interaction
- if several users make changes within a short time window, the channel can receive multiple near-duplicate status messages
- this creates channel noise and makes it harder to identify the latest state at a glance

Because the project currently keeps intermediate receipt-selection state in memory rather than persisting each change immediately, the system needs an in-process strategy that improves safety without adding premature storage complexity.

## Options Considered

### Option A - Keep Immediate Per-Interaction Mutation and Publishing

Advantages:

- simplest continuation of the current implementation style
- no additional scheduling or synchronization logic
- public updates appear immediately after each interaction

Disadvantages:

- same-session race conditions remain possible
- channel noise grows quickly during active multi-user interaction
- public message metadata can be updated out of order
- user experience degrades when many near-duplicate public messages appear

### Option B - Serialize Per-Session Updates and Debounce Public Publishing

Advantages:

- same-session state changes become deterministic
- public message volume is reduced during bursts of interaction
- better fit for the current in-memory interaction model
- improves channel readability without requiring immediate persistence of every change

Disadvantages:

- implementation complexity increases
- public updates are slightly delayed
- timers, cancellation, and session lifecycle cleanup must be handled carefully

## Decision

We will introduce two coordinated rules for receipt interaction handling in `discord-api`.

### 1. Serialize updates per receipt session

All state-changing operations for the same receipt session must be processed sequentially rather than concurrently.

This means:

- the system will apply per-session synchronization keyed by receipt session identity
- only one mutation flow for a given receipt session may run at a time
- session reads and writes that affect interaction state and public-message publication must be handled within that serialized flow

The goal is to make receipt-session mutation deterministic and reduce race conditions between concurrent Discord interactions.

### 2. Debounce public message publishing for 2 to 3 seconds

Public receipt status messages will not be re-published immediately for every interaction.

Instead:

- interaction state will be updated first
- the session will be marked as needing a public refresh
- public publishing will be delayed by a short debounce window of approximately 2 to 3 seconds
- if additional changes arrive during that window, the pending publish should be rescheduled so that only the latest state is published

This debounce rule applies to routine interaction updates such as selection and item edits.

The precise implementation detail may vary, but the intended product behavior is:

- bursts of user interaction should converge into a single public update
- the channel should show the latest meaningful state rather than every intermediate step

Time-sensitive terminal actions such as final confirmation may still bypass normal debounce behavior if immediate publication is needed for correctness or user clarity.

## Consequences

### Positive

- reduces race conditions within a single in-memory receipt session
- provides a clearer ordering model for multi-user Discord interactions
- lowers the number of near-duplicate public messages in active channels
- improves readability of the settlement workflow for users
- keeps the implementation aligned with the current in-memory interaction model

### Negative

- public updates will no longer appear instantly for every interaction
- implementation complexity increases because the service must manage per-session synchronization and publish scheduling
- care is required to avoid stale scheduled publishes, leaked timers, or mismatched session lifecycle cleanup
- terminal actions such as confirm may need explicit exception handling rather than using the generic debounce path

## Follow-up Notes

This decision addresses concurrency and message volume within the current interaction architecture, but it does not by itself resolve the separate question of the long-term public message lifecycle strategy.

In particular:

- the project still needs a cleaner approach for maintaining a single clear public receipt message
- Discord channel/message access constraints such as `50001 Missing Access` must still be considered when designing the final message update strategy

This decision should therefore be treated as an interaction-safety and channel-noise reduction measure, not as the final answer for the entire public-message design.

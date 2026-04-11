# 023 - Expire Stale Discord Receipt UI State By Inactivity TTL

## Status
Accepted

## Context

`discord-api` keeps active receipt UI state in memory while a receipt is pending, being checked, or waiting for user interaction.

Recent performance review showed two separate stale-state risks:

- abandoned upload prompt interactions can remain in memory if a user opens the upload modal and never submits it
- pending or active receipt sessions can remain in memory indefinitely if the flow never reaches `confirm` or `cancel`

This is not the same as confirmed session retention, which was already cleaned up on final confirmation.
The remaining issue is long-lived in-progress state that no longer represents useful live work.

The repository now has three distinct classes of in-progress state:

- upload prompt interactions
- pending receipt sessions (`IsDraftReady == false`)
- active check receipt sessions (`IsDraftReady == true` and not confirmed)

These classes do not all have the same value or expected lifetime.

## Options Considered

### 1. Keep all in-progress state indefinitely until explicit user action

Pros:

- simplest logic
- users can return at any time without losing state

Cons:

- abandoned flows accumulate in memory
- stale Discord interaction references remain longer than necessary
- operational cleanup becomes ambiguous

### 2. Apply a single TTL to every in-progress state object

Pros:

- simple cleanup model
- bounded memory growth

Cons:

- pending upload state and active check state have different practical lifetimes
- a single short TTL is too aggressive for real check sessions
- a single long TTL is too lenient for obviously abandoned upload flows

### 3. Apply different inactivity TTLs per state type

Pros:

- treats abandoned flows and active receipt sessions differently
- bounds memory usage without making legitimate active work too fragile
- keeps cleanup policy explicit and explainable

Cons:

- more policy logic
- requires a background cleanup sweep
- stale public messages may be deleted without direct user action

## Decision

Use inactivity-based TTL cleanup with different thresholds per state type.

Chosen TTLs:

- abandoned upload prompt interaction: 15 minutes
- pending receipt session: 15 minutes
- active check receipt session: 6 hours

TTL is based on `UpdatedAtUtc` for receipt sessions and creation time for upload prompt interactions.

Cleanup behavior:

- cancel pending debounced refresh
- delete stale private panel responses when possible
- delete stale public pending/check messages when possible
- remove the in-memory session from the session store
- cleanup the session-scoped lock entry

Confirmed sessions are not part of this TTL policy because they are already cleaned up during the confirm flow.

## Consequences

Positive:

- abandoned state no longer grows without bound
- upload and pending flows are cleaned up aggressively
- active check sessions still allow reasonable return time for users
- memory retention of Discord interaction/message references is reduced

Negative:

- a user can lose an in-progress check session after 6 hours of inactivity
- public receipt messages can disappear through background cleanup rather than explicit user action
- cleanup now depends on a hosted background sweep loop

## Follow-up Notes

- The current cleanup interval is 1 minute.
- If user feedback shows 6 hours is too short or too long, adjust the TTL in code and keep this decision as the rationale for using differentiated inactivity TTLs.
- This decision is closely related to the performance review in `docs/problem-searching/performance-review-2026-04-07.md`.

# 017 - Confirm Receipt Before History Persistence Completes

## Status
Accepted

## Context
`discord-api` was extended to save a settlement history snapshot to Cosmos DB at confirm time.

In the initial implementation, pressing the confirm button worked in this order.

1. save the history document to Cosmos DB
2. update the public message to confirmed only if the save succeeds

This was simple from a data-preservation point of view, but it created a user-experience problem.

- Cosmos write latency directly delayed the confirm UX.
- when the DB was slow or had a transient failure, the confirm button appeared to do nothing immediately.
- users could easily feel that confirm did not work.

What matters more in the current receipt UI is that the user sees the confirmed state immediately after clicking confirm. History persistence is important, but if it blocks the confirm UI itself, the interaction quality degrades.

## Options Considered
1. update the confirm UI only after history persistence completes
- intuitive implementation
- can block confirm if history storage fails
- but DB latency and DB failure directly worsen confirm UX

2. update the confirm UI first and persist history asynchronously in the background
- the user sees the confirmed message immediately
- DB latency no longer directly affects confirm UX
- but confirm may succeed while history persistence later fails

3. update the confirm UI first, then retry history persistence and notify the user on final failure
- keeps the UX advantage of option 2
- provides better operational visibility than pure fire-and-forget
- allows the user to know that history may be missing if persistence ultimately fails

## Decision
When the confirm button is pressed, **update the public message to the confirmed state first**, and then persist settlement history **as a background asynchronous task**.

History persistence policy:

- the confirm UI does not wait for the Cosmos write to complete
- history save runs in the background
- save failure retries up to 2 times
- total attempts are the initial attempt plus 2 retries
- on final failure, write an error log
- on final failure, send the user an ephemeral follow-up message saying `Failed to save settlement history.`

In other words, confirm UX is prioritized, while history persistence is handled as best-effort with retry.

## Consequences
Positive outcomes:

- the confirm button feels more responsive
- Cosmos latency or transient failure no longer blocks the public message transition to confirmed
- users can immediately tell that confirm worked
- retry and failure notification prevent history-save failure from being silently hidden

Negative outcomes and costs:

- confirm completion and successful history persistence are now separate events
- if the DB fails repeatedly, the confirmed message can remain while the history document is missing
- background save retry and follow-up failure handling logic become necessary

## Follow-up Notes
- the current retry policy is a fixed 2 retries; it can later be extended to exponential backoff or queue-based reprocessing if needed
- if missing-history reprocessing becomes necessary in the long term, an outbox or retry-queue style design can be considered
- this decision is tailored to the current Discord interaction model, where confirm UX is the higher priority

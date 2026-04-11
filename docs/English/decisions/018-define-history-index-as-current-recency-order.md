# 018 - Define History Index As Current Recency Order

## Status
Accepted

## Context
`discord-api` needed to support looking up previous confirm results through the `/history` command.

The agreed basic UX was:

- `/history` shows a compact list of the most recent 30 settlement results
- each entry is numbered from `1` to `30`
- if the user specifies `index`, the system shows that entry in detail

The meaning of `index` needed to be defined.

There were two main interpretations.

- interpretation A:
  the first, second, and third items from the list the user just saw
- interpretation B:
  the first, second, and third items in current recency order at query time

Interpretation A would require temporarily storing the list result in memory per user and reusing that cache for later detail lookup.

Interpretation B would instead re-query Cosmos DB using the same current sort rule and select the `n`th result at the current time.

The intended UX was “the most recent one is number 1.” For that reason, index should be interpreted as current recency order rather than the position inside the previously shown list.

## Options Considered
1. cache the `/history` list per user and interpret index against that cache
- the detail lookup exactly matches the list the user just saw
- but requires per-user history browse cache, TTL, overwrite logic, and restart behavior
- implementation complexity grows compared to the current project needs

2. define index as current recency order and re-query the DB each time
- no cache is needed
- implementation stays simple
- behavior is stable across server restarts
- index meaning can change if new history arrives, but that is intentional under this policy

## Decision
Define `/history` index as the **current recency order**.

That means:

- `index:1` = the most recent confirmed settlement at the current point in time
- `index:2` = the second most recent confirmed settlement at the current point in time
- `index:30` = the thirtieth most recent confirmed settlement at the current point in time

Accordingly:

- the `/history` list queries the latest 30 results using `confirmedAtUtc DESC`
- detail lookup also re-queries using `confirmedAtUtc DESC`
- no separate cache is kept for `/history` list results

## Consequences
Positive outcomes:

- implementation is simple
- no per-user in-memory history cache is needed
- the rule stays consistent across restarts or cache expiration
- the meaning of index is directly tied to the current recency sort rule

Negative outcomes and costs:

- if a new confirm happens after the user looked at a list, the number meaning can change
- in other words, “the number 1 from earlier” may not match “number 1 now”
- that is an intentional behavior under the current policy

## Follow-up Notes
- the base `/history` query uses `uploadedByUserId == currentUserId` with `confirmedAtUtc DESC`
- the list output remains a compact summary while the detail output follows a structure similar to the confirm message
- if later UX requires “index based on the list I just saw,” a separate browse-context cache can be introduced in a new decision

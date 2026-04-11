# 013 - Use Session-Scoped In-Memory Cache for Discord Receipt UI

## Status
Accepted

## Context

The `discord-api` service is responsible for handling the interactive receipt-selection workflow inside Discord.

As the receipt UI became more complete, the service began performing frequent public message updates for:

- item selection
- add item
- remove item
- edit item
- confirm

During optimization work, it became clear that part of the interaction latency was caused by repeated in-process recomputation and repeated message resolution work.

In particular:

- the main public receipt message could be fetched again before modification even when the service had already resolved it earlier in the same session
- receipt embed rendering repeatedly recalculated the same derived values such as user-item mappings, unassigned items, settlement lines, and display names during a single render

Because the current receipt interaction model already keeps active workflow state in memory through `ReceiptSessionState`, the project needed a practical way to reduce repeated work without introducing a distributed cache or durable persistence for every UI update.

## Options Considered

### Option A - Recompute and Re-resolve Everything on Every Interaction

Advantages:

- simplest model
- minimal extra state to maintain
- avoids any cache invalidation concerns

Disadvantages:

- repeated Discord API lookups increase interaction latency
- repeated derived-data recomputation increases CPU work during rendering
- performance degrades more noticeably as receipt size and participant count grow

### Option B - Use Session-Scoped In-Memory Caching

Advantages:

- reduces repeated Discord message resolution work inside the active session
- reduces repeated derived-data computation during message rendering
- fits the existing in-memory session model already used by `discord-api`
- improves interaction responsiveness without introducing external infrastructure

Disadvantages:

- cached values are process-local and are lost on restart
- stale cache references are possible and require safe fallback behavior
- additional session state must be maintained carefully

## Decision

We will use session-scoped in-memory caching inside `discord-api` for Discord receipt UI optimization.

This includes:

- caching the current main public receipt message reference inside `ReceiptSessionState`
- using precomputed render context data during a single receipt message render instead of recalculating the same derived values repeatedly

The cache is explicitly scoped to the active in-memory receipt session and is not treated as durable state.

If a cached Discord message reference is unavailable or no longer usable, the service must fall back to resolving the message again rather than assuming the cache is always valid.

## Consequences

### Positive

- lower latency during interactive receipt updates
- fewer redundant Discord message fetches
- less repeated CPU work while building receipt embeds
- better responsiveness for larger receipts and more active sessions
- no new infrastructure dependency is required

### Negative

- cache contents are not durable across process restarts
- cache invalidation and fallback behavior must be handled correctly
- optimization logic becomes somewhat more complex than a pure recompute-every-time model

## Follow-up Notes

This decision is intentionally limited to local in-memory optimization for the current single-process interaction model.

It does not replace the need for careful concurrency handling. Session-scoped caching should be used together with:

- per-session serialization for state mutation
- safe fallback when cached Discord objects are unavailable
- future reconsideration if the service is scaled to multiple application instances

If `discord-api` later runs as multiple instances, these caches must be treated as process-local hints rather than globally authoritative state.

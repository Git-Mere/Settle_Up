# 025 - Add Production-Oriented Observability Baseline With Low-Cardinality Metrics

## Status
Accepted

## Context

The repository already had structured application logging and OpenTelemetry tracing, but the current production-oriented questions were only partially covered:

- whether parser success/failure rates were changing over time
- whether parser-to-discord callback retries or failures were increasing
- whether active receipt UI sessions were accumulating
- whether receipt confirmation and history persistence were healthy

At the same time, the current system includes hot-path Discord interactions and in-memory session state. Adding excessive logging or high-cardinality metric dimensions would increase runtime overhead, ingestion noise, and Azure Monitor cost without improving operational clarity.

The project therefore needed a deliberately small observability baseline that improves production visibility while preserving the current performance-sensitive interaction flow.

## Options Considered

### 1. Keep logs and traces only

- no new metric work required
- preserves the simplest current setup
- still leaves failure-rate, retry-rate, and active-session visibility weak
- makes dashboards and alerting harder than they need to be

### 2. Add broad metrics and tag everything aggressively

- maximizes immediate instrumentation coverage
- tempting because it exposes many slices quickly
- creates high-cardinality risk if identifiers such as user IDs, receipt IDs, or blob URLs are used
- increases ingestion cost and operational noise
- more likely to hurt hot-path performance in Discord interaction flows

### 3. Add a narrow first-pass metric set with low-cardinality dimensions only

- improves production visibility for the main system health questions
- avoids over-instrumenting routine interaction paths
- keeps room for later expansion based on real operational need
- requires selecting only the most valuable counters, histograms, and session gauges

## Decision

Adopt option 3.

The project will keep structured logs and traces, and add a narrow first-pass metrics baseline focused on production health visibility.

The initial metric set will emphasize:

- parser success/failure
- parser-to-discord callback success/failure/retry
- active receipt session counts
- receipt confirmation count
- settlement history failure count
- confirm/history/parse/callback/Cosmos durations where helpful

The implementation will avoid high-cardinality metric tags. Values such as `receiptId`, `userId`, `guildId`, `blobUrl`, and `merchantName` must not be used as metric dimensions.

Logs will continue to carry detailed identifiers where needed, while metrics remain aggregation-oriented.

## Consequences

Positive:

- the main end-to-end flow becomes easier to observe in Azure Monitor
- dashboards and alerting can be built around parser health, callback health, and active session health
- the system gains useful operational signal without needing verbose hot-path logs everywhere
- instrumentation remains consistent with the existing OpenTelemetry direction

Negative:

- the first metric set is intentionally incomplete and will not answer every future question
- some deeper business analysis still requires logs or traces
- active session counts depend on correct state-transition instrumentation and should be reviewed when lifecycle behavior changes

## Follow-up Notes

- this decision intentionally favors a small, production-safe first pass over broad instrumentation coverage
- if future operational needs require more detailed metrics, expansion should be justified by real usage data
- hot interaction paths should continue to prefer metrics over verbose informational logs
- related implementation summary: [observability-signals.md](/home/aero-mere/CS397/Settle_Up/docs/observability-signals.md)

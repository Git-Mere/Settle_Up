# Observability Signals

## Purpose

This document summarizes the current production-oriented observability signals that Settle Up collects across logs, traces, and metrics.

The goal of the current observability layer is:

- identify whether the main receipt flow is healthy
- detect failures in parser-to-discord delivery
- understand where latency is accumulating
- keep session lifecycle visible without adding excessive hot-path overhead

This document intentionally focuses on the current implemented signals, not every future idea.

## Logging

### Common behavior

- console logging is single-line with timestamps
- default application log level is `Information`
- `Microsoft`, `System`, `HttpClient`, and Azure SDK noise is filtered down to `Warning`
- if `APPLICATIONINSIGHTS_CONNECTION_STRING` is configured, logs are also exported to Azure Monitor / Application Insights

### `discord-api` logs

Current important logs:

- bot lifecycle
  - bot starting
  - gateway connection starting
  - bot stopping
- Discord command and interaction lifecycle
  - slash command started/completed/failed
  - button/select/modal completed/failed
- receipt upload flow
  - `/settle-up` accepted
  - blob uploader not configured
  - blob upload started/rejected/failed/completed
- parser callback intake
  - `/getting_draft` request received
  - invalid payload
  - draft found / draft not found
  - unexpected endpoint error
- receipt session lifecycle
  - pending upload session created
  - custom session created
  - draft session upserted
  - expired session cleaned up
- settlement lifecycle
  - receipt confirmed summary
  - settlement history save failed / retries exhausted
  - settlement history save success
- authorization violations
  - owner-only action denied

### `receipt-parser` logs

Current important logs:

- service lifecycle
  - parser service started
  - parser service stopping
- Event Grid intake
  - blob event received
  - blob URL extraction failed
  - blob event processing completed
  - blob event processing failed
- local test intake
  - local upload processing started/completed/failed
- parsing lifecycle
  - Document Intelligence warm-up started/completed/skipped
  - receipt parsing started
  - blob download failed
  - receipt parsing completed
  - receipt parsing failed
- Cosmos persistence
  - Cosmos write started/completed/failed
  - Cosmos repository warm-up started/completed
- downstream callback
  - discord-api send started
  - send succeeded
  - retry scheduled
  - timeout retry scheduled
  - final retry exhaustion / non-retryable failure

## Traces

Traces are emitted through OpenTelemetry `ActivitySource` and exported to Azure Monitor when `APPLICATIONINSIGHTS_CONNECTION_STRING` is configured.

### Shared trace sources

- `SettleUp.DiscordApi`
- `SettleUp.ReceiptParser`

### Important traced operations

`discord-api`:

- `discord.ready`
- `discord.slash_command.execute`
- `discord.button.execute`
- `discord.select_menu.execute`
- `discord.modal.submit`
- `discord.blob.upload`

`receipt-parser`:

- `receipt_parser.blob_event.process`
- `receipt_parser.local_upload.process`
- `receipt_parser.document_intelligence.parse`
- `receipt_parser.document_intelligence.parse_binary`
- `receipt_parser.cosmos.upsert`
- `receipt_parser.discord_api.send`

Also included through instrumentation:

- ASP.NET Core request traces
- HttpClient dependency traces

## Metrics

Metrics are emitted through OpenTelemetry `Meter` and exported to Azure Monitor metrics when `APPLICATIONINSIGHTS_CONNECTION_STRING` is configured.

The current metric set is intentionally small to keep cardinality and hot-path overhead low.

### `discord-api` metrics

Counters:

- `draft_received_total`
- `receipt_confirmed_total`
- `history_save_failed_total`
- `discord_permission_denied_total`

UpDownCounters:

- `active_receipt_sessions`
- `active_pending_upload_sessions`

Histograms:

- `receipt_confirm_duration_ms`
- `history_save_duration_ms`

Existing command/upload metrics already present in the service:

- `discord_commands_registered_total`
- `discord_slash_commands_total`
- `discord_image_upload_timeout_total`
- `discord_slash_command_duration_ms`
- `discord_image_wait_duration_ms`

### `receipt-parser` metrics

Counters:

- `receipt_parse_succeeded_total`
- `receipt_parse_failed_total`
- `discord_callback_succeeded_total`
- `discord_callback_failed_total`
- `discord_callback_retry_total`

Histograms:

- `receipt_parse_duration_ms`
- `discord_callback_duration_ms`
- `cosmos_write_duration_ms`

## Cardinality Rules

To keep metric cost and query behavior safe in production, the current implementation avoids high-cardinality metric dimensions.

Do not use these as metric tags:

- `receiptId`
- `userId`
- `guildId`
- `blobUrl`
- `merchantName`

These values remain appropriate for logs and traces when needed, but not for metrics.

## Current Operational Intent

The current signal set is meant to answer these production questions quickly:

- is receipt parsing succeeding or failing?
- is parser-to-discord callback healthy?
- are active draft sessions accumulating unexpectedly?
- are confirm operations completing?
- is settlement history persistence failing?
- are owner-only actions being denied frequently?

## Related Documents

- [codex.md](/home/aero-mere/CS397/Settle_Up/codex.md)
- [performance-review-2026-04-07-post-refactor.md](/home/aero-mere/CS397/Settle_Up/docs/problem-searching/performance-review-2026-04-07-post-refactor.md)
- [025-add-production-oriented-observability-baseline-with-low-cardinality-metrics.md](/home/aero-mere/CS397/Settle_Up/docs/decisions/025-add-production-oriented-observability-baseline-with-low-cardinality-metrics.md)

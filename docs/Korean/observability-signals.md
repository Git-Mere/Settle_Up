# 관측성 신호

## 목적

이 문서는 Settle Up이 현재 로그, 트레이스, 메트릭에서 수집하는 생산 환경 중심 관측성 신호를 정리한다.

현재 관측성 계층의 목표는 다음과 같다.

- 주요 영수증 흐름이 정상적인지 확인한다.
- parser에서 discord로 가는 전달 경로의 실패를 탐지한다.
- 지연이 어느 구간에서 누적되는지 이해한다.
- 과도한 hot-path 오버헤드 없이 session lifecycle을 보이게 만든다.

이 문서는 향후 아이디어 전체가 아니라, 현재 구현된 신호를 중심으로 정리한다.

## Logging

### 공통 동작

- 콘솔 로그는 single-line 형식과 timestamp를 사용한다.
- 기본 애플리케이션 로그 레벨은 `Information`이다.
- `Microsoft`, `System`, `HttpClient`, Azure SDK 관련 노이즈는 `Warning` 수준으로 필터링한다.
- `APPLICATIONINSIGHTS_CONNECTION_STRING`가 설정되어 있으면 로그도 Azure Monitor / Application Insights로 export된다.

### `discord-api` 로그

현재 중요한 로그:

- bot lifecycle
  - bot starting
  - gateway connection starting
  - bot stopping
- Discord command / interaction lifecycle
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

### `receipt-parser` 로그

현재 중요한 로그:

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

트레이스는 OpenTelemetry `ActivitySource`를 통해 생성되며, `APPLICATIONINSIGHTS_CONNECTION_STRING`가 설정되어 있으면 Azure Monitor로 export된다.

### 공통 trace source

- `SettleUp.DiscordApi`
- `SettleUp.ReceiptParser`

### 중요한 traced operation

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

instrumentation으로 함께 포함되는 항목:

- ASP.NET Core request trace
- HttpClient dependency trace

## Metrics

메트릭은 OpenTelemetry `Meter`를 통해 생성되며, `APPLICATIONINSIGHTS_CONNECTION_STRING`가 설정되어 있으면 Azure Monitor metrics로 export된다.

현재 메트릭 세트는 cardinality와 hot-path 오버헤드를 낮게 유지하기 위해 의도적으로 작게 유지한다.

### `discord-api` 메트릭

Counter:

- `draft_received_total`
- `receipt_confirmed_total`
- `history_save_failed_total`
- `discord_permission_denied_total`

UpDownCounter:

- `active_receipt_sessions`
- `active_pending_upload_sessions`

Histogram:

- `receipt_confirm_duration_ms`
- `history_save_duration_ms`

서비스에 기존부터 있던 command/upload 관련 메트릭:

- `discord_commands_registered_total`
- `discord_slash_commands_total`
- `discord_image_upload_timeout_total`
- `discord_slash_command_duration_ms`
- `discord_image_wait_duration_ms`

### `receipt-parser` 메트릭

Counter:

- `receipt_parse_succeeded_total`
- `receipt_parse_failed_total`
- `discord_callback_succeeded_total`
- `discord_callback_failed_total`
- `discord_callback_retry_total`

Histogram:

- `receipt_parse_duration_ms`
- `discord_callback_duration_ms`
- `cosmos_write_duration_ms`

## Cardinality Rules

프로덕션에서 메트릭 비용과 조회 특성을 안전하게 유지하기 위해, 현재 구현은 high-cardinality metric dimension을 피한다.

다음 값은 metric tag로 사용하지 않는다.

- `receiptId`
- `userId`
- `guildId`
- `blobUrl`
- `merchantName`

이 값들은 필요 시 로그와 트레이스에는 쓸 수 있지만, 메트릭에는 사용하지 않는다.

## Current Operational Intent

현재 signal set은 다음 생산 환경 질문에 빠르게 답하기 위해 설계되었다.

- receipt parsing이 성공하고 있는가, 실패하고 있는가?
- parser-to-discord callback은 정상적인가?
- active draft session이 예상보다 많이 쌓이고 있지 않은가?
- confirm operation은 완료되고 있는가?
- settlement history persistence가 실패하고 있지 않은가?
- owner-only action denied가 자주 발생하고 있는가?

## Related Documents

- [codex.md](/home/aero-mere/CS397/Settle_Up/codex.md)
- [performance-review-2026-04-07-post-refactor.md](/home/aero-mere/CS397/Settle_Up/docs/problem-searching/performance-review-2026-04-07-post-refactor.md)
- [025-add-production-oriented-observability-baseline-with-low-cardinality-metrics.md](/home/aero-mere/CS397/Settle_Up/docs/decisions/025-add-production-oriented-observability-baseline-with-low-cardinality-metrics.md)

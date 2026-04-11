# 025 - 낮은 Cardinality 메트릭 기반의 프로덕션용 관측성 기본선 추가

## Status
Accepted

## Context

저장소에는 이미 구조화된 애플리케이션 로그와 OpenTelemetry trace가 있었지만, 실제 프로덕션 관점에서 중요한 질문은 일부만 다뤄지고 있었다.

- parser 성공/실패 비율이 시간에 따라 어떻게 변하는지
- parser-to-discord callback retry 또는 failure가 증가하고 있는지
- active receipt UI session이 비정상적으로 누적되고 있는지
- receipt confirm과 history persistence가 정상적인지

동시에 현재 시스템은 hot-path Discord interaction과 in-memory session state를 포함한다. 과도한 logging이나 high-cardinality metric dimension을 추가하면, 운영 가시성을 충분히 높이지 못한 채 runtime overhead, ingestion noise, Azure Monitor 비용만 늘릴 수 있다.

따라서 현재 프로젝트에는 성능 민감 구간을 유지하면서도 생산 환경 가시성을 높여 주는, 의도적으로 작은 observability baseline이 필요했다.

## Options Considered

### 1. 로그와 trace만 유지

- 새로운 metric 작업이 필요 없다.
- 가장 단순한 현재 구성을 유지할 수 있다.
- 하지만 failure rate, retry rate, active session 가시성은 여전히 약하다.
- dashboard와 alerting 구성이 필요 이상으로 어려워진다.

### 2. 폭넓은 metric을 추가하고 tag를 적극적으로 붙인다

- 즉시 더 많은 계측 범위를 확보할 수 있다.
- 빠르게 여러 slice를 보고 싶을 때는 매력적으로 보일 수 있다.
- 하지만 user ID, receipt ID, blob URL 같은 식별자를 사용하면 high-cardinality 위험이 생긴다.
- ingestion 비용과 운영 노이즈가 커진다.
- Discord interaction hot path에 성능 부담을 줄 가능성이 더 높다.

### 3. low-cardinality dimension만 사용하는 좁은 1차 metric 세트를 추가한다

- 주요 시스템 상태 질문에 대한 생산 환경 가시성을 높일 수 있다.
- routine interaction path를 과도하게 계측하지 않는다.
- 실제 운영 필요에 맞춰 이후 확장할 여지를 남긴다.
- 가장 가치 있는 counter, histogram, session gauge만 골라야 한다.

## Decision

3번을 채택한다.

프로젝트는 structured log와 trace를 유지하면서, 생산 환경 상태를 보기 위한 좁은 1차 metrics baseline을 추가한다.

초기 metric 세트는 다음에 집중한다.

- parser success / failure
- parser-to-discord callback success / failure / retry
- active receipt session count
- receipt confirmation count
- settlement history failure count
- confirm, history, parse, callback, Cosmos duration 지표

구현 시 high-cardinality metric tag는 사용하지 않는다. `receiptId`, `userId`, `guildId`, `blobUrl`, `merchantName` 같은 값은 metric dimension으로 사용하지 않는다.

세부 식별 정보는 필요 시 로그에 두고, 메트릭은 집계 중심으로 유지한다.

## Consequences

긍정적 결과:

- 주요 end-to-end 흐름을 Azure Monitor에서 더 쉽게 볼 수 있다.
- parser health, callback health, active session health를 기준으로 dashboard와 alert를 만들 수 있다.
- hot-path에 verbose log를 남발하지 않고도 운영 신호를 확보할 수 있다.
- 기존 OpenTelemetry 방향과 자연스럽게 일치한다.

부정적 결과:

- 1차 metric 세트는 의도적으로 작기 때문에, 모든 미래 질문에 답하지는 못한다.
- 더 깊은 business analysis는 여전히 로그나 trace가 필요하다.
- active session count는 state-transition 계측이 정확해야 하며, lifecycle 변화가 생기면 함께 검토해야 한다.

## Follow-up Notes

- 이 결정은 넓은 범위의 계측보다, 생산 환경에 안전한 작은 1차 계측 세트를 우선시한다.
- 이후 더 자세한 metric이 필요하면 실제 운영 데이터에 근거해 확장해야 한다.
- hot interaction path에서는 verbose informational log보다 metric을 우선한다.
- 관련 구현 요약: [observability-signals.md](/home/aero-mere/CS397/Settle_Up/docs/observability-signals.md)

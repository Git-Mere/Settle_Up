# 024 - Parser Draft Document를 Cosmos 한 번 쓰기로 저장한다

## 상태
Accepted

## 배경

`receipt-parser`는 parsed receipt draft를 `discord-api`로 보내기 전에 Cosmos DB에 저장한다.

기존 구현에서는 일반적인 성공 경로에서 같은 draft document를 두 번 저장하고 있었다.

1. delivery status가 `Pending`인 큰 draft document 저장
2. `discord-api`로 draft 전송
3. delivery status가 `Sent`인 거의 같은 큰 draft document를 다시 저장

두 write의 실질적 차이는 notification status, attempt count, timestamp, last error 같은 delivery tracking field 정도에 한정되어 있었다.

즉 성공 경로는 다음 비용을 지불하고 있었다.

- Cosmos upsert 두 번
- 전체 document serialization 두 번
- 같은 item list와 parse metadata에 대한 반복 write

서비스는 이미 `discord-api` 전송 시 transient HTTP failure에 대해 즉시 in-process retry를 수행하고 있다.
이 retry는 Cosmos에 저장된 pending delivery status를 다시 읽어서 동작하는 것이 아니라, HTTP failure와 exception을 기준으로 동작한다.

## 검토한 선택지

### 1. 현재의 두 번 쓰기 status-tracking 모델 유지

장점:

- delivery state가 Cosmos에 명시적으로 저장된다.
- 저장된 document만으로 failed delivery attempt를 조사하기 쉽다.
- 향후 reprocessing 설계를 status field 위에서 확장하기 쉽다.

단점:

- 성공 경로에서 큰 document를 두 번 써야 한다.
- Cosmos 비용이 필요 이상으로 커진다.
- 두 번째 upsert 대부분은 변하지 않은 receipt content를 다시 쓰는 일이다.

### 2. delivery status를 별도 lightweight document 또는 container로 분리

장점:

- delivery observability를 유지할 수 있다.
- 상태 변경 때마다 큰 draft body를 다시 쓰지 않아도 된다.
- delivery tracking에 대해 별도 retention policy를 둘 수 있다.

단점:

- document와 schema 복잡도가 늘어난다.
- 추가 write 자체는 여전히 필요하다.
- 아직 concrete reprocessing workflow가 없는 시점에 model/query surface가 하나 더 생긴다.

### 3. parsed draft document는 한 번만 저장하고, delivery outcome은 retry + log에 의존

장점:

- 성공 경로의 Cosmos write 비용이 가장 낮다.
- storage 모델이 가장 단순하다.
- 추가 persistence churn 없이 retry 동작을 유지할 수 있다.

단점:

- Cosmos에 persisted delivery status가 남지 않는다.
- failed delivery investigation은 저장된 metadata보다 log에 더 의존하게 된다.
- 나중에 reprocessing workflow가 필요해지면 새로운 persistence 설계가 필요하다.

## 결정

parsed draft document는 Cosmos DB에 정확히 한 번만 저장한다.

저장되는 draft document에는 parsed receipt content와 downstream consumer가 필요한 metadata만 포함한다.
notification status, attempt count, sent timestamp, last error 같은 delivery tracking field는 draft document schema에서 제거한다.

`receipt-parser`는 계속해서 다음을 수행한다.

- parsed draft document를 한 번 저장한다.
- `discord-api`로 delivery를 시도한다.
- transient failure에는 즉시 in-process retry한다.
- 최종 delivery failure를 log로 남기고 throw한다.

더 이상 `Pending` 또는 `Sent` delivery status를 기록하기 위한 두 번째 Cosmos upsert는 수행하지 않는다.

## 결과

긍정적 결과:

- 성공 경로 비용이 큰 Cosmos write 1회로 줄어든다.
- document schema가 더 단순해진다.
- delivery bookkeeping으로 인한 write amplification이 제거된다.
- transient HTTP failure에 대한 retry 동작은 그대로 유지된다.

부정적 결과:

- Cosmos에는 더 이상 persisted delivery status가 남지 않는다.
- 최종 실패 분석이 log에 더 의존하게 된다.
- 향후 delivery reprocessing이 필요해지면 새로운 명시적 persistence 설계가 필요하다.

## 후속 메모

- 나중에 durable reprocessing이 필요해지면, 큰 document double write를 되살리기보다 전용 lightweight delivery-tracking 모델을 우선 검토한다.
- 이 결정은 `docs/problem-searching/performance-review-2026-04-07.md`의 성능 리뷰를 근거로 한다.
- 이 결정으로 `receipt-parser`에서 Cosmos의 실질적 역할도 바뀐다. Cosmos는 delivery lifecycle state가 아니라 parsed draft content를 저장한다.

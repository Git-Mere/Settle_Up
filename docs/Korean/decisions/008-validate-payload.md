# 008 - `/getting_draft` 경계에서 payload를 검증한다

## Status
Accepted

## Context

`discord-api` 서비스는 내부 컴포넌트로부터 구조화된 draft receipt 데이터를 받기 위해 `/getting_draft` endpoint를 노출한다.

현재 프로젝트 단계에서는 request payload 구조가 아직 진화 중이다. 그래서 개발 및 통합 과정에서 malformed, incomplete, inconsistent request가 들어올 위험이 높다.

따라서 endpoint가 들어오는 데이터를 얼마나 엄격하게 검증할지에 대한 결정이 필요했다.

1. 최소한의 validation만 하고 필요하면 뒤에서 실패시킨다.
2. API boundary에서 먼저 payload를 검증한다.

## Options Considered

### Option A - 최소 검증

장점:

- 초기 구현이 더 단순하다.
- 초반 iteration 속도가 빠르다.

단점:

- malformed request가 시스템 더 깊숙한 곳에서 실패한다.
- debugging이 더 어려워진다.
- 일관성 없는 데이터가 downstream processing이나 storage까지 전파될 수 있다.

### Option B - API boundary에서 검증

장점:

- invalid request를 즉시 거부할 수 있다.
- 통합 단계에서 debugging이 쉬워진다.
- service contract가 더 명확해진다.
- malformed data가 깊은 내부로 들어가는 것을 막을 수 있다.

단점:

- schema가 변할 때 validation logic도 함께 유지해야 한다.
- 구현이 약간 더 복잡하다.

## Decision

`/getting_draft` payload는 처리 전에 API boundary에서 검증한다.

validation 실패 시:

- endpoint는 `400 Bad Request`를 반환한다.
- 요청은 이후 workflow로 진행되지 않는다.
- validation failure를 로그로 남긴다.

인증은 이 결정 범위에 포함하지 않으며, 이후 별도로 추가할 수 있다.

## Consequences

### Positive

- invalid request를 초기에 거부할 수 있다.
- integration debugging이 쉬워진다.
- malformed data가 storage나 downstream logic까지 도달할 가능성이 줄어든다.
- endpoint contract가 더 명확해진다.

### Negative

- payload schema가 바뀔 때 validation rule도 함께 바뀌어야 한다.
- 추가 구현 복잡도가 생긴다.

## Follow-up Notes

향후 다음을 추가로 고려할 수 있다.

- authentication 또는 service identity verification
- OpenAPI나 JSON Schema를 통한 formal schema definition
- service boundary 전반에서 재사용 가능한 shared validation helper

# 007 - `receipt-parser`와 `discord-api` 사이에는 HTTP를 사용한다

## Status
Accepted

## Context

`receipt-parser` 서비스는 영수증 이미지를 처리해 구조화된 draft receipt 데이터를 만든다.

파싱이 끝난 뒤에는 이 draft를 기반으로 다음 Discord-side workflow를 진행할 수 있도록 `discord-api`를 깨워야 한다.

현재 프로젝트 단계에서는 parser-to-Discord 통합 방식에 대해 구체적인 선택이 필요했다.

1. 파싱된 draft를 HTTP로 직접 `discord-api`에 전송한다.
2. event를 publish하고 `discord-api`가 비동기로 소비하게 한다.

이 결정은 complexity, coupling, debugging workflow, delivery speed에 영향을 준다.

## Options Considered

### Option A - 직접 HTTP 요청

흐름:

1. 사용자가 Discord에서 영수증을 업로드한다.
2. 이미지가 Blob Storage에 저장된다.
3. Event Grid가 `receipt-parser`를 trigger한다.
4. `receipt-parser`가 구조화된 receipt 데이터를 추출한다.
5. `receipt-parser`가 결과를 HTTP로 `discord-api`에 전송한다.
6. `discord-api`가 Discord interaction flow를 이어서 처리한다.

장점:

- 현재 단계에서 end-to-end 구현이 더 단순하다.
- local debugging과 request tracing이 쉽다.
- 개발 중 request/response 동작을 이해하기 쉽다.
- event hop을 하나 더 추가하는 것보다 움직이는 부품이 적다.

단점:

- 두 서비스 간 coupling이 더 강해진다.
- downstream availability가 delivery에 직접 영향을 준다.
- retry와 validation을 명시적으로 구현해야 한다.

### Option B - Event-Driven Handoff

장점:

- 서비스 간 coupling이 더 느슨하다.
- 장기적인 event-driven 아키텍처 방향과 더 잘 맞는다.
- 이후 더 많은 downstream consumer를 붙이기 쉽다.

단점:

- 단기적으로 인프라와 운영 복잡도가 더 커진다.
- 현재 단계에서는 구현 속도가 느려진다.
- 이 특정 통합 경로에 대해 local debugging이 더 어려워진다.

## Decision

parsed draft delivery를 위해 `receipt-parser`에서 `discord-api`로 가는 통신은 HTTP를 사용한다.

## Consequences

### Positive

- parser-to-Discord handoff를 더 빠르게 구현할 수 있다.
- 현재 단계에서 end-to-end debugging이 단순하다.
- draft delivery에 대한 단기 service contract가 더 명확하다.

### Negative

- 두 서비스 간 직접 의존성이 더 강해진다.
- retry, error handling, validation을 더 주의 깊게 다뤄야 한다.
- 장기적인 event-driven 이상과는 덜 맞는 경로다.

## Follow-up Notes

이 결정은 현재 단계의 실용적 구현 선택이지, event-driven 설계를 일반적으로 부정하는 것은 아니다.

따라서 이 HTTP endpoint는 명시적인 service contract로 취급해야 하며, 다음으로 강화되어야 한다.

- request validation
- 명확한 logging
- retry behavior
- 필요 시 인증 또는 service verification

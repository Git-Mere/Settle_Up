# 005 - 서비스 간 통신은 Event-Driven 방향을 우선한다

## Status
Accepted

## Context

Settle Up은 multi-service 시스템으로 설계된다. 서비스들은 receipt processing과 settlement의 전체 workflow를 완료하기 위해 서로 조율해야 한다.

초기 설계에서 중요한 질문은 서비스 간 통신 방식을 무엇으로 볼 것인가였다.

1. 서비스 간 직접 동기 HTTP 호출
2. event bus를 통한 event-driven 통신

이 결정은 coupling, resilience, extensibility, 운영 복잡도에 영향을 준다.

## Options Considered

### Option A - 직접 service-to-service HTTP

장점:

- 초기 구현이 더 단순하다.
- 아주 초기 개발 단계에서는 debugging이 쉽다.
- downstream 서비스에서 즉시 응답을 받을 수 있다.

단점:

- 서비스 간 coupling이 강해진다.
- upstream 동작이 downstream availability에 직접 의존한다.
- 이후 추가 consumer를 붙이기 어렵다.
- 런타임 의존성이 더 강해진다.

### Option B - Event-Driven Communication

장점:

- 서비스 간 coupling이 느슨해진다.
- 여러 서비스가 같은 event에 반응할 수 있다.
- 비동기 처리로 resilience가 좋아진다.
- cloud-native event-driven architecture와 더 잘 맞는다.

단점:

- 순수 direct call보다 인프라가 더 복잡하다.
- 즉시 동기 완료 대신 eventual consistency를 받아들여야 한다.
- event flow debugging이 더 어려울 수 있다.

## Decision

서비스 간 통신의 선호 아키텍처 방향은 event-driven communication으로 둔다.

## Consequences

### Positive

- 서비스 간 직접 coupling이 줄어든다.
- 향후 consumer 확장성이 좋아진다.
- 이미 Blob event에 사용 중인 cloud eventing 모델과도 잘 맞는다.
- domain-event 중심 아키텍처를 더 명확히 할 수 있다.

### Negative

- event 인프라와 운영 tracing의 중요성이 커진다.
- eventual consistency를 받아들여야 한다.
- debugging에는 더 많은 도구와 규율이 필요할 수 있다.

## Follow-up Notes

이 결정은 장기적 architectural preference를 나타내는 것이지, 모든 통합을 즉시 event-driven으로 강제한다는 뜻은 아니다.

프로젝트가 진화하는 동안 특정 흐름에서는 더 실용적인 단기 방식이 선택될 수 있다. 그런 예외는 이 전체 방향을 뒤집는 것으로 보지 말고, 별도의 결정으로 명시해야 한다.

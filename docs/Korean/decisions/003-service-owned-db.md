# 003 - 서비스별 소유 데이터베이스 사용

## Status
Accepted

## Context

Settle Up은 parsing, Discord interaction, 그리고 향후 settlement 관련 기능처럼 서로 다른 책임을 가진 multi-service 시스템으로 설계된다.

이 구조에서 persistent storage ownership을 어떻게 둘지에 대한 결정이 필요했다.

1. 모든 서비스가 하나의 데이터베이스를 공유한다.
2. 각 서비스가 자신의 데이터베이스와 데이터 모델을 소유한다.

이 결정은 서비스 경계, coupling, 장기적인 스키마 진화 방식에 영향을 준다.

## Options Considered

### Option A - 서비스 간 공유 데이터베이스

장점:

- 초기 인프라가 단순하다.
- 도메인 간 직접 조회가 쉽다.
- 초기 개발 단계에서 설정이 적다.

단점:

- 서비스 간 coupling이 강해진다.
- 각 서비스가 다른 서비스의 내부 스키마에 의존하게 될 수 있다.
- 한 도메인의 스키마 변경이 다른 도메인을 깨뜨릴 수 있다.
- 일반적인 microservice 설계 원칙과 잘 맞지 않는다.

### Option B - 서비스별 소유 데이터베이스

장점:

- 서비스별 data ownership이 명확하다.
- 서비스 간 coupling이 줄어든다.
- 각 서비스가 자신의 스키마를 독립적으로 진화시킬 수 있다.
- service-oriented architecture 원칙과 잘 맞는다.

단점:

- 추가 인프라 설정이 필요하다.
- 직접 쿼리가 아니라 API나 event를 통해 데이터를 공유해야 한다.

## Decision

각 서비스가 자신의 데이터베이스를 관리하는 service-owned database model을 채택한다.

## Consequences

### Positive

- 서비스 경계가 더 명확해진다.
- 스키마 coupling이 줄어든다.
- 서비스 내부 구조를 장기적으로 더 쉽게 진화시킬 수 있다.
- 독립 배포와 변경 관리에 더 잘 맞는다.

### Negative

- 서비스 간 데이터 접근이 더 명시적이고 복잡해진다.
- 서비스가 늘어날수록 인프라 구성도 늘어날 수 있다.

## Follow-up Notes

이 모델에서는 parser service가 parsed receipt draft 데이터를 소유하고, 다른 서비스는 parser 저장소에 직접 접근하지 말고 자신의 persistence 요구를 별도로 가져야 한다.

서비스 간 통신은 공유 데이터베이스 접근이 아니라 HTTP나 event 같은 contract를 통해 이뤄져야 한다.

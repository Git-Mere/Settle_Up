# 002 - Parser를 컨테이너 서비스로 배포

## Status
Accepted

## Context

Settle Up의 receipt parsing 컴포넌트는 다음을 담당한다.

- Azure Blob Storage의 blob 생성 이벤트를 Event Grid를 통해 수신
- Azure Document Intelligence를 호출해 영수증 이미지에서 구조화된 데이터를 추출
- 추출 결과를 처리
- 파싱 결과를 저장

현재 기능은 주로 event-driven이지만, parser는 앞으로 더 많은 처리 로직과 운영용 endpoint를 포함하도록 커질 가능성이 높다.

검토한 배포 방식은 두 가지였다.

1. Azure Functions 기반의 serverless event-driven 컴포넌트
2. Azure Container Apps에 배포하는 containerized microservice

## Options Considered

### Option A - Azure Function

장점:

- Event Grid trigger와의 자연스러운 통합
- 인프라 관리가 적다.
- 이벤트 양에 따른 자동 확장
- 단순 event-driven workload에는 빠르게 구현 가능

단점:

- 기능이 더 큰 서비스로 성장할 경우 적합성이 떨어진다.
- debugging이나 manual reprocessing을 위한 추가 API를 노출하기 어렵다.
- 런타임과 호스팅 모델 제약이 더 크다.
- 장기적인 서비스 아키텍처 방향과의 정렬이 약하다.

### Option B - Containerized Microservice

장점:

- 런타임과 애플리케이션 구조를 더 자유롭게 제어할 수 있다.
- 추가 HTTP endpoint를 쉽게 노출할 수 있다.
- 전체 multi-service 아키텍처 방향과 잘 맞는다.
- 향후 처리 흐름이나 운영 기능을 확장하기 쉽다.
- `discord-api` 같은 다른 서비스와 배포 모델을 맞추기 쉽다.

단점:

- 컨테이너 이미지를 만들고 유지해야 한다.
- 순수 serverless function보다 인프라 설정이 약간 더 필요하다.

## Decision

parser는 Azure Container Apps에 배포되는 containerized service로 구현한다.

## Consequences

### Positive

- 프로젝트의 service-oriented 방향과 더 강하게 정렬된다.
- debugging, reprocessing, 운영용 endpoint 확장이 쉬워진다.
- 런타임 동작을 더 잘 제어할 수 있다.
- 저장소 내 다른 서비스와 배포 모델 일관성이 좋아진다.

### Negative

- 단순 serverless trigger보다 운영 설정이 더 필요하다.
- 컨테이너 build 및 deploy를 유지해야 한다.

## Follow-up Notes

이 결정은 receipt processing의 event-driven 성격 자체를 버리는 것이 아니다. parser는 여전히 Event Grid로 trigger될 수 있지만, 순수 function이 아니라 장시간 실행되는 서비스로 동작한다.

이렇게 하면 향후 서비스가 커질 때의 유연성을 유지하면서도 현재 event-driven 흐름과의 호환성을 보존할 수 있다.

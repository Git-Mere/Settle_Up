# 010 - Receipt 상태는 최종 Confirm 시점에만 저장

## 상태
Accepted

## 배경

별도의 `settlement-service`를 제거한 이후, `discord-api`가 구매자 선택과 최종 정산 확정을 함께 담당하게 됐다.

이 과정에서 receipt interaction 상태를 Cosmos DB에 얼마나 자주 저장할지에 대한 설계 문제가 남아 있었다.

검토한 접근은 두 가지였다.

1. 구매자 선택 변경이 발생할 때마다 즉시 저장한다.
2. 중간 선택 상태는 메모리에 두고, 최종 confirm 시점에만 저장한다.

이 결정은 write volume, concurrency risk, workflow complexity에 직접 영향을 준다.

## 검토한 선택지

### 선택지 A - 모든 중간 변경을 즉시 저장

장점:

- interaction 상태가 더 이른 시점에 durable해진다.
- 프로세스 재시작 이후 복구가 더 쉬울 수 있다.

단점:

- 데이터베이스 write 수가 크게 늘어난다.
- concurrent modification 문제가 생길 가능성이 높아진다.
- 빠른 다중 사용자 상호작용 중 구현 복잡도가 커진다.

### 선택지 B - 최종 Confirm 시점에만 저장

장점:

- Cosmos DB write 수가 줄어든다.
- interaction 단계 구현이 단순해진다.
- item selection 중 concurrent document update 위험이 낮아진다.
- 최종 write 시점이 더 명확해진다.

단점:

- 중간 상태는 durable하지 않다.
- 프로세스가 재시작되면 메모리 상 진행 상태를 잃을 수 있다.

## 결정

receipt settlement 데이터는 initiating user가 최종 `Confirm` 버튼을 눌렀을 때만 저장한다.

구매자 선택 단계에서는 다음 원칙을 따른다.

- 중간 사용자 선택 상태는 `discord-api`가 관리한다.
- 중간 변경은 Cosmos DB에 계속 저장하지 않는다.
- 최종 confirm 전에는 서버 측 validation을 반드시 수행한다.

## 결과

### 긍정적 결과

- Cosmos DB write 수가 줄어든다.
- 현재 프로젝트 단계에서 구현이 더 단순하다.
- active selection 중 concurrent document update 위험이 줄어든다.
- 최종 persistence 시점의 책임이 더 명확해진다.

### 부정적 결과

- 진행 중인 selection 상태는 durable하지 않다.
- 프로세스 재시작 시 진행 중 상호작용을 잃을 수 있다.
- 최종 confirm 시 robust한 validation이 여전히 필요하다.

## 후속 메모

confirm validation은 최소한 아래 사항을 확인해야 한다.

- 요청자가 confirm 권한이 있는지
- receipt가 이미 finalized 상태가 아닌지
- 필요한 item이 모두 할당되었는지
- 적용되어야 하는 자동 business rule이 모두 해결되었는지

향후 배포나 재시작을 넘어 진행 중 interaction 상태를 복구해야 한다면, 중간 persistence를 다시 검토할 수 있다.

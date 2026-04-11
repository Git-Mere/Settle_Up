# 009 - 별도 Settlement Service 제거

## 상태
Accepted

## 배경

초기 아키텍처에서는 구매자 선택 단계 이후에 별도의 `settlement-service`를 두는 구상이 있었다.

당시 흐름은 대략 다음과 같았다.

1. 사용자가 Discord를 통해 영수증을 업로드한다.
2. 이미지가 Blob Storage에 저장된다.
3. 파싱이 트리거된다.
4. `receipt-parser`가 draft receipt를 생성한다.
5. 사용자들이 Discord에서 item 소유자를 선택한다.
6. `settlement-service`가 최종 정산을 계산한다.
7. `settlement-service`가 결과를 저장한다.

구조를 다듬는 과정에서, 정산 동작이 Discord 상호작용 상태와 강하게 결합되어 있다는 점이 분명해졌다.

- 사용자는 Discord UI 컴포넌트로 item을 선택하고 수정한다.
- 봇은 현재 상태를 Discord 메시지에 바로 보여줘야 한다.
- 봇은 button, select, modal 상호작용에 직접 반응해야 한다.
- 최종 정산은 Discord 상호작용 흐름 안에서 직접 트리거된다.

이 때문에, 현재 단계에서 별도 서비스가 여전히 충분한 가치를 제공하는지 다시 검토하게 됐다.

## 검토한 선택지

### 선택지 A - 별도의 `settlement-service` 유지

장점:

- 정산 로직을 Discord 상호작용 코드와 더 강하게 분리할 수 있다.
- 정산 규칙이 커졌을 때 향후 분리 경로가 더 명확하다.

단점:

- 서비스 경계가 하나 더 늘어난다.
- 상태 동기화 복잡도가 증가한다.
- 배포와 디버깅 부담이 커진다.
- 상호작용 단계의 receipt 상태를 어느 서비스가 소유하는지 모호해진다.

### 선택지 B - `discord-api` 내부에서 정산 처리

장점:

- 현재 단계의 아키텍처가 더 단순해진다.
- Discord 상호작용 상태와 정산 로직을 가까이 둘 수 있다.
- 동적인 메시지 중심 워크플로를 구현하기 쉽다.
- 지금 단계에서 가치가 크지 않은 두 번째 persistence 흐름을 피할 수 있다.

단점:

- `discord-api`가 더 넓은 책임을 갖게 된다.
- 규칙이 훨씬 복잡해지면 장기적으로 다시 분리가 필요할 수 있다.

## 결정

현재는 별도의 `settlement-service`를 제거하고, 정산 워크플로 로직을 `discord-api` 내부에서 처리한다.

`receipt-parser`는 계속해서 파싱과 초기 draft receipt 생성만 담당한다.

## 결과

### 긍정적 결과

- 움직이는 구성 요소가 줄어 전체 아키텍처가 단순해진다.
- 구현과 디버깅이 쉬워진다.
- Discord 상호작용 워크플로의 책임 소유가 더 명확해진다.
- 동적인 receipt selection UI를 더 잘 지원할 수 있다.

### 부정적 결과

- `discord-api`가 interaction과 settlement concern을 함께 소유하게 된다.
- 정산 규칙이 크게 복잡해지면 향후 다시 분리가 필요할 수 있다.

## 후속 메모

receipt lifecycle은 다음과 같은 상태를 중심으로 계속 명확하게 모델링해야 한다.

- `Draft`
- `SelectionInProgress`
- `Finalized`

향후 시스템이 Discord 외 채널로 확장되거나 정산 로직이 크게 커지면, 전용 settlement service를 다시 검토할 수 있다.

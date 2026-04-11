# 012 - Receipt Session 업데이트를 직렬화하고 공개 메시지 발행을 디바운스한다

## 상태
Accepted

## 배경

`discord-api` 서비스는 현재 `ReceiptSessionState`를 통해 receipt interaction 상태를 메모리에서 관리한다.

하나의 receipt session에는 여러 Discord 사용자가 거의 동시에 접근할 수 있다.

- item selection
- add item
- remove item
- edit item
- final confirmation

현재 단계의 구현에서는 이 상호작용 핸들러들이 같은 in-memory session object를 직접 수정하고, 변경이 있을 때마다 새로운 공개 Discord 메시지를 즉시 발행할 수도 있다.

이 구조는 서로 연결된 두 가지 문제를 만들었다.

1. 하나의 receipt session 내부 concurrency risk
2. 여러 사용자가 짧은 시간 안에 상호작용할 때 과도한 공개 메시지 생성

첫 번째 문제는 현재 구현을 점검하면서 분명해졌다.

- session store는 top-level lookup에 concurrent dictionary를 사용한다.
- 하지만 저장된 각 `ReceiptSessionState` 안에는 item list, user selection map, pending edit token 같은 mutable in-memory collection이 여전히 들어 있다.
- 따라서 여러 interaction request가 같은 receipt session을 동시에 읽고 수정할 수 있다.

이 구조는 다음과 같은 race condition으로 이어질 수 있다.

- 한 사용자의 선택이 다른 업데이트를 덮어쓰거나 중간에 섞이는 경우
- item add/remove/edit와 selection update가 충돌하는 경우
- 공개 메시지 metadata가 순서 없이 갱신되는 경우
- final confirmation이 계속 변하는 in-memory state를 기준으로 평가되는 경우

두 번째 문제는 현재 공개 메시지 전략에서 온다.

- 각 interaction 뒤에 새로운 공개 메시지가 발행될 수 있다.
- 여러 사용자가 짧은 시간에 변경하면 채널에 거의 같은 상태 메시지가 여러 개 쌓일 수 있다.
- 이로 인해 채널 노이즈가 커지고, 최신 상태를 한눈에 파악하기 어려워진다.

현재 프로젝트는 각 변경을 즉시 저장하지 않고 intermediate receipt-selection state를 메모리에서 유지하고 있으므로, storage 복잡도를 성급하게 늘리지 않으면서도 안전성을 높일 수 있는 in-process 전략이 필요하다.

## 검토한 선택지

### 선택지 A - interaction마다 즉시 mutate하고 즉시 publish

장점:

- 현재 구현 스타일을 가장 단순하게 이어갈 수 있다.
- 추가 scheduling이나 synchronization 로직이 없다.
- 모든 상호작용 뒤에 공개 업데이트가 즉시 보인다.

단점:

- 같은 session에서 race condition이 계속 가능하다.
- 활발한 다중 사용자 상호작용 중 채널 노이즈가 빠르게 커진다.
- 공개 메시지 metadata가 순서 없이 갱신될 수 있다.
- 거의 동일한 공개 메시지가 반복되면서 사용자 경험이 나빠진다.

### 선택지 B - session별 업데이트를 직렬화하고 공개 발행을 디바운스

장점:

- 같은 session의 상태 변경 순서가 결정적이 된다.
- 상호작용 burst 동안 공개 메시지 수를 줄일 수 있다.
- 현재 in-memory interaction 모델과 잘 맞는다.
- 모든 변경을 즉시 저장하지 않아도 채널 가독성을 개선할 수 있다.

단점:

- 구현 복잡도가 증가한다.
- 공개 업데이트가 약간 지연된다.
- timer, cancellation, session lifecycle cleanup을 주의 깊게 다뤄야 한다.

## 결정

`discord-api`의 receipt interaction 처리에는 아래 두 가지 규칙을 함께 도입한다.

### 1. receipt session 단위로 업데이트를 직렬화한다

같은 receipt session에 대한 모든 상태 변경 작업은 동시에 처리하지 않고 순차적으로 처리한다.

이는 다음을 의미한다.

- 시스템은 receipt session identity를 기준으로 한 per-session synchronization을 적용한다.
- 하나의 receipt session에 대해서는 한 번에 하나의 mutation flow만 실행된다.
- interaction state와 public-message publication에 영향을 주는 session read/write는 모두 이 직렬화된 흐름 안에서 처리한다.

목표는 receipt session mutation을 결정적으로 만들고, 동시 Discord 상호작용 사이의 race condition을 줄이는 것이다.

### 2. 공개 메시지 발행에 2~3초 디바운스를 적용한다

공개 receipt status message는 모든 interaction마다 즉시 다시 발행하지 않는다.

대신 다음 순서를 따른다.

- 먼저 interaction state를 업데이트한다.
- session을 public refresh 필요 상태로 표시한다.
- 약 2~3초의 짧은 debounce window 뒤에 공개 발행을 수행한다.
- 그 사이 추가 변경이 들어오면 pending publish를 재조정하여 최신 상태만 발행한다.

이 디바운스 규칙은 selection, item edit 같은 routine interaction update에 적용된다.

구현 세부는 달라질 수 있지만, 의도한 제품 동작은 다음과 같다.

- 사용자 상호작용 burst는 하나의 공개 업데이트로 수렴해야 한다.
- 채널에는 모든 중간 상태가 아니라 최신의 의미 있는 상태가 보여야 한다.

final confirmation 같은 time-sensitive terminal action은 정확성이나 사용자 명확성을 위해 필요하면 일반 디바운스 경로를 우회할 수 있다.

## 결과

### 긍정적 결과

- 하나의 in-memory receipt session 내부 race condition이 줄어든다.
- 다중 사용자 Discord 상호작용의 순서 모델이 더 명확해진다.
- active channel에서 거의 동일한 공개 메시지 수가 줄어든다.
- 사용자 입장에서 settlement workflow를 더 읽기 쉽게 만든다.
- 현재 in-memory interaction 모델과 구현 방향이 잘 맞는다.

### 부정적 결과

- 모든 interaction마다 공개 업데이트가 즉시 보이지는 않는다.
- 서비스가 per-session synchronization과 publish scheduling을 관리해야 하므로 구현 복잡도가 증가한다.
- stale scheduled publish, leaked timer, mismatched session lifecycle cleanup을 피하기 위한 주의가 필요하다.
- confirm 같은 terminal action은 generic debounce path 대신 명시적 예외 처리가 필요할 수 있다.

## 후속 메모

이 결정은 현재 interaction architecture 안에서 concurrency와 message volume 문제를 다루는 것이며, 장기적인 공개 메시지 lifecycle 전략 자체를 완전히 해결하는 것은 아니다.

특히 아래 문제는 여전히 별도로 다뤄야 한다.

- 프로젝트는 여전히 하나의 명확한 공개 receipt message를 유지하는 더 깔끔한 전략이 필요하다.
- 최종 메시지 갱신 전략에서는 `50001 Missing Access` 같은 Discord channel/message access 제약도 고려해야 한다.

따라서 이 결정은 interaction safety와 channel noise 감소를 위한 조치로 봐야 하며, 공개 메시지 설계 전체의 최종 해답으로 보면 안 된다.

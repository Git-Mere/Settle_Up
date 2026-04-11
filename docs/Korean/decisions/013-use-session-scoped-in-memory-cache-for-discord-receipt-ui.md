# 013 - Discord Receipt UI를 위해 Session 범위 In-Memory Cache 사용

## 상태
Accepted

## 배경

`discord-api` 서비스는 Discord 내부에서 동작하는 interactive receipt-selection workflow를 담당한다.

receipt UI가 점점 완성되면서, 서비스는 다음 동작에 대해 공개 메시지를 자주 갱신하게 됐다.

- item selection
- add item
- remove item
- edit item
- confirm

최적화 작업 중, interaction latency의 일부가 같은 프로세스 안에서 반복되는 재계산과 메시지 재조회 작업 때문에 생긴다는 점이 드러났다.

구체적으로는 다음과 같았다.

- 서비스가 같은 session 안에서 이미 main public receipt message를 찾았더라도, 수정 전에 다시 fetch하는 경우가 있었다.
- receipt embed rendering 중에도 user-item mapping, unassigned item, settlement line, display name 같은 동일한 파생 값을 한 번의 render 안에서 반복 계산하고 있었다.

현재 receipt interaction 모델은 이미 `ReceiptSessionState`를 통해 active workflow state를 메모리에 유지하고 있으므로, 분산 캐시나 모든 UI 업데이트에 대한 durable persistence를 도입하지 않고도 반복 작업을 줄일 수 있는 실용적인 방법이 필요했다.

## 검토한 선택지

### 선택지 A - 모든 interaction마다 전부 다시 계산하고 다시 조회

장점:

- 모델이 가장 단순하다.
- 추가로 유지할 상태가 거의 없다.
- cache invalidation 문제를 피할 수 있다.

단점:

- 반복되는 Discord API lookup이 interaction latency를 키운다.
- 반복되는 파생 데이터 재계산이 rendering 중 CPU 사용을 늘린다.
- receipt 크기와 participant 수가 커질수록 성능 저하가 더 눈에 띈다.

### 선택지 B - session 범위 in-memory cache 사용

장점:

- active session 안에서 반복되는 Discord message resolution 작업을 줄인다.
- message rendering 중 반복되는 파생 데이터 계산을 줄인다.
- 이미 `discord-api`가 쓰고 있는 in-memory session 모델과 잘 맞는다.
- 외부 인프라 없이 interaction responsiveness를 개선할 수 있다.

단점:

- 캐시 값은 프로세스 로컬이며 재시작 시 사라진다.
- stale cache reference가 생길 수 있으므로 안전한 fallback 동작이 필요하다.
- 추가 session state를 조심해서 관리해야 한다.

## 결정

`discord-api` 내부에서 Discord receipt UI 최적화를 위해 session-scoped in-memory caching을 사용한다.

구체적으로는 다음을 포함한다.

- 현재 main public receipt message reference를 `ReceiptSessionState` 안에 cache한다.
- 한 번의 receipt message render 중에는 같은 파생 값을 반복 계산하지 않고 precomputed render context 데이터를 사용한다.

이 cache는 active in-memory receipt session 범위에만 속하며, durable state로 취급하지 않는다.

cached Discord message reference를 사용할 수 없거나 더 이상 유효하지 않으면, 서비스는 캐시가 항상 유효하다고 가정하지 말고 메시지를 다시 resolve해야 한다.

## 결과

### 긍정적 결과

- interactive receipt update 중 latency가 낮아진다.
- 중복 Discord message fetch가 줄어든다.
- receipt embed를 만들 때 반복되는 CPU 작업이 줄어든다.
- 큰 영수증이나 더 활발한 session에서도 응답성이 좋아진다.
- 새로운 인프라 의존성이 필요 없다.

### 부정적 결과

- cache 내용은 프로세스 재시작을 넘어 유지되지 않는다.
- cache invalidation과 fallback 동작을 올바르게 처리해야 한다.
- 매번 전부 다시 계산하는 모델보다 최적화 로직이 다소 복잡해진다.

## 후속 메모

이 결정은 현재 single-process interaction 모델에 맞춘 local in-memory optimization에 한정된다.

이는 concurrency handling의 필요성을 대체하지 않는다. session-scoped caching은 아래와 함께 사용해야 한다.

- state mutation을 위한 per-session serialization
- cached Discord object를 사용할 수 없을 때의 안전한 fallback
- 서비스가 여러 애플리케이션 인스턴스로 확장될 경우에 대한 재검토

향후 `discord-api`가 multiple instance로 실행된다면, 이 캐시는 globally authoritative state가 아니라 process-local hint로 취급해야 한다.

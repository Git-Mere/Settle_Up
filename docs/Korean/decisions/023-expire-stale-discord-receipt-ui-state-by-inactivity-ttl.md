# 023 - 비활성 TTL로 오래된 Discord Receipt UI 상태를 만료시킨다

## 상태
Accepted

## 배경

`discord-api`는 receipt가 pending 상태이거나, check 중이거나, 사용자 상호작용을 기다리는 동안 active receipt UI 상태를 메모리에 유지한다.

최근 성능 리뷰에서 오래된 상태가 남는 두 가지 별도 위험이 확인됐다.

- 사용자가 upload modal을 열어 놓고 제출하지 않으면 abandoned upload prompt interaction이 메모리에 남을 수 있다.
- 흐름이 `confirm` 또는 `cancel`까지 가지 않으면 pending 또는 active receipt session이 메모리에 무기한 남을 수 있다.

이 문제는 confirmed session retention과는 다르다. confirmed session은 이미 final confirmation 시점에 cleanup되도록 정리되어 있었다.
지금 남은 문제는 더 이상 의미 있는 live work를 나타내지 않는, 오래 살아 있는 in-progress state다.

현재 저장소에는 세 가지 종류의 in-progress state가 존재한다.

- upload prompt interaction
- pending receipt session (`IsDraftReady == false`)
- active check receipt session (`IsDraftReady == true` 이고 confirmed가 아님)

이 상태들은 모두 가치와 예상 수명이 같지 않다.

## 검토한 선택지

### 1. 명시적 사용자 행동이 있을 때까지 모든 in-progress state를 무기한 유지

장점:

- 로직이 가장 단순하다.
- 사용자가 언제든 돌아와도 상태를 잃지 않는다.

단점:

- abandoned flow가 메모리에 누적된다.
- 오래된 Discord interaction reference가 필요 이상 오래 남는다.
- 운영 관점에서 cleanup 기준이 모호해진다.

### 2. 모든 in-progress state object에 동일한 TTL 적용

장점:

- cleanup 모델이 단순하다.
- 메모리 증가를 제한할 수 있다.

단점:

- pending upload state와 active check state의 현실적인 수명이 다르다.
- 짧은 단일 TTL은 실제 check session에 너무 공격적이다.
- 긴 단일 TTL은 명백히 abandoned된 upload flow에 너무 관대하다.

### 3. 상태 유형별로 다른 inactivity TTL 적용

장점:

- abandoned flow와 active receipt session을 다르게 다룰 수 있다.
- 정상적인 active work를 지나치게 취약하게 만들지 않으면서 메모리 사용량을 제한할 수 있다.
- cleanup 정책이 더 명시적이고 설명 가능하다.

단점:

- 정책 로직이 늘어난다.
- background cleanup sweep이 필요하다.
- 사용자 직접 행동 없이도 오래된 공개 메시지가 삭제될 수 있다.

## 결정

상태 유형별로 다른 기준을 적용하는 inactivity-based TTL cleanup을 사용한다.

선택한 TTL:

- abandoned upload prompt interaction: 15분
- pending receipt session: 15분
- active check receipt session: 6시간

TTL은 receipt session의 경우 `UpdatedAtUtc`, upload prompt interaction의 경우 creation time을 기준으로 계산한다.

cleanup 동작:

- pending debounced refresh 취소
- 가능하면 오래된 private panel response 삭제
- 가능하면 오래된 public pending/check message 삭제
- in-memory session을 session store에서 제거
- session-scoped lock entry cleanup

confirmed session은 이미 confirm flow 중 cleanup되므로 이 TTL 정책 대상이 아니다.

## 결과

긍정적 결과:

- abandoned state가 무한정 증가하지 않는다.
- upload와 pending flow를 더 공격적으로 정리할 수 있다.
- active check session에도 사용자가 돌아올 수 있는 합리적인 시간이 남는다.
- Discord interaction/message reference의 메모리 유지 시간이 줄어든다.

부정적 결과:

- 사용자는 6시간 동안 상호작용이 없으면 진행 중이던 check session을 잃을 수 있다.
- 공개 receipt message가 명시적 사용자 행동 없이 background cleanup으로 사라질 수 있다.
- cleanup이 hosted background sweep loop에 의존하게 된다.

## 후속 메모

- 현재 cleanup interval은 1분이다.
- 사용자 피드백상 6시간이 너무 짧거나 길다면, 코드의 TTL을 조정하고 이 결정 문서는 differentiated inactivity TTL을 쓰는 근거로 유지한다.
- 이 결정은 `docs/problem-searching/performance-review-2026-04-07.md`의 성능 리뷰와 밀접하게 연결된다.

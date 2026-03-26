# 018 - Define History Index As Current Recency Order

## Status
Accepted

## Context
`discord-api`는 `/history` 명령어를 통해 사용자가 과거 confirm 결과를 조회할 수 있도록 할 계획이다.

현재 합의된 기본 UX는 다음과 같다.

- `/history`는 최근 30개의 정산 결과를 간략하게 보여준다.
- 각 항목에는 `1`부터 `30`까지 번호를 붙인다.
- 사용자가 `index`를 지정하면 해당 항목의 상세 정보를 다시 보여준다.

이때 `index`의 의미를 어떻게 정의할지 결정이 필요했다.

가능한 해석은 두 가지였다.

- 해석 A:
  사용자가 “방금 본 목록” 기준의 1번, 2번, 3번
- 해석 B:
  현재 시점 기준 최신순으로 정렬했을 때의 1번, 2번, 3번

해석 A를 쓰려면 목록 결과를 사용자별로 메모리에 잠깐 저장하고, 이후 상세 조회에서 그 캐시를 다시 참조해야 한다.

해석 B를 쓰면 매번 Cosmos DB에서 같은 정렬 기준으로 다시 조회해서 현재 시점의 `n`번째 결과를 선택하면 된다.

사용자 의도는 “최신 것이 1번”이었다. 따라서 index는 “방금 본 목록의 위치”가 아니라 “현재 시점에서 최신순으로 정렬한 순번”으로 보는 것이 더 맞다.

## Options Considered
1. `/history` 목록 결과를 사용자별로 메모리에 저장하고 index를 그 캐시 기준으로 해석
- 사용자가 방금 본 목록과 상세 조회 결과가 정확히 대응된다.
- 하지만 사용자별 history 목록 캐시, TTL, 덮어쓰기, 재시작 시 초기화 같은 관리가 필요하다.
- 현재 프로젝트 요구사항에 비해 구현 복잡도가 올라간다.

2. index를 현재 시점 기준 최신순 순번으로 정의하고, 상세 조회 때마다 DB에서 다시 조회
- 캐시가 필요 없다.
- 구현이 단순하고, 서버 재시작과 무관하게 항상 동작한다.
- 중간에 새 history가 생기면 번호 의미가 바뀔 수 있지만, 그 자체가 의도된 동작이다.

## Decision
`/history`의 index는 **현재 시점 기준 최신순 순번**으로 정의한다.

즉:

- `index:1` = 현재 시점에서 가장 최근 confirmed settlement
- `index:2` = 현재 시점에서 두 번째로 최근 confirmed settlement
- `index:30` = 현재 시점에서 서른 번째로 최근 confirmed settlement

이 정의에 따라:

- `/history` 목록은 `confirmedAtUtc DESC` 기준으로 최근 30개를 조회한다.
- 상세 조회도 동일하게 `confirmedAtUtc DESC` 기준으로 다시 조회한다.
- 상세 조회를 위해 `/history` 목록 결과를 별도 캐시하지 않는다.

## Consequences
긍정적 결과:

- 구현이 단순하다.
- 사용자별 in-memory history cache가 필요 없다.
- 서버 재시작이나 캐시 만료와 무관하게 일관된 규칙으로 동작한다.
- 최신순 조회 정책과 index 의미가 직접적으로 연결된다.

부정적 결과 및 비용:

- 사용자가 목록을 본 뒤 새로운 confirm이 생기면 번호 의미가 바뀔 수 있다.
- 즉, “아까 본 1번”과 “지금의 1번”이 다를 수 있다.
- 하지만 이것은 현재 의도된 정책이다.

## Follow-up Notes
- `/history`의 기본 쿼리는 `uploadedByUserId == currentUserId` 와 `confirmedAtUtc DESC` 정렬을 사용한다.
- 목록 결과는 간략 요약, 상세 결과는 confirm 메시지와 유사한 구조를 사용하는 방향을 따른다.
- 이후 “방금 본 목록 기준 index” UX가 필요해지면, 사용자별 history browse context cache를 별도 decision으로 추가할 수 있다.

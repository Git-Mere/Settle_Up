# 017 - Confirm Receipt Before History Persistence Completes

## Status
Accepted

## Context
`discord-api`는 confirm 시점에 settlement history snapshot을 Cosmos DB에 저장하도록 확장됐다.

초기 구현에서는 confirm 버튼을 누르면 다음 순서로 동작했다.

1. history 문서를 Cosmos DB에 저장
2. 저장이 성공해야만 공개 메시지를 confirmed 상태로 갱신

이 방식은 데이터 보존 관점에서는 단순했지만, 사용자 경험에 문제가 있었다.

- Cosmos write latency가 confirm UX 지연으로 직접 이어졌다.
- DB 응답이 느리거나 일시 오류가 있으면 confirm 버튼은 눌렸는데 화면이 바로 전환되지 않았다.
- 사용자는 “confirm이 먹지 않았다”고 느끼기 쉬웠다.

현재 receipt UI에서 더 중요한 것은 사용자가 confirm 클릭 후 즉시 확정 상태를 보는 것이다. history 저장은 중요하지만, confirm UI 자체를 블로킹하는 작업으로 두면 전체 상호작용 품질이 나빠진다.

## Options Considered
1. history 저장이 끝난 뒤에만 confirm UI를 전환
- 구현은 직관적이다.
- history 저장 실패 시 confirm도 함께 막을 수 있다.
- 하지만 DB latency와 장애가 confirm UX를 직접 악화시킨다.

2. confirm UI를 먼저 전환하고, history 저장은 뒤에서 비동기 처리
- 사용자는 즉시 confirmed 메시지를 본다.
- DB 지연이 confirm UX에 직접 반영되지 않는다.
- 대신 confirm은 성공했는데 history 저장은 실패할 수 있다.

3. confirm UI를 먼저 전환하고, history 저장 실패 시 retry 후 사용자에게 안내
- 2번의 UX 장점을 유지한다.
- 단순 fire-and-forget보다 운영 가시성이 낫다.
- 최종 실패 시 사용자가 history 누락 가능성을 인지할 수 있다.

## Decision
confirm 버튼을 누르면 **공개 메시지를 먼저 confirmed 상태로 갱신**하고, settlement history 저장은 **background task로 비동기 처리**한다.

history 저장 정책은 다음과 같다.

- confirm UI는 Cosmos write 완료를 기다리지 않는다.
- history 저장은 background에서 수행한다.
- 저장 실패 시 최대 2회 retry한다.
- 총 시도 횟수는 최초 1회 + retry 2회다.
- 최종 실패 시 에러 로그를 남긴다.
- 최종 실패 시 사용자에게 ephemeral follow-up으로 `history등록에 실패했습니다.` 메시지를 보낸다.

즉, confirm UX는 우선적으로 보장하고, history persistence는 best-effort + retry 방식으로 처리한다.

## Consequences
긍정적 결과:

- confirm 버튼에 대한 체감 응답 속도가 좋아진다.
- Cosmos 지연이나 일시 오류가 공개 메시지 확정 전환을 막지 않는다.
- 사용자는 confirm이 정상 동작했는지 바로 확인할 수 있다.
- retry와 실패 안내를 통해 history 저장 실패를 완전히 숨기지 않는다.

부정적 결과 및 비용:

- confirm 완료와 history 저장 성공이 분리된다.
- DB가 반복 실패하면 confirmed 메시지는 남지만 history 문서는 누락될 수 있다.
- background save retry와 follow-up failure 처리 로직이 추가된다.

## Follow-up Notes
- 현재 retry 정책은 고정 2회이며, 필요하면 later session에서 exponential backoff나 queue 기반 재처리로 확장할 수 있다.
- 장기적으로 history 저장 누락 재처리까지 필요해지면 outbox/retry queue 성격의 별도 설계를 검토할 수 있다.
- 이 결정은 confirm UX를 우선시하는 현재 Discord interaction 특성에 맞춘 것이다.

# 021 - Add A Custom Manual Settlement Entrypoint With A Blank Receipt Session

## Status
Accepted

## Context
현재 `discord-api`의 기본 정산 시작점은 receipt image 업로드와 parser callback이다.

하지만 실제 사용 흐름에서는 다음 같은 경우가 있다.

- 영수증 이미지 없이 바로 정산하고 싶은 경우
- OCR 결과를 기다리지 않고 사용자가 직접 item을 추가하고 싶은 경우
- parser 품질과 무관하게 수동 정산을 시작하고 싶은 경우

기존 UI는 add/remove/edit/select/confirm 흐름이 이미 준비돼 있기 때문에, 빈 receipt session만 만들 수 있으면 parser 없이도 같은 정산 UX를 재사용할 수 있다.

이번 기능의 핵심은 "영수증 파싱 없이도 공개 check 메시지를 만들고 owner가 직접 채워 넣는 시작점"을 만드는 것이다.

## Options Considered
1. `/settle-up`만 유지하고 수동 정산은 지원하지 않는다
- 흐름이 단순하다.
- 하지만 영수증 없는 정산, OCR 우회, 단순 공유 비용 정산에 대응하지 못한다.

2. `/custom`으로 빈 draft를 만들고 기존 receipt UI를 재사용한다
- 기존 add/remove/edit/select/confirm 흐름을 그대로 활용할 수 있다.
- parser 없이도 같은 settlement UX를 제공할 수 있다.
- 초기값과 confirm 가능 조건만 명확히 정하면 구현이 단순하다.

3. `/custom` 전용으로 완전히 다른 UI를 새로 만든다
- 수동 정산 전용 경험을 세밀하게 설계할 수 있다.
- 하지만 기존 receipt UI와 기능이 중복되고 유지보수 비용이 늘어난다.

## Decision
`/custom` slash command를 추가해 parser 없이 빈 receipt session을 바로 생성한다.

초기 session 값은 다음과 같다.

- `Seller Name`: `Custom`
- `Purchase Date`: 명령 실행 시점 날짜
- `Buyer Name`: 명령 실행자
- `Item Total Price`, `Tax`, `Tip`, `SST`, `SLT`, `Total Price`: 모두 `0`
- item 목록: 비어 있음

명령 옵션 정책은 다음과 같다.

- `payment_contact`는 optional string option으로 받는다.
- 값이 있으면 이후 confirm UI의 `Pay to`에 사용한다.
- 값이 없으면 비워 둔다.

동작 정책은 다음과 같다.

- `/custom` 실행 직후 공개 check 메시지를 생성한다.
- 이후 owner가 `Add item` 등 기존 receipt 조작 기능으로 내용을 채운다.
- 빈 상태에서는 confirm을 허용하지 않는다.
- 최소 1개 이상의 item이 존재하고, 기존 규칙대로 모든 item 배정이 완료됐을 때만 confirm 가능하다.
- 언어는 현재 `/language` 정책을 그대로 따른다.

## Consequences
긍정적 결과:

- parser 없이도 곧바로 settlement를 시작할 수 있다.
- 영수증 없는 비용 분담과 OCR 우회 시나리오를 지원한다.
- 기존 receipt UI와 상호작용 로직을 재사용해 구현 범위를 작게 유지할 수 있다.

부정적 결과 및 비용:

- confirm 전까지는 기존 receipt session처럼 메모리 상태에만 존재한다.
- 봇 재시작 시 진행 중 `/custom` 세션은 사라진다.
- parser 기반 draft와 달리 item 입력을 owner가 직접 해야 한다.

## Follow-up Notes
- `/custom` confirmed 결과는 일반 receipt와 동일하게 confirm 이후 history 저장 경로를 사용한다.
- 현재는 수동 정산 진입점만 추가하며, parser draft와 별도의 영구 저장 경로를 만들지 않는다.
- 필요하면 이후 `Custom` seller name, 기본 currency, 초기 tax/tip 편집 UX를 다시 조정할 수 있다.

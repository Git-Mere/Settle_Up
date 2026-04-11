# 015 - Store Confirmed Settlement History In A Discord-Api-Owned Cosmos Container

## Status
Accepted

## Context
`receipt-parser`는 현재 영수증 parse 결과 초안을 Cosmos DB에 저장하고 있다. 이 데이터는 parser가 OCR/정규화 결과를 관리하고 `discord-api`로 draft를 전달하기 위한 서비스 내부 데이터다.

이제 `discord-api`는 confirm 시점의 핵심 정산 결과를 영구 저장하려고 한다. 이 데이터는 이후 `/history` 명령어에서 과거 정산 결과를 조회하는 데 사용될 예정이다.

이 저장 데이터는 `receipt-parser`가 보관하는 draft parse 문서보다 더 간략하고, 확정된 정산 결과 중심이어야 한다.

즉, 이번에 저장하려는 것은 다음과 같은 성격의 데이터다.

- parse 중간 산출물이 아님
- confirm 이후의 user-facing settlement snapshot임
- Discord UI와 history 조회를 위한 읽기 모델임
- 소유 서비스는 `receipt-parser`가 아니라 `discord-api`임

기존 결정 [003-service-owned-db.md](/home/aero-mere/CS397/Settle_Up/docs/decisions/003-service-owned-db.md) 에서는 다음을 이미 채택했다.

- `We will adopt a service-owned database model where each service manages its own database.`
- `the parser service owns parsed receipt draft data, while other services should own their own persistence needs rather than relying on direct access to parser-owned storage.`

따라서 confirm history 저장 위치를 정할 때도, parser-owned 저장소에 그대로 얹는 방식이 적절한지 다시 판단해야 했다.

## Options Considered
1. `receipt-parser`가 현재 쓰는 container에 confirm history까지 함께 저장
- 초기 구현은 가장 빠를 수 있다.
- 하지만 parser draft 데이터와 confirmed settlement history가 한 container에 섞이게 된다.
- 데이터 owner가 달라지고 스키마 책임도 섞여 service boundary가 약해진다.
- `/history` 조회 요구가 parser 내부 문서 구조에 종속될 위험이 있다.

2. 같은 Cosmos account와 같은 database를 사용하되, `discord-api` 전용 container를 추가
- 인프라를 새로 크게 늘리지 않고도 소유권을 분리할 수 있다.
- parser와 discord-api가 같은 account와 database를 써도, container 단위로 책임 경계를 유지할 수 있다.
- history 조회 모델을 parser draft 문서와 독립적으로 설계할 수 있다.
- 현재 단계에서는 운영 복잡도와 서비스 분리 원칙의 균형이 가장 좋다.

3. 같은 account 안에 `discord-api` 전용 database와 container를 모두 새로 추가
- database 단위 분리가 더 명확한 경계를 줄 수 있다.
- 하지만 현재 단계에서는 같은 database 안에 container만 분리해도 충분하다.
- 운영/설정 복잡도가 조금 더 커진다.

4. `discord-api`용으로 완전히 새로운 Cosmos account를 추가
- 보안, 비용, 네트워크, 백업 정책을 강하게 분리해야 하면 장점이 있다.
- 하지만 현재 단계에서는 인프라 비용과 운영 복잡도가 크다.
- 저장하려는 데이터가 핵심 history snapshot이긴 하지만, 아직 완전 별도 account까지 필요한 규모나 요구사항은 아니다.

## Decision
confirm 이후 settlement history는 **기존 Cosmos account와 기존 database를 그대로 사용하되, `discord-api` 전용 새 container를 만들어 저장**한다.

구체적 방향은 다음과 같다.

- `receipt-parser`의 기존 draft container는 그대로 parser가 소유한다.
- `discord-api`는 같은 Cosmos account, 같은 database 안에서 자신이 소유하는 history container를 사용한다.
- confirmed settlement history는 parser container에 저장하지 않는다.
- `/history` 조회는 `discord-api`가 소유한 history 문서를 기준으로 동작한다.

이 결정은 다음 균형을 의도한다.

- 새 account나 새 database까지 만들지 않아 초기 운영 복잡도를 낮춘다.
- parser container 재사용으로 service boundary를 무너뜨리지 않는다.
- history 조회 모델을 `discord-api` 요구사항에 맞게 간결하게 설계한다.

## Consequences
긍정적 결과:

- `receipt-parser` draft 데이터와 `discord-api` confirmed history 데이터의 ownership이 분리된다.
- `/history` 기능이 parser 내부 스키마에 종속되지 않는다.
- 같은 account와 database를 재사용하므로 초기 배포/운영 부담이 비교적 작다.
- container별 인덱싱, TTL, partition key, RU 전략을 history 용도에 맞게 따로 잡을 수 있다.

부정적 결과 및 비용:

- 같은 database 안에서도 container를 추가로 관리해야 한다.
- `discord-api` 쪽에 별도 Cosmos 접근 설정과 저장 코드가 추가된다.
- account는 공유하므로 완전한 인프라 격리는 아니다.

## Follow-up Notes
- history 문서는 parser draft 문서보다 더 간략한 confirm snapshot이어야 한다.
- 예상 저장 정보는 `receiptId`, owner/user identifiers, merchant name, confirm timestamp, money summary, participant totals, participant item summary 등 `/history` 조회에 필요한 핵심 필드 중심으로 설계한다.
- partition key와 조회 패턴은 `/history`의 실제 UX를 기준으로 별도 정리할 수 있다.
- 현재 방향에서는 parser와 같은 database를 쓰더라도 container ownership은 `discord-api`에 있다는 점을 코드와 문서에서 분명히 유지해야 한다.
- 보안/비용/운영 요구가 커지면, 이후 새 Cosmos account 분리를 별도 decision으로 다시 검토할 수 있다.

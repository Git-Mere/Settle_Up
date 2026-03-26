# 016 - Define Confirmed Settlement History Document And History Query Policy

## Status
Accepted

## Context
`discord-api`는 confirm 시점의 정산 결과를 영구 저장하고, 이후 `/history` 명령어로 과거 정산 결과를 조회할 수 있도록 할 계획이다.

이 데이터는 `receipt-parser`가 저장하는 draft parse 문서와 목적이 다르다.

- parser 문서는 OCR/정규화 결과와 delivery 상태를 위한 서비스 내부 데이터다.
- history 문서는 confirm 이후의 최종 정산 결과를 다시 보여주기 위한 user-facing snapshot이다.

따라서 history 문서는 parser 원문 중심 구조가 아니라, `discord-api`의 confirm UI와 조회 요구사항에 맞는 간결한 읽기 모델이어야 한다.

또한 `/history`의 첫 번째 사용 시나리오는 “해당 사용자가 예전에 했던 정산 결과 보기”다. 따라서 저장 구조와 partition key도 이 조회 패턴에 맞춰야 했다.

## Options Considered
1. parser draft와 비슷한 상세 문서를 history에도 그대로 저장
- 구현은 일부 필드 재사용이 쉬울 수 있다.
- 하지만 `/history`에서 필요한 것보다 데이터가 너무 많아진다.
- confirm 이후 보여줄 핵심 정보보다 parser 내부 표현에 더 종속된다.

2. confirm 결과에 필요한 핵심 필드만 담은 snapshot 문서를 저장
- `/history`에 필요한 정보만 간결하게 보관할 수 있다.
- confirm 시점 UI와 거의 같은 형태로 읽기 모델을 만들 수 있다.
- parser draft 스키마와 독립적으로 진화할 수 있다.

3. `/history`를 guild 전체 조회 중심으로 설계하고 `guildId`를 주 partition key로 사용
- 서버 단위 탐색에는 유리할 수 있다.
- 하지만 현재 우선 요구사항인 “해당 유저가 한 정산 결과 보기”에는 덜 직접적이다.

4. `/history`를 uploader 중심 조회로 설계하고 `uploadedByUserId`를 partition key로 사용
- 현재 요구사항에 가장 직접적으로 맞는다.
- 한 사용자의 과거 정산 결과를 시간순으로 조회하기 쉽다.
- 이후 guild 단위 탐색이 필요해지면 별도 query/index 전략을 추가로 설계할 수 있다.

## Decision
confirmed settlement history는 **confirm 시점의 핵심 정산 정보를 담은 snapshot 문서**로 저장한다.

문서 구조는 다음 원칙을 따른다.

- confirm 시점의 핵심 결과만 저장한다.
- parser raw result나 draft 내부 표현은 history 문서에 그대로 복사하지 않는다.
- 사용자에게 다시 보여줄 값은 현재 confirm 공개 메시지와 유사한 형태로 저장한다.
- participant별 최종 금액과 선택 아이템 요약을 함께 저장한다.

추천 문서 형태는 아래와 같다.

```json
{
  "id": "history_01JXYZ...",
  "type": "confirmed_settlement",
  "receiptId": "receipt_abc123",
  "guildId": "123456789012345678",
  "channelId": "123456789012345679",
  "messageId": "123456789012345680",

  "uploadedByUserId": "111111111111111111",
  "uploadedByDisplayName": "mere",
  "merchantName": "Sunset Diner",
  "transactionDate": "2026-03-12",
  "currency": "USD",

  "subtotal": 42.00,
  "tax": 3.36,
  "sst": null,
  "slt": null,
  "tip": 8.40,
  "total": 53.76,
  "tipSplitMode": "Proportional",

  "paymentContact": "zelle@example.com",

  "participants": [
    {
      "userId": "111111111111111111",
      "displayName": "mere",
      "amount": 18.20,
      "taxAmount": 1.44,
      "sstAmount": 0.00,
      "sltAmount": 0.00,
      "tipAmount": 3.20,
      "items": [
        { "name": "Burger", "quantity": 1, "amount": 14.00, "isAlcohol": false },
        { "name": "Fries", "quantity": 1, "amount": 5.00, "isAlcohol": false }
      ]
    }
  ],

  "confirmedAtUtc": "2026-03-12T21:15:00Z",
  "createdAtUtc": "2026-03-12T21:15:00Z",
  "updatedAtUtc": "2026-03-12T21:15:00Z"
}
```

UI/조회 정책은 다음과 같이 정한다.

- `/history`는 현재 단계에서 **해당 유저가 한 정산 결과만** 조회한다.
- 즉 기본 조회 기준은 `uploadedByUserId == currentUserId` 이다.
- history 결과를 다시 보여줄 때는 현재 confirm 공개 메시지와 유사한 형태를 사용한다.
- `SST`, `SLT`, `Tip`은 값이 있는 경우에만 결과에 포함한다.
- 값이 없는 경우 confirm UI와 동일하게 숨긴다.

partition key는 **`/uploadedByUserId`** 로 한다.

이 선택은 현재 `/history`의 주 사용 시나리오인 “내가 이전에 확정한 정산 결과 보기”에 가장 직접적으로 맞는다.

## Consequences
긍정적 결과:

- history 문서가 confirm UI와 가까운 형태라 조회 렌더링이 단순해진다.
- parser draft와 분리된 snapshot이므로 history가 parser 내부 스키마 변화에 덜 영향을 받는다.
- `/history`의 1차 사용자 경험인 “내 기록 보기”에 맞는 partition key를 사용하게 된다.
- `SST`, `SLT`, `Tip`이 있는 경우에만 노출하는 현재 UI 정책을 저장/조회에도 일관되게 유지할 수 있다.

부정적 결과 및 비용:

- guild 전체 history 탐색은 현재 구조에서 1차 최적화 대상이 아니다.
- confirm 시점 snapshot을 별도로 만들고 저장하는 코드가 필요하다.
- confirm UI와 history UI가 함께 진화할 경우, snapshot 필드와 렌더링 정책을 같이 관리해야 한다.

## Follow-up Notes
- 이 결정은 `015-store-confirmed-settlement-history-in-a-discord-api-owned-cosmos-container.md`와 함께 본다.
- history 문서는 기존 Cosmos account와 기존 database를 재사용하되, `discord-api`가 소유하는 별도 history container에 저장하는 전제를 따른다.
- history 문서 저장 시 `discord-api`의 confirm 렌더링에 필요한 participant item summary를 함께 생성해야 한다.
- `/history` 초기 구현은 uploader 기준 목록 조회 + 단건 상세 재표시 흐름으로 시작하는 것이 적절하다.
- 이후 guild-wide history, search, filters가 필요해지면 새로운 query/index 전략을 별도 decision으로 추가할 수 있다.

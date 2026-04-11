# 022 - Treat General Tax On KRW Receipts As Tax Included

## Status
Accepted

## Context
`discord-api`는 draft receipt를 받은 뒤 item 금액 합계와 tax/tip 값을 바탕으로 최종 정산 금액을 다시 계산한다.

현재 일반 정책은 다음과 같다.

- item 금액은 subtotal로 사용한다.
- 일반 tax는 item 구매 비율대로 배분한다.
- tip과 특수 세금(`SST`, `SLT`)도 별도 항목으로 계산한다.

이 정책은 미국 영수증처럼 세금이 item 가격과 별도로 표시되는 경우에는 맞지만, 한국 영수증에는 그대로 적용하기 어렵다.

한국 영수증에서는 상품 가격에 이미 부가세가 포함된 상태인 경우가 많다. 그런데 OCR 결과가 `Tax` 또는 `TaxDetails`를 별도 금액으로 추출하면, 현재 UI에서는 다음 문제가 발생한다.

- item 금액에는 이미 세금이 포함돼 있다.
- `discord-api`가 일반 tax를 다시 더해 총액을 계산한다.
- 결과적으로 참여자 정산 시 tax가 한 번 더 붙는다.

이 문제는 단순히 tax UI를 숨기는 것만으로 해결되지 않는다. 계산 경로에서 tax가 여전히 포함되면 실제 정산 금액은 계속 왜곡된다.

## Options Considered
1. 모든 국가에 같은 일반 tax 정책 유지
- 구현은 단순하다.
- 하지만 한국 영수증에서 세금 이중 과금이 발생한다.

2. 한국 영수증에서도 tax UI만 숨기고 계산은 유지
- 시각적으로는 덜 어색해질 수 있다.
- 하지만 실제 settlement 금액은 여전히 잘못된다.

3. `CountryRegion`이나 별도 tax policy 필드를 parser에서 내려주고, 그에 따라 tax 포함 여부를 결정
- 장기적으로 가장 명시적인 구조다.
- 하지만 지금 당장 필요한 수정에 비해 payload/계약 변경 범위가 커진다.

4. `KRW` receipt는 일반 tax를 tax-included로 보고 `discord-api`에서 `Tax=0`으로 정규화
- 현재 parser payload만으로 즉시 적용 가능하다.
- 계산과 UI를 함께 바로잡을 수 있다.
- 국가/세금 정책을 currency에 의존하는 단순화가 들어간다.

## Decision
현재는 `Currency == KRW`인 draft receipt에 대해 일반 `Tax`를 tax-included로 간주한다.

적용 방식:

- draft payload를 session으로 만들 때 일반 `Tax`를 `0`으로 정규화한다.
- draft payload를 기존 pending session에 다시 적용할 때도 같은 정규화를 수행한다.
- `SST`, `SLT`, `Tip`은 이 결정으로 자동 변경하지 않는다.

결과적으로:

- KRW receipt는 일반 tax가 총액 계산에 다시 더해지지 않는다.
- KRW receipt는 tax header와 tax section이 표시되지 않는다.
- item 금액은 한국 영수증에서 실제 결제 금액에 더 가까운 기준으로 유지된다.

## Consequences
긍정적 결과:

- 한국 영수증에서 일반 tax 이중 과금을 막을 수 있다.
- UI와 실제 settlement 계산이 함께 일관되게 수정된다.
- parser payload 계약을 당장 바꾸지 않고도 빠르게 문제를 해결할 수 있다.

부정적 결과 및 비용:

- 일반 tax 포함 여부를 currency만으로 추론한다.
- `KRW`이지만 예외적으로 세금을 별도 더해야 하는 문서가 있으면 현재 정책으로는 표현하기 어렵다.
- 장기적으로는 tax treatment를 payload 수준에서 더 명시적으로 다루는 설계가 필요할 수 있다.

## Follow-up Notes
- 이 결정은 현재 한국 영수증에서 발견된 실제 이중 과금 문제를 막기 위한 우선 정책이다.
- 장기적으로는 parser가 `CountryRegion` 또는 `TaxTreatment` 같은 명시적 필드를 보내는 방향을 재검토할 수 있다.
- 이 결정은 일반 tax에만 적용하며, `Tip`, `SST`, `SLT` 처리 정책을 자동으로 바꾸지는 않는다.

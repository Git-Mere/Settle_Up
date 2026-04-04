# 019 - Attribute Negative Receipt Lines To The Previous Item And Ignore Unmatched Discounts

## Status
Accepted

## Context
`receipt-parser`가 OCR 결과를 `discord-api` draft payload로 넘길 때, 영수증의 할인 줄이 일반 item처럼 들어오는 문제가 있었다.

대표적인 예시는 다음과 같았다.

- 상품 줄 아래에 `-6.00` 같은 할인 줄이 따로 존재
- OCR 결과가 이 줄을 별도 item으로 추출
- `discord-api`는 item 합계를 기준으로 subtotal과 total을 다시 계산

이 상태로 draft를 만들면 할인 줄이 "음수 item"으로 정산 UI에 들어가게 된다.

그 결과:

- 공개 check UI에 할인 줄이 item처럼 보일 수 있다.
- 참여자 선택 대상에도 섞일 수 있다.
- add/remove/edit 이후 재계산 시 총액이 왜곡될 수 있다.

현재 `discord-api`의 실제 사용자 흐름에서는 "어떤 상품이 얼마 할인됐는지"가 중요하다. 단순히 receipt-level discount 총합만 보여주면, 할인 혜택이 어느 item에 붙었는지 알기 어렵다.

동시에 OCR 품질 특성상 모든 할인 줄을 정확히 상품에 귀속하는 것은 보장할 수 없다.

## Options Considered
1. 음수 금액 줄을 일반 item으로 그대로 유지
- 구현은 가장 단순하다.
- 하지만 할인 줄이 정산 대상 item처럼 섞여 subtotal/total 재계산을 망친다.
- 실제 사용자 경험이 나쁘다.

2. 음수 금액 줄을 모두 receipt-level discount 총합으로만 처리
- 총액 왜곡은 막을 수 있다.
- 하지만 어떤 item이 할인됐는지 UI에서 사라진다.
- item-level settlement 관점에서 정보 손실이 크다.

3. 음수 금액 줄을 우선 직전 일반 item에 귀속하고, 매칭 실패 할인은 별도 `Unattributed Discount`로 표시 및 차감
- 대부분의 마트형 영수증 패턴에 잘 맞는다.
- item-level 할인 표시가 가능하다.
- 다만 OCR 순서가 틀리거나 전역 할인인 경우 `Unattributed Discount` UX와 계산 정책을 추가로 가져가야 한다.

4. 음수 금액 줄을 우선 직전 일반 item에 귀속하고, 매칭 실패 할인은 적용하지 않는다
- 가장 흔한 할인 패턴을 간단하게 처리할 수 있다.
- 잘못된 전역 차감을 피할 수 있다.
- 매칭 실패 할인은 자동 반영되지 않지만, 사용자가 `Edit item`으로 수동 보정할 수 있다.

## Decision
음수 금액 receipt line은 다음 정책으로 처리한다.

- 일반 item으로 draft에 넣지 않는다.
- 먼저 "직전 일반 item"에 할인으로 귀속을 시도한다.
- 귀속에 성공하면 해당 item의 할인으로 누적한다.
- 같은 item 아래 할인 줄이 연속으로 나오면 모두 같은 item에 누적한다.
- 귀속에 실패한 할인은 `discord-api` 계산과 UI에서 적용하지 않는다.

UI 정책은 다음과 같다.

- 할인 귀속 성공 item은 한 줄 요약 형태로 표시한다.
- 예: `Protein Bar - $3.50 (discount -$1.00)`
- 귀속 실패 할인은 별도 `Unattributed Discount` 섹션을 두지 않는다.

계산 정책은 다음과 같다.

- item-level 정산 금액은 할인 반영 후 net amount를 기준으로 한다.
- `Item Total Price`는 할인 적용 후 item 합계다.
- 귀속 실패 할인은 총액에서 추가 차감하지 않는다.

## Consequences
긍정적 결과:

- 할인 줄이 일반 정산 item으로 섞이는 문제를 막을 수 있다.
- 대부분의 영수증에서 사용자가 기대하는 "바로 위 item 할인" 패턴을 반영할 수 있다.
- 공개 UI에서 할인 정보를 item 맥락과 함께 보여줄 수 있다.
- 잘못 매칭한 할인이나 OCR 순서 오류 때문에 전체 total을 잘못 차감하는 위험을 줄일 수 있다.

부정적 결과 및 비용:

- 직전 item 규칙만으로는 모든 할인 형태를 정확히 처리할 수 없다.
- 상품명 매칭, 위치 기반 매칭, 전역 쿠폰 등은 현재 다루지 않는다.
- 귀속 실패 할인은 자동 반영되지 않으므로 사용자가 `Edit item`으로 수동 보정해야 할 수 있다.

## Follow-up Notes
- 현재는 `receipt-parser`와 `discord-api`가 함께 item-level discount 필드를 사용한다.
- `/test` debug scenario로 할인 1회 및 같은 item에 대한 stacked discount 케이스를 재현할 수 있다.
- 장기적으로 OCR 품질 때문에 귀속 실패가 자주 보이면, 상품명 매칭이나 전역 discount 정책을 별도 decision으로 다시 검토할 수 있다.
- 이 결정은 public receipt UI가 item 중심 정산 흐름이라는 현재 UX를 전제로 한다.

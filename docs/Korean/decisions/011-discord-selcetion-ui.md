# 011 - 병합된 Item UI와 균등 분할, Pagination 사용

## 상태
Accepted

## 배경

Discord 기반 receipt settlement workflow에는 여러 사용자가 어떤 receipt item에 참여했는지 선택할 수 있는 UI가 필요하다.

설계에는 다음 제약이 영향을 줬다.

- Discord select menu에는 option 수 제한이 있다.
- 실제 영수증에는 같은 item line이 반복되는 경우가 많다.
- 하나의 item을 여러 사용자가 함께 소비할 수 있다.
- UI는 Discord 메시지 형식 안에서도 읽기 쉬워야 한다.
- 사용자가 정산 동작을 이해할 수 있어야 한다.

초기에는 모든 OCR line item을 그대로 개별 표시하는 방식을 고려했지만, UI 복잡도와 의미 해석의 모호성이 너무 컸다.

## 검토한 선택지

### 선택지 A - 모든 OCR line item을 개별 표시

장점:

- 원본 영수증 구조에 더 가깝다.
- 더 세밀한 할당 의미를 지원할 수 있다.

단점:

- 반복 item이 많을수록 UI가 지저분해진다.
- 메시지 가독성이 크게 나빠진다.
- 사용자 상호작용이 어려워진다.
- Discord component 제한에 더 쉽게 걸린다.

### 선택지 B - 동일 item을 병합하고 단순한 선택 규칙 사용

장점:

- UI가 더 깔끔하고 읽기 쉽다.
- 사용자가 이해하기 쉽다.
- Discord component 제약에 더 잘 맞는다.
- 정산 계산 모델이 단순해진다.

단점:

- line item 단위의 세밀함 일부를 잃는다.
- 더 단순한 split behavior를 전제로 한다.

## 결정

Discord receipt selection UI는 다음 원칙을 따른다.

1. 동일한 item은 수량 정보를 포함한 정규화 표현으로 병합한다.
2. UI는 기본적으로 사용자별 선택 상태를 중심으로 표시한다.
3. 여러 사용자가 선택한 item은 shared section에 표시한다.
4. 여러 사용자가 선택한 item의 비용은 선택한 사용자 수만큼 균등 분할한다.
5. 사용자는 Discord string select menu로 item을 선택한다.
6. 영수증이 Discord component 제한을 넘으면 pagination을 사용한다.

## 결과

### 긍정적 결과

- Discord UI가 훨씬 더 깔끔하고 읽기 쉬워진다.
- 반복 OCR item으로 인한 혼란이 줄어든다.
- 정산 계산 동작이 단순하고 예측 가능하다.
- Discord select menu 제한과 호환된다.
- shared item 표현을 더 잘 지원한다.

### 부정적 결과

- item 수량이 사용자별 실제 소비량을 직접 표현하지는 않는다.
- shared item에 대해 equal split behavior를 가정한다.
- 이 버전에서는 더 고급 split mode를 지원하지 않는다.

## 후속 메모

향후에는 다음과 같은 richer split mode를 검토할 수 있다.

- 수량 기반 할당
- 비율 분할
- 더 고급 정산 규칙 변형

현재 프로젝트 범위에서는, 병합된 item + 균등 분할 모델이 사용성, 구현 단순성, Discord UI 제약 사이에서 가장 좋은 균형을 제공한다.

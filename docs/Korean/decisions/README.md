# 의사결정 기록

이 디렉터리는 프로젝트의 아키텍처 및 설계 의사결정 기록을 가벼운 ADR 스타일로 저장한다.

각 파일은 시스템 구조, 서비스 경계, 런타임 동작, 전달 흐름, 장기적인 구현 방향에 영향을 주는 하나의 결정을 문서화한다.

## 목적

이 폴더는 다음과 같은 결정을 기록하기 위해 사용한다.

- 시스템 아키텍처를 바꾸는 결정
- 서비스 경계 또는 ownership을 정의하는 결정
- 중요한 구현 방향을 고정하는 결정
- 이전 아키텍처 가정을 대체하는 결정
- 바로 눈에 띄지 않는 기술적 tradeoff의 이유를 설명하는 결정

이 기록의 목적은 단순히 결과를 남기는 것이 아니라, 그 판단 근거를 보존하는 것이다.

## 표준 형식

모든 decision 문서는 아래 섹션 구조를 따른다.

```md
# NNN - Decision Title

## Status
Accepted

## Context
...

## Options Considered
...

## Decision
...

## Consequences
...

## Follow-up Notes
...
```

섹션별 기대사항:

- `Status`
  - 현재 상태를 나타낸다.
  - 권장 값: `Proposed`, `Accepted`, `Superseded`, `Deprecated`

- `Context`
  - 왜 이 결정이 필요했는지 설명한다.
  - 기술적 제약, 현재 시스템 상태, 해결하려는 실제 문제를 포함한다.

- `Options Considered`
  - 실제로 검토한 현실적인 선택지들을 적는다.
  - 허수아비 선택지가 아니라 tradeoff를 포함해야 한다.

- `Decision`
  - 최종 선택한 방향을 명확하고 직접적으로 적는다.

- `Consequences`
  - 긍정적 결과와 부정적 결과를 모두 적는다.
  - 모호한 표현보다 구체적인 tradeoff를 선호한다.

- `Follow-up Notes`
  - 구현 메모, 예외, 이후 재검토 포인트, 관련 decision 링크 등을 적는다.

## 번호 규칙

Decision 파일은 고정된 숫자 prefix를 사용한다.

- 형식: `NNN-kebab-case-title.md`
- 예: `012-serialize-receipt-session-updates-and-debounce-public-message-publishing.md`

규칙:

- 번호는 순차적으로 증가하며 3자리 zero-padding을 사용한다.
- 새 decision을 추가할 때는 다음 사용 가능한 번호를 사용한다.
- 순서를 바꾸기 위해 기존 문서 번호를 재배열하지 않는다.
- 나중에 decision이 교체되더라도 기존 파일을 재활용하지 않고 남겨 둔다.

이 규칙은 commit, discussion, 다른 문서에서 stable reference를 유지하기 위해 필요하다.

## 파일명 규칙

짧지만 설명적인 kebab-case 제목을 사용한다.

좋은 예:

- `013-use-single-public-receipt-message.md`
- `014-add-callback-authentication.md`

피해야 할 예:

- `misc-update.md` 같은 모호한 이름
- 문장처럼 너무 긴 파일명
- 숫자 prefix가 없는 파일명

## 작성 규칙

Decision 문서를 추가하거나 수정할 때는 다음을 따른다.

- 개인 일기처럼 쓰지 말고 repository 관점에서 쓴다.
- 결정을 촉발한 실제 기술 문제를 설명한다.
- 의미 있는 대안과 tradeoff를 포함한다.
- 직접적이고 구현 지향적인 언어를 사용한다.
- 일반적인 구호보다 구체적인 서술을 선호한다.

피해야 할 것:

- 서로 관련 없는 여러 결정을 하나 문서에 섞는 것
- 실제 decision이 아닌 단순 구현 디테일을 기록하는 것
- 이전 판단을 status 표시 없이 덮어쓰는 것

## 변경 규칙

기존 방향이 바뀌는 경우:

1. 이전 decision record를 삭제하지 않는다.
2. 필요하면 기존 문서의 status를 `Superseded` 등으로 갱신한다.
3. 새 번호로 새 decision record를 만든다.
4. 필요하면 `Follow-up Notes`에서 이전 decision을 참조한다.

Decision record는 문서인 동시에 역사 기록이다.

## 범위 가이드

좋은 decision record 후보:

- monorepo vs multi-repo 구조
- service-to-service communication model
- receipt state persistence 위치
- Discord UI interaction model
- delivery retry model
- concurrency handling strategy

좋지 않은 후보:

- 단순 리팩터링
- 변수 이름 변경
- 일회성 버그 수정
- 코드 포맷팅 변경

## 현재 규약

이 폴더의 문서는 이 README에 정의한 ADR 구조를 표준으로 사용한다.

새 문서는 repository가 새로운 형식을 공식 채택하지 않는 한 같은 형식을 따라야 한다.

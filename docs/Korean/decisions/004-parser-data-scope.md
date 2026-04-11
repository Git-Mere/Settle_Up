# 004 - Parser 데이터 범위를 파싱된 영수증 내용으로 제한

## Status
Accepted

## Context

receipt parser는 업로드된 영수증을 분석해 구조화된 receipt 데이터를 만들어내는 역할을 맡는다.

여기서 parser가 소유하는 데이터에 사용자 주도 배정 정보까지 포함해야 하는지에 대한 설계 질문이 생겼다.

예:

- 누가 어떤 item을 샀는지
- 누가 어떤 item을 함께 나눴는지
- payer나 participant 정보

이 결정은 서비스 책임 경계와 parser 소유 데이터의 변경 가능성에 영향을 준다.

## Options Considered

### Option A - 사용자 배정 데이터를 parser DB에 함께 저장

장점:

- 영수증 관련 정보를 하나의 문서에 담을 수 있다.
- 전체 영수증 흐름에 관여하는 서비스 수가 줄어든다.
- 일부 조회는 더 단순해질 수 있다.

단점:

- parser ownership에 settlement와 interaction 관심사가 섞인다.
- 사용자 주도 workflow state가 parser 데이터에 들어간다.
- parser 문서가 더 자주 바뀌고 덜 안정적이 된다.
- 서비스 경계가 흐려진다.

### Option B - Parser 데이터는 파싱된 영수증 내용에만 집중

장점:

- parsing 책임과 settlement 책임의 분리가 더 명확하다.
- parser 데이터가 문서 해석이라는 객관적 결과에 더 집중한다.
- parser 문서가 더 안정적이고 이해하기 쉬워진다.
- service-owned data 원칙과 잘 맞는다.

단점:

- downstream 서비스가 사용자 interaction 및 settlement state를 별도로 가져야 한다.
- identifier와 contract를 통한 조율이 필요하다.

## Decision

parser database에는 parsed receipt 데이터만 저장한다. 사용자 배정 정보와 settlement interaction state는 parser domain 바깥에서 처리한다.

## Consequences

### Positive

- 서비스 책임 경계가 더 명확해진다.
- parser 문서가 불변에 가까운 parsed fact에 더 가까워진다.
- 사용자 주도 workflow logic이 parser 서비스 밖에 남는다.

### Negative

- downstream 서비스가 더 많은 interaction-specific state를 관리해야 한다.
- 서비스 간 조율은 계속 필요하다.

## Follow-up Notes

parser는 추출된 receipt field와 정규화된 receipt 구조에 계속 집중해야 한다.

향후 workflow recovery나 audit 요구가 더 커지면, downstream 문서가 parser가 소유한 receipt identifier를 참조하는 방향으로 확장할 수는 있지만, parser ownership 자체를 settlement interaction state까지 넓히는 방식은 지양한다.

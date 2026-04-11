# 020 - Use In-Memory User Language Preferences With Owner Language For Public Receipt Messages

## Status
Accepted

## Context
`discord-api`에 한국어/영어 전환 기능을 넣으려면 `/language` 명령과 문자열 분리 구조가 필요했다.

이번 결정에서 해결해야 했던 핵심 문제는 두 가지였다.

1. 공개 receipt 메인 메시지는 여러 사용자가 함께 본다.
2. private/ephemeral/history 메시지는 호출 사용자마다 다르게 보여줄 수 있다.

Discord 상호작용 모델상 같은 공개 메시지를 사용자별로 다르게 렌더링할 수는 없다. 따라서 "한 사용자는 한국어, 다른 사용자는 영어" 같은 개인 설정을 도입하면, 공개 메인 메시지의 언어 기준을 별도로 정해야 했다.

추가 제약도 있었다.

- debug가 아닌 일반 사용 흐름에서는 언어 설정이 복잡한 저장소 의존성 없이 바로 동작해야 한다.
- slash command 설명과 option 설명은 Discord 등록 메타데이터라 사용자별로 다르게 바꾸기 어렵다.
- 운영 로그와 예외 메시지는 디버깅 일관성을 위해 영어로 유지하는 편이 낫다.

## Options Considered
1. 언어 설정 없이 기본 언어 하나만 유지
- 구현은 가장 단순하다.
- 하지만 한국어/영어 혼합 사용자 환경을 지원하지 못한다.

2. 사용자별 언어 설정을 영구 저장하고, 공개 메시지도 각 사용자에게 다르게 보여주려 시도
- 사용자 선호는 잘 보존된다.
- 하지만 Discord 공개 메시지는 사용자별 렌더링이 불가능하다.
- 현재 프로젝트 단계에 비해 저장소와 동기화 복잡도가 크다.

3. 사용자별 언어 설정을 두되, 공개 receipt 메시지는 owner 언어를 기준으로 고정
- 공개 메시지 언어가 세션 중간에 흔들리지 않는다.
- private/ephemeral/history는 사용자별 언어를 유지할 수 있다.
- 구현이 비교적 단순하다.

4. 사용자별 언어 설정을 두되, 공개 receipt 메시지는 마지막으로 `/language`를 바꾼 사람 기준으로 변경
- 기술적으로는 가능하다.
- 하지만 정산 참여자 누구나 공개 메시지 언어를 흔들 수 있어 UX가 불안정하다.

## Decision
언어 전환은 다음 정책으로 구현한다.

- `/language` slash command를 추가한다.
- 지원 언어는 `English`와 `Korean` 두 가지다.
- 사용자 언어 설정은 메모리 기반으로 보관한다.
- 기본 언어는 `English`다.
- private/ephemeral/history 메시지는 호출 사용자 언어를 사용한다.
- 공개 receipt 메인 메시지는 owner 언어를 사용한다.
- owner가 `/language`를 변경하면, 본인이 owner인 진행 중 공개 receipt 메시지는 즉시 refresh한다.
- slash command 설명과 option 설명은 쉬운 영어로 등록한다.
- 로그와 exception 메시지는 영어로 통일한다.

## Consequences
긍정적 결과:

- 저장소 스키마나 Cosmos 의존성 없이 빠르게 다국어 UI를 도입할 수 있다.
- 공개 메시지는 owner 기준으로 안정적으로 유지된다.
- 참여자별 private/ephemeral UI는 각자 선호 언어로 볼 수 있다.
- 로그와 예외를 영어로 통일해 운영 시 검색성과 일관성을 높일 수 있다.

부정적 결과 및 비용:

- 봇 재시작 시 사용자 언어 설정이 사라진다.
- 공개 메인 메시지는 owner 기준 하나의 언어만 가질 수 있다.
- slash command 메타데이터는 사용자별 현지화를 하지 못하고 영어로 고정된다.

## Follow-up Notes
- 향후 재시작 후에도 언어 설정을 유지해야 하면 별도 저장소 정책을 decision으로 추가해야 한다.
- 공개 메시지의 owner-language 정책은 Discord 공개 메시지의 shared rendering 제약을 전제로 한다.
- 이 결정으로 `discord-api`는 UI 문자열을 별도 localization 계층으로 분리하는 방향을 채택했다.

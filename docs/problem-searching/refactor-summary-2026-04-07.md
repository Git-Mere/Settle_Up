# Refactor Summary 2026-04-07

이 문서는 2026-04-07 세션에서 수행한 구조 단순화 리팩토링 내용을 정리한다.

목표는 기능 변경이 아니라, 현재 동작을 유지한 채 책임 경계를 더 명확하게 만들고 중복 cleanup / rendering / persistence 로직을 분리하는 것이었다.

## Scope

- `services/discord-api`
- `services/receipt-parser`

## Summary

이번 리팩토링으로 다음 변화가 있었다.

- `discord-api`는 interaction, session, history, rendering, blob, testing, calculations 기준으로 `Services`를 서브폴더로 정리했다.
- session 종료 cleanup, private panel cleanup, selection panel rendering, history persistence retry를 각각 전용 서비스로 분리했다.
- `ReceiptInteractionService`는 orchestration 중심으로 축소됐다.
- `receipt-parser`는 draft document / outbound payload 생성 책임을 `ReceiptDraftFactory`로 분리해 `ReceiptProcessingService`를 orchestration 중심으로 축소했다.

## Discord API Changes

### 1. Services 폴더를 역할별 하위 디렉터리로 재구성

현재 `services/discord-api/src/Services`는 아래 구조를 따른다.

- `Blob/`
- `Calculations/`
- `History/`
- `Interaction/`
- `Rendering/`
- `Session/`
- `Testing/`

이 변경의 목적은 "서비스가 많아졌을 때 파일 이름만으로 찾는 비용"을 줄이고, interaction/session/history 경계를 더 분명하게 만드는 것이다.

관련 문서 반영:

- [services/discord-api/README.md](/home/aero-mere/CS397/Settle_Up/services/discord-api/README.md)
- [services/discord-api/codex.md](/home/aero-mere/CS397/Settle_Up/services/discord-api/codex.md)

### 2. Session cleanup 경로 공통화

기존에는 confirm, cancel, pending delete, TTL expiry 경로가 debounce 취소, private panel cleanup, main message delete, session remove, lock cleanup를 각각 들고 있었다.

이제 공통 cleanup 책임은 `ReceiptSessionLifetimeService`로 이동했다.

관련 코드:

- [ReceiptSessionLifetimeService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptSessionLifetimeService.cs)
- [ReceiptInteractionService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptInteractionService.cs)
- [ReceiptSessionExpiryService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptSessionExpiryService.cs)
- [ReceiptDraftSessionService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptDraftSessionService.cs)

효과:

- cleanup 로직 중복 감소
- 경로별 cleanup 동작 불일치 위험 감소
- confirm/cancel/expiry 처리 추론이 쉬워짐

### 3. Private panel 관리 분리

기존에는 `ReceiptInteractionService`가 active private panel 교체와 전체 panel 정리를 직접 들고 있었다.

이제 panel 교체, 등록, 일괄 정리는 `ReceiptPrivatePanelService`가 담당한다.

관련 코드:

- [ReceiptPrivatePanelService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptPrivatePanelService.cs)

효과:

- interaction orchestration 코드에서 Discord private panel 수명주기 로직 제거
- session cleanup 경로와 panel cleanup 경로 연결이 더 명확해짐

### 4. Selection panel rendering/response 분리

selection panel의 content와 component 구성, 그리고 respond/update 분기는 `ReceiptSelectionPanelService`로 분리했다.

관련 코드:

- [ReceiptSelectionPanelService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptSelectionPanelService.cs)
- [ReceiptInteractionService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptInteractionService.cs)

효과:

- `ReceiptInteractionService`에서 UI panel build 보조 책임 제거
- selection panel 관련 로직을 한곳에서 다루기 쉬워짐

### 5. Settlement history background persistence 분리

history 저장 retry와 실패 follow-up 응답은 `SettlementHistoryPersistenceService`로 분리했다.

관련 코드:

- [SettlementHistoryPersistenceService.cs](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/History/SettlementHistoryPersistenceService.cs)

효과:

- confirm flow에서 history retry 상세 구현이 빠짐
- confirm orchestration이 더 짧고 읽기 쉬워짐

### 6. `ReceiptInteractionService` 축소

리팩토링 전후 비교에서 가장 큰 차이는 `ReceiptInteractionService`의 역할 정리다.

- 이전: interaction routing + panel rendering + panel cleanup + history retry + session cleanup 일부
- 현재: interaction routing + validation + mutation orchestration 중심

이로 인해 파일 크기도 줄었고, “실제 receipt mutation 흐름”에 더 집중된 형태가 됐다.

## Receipt Parser Changes

### 1. Draft document / notification payload factory 분리

`ReceiptProcessingService`에 있던 아래 책임을 `ReceiptDraftFactory`로 이동했다.

- `ReceiptDocument` 생성
- `DiscordDraftNotificationPayload` 생성
- `uploadedByUserId` blob path 추출

관련 코드:

- [ReceiptDraftFactory.cs](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/ReceiptDraftFactory.cs)
- [ReceiptProcessingService.cs](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/ReceiptProcessingService.cs)

효과:

- `ReceiptProcessingService`가 parse -> save -> send orchestration에 더 집중
- draft contract 생성 규칙이 별도 파일에 모여 찾기 쉬워짐
- parser 쪽도 서비스 책임 경계가 더 분명해짐

## Verification

리팩토링 후 아래 빌드를 수행해 컴파일 상태를 확인했다.

- `dotnet build services/discord-api/src/DiscordApi.csproj -c Release`
- `dotnet build services/receipt-parser/receipt-parser.csproj -c Release`

확인 결과:

- `discord-api`: 0 warnings, 0 errors
- `receipt-parser`: 0 warnings, 0 errors

또한 단독 사용자 기준 수동 테스트에서 기대한 주요 동작이 유지되는 것을 확인했다.

## Remaining Work

이번 리팩토링은 구조 단순화 중심이었고, 다음 단계는 최적화/잔여 리스크 감소다.

현재 후속 후보:

1. `ReceiptDraftSessionService`의 display name fallback REST 호출 최소화
2. `ReceiptMainMessageDebounceService`의 task/CTS churn 축소
3. selection panel / renderer의 precomputed display name 재사용
4. active session memory footprint 축소

즉 이번 변경은 "리팩토링 완료"라기보다는, 이후 최적화와 문제 분석을 더 쉽게 만드는 기준선 정리로 보는 것이 맞다.

## Follow-up Adjustments After Refactor

리팩토링 직후 추가로 반영한 작은 조정:

- `ReceiptDraftSessionService`의 업로더 display name 확인을 draft session lock 안에서 다시 수행하도록 조정했다.
- active check receipt session TTL은 `6시간`에서 `3시간`으로 낮췄다.

이 조정의 목적은 callback retry/중복 상황에서의 중복 Discord REST 조회 가능성을 줄이고, abandoned check session의 메모리/메시지 유지 시간을 더 짧게 가져가는 것이다.

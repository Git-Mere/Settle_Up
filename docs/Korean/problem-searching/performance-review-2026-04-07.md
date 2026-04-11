# Performance Review 2026-04-07

이 문서는 2026-04-07 기준 현재 코드 상태를 다시 점검한 결과다.

직전 리뷰에서 지적됐던 몇 가지 큰 항목은 이미 해소된 상태였다.

- `discord-api` Blob uploader warm-up 추가
- `receipt-parser` parser/Cosmos warm-up 추가
- parser callback 시 업로더 표시 이름 재사용 경로 추가
- confirm 후 receipt session / session lock cleanup 추가

따라서 이번 문서는 "이전 리뷰 이후에도 아직 남아 있는 비효율 후보"에 집중한다.

## Scope

- `services/discord-api`
- `services/receipt-parser`

집중적으로 본 흐름:

1. Discord receipt interaction mutation
2. confirm 직전/직후 렌더링 및 history 생성
3. parser 저장/전송 hot path
4. 장기 실행 시 stale object 누적 가능성

## Summary

현재 가장 눈에 띄는 비효율은 두 종류다.

- confirm/history 경로에서 같은 allocation 성격의 계산을 여러 번 다시 수행하는 구조
- 사용자가 업로드 플로우를 끝내지 않거나 downstream callback이 오지 않을 때 in-memory object가 오래 남는 구조

parser 쪽에서는 성공 경로에서 Cosmos upsert를 두 번 수행하는 부분이 비용 대비 효과를 다시 검토할 만하다.

## Findings

### 1. Confirm 시 participant item share 계산이 participant 수만큼 다시 반복된다

심각도:

- High

설명:

`ConfirmedSettlementHistoryDocument.FromSession(...)`는 먼저 `ReceiptAllocationService.Calculate(session)`를 한 번 수행한다.
그 다음 각 participant를 만들 때 `BuildParticipantItems(session, participant.UserId)`를 호출하는데,
이 메서드는 내부에서 다시 `ReceiptAllocationService.CalculateParticipantItemShares(session)`를 호출한다.

문제는 이 `CalculateParticipantItemShares(...)`가 participant마다 전체 session item을 다시 순회한다는 점이다.
즉 confirm 시 participant가 5명이면 같은 share map을 5번 다시 계산한다.

관련 코드:

- [ConfirmedSettlementHistoryDocument.cs#L78](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ConfirmedSettlementHistoryDocument.cs#L78)
- [ConfirmedSettlementHistoryDocument.cs#L105](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ConfirmedSettlementHistoryDocument.cs#L105)
- [ConfirmedSettlementHistoryDocument.cs#L137](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ConfirmedSettlementHistoryDocument.cs#L137)
- [ReceiptAllocationService.cs#L66](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptAllocationService.cs#L66)

영향:

- confirm hot path CPU 사용 증가
- item 수와 participant 수가 함께 늘면 불필요한 계산이 빠르게 커짐
- history persistence 직전의 메모리 할당과 dictionary 생성 반복

권장 방향:

- participant item share map을 confirm/history 생성 시작 시 한 번만 계산
- `BuildParticipantItems(...)`에 precomputed share map을 파라미터로 넘기기
- 가능하면 `ReceiptAllocationService.Calculate(...)` 결과와 share map을 하나의 immutable result object로 묶기

### 2. allocation/render helper가 `UserSelections`를 item마다 반복 스캔한다

심각도:

- Medium

설명:

현재 `ReceiptAllocationService.Calculate(...)`와 `CalculateParticipantItemShares(...)`는 item마다
`ReceiptSessionStateService.GetUsersForItem(session, item.Id)`를 호출한다.
그런데 `GetUsersForItem(...)`는 item 하나를 찾기 위해 `session.UserSelections` 전체를 매번 훑는다.

같은 패턴은 `GetUnassignedItems(...)`에도 있다.
여기는 item 목록을 훑으면서 내부에서 다시 `GetUsersForItem(...)`를 호출한다.

즉 현재 구조는 "item -> users" 역방향 매핑이 없어서, render/confirm/alcohol/tax 계산 경로에서 같은 관계를 여러 번 다시 찾는다.

관련 코드:

- [ReceiptAllocationService.cs#L7](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptAllocationService.cs#L7)
- [ReceiptAllocationService.cs#L72](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptAllocationService.cs#L72)
- [ReceiptSessionStateService.cs#L340](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptSessionStateService.cs#L340)
- [ReceiptSessionStateService.cs#L369](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptSessionStateService.cs#L369)
- [ReceiptMessageRenderer.cs#L16](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptMessageRenderer.cs#L16)

영향:

- receipt item 수와 참여자 수가 커질수록 렌더링/confirm 계산의 상수 비용이 눈에 띄게 증가
- 불필요한 배열 생성(`ToArray`)과 정렬 비용이 반복됨
- 현재 item 수가 작을 때는 괜찮지만, 큰 장보기/회식 영수증에서는 체감될 가능성이 있음

권장 방향:

- mutation 시점에 `itemId -> assignedUsers` reverse index를 session에 유지
- 또는 `ReceiptRenderContext.Create(...)`에서 만든 map을 allocation/historical paths도 재사용
- `GetUsersForItem(...)`를 범용 helper로 남기더라도 hot path에서는 precomputed map만 사용

### 3. 업로드 플로우가 중간에 끊기면 interaction/session 객체가 오래 남을 수 있다

심각도:

- Medium

설명:

`/settle-up` 플로우에서 버튼을 누르면 `_uploadPromptInteractions`에 `SocketMessageComponent` 전체를 저장한다.
이 값은 modal submit이 성공적으로 끝나 `TryDeleteUploadPromptAsync(...)`가 호출될 때만 제거된다.

즉 사용자가 버튼만 누르고 modal을 닫거나, 클라이언트가 끊기거나, modal submit 전에 흐름을 버리면 이 dictionary entry는 계속 남는다.

또 pending upload session도 `CreatePendingUploadSessionAndReturnAsync(...)`에서 store에 들어간 뒤,
업로드 실패 또는 parser callback success 경로에서만 자연스럽게 정리된다.
parser callback이 오지 않는 stalled case에 대한 TTL/cleanup 경로는 현재 보이지 않는다.

관련 코드:

- [SettleUpCommandHandler.cs#L18](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Commands/SettleUpCommandHandler.cs#L18)
- [SettleUpCommandHandler.cs#L108](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Commands/SettleUpCommandHandler.cs#L108)
- [SettleUpCommandHandler.cs#L214](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Commands/SettleUpCommandHandler.cs#L214)
- [ReceiptDraftSessionService.cs#L32](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptDraftSessionService.cs#L32)
- [ReceiptSessionState.cs#L26](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L26)
- [ReceiptSessionStore.cs#L5](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptSessionStore.cs#L5)

영향:

- 장기 실행 시 abandoned flow가 누적되면 메모리 사용량 증가
- retained object 안에 Discord interaction/message/channel reference가 들어 있어 가벼운 누수보다 더 무거울 수 있음
- stale pending UI가 남아 운영 추적을 어렵게 할 수 있음

권장 방향:

- `_uploadPromptInteractions`에 TTL 기반 sweep 추가
- pending receipt session에도 creation timestamp 기준 stale cleanup 추가
- cleanup 대상은 `IsDraftReady == false` 또는 "callback 미수신 n분 경과" 기준으로 좁히기

### 4. parser 성공 경로가 동일 문서를 Cosmos에 두 번 upsert한다

심각도:

- Medium

설명:

`ReceiptProcessingService.SaveAndSendDraftAsync(...)`는 먼저 `NotificationStatus=Pending` 문서를 저장하고,
HTTP delivery가 성공하면 거의 같은 본문을 `NotificationStatus=Sent`로 다시 upsert한다.

실패 복구와 재처리 설계를 생각하면 이중 저장이 의도일 수는 있다.
하지만 현재 코드 기준으로는 성공 경로가 더 흔한 경로일 가능성이 높고, 그 경우 item 목록과 metadata를 포함한 큰 문서를 두 번 serialize/write 한다.

관련 코드:

- [ReceiptProcessingService.cs#L87](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/ReceiptProcessingService.cs#L87)
- [ReceiptProcessingService.cs#L93](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/ReceiptProcessingService.cs#L93)
- [ReceiptProcessingService.cs#L100](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/ReceiptProcessingService.cs#L100)
- [ReceiptProcessingService.cs#L101](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/ReceiptProcessingService.cs#L101)
- [CosmosReceiptRepository.cs#L40](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/CosmosReceiptRepository.cs#L40)

영향:

- 성공 경로 RU 비용 증가
- serialization/allocation 두 배
- Event Grid burst 시 Cosmos write pressure 증가

권장 방향:

- 현재 Pending 선저장이 crash recovery에 꼭 필요한지 재검토
- 필요하다면 status-only 별도 document 또는 lightweight delivery tracking record 분리 검토
- 아니면 성공 경로에서는 한 번만 쓰고, 실패 시에만 retry/reprocessing metadata를 저장하는 방향 검토

### 5. uncached render 시 section별 LINQ materialization이 많다

심각도:

- Low

설명:

`ReceiptMessageRenderer`는 render cache가 invalidated될 때마다 `ReceiptRenderContext.Create(...)`를 다시 만들고,
각 section builder에서 `Select/Where/OrderBy/ToArray`를 여러 번 사용한다.

현재는 debounce + rendered cache 덕분에 아주 급한 병목은 아니지만,
아이템이 많은 영수증에서 짧은 시간 내 mutation이 반복되면 section별 중간 배열과 문자열이 계속 할당된다.

관련 코드:

- [ReceiptMessageRenderer.cs#L5](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptMessageRenderer.cs#L5)
- [ReceiptMessageRenderer.cs#L149](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptMessageRenderer.cs#L149)
- [ReceiptMessageRenderer.cs#L168](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptMessageRenderer.cs#L168)
- [ReceiptMessageRenderer.cs#L362](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptMessageRenderer.cs#L362)

영향:

- hot mutation 구간에서 GC pressure 증가 가능
- item 수가 많을수록 section 문자열 생성 비용 증가

권장 방향:

- 급한 문제는 아니므로 allocation helper 중복 제거가 먼저
- 그 다음 단계에서 section builder를 imperative loop 기반으로 정리 검토
- render context가 이미 가진 map/total을 section별 중복 필터보다 더 많이 재사용하도록 조정

## Non-Findings

이번 재검토에서 "이전에는 우려였지만 현재는 해소된 상태"로 본 항목:

- confirm 이후 session/lock cleanup 부재
- Blob uploader cold path에서 매 요청 container readiness 재실행
- parser startup credential/container cold warm-up 부재
- draft callback 직전 Discord user display name 강제 재조회

## Recommended Next Steps

우선순위 제안:

1. `ConfirmedSettlementHistoryDocument`와 `ReceiptAllocationService`의 중복 계산 구조부터 줄이기
2. pending upload / abandoned interaction TTL cleanup 추가
3. parser 성공 경로의 이중 Cosmos upsert가 정말 필요한지 재검토
4. 그 다음에 render path LINQ allocation 정리

## Suggested First Refactor

가장 먼저 손대기 좋은 지점은 allocation 결과 객체 확장이다.

예를 들어 아래 정보를 한 번에 계산해 하나의 result로 반환하면 현재 중복의 상당수를 줄일 수 있다.

- `itemId -> assignedUsers`
- `userId -> itemShares`
- `participant breakdown`
- `tax lines`
- `tip lines`

이렇게 하면:

- confirm embed
- history document 생성
- unassigned/shared/individual section
- tax/tip section

이 모두 같은 precomputed graph를 재사용할 수 있다.

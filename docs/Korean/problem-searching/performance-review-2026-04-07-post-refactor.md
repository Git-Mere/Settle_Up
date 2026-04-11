# Performance Review 2026-04-07 Post Refactor

이 문서는 2026-04-07 기준, 최근 구조 리팩토링 이후의 현재 코드 상태를 다시 점검한 결과를 정리한다.

직전 성능/메모리 리뷰에서 지적됐던 큰 항목 중 일부는 이미 해소된 상태다.

- `discord-api` Blob uploader warm-up 추가
- `receipt-parser` startup warm-up 추가
- confirm 후 receipt session / session lock cleanup 추가
- `/language`의 기존 공개 receipt message refresh 제거
- `discord-api` interaction / session / history 책임 분리

따라서 이번 문서는 "리팩토링 이후에도 아직 남아 있는 병목 가능성, 메모리 유지 비용, 불필요한 호출"에 집중한다.

## Scope

- `services/discord-api`
- `services/receipt-parser`

집중적으로 본 흐름:

1. parser callback으로 draft session upsert
2. Discord 공개 main message refresh / confirm
3. session TTL cleanup
4. selection panel open/update
5. render context 생성과 settlement 계산

## Summary

현재 기준으로 즉시 수정이 필요한 치명적 문제는 보이지 않았다.

다만 다음 성격의 잔여 리스크는 남아 있다.

- draft callback 임계 경로에 아직 Discord REST 조회가 남아 있음
- active session 하나가 `IDiscordInteraction`, edit token, render cache 등을 오래 들고 있을 수 있음
- debounced refresh가 rapid interaction 시 작은 task/CTS allocation을 반복함
- selection panel / render path에 일부 반복 계산이 남아 있음

즉 현재 상태는 "터지는 누수"보다는 "세션 하나당 메모리 유지 비용과 hot path 반복 계산이 남아 있는 상태"로 보는 것이 맞다.

## Findings

### 1. Draft callback 직전 업로더 표시 이름 조회가 여전히 임계 경로에 있다

심각도:

- Medium

설명:

`ReceiptDraftSessionService`는 draft payload를 session에 반영하기 전에 `FindExistingSession(...)`로 기존 세션을 찾고, 그 결과를 바탕으로 `ResolveUploadedByDisplayNameAsync(...)`를 호출한다.

기존 pending session에 표시 이름이 남아 있으면 재사용되고, 최근 수정으로 display name 확인도 session lock 안에서 다시 수행되도록 바뀌었다.
따라서 "같은 receipt에 대한 callback retry가 겹칠 때 lock 밖에서 중복 조회" 문제는 줄었다.

다만 cache miss인 경우에는 여전히 Discord REST fallback이 draft publish 임계 경로에 남아 있다.

관련 코드:

- [ReceiptDraftSessionService.cs#L172](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptDraftSessionService.cs#L172)
- [ReceiptDraftSessionService.cs#L177](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptDraftSessionService.cs#L177)
- [ReceiptDraftSessionService.cs#L177](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptDraftSessionService.cs#L177)
- [ReceiptDraftSessionService.cs#L282](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptDraftSessionService.cs#L282)
- [ReceiptDraftSessionService.cs#L301](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptDraftSessionService.cs#L301)
- [ReceiptDraftSessionService.cs#L311](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptDraftSessionService.cs#L311)

영향:

- parser callback 이후 공개 check 메시지 표시 직전의 임계 경로에 네트워크 호출이 남음
- cache miss인 경우 첫 draft publish가 Discord REST latency 영향을 받음

권장 방향:

- 현재 수정으로 lock 안 재확인은 들어갔다
- 다음 단계가 필요하다면 pending session 생성 시 확보한 display name을 더 강하게 신뢰하고 fallback만 REST로 두기

### 2. Active receipt session이 interaction/token reference를 TTL 동안 유지한다

심각도:

- Medium

설명:

현재 `ReceiptSessionState`는 `PendingEditItemIds`, `ActivePrivatePanelInteractions`, `MainMessage`, `CachedRenderedMessage`를 session 객체 안에 직접 보관한다.

confirm/cancel/expiry cleanup이 들어가면서 전역 누수 위험은 크게 줄었지만, active session 하나가 오래 살아 있으면 여전히 무거운 객체를 들고 있게 된다.
특히 사용자가 panel을 여러 번 열고, edit modal token을 여러 번 만들고, 세션이 confirm되지 않은 채 오래 남으면 session 하나의 메모리 footprint가 커질 수 있다.

관련 코드:

- [ReceiptSessionState.cs#L23](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L23)
- [ReceiptSessionState.cs#L24](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L24)
- [ReceiptSessionState.cs#L25](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L25)
- [ReceiptSessionState.cs#L26](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L26)
- [ReceiptSessionState.cs#L27](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L27)
- [ReceiptSessionState.cs#L28](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L28)
- [ReceiptSessionState.cs#L38](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L38)
- [ReceiptSessionState.cs#L39](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Models/ReceiptSessionState.cs#L39)

영향:

- long-lived active session의 메모리 사용량 증가
- abandon된 interaction reference가 TTL까지 남을 수 있음

권장 방향:

- `PendingEditItemIds`에 TTL 또는 bounded size 검토
- `ActivePrivatePanelInteractions`를 interaction 전체 대신 최소 식별자만 저장하는 방향 검토
- `CachedRenderedMessage` 크기가 커질 경우 size/age 기준 invalidation 검토

### 3. Debounced refresh는 rapid mutation에서 task/CTS allocation churn이 있다

심각도:

- Medium

설명:

`ReceiptMainMessageDebounceService.ScheduleRefresh(...)`는 호출마다 새 `CancellationTokenSource`를 만들고 fire-and-forget task를 시작한다.
이전 pending refresh는 cancel/dispose 하므로 논리적 누수는 크지 않지만, 클릭이 빠르게 반복되면 짧은 수명의 CTS와 task가 많이 생성된다.

관련 코드:

- [ReceiptMainMessageDebounceService.cs#L25](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptMainMessageDebounceService.cs#L25)
- [ReceiptMainMessageDebounceService.cs#L29](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptMainMessageDebounceService.cs#L29)
- [ReceiptMainMessageDebounceService.cs#L38](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptMainMessageDebounceService.cs#L38)
- [ReceiptMainMessageDebounceService.cs#L55](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptMainMessageDebounceService.cs#L55)

영향:

- rapid click / mutation 구간에서 allocation 증가
- 아주 큰 문제는 아니지만 interaction burst가 길어지면 GC pressure 증가 가능

권장 방향:

- per-session single timer 모델 검토
- 또는 next-refresh-at 방식의 coalescing worker 검토

### 4. TTL cleanup은 전체 세션을 주기적으로 배열 복사 후 순회한다

심각도:

- Low

설명:

`ReceiptSessionExpiryService`는 1분마다 `ReceiptSessionStore.GetAll()`을 호출해 전체 세션을 배열로 복사한 뒤 순회한다.
현재 세션 수가 크지 않다면 문제는 작지만, active session 수가 많아질수록 주기적 allocation과 선형 탐색 비용이 커진다.

관련 코드:

- [ReceiptSessionStore.cs#L76](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptSessionStore.cs#L76)
- [ReceiptSessionStore.cs#L78](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptSessionStore.cs#L78)
- [ReceiptSessionExpiryService.cs#L56](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptSessionExpiryService.cs#L56)
- [ReceiptSessionExpiryService.cs#L62](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Session/ReceiptSessionExpiryService.cs#L62)

영향:

- active session 수 증가 시 background sweep 비용 증가
- 주기적 배열 allocation

권장 방향:

- 현재 규모에서는 허용 가능
- 규모가 커지면 min-heap/expiry bucket 또는 owner/session index 검토

### 5. Selection panel item display name 계산은 duplicate item이 많을 때 O(n^2)로 커진다

심각도:

- Low

설명:

`ReceiptSelectionPanelService`는 option label을 만들 때 item마다 `GetSelectionDisplayName(...)`를 호출한다.
이 메서드는 내부에서 `session.Items.Count(...)`와 `GetInstanceIndex(...)`를 다시 돌기 때문에 duplicate group이 많은 큰 영수증에서는 반복 스캔이 생긴다.

관련 코드:

- [ReceiptSelectionPanelService.cs#L65](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptSelectionPanelService.cs#L65)
- [ReceiptSelectionPanelService.cs#L67](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptSelectionPanelService.cs#L67)
- [ReceiptSelectionPanelService.cs#L130](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptSelectionPanelService.cs#L130)
- [ReceiptSelectionPanelService.cs#L139](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Interaction/ReceiptSelectionPanelService.cs#L139)

영향:

- `/custom`처럼 item 수가 많은 세션에서 panel open/update 비용 증가 가능

권장 방향:

- render context처럼 duplicate index map을 한 번 만들어 재사용
- panel build 시 `itemId -> displayName` precompute

### 6. Renderer는 한 번의 uncached render에서 allocation 성격 계산을 둘로 나눠 수행한다

심각도:

- Low

설명:

`ReceiptMessageRenderer.ReceiptRenderContext.Create(...)`는 `ReceiptAllocationService.Calculate(session)`와 `CalculateParticipantItemShares(...)`를 둘 다 호출한다.
둘 다 assignment 관계를 바탕으로 map/dictionary를 만든다는 점에서 계산 graph가 일부 겹친다.

render cache가 있으므로 급한 병목은 아니지만, cache invalidation이 잦은 mutation 구간에서는 중복 계산이 남아 있다.

관련 코드:

- [ReceiptMessageRenderer.cs#L412](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Rendering/ReceiptMessageRenderer.cs#L412)
- [ReceiptMessageRenderer.cs#L415](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Rendering/ReceiptMessageRenderer.cs#L415)
- [ReceiptMessageRenderer.cs#L460](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Rendering/ReceiptMessageRenderer.cs#L460)
- [ReceiptMessageRenderer.cs#L461](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/Rendering/ReceiptMessageRenderer.cs#L461)

영향:

- repeated mutation 시 CPU/alloc 증가
- 큰 영수증에서 render cache miss 비용이 커질 수 있음

권장 방향:

- allocation result와 participant item share를 하나의 immutable result로 묶기
- render/history 모두 그 결과를 재사용하도록 정리

## Non-Findings

이번 재점검에서 "이전에는 문제였지만 현재는 해소된 상태"로 본 항목:

- confirm 이후 session cleanup 부재
- session lock cleanup 부재
- `/language`가 전체 세션을 refresh하는 구조
- Blob uploader readiness를 요청 경로에서 매번 준비하는 구조

## Recommended Next Steps

우선순위 제안:

1. `ReceiptDraftSessionService`의 display name fallback을 더 강하게 캐시 우선으로 정리
2. `ReceiptMainMessageDebounceService`의 CTS/task churn을 줄이는 단순한 coalescing 구조 검토
3. selection panel과 renderer의 duplicate display name / allocation precompute 재사용
4. active session 메모리 footprint 축소(`IDiscordInteraction`, edit token, render cache 수명)

## Suggested First Change

가장 먼저 손대기 좋은 지점은 `ReceiptDraftSessionService`다.

이유:

- 네트워크 호출이 남아 있는 경로라 효과가 바로 보인다
- 구조 변경 범위가 작다
- callback retry/중복 수신 상황에서 중복 REST 조회를 줄일 수 있다

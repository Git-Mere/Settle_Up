# codex.md

## Service Name
- `discord-api`

## Session Summary (updated)
이번 세션까지의 `discord-api`는 "worker + HTTP receiver 통합 호스팅 + receipt draft UI + tax/tip settlement + history persistence/query + 공개 메인 메시지 수정 기반 전환 + 직렬화/디바운스/캐시 최적화 + check/confirm UX 정리 + item-level discount 처리 + language 전환 + custom manual settlement entrypoint" 상태다.

1. 공통 observability bootstrap 적용
- `shared/SettleUp.Observability`를 참조하도록 변경.
- console logging은 `ILogger` 중심 단일 라인 출력으로 정리.
- OpenTelemetry raw console exporter는 제거하고 Azure Monitor exporter를 환경 변수 기반으로 활성화한다.

2. Host / DI 구조 정리
- `Program.cs`는 .NET 8 `WebApplicationBuilder` 기반으로 정리했다.
- `DiscordBotWorker`가 Discord 클라이언트 시작/중지와 이벤트 라우팅을 담당.
- `BlobUploaderProvider`로 Blob 업로더 초기화 상태를 캡슐화.
- 같은 프로세스에서 Kestrel HTTP 서버도 함께 실행한다.

3. HTTP receiver 추가
- `POST /getting_draft` endpoint를 추가했다.
- `ReceiptDraftNotificationRequest` DTO로 payload를 받는다.
- receipt id / user id / merchant / item count를 structured log로 남기고 `200 OK` + `{ "message": "draft received" }`를 반환한다.
- 기본 리슨 주소는 `http://0.0.0.0:5000`이며 `ASPNETCORE_URLS`로 오버라이드 가능하다.

4. 테스트용 draft UI 명령 추가
- `/test` slash command를 추가했다.
- `src/TestData/sample-receipt-draft.json`을 로드해 receipt selection UI 세션을 생성한다.
- 실행 사용자 id를 `uploadedByUserId`로 덮어써 현재 채널에 테스트 UI를 생성한다.

5. Blob 업로드 기능 추가
- `/settle-up` 플로우에서 업로드 파일을 Azure Blob으로 저장.
- `BlobImageUploader`로 로직 분리.
- 허용 파일: `jpg`, `jpeg`, `png`.

6. 의미 있는 application log 보강
- 봇 시작/정지, Discord ready, 명령 시작/완료/실패, Blob 업로드 시작/완료/실패를 `ILogger` structured log로 기록.
- Discord 내부 로그도 `Console.WriteLine` 대신 `ILogger`로 매핑.
- `/getting_draft` 호출도 `ILogger`로 기록한다.

7. receipt interaction UI 구현
- 체크 섹션 embed:
  - seller / purchase date / buyer / item total / tax / total
  - shared / individual / unassigned
  - `Select Item`, `Add item`, `Remove item`, `Edit item`, `Confirm`
- confirm embed:
  - 동일 header
  - payment contact
  - settlement line list
- add/remove/edit 시 금액 header 재계산 반영.
- add item으로 만든 manual item도 edit 가능하도록 modal custom id는 짧은 token 매핑을 사용한다.

8. 공개 메인 메시지 수정 전략 전환
- 기존 "새 공개 메시지 계속 발행" 중심 흐름에서, 현재는 기존 공개 메인 메시지를 수정하는 방향으로 정리했다.
- `select/add/remove/edit/confirm`는 모두 최종적으로 기존 메인 메시지를 갱신하는 방향을 기본으로 본다.
- `select/remove/edit` private panel은 사용자+모드 기준으로 하나만 유지되며, confirm 시 열린 panel cleanup을 시도한다.
- 권한 모델은 `Select item`만 참여자 전체가 가능하고, `Add item` / `Remove item` / `Edit item` / `Confirm`은 업로더만 가능하다.

9. 성능 / 동시성 최적화
- `ReceiptSessionLockManager`를 추가해 같은 receipt session의 mutation을 직렬화한다.
- `ReceiptMainMessageDebounceService`를 추가해 routine interaction의 공개 메인 메시지 갱신을 1초 디바운스로 묶는다.
- `ReceiptSessionState.MainMessage`에 현재 공개 메인 메시지 객체를 캐시해 불필요한 `GetMessageAsync`를 줄인다.
- `ReceiptMessageRenderer`에 render context 캐시를 넣어 user-item mapping, unassigned item, settlement line, display name 계산을 한 번만 수행한다.
- Discord API의 `429/502/503/504`에 대해 메인 메시지 갱신 retry를 추가했다.

10. `/settle-up` 상호작용 플로우 변경
- 기존: slash 후 채널 메시지 업로드 대기
- 현재: slash -> 버튼 표시 -> 버튼 클릭 -> 모달(파일 업로드 컴포넌트) -> Blob 업로드

11. check / confirm UX 정리
- check 공개 메시지의 item 표시는 더 이상 이름 알파벳순으로 재정렬하지 않고, parser draft에서 들어온 원래 순서를 유지한다.
- confirmed embed의 `Payment` 필드명은 `Pay to`로 변경했다.
- confirm 후 추가 텍스트(`정산을 확정했습니다...`)는 더 이상 남기지 않고 confirmed embed만 갱신한다.
- add item 후 추가 텍스트(`아이템을 추가했습니다.`)도 더 이상 남기지 않고 공개 메인 메시지만 갱신한다.
- check 단계 메인 메시지에 owner 전용 `Cancel` 버튼을 추가했다.
- `Cancel`은 공개 메인 메시지 삭제, 열린 private panel cleanup, session store 제거, debounce cancel까지 수행한다.
- cancel 처리 중 삭제된 공개 메시지를 다시 수정하려다 발생하던 Discord `10008 Unknown Message` 오류는 후속 `ModifyOriginalResponseAsync` 호출 제거로 정리했다.

12. tax / tip / alcohol UI 추가
- 일반 tax는 선택한 아이템 금액 비율대로 배분한다.
- `SST`, `SLT`는 alcohol로 지정된 아이템을 고른 참여자끼리 가격 비율로 배분한다.
- owner 전용 `Mark Alcohol` 버튼으로 alcohol item을 지정한다.
- check embed에 `Tax`, `Tip` 섹션이 추가됐다.
- `Tip`은 proportional / equal split 두 가지 모드를 owner가 토글할 수 있다.

13. history 저장 / 조회 추가
- confirm 시 settlement snapshot을 `discord-api`가 Cosmos에 저장한다.
- confirm UX는 먼저 Discord 메시지를 확정하고, history 저장은 background에서 retry와 함께 처리한다.
- `/history list`, `/history detail index:<번호>` slash command가 추가됐다.
- `index:1`은 현재 시점 기준 가장 최근 history다.

14. 업로드 UX 정리
- `/settle-up` 업로드 모달 제출 직후 pending 공개 메시지를 먼저 띄운다.
- 업로드 성공 후 기존 ephemeral 업로드 버튼 메시지는 삭제한다.
- check 단계의 item 선택 패널은 영수증 원래 순서를 유지한다.

15. 최근 리팩터링
- rendered embed를 세션에 캐시해 같은 상태에서 중복 렌더링을 줄였다.
- settlement history Cosmos container를 lazy 초기화해 반복 `CreateContainerIfNotExistsAsync` 호출을 줄였다.
- 미사용 publish/helper 메서드와 미사용 계산 helper를 정리했다.
- `/pingtest`, `/test`는 이제 Development 환경에서만 등록된다.

16. item-level discount 처리
- parser에서 온 음수 line item은 일반 item으로 그대로 노출하지 않는다.
- 할인은 우선 직전 item에 귀속된 것으로 간주하고, item net amount와 `discountAmount`를 함께 사용한다.
- check UI item 표시는 한 줄 요약 형태로 렌더링한다.
- 예: `Protein Bar - $3.50 (discount -$1.00)`
- 귀속 실패 할인은 현재 UI와 total 계산에서 적용하지 않는다.
- 사용자 보정이 필요하면 owner가 `Edit item`으로 직접 수정한다.

17. language 전환 추가
- `/language` slash command를 추가했다.
- 지원 언어는 English / Korean 두 가지다.
- 사용자 언어 설정은 메모리 기반이고, 기본값은 English다.
- private/ephemeral/history는 호출 사용자 언어를 따른다.
- 공개 receipt 메인 메시지는 owner 언어를 따른다.
- owner가 `/language`를 바꾸면 본인이 owner인 진행 중 공개 receipt 메시지를 즉시 refresh한다.
- slash command 설명과 option 설명은 쉬운 영어로 등록한다.
- 로그와 exception 메시지는 영어로 통일한다.

18. `/custom` 수동 정산 시작점 추가
- parser 없이 빈 receipt session을 바로 생성하는 `/custom` slash command를 추가했다.
- `Seller Name`은 `Custom`, `Purchase Date`는 현재 날짜, `Buyer Name`은 명령 실행자다.
- 초기 금액 header와 item 목록은 모두 비어 있고 0으로 시작한다.
- optional `payment_contact` slash option을 받을 수 있다.
- 공개 check 메시지를 바로 띄운 뒤 owner가 `Add item` 등 기존 조작 UI로 채워 넣는다.
- item이 0개인 상태에서는 confirm이 불가능하고, item이 1개 이상이며 모두 배정됐을 때만 confirm 가능하다.

## Current File Layout (relevant)
```text
services/discord-api/
├─ src/
│  ├─ Program.cs
│  ├─ DiscordBotWorker.cs
│  ├─ BlobUploaderProvider.cs
│  ├─ DiscordApi.csproj
│  ├─ Commands/
│  │  ├─ CustomReceiptCommandHandler.cs
│  │  ├─ HistoryCommandHandler.cs
│  │  ├─ LanguageCommandHandler.cs
│  │  ├─ PingTestCommandHandler.cs
│  │  ├─ SettleUpCommandHandler.cs
│  │  └─ TestReceiptCommandHandler.cs
│  ├─ Localization/
│  │  ├─ AppLanguage.cs
│  │  ├─ DiscordUiText.cs
│  │  └─ UserLanguagePreferenceStore.cs
│  ├─ Models/
│  │  ├─ ConfirmedSettlementHistoryDocument.cs
│  │  └─ ReceiptDraftNotificationRequest.cs
│  ├─ Services/
│  │  ├─ ReceiptInteractionService.cs
│  │  ├─ ReceiptMainMessageService.cs
│  │  ├─ ReceiptMessageRenderer.cs
│  │  ├─ SettlementHistoryCosmosRepository.cs
│  │  └─ SettlementHistoryMessageRenderer.cs
│  ├─ Storage/
│  │  └─ BlobImageUploader.cs
│  ├─ TestData/
│  │  ├─ sample-receipt-draft-general-market.json
│  │  ├─ sample-receipt-draft-discount-market.json
│  │  ├─ sample-receipt-draft-liquor-tax-market.json
│  │  ├─ sample-receipt-draft-stacked-discount-market.json
│  │  └─ sample-receipt-draft-restaurant-tip.json
│  └─ Observability/
│     └─ Telemetry.cs
├─ Dockerfile
├─ README.md
└─ codex.md
```

## Runtime Flow (current)
### HTTP receiver
- Kestrel이 기본적으로 `0.0.0.0:5000`에서 리슨한다.
- `POST /getting_draft`로 parser callback payload를 받으면 validation 후 `ReceiptDraftSessionService`로 넘긴다.
- `uploadedByUserId`가 없으면 현재는 validation 단계에서 실패시켜 500 대신 입력 오류로 드러나게 했다.

### `/test`
1. slash 실행
2. 샘플 draft JSON 로드
3. 실행 사용자 id로 payload 덮어쓰기
4. 기존 receipt session/UI 생성 경로 재사용
5. 현재 채널에 테스트 UI 전송
6. 이 명령은 Development 환경에서만 등록된다.
7. 현재 할인 검증용 scenario(`Discount Market`, `Stacked Discount Market`)도 포함한다.

### `/pingtest`
- 즉시 ephemeral 응답: `pong! slash command 정상 작동 중입니다.`
- 이 명령도 Development 환경에서만 등록된다.

### `/settle-up`
1. slash 실행
2. ephemeral 메시지에 `영수증 업로드` 버튼 표시
3. 버튼 클릭 시 모달 열기
4. 모달 내 file upload component로 파일 제출
5. 제출 attachment를 Blob에 업로드
6. 공개 채널에 pending 메시지를 먼저 올림
7. Blob 업로드 성공 후 parser가 draft callback을 보내면 기존 pending 메시지를 draft check 메시지로 교체

### `/history`
- `/history list`
  - 현재 사용자(owner)가 confirm한 최근 history를 최신순으로 보여준다.
- `/history detail index:<번호>`
  - 현재 시점 기준 최신순 `index`번째 history 상세를 보여준다.

### `/language`
- 사용자는 `English` 또는 `Korean`을 선택할 수 있다.
- 기본값은 English다.
- private/ephemeral/history는 호출 사용자 언어를 따른다.
- 공개 receipt 메인 메시지는 owner 언어를 따른다.
- owner가 언어를 바꾸면 진행 중 공개 receipt 메시지도 함께 refresh된다.

### `/custom`
1. slash 실행
2. optional `payment_contact`를 받는다.
3. 빈 receipt session을 생성한다.
4. 현재 채널에 공개 check 메시지를 바로 전송한다.
5. owner가 `Add item` 등 기존 조작 UI로 수동 입력한다.
6. item이 1개 이상이고 모두 배정됐을 때 confirm 가능하다.

## Environment Variables (currently used)
필수/준필수:
- `DISCORD_BOT_TOKEN`
- `AZURE_BLOB_CONTAINER_NAME`
- `AZURE_BLOB_CONNECTION_STRING` **or** `AZURE_BLOB_ACCOUNT_URL`

선택:
- `OTEL_SERVICE_NAME` (기본값: `discord-api`)
- `APPLICATIONINSIGHTS_CONNECTION_STRING`
- `ASPNETCORE_URLS` (기본값: `http://0.0.0.0:5000`)
- `SettlementHistory__CosmosConnectionString` 또는 `SettlementHistory__CosmosAccountEndpoint`
- `SettlementHistory__CosmosDatabaseId`
- `SettlementHistory__CosmosContainerId`

추가 참고:
- `Program.cs`에서 `DotNetEnv`로 `../.env`를 로드함 (`Env.Load("../.env")`).
- 실행 위치/배포 환경에 따라 `.env` 경로가 기대와 다를 수 있으니 주의 필요.

## Blob Upload Notes
- 구현 파일: `src/Storage/BlobImageUploader.cs`
- 업로드 경로 패턴:
  - `receipts/{yyyy}/{MM}/{dd}/{userId}/{guid}.{ext}`
- MIME은 확장자 기준으로 `image/jpeg` 또는 `image/png` 설정.

## Observability Notes
- console은 `ILogger` 중심의 사람이 읽기 쉬운 structured log만 출력한다.
- OpenTelemetry trace는 `shared/SettleUp.Observability` bootstrap으로 구성한다.
- `APPLICATIONINSIGHTS_CONNECTION_STRING`이 있으면 Azure Monitor / Application Insights로 trace를 export한다.
- connection string이 없으면 exporter 없이 계속 실행한다.
- `System.Net.Http` raw activity dump는 더 이상 콘솔에 직접 출력하지 않는다.

## Current Constraints / Next Step
- `docs/decisions/007-use-http-for-communication-between-parser-discordapi`에 따라 기본 callback endpoint 골격은 추가됐다.
- `docs/decisions/012-serialize-receipt-session-updates-and-debounce-public-message-publishing`와 `013-use-session-scoped-in-memory-cache-for-discord-receipt-ui`는 현재 동시성/성능 최적화의 기준 문서다.
- 현재 로컬과 Azure 둘 다 기준으로 receipt upload -> pending -> parser draft -> check -> confirm -> history 저장/조회까지 동작 확인이 끝났다.
- language command는 구현됐고, 현재 공개 메시지는 owner 언어 기준, private/ephemeral/history는 사용자 언어 기준으로 동작한다.
- 공개 Discord 메시지는 사용자별로 다른 disabled 상태나 다른 언어를 줄 수 없다는 제약이 여전히 있다.
- `/custom`은 parser 없이 빈 정산 세션을 여는 진입점으로 추가됐다.
- 그 다음 축은 계속 리팩터링과 callback 검증 강화다.
- shared observability project를 참조하므로 Dockerfile과 workflow는 repository-root build context를 기준으로 유지해야 한다.

## Known Decisions / Open Items
1. Blob 자동 삭제 30일
- 사용자 요청이 있었지만 해당 작업은 "취소" 요청으로 중단됨.
- 아직 코드/정책 반영 안 됨.
- 권장 방식은 Azure Storage Lifecycle Management 정책으로 처리.

2. 모달 파일 업로드 컴포넌트 호환성
- 현재 `Discord.Net 3.19.0` 기준으로 빌드는 통과.
- 실제 디스코드 클라이언트 동작은 환경/권한 설정과 함께 실사용 검증 필요.

3. `.env.example` 추적
- `services/discord-api/.env.example` 파일은 존재하지만 현재 `.gitignore` 영향으로 git 추적되지 않음.

4. 공개 메시지의 per-user disable 불가
- Discord 공개 메시지 버튼은 사용자별로 enabled/disabled 상태를 다르게 줄 수 없다.
- owner 전용 버튼도 현재는 non-owner가 클릭 자체는 가능하고, 서버에서 권한 체크 후 ephemeral로 막는다.
- 이 제약 때문에 공개 메시지 언어도 사용자별이 아니라 owner 기준 하나로 고정한다.

5. UI language switching
- `/language`는 구현 완료됐다.
- 사용자 설정은 메모리 기반이라 재시작 시 초기화된다.
- slash command 메타데이터는 사용자별 현지화를 하지 않고 쉬운 영어로 유지한다.

## Next Codex Session Quick Start
1. `/getting_draft` 인증/검증 규칙 추가
2. language 설정을 영구 저장해야 하는지 재검토
3. `/custom` 사용 흐름에서 추가 편집 UX가 필요한지 확인
4. discount 귀속 실패가 실제로 얼마나 나오는지 운영 샘플 확인
5. Dockerfile / workflow가 shared project build context를 계속 만족하는지 확인
6. 필요 시 `docs/decisions/019`, `020`, `021` 포함 관련 ADR을 먼저 확인한다
7. 변경 후 검증:
- `dotnet build services/discord-api/src/DiscordApi.csproj -c Release`

## Last Verified State
- `dotnet build services/discord-api/src/DiscordApi.csproj -c Release` 성공
- Docker build succeeds only when repository-root build context is used so shared observability project is included
- `/test` 기준 select/add/remove/edit/confirm 흐름과 private panel lifecycle 정리 확인
- `/history list`, `/history detail index:<번호>` 동작 확인
- tax/tip/alcohol UI와 history 저장/조회가 로컬과 Azure 둘 다에서 동작 확인됨
- 공개 메인 메시지 수정 경로, 세션 직렬화, 1초 디바운스, 메시지 캐시, render context 캐시, retry 적용 상태
- rendered embed cache와 history Cosmos container lazy initialization 리팩터링 반영
- add item 후 edit 시 발생하던 `Modal CustomId <= 100` 오류는 짧은 edit token 매핑으로 수정
- check 공개 메시지 item 순서는 원본 영수증 순서 유지로 변경
- confirmed embed 필드명은 `Pay to`로 변경
- confirm / add item 후 별도 텍스트 응답 없이 embed만 남도록 정리
- owner 전용 `Cancel` 버튼으로 공개 메시지 + 열린 private panel + session cleanup 가능
- cancel 시 `Discord 10008 Unknown Message` 오류는 재현 후 수정했고 현재 빌드 통과 상태
- `/pingtest`, `/test`는 Development 환경에서만 등록되도록 변경됨
- item-level discount와 stacked discount UI가 `/test` scenario로 확인 가능
- `/language`와 owner-language public message 정책이 반영됨
- `/custom`으로 parser 없이 빈 receipt 정산 세션 생성 가능

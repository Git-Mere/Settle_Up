# codex.md

## Service Name
- `receipt-parser`

## Session Summary (updated)
이번 세션까지의 `receipt-parser`는 "discord-api HTTP callback 전환 + delivery 상태 추적 + retry 추가 + 실제 Azure Blob URL 기준 `uploadedByUserId` 추출 수정 + persistence/parsing flow 리팩터링 + item-level discount 정규화 + startup warm-up" 상태다.

1. Blob 생성 이벤트 수신
- 엔드포인트: `POST /api/events/blob-created`
- Event Grid payload를 파싱하고 구독 검증 이벤트를 처리한다.

2. 영수증 파싱 수행 (Document Intelligence)
- Blob URL을 기반으로 이미지 데이터를 읽는다.
- Azure Document Intelligence `prebuilt-receipt` 모델을 호출한다.
- 결과에서 `merchantName`, `subtotal`, `tax`, `total`, `transactionDate`, `items`를 추출한다.
- `parseMetadata`에 `modelId`, `merchantConfidence`, `totalConfidence`를 포함한다.

3. 파싱 결과 저장
- 파싱 결과를 `ReceiptDocument`로 구성해 Cosmos DB에 upsert한다.
- 현재 Cosmos 컨테이너 파티션 키는 `/Id` 기준으로 사용한다.
- 문서는 Cosmos 요구사항에 맞춰 `id`와 파티션 키용 `Id`를 함께 직렬화한다.
- 저장 문서는 아래 계약 필드를 따른다:
  - `id`, `status`, `blobUrl`, `uploadedByUserId`, `merchantName`, `transactionDate`
  - `currency`, `subtotal`, `tax`, `total`, `items`, `parseMetadata`
  - `notificationStatus`, `notificationAttemptCount`, `lastNotificationAttemptAt`, `notificationSentAtUtc`, `lastNotificationError`
  - `createdAtUtc`, `updatedAtUtc`

4. discord-api HTTP 전송
- 파싱/저장 후 `POST /getting_draft`로 초안 payload를 JSON으로 전송한다.
- 운영 경로는 `ReceiptParser__DiscordApiUrl`, 로컬 업로드 테스트 경로는 `ReceiptParser__DiscordApiUrl_local_test`를 사용한다.
- 저장 문서(`ReceiptDocument`)와 outbound payload(`DiscordDraftNotificationPayload`)는 분리했다.
- retry는 최대 3회까지 수행하며, retryable 대상은 네트워크 오류/timeout/5xx/408/429다.
- 400/401/403/404 등 non-transient 4xx는 재시도하지 않는다.

5. `uploadedByUserId` 추출 수정
- 실제 Azure Blob URL 패턴은 `receipts/{yyyy}/{MM}/{dd}/{userId}/{file}`이며, parser는 이제 이 경로에서 `userId`를 올바르게 추출한다.
- 이전에는 `<container>/receipts/{yyyy}/{MM}/{dd}/{userId}/{file}`를 기대하는 오프셋 버그가 있어 `uploadedByUserId`가 null이 될 수 있었다.
- backward-compatible fallback도 남겨 두었다.
- `discord-api`는 현재 `uploadedByUserId`를 owner 권한 모델과 draft session 생성에 사용하므로, 이 필드는 callback 계약상 실질적인 필수 필드다.

6. 로컬 테스트 보조 엔드포인트
- `ReceiptParser__EnableLocalUploadTestEndpoint=true`일 때
  `POST /api/tests/local-upload-parse`를 활성화한다.
- 테스트 엔드포인트도 운영 경로와 동일하게 파싱 후 Cosmos 저장 및 discord-api HTTP 전송 시도를 수행한다.
- 테스트 엔드포인트 응답도 discord-api로 보내는 outbound payload 스키마를 반환한다.
- 요청 완료 로그는 `ILogger` 기반 structured log로 남긴다.

7. Cosmos 인증 전략 정리
- 로컬 테스트 편의를 위해 connection string과 Azure IAM(RBAC) 둘 다 지원한다.
- `ReceiptParser__CosmosConnectionString`이 있으면 이를 우선 사용한다.
- 없으면 `ReceiptParser__CosmosAccountEndpoint` + `DefaultAzureCredential`로 연결한다.

8. Observability / logging 정리
- `shared/SettleUp.Observability`를 참조하도록 변경.
- console은 `ILogger` 기반 structured log 중심으로 정리하고 OpenTelemetry raw console dump는 제거했다.
- `APPLICATIONINSIGHTS_CONNECTION_STRING`이 있으면 Azure Monitor exporter를 활성화한다.
- 이벤트 수신, 파싱 시작/완료/실패, Cosmos upsert 시작/완료/실패, discord-api send 시작/성공/재시도/최종 실패를 의미 있는 application log로 남긴다.

9. 리팩토링
- `ReceiptProcessingService`에서 문서/outbound payload 생성 로직을 분리:
  - `BuildReceiptDocument(...)`
-  - `BuildDiscordDraftNotificationPayload(...)`
- 공통 후처리 로직을 `SaveAndSendDraftAsync(...)`로 묶어 로컬/운영 경로가 같은 저장 및 전송 흐름을 사용한다.
- `EventGridWebhookEndpoint`에서 payload 파싱을 `TryParseEventsAsync(...)`로 분리해 가독성 개선.
- 원문 OCR 결과(`rawResultJson`) 저장을 제거해 Cosmos 저장 문서를 정규화 필드만 포함하도록 정리했다.

10. 최근 성능 / 구조 정리
- Cosmos 저장소는 이제 container를 lazy 초기화해 매 save마다 `CreateContainerIfNotExistsAsync`를 반복 호출하지 않는다.
- Blob 다운로드는 `DownloadToAsync + MemoryStream + BinaryData.FromStream` 대신 `DownloadContentAsync`로 바로 `BinaryData`를 사용한다.
- `DocumentIntelligenceReceiptParser`는 `DefaultAzureCredential` 인스턴스를 재사용한다.
- `DiscordApiDraftClient`는 실제 사용하지 않는 마지막 retry delay 항목을 제거했다.
- `ReceiptProcessingService`의 sent/pending notification 문서 생성은 공통 helper로 정리했다.

11. 할인 line 처리 정리
- OCR 결과의 음수 금액 line은 일반 item으로 그대로 넘기지 않는다.
- 우선 직전 일반 item에 item-level discount로 귀속을 시도한다.
- 같은 item 아래 할인 line이 연속되면 같은 item에 누적한다.
- 귀속 성공 item은 원가와 할인 금액을 함께 보존하도록 정규화한다.
- 귀속 실패 할인은 downstream 계산과 UI에서 자동 반영하지 않는다.
- 이 정책은 `docs/decisions/019-attribute-negative-receipt-lines-to-the-previous-item-and-ignore-unmatched-discounts.md`를 따른다.

12. 최근 성능 정리
- startup 시 `ParserWarmupService`가 parser warm-up을 수행한다.
- Azure AD 인증 경로를 쓸 때 `DocumentIntelligenceReceiptParser`는 Document Intelligence와 Storage scope token을 미리 받아 first-hit 지연을 줄인다.
- `CosmosReceiptRepository`는 startup warm-up에서 container initialization을 먼저 수행한다.
- 이 변경의 목적은 첫 영수증 처리의 cold path를 줄이는 것이다.
- 관련 조사 문서는 `docs/problem-searching/performance-review-2026-04-04.md`에 있다.

## Current File Layout (relevant)
```text
services/receipt-parser/
├─ Program.cs
├─ receipt-parser.csproj
├─ appsettings.json
├─ .env.example
├─ Configuration/
│  └─ ReceiptParserOptions.cs
├─ Endpoints/
│  └─ EventGridWebhookEndpoint.cs
├─ Services/
│  ├─ ReceiptProcessingService.cs
│  ├─ DocumentIntelligenceReceiptParser.cs
│  ├─ CosmosReceiptRepository.cs
│  └─ DiscordApiDraftClient.cs
├─ Models/
│  ├─ ParsedReceiptResult.cs
│  ├─ ParsedReceiptItem.cs
│  ├─ ParseMetadata.cs
│  ├─ ReceiptDocument.cs
│  └─ DiscordDraftNotificationPayload.cs
├─ Observability/
│  └─ Telemetry.cs
└─ tests/
   └─ LocalUploadTest/
```

## Runtime Flow (current)
1. `discord-api`가 영수증 이미지를 Azure Blob Storage에 업로드
2. Blob 생성 이벤트가 Event Grid를 통해 `receipt-parser`로 전달
3. `receipt-parser`가 이벤트에서 blob URL 추출
4. Document Intelligence(`prebuilt-receipt`)로 분석 수행
5. 결과를 내부 모델로 파싱하고 Cosmos DB에 `notificationStatus=Pending` 상태로 저장
6. `receipt-parser`가 `discord-api`의 `/getting_draft`로 HTTP POST 전송
7. 성공 시 Cosmos DB 문서를 `notificationStatus=Sent`로 업데이트
8. 실패 시 pending 상태와 시도 횟수/오류를 남겨 재처리 가능하게 유지

로컬 테스트 플로우:
1. `POST /api/tests/local-upload-parse`로 이미지 업로드
2. `receipt-parser`가 Document Intelligence로 분석 수행
3. Cosmos DB에 pending 상태로 저장
4. `ReceiptParser__DiscordApiUrl_local_test`를 사용해 `discord-api`로 HTTP 전송
5. 성공 시 sent 상태로 업데이트
6. 동일한 outbound payload 스키마를 응답으로 반환

샘플 payload 형태:
```json
{
  "id": "8c7c2c3a-7f42-4dd1-9a0f-123456789abc",
  "status": "Parsed",
  "blobUrl": "https://...",
  "uploadedByUserId": "discordUser123",
  "merchantName": "Costco",
  "transactionDate": "2026-03-08",
  "currency": "USD",
  "subtotal": 20.99,
  "tax": 2.5,
  "total": 23.49,
  "items": [
    {
      "id": "item1",
      "description": "Pizza",
      "quantity": 1,
      "unitPrice": 12.99,
      "totalPrice": 12.99,
      "originalUnitPrice": 12.99,
      "originalTotalPrice": 12.99,
      "discountAmount": 0
    }
  ],
  "parseMetadata": {
    "modelId": "prebuilt-receipt",
    "merchantConfidence": 0.97,
    "totalConfidence": 0.99
  },
  "createdAtUtc": "2026-03-08T20:00:00Z",
  "updatedAtUtc": "2026-03-08T20:00:00Z"
}
```

## Environment Variables (currently used)
필수/준필수:
- `ReceiptParser__DocumentIntelligenceEndpoint`
- `ReceiptParser__CosmosConnectionString` 또는 `ReceiptParser__CosmosAccountEndpoint`

권장:
- `ReceiptParser__DocumentIntelligenceApiKey`
- `ReceiptParser__ModelId` (기본값 `prebuilt-receipt`)
- `ReceiptParser__CosmosDatabaseId` (기본값 `draft-receipt-db`)
- `ReceiptParser__CosmosContainerId` (기본값 `draft-receipt`)
- `ReceiptParser__DiscordApiUrl`
- `ReceiptParser__DiscordApiUrl_local_test`
- `ReceiptParser__EnableLocalUploadTestEndpoint`
- `OTEL_SERVICE_NAME` (기본값 `receipt-parser`)
- `APPLICATIONINSIGHTS_CONNECTION_STRING`

Cosmos 인증:
- `ReceiptParser__CosmosConnectionString`이 있으면 이를 우선 사용한다.
- 없으면 `CosmosAccountEndpoint` + `DefaultAzureCredential`로 생성한다.
- Azure 배포 시 Managed Identity에 Cosmos DB data-plane RBAC 권한이 필요하다.
- 로컬에서는 connection string 또는 `az login`/개발 도구 로그인 자격 증명을 사용한다.

`.env` 로딩 동작:
- `Program.cs`에서 아래 순서로 탐색 후 처음 발견된 파일을 로드한다.
  - `<cwd>/.env`
  - `<cwd>/services/receipt-parser/.env`

## Observability Notes
- console은 `ILogger` 기반 application log 중심으로 출력한다.
- OpenTelemetry trace는 `shared/SettleUp.Observability` bootstrap에서 공통 구성한다.
- ASP.NET Core / HttpClient instrumentation과 커스텀 activity source를 함께 사용한다.
- `APPLICATIONINSIGHTS_CONNECTION_STRING`이 있으면 Azure Monitor / Application Insights로 trace를 export한다.
- connection string이 없으면 exporter 없이 서비스가 계속 실행된다.

## Known Decisions / Open Items
1. Item-level 정확도
- 현재 `Items` 파싱은 문서 필드 구조(`ValueList`/`ValueDictionary`) 기반 1차 매핑이다.
- 실제 영수증 포맷별 정확도/정규화는 추가 검증이 필요하다.
- 할인도 현재는 "직전 일반 item" 규칙만 사용한다.
- OCR 순서가 흔들리거나 전역 할인인 경우 자동 귀속은 하지 않는다.

2. 신뢰 경계 및 검증 강화
- Event Grid payload 및 blob URL 검증 규칙을 더 엄격하게 정의할 필요가 있다.

3. Downstream contract 확정
- HTTP callback payload 스키마를 `discord-api` 소비 요구사항과 맞춰 더 엄격히 검증할 필요가 있다.
- 현재 `uploadedByUserId`는 사실상 필수 계약 필드다. callback 계약을 바꿀 때는 parser와 discord-api 양쪽 validation을 함께 수정해야 한다.

4. Currency 추론 로직
- `CurrencyCode`가 없을 때 `$` 기준으로 `USD`를 추론한다.
- 다국적 통화 처리 정책은 추가 정의가 필요하다.

5. language command 후속 영향
- `discord-api`에 language command가 추가됐다.
- parser payload 필드 자체는 localization과 직접 연결되지 않지만, downstream UI가 item-level discount 문구를 언어별로 렌더링할 수 있게 됐다.

## Next Codex Session Quick Start
1. 실제 Azure 환경에서 callback payload에 `uploadedByUserId`가 계속 안정적으로 들어가는지 재검증
2. discount 귀속 실패가 실제 영수증에서 얼마나 나오는지 샘플로 확인
3. discord-api callback 인증/검증 규칙 추가
4. 전송 실패 문서 재처리 경로 설계
5. 추가 리팩터링이 필요하면 parsing / persistence / delivery 경계를 유지한 채 진행
6. Docker/CI workflow가 shared project build context를 계속 만족하는지 확인
7. 관련 decision 문서를 추가할 경우 `docs/decisions/README.md` 포맷과 번호 체계를 따른다
8. 변경 후 검증:
- `dotnet build services/receipt-parser/receipt-parser.csproj -c Release`

## Last Verified State
- 로컬 코드 기준으로 Event Grid -> Document Intelligence -> Cosmos -> discord-api HTTP callback 파이프라인 코드가 존재
- 저장 문서와 outbound HTTP payload를 분리했다.
- 원문 OCR 결과(`rawResultJson`)는 저장하지 않음
- 로컬 업로드 테스트 엔드포인트도 운영 경로와 동일하게 Cosmos 저장 및 discord-api HTTP 전송 시도 수행
- Cosmos 저장은 현재 컨테이너 계약(`/Id` partition key)에 맞춰 동작 확인
- 빌드 검증: `dotnet build services/receipt-parser/receipt-parser.csproj -c Release` 성공
- Docker build succeeds only when repository-root build context is used so shared observability project is included
- 실제 Azure 테스트에서 `uploadedByUserId` null로 callback이 실패하던 이슈를 확인했고, Blob path 추출 로직을 수정했다
- 로컬과 Azure 둘 다 기준으로 callback 포함 전체 파이프라인 동작 확인됨
- Cosmos lazy container init, blob download path simplification, notification document builder 정리까지 반영됨
- item-level discount 정규화가 반영됐고, 귀속 실패 할인은 자동 반영하지 않는 정책으로 정리됨
- parser startup warm-up이 추가돼 first-hit latency를 줄이는 방향으로 정리됨
- next planned change: callback auth/reprocessing + continued refactoring

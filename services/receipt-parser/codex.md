# codex.md

## Service Name
- `receipt-parser`

## Session Summary (updated)
이번 세션에서 `receipt-parser`는 "Cosmos 저장 경로 정렬 + 로컬/운영 처리 경로 통합 + 저장 스키마 정리"를 진행했다.

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
  - `createdAtUtc`, `updatedAtUtc`

4. 파싱 완료 이벤트 발행
- Downstream Event Grid topic 설정이 있으면 `SettleUp.ReceiptParsed` 이벤트를 발행한다.
- 설정이 없으면 경고 로그만 남기고 발행은 건너뛴다.

5. 로컬 테스트 보조 엔드포인트
- `ReceiptParser__EnableLocalUploadTestEndpoint=true`일 때
  `POST /api/tests/local-upload-parse`를 활성화한다.
- 테스트 엔드포인트도 운영 경로와 동일하게 파싱 후 Cosmos 저장 및 downstream 이벤트 발행 시도를 수행한다.
- 테스트 엔드포인트 응답도 운영 이벤트와 동일한 payload 스키마를 반환한다.
- 같은 payload를 콘솔에 pretty JSON으로 출력한다.

6. Cosmos 인증 전략 정리
- 로컬 테스트 편의를 위해 connection string과 Azure IAM(RBAC) 둘 다 지원한다.
- `ReceiptParser__CosmosConnectionString`이 있으면 이를 우선 사용한다.
- 없으면 `ReceiptParser__CosmosAccountEndpoint` + `DefaultAzureCredential`로 연결한다.

7. 리팩토링
- `ReceiptProcessingService`에서 문서/이벤트 payload 생성 로직을 분리:
  - `BuildReceiptDocument(...)`
  - `BuildReceiptParsedEventPayload(...)`
- 공통 후처리 로직을 `SaveAndPublishAsync(...)`로 묶어 로컬/운영 경로가 같은 저장 및 발행 흐름을 사용한다.
- `EventGridWebhookEndpoint`에서 payload 파싱을 `TryParseEventsAsync(...)`로 분리해 가독성 개선.
- 원문 OCR 결과(`rawResultJson`) 저장을 제거해 Cosmos 저장 문서를 정규화 필드만 포함하도록 정리했다.

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
│  └─ ReceiptParsedEventPublisher.cs
├─ Models/
│  ├─ ParsedReceiptResult.cs
│  ├─ ParsedReceiptItem.cs
│  ├─ ParseMetadata.cs
│  ├─ ReceiptDocument.cs
│  └─ ReceiptParsedEventPayload.cs
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
5. 결과를 내부 모델로 파싱하고 Cosmos DB 저장
6. 파싱 완료 이벤트를 Event Grid로 발행
7. `discord-api`가 해당 이벤트를 구독해 후속 메시지를 생성

로컬 테스트 플로우:
1. `POST /api/tests/local-upload-parse`로 이미지 업로드
2. `receipt-parser`가 Document Intelligence로 분석 수행
3. 운영 경로와 동일하게 Cosmos DB 저장
4. Downstream Event Grid 발행 설정이 있으면 동일하게 발행 시도
5. 동일한 payload 스키마를 응답으로 반환

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
      "totalPrice": 12.99
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
- `ReceiptParser__CosmosDatabaseId`
- `ReceiptParser__CosmosContainerId`
- `ReceiptParser__DownstreamEventGridTopicEndpoint`
- `ReceiptParser__DownstreamEventGridTopicKey`
- `ReceiptParser__DownstreamEventType`
- `ReceiptParser__EnableLocalUploadTestEndpoint`
- `OTEL_SERVICE_NAME` (기본값 `receipt-parser`)

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
- OpenTelemetry tracing이 활성화되어 있으며 콘솔 exporter를 사용한다.
- ASP.NET Core / HttpClient instrumentation과 커스텀 activity source를 함께 사용한다.
- 현재 운영 모니터링은 로그 중심으로 사용한다.

## Known Decisions / Open Items
1. Item-level 정확도
- 현재 `Items` 파싱은 문서 필드 구조(`ValueList`/`ValueDictionary`) 기반 1차 매핑이다.
- 실제 영수증 포맷별 정확도/정규화는 추가 검증이 필요하다.

2. 신뢰 경계 및 검증 강화
- Event Grid payload 및 blob URL 검증 규칙을 더 엄격하게 정의할 필요가 있다.

3. Downstream contract 확정
- `SettleUp.ReceiptParsed` payload 스키마를 `discord-api` 소비 요구사항과 맞춰 고정해야 한다.

4. Currency 추론 로직
- `CurrencyCode`가 없을 때 `$` 기준으로 `USD`를 추론한다.
- 다국적 통화 처리 정책은 추가 정의가 필요하다.

## Next Codex Session Quick Start
1. `Services/DocumentIntelligenceReceiptParser.cs`에서 아이템/금액 파싱 정확도 개선
2. `Services/ReceiptProcessingService.cs`에서 blob 경로 기반 `uploadedByUserId` 추출 규칙 고도화
3. `Services/CosmosReceiptRepository.cs`에서 인덱싱/파티션 전략 재검토
4. `Services/ReceiptParsedEventPublisher.cs`와 `shared/contracts`의 이벤트 스키마 정렬
5. 변경 후 검증:
- `dotnet build services/receipt-parser/receipt-parser.csproj -c Release`

## Last Verified State
- 로컬 코드 기준으로 Event Grid -> Document Intelligence -> Cosmos -> Downstream Event Grid 파이프라인 코드가 존재
- 표준 파싱 payload 스키마(`id/status/items/parseMetadata/timestamps`) 적용 완료
- 원문 OCR 결과(`rawResultJson`)는 저장하지 않음
- 로컬 업로드 테스트 엔드포인트도 운영 경로와 동일하게 Cosmos 저장 및 downstream 발행 시도 수행
- Cosmos 저장은 현재 컨테이너 계약(`/Id` partition key)에 맞춰 동작 확인
- 빌드 검증: `dotnet build services/receipt-parser/receipt-parser.csproj -c Release --no-restore` 성공

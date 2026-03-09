# AGENTS.md

## Service Overview
이 서비스는 Settle Up 프로젝트의 영수증 파싱 파이프라인을 담당하는 C# 기반 웹 서비스다.

주요 역할:
- Azure Event Grid로 전달된 Blob 생성 이벤트 수신
- Azure Blob Storage의 영수증 이미지 조회
- Azure Document Intelligence `prebuilt-receipt` 모델로 분석 수행
- 파싱 결과를 정규화해 Cosmos DB에 저장
- 파싱 완료 이벤트를 Downstream Event Grid로 발행

## Current Scope
현재 우선순위:
- 이벤트 수신 엔드포인트를 안정적으로 동작시키기
- Document Intelligence 호출과 결과 파싱 안정화
- 파싱 결과 JSON 스키마를 서비스 간 계약으로 일관되게 유지
- Cosmos DB 저장 구조를 단순하고 명확하게 유지
- Downstream 이벤트 발행 연결
- 로컬 `.env` 기반 실행/테스트 흐름 유지

## Expected Configuration
환경별 설정과 민감값은 반드시 환경 변수로 관리한다.

주요 변수 예시:
- `ReceiptParser__DocumentIntelligenceEndpoint`
- `ReceiptParser__DocumentIntelligenceApiKey`
- `ReceiptParser__ModelId` (기본값: `prebuilt-receipt`)
- `ReceiptParser__CosmosConnectionString`
- `ReceiptParser__CosmosAccountEndpoint`
- `ReceiptParser__CosmosDatabaseId`
- `ReceiptParser__CosmosContainerId`
- `ReceiptParser__DownstreamEventGridTopicEndpoint`
- `ReceiptParser__DownstreamEventGridTopicKey`
- `ReceiptParser__DownstreamEventType`
- `ReceiptParser__EnableLocalUploadTestEndpoint`
- `OTEL_SERVICE_NAME`

토큰/키/연결 문자열은 코드에 하드코딩하지 않는다.

Cosmos DB 연결은 로컬 편의를 위해 connection string과 Azure IAM(RBAC) 둘 다 지원한다.
`ReceiptParser__CosmosConnectionString`이 있으면 이를 우선 사용하고, 없으면 `ReceiptParser__CosmosAccountEndpoint` + `DefaultAzureCredential`로 연결한다.
배포 환경에서는 Managed Identity, 로컬 개발에서는 connection string 또는 Azure CLI / Visual Studio 로그인 자격 증명을 사용할 수 있다.

## Coding Guidelines
- 엔드포인트, 파싱, 저장소, 이벤트 발행 책임을 분리한다.
- 입력(payload, 이벤트 타입, 필수 필드)을 명시적으로 검증한다.
- 비동기 I/O는 `async/await`로 일관되게 처리한다.
- 실패 지점(Document Intelligence, Cosmos, Event Grid)을 로그로 추적 가능하게 남긴다.
- 모델/DTO는 서비스 경계를 드러내도록 명확한 이름을 사용한다.
- 상태값/매핑 규칙은 상수 또는 전용 빌더 함수로 중복 없이 관리한다.

## Parsed Receipt Contract (Current)
현재 기본 결과 객체는 아래 필드를 포함한다:
- `id`
- `Id` (Cosmos 컨테이너 partition key가 `/Id`일 때 저장 문서에 포함)
- `status` (`Parsed`)
- `blobUrl`
- `uploadedByUserId`
- `merchantName`
- `transactionDate` (`DateOnly`)
- `currency`
- `subtotal`
- `tax`
- `total`
- `items[]` (`id`, `description`, `quantity`, `unitPrice`, `totalPrice`)
- `parseMetadata` (`modelId`, `merchantConfidence`, `totalConfidence`)
- `createdAtUtc`
- `updatedAtUtc`

로컬 테스트 엔드포인트도 동일 스키마로 응답해야 하며, 가능하면 운영 경로와 동일한 저장/발행 흐름을 재사용한다.

## Eventing Guidelines
- `Microsoft.EventGrid.SubscriptionValidationEvent` 처리를 반드시 지원한다.
- 지원하지 않는 이벤트 타입은 안전하게 스킵한다.
- `Microsoft.Storage.BlobCreated`에서 `url` 추출 실패 시 저장/발행을 진행하지 않는다.
- 다운스트림 발행 설정이 없으면 경고 로그를 남기고 발행은 건너뛴다.
- Blob 경로 패턴에서 `uploadedByUserId` 추출 규칙을 바꿀 경우 `discord-api`와 함께 계약을 갱신한다.

## Security Guidelines
- 외부 이벤트 payload는 신뢰하지 않고 항상 검증한다.
- Blob URL 입력은 예상 스키마/형식 기준으로 방어적으로 처리한다.
- 실제 운영 비밀값은 환경 변수 또는 Key Vault를 사용한다.
- 원문 OCR 결과(JSON)는 현재 저장하지 않는다. 필요 시 다시 도입하기 전 민감정보 처리 정책을 먼저 정의한다.

## Observability Guidelines
- OpenTelemetry는 현재 로그/트레이싱 중심으로 사용한다.
- 주요 구간(이벤트 수신, 파싱, 저장, 이벤트 발행)에 activity를 남긴다.
- 로컬에서는 콘솔 exporter 기반으로 동작 확인을 우선한다.

## Docker / CI Direction
- 서비스 단독 빌드 및 실행이 가능해야 한다.
- Docker 빌드 시 불필요한 파일 포함을 최소화한다.
- CI는 최소 `restore -> build -> test -> image build` 단계를 목표로 한다.

## Integration Direction
이 서비스는 향후 다음 상호작용을 전제로 설계한다:
- Upstream: `discord-api`가 Blob 업로드 후 이벤트를 생성
- Internal: `receipt-parser`가 분석 및 DB 저장
- Downstream: 파싱 완료 이벤트를 발행하고 `discord-api`가 구독해 사용자 메시지를 생성

## Documentation Rule
서비스 구조/흐름/환경 변수가 바뀌면 다음 문서를 함께 업데이트한다:
- `services/receipt-parser/codex.md`
- 서비스 README(추가 시)

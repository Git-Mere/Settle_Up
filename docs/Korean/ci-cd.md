# CI/CD

Settle Up은 현재 서비스별 GitHub Actions workflow를 사용하는 방향을 따른다.

이 문서는 현재 저장소의 CI/CD 구조, workflow 범위, Docker build 규칙, Azure 배포 전제, 그리고 운영 시 주의할 점을 정리한다.

## Current Approach

현재 원칙:

- 서비스별로 독립 workflow를 둔다
- 최소 단계는 `restore -> build -> publish -> docker build -> push`
- shared project가 있더라도 배포 단위는 서비스별로 유지한다

현재 workflow:

- `discord-api CI/CD`
- `receipt-parser CI`
- `settlement-service CI` placeholder

## Workflow Inventory

### `discord-api CI/CD`

대상:

- `services/discord-api/**`
- `.github/workflows/discord-api-ci-cd.yml`

주요 단계:

1. checkout
2. setup .NET 8
3. `dotnet restore`
4. `dotnet build -c Release`
5. `dotnet publish -c Release`
6. repository root context로 Docker image build
7. ACR login
8. image push

생성 tag:

- `${{ github.sha }}`
- `latest`

### `receipt-parser CI`

대상:

- `services/receipt-parser/**`
- `.github/workflows/receipt-parser-ci.yml`

주요 단계:

1. checkout
2. setup .NET 8
3. `dotnet restore`
4. `dotnet build -c Release`
5. `dotnet publish -c Release`
6. repository root context로 Docker image build
7. ACR login
8. image push

### `settlement-service CI`

현재는 placeholder workflow만 존재한다.

의미:

- 향후 별도 서비스가 만들어질 수 있음을 반영
- 아직 실 구현/배포 파이프라인은 없음

## Build Context Policy

현재 저장소에서 가장 중요한 CI/CD 주의점 중 하나는 Docker build context다.

`discord-api`와 `receipt-parser`는 공통 observability 프로젝트를 참조한다.

즉:

- `shared/SettleUp.Observability`가 이미지 build 시 함께 보여야 한다
- Docker build는 서비스 폴더가 아니라 repository root context를 기준으로 해야 한다

현재 workflow도 이 전제를 따른다.

예:

```bash
docker build \
  --file services/discord-api/Dockerfile \
  --platform linux/amd64 \
  .
```

이 규칙을 깨면 발생할 수 있는 문제:

- shared project restore/build 실패
- Dockerfile copy 단계 불일치
- CI만 실패하거나 로컬만 성공하는 상태

따라서 shared reference, Dockerfile copy path, workflow build context는 항상 함께 검토해야 한다.

## Release Artifacts

현재 서비스별 산출물:

- .NET publish output
- Docker image

이미지 저장소:

- Azure Container Registry

현재 workflow는 GitHub Secrets를 사용해 ACR 로그인한다.

필요 secret:

- `ACR_NAME`
- `ACR_USERNAME`
- `ACR_PASSWORD`

## Azure Deployment Model

현재 코드/설정 방향상 배포 대상은 Azure Container Apps를 전제로 한다.

주요 운영 설정:

- Container App environment variables
- Managed Identity
- Key Vault secret reference
- Azure Blob Storage / Event Grid / Cosmos / Document Intelligence 연동

### Secret Strategy

현재 권장 방향:

- 로컬: `.env` 또는 일반 environment variable
- Azure: Container App environment + Key Vault reference

현재 코드에서 alias fallback을 지원하는 secret 이름:

- `discord-bot-token` -> `DISCORD_BOT_TOKEN`
- `applicationinsights-connection-string` -> `APPLICATIONINSIGHTS_CONNECTION_STRING`
- `ReceiptParser-DocumentIntelligenceApiKey` -> `ReceiptParser__DocumentIntelligenceApiKey`

이 구조는 애플리케이션이 Key Vault SDK를 직접 호출하지 않고, Container App이 secret을 환경 변수로 주입하는 방식을 전제로 한다.

## Identity and Access

현재 서비스들은 일부 Azure 리소스에 대해 connection string과 IAM/RBAC를 모두 지원한다.

### `discord-api`

- Blob Storage:
  - `AZURE_BLOB_CONNECTION_STRING` 우선
  - 없으면 `AZURE_BLOB_ACCOUNT_URL + DefaultAzureCredential`

- Settlement history Cosmos:
  - `SettlementHistory__CosmosConnectionString` 우선
  - 없으면 `SettlementHistory__CosmosAccountEndpoint + DefaultAzureCredential`

### `receipt-parser`

- Document Intelligence:
  - `ReceiptParser__DocumentIntelligenceApiKey`가 있으면 key auth
  - 없으면 `DefaultAzureCredential`

- Cosmos draft store:
  - `ReceiptParser__CosmosConnectionString` 우선
  - 없으면 `ReceiptParser__CosmosAccountEndpoint + DefaultAzureCredential`

즉 CI/CD 이후 실제 배포 환경에서는:

- connection string 기반 운영도 가능
- Managed Identity 기반 운영도 가능

다만 둘 다 넣으면 현재 코드는 connection string을 우선 사용한다.

## Validation Expectations

현재 workflow에는 별도 테스트 단계가 거의 없거나 최소 수준이다.

현실적인 검증 기준:

- `dotnet restore`
- `dotnet build`
- `dotnet publish`
- Docker image build

추가로 사람이 자주 확인하는 항목:

- `/settle-up` 업로드 플로우
- `receipt-parser` callback
- 공개 receipt UI 갱신
- `/history`
- `/custom`
- `/language`

즉 현재 CI는 "컴파일/패키징 보장" 중심이고, 상호작용 UX 검증은 아직 수동 테스트 비중이 크다.

## Change Management Rules

다음 변경은 CI/CD 검토를 반드시 함께 해야 한다.

- Dockerfile 수정
- shared project reference 수정
- build context 수정
- environment variable 이름 수정
- authentication strategy 수정
- Container App secret/key vault naming 변경

이런 변경은 코드만 고치고 workflow를 안 보면 쉽게 drift가 생긴다.

## Current Gaps

- GitHub Actions에 자동 테스트 단계가 충분하지 않다
- Azure deployment apply 단계는 workflow에 직접 포함돼 있지 않다
- environment promotion 전략(dev/staging/prod) 문서화가 아직 약하다
- secret rotation 운영 절차는 아직 별도 문서가 없다

## Recommended Next Improvements

1. service별 smoke test 또는 최소 integration test를 CI에 추가
2. Container App deploy step을 workflow에 명시할지 결정
3. environment별 secret/config matrix를 문서화
4. Docker image tag 정책에 release tag 또는 semver 정책을 추가 검토
5. callback validation/health check를 운영 체크리스트에 포함

## Related Documents

- `docs/api.md`
- `docs/architecture.md`
- `docs/decisions/001-monorepo.md`
- `docs/decisions/007-use-http-for-communication-between-parser-discordapi.md`
- `docs/decisions/012-serialize-receipt-session-updates-and-debounce-public-message-publishing.md`
- `docs/decisions/013-use-session-scoped-in-memory-cache-for-discord-receipt-ui.md`

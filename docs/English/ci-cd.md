# CI/CD

Settle Up currently follows a service-specific GitHub Actions workflow model.

This document summarizes the current CI/CD structure in the repository, workflow scope, Docker build rules, Azure deployment assumptions, and operational cautions.

## Current Approach

Current principles:

- keep independent workflows per service
- use at least `restore -> build -> publish -> docker build -> push`
- even when shared projects exist, preserve service-by-service deployment units

Current workflows:

- `discord-api CI/CD`
- `receipt-parser CI`
- `settlement-service CI` placeholder

## Workflow Inventory

### `discord-api CI/CD`

Targets:

- `services/discord-api/**`
- `.github/workflows/discord-api-ci-cd.yml`

Main steps:

1. checkout
2. setup .NET 8
3. `dotnet restore`
4. `dotnet build -c Release`
5. `dotnet publish -c Release`
6. build Docker image using the repository root as context
7. ACR login
8. image push

Generated tags:

- `${{ github.sha }}`
- `latest`

### `receipt-parser CI`

Targets:

- `services/receipt-parser/**`
- `.github/workflows/receipt-parser-ci.yml`

Main steps:

1. checkout
2. setup .NET 8
3. `dotnet restore`
4. `dotnet build -c Release`
5. `dotnet publish -c Release`
6. build Docker image using the repository root as context
7. ACR login
8. image push

### `settlement-service CI`

A placeholder workflow currently exists.

Meaning:

- it reflects the possibility of future services
- there is no implemented build or deployment pipeline yet

## Build Context Policy

One of the most important CI/CD constraints in the current repository is Docker build context.

`discord-api` and `receipt-parser` both reference the shared observability project.

This means:

- `shared/SettleUp.Observability` must be visible during image build
- Docker builds must use the repository root as build context rather than the service folder as context

The current workflows follow this assumption.

Example:

```bash
docker build \
  --file services/discord-api/Dockerfile \
  --platform linux/amd64 \
  .
```

If this rule is broken, the following problems can occur.

- shared project restore/build failures
- Dockerfile copy-step mismatches
- CI failing while local builds appear to work, or vice versa

For that reason, shared references, Dockerfile copy paths, and workflow build context must always be reviewed together.

## Release Artifacts

Current per-service outputs:

- .NET publish output
- Docker image

Image registry:

- Azure Container Registry

Current workflows use GitHub Secrets for ACR login.

Required secrets:

- `ACR_NAME`
- `ACR_USERNAME`
- `ACR_PASSWORD`

## Azure Deployment Model

The current code and configuration direction assumes Azure Container Apps as the deployment target.

Main operational settings:

- Container App environment variables
- Managed Identity
- Key Vault secret references
- integration with Azure Blob Storage, Event Grid, Cosmos DB, and Document Intelligence

### Secret Strategy

Current recommended direction:

- local: `.env` or standard environment variables
- Azure: Container App environment plus Key Vault references

The current code supports the following alias fallbacks.

- `discord-bot-token` -> `DISCORD_BOT_TOKEN`
- `applicationinsights-connection-string` -> `APPLICATIONINSIGHTS_CONNECTION_STRING`
- `ReceiptParser-DocumentIntelligenceApiKey` -> `ReceiptParser__DocumentIntelligenceApiKey`

This model assumes that the application does not call the Key Vault SDK directly. Instead, Container Apps injects secrets as environment variables.

## Identity and Access

The services currently support both connection strings and IAM/RBAC for some Azure resources.

### `discord-api`

- Blob Storage:
  - `AZURE_BLOB_CONNECTION_STRING` first
  - otherwise `AZURE_BLOB_ACCOUNT_URL + DefaultAzureCredential`

- Settlement history Cosmos:
  - `SettlementHistory__CosmosConnectionString` first
  - otherwise `SettlementHistory__CosmosAccountEndpoint + DefaultAzureCredential`

### `receipt-parser`

- Document Intelligence:
  - use key auth if `ReceiptParser__DocumentIntelligenceApiKey` exists
  - otherwise use `DefaultAzureCredential`

- Cosmos draft store:
  - `ReceiptParser__CosmosConnectionString` first
  - otherwise `ReceiptParser__CosmosAccountEndpoint + DefaultAzureCredential`

In other words, after CI/CD, the production deployment can operate either with connection strings or with Managed Identity.

If both are configured, the current code prefers connection strings.

## Validation Expectations

The workflows currently include little or only minimal automated testing.

The realistic current validation bar is:

- `dotnet restore`
- `dotnet build`
- `dotnet publish`
- Docker image build

Additional items frequently verified by hand:

- `/settle-up` upload flow
- `receipt-parser` callback
- public receipt UI refresh
- `/history`
- `/custom`
- `/language`

In other words, current CI focuses on compile/package guarantees, while interaction UX validation still relies heavily on manual testing.

## Change Management Rules

The following changes must always include a CI/CD review.

- Dockerfile changes
- shared project reference changes
- build context changes
- environment variable name changes
- authentication strategy changes
- Container App secret or Key Vault naming changes

These are easy places for drift if code is changed without reviewing workflows.

## Current Gaps

- GitHub Actions does not yet include enough automated test stages
- the Azure deployment apply step is not directly included in workflows
- environment promotion strategy across dev/staging/prod is under-documented
- there is not yet a separate document for secret rotation operations

## Recommended Next Improvements

1. add smoke tests or minimal integration tests per service
2. decide whether the Container App deploy step should be explicit in workflows
3. document environment-specific secret/config matrices
4. consider adding release tags or a semver-based image tagging policy
5. include callback validation and health checks in the operational checklist

## Related Documents

- `docs/api.md`
- `docs/architecture.md`
- `docs/decisions/001-monorepo.md`
- `docs/decisions/007-use-http-for-communication-between-parser-discordapi.md`
- `docs/decisions/012-serialize-receipt-session-updates-and-debounce-public-message-publishing.md`
- `docs/decisions/013-use-session-scoped-in-memory-cache-for-discord-receipt-ui.md`

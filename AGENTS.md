# AGENTS.md

## Project Overview
This repository contains the Settle Up project, a cloud-based expense and receipt settlement system.
The system is being built as a multi-service architecture, and one of the first services is the Discord API service.

The long-term goal is:
- users upload receipt images through Discord
- receipt images are stored in cloud storage
- OCR / receipt parsing extracts item information
- users confirm who bought which items
- the system calculates settlement amounts

## Current Focus
At the moment, prioritize:
1. setting up the Discord API service
2. making the service build and run correctly in Docker
3. preparing CI/CD with GitHub Actions
4. keeping the project structure clean for future services

Current implementation notes:
- `discord-api` and `receipt-parser` now share a common observability/bootstrap project under `shared/SettleUp.Observability`.
- If shared code, project references, or Docker build contexts change, related workflow files in `.github/workflows/` must be reviewed together with service Dockerfiles.
- Changes already captured under `docs/decisions/` should be treated as part of the current project state and included in commits when they reflect accepted direction.

## Architecture Direction
This is a mono-repo that may contain multiple services, such as:
- discord-api
- receipt-parser
- settlement-service
- user-service
- export-service

Each service should:
- have its own source directory
- have its own Dockerfile
- have its own environment-variable configuration
- be independently buildable and deployable

## Coding Guidelines
- Prefer clear and simple code over overly clever abstractions.
- Use descriptive names.
- Avoid premature optimization.
- Keep service boundaries explicit.
- Use async/await properly for network and I/O operations.
- Do not hardcode secrets or tokens.
- Configuration should come from environment variables.

## Security Guidelines
- Never commit secrets.
- Never commit real tokens, connection strings, or private keys.
- Use Azure Key Vault or environment variables for sensitive configuration.
- Treat all external input as untrusted.
- Validate Discord payloads and webhook/event inputs where applicable.

## Docker Guidelines
- Each service should have its own Dockerfile.
- Images should be small and production-friendly.
- Use multi-stage builds when appropriate.
- Prefer explicit working directories and copy steps.

## CI/CD Guidelines
- CI/CD should be managed per service when practical.
- Each service may have its own workflow file if build/test/deploy steps differ.
- Shared workflows can be introduced later if duplication becomes large.
- Main CI goals:
  - restore dependencies
  - build
  - test
  - build Docker image
  - optionally push to Azure Container Registry

## Repository Conventions
- Root-level documentation explains the whole system.
- Service-level documentation explains service-specific behavior.
- Put general design documents under `/docs`.
- Put service-specific docs inside each service folder.

## When Editing
If making changes:
- preserve the multi-service direction
- avoid breaking future service separation
- keep documentation in sync with structure
- prefer minimal but correct implementations first
- if build, Docker, or shared-project behavior changes, verify the matching workflow file as part of the same change

## Current Project Notes
- `discord-api`는 현재 업로드 pending 메시지, parser draft 수신, 체크 섹션 embed, item selection/add/remove/edit, confirm embed까지 실제 서버 기준으로 다시 동작 검증 중이다.
- `discord-api` receipt UI는 이제 기본적으로 기존 공개 메인 메시지를 수정하는 방향으로 정리됐다. select/add/remove/edit는 private 패널을 통해 상태를 바꾸고, 공개 메시지는 세션별 직렬화 + 1초 디바운스로 갱신한다.
- `discord-api`에는 현재 receipt session 단위 in-memory 락(`ReceiptSessionLockManager`), 공개 메인 메시지 1초 디바운스(`ReceiptMainMessageDebounceService`), 메인 메시지 객체 캐시, 렌더링 계산 캐시가 들어가 있다.
- `discord-api`의 private selection panel은 사용자+모드 기준으로 하나만 유지되며, confirm 시 열린 private panel 정리를 시도한다.
- `discord-api` 기준 권한 모델은 현재 `Select item`은 참여자 누구나 가능하고, `Add item` / `Remove item` / `Edit item` / `Confirm`은 업로더(owner)만 가능하다.
- `receipt-parser` -> `discord-api` HTTP callback 경로는 계속 HTTP 기반이고, `/test`는 parser callback 이후 UI를 재현하는 shortcut 경로다. 핵심 세션 생성/갱신 로직은 둘 다 `ReceiptDraftSessionService`를 공유한다.
- `receipt-parser`는 실제 Azure Blob URL 패턴 기준으로 `uploadedByUserId`를 다시 추출하도록 수정됐다. 따라서 blob URL 패턴을 다시 바꾸면 parser의 추출 규칙과 `discord-api` 계약을 함께 확인해야 한다.
- `docs/decisions`는 현재 `README.md`에 정의한 공통 ADR 포맷과 번호 체계를 따른다. 최근 관련 결정은 `012`(세션별 직렬화 + 공개 메시지 디바운스)와 `013`(session-scoped in-memory cache)다.

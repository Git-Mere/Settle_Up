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
- `discord-api`는 현재 업로드 pending 메시지, parser draft 수신, 체크 섹션 embed, item selection/add/remove/edit, confirm embed까지 로컬 기준 동작한다.
- `discord-api`의 receipt UI는 현재 "기존 공개 메시지 안정적 수정" 대신 "새 공개 메시지 발행" 전략을 기본으로 사용한다. 이건 현재 서버/권한 환경에서 Discord REST channel lookup이 `50001 Missing Access`로 자주 실패하기 때문이다.
- 따라서 다음 세션에서 `discord-api` 작업을 이어갈 때 가장 먼저 볼 포인트는 공개 체크 메시지 정리 전략과 Discord 채널 접근 제약이다.
- `receipt-parser` -> `discord-api` HTTP callback 경로는 이미 연결돼 있고, 로컬 테스트에서는 `/test`가 parser callback 이후 UI를 재현하는 기준 경로다.

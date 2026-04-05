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
- `discord-api`는 현재 업로드 pending 메시지, parser draft 수신, 체크 섹션 embed, item selection/add/remove/edit, confirm embed, history 조회까지 로컬과 Azure 둘 다에서 동작 확인이 끝난 상태다.
- `discord-api` receipt UI는 이제 기본적으로 기존 공개 메인 메시지를 수정하는 방향으로 정리됐다. select/add/remove/edit는 private 패널을 통해 상태를 바꾸고, 공개 메시지는 세션별 직렬화 + 1초 디바운스로 갱신한다.
- `discord-api`에는 현재 receipt session 단위 in-memory 락(`ReceiptSessionLockManager`), 공개 메인 메시지 1초 디바운스(`ReceiptMainMessageDebounceService`), 메인 메시지 객체 캐시, 렌더링 결과 캐시가 들어가 있다.
- `discord-api`의 private selection panel은 사용자+모드 기준으로 하나만 유지되며, confirm 시 열린 private panel 정리를 시도한다.
- `discord-api` 기준 권한 모델은 현재 `Select item`은 참여자 누구나 가능하고, `Add item` / `Remove item` / `Edit item` / `Confirm`은 업로더(owner)만 가능하다.
- `discord-api`는 현재 tax/tip 정책과 history 기능까지 포함한다. confirm은 먼저 Discord UI를 갱신하고, settlement history는 background에서 Cosmos에 저장하며 실패 시 retry 후 ephemeral 오류를 남긴다.
- `discord-api`의 `/history`는 현재 `/history` 또는 `/history index:<번호>` 구조이고, `index:1`은 현재 시점 기준 가장 최근 history를 뜻한다.
- `discord-api`의 debug slash command(`/pingtest`, `/test`)는 이제 Development 환경에서만 등록된다. Azure Production에서는 보이지 않는 것이 정상이다.
- `receipt-parser` -> `discord-api` HTTP callback 경로는 계속 HTTP 기반이고, `/test`는 parser callback 이후 UI를 재현하는 shortcut 경로다. 핵심 세션 생성/갱신 로직은 둘 다 `ReceiptDraftSessionService`를 공유한다.
- `receipt-parser`는 실제 Azure Blob URL 패턴 기준으로 `uploadedByUserId`를 다시 추출하도록 수정됐다. 따라서 blob URL 패턴을 다시 바꾸면 parser의 추출 규칙과 `discord-api` 계약을 함께 확인해야 한다.
- `receipt-parser`도 현재 로컬과 Azure 둘 다에서 Event Grid -> Document Intelligence -> Cosmos -> discord-api callback 흐름이 다시 동작 확인된 상태다.
- `discord-api`에는 이제 `/language`가 들어갔고, 한국어/영어 선택을 지원한다. 공개 receipt 메인 메시지는 owner 언어를 따르고, private/ephemeral/history는 호출 사용자 언어를 따른다.
- 사용자 언어 설정은 메모리 기반이고 기본 언어는 English다. slash command 메타데이터와 로그/exception은 영어로 유지한다.
- `/language`는 이제 기존에 열려 있는 공개 receipt 메시지를 refresh하지 않는다. 언어 변경 이후에 새로 생성되거나 새로 응답되는 UI부터 선택 언어를 적용한다.
- `discord-api`는 item-level discount를 지원한다. 할인 line은 우선 직전 일반 item에 귀속되고, 귀속 실패 할인은 자동 적용하지 않는다.
- `discord-api`에는 `/custom`이 추가돼 parser 없이 빈 공개 check 메시지로 수동 정산을 시작할 수 있다.
- `discord-api`는 현재 `Currency == KRW`인 영수증의 일반 `Tax`를 포함세로 보고 계산과 UI에서 제외한다. 한국 영수증에서 tax 이중 과금을 막기 위한 정책이다.
- 최근 성능 점검 결과 문서는 `docs/problem-searching/performance-review-2026-04-04.md`에 정리돼 있다. cold path 지연과 in-memory lifecycle 이슈를 볼 때 이 문서부터 확인한다.
- `discord-api`는 startup 시 Blob uploader warm-up을 수행하고, pending -> draft 전환 시 업로더 표시 이름을 가능한 한 기존 세션 캐시에서 재사용한다.
- `receipt-parser`는 startup 시 Document Intelligence 자격 증명과 Cosmos container warm-up을 수행해 첫 영수증 cold path를 줄이도록 정리됐다.
- `discord-api`는 confirm 이후 in-memory receipt session과 session lock을 cleanup한다. confirmed 공개 메시지는 남지만, 세션 객체와 lock은 메모리에 계속 남지 않는다.
- 다음 세션에서도 두 서비스 리팩터링을 계속 진행할 가능성이 높다. 특히 `receipt-parser` callback 검증 강화와 discount 귀속 정확도 확인이 유력하다.
- `docs/decisions`는 현재 `README.md`에 정의한 공통 ADR 포맷과 번호 체계를 따른다. 최근 관련 결정은 `012`(세션별 직렬화 + 공개 메시지 디바운스)와 `013`(session-scoped in-memory cache)다.

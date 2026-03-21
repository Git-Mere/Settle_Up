# codex.md

## Project Name
Settle Up

## Summary
Settle Up is a cloud-based receipt parsing and settlement system integrated with Discord.

A user uploads a receipt image through a Discord bot.
The system stores the image, parses the receipt, asks users to confirm item ownership, and calculates how much each person owes.

## Planned High-Level Flow
1. A user uploads a receipt image through Discord.
2. The Discord API service receives the command or attachment.
3. The receipt image is stored in Blob Storage.
4. A cloud event triggers receipt parsing.
5. OCR / receipt intelligence extracts merchant, total, tax, and line items.
6. The parsed result is stored in a database.
7. The Discord bot sends the parsed items back to users.
8. Users confirm which items belong to whom.
9. The settlement service calculates balances.
10. Results are returned through Discord.

## Current Implementation Stage
The project is currently in the early multi-service foundation stage.

Current state:
- `discord-api` runs as a Discord bot worker service with shared observability/bootstrap support.
- `receipt-parser` runs as an HTTP service triggered by Blob/Event Grid, stores draft receipts in Cosmos DB, and sends parsed drafts to `discord-api` over HTTP.
- both current services build locally and in Docker, and their CI workflows must stay aligned with shared project references.

## Repository Shape
This repository is intended to be a mono-repo.

Example:
- `/services/discord-api`
- `/services/receipt-parser`
- `/services/settlement-service`
- `/docs`
- `/infra`

## Technical Direction
- Backend services: C# / .NET 8
- Containerization: Docker
- CI/CD: GitHub Actions
- Cloud: Azure
- Secret management: environment variables first, Key Vault later
- Storage: Blob Storage for receipt images
- Database: likely Cosmos DB or another cloud database depending on final design
- Messaging / eventing: Event Grid or similar event-driven components

## Important Constraints
- Do not assume a single-service architecture.
- Design with future service separation in mind.
- Keep secrets out of source control.
- Prefer simple and production-friendly folder structures.
- Documentation should stay practical and implementation-oriented.

## Immediate Priorities
1. keep Docker and GitHub Actions build contexts aligned with shared projects
2. harden `receipt-parser` -> `discord-api` HTTP delivery with validation/reprocessing as needed
3. keep the receipt UI stable under real Discord interaction load
4. keep observability/logging pattern consistent for future services
5. future Azure deployment readiness

## Notes for Service Work
When working inside a service directory, follow the local documentation there first.
Root docs describe the overall system.
Service docs describe local implementation details.

Accepted cross-service current state:
- per `docs/decisions/007-use-http-for-communication-between-parser-discordapi`, `discord-api` now exposes an HTTP endpoint to receive parsed receipt drafts
- `receipt-parser` now sends parsed results to `discord-api` over HTTP instead of downstream Event Grid

## Next Session Notes
- `discord-api` receipt UI는 현재 공개 메인 메시지 수정 기반으로 정리됐다. routine interaction(select/add/remove/edit)은 1초 디바운스 후 공개 메시지 갱신, confirm은 즉시 갱신이다.
- 이번 세션에서 세션별 직렬화, 공개 메인 메시지 캐시, render context 캐시, Discord API retry가 들어갔다. 다음 세션에서 UI 이슈를 볼 때는 성능/동시성 관련 코드를 먼저 확인하면 된다.
- `/test`는 parser callback 이후 UI를 재현하는 shortcut이고, 실제 parser callback은 `/getting_draft`를 통해 같은 `ReceiptDraftSessionService` 경로를 탄다. 단 `/getting_draft`는 payload validation을 더 많이 수행한다.
- `receipt-parser`는 실제 Azure Blob URL 패턴(`receipts/{yyyy}/{MM}/{dd}/{userId}/{file}`) 기준으로 `uploadedByUserId`를 추출하도록 고쳐졌다.
- `discord-api` 권한 모델은 현재 `Select item`만 참여자 전체에게 열려 있고, `Add item` / `Remove item` / `Edit item` / `Confirm`은 업로더만 가능하다.
- 다음 세션에서 문서성 결정이 필요하면 `docs/decisions/README.md` 포맷을 따르고, 관련 기존 결정으로 `012`, `013`을 먼저 확인한다.
- 다음 세션에서 우선 확인할 파일:
  - `services/discord-api/src/Services/ReceiptInteractionService.cs`
  - `services/discord-api/src/Services/ReceiptMainMessageService.cs`
  - `services/discord-api/src/Services/ReceiptMainMessageDebounceService.cs`
  - `services/discord-api/src/Services/ReceiptSessionLockManager.cs`
  - `services/receipt-parser/Services/ReceiptProcessingService.cs`

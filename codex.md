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
2. wire the received draft payload in `discord-api` into the next Discord follow-up flow
3. harden `receipt-parser` -> `discord-api` HTTP delivery with validation/reprocessing as needed
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
- `discord-api` receipt UI는 현재 `/test` 기준으로 체크 섹션 embed, 아이템 선택, add/remove/edit, confirm 메시지 흐름이 대부분 동작한다.
- 최근 수정으로 add item으로 생성된 manual item도 edit 가능하도록, 긴 `itemId`를 modal custom id에 직접 넣지 않고 세션 내 짧은 edit token 매핑으로 처리한다.
- 아직 남아 있는 대표 이슈는 "이전 Settlement Check 공개 메시지 정리"다. 현재 Discord channel re-lookup이 `50001 Missing Access`로 막히는 환경이 있어서, 메시지 수정/삭제 전략을 바꿀 때 이 제약을 전제로 봐야 한다.
- 다음 세션에서 receipt UI를 만질 경우 우선 확인할 파일은 `services/discord-api/src/Services/ReceiptInteractionService.cs`, `services/discord-api/src/Services/ReceiptMainMessageService.cs`, `services/discord-api/src/Models/ReceiptSessionState.cs`다.

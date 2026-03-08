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
The project is currently at the early foundation stage.
The immediate focus is the Discord API service:
- create the service
- make it build and run correctly
- containerize it
- prepare CI/CD
- keep the repository ready for additional services

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
1. Discord API service scaffolding
2. Dockerfile for Discord API service
3. local environment variable setup
4. GitHub Actions CI workflow
5. future Azure deployment readiness

## Notes for Service Work
When working inside a service directory, follow the local documentation there first.
Root docs describe the overall system.
Service docs describe local implementation details.
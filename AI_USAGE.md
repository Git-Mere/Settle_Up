# AI Usage Statement

This project used AI assistance as a development support tool. The final implementation, testing decisions, deployment checks, and submitted repository contents were reviewed and controlled by the project author.

## How AI Was Used

AI was used for the following tasks:

- Planning and organizing the multi-service repository structure.
- Reviewing architectural tradeoffs between the Discord API service, receipt parser service, Azure Blob Storage, Event Grid, Cosmos DB, and HTTP callbacks.
- Drafting and refining documentation, including architecture notes, API notes, decision records, and performance review summaries.
- Helping identify code paths during debugging, especially around receipt upload, Blob event handling, parser callbacks, Discord UI state, history persistence, and localization behavior.
- Suggesting focused refactoring options to keep service boundaries clear and reduce duplicated logic.
- Checking for submission readiness, including identifying generated build folders, ignored files, and large binary files.

## How AI Was Not Used

AI was not used as an autonomous replacement for project ownership. In particular:

- Secrets, credentials, tokens, and private connection strings were not intentionally provided to AI.
- AI-generated suggestions were reviewed before being accepted.
- Runtime behavior was validated through local and Azure testing rather than assumed from AI output.
- Final code changes were committed only after checking the affected files and repository state.

## Human Review and Verification

The project author made the final decisions about:

- Service architecture and repository organization.
- Azure resource usage and deployment configuration.
- Discord interaction behavior and permission model.
- Receipt parsing flow and callback contract.
- Which files should remain in the submitted repository.

AI assistance was used to speed up implementation, debugging, documentation, and review, but the submitted work reflects human-directed design choices and verification.

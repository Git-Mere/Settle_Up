# 001 - Monorepo

## Status
Accepted

## Context
I considered two repository organization options for Settle Up's multi-service architecture:
- Fully independent service repositories (service-level organization)
- A single monorepo containing all services

## Option A: Fully Independent Service Repositories
### Advantages
- Services can be completely independent from each other.
- CI/CD can be split per service with strong isolation.
- Team collaboration can be efficient when teams are clearly separated by service ownership.

### Disadvantages
- Overall management becomes more complex across multiple repositories.
- Sharing and maintaining common code/contracts is harder.

## Option B: Monorepo
### Advantages
- Easier overall repository management.
- Simpler CI setup and maintenance, especially in early stages.
- Better fit for cloud-oriented development where services evolve together and need shared infrastructure patterns.

## Decision
We chose a monorepo.

## Rationale
At the current stage of the project, simplicity of management, straightforward CI operations, and cloud-friendly coordination across services provide more practical value than strict repository-level isolation.

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
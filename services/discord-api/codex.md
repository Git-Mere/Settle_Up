# codex.md

## Service Name
discord-api

## Purpose
This service is the Discord-facing entry point for the Settle Up system.

Its job is to:
- run the Discord bot
- receive commands and uploaded content
- respond to users
- later coordinate with storage, parsing, and settlement services

## Current Stage
This service is in the setup phase.

Immediate tasks:
- scaffold the .NET project
- install required Discord library dependencies
- read bot token from environment variables
- start the bot successfully
- containerize the service
- prepare GitHub Actions workflow

## Likely Responsibilities
Short term:
- bot startup
- command handling
- logging
- basic health and configuration checks

Long term:
- receipt upload handling
- user interaction flow for item ownership
- calling external services
- returning settlement results

## Suggested Internal Structure
Example direction:
```text
discord-api/
├─ src/
│  ├─ Program.cs
│  ├─ Configuration/
│  ├─ Bot/
│  ├─ Commands/
│  └─ Services/
├─ Dockerfile
└─ README.md
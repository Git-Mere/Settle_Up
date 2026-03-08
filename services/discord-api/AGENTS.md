# AGENTS.md

## Service Overview
This service is responsible for Discord bot interaction for the Settle Up project.

It should:
- connect to Discord using a bot token
- listen for commands or message-based interactions
- receive receipt uploads or related user input
- trigger or forward work to other services later

## Current Scope
Right now, focus on:
- making the bot start correctly
- loading configuration from environment variables
- making the service run locally
- making the service run in Docker
- preparing the service for CI/CD

## Expected Configuration
Use environment variables for all sensitive or environment-specific values.

Examples:
- `DISCORD_BOT_TOKEN`
- `DISCORD_GUILD_ID` if needed for development
- `ASPNETCORE_ENVIRONMENT` if applicable

Do not hardcode tokens.

## Coding Guidelines
- Keep the entry point simple.
- Separate bot startup, command handling, and infrastructure concerns.
- Prefer small classes with clear responsibilities.
- Use async/await correctly.
- Log useful startup and error information.

## Discord-Specific Guidelines
- Treat user input as untrusted.
- Avoid assumptions about message format.
- Make command handling explicit and easy to extend.
- Keep logic modular so future slash commands or message commands can be added cleanly.

## Integration Direction
In the future, this service may:
- upload receipt images to storage
- notify parser services
- query settlement results
- send confirmation messages back to Discord users

For now, keep the implementation minimal but extensible.

## Docker Guidelines
- The container should start the bot reliably.
- Make sure the correct `.dll` is executed.
- Verify build/publish output paths carefully.
- Prefer multi-stage builds for production images.

## Documentation Rule
If the service structure changes significantly, update:
- `services/discord-api/codex.md`
- `services/discord-api/README.md`
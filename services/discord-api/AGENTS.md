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
- `APPLICATIONINSIGHTS_CONNECTION_STRING` for Azure Monitor trace export

Do not hardcode tokens.

## Coding Guidelines
- Keep the entry point simple.
- Separate bot startup, command handling, and infrastructure concerns.
- Prefer small classes with clear responsibilities.
- Use async/await correctly.
- Log useful startup and error information.
- Prefer `ILogger` for human-readable application logs.
- Keep OpenTelemetry tracing for correlation/dependency tracing rather than raw console dumps.

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

## Observability Guidelines
- Console output should be driven by `ILogger` and stay human-readable.
- Discord gateway state, command start/completion/failure, and blob upload results should be logged as structured application logs.
- OpenTelemetry traces should be exported to Azure Monitor / Application Insights when `APPLICATIONINSIGHTS_CONNECTION_STRING` is configured.
- If the connection string is missing, the service must continue to run with console logging only.

## Documentation Rule
If the service structure changes significantly, update:
- `services/discord-api/codex.md`
- `services/discord-api/README.md`

# 006 - Standardize Observability and Logging

## Status
Accepted

## Context

Settle Up currently contains multiple services, including:

- `discord-api`
- `receipt-parser`

As observability was added, console output became noisy because several different signal types appeared together:

- Discord.Net internal logs
- OpenTelemetry `HttpClient` instrumentation output
- custom application `Activity` traces
- general application logs

This made local debugging harder because low-level tracing and raw activity dumps were mixed with high-value operational events such as:

- service startup
- Discord ready
- slash command execution
- blob event processing
- Cosmos DB writes
- failures and warnings

The project also needs an observability pattern that scales cleanly as new services are added and supports Azure Monitor / Application Insights integration through `APPLICATIONINSIGHTS_CONNECTION_STRING`.

## Options Considered

### Option A - Keep Mixed Console-Centric Logging

Advantages:

- simplest setup
- minimal conceptual overhead
- easy to start with during initial development

Disadvantages:

- console output becomes difficult to read
- application logs and tracing signals are not clearly separated
- low-value noise can hide important operational information
- scaling the same approach across multiple services becomes messy

### Option B - Separate Human-Readable Logs from Tracing

Advantages:

- application logs remain readable in console
- tracing can be exported to dedicated observability tooling
- easier to scale a consistent pattern across services
- better support for dependency tracing and cross-service correlation

Disadvantages:

- setup is more complex
- developers must understand the distinction between logging and tracing
- shared observability bootstrap adds some project structure overhead

## Decision

We will separate application logging from observability tracing.

The standard is:

- `ILogger` for human-readable application logs
- OpenTelemetry for tracing and dependency observability
- Azure Monitor / Application Insights as the primary destination for exported traces when configured

Console output should prioritize readable application logs rather than raw trace dumps.

## Consequences

### Positive

- console output becomes easier to read
- application logs and tracing have clearer responsibilities
- Azure Monitor can receive richer telemetry without cluttering console output
- the project gains a scalable observability pattern for future services

### Negative

- setup is more complex than console logging alone
- developers need to learn and maintain the distinction between `ILogger` and OpenTelemetry
- shared observability code introduces some structural overhead

## Follow-up Notes

This pattern should be applied consistently across current and future services.

Implementation expectations include:

- using `ILogger` for meaningful application events
- minimizing noisy raw console trace output
- exporting traces to Azure Monitor / Application Insights when `APPLICATIONINSIGHTS_CONNECTION_STRING` is present
- keeping services operational even when the exporter is not configured

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
- preparing the service to receive parser results over HTTP

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

Accepted next step:
- this service should evolve from worker-only to worker + HTTP receiver
- `receipt-parser` will send parsed receipt results to `discord-api` over HTTP instead of downstream Event Grid

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
- related workflow/Docker settings if shared project references or build contexts change

## Current Service Notes
- receipt selection UI는 현재 public embed + private panel 조합으로 동작하며 `/test`가 parser callback 이후 상태를 재현하는 빠른 테스트 경로다. 로컬과 Azure 둘 다에서 현재 핵심 플로우 동작 확인이 끝난 상태다.
- add/remove/edit/confirm 로직은 `ReceiptInteractionService`가 처리하고, 공개 메인 메시지 수정/발행은 `ReceiptMainMessageService`, draft session 생성/갱신은 `ReceiptDraftSessionService`가 담당한다.
- routine interaction(select/add/remove/edit)은 공개 메인 메시지를 즉시 갱신하지 않고 1초 디바운스 후 갱신한다. confirm은 즉시 confirmed 메시지로 갱신한다.
- receipt session mutation은 현재 `ReceiptSessionLockManager`로 직렬화된다. 다음 세션에서 동시성/성능 이슈를 볼 때는 이 락 경로를 먼저 확인한다.
- 공개 메인 메시지는 세션 내 `MainMessage` 캐시를 사용하고, embed 렌더링은 세션 단위 rendered message cache를 사용한다.
- private selection panel은 사용자+모드 기준으로 하나만 유지하며, confirm 시 열린 panel cleanup을 시도한다.
- 권한 모델은 현재 `Select item`만 참여자 누구나 가능하고, `Add item` / `Remove item` / `Edit item` / `Confirm`은 업로더만 가능하다.
- Discord 공개 메시지 버튼은 사용자별로 disabled 상태를 다르게 줄 수 없다. owner 전용 버튼도 non-owner 클릭 자체는 가능하고, 서버에서 권한 체크 후 ephemeral로 막는다.
- Discord API 일시 오류(`429/502/503/504`)는 `ReceiptMainMessageService`의 retry 경로로 흡수한다. UI 지연/실패를 볼 때는 권한 이슈와 transient error를 구분해서 확인한다.
- settlement history는 `discord-api`가 owner 기준으로 Cosmos에 저장하고, 현재 `/history` 또는 `/history index:<번호>`로 조회한다.
- confirm은 먼저 Discord UI를 업데이트하고 history 저장은 background로 수행한다. 저장 실패 시 retry 후 ephemeral 오류 메시지를 남긴다.
- debug command인 `/pingtest`, `/test`는 이제 Development 환경에서만 등록된다. Azure Production에서 안 보이는 것이 정상이다.
- `/language`가 추가됐고, 지원 언어는 English / Korean 두 가지다. 공개 receipt 메인 메시지는 owner 언어를 따르고, private/ephemeral/history는 호출 사용자 언어를 따른다.
- 사용자 언어 설정은 메모리 기반이라 봇 재시작 시 초기화된다. slash command 설명과 옵션 설명은 쉬운 영어로 유지한다.
- item-level discount가 들어갔고, 할인은 우선 직전 일반 item에 귀속한다. 귀속 실패 할인은 자동 적용하지 않고 필요 시 owner가 `Edit item`으로 수동 수정한다.
- `/custom`이 추가돼 parser 없이 빈 receipt check 메시지를 바로 시작할 수 있다. `payment_contact`는 optional slash option이고, item이 1개 이상이며 모두 배정됐을 때만 confirm 가능하다.
- `Currency == KRW`인 draft는 일반 `Tax`를 포함세로 보고 `0`으로 정규화한다. 그래서 한국 영수증은 일반 tax header/section이 보이지 않고 정산에도 한 번 더 붙지 않는다.

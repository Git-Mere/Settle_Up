# codex.md

## Service Name
- `discord-api`

## Session Summary (updated)
이번 세션에서 `discord-api`는 "worker + HTTP receiver 통합 호스팅 + 공통 observability 적용 + structured logging 보강"을 진행했다.

1. 공통 observability bootstrap 적용
- `shared/SettleUp.Observability`를 참조하도록 변경.
- console logging은 `ILogger` 중심 단일 라인 출력으로 정리.
- OpenTelemetry raw console exporter는 제거하고 Azure Monitor exporter를 환경 변수 기반으로 활성화한다.

2. Host / DI 구조 정리
- `Program.cs`는 .NET 8 `WebApplicationBuilder` 기반으로 정리했다.
- `DiscordBotWorker`가 Discord 클라이언트 시작/중지와 이벤트 라우팅을 담당.
- `BlobUploaderProvider`로 Blob 업로더 초기화 상태를 캡슐화.
- 같은 프로세스에서 Kestrel HTTP 서버도 함께 실행한다.

3. HTTP receiver 추가
- `POST /getting_draft` endpoint를 추가했다.
- JSON payload를 받아 기본 로그를 남기고 `200 OK` + `{ "message": "draft received" }`를 반환한다.
- 기본 리슨 주소는 `http://0.0.0.0:5000`이며 `ASPNETCORE_URLS`로 오버라이드 가능하다.

4. Blob 업로드 기능 추가
- `/settle-up` 플로우에서 업로드 파일을 Azure Blob으로 저장.
- `BlobImageUploader`로 로직 분리.
- 허용 파일: `jpg`, `jpeg`, `png`.

5. 의미 있는 application log 보강
- 봇 시작/정지, Discord ready, 명령 시작/완료/실패, Blob 업로드 시작/완료/실패를 `ILogger` structured log로 기록.
- Discord 내부 로그도 `Console.WriteLine` 대신 `ILogger`로 매핑.
- `/getting_draft` 호출도 `ILogger`로 기록한다.

6. `/settle-up` 상호작용 플로우 변경
- 기존: slash 후 채널 메시지 업로드 대기
- 현재: slash -> 버튼 표시 -> 버튼 클릭 -> 모달(파일 업로드 컴포넌트) -> Blob 업로드

## Current File Layout (relevant)
```text
services/discord-api/
├─ src/
│  ├─ Program.cs
│  ├─ DiscordBotWorker.cs
│  ├─ BlobUploaderProvider.cs
│  ├─ DiscordApi.csproj
│  ├─ Commands/
│  │  ├─ PingTestCommandHandler.cs
│  │  └─ SettleUpCommandHandler.cs
│  ├─ Storage/
│  │  └─ BlobImageUploader.cs
│  └─ Observability/
│     └─ Telemetry.cs
├─ Dockerfile
├─ README.md
└─ codex.md
```

## Runtime Flow (current)
### HTTP receiver
- Kestrel이 기본적으로 `0.0.0.0:5000`에서 리슨한다.
- `POST /getting_draft`로 JSON payload를 받으면 로그를 남기고 성공 응답을 반환한다.

### `/pingtest`
- 즉시 ephemeral 응답: `pong! slash command 정상 작동 중입니다.`

### `/settle-up`
1. slash 실행
2. ephemeral 메시지에 `영수증 업로드` 버튼 표시
3. 버튼 클릭 시 모달 열기
4. 모달 내 file upload component로 파일 제출
5. 제출 attachment를 Blob에 업로드
6. 성공 시 ephemeral로 `container/blob/url` 반환

## Environment Variables (currently used)
필수/준필수:
- `DISCORD_BOT_TOKEN`
- `AZURE_BLOB_CONTAINER_NAME`
- `AZURE_BLOB_CONNECTION_STRING` **or** `AZURE_BLOB_ACCOUNT_URL`

선택:
- `OTEL_SERVICE_NAME` (기본값: `discord-api`)
- `APPLICATIONINSIGHTS_CONNECTION_STRING`
- `ASPNETCORE_URLS` (기본값: `http://0.0.0.0:5000`)

추가 참고:
- `Program.cs`에서 `DotNetEnv`로 `../.env`를 로드함 (`Env.Load("../.env")`).
- 실행 위치/배포 환경에 따라 `.env` 경로가 기대와 다를 수 있으니 주의 필요.

## Blob Upload Notes
- 구현 파일: `src/Storage/BlobImageUploader.cs`
- 업로드 경로 패턴:
  - `receipts/{yyyy}/{MM}/{dd}/{userId}/{guid}.{ext}`
- MIME은 확장자 기준으로 `image/jpeg` 또는 `image/png` 설정.

## Observability Notes
- console은 `ILogger` 중심의 사람이 읽기 쉬운 structured log만 출력한다.
- OpenTelemetry trace는 `shared/SettleUp.Observability` bootstrap으로 구성한다.
- `APPLICATIONINSIGHTS_CONNECTION_STRING`이 있으면 Azure Monitor / Application Insights로 trace를 export한다.
- connection string이 없으면 exporter 없이 계속 실행한다.
- `System.Net.Http` raw activity dump는 더 이상 콘솔에 직접 출력하지 않는다.

## Current Constraints / Next Step
- `docs/decisions/007-use-http-for-communication-between-parser-discordapi`에 따라 기본 callback endpoint 골격은 추가됐다.
- 다음 작업은 `/getting_draft` request/response DTO 계약을 `receipt-parser`와 맞춰 구체화하는 것이다.
- shared observability project를 참조하므로 Dockerfile과 workflow는 repository-root build context를 기준으로 유지해야 한다.

## Known Decisions / Open Items
1. Blob 자동 삭제 30일
- 사용자 요청이 있었지만 해당 작업은 "취소" 요청으로 중단됨.
- 아직 코드/정책 반영 안 됨.
- 권장 방식은 Azure Storage Lifecycle Management 정책으로 처리.

2. 모달 파일 업로드 컴포넌트 호환성
- 현재 `Discord.Net 3.19.0` 기준으로 빌드는 통과.
- 실제 디스코드 클라이언트 동작은 환경/권한 설정과 함께 실사용 검증 필요.

3. `.env.example` 추적
- `services/discord-api/.env.example` 파일은 존재하지만 현재 `.gitignore` 영향으로 git 추적되지 않음.

## Next Codex Session Quick Start
1. `/getting_draft` request/response DTO 계약 확정
2. Discord 후속 메시지 전송 흐름을 parser callback 입력 기준으로 연결
3. 인증/검증 규칙 추가
4. Dockerfile / workflow가 shared project build context를 계속 만족하는지 확인
5. 변경 후 검증:
- `dotnet build services/discord-api/src/DiscordApi.csproj -c Release`

## Last Verified State
- `dotnet build services/discord-api/src/DiscordApi.csproj -c Release` 성공
- Docker build succeeds only when repository-root build context is used so shared observability project is included
- next planned change: replace placeholder `/getting_draft` payload handling with parser callback contract + Discord follow-up flow

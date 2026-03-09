# codex.md

## Service Name
- `discord-api`

## Session Summary (up to commit `c60b605`)
이번 세션에서 `discord-api`는 초기 스캐폴드 상태에서 다음 수준까지 진행됨.

1. `src/DiscordApi/*` 중첩 구조를 평탄화함
- 현재는 `services/discord-api/src/` 바로 아래에 프로젝트 엔트리 파일이 위치.
- CI/Docker 경로도 함께 수정됨.

2. OpenTelemetry 기본 연결 추가
- `Program.cs`에서 tracing provider 설정.
- 주요 이벤트(`ready`, `slash`, `button`, `modal`, `message`)에 activity 태깅.
- 참고: metric export는 개발 중 노이즈 때문에 `Program.cs`에서 제거함. `Telemetry.cs`의 metric 정의는 남겨둔 상태.

3. Blob 업로드 기능 추가
- `/settle-up` 플로우에서 업로드 파일을 Azure Blob으로 저장.
- `BlobImageUploader`로 로직 분리.
- 허용 파일: `jpg`, `jpeg`, `png`.

4. 명령어 핸들러 분리
- `Program.cs`는 부트스트랩/이벤트 라우팅 중심으로 정리.
- `pingtest`, `settle-up` 명령어를 별도 클래스 파일로 분리.

5. `/settle-up` 상호작용 플로우 변경
- 기존: slash 후 채널 메시지 업로드 대기
- 현재: slash -> 버튼 표시 -> 버튼 클릭 -> 모달(파일 업로드 컴포넌트) -> Blob 업로드

## Current File Layout (relevant)
```text
services/discord-api/
├─ src/
│  ├─ Program.cs
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

추가 참고:
- `Program.cs`에서 `DotNetEnv`로 `../.env`를 로드함 (`Env.Load("../.env")`).
- 실행 위치/배포 환경에 따라 `.env` 경로가 기대와 다를 수 있으니 주의 필요.

## Blob Upload Notes
- 구현 파일: `src/Storage/BlobImageUploader.cs`
- 업로드 경로 패턴:
  - `receipts/{yyyy}/{MM}/{dd}/{userId}/{guid}.{ext}`
- MIME은 확장자 기준으로 `image/jpeg` 또는 `image/png` 설정.

## Observability Notes
- tracing은 활성화되어 있음 (`AddConsoleExporter`).
- metrics는 현재 `Program.cs`에서 provider를 만들지 않음.
  - 즉, `Telemetry.cs`에 metric instrument 정의는 있지만 실제 export/record 파이프라인은 비활성.

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
1. `services/discord-api/src/Program.cs`에서 이벤트 라우팅 구조 확인
2. `services/discord-api/src/Commands/SettleUpCommandHandler.cs`에서 업로드 흐름 수정
3. `services/discord-api/src/Storage/BlobImageUploader.cs`에서 Blob 정책/메타데이터 관련 확장
4. 변경 후 검증:
- `dotnet build services/discord-api/src/DiscordApi.csproj -c Release`

## Last Verified State
- latest commit in this session: `c60b605`
- `main` -> `origin/main` 푸시 완료

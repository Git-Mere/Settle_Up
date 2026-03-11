# discord-api

Discord API 서비스입니다.

현재 이 서비스는 하나의 프로세스에서 다음 두 역할을 함께 수행합니다.

- Discord 봇 워커 실행
- HTTP endpoint 수신 (`POST /getting_draft`)

## HTTP Endpoint

- `POST /getting_draft`
- JSON payload를 받아 로그를 남기고 `200 OK`를 반환합니다.

예시 응답:

```json
{
  "message": "draft received"
}
```

기본 리슨 주소:

- `http://0.0.0.0:5000`
- 필요하면 `ASPNETCORE_URLS` 환경 변수로 오버라이드할 수 있습니다.

## Environment Variables

아래 값들을 환경변수로 넣으면 `/settle-up` 이미지 업로드 시 Azure Blob Storage로 저장됩니다.

- `DISCORD_BOT_TOKEN` : 디스코드 봇 토큰
- `AZURE_BLOB_CONTAINER_NAME` : 업로드할 Blob 컨테이너 이름 (예: `receipts`)
- `AZURE_BLOB_CONNECTION_STRING` : Blob 연결 문자열 (권장)
- `AZURE_BLOB_ACCOUNT_URL` : 연결 문자열 대신 사용할 계정 URL (예: `https://<account>.blob.core.windows.net`)
- `ASPNETCORE_URLS` : HTTP 서버 리슨 주소 (기본값 `http://0.0.0.0:5000`)

주의:
- `AZURE_BLOB_CONNECTION_STRING` 또는 `AZURE_BLOB_ACCOUNT_URL` 중 하나는 반드시 필요합니다.
- `AZURE_BLOB_ACCOUNT_URL` 방식을 쓰면 실행 환경에서 `DefaultAzureCredential` 인증이 가능해야 합니다.

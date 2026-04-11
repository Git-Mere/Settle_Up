# API

Settle Up은 현재 두 개의 핵심 서비스 API를 중심으로 동작한다.

- `discord-api`
- `receipt-parser`

이 문서는 현재 실제로 구현된 HTTP endpoint, Discord interaction entrypoint, 그리고 서비스 간 계약을 정리한다.

## Scope

현재 이 문서에 포함하는 범위는 다음과 같다.

- `discord-api` HTTP callback endpoint
- `receipt-parser` Event Grid webhook endpoint
- `receipt-parser` 로컬 테스트 endpoint
- Discord slash command entrypoints
- `receipt-parser -> discord-api` draft payload 계약

장기적으로 추가될 수 있는 사용자 서비스, settlement-service, export-service API는 아직 이 문서 범위에 포함하지 않는다.

## `discord-api`

### HTTP Endpoint

#### `POST /getting_draft`

`receipt-parser`가 파싱 완료된 draft receipt를 전달하는 내부 callback endpoint다.

주요 역할:

- payload validation
- owner(`uploadedByUserId`) 확인
- pending session 생성 또는 기존 session 갱신
- 공개 receipt 메시지 생성 또는 refresh

성공 응답:

```json
{
  "message": "draft received"
}
```

현재 기본 리슨 주소:

- `http://0.0.0.0:5000`

환경 변수:

- `ASPNETCORE_URLS`

### Discord Slash Commands

현재 주요 명령은 다음과 같다.

#### `/settle-up`

영수증 이미지 업로드를 시작한다.

흐름:

1. slash command 실행
2. ephemeral 버튼 응답
3. 버튼 클릭 시 upload modal 열림
4. 파일 업로드
5. Blob 저장
6. 공개 pending 메시지 생성
7. parser callback 이후 공개 check 메시지로 전환

#### `/custom`

parser 없이 빈 receipt session을 바로 만든다.

주요 특징:

- `Seller Name = Custom`
- `Purchase Date = command 실행 시각 기준 날짜`
- `Buyer Name = command 실행자`
- 금액 필드는 모두 `0`
- owner가 이후 `Add item` 등으로 직접 채움

optional option:

- `payment_contact`

#### `/history`

현재 사용자(owner)가 confirm한 settlement history를 조회한다.

동작:

- `/history` : 최근 history 목록 조회
- `/history index:<번호>` : 현재 시점 기준 최신순 `n`번째 history 상세 조회

정책:

- `index:1`은 현재 시점 기준 가장 최근 confirm 결과
- 목록 캐시는 두지 않고 현재 정렬 기준으로 다시 조회

#### `/language`

사용자 UI 언어를 설정한다.

지원 언어:

- `English`
- `Korean`

정책:

- private/ephemeral/history는 호출 사용자 언어 사용
- 공개 receipt 메인 메시지는 owner 언어 사용
- 설정은 메모리 기반이며 재시작 시 초기화

#### Debug Commands

Development 환경에서만 등록된다.

- `/pingtest`
- `/test`

`/test` scenario 예시:

- 일반 draft
- liquor tax draft
- restaurant tip draft
- discount draft
- stacked discount draft

## `receipt-parser`

### HTTP Endpoints

#### `POST /api/events/blob-created`

Azure Event Grid가 Blob 생성 이벤트를 전달하는 운영 endpoint다.

주요 역할:

- Event Grid subscription validation 처리
- Blob created event 파싱
- Blob URL에서 `uploadedByUserId` 추출
- Document Intelligence `prebuilt-receipt` 실행
- Cosmos draft document upsert
- `discord-api /getting_draft` callback

#### `POST /api/tests/local-upload-parse`

로컬 테스트 보조 endpoint다.

활성 조건:

- `ReceiptParser__EnableLocalUploadTestEndpoint=true`

주요 역할:

- 파일 업로드 수신
- Document Intelligence 호출
- Cosmos 저장
- `discord-api` local callback 전송

## Service-to-Service Contract

### Draft Notification Payload

현재 `receipt-parser -> discord-api` payload는 아래 성격의 필드를 포함한다.

핵심 식별:

- `id`
- `status`
- `blobUrl`
- `uploadedByUserId`

receipt header:

- `merchantName`
- `transactionDate`
- `currency`
- `subtotal`
- `tax`
- `sst`
- `slt`
- `tip`
- `total`

items:

- `id`
- `description`
- `quantity`
- `unitPrice`
- `totalPrice`
- `originalUnitPrice`
- `originalTotalPrice`
- `discountAmount`

metadata:

- `parseMetadata.modelId`
- `parseMetadata.merchantConfidence`
- `parseMetadata.totalConfidence`
- `createdAtUtc`
- `updatedAtUtc`

### Contract Notes

- `uploadedByUserId`는 사실상 필수다.
- 현재 owner 권한 모델과 draft session 생성이 이 값에 의존한다.
- Blob URL 패턴이 바뀌면 parser 추출 규칙과 `discord-api` validation을 함께 봐야 한다.

## Current Data Handling Rules

### Discount Handling

현재 discount line 정책:

- 음수 금액 line은 일반 item으로 draft에 넣지 않는다.
- 우선 직전 일반 item에 discount로 귀속한다.
- 귀속 성공 item은 item-level discount로 전달한다.
- 귀속 실패 할인은 자동 반영하지 않는다.

관련 결정:

- `docs/decisions/019`

### KRW Tax Handling

현재 `discord-api` 정책:

- `Currency == KRW`이면 일반 `Tax`는 포함세로 보고 `0`으로 정규화
- 따라서 한국 영수증은 일반 tax가 총액에 다시 더해지지 않는다
- tax header / tax section도 표시되지 않는다

관련 결정:

- `docs/decisions/022`

## Authentication and Trust Boundary

현재 구현 기준:

- Discord interaction은 Discord gateway/session을 통해 수신
- `receipt-parser -> discord-api` callback은 현재 HTTP 기반
- callback 인증/서명 검증은 아직 강화 대상이다

즉 현재는 운영 구조는 돌아가지만, service-to-service trust hardening은 후속 작업으로 남아 있다.

## Related Documents

- `docs/architecture.md`
- `docs/ci-cd.md`
- `docs/decisions/007-use-http-for-communication-between-parser-discordapi.md`
- `docs/decisions/019-attribute-negative-receipt-lines-to-the-previous-item-and-ignore-unmatched-discounts.md`
- `docs/decisions/020-use-in-memory-user-language-preferences-with-owner-language-for-public-receipt-messages.md`
- `docs/decisions/021-add-a-custom-manual-settlement-entrypoint-with-a-blank-receipt-session.md`
- `docs/decisions/022-treat-general-tax-on-krw-receipts-as-tax-included.md`

# Architecture

Settle Up은 영수증 기반 비용 정산을 목표로 하는 multi-service mono-repo다.

현재 실제로 동작 중심이 잡혀 있는 서비스는 다음 두 개다.

- `discord-api`
- `receipt-parser`

이 문서는 현재 시스템의 구조, 책임 분리, 런타임 흐름, 그리고 현재 채택된 설계 방향을 정리한다.

## Architecture Summary

현재 구조는 다음 흐름을 중심으로 한다.

1. 사용자가 Discord에서 영수증 업로드
2. `discord-api`가 Blob Storage에 이미지 저장
3. Blob 생성 이벤트가 `receipt-parser`로 전달
4. `receipt-parser`가 Document Intelligence로 파싱
5. `receipt-parser`가 draft를 Cosmos에 저장
6. `receipt-parser`가 `discord-api`에 HTTP callback 전송
7. `discord-api`가 공개 settlement UI를 생성 또는 갱신
8. 사용자가 item 배정 후 confirm
9. `discord-api`가 confirmed history를 Cosmos에 저장

## Repository Structure

현재 주요 폴더:

```text
/
├─ docs/
│  ├─ decisions/
│  ├─ api.md
│  ├─ architecture.md
│  └─ ci-cd.md
├─ shared/
│  └─ SettleUp.Observability/
└─ services/
   ├─ discord-api/
   └─ receipt-parser/
```

장기적으로는 아래 서비스가 추가될 수 있다.

- `settlement-service`
- `user-service`
- `export-service`

현재는 아직 설계 후보 수준이며, 핵심 사용자 흐름은 `discord-api`와 `receipt-parser`가 담당한다.

## Service Responsibilities

### `discord-api`

주요 책임:

- Discord bot gateway 연결
- slash command / button / modal 처리
- 영수증 업로드 시작점 제공
- parser callback 수신
- 공개 receipt UI 렌더링
- item selection / add / remove / edit / confirm 처리
- history 저장 및 조회

현재 특징:

- 하나의 프로세스에서 Discord worker와 HTTP receiver를 함께 실행
- 공개 메인 메시지 1개를 수정하는 구조
- private panel과 공개 메시지 조합 UI
- session-scoped in-memory state 사용

### `receipt-parser`

주요 책임:

- Event Grid webhook 수신
- Blob 이미지 다운로드
- Document Intelligence receipt parsing
- 정규화된 draft document 저장
- `discord-api`로 draft callback 전송

현재 특징:

- 파싱 완료 결과와 전송 상태를 Cosmos에 저장
- retryable callback 정책 포함
- `uploadedByUserId` 추출이 핵심 계약 포인트

### `shared/SettleUp.Observability`

주요 책임:

- 공통 logging bootstrap
- OpenTelemetry 설정
- Azure Monitor / Application Insights exporter 연결

현재 방향:

- 서비스별 observability 코드를 따로 복제하지 않고 shared bootstrap 재사용

## Runtime Architecture

### Receipt Upload and Parsing Flow

```text
Discord User
  -> discord-api (/settle-up)
  -> Azure Blob Storage
  -> Event Grid
  -> receipt-parser
  -> Azure Document Intelligence
  -> Cosmos DB (draft receipt)
  -> discord-api (/getting_draft)
  -> Discord public settlement UI
```

### Manual Settlement Flow

```text
Discord User
  -> discord-api (/custom)
  -> in-memory blank receipt session
  -> Discord public settlement UI
  -> owner adds items manually
  -> confirm
  -> Cosmos DB (confirmed history)
```

### History Flow

```text
Discord User
  -> discord-api (/history or /history index:n)
  -> Cosmos DB (confirmed history container)
  -> ephemeral history response
```

## State Model

### In-Progress Receipt State

진행 중 receipt state는 현재 `discord-api` 메모리 세션에 있다.

포함 내용:

- receipt header
- parsed items
- user selections
- public message metadata
- render cache
- pending edit token
- owner language for public UI

이 상태는 재시작 시 유지되지 않는다.

### Confirmed History State

confirm 이후에는 snapshot을 Cosmos DB에 저장한다.

정책:

- confirm UI를 먼저 갱신
- history persistence는 background에서 retry
- 저장 실패 시 ephemeral 오류 안내

관련 결정:

- `docs/decisions/017`

## Discord UI Architecture

현재 receipt UI는 다음 원칙을 따른다.

- 공개 메인 메시지 하나를 중심으로 유지
- routine interaction은 private panel에서 수행
- 공개 메시지는 debounce 후 갱신
- confirm은 즉시 confirmed embed로 전환

주요 버튼:

- `Select Item`
- `Add Item`
- `Remove Item`
- `Edit Item`
- `Mark Alcohol`
- `Confirm`
- `Cancel`

권한 정책:

- `Select Item`은 참여자 누구나 가능
- `Add/Remove/Edit/Mark Alcohol/Confirm/Cancel`은 owner 전용

중요한 제약:

- Discord 공개 메시지는 사용자별로 다른 버튼 disabled 상태를 줄 수 없다
- 따라서 owner 전용 버튼도 non-owner가 클릭 자체는 가능하고, 서버에서 차단한다
- 공개 메시지 언어도 사용자별이 아니라 owner 기준 하나만 사용한다

## Tax and Money Model

현재 money model은 다음을 포함한다.

- `Subtotal`
- `Tax`
- `SST`
- `SLT`
- `Tip`
- `Total`

추가 정책:

- discount는 item-level discount 우선
- 귀속 실패 할인은 자동 반영하지 않음
- `KRW` receipt는 일반 tax를 포함세로 보고 계산/표시에서 제외

관련 결정:

- `docs/decisions/014`
- `docs/decisions/019`
- `docs/decisions/022`

## Localization Model

현재 localization은 `discord-api` 내부에서 처리한다.

정책:

- 사용자 언어 설정은 메모리 기반
- 지원 언어는 English / Korean
- private/ephemeral/history는 호출 사용자 언어 사용
- 공개 receipt 메인 메시지는 owner 언어 사용
- slash command 설명은 쉬운 영어로 유지
- 로그와 exception 메시지는 영어로 유지

관련 결정:

- `docs/decisions/020`

## Integration and Trust Boundaries

현재 주요 경계는 다음과 같다.

- Discord <-> `discord-api`
- `discord-api` <-> Azure Blob Storage
- Blob Storage/Event Grid <-> `receipt-parser`
- `receipt-parser` <-> Document Intelligence
- `receipt-parser` <-> Cosmos DB
- `receipt-parser` <-> `discord-api`
- `discord-api` <-> Cosmos DB

보안 관점에서 특히 중요한 지점:

- Event Grid payload validation
- Blob URL parsing
- `receipt-parser -> discord-api` callback validation
- Azure secret/configuration management

현재 secret은 환경 변수 기반이고, Azure에서는 Container App environment + Key Vault reference 조합을 사용할 수 있다.

## Performance and Concurrency

현재 `discord-api`는 다음 최적화를 사용한다.

- receipt session 단위 in-memory lock
- 공개 메인 메시지 갱신 1초 debounce
- 공개 메인 메시지 객체 캐시
- rendered embed cache
- Discord transient error retry

관련 결정:

- `docs/decisions/012`
- `docs/decisions/013`

`receipt-parser`는 다음을 사용한다.

- Cosmos container lazy initialization
- callback retry 정책
- blob download path 단순화

## Current Limitations

- 진행 중 receipt session은 재시작 시 사라진다
- callback 인증/검증이 아직 강화 전 단계다
- parser의 item normalization은 여전히 OCR 품질에 영향을 받는다
- 국가별 tax policy는 아직 일부(`KRW`)만 명시 정책이 있다
- service count는 늘어날 수 있지만 현재 핵심 흐름은 두 서비스에 집중돼 있다

## Related Documents

- `docs/api.md`
- `docs/ci-cd.md`
- `docs/decisions/README.md`

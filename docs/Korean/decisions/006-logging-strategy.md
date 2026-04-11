# 006 - Observability와 Logging 전략 표준화

## Status
Accepted

## Context

Settle Up에는 현재 다음과 같은 여러 서비스가 있다.

- `discord-api`
- `receipt-parser`

observability를 넣는 과정에서 서로 다른 신호가 한 콘솔 출력에 섞이면서 콘솔이 매우 시끄러워졌다.

- Discord.Net 내부 로그
- OpenTelemetry `HttpClient` instrumentation 출력
- custom application `Activity` trace
- 일반 application log

그 결과, local debugging이 더 어려워졌다. 낮은 수준의 trace와 raw activity dump가 다음 같은 높은 가치의 운영 이벤트와 함께 섞였기 때문이다.

- service startup
- Discord ready
- slash command execution
- blob event processing
- Cosmos DB write
- failure 및 warning

또한 프로젝트는 새 서비스가 추가되어도 깔끔하게 확장되는 observability 패턴과, `APPLICATIONINSIGHTS_CONNECTION_STRING`를 통한 Azure Monitor / Application Insights 통합을 지원할 필요가 있다.

## Options Considered

### Option A - 콘솔 중심 mixed logging 유지

장점:

- 가장 단순한 구성
- 개념적 부담이 적다.
- 초기 개발 단계에서는 시작하기 쉽다.

단점:

- 콘솔 출력 가독성이 급격히 나빠진다.
- application log와 tracing 신호가 명확히 분리되지 않는다.
- 중요 운영 정보가 낮은 가치의 노이즈에 묻힐 수 있다.
- 여러 서비스로 같은 패턴을 확장하기 어렵다.

### Option B - 사람이 읽는 로그와 tracing을 분리

장점:

- application log를 콘솔에서 읽기 쉽게 유지할 수 있다.
- tracing은 전용 observability 도구로 export할 수 있다.
- 서비스가 늘어나도 일관된 패턴으로 확장하기 쉽다.
- dependency tracing과 cross-service correlation을 더 잘 지원한다.

단점:

- 설정이 더 복잡하다.
- 개발자가 logging과 tracing의 차이를 이해해야 한다.
- shared observability bootstrap이 구조적 오버헤드를 조금 추가한다.

## Decision

application logging과 observability tracing을 분리한다.

표준은 다음과 같다.

- 사람이 읽는 application log는 `ILogger` 사용
- tracing과 dependency observability는 OpenTelemetry 사용
- 설정된 경우 export 대상은 Azure Monitor / Application Insights를 기본으로 사용

콘솔 출력은 raw trace dump보다 읽기 쉬운 application log를 우선해야 한다.

## Consequences

### Positive

- 콘솔 출력 가독성이 좋아진다.
- application log와 tracing의 역할이 더 명확해진다.
- 콘솔은 깔끔하게 두면서 Azure Monitor로 더 풍부한 telemetry를 보낼 수 있다.
- 향후 서비스에도 확장 가능한 observability 패턴을 갖게 된다.

### Negative

- 단순 콘솔 로깅보다 설정이 더 복잡하다.
- 개발자가 `ILogger`와 OpenTelemetry의 구분을 이해하고 유지해야 한다.
- shared observability 코드가 구조적 오버헤드를 조금 만든다.

## Follow-up Notes

이 패턴은 현재 서비스와 향후 서비스 전반에 일관되게 적용해야 한다.

구현 기대사항은 다음과 같다.

- 의미 있는 application event는 `ILogger`로 기록한다.
- noisy한 raw console trace output은 최소화한다.
- `APPLICATIONINSIGHTS_CONNECTION_STRING`가 있으면 trace를 Azure Monitor / Application Insights로 export한다.
- exporter가 없어도 서비스는 계속 동작해야 한다.

# Performance Review 2026-04-04

이 문서는 `discord-api`와 `receipt-parser`의 현재 구현을 기준으로, 병목 가능 지점과 메모리 증가 위험을 점검한 결과를 정리한다.

리뷰의 초점은 다음 두 가지였다.

- 사용자가 영수증을 업로드한 뒤 공개 check 메시지가 나오기까지의 지연
- 서비스가 오래 실행될 때 불필요한 호출과 in-memory 상태가 누적되는 구조가 있는지 여부

## Scope

검토 범위:

- `services/discord-api`
- `services/receipt-parser`

특히 다음 흐름을 집중적으로 봤다.

1. Discord `/settle-up` 업로드 시작
2. Blob 업로드
3. Event Grid -> `receipt-parser`
4. Document Intelligence 파싱
5. parser Cosmos 저장
6. `discord-api` callback
7. 공개 check 메시지 게시 또는 갱신

## Summary

현재 체감되는 첫 업로드 지연은 단일 원인보다는 cold path가 연속으로 겹친 결과로 보인다.

주요 원인은 다음과 같다.

- `discord-api` 업로드 시 Blob container 존재 확인을 매번 수행
- `receipt-parser`가 첫 파싱 시 Blob 다운로드, Document Intelligence 분석, Cosmos container 초기화를 모두 cold path에서 수행
- `discord-api`가 parser callback 처리 중 업로더 표시 이름을 Discord REST API로 다시 조회

추가로, 현재 `discord-api`는 confirm된 세션과 세션별 lock을 메모리에 계속 유지하므로 장기 실행 시 메모리 증가 위험이 있다.

## Findings

### 1. Draft publish 직전에 Discord 사용자 정보를 다시 조회한다

심각도:

- High

설명:

`discord-api`는 parser callback을 받아 공개 check 메시지를 만들거나 갱신하기 전에 `ResolveUploadedByDisplayNameAsync`를 호출해 업로더 이름을 Discord REST에서 다시 조회한다.

이 호출은 check 메시지 게시 직전의 임계 경로에 들어 있다. 이미 pending 세션 생성 시 업로더 표시 이름을 알고 있는 경우에도 재조회하므로, 불필요한 네트워크 호출이 하나 추가된다.

첫 실행에서 특히 느리게 느껴지는 이유와도 잘 맞는다. 첫 Discord REST 호출은 DNS, TLS, connection setup 비용까지 함께 낼 수 있기 때문이다.

관련 코드:

- [ReceiptDraftSessionService.cs#L165](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptDraftSessionService.cs#L165)
- [ReceiptDraftSessionService.cs#L178](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptDraftSessionService.cs#L178)
- [ReceiptDraftSessionService.cs#L281](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptDraftSessionService.cs#L281)

영향:

- parser callback 이후 공개 check 메시지 표시까지의 시간 증가
- cold start에서 체감 지연 확대

권장 방향:

- pending 세션에 이미 저장된 표시 이름을 우선 재사용
- 꼭 필요할 때만 Discord REST 재조회

### 2. Blob container existence check가 업로드마다 호출된다

심각도:

- High

설명:

`discord-api`의 Blob 업로더는 업로드 시마다 `CreateIfNotExistsAsync`를 호출한다.

이 호출은 네트워크 round trip이 필요하며, 운영에서 container가 이미 존재하는 상황에서도 매 업로드마다 실행된다. 첫 업로드에서는 Azure 인증과 첫 연결 초기화 비용까지 겹치므로 더 느릴 수 있다.

관련 코드:

- [BlobImageUploader.cs#L61](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Storage/BlobImageUploader.cs#L61)
- [BlobImageUploader.cs#L68](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Storage/BlobImageUploader.cs#L68)

영향:

- `/settle-up` 업로드 시작 단계의 불필요한 지연
- 첫 업로드에서 cold auth 비용과 중첩

권장 방향:

- startup 또는 lazy one-time initialization으로 이동
- 요청 경로에서 매번 container existence check를 하지 않도록 변경

### 3. `receipt-parser`의 첫 번째 처리 경로는 cold path가 길다

심각도:

- High

설명:

현재 `receipt-parser`의 첫 번째 영수증 처리는 다음 비용을 연속으로 부담한다.

- Blob 다운로드
- Document Intelligence 호출
- parser Cosmos container initialization

`AnalyzeDocumentAsync(WaitUntil.Completed, ...)`는 receipt 분석 완료까지 대기하는 구조이고, parser Cosmos repository는 첫 저장 시 `CreateContainerIfNotExistsAsync`를 수행한다.

관련 코드:

- [DocumentIntelligenceReceiptParser.cs#L39](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/DocumentIntelligenceReceiptParser.cs#L39)
- [DocumentIntelligenceReceiptParser.cs#L71](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/DocumentIntelligenceReceiptParser.cs#L71)
- [CosmosReceiptRepository.cs#L46](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/CosmosReceiptRepository.cs#L46)
- [CosmosReceiptRepository.cs#L90](/home/aero-mere/CS397/Settle_Up/services/receipt-parser/Services/CosmosReceiptRepository.cs#L90)

영향:

- 첫 번째 영수증 처리 시간이 유독 길어짐
- 사용자가 "봇이 켜지고 처음 한 번 특히 느리다"고 느끼는 증상과 일치

권장 방향:

- parser startup warm-up 고려
- Cosmos container initialization을 요청 경로 밖으로 이동하거나 one-time prewarm
- 운영에서 cold start 자체를 줄이는 배포 설정 검토

### 4. Confirm된 세션이 메모리에서 제거되지 않는다

심각도:

- Medium

설명:

현재 `discord-api`는 cancel 시에는 세션을 제거하지만, confirm 시에는 세션을 `IsConfirmed = true`로만 바꾸고 store에서 제거하지 않는다.

즉 confirm된 세션이 계속 `_sessions`에 남는다. 세션 수가 계속 증가하면 메모리 사용량뿐 아니라 전체 세션 선형 탐색을 사용하는 기능에도 영향이 갈 수 있다.

관련 코드:

- [ReceiptInteractionService.cs#L561](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptInteractionService.cs#L561)
- [ReceiptInteractionService.cs#L664](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptInteractionService.cs#L664)
- [ReceiptSessionStore.cs#L5](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptSessionStore.cs#L5)
- [ReceiptSessionStore.cs#L76](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptSessionStore.cs#L76)

영향:

- 장기 실행 시 메모리 증가
- 일부 기능에서 선형 탐색 비용 증가

권장 방향:

- confirm 후 세션 제거 또는 TTL 기반 정리 추가
- confirmed UI를 유지해야 한다면 최소 메타데이터만 남기도록 축소 검토

### 5. 세션별 lock이 정리되지 않는다

심각도:

- Medium

설명:

`ReceiptSessionLockManager`는 세션별 `SemaphoreSlim`을 `ConcurrentDictionary`에 보관하지만, 제거 로직이 없다.

세션 수가 늘어날수록 lock dictionary도 함께 커진다. confirm된 세션을 store에서 제거하지 않는 현재 구조와 결합되면 이 누적이 더 의미 있게 된다.

관련 코드:

- [ReceiptSessionLockManager.cs#L5](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptSessionLockManager.cs#L5)

영향:

- 장기 실행 시 메모리 증가

권장 방향:

- 세션 종료 시 lock entry 정리
- 또는 reference counting / TTL 기반 정리

### 6. `/language`는 전체 세션을 선형 탐색한다

심각도:

- Low

설명:

`/language`는 사용자의 공개 언어를 바꾼 뒤, `ReceiptSessionStore.GetAll()`로 전체 세션을 순회해 owner가 같은 세션을 refresh한다.

현재는 자주 호출되는 경로가 아니어서 급한 병목은 아니지만, confirm된 세션이 계속 메모리에 남는 구조와 결합되면 비용이 점점 커질 수 있다.

관련 코드:

- [LanguageCommandHandler.cs#L52](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Commands/LanguageCommandHandler.cs#L52)
- [ReceiptSessionStore.cs#L76](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptSessionStore.cs#L76)

권장 방향:

- owner id 기준 보조 index 추가 검토
- 또는 active session만 대상으로 제한

### 7. 사용자 언어 설정도 메모리에 계속 쌓인다

심각도:

- Low

설명:

`UserLanguagePreferenceStore`는 in-memory dictionary만 사용하고 제거/만료 정책이 없다.

현재 규모에서는 큰 문제는 아니지만, 서비스가 오래 살아 있고 사용자 수가 계속 늘면 사전도 계속 커진다.

관련 코드:

- [UserLanguagePreferenceStore.cs#L5](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Localization/UserLanguagePreferenceStore.cs#L5)

권장 방향:

- 현 단계에서는 허용 가능
- 운영 규모가 커지면 persistence 또는 cleanup 정책 검토

## Non-Issues for This Symptom

이번 체감 지연과 직접 관련이 적어 보인 항목:

- 공개 메시지 1초 디바운스

이유:

- draft callback 이후 공개 check 메시지 생성/갱신은 디바운스 경로보다 직접 `SendToChannelAsync` 또는 `RefreshAsync`를 호출하는 경로를 사용한다.

관련 코드:

- [ReceiptDraftSessionService.cs#L202](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptDraftSessionService.cs#L202)
- [ReceiptDraftSessionService.cs#L222](/home/aero-mere/CS397/Settle_Up/services/discord-api/src/Services/ReceiptDraftSessionService.cs#L222)

## Why The First Upload Feels Especially Slow

현재 첫 업로드의 cold path는 대략 아래 순서로 해석된다.

1. `discord-api`가 Blob container existence check 수행
2. Blob 업로드 시 Azure 인증 및 첫 연결 비용 발생 가능
3. `receipt-parser`가 Blob 다운로드
4. `receipt-parser`가 Document Intelligence 첫 분석 호출
5. `receipt-parser`가 Cosmos container initialization 및 upsert 수행
6. `discord-api` callback 처리 중 Discord REST로 업로더 이름 재조회
7. 공개 check 메시지 게시 또는 갱신

즉 "첫 영수증에서 특히 느리다"는 것은 현재 코드와 운영 특성상 충분히 설명되는 증상이다.

## Recommended Next Steps

우선순위 제안:

1. `discord-api` draft publish 경로에서 Discord 사용자 이름 재조회 제거
2. Blob container existence check를 요청 경로 밖으로 이동
3. parser Cosmos container initialization을 one-time prewarm으로 이동
4. confirm 이후 세션 및 lock cleanup 정책 추가
5. 필요 시 startup warm-up 추가

## Notes

이 문서는 현재 코드 탐색 기준의 진단 문서다.

- 실제 지연 비율은 Azure 환경, cold start 빈도, network 상태, Document Intelligence 응답 시간에 따라 달라질 수 있다.
- 정량적 latency profiling이 필요하면 추후 OpenTelemetry span duration과 structured logs를 함께 수집하는 추가 작업이 필요하다.

# EF-25 — 복사용 목표 실행 명령문

```text
ServeredMeridian의 EF-25 내구도형 피해 집계 조회 정책 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-24-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-25-damage-aggregation-query-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-25-damage-aggregation-query-goal.md

이번 단계만 수행하고 work와 EF-23~24 미커밋 변경을 유지해. 커밋하지 마. 내구도형5종의 Query/Apply/미션/예약·타격/턴 기록 호출부를 조사하고, 전환 전 실제 입력/결과와 원본/GUID/과거 증거를 저장해. 본체별 종류의 내구도 전체 범위와 금속기둥1~9/4칸, hit0/같은hit 같은칸/다른칸/다른hit·같은본체/다른본체·null/새/같은턴/다음턴·색/null·원인4/-1/4·삭제/벽/거리/보호/비활성을 구분해.

계획의 불변 ElementDamageAggregationPolicy(PerHitCell, AlreadyApplied)를 추가하고 ElementDefinition의 기존2~6인자 계약을 유지하며7인자 경로와 필수 조회를 추가해. false/true 정책을 각각 한 번 준비하고 Crate/Scrap/Safe/ColorLock은false를 공유, Appliance는true로 등록해. Generator에는 필수 집계 정책을 요구하지 마. Query의 원인/발전기/색/내구도 검사 뒤 기존 집계 결과만 같은 Get→Require→AlreadyApplied와 PerHitCell로 최소 연결해. null 문맥/hit0·기존 반응/양/메시지·순서를 유지해. 누락은 ID 포함 오류이고 기본값을 대체하지 마. true/false 메모리 정책도 같은 카탈로그 조회에 반영됨을 확인해.

검사부터 작성해 계약 누락 RED와 연결 후 GREEN을 확인해. 동일 저장 입력/시드로 Query 상태/비공개 문맥/규칙·전역 난수 무변경과 실제 피해/턴당·칸별/hit0/제거·미션/효과/연결 철거/예약·MemoryPack 전체 바이트/ID/버전1/50구간을 전후 비교해. 기존 원인/색/배치 정책·발전기/공급 고철 경계를 보존해.

별도 Editor에서 새 검사와 ColorMatchPolicyVerification.Run, GeneratorReactionPolicyVerification.Run, ApplianceDamagePolicyVerification.Run, ColorLockDamagePolicyVerification.Run, CapsuleDamagePolicyVerification.Run, ScrapDamagePolicyVerification.Run, CrateDamagePolicyVerification.Run, GeneratorPlacementVerification.Run, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data, PowerEffectVerification.Data를 실행해. 정확한 메서드/실제 입력·응답·결과/각 종료0/필수FAIL0과 과거 출력 백업·복원 바이트 동일을 기록해.

피해 실행/Apply/Remove/RegisterDamage/RegisterHit/턴 기록·ReservedDamage/예약 투영·Mission·GeneratorRules.Query/Apply/충전/연결/철거, 기존 원인/색/배치 정책·공급/낙하·드론/봇/UI/MVVM/표현/풀·제작/배포/저장/변환은 변경하지 마. EF-05/09/11·원본/GUID/enum 숫자/기존 작업을 보존해. 커밋/빌드/재패킹/이미지/팩 재생성/사용자 Unity 종료/씬 저장 금지. 실패를 숨기거나 목표를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-25-progress.md에 변경·실측·검증·차이·미검증·남은 문제를 저장해. 완료 조건 충족 후 보고하고 결과에 맞춘 다음 한 단계 계획/목표/명령문만 작성해. 명령문을 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에 전체 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

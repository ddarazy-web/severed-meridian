# EF-26 — 복사용 목표 실행 명령문

```text
ServeredMeridian의 EF-26 내구도형 예약 피해량 조회 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-25-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-26-reserved-damage-query-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-26-reserved-damage-query-goal.md

이번 단계만 수행하고 work와 EF-23~25 미커밋 변경을 유지해. 커밋하지 마. ReservedDamage/Project/PendingBodyRemovals·드론 후보/예약·실제 타격 호출부를 조사하고 원본/GUID/기존 작업/과거 출력과 전환 전 실제 입력/결과를 저장해. 5종 내구도 전체/0·cells0/1/2/4/9/81/-1·중복/다른칸/다른본체·문맥/삭제·발전기/미지원의 경계를 구분해.

검사부터 작성해 전환 전 기준을 확보해. 테스트 메모리에서 같은 ID의 true/false 집계 정의를 조회하도록 준비하고 직접 ReservedDamage가 그 값을 따르는지 검사해 기존 종류 조건의 실제 RED를 확인해. 원래 등록 참조는 finally에서 정확히 복원하고 공개 수정 API나 문자열 실패를 만들지 마. 기존 DamageAggregationPolicy.PerHitCell을 재사용해 ObstacleDamageRules.ReservedDamage의 집계 단위 조건만 최소 연결한 뒤 GREEN과 원래 입력 전체 동등성을 확인해. 유효 내구도형5종은 같은 Get→Require→PerHitCell을 사용하고 true면 cells, false면 Min(1,cells), 최종 내구도 Min을 유지해. Generator/미지원은 기존 본체별 계산을 유지하며 정책을 강제하지 마. 유효5종 누락은 ID 포함 오류이고 기본값 대체는 없어야 해. 새 정책 타입/메서드/필드/생성 계약을 만들거나 음수 경계를 임의 보정하지 마.

동일 저장 입력/시드로 전체 결과·상태/비공개 문맥/규칙·전역 난수 무변경과 실제 중복 제거/본체·칸 집계/상한·기여/완료 예상·예약 취소/무효화·타격/미션/효과·발전기 충전/연결/철거·MemoryPack 전체 바이트/ID/버전1/50구간 전후 일치를 확인해. 실제 피해량/예약 결과 검사로 연결을 검증해.

별도 Editor에서 새 검사와 DamageAggregationPolicyVerification.Run, ColorMatchPolicyVerification.Run, GeneratorReactionPolicyVerification.Run, ApplianceDamagePolicyVerification.Run, ColorLockDamagePolicyVerification.Run, CapsuleDamagePolicyVerification.Run, ScrapDamagePolicyVerification.Run, CrateDamagePolicyVerification.Run, GeneratorPlacementVerification.Run, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data, PowerEffectVerification.Data를 각각 실행해. 정확한 메서드/실제 입력·응답·결과/각 종료0/필수FAIL0과 과거 출력 백업·복원 바이트 동일을 기록해.

Query/Apply/Remove/RegisterDamage/RegisterHit/턴 기록·MissionProgressRules.Project/Query/PendingBodyRemovals/완료·DroneTargetManager/예약 저장·GeneratorRules/충전/연결/철거, 기존 정책/카탈로그/생성 계약·공급/낙하/드론/봇/UI/MVVM/표현/풀·제작/배포/저장/변환은 변경하지 마. EF-05/09/11·원본/GUID/enum 숫자/기존 작업을 보존해. 커밋/빌드/재패킹/이미지/팩 재생성/사용자 Unity 종료/씬 저장 금지. 실패를 숨기거나 목표를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-26-progress.md에 변경·실측·검증·차이·미검증·남은 문제를 저장해. 완료 조건 충족 후 보고하고 결과에 맞춘 다음 한 단계 계획/목표/명령문만 작성해. 명령문을 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에 전체 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

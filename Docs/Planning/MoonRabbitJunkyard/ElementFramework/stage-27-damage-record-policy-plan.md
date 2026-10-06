# EF-27 — 실제 타격 기록 집계 연결 계획

상태: 완료. 185192 PASS/0 FAIL, 최종20종 종료0. [검증 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-27-progress.md). EF-25 조회/EF-26 예약 피해량 다음의 실제 타격 기록 한 책임만 연결한다.

연결: [가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-26 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-26-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-27-damage-record-policy-goal.md).

## 목적과 최소 계약

Query와 ReservedDamage는 기존 DamageAggregationPolicy를 읽지만 Apply는 아직 Appliance 종류로 RegisterHit/나머지 RegisterDamage를 선택한다. 기존5종이 동일하게 동작하더라도 등록 정책을 바꾼 실제 실행과 조회가 어긋나는 경계다. 이 기록 선택만 PerHitCell로 연결한다. 새 정책 타입/메서드/필드/생성 계약을 만들지 않는다.

Apply의 지원 내구도형5종은 Get→RequireDamageAggregationPolicy→PerHitCell을 읽어 true면 기존 context.RegisterHit(hit,cell.Coordinate), false면 기존 context.RegisterDamage(index)를 호출한다. 기록 다음 내구도 감소→0이면 Remove→GeneratorRules.TargetRemoved→남은 내구도 반환의 순서는 유지한다. 유효5종 누락은 ID 포함 오류로 거절하고 기본값 대체가 없다. 발전기/미지원 직접 Apply의 기존 본체 기록 경계는 별도로 확보하고 정책을 강제하지 않는다. 실제 발전기 Charge 경로는 수정하지 않는다.

## 작업과 검증

1. work/EF-23~26 미커밋·원본/GUID/과거 출력을 보호한다. 실제 Evaluate/Query→Apply 호출부와 본체/칸 기록·삭제/발전기 통보를 조사한다. 전체 내구도·같은본체/다른본체·같은칸/다른칸·같은hit/다른hit/hit0·같은턴/다음턴·null/0/삭제 및 발전기/미지원 직접 경계를 구분해 저장 입력/결과를 확보한다.
2. DamageRecordPolicyVerification의 Before/Run을 먼저 작성한다. 같은 ID의 테스트 메모리 true/false 정의로 실제 Query→Apply→재Query/다음 타격을 실행해 남은 내구도와 비공개 기록이 정책을 따르는지 검사한다. 기존 종류 조건의 실제 RED를 확인한다. 등록 참조는 finally에서 정확히 복원하고 공개 수정 API·문자열 실패를 만들지 않는다.
3. 기록 선택만 최소 연결한 뒤 GREEN과 동일 저장 입력/시드의 전체 결과·상태/비공개 문맥·규칙/전역 난수·미션/효과·예약/기여/취소/재선정·발전기 충전/연결/철거·MemoryPack 전체 바이트/본체ID/버전1/50구간 동등성을 확인한다. 원래 등록 기준의 모든 종류별 반응·공급 고철을 유지한다. 직접 Apply는 호출자가 Query를 먼저 거친 정상 경로와 구분하며 음수/0/null을 임의 보정하지 않는다.
4. 별도 안전한 Editor에서 새 검사와 EF-26 필수19종을 각각 실행한다: ReservedDamagePolicyVerification.Run, DamageAggregationPolicyVerification.Run, ColorMatchPolicyVerification.Run, GeneratorReactionPolicyVerification.Run, ApplianceDamagePolicyVerification.Run, ColorLockDamagePolicyVerification.Run, CapsuleDamagePolicyVerification.Run, ScrapDamagePolicyVerification.Run, CrateDamagePolicyVerification.Run, GeneratorPlacementVerification.Run, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data, PowerEffectVerification.Data. Elements 검사는 Elements.Editor, 마지막5종은 Levels.Editor다. 정확한 메서드/실제 입력·응답·결과·각 종료0/필수FAIL0과 과거 출력 백업/복원 바이트 동일을 기록한다.
5. stage-27-progress.md에 변경·실측·차이·미검증·남은 문제를 저장하고 계획/목표를 갱신한다. 완료 보고와 결과에 맞춘 다음 한 단계 계획/목표/전체 복사용 명령문만 작성한다. 다음 구현은 시작하지 않는다.

실행 담당: executing-plans. 에이전트는 별도 요청 시에만 사용한다.

## 제외

Query/ReservedDamage/Remove/RegisterDamage/RegisterHit 자체·턴 기록 구조·MissionProgressRules/DroneTargetManager/예약 저장·GeneratorRules/충전/연결/철거, 기존 정책/카탈로그/생성 계약·공급/낙하/드론/봇/UI/MVVM/표현/풀·제작/배포/저장/변환은 수정하지 않는다. EF-05/09/11·원본/meta/GUID/enum 숫자/기존 작업을 보존한다. 커밋/빌드/재패킹/이미지/팩 재생성/사용자 Unity 종료/씬 저장 금지.

# EF-26 — 내구도형 예약 피해량 조회 연결 계획

상태: 완료. 새7793+기존135120=142913 PASS/0 FAIL, 전후4540행 전체 바이트 동일. 결과는 [EF-26 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-26-progress.md)에 기록했다. 다음은 EF-27 준비만 완료했다.

연결: [가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-25 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-25-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-26-reserved-damage-query-goal.md).

## 목적과 최소 계약

MissionProgressRules.Project는 중복 좌표를 제거하고 본체별로 묶어 ReservedDamage를 조회한다. 현재 Appliance만 칸 수, 나머지는 최대1을 사용하는 조건이 남았다. EF-25의 불변 DamageAggregationPolicy.PerHitCell을 재사용해 이 조건만 정의 조회로 연결한다. 새 정책 타입/메서드·생성자·정의 필드를 추가하지 않는다.

ObstacleDamageRules.ReservedDamage에서 지원 내구도형5종만 Get→RequireDamageAggregationPolicy→PerHitCell을 읽는다. true면 cells, false면 Min(1,cells), 마지막 Min(body.Durability,...)는 그대로다. Generator와 미지원 종류의 기존 본체별 계산도 그대로 유지하고 필수 정책 조회에 들어가지 않도록 명시적으로 구분한다. 실제 Project의 발전기 충전 별도 분기와 합산/기여는 수정하지 않는다. 유효5종 누락은 기존 Require의 ID 포함 오류를 유지한다. 음수 등 직접 조회 경계도 현재 의미를 임의 보정하지 않는다.

## 작업과 검증

1. work/EF23~25 미커밋·원본/GUID/과거 출력 보호, ReservedDamage/Project/PendingBodyRemovals·드론 후보/예약·실제 타격 호출부 확인. 5종 내구도 전체/0·cells0/1/2/4/9/81와-1·중복/다른칸/다른본체·문맥/삭제·발전기/미지원의 실제 입력/응답과 전체 원본/팩 증거를 저장한다.
2. ReservedDamagePolicyVerification의 Before/Run을 먼저 작성해 전환 전 기준을 확보한다. 테스트 메모리에서 같은 ID의 true/false 집계 정의를 조회하도록 준비하고 직접 ReservedDamage가 그 값을 따르는지 검사해 기존 종류 조건의 실제 RED를 확인한다. 원래 등록 참조는 finally에서 정확히 복원하고 공개 수정 API/새 계약/문자열 실패를 만들지 않는다. 최소 연결 후 같은 검사 GREEN과 원래 입력 전체 동등성을 확인한다.
3. 전체 저장 결과·상태/비공개 문맥/규칙·난수 무변경, 실제 중복 좌표 제거/본체·칸 집계·상한/기여·완료 예상·예약 취소/무효화·발전기 충전/연결/철거·실제 타격/미션/효과·MemoryPack 전체바이트/ID/버전1/50구간을 전후 비교한다. 각 종류의 기존 반응 조건과 공급 고철을 보존한다.
4. 별도 안전한 Editor에서 새 검사와 EF-25 기존18종을 각각 실행해 정확한 메서드/실제 입력·응답·각 종료0/필수FAIL0을 기록하고 과거 출력은 백업/복원 후 바이트 동일을 확인한다.
5. stage-26-progress.md에 변경/실측/차이/미검증/남은 문제를 저장하고 현재 계획/목표를 갱신한다. 완료 후 결과에 맞춘 다음 한 단계 계획/목표/전체 복사용 명령문만 작성·제시한다. 다음 구현은 시작하지 않는다.

실행 담당: executing-plans. 에이전트는 별도 요청 시에만 사용한다.

## 제외

Query/Apply/Remove/RegisterDamage/RegisterHit/턴 기록·MissionProgressRules.Project/Query/PendingBodyRemovals/완료·DroneTargetManager/예약 저장·GeneratorRules/충전/연결/철거, 기존 정책/카탈로그/생성 계약·공급/낙하/봇/드론/UI/MVVM/표현/풀·제작/배포/저장/변환은 수정하지 않는다. EF-05/09/11·원본/meta/enum 숫자/기존 작업을 보존한다. 커밋/빌드/재패킹/이미지/팩 재생성/사용자 Unity 종료/씬 저장 금지.

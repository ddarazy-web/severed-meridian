# EF-25 — 내구도형 피해 집계 조회 정책 연결 계획

상태: 완료. 별도 Editor18종 종료0/필수FAIL0, 전체 저장 결과 전후 동일.

연결: [가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-24 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-24-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-25-damage-aggregation-query-goal.md).

## 목적과 최소 계약

현재 내구도형4종은 HasDamaged(bodyIndex), 금속기둥 상자는 HasHit(hit,cell)로 조회한다. 이 두 경로는 같은 Query 끝의 집계 조회 책임이다. 5종을 개별 단계로 반복하지 않고 불변 정책 하나와 공유 값 두 개로 연결한다. 실제 RegisterDamage/RegisterHit, 피해 적용, ReservedDamage/예약 투영은 이번 단계에서 바꾸지 않는다.

Elements/Data에 불변 ElementDamageAggregationPolicy를 추가한다. 읽기 전용 PerHitCell(bool)과 AlreadyApplied(TurnEffectContext context,int bodyIndex,BoardCoordinate cell,int hit)를 제공한다. false는 context?.HasDamaged(bodyIndex), true는 context?.HasHit(hit,cell)만 읽는다. null 문맥은false이고 hit0의 기존 제한 없음도 유지한다. 상태·문맥을 필드에 보유하거나 기록하지 않는다.

ElementDefinition에 읽기 전용 DamageAggregationPolicy/RequireDamageAggregationPolicy와7인자 생성 경로를 추가하고 기존2~6인자 계약을 보존한다. 누락은 ID 포함 오류이며 기본값 대체가 없다. LegacyElementDefinitions에 본체별 false/칸별 true 불변 값을 각각 한 번 준비한다. Crate/Scrap/Safe/ColorLock은false를 공유하고 Appliance는true를 사용한다. Generator에는 필수 집계 정책을 요구하지 않는다.

ObstacleDamageRules.Query의 원인/발전기/색/내구도 검사 순서를 유지하고, 마지막 집계 결과만 Get→Require→AlreadyApplied와 PerHitCell로 결정한다. 본체별/폐가전칸별 기존 Response/Amount/Message를 그대로 사용한다. Supports·ReservedDamage·Mission·Apply/Remove·GeneratorRules는 수정하지 않는다. 기존 카탈로그 불변 계약 검사에는 새 불변 필드 타입만 반영한다.

## 작업과 검증

1. work/EF23~24 미커밋 변경·원본/GUID/과거 증거를 보호한다. 5종 Query/Apply/미션/예약·타격/턴 기록 호출부를 조사하고 전환 전 실제 입력/응답을 저장한다. 본체별 종류의 내구도 전체 범위, 칸별1~9/4칸, hit0/같은hit 같은칸/다른칸/다른hit·같은본체/다른본체·null/새/같은턴/다음턴·색/null·원인4/-1/4·삭제/벽/거리/보호/비활성을 구분한다.
2. 검사부터 작성해 동일 저장 입력/시드 결과를 비교하고 새 계약 누락 RED를 확인한다. 위 정책/공유 값/기존 생성자/필수 조회를 최소 연결하고 true/false 메모리 정책의 같은 카탈로그 조회도 확인한다.
3. Query 상태/비공개 문맥/규칙·전역 난수 무변경과 반응/양/메시지, 실제 피해·턴당/칸별/hit0·제거/미션/효과/연결 철거·예약 투영·전체 MemoryPack 바이트/ID/버전1/50구간 전후 일치를 확인한다. 발전기/색/원인 허용·공급 고철의 기존 경계도 유지한다.
4. 새 검사와 EF-24의 기존17종(ColorMatchPolicyVerification.Run, GeneratorReactionPolicyVerification.Run 및 EF-23 기존15종)을 각각 별도 안전한 Editor에서 실행한다. 정확한 메서드·실제 입력/응답·결과·각 종료0/필수FAIL0·과거 출력 백업/복원 바이트 동일을 남긴다.
5. stage-25-progress.md에 변경/실측/차이/미검증/남은 문제를 저장하고 현재 계획/목표를 갱신한다. 완료 후 실제 결과에 맞춘 다음 한 단계 계획/목표/전체 복사용 명령문만 작성·제시한다. 다음 구현은 시작하지 않는다.

실행 담당: executing-plans. 에이전트는 별도 요청 시에만 사용한다.

## 제외

피해 실행/Apply/Remove/RegisterDamage/RegisterHit/턴 기록·ReservedDamage/예약 투영·Mission·GeneratorRules.Query/Apply/충전/연결/철거, 기존 원인/색/배치 정책·공급/낙하·드론/봇/UI/MVVM/표현/풀·제작/배포/저장/변환은 변경하지 않는다. EF-05/09/11·원본/meta/enum 숫자/기존 작업을 보존한다. 커밋/빌드/재패킹/이미지/팩 재생성/사용자 Editor 종료/씬 저장 금지. 실패를 숨기거나 목표를 축소하지 않는다.

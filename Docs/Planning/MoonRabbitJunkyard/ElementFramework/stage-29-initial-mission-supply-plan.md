# EF-29 — 최초 장애물 미션 수량 정의 연결 계획

상태: 완료. 새47201+기존228623=275824 PASS/0 FAIL, 최종22종 별도 Editor 종료0과 보존 감사 통과. EF-28의 검증된 제거 미션 프로필을 제작 수량 조회에 재사용하는 다음 한 단계다.

실행 담당: superpowers:executing-plans. 에이전트는 별도 요청 시에만 사용한다.

목적: 최초 배치 장애물의 미션 수량과 실제 제거 미션이 같은 정의 매핑을 사용하게 한다.

구조: LevelMissionRules.Supply의 최초 장애물 수량 predicate만 연결한다. 기존 공급 계산과 검증 알고리즘은 유지한다.

환경: ServeredMeridian/Unity6000.3.10f1/9×9/현재 패키지·asmdef.

연결: [가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-28 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-28-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-29-initial-mission-supply-goal.md).

## 근거와 범위

런타임 기여/제거는 RemovalMissionProfile을 읽지만 LevelMissionRules.Supply의 Initial은 아직 MissionKind→ObstacleKind switch다. Validate와 LevelStateBuilder가 Supply를 사용하므로 같은 ID의 제거 미션을 바꾸면 제작 수량 검증과 실행이 달라질 수 있다. 최초 배치 본체 매칭 한 책임만 연결한다. 큰 구간 E 전체나 공급 생성 전환은 아니다.

변경 후보:

- Missions/Rules/LevelMissionRules.cs: Supply의 최초 장애물 매칭 predicate만. Elements using이 필요하면 추가한다.
- Elements/Editor/Tests/InitialMissionSupplyVerification.cs(+meta): Before/Run, 같은ID 실제 수량/검증과 전체 보존 검사.
- 실제 영향이 입증된 기존 테스트 fixture만 최소 조정: DamageRecordPolicyVerification의 임시 집계 정의는 원래 RemovalMissionProfile을 보존하고, RemovalMissionProfileVerification의 조회용 상태 준비는 임시 정의 교체 전에 수행한다. 실제 실패/호출부를 확인한 뒤 필요한 부분만 변경한다. 기존 생성자 계약/누락 오류 기대값을 약하게 만들지 않는다.

## 최소 계약

제거 미션5종(Crate/Scrap/Safe/ColorLock/Appliance)과 내구도형 장애물5종에만 Get→RequireRemovalMissionProfile→Kind 비교를 사용한다. 본체 목록의 항목 수를 세며 내구도·2×2 점유 칸수로 증폭하지 않는다. 발전기/미지원 장애물은 제외하고 프로필을 강제하지 않는다. 비제거/미지원 미션에는 이 필수 조회를 강제하지 않는다. null 장애물 목록의 기존0 반환을 유지한다. 유효 대상의 누락은 ID 포함 오류이며 기본 상자로 대체하지 않는다.

Supply의 Color/Web/Mold/Dust/Recovery 최초 수량, Scrap/Recovery의 Fixed·Maintained·Dynamic·GoalBased·Maximum과 산식/상한·표시문, Validate의 알고리즘/오류코드/경로/메시지, Name은 수정하지 않는다. 기본 등록이면 전체 결과가 같아야 한다. 다른 미션 프로필을 등록했을 때 수량과 그에 따른 검증 판단이 달라지는 것은 의도한 연결 결과로 기록한다.

## 작업과 검증

- [x] work/EF-23~28 미커밋·원본GUID·과거 출력 백업. Supply→Validate→LevelStateBuilder 호출부와 null/미지원 경계를 조사한다. 같은 저장 입력/시드와 전체 결과 기준을 확보한다.
- [x] 검사부터 작성한다. 원래부터 Crate/Scrap 본체와 양쪽 미션을 가진 유효 레벨을 저장하고 같은 ID Crate의 프로필을 Scrap으로 테스트 메모리에서 바꾼다. 실제 Supply의 Initial은 Crate0/Scrap2가 되어야 한다. 기존 종류 조건의 actual1/1 대비 want0/2 RED와 실제 Validate의 부족 공급 판단을 확보한다. 원래 등록 참조는 finally에서 정확히 복원하고 공개 수정 API/문자열 실패를 만들지 않는다.
- [x] 최초 본체 매칭만 연결하고 GREEN. 5종의 전체 내구도/본체0·1·2/2×2·다른종 혼합/미션목표0·1·상한/누락ID/발전기·미지원/null목록을 검사한다. 새 생산 정책/프로필/생성 계약은 만들지 않는다. 기존 fixture가 제작 검증을 거치는 시점을 추적해 필요한 임시 정의의 원래 프로필 참조나 교체 전 상태 준비만 보존한다.
- [x] 같은 입력에서 전체 MissionSupplySummary 필드/문자열과 Validate의 전체 오류 목록을 전후 비교한다. 고정·유지·동적 공급, 비제거 미션, 완료/예약·드론 재선정·발전기·전체 실행 상태/문맥/규칙·전역 난수·전체팩 바이트/본체ID/버전1/50구간도 유지한다. 새 Run과 EF-28 최종21종을 각각 별도 Editor 실제 종료0/필수FAIL0으로 확인한다.
- [x] 과거 결과/값 출력은 백업→삭제→새 출력 생성 확인→증거 복사→finally 전체 바이트 복원. 무시된 Addressables 상태/패키지 서명도 원본 보호 목록에 포함한다. 최종 변경 범위/원본GUID/기존작업/과거 증거 감사와 git diff --check, 보고·다음 한 단계 문서/전체 명령문을 마무리한다. 다음 구현은 시작하지 않는다.

필수 기존21종: RemovalMissionProfileVerification.Run과 [EF-28 계획](stage-28-removal-mission-plan.md)에 명시한20종. 정확한 메서드는 [EF-28 최종 실행 표](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-28-progress.md)와 대조하며 누락/대체하지 않는다. 마지막5종은 Levels.Editor, 나머지는 Elements.Editor다.

## 제외와 검토 초점

RemovalMissionProfile/정의/기본 등록/생성 계약, ObstacleDamageRules/미션 진행/드론 관리자/발전기/공급 규칙·생성/낙하/비행/UI/MVVM/표현/풀/봇/저장/변환/원본 에셋은 변경하지 않는다. EF-05/09/11·GUID/enum 유지. 커밋/푸시/빌드/재패킹/이미지/팩 재생성/사용자Unity종료/씬저장 금지.

검토 초점: 2×2를4본체로 세는 오류, 발전기를 기본 상자로 집계하는 오류, 비제거/미지원 미션에 누락 프로필을 강제하는 오류, 공급량 산식의 주변 변경, 임시 정의의 제작 검증과 런타임 검증 시점 혼동. 위 검사에서 각각 실제 결과로 확인한다.

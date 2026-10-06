# EF-28 — 내구도형 제거 미션 정의 연결 계획

상태: 완료.228623 PASS/0 FAIL,최종21종 종료0. [완료 기록](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-28-progress.md).

실행 담당: superpowers:executing-plans. 에이전트는 별도 요청 시에만 사용한다.

목적: 내구도형5종의 제거 미션 종류를 정의에서 읽어 실제 기여 조회와 제거 완료가 같은 매핑을 사용하게 한다.

구조: 기존 ObstacleDamageRules.Mission 공용 경계만 연결한다. 제거/미션 진행 알고리즘이나 드론 정책은 바꾸지 않는다.

환경: ServeredMeridian, Unity6000.3.10f1,9×9,현재 패키지/asmdef 관례 유지.

연결: [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [가이드](integration-guideline.md) · [EF-27 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-27-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-28-removal-mission-goal.md).

## 근거와 변경 경계

ObstacleDamageRules.Mission은 아직 종류 switch다. Remove의 완료, MissionProgressRules의 직접 장애물 기여와 발전기 연결 대상 기여가 이 메서드를 공유한다. 이5종 매핑 한 책임을 함께 연결한다. LevelMissionRules의 제작 검증·자동 집계는 다른 책임이므로 이번에 바꾸지 않는다.

- 새 Data/ElementRemovalMissionProfile.cs(+meta): 불변 MissionKind Kind. Crate/Scrap/Safe/ColorLock/Appliance만 유효하며 다른 값은 생성 오류다.
- Data/ElementDefinition.cs: 읽기 전용 RemovalMissionProfile과 RequireRemovalMissionProfile, 누락ID 포함 오류,8인자 생성자. 기존2~7인자 생성자는 기존 계약을 보존하고 새 메타데이터는 null로 위임한다.
- Runtime/LegacyElementDefinitions.cs: 내구도형5종에 기존과 같은 미션 프로필 등록. 발전기에는 강제하지 않는다.
- Obstacles/Runtime/ObstacleDamageRules.cs: Mission의 유효5종만 Get→Require→Kind. Generator/미지원의 기존 Crate 반환은 유지한다. Apply/Query/ReservedDamage/Remove는 그대로 둔다.
- Editor/Tests/ElementCatalogVerification.cs: 기존 불변 계약 검사에 새 프로필만 추가. 관련 없는 검사를 완화하지 않는다.
- Editor/Tests/RemovalMissionProfileVerification.cs(+meta): Before/Run과 실제 연결·보존 검사. 기존 검사 재사용 시 원래 입력/출력 소유권을 보존한다.

## 작업 순서와 검증

- [x] 원본/GUID/work/EF-23~27 미커밋과 과거 출력 백업. 실제 Mission 호출부, 직접/발전기 연결 기여와 Apply→Remove→완료 순서를 조사한다. 동일 저장 입력/시드의 전체 상태·문맥·난수·미션/효과·예약과 팩 기준을 확보한다.
- [x] 새 계약 검사를 먼저 작성해 실제 계약 부재 RED를 확보한다. 불변/유효5종/미지원 값/누락ID/기존 생성자 호환을 검증한다. 계약과 기본 등록만 추가하되 Mission switch는 아직 유지한다.
- [x] 다음으로 같은 ID의 다른 제거 미션 프로필을 테스트 메모리에서 등록한다. 원래부터 서로 다른 본체와 양쪽 미션을 가진 유효 레벨을 만든 뒤 등록만 교체해 실제 미션 기여 조회와 실제 타격→제거 완료를 실행한다. 기존 switch가 프로필을 무시하는 동작 RED를 actual/want로 확보한다. 등록 참조는 finally에서 정확히 복원한다. 계약 컴파일 실패만으로 실제 연결 RED를 대신하지 않는다.
- [x] 공용 Mission 경계만 연결한다. 같은ID 교체 시 조회·실제 완료가 함께 새 미션을 사용함을 확인한다. 기본5종은 전체 내구도/다른 본체·같은턴/다음턴·미션완료/이미삭제·발전기 연결/철거·미지원 직접 경계를 유지한다. 새 정의 메타데이터 자체의 의도적 차이는 보고하고 결과에서 임의 삭제하지 않는다.
- [x] 동일 입력/시드의 전체 실행 결과/상태·비공개 문맥·규칙/전역 난수·예약/기여/완료예상/취소/무효화/재선정·MemoryPack 전체 바이트/본체ID/버전1/50구간 동등성을 확인한다. 새 Run과 EF-27 최종20종 각각 별도 Editor 실제 종료0/필수FAIL0 확인. 기존 테스트의 임시 정의 생성자에 새 필수 프로필이 필요한 경우 실제 호출 근거를 조사하고 그 테스트 fixture만 최소 갱신하며 기대값을 낮추지 않는다.
- [x] 과거 출력은 백업→삭제→새 출력 확인→증거 복사→finally 원본 바이트 복원. 최종 원본/GUID/기존 작업 감사와 git diff --check, 결과 보고·다음 한 단계 문서/전체 명령문 준비. 다음 구현은 시작하지 않는다.

필수 기존20종: DamageRecordPolicyVerification.Run, ReservedDamagePolicyVerification.Run, DamageAggregationPolicyVerification.Run, ColorMatchPolicyVerification.Run, GeneratorReactionPolicyVerification.Run, ApplianceDamagePolicyVerification.Run, ColorLockDamagePolicyVerification.Run, CapsuleDamagePolicyVerification.Run, ScrapDamagePolicyVerification.Run, CrateDamagePolicyVerification.Run, GeneratorPlacementVerification.Run, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run(모두 Elements.Editor), FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data, PowerEffectVerification.Data(마지막5종 Levels.Editor).

## 제외와 검토 초점

MissionProgressRules의 알고리즘/완료·Remove/Apply/Query/ReservedDamage/턴 기록·DroneTargetManager/GeneratorRules/제작 미션 집계·낙하/공급/비행/UI/MVVM/표현/풀/봇/저장/변환은 변경하지 않는다. 미션 종류 enum 숫자·원본meta·EF-05/09/11 유지. 커밋/푸시/빌드/재패킹/이미지/팩 재생성/사용자Unity종료/씬저장 금지.

중점 검사: 두 미션 중 잘못된 미션 감소, 조회와 완료 매핑 불일치, 발전기 연결 대상의 기여, 이전 생성자 누락 프로필의 실제 호출 영향, 테스트 교체 참조 미복원. 모두 위 검사에서 실제 상태로 증명한다. 이 단계는 제작 도구·새 드론 선택 정책의 완료를 의미하지 않는다.

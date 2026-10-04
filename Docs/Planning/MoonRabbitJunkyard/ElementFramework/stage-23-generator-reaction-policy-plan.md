# EF-23 — 발전기 반응 원인 허용 조회 연결 계획

상태: 준비 완료, 미실행. EF-22 이후 큰 구간 C의 마지막 기존 장애물 허용 원인 조회 연결.

연결: [가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-22 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-22-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-23-generator-reaction-policy-goal.md).

## 목적과 경계

발전기는 피해/내구도가 아니라 Charge 반응을 사용한다. 기존 내부 ObstacleDamageRules.Query는 GeneratorRules.Query로 먼저 위임하므로 네 원인과 미정의 원인 모두 Charge/AlreadyDamaged다. 외부 MagnetAdjacent는 색 자물쇠 전용 예외에서 None으로 거절된다. 내부 허용과 외부 경계를 혼동하지 않는다.

obstacle.generator에 기존 ElementDamageSourcePolicy의 네 값 true를 한 번 조합한다. Get→Require→Allows 허용 조회에 Generator를 포함하고 GeneratorRules.Query 위임은 허용 조회 뒤, 색/내구도/칸별 피해 검사 전으로 옮긴다. 기존 일반 인접/Power/Hammer 충전과 내부 MagnetAdjacent 허용·외부 거절을 유지한다. 발전기 Durability0을 제거 본체로 재해석하지 않는다. 누락은 ID 포함 오류이고 기본값을 대체하지 않는다.

## 작업 단위와 검증

1. work와 미커밋 변경을 유지한다. 발전기 Query/Apply/Targets/TargetRemoved/ActiveConnections·실제 일반 인접/Power/Hammer·미션/예약/철거 호출부를 조사한다. 필요 충전3~5/현재 충전0~완충 전/점유4칸, 원인4개/-1/4, null/새/같은턴/다음턴 문맥, 출발색/null·벽/거리/비활성/보호와 위임/반응 순서의 실제 전환 전 입력·결과를 저장한다. 삭제 뒤 외부 Empty와 점유를 남긴 내부 Query를 구별하고 원본/GUID/과거 증거를 보호한다.
2. 기존 불변 정책에 네 원인 허용을 한 번 등록하고 위임 위치를 최소 변경해 같은 필수 카탈로그 조회를 연결한다. 정책 누락 ID 오류·다른 메모리 정책의 동일 조회를 확인한다. 기존5종 허용 정책·생성/배치/충전 프로필·색 조건·칸/hit 집계를 유지한다.
3. 동일 저장 입력/시드로 Query 상태/비공개 문맥/규칙·전역 난수 무변경과 실제 충전·같은 본체 턴당1충전·완충 작동/연결 대상 제거·직접 대상 제거 후 연결 해제/무충전 철거·미션/효과/예약·MemoryPack 바이트/ID/버전1/50구간을 전후 비교한다. 연결 대상4종·복수 대상/복수 발전기·벽/와이어 표시 비의존은 기존 안전한 GeneratorVerification 메모리 사례를 재사용해 확인한다. 피해 실행과 충전/철거 실행 소스는 수정하지 않는다.
4. 새 검사와 ApplianceDamagePolicyVerification.Run, ColorLockDamagePolicyVerification.Run, CapsuleDamagePolicyVerification.Run, ScrapDamagePolicyVerification.Run, CrateDamagePolicyVerification.Run, GeneratorPlacementVerification.Run, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data, PowerEffectVerification.Data를 각각 별도 Editor에서 실행한다. 실제 입력/응답·각 종료0·필수 FAIL0·과거 출력 복원을 기록한다.
5. stage-23-progress.md에 변경/실측/보존/미검증/남은 문제를 저장하고 완료 보고한다. 결과에 맞춘 다음 한 단계 계획/목표/전체 복사용 명령문만 작성한다. 다음 구현은 시작하지 않는다.

실행 담당: executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 제외

GeneratorRules.Query/Apply/충전 수치/작동/철거·Apply/Remove/Mission/예약/턴 집계·다른 종류 정책/색 조건·공급/낙하·드론·봇 DTO·UI/MVVM·표현/풀·제작/배포·저장 포맷/변환/팩 재생성은 변경하지 않는다. EF-05/09/11·원본/GUID/enum 숫자/기존 작업을 보존한다. 빌드·재패킹·이미지·임의 커밋·사용자 Unity 종료·씬 저장 금지. 실패를 숨기거나 목표를 축소하지 않는다.

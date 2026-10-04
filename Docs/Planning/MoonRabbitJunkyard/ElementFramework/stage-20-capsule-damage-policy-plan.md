# EF-20 — 고물 회수 캡슐 피해 원인 허용 정책 연결 계획

상태: 완료. 큰 구간 C의 세 번째 읽기 전용 피해 정책 연결. 실측은 EF-20 결과 보고서 참조.

연결: [가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-19 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-19-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-20-capsule-damage-policy-goal.md).

## 목적과 경계

기존 ElementDamageSourcePolicy를 재사용하여 `obstacle.recovery-capsule`의 Power/Hammer 허용·AdjacentMatch/MagnetAdjacent 거절을 정의에 한 번 조합한다. 캡슐1종의 읽기 전용 허용 조회만 연결하며 피해 실행·색 조건·턴 제한·예약을 정책에 넣지 않는다.

기존 종류 이름 ObstacleKind.Safe/MissionKind.Safe와 숫자·저장 ID는 그대로다. 캡슐의 미정의 피해 원인은 상자/고철과 달리 기존 내부 조회에서 거절되므로 기준을 확보하고 그대로 유지한다. 외부 벽/거리/활성/보호와 자석 인접 경계의 우선순위도 유지한다. 발전기의 연결 대상이므로 실제 직접 파괴에 따른 무충전 발전기 철거·연결/미션도 확인한다.

## 작업 단위와 검증

1. EF-19 결과·캡슐 Query/Apply/Remove·미션/예약·발전기 연결 대상 철거 호출부와 기존 작업을 확인한다. 원본/GUID를 보호하고 내구도1~5/네 원인/미정의 원인·동일턴/다음턴/제거·벽/거리/비활성/보호의 실제 전환 전 입력·응답을 저장한다. 외부/내부 조회를 구별한다.
2. 같은 정책을 캡슐 정의에 한 번 준비하고 Get→Require→Allows의 허용 조회만 연결한다. 누락은 ID 포함 오류이며 기본값 대체는 없다. 다른 메모리 정책도 같은 카탈로그 경로에서 조회함을 확인한다. 기존 생성/배치/충전과 상자·고철 정책을 유지한다.
3. 동일 저장 입력/시드의 Query 상태/비공개 문맥/규칙·전역 난수 무변경, 실제 Power/Hammer 피해/제거·미션/효과·예약 피해·연결 대상 파괴/발전기 철거·MemoryPack 바이트/본체 ID·버전1/50구간을 전후 비교한다. 일반/자석 인접 거절과 미정의 원인의 기존 거절 메시지·순서를 유지한다.
4. 별도 Editor에서 새 검사와 ScrapDamagePolicyVerification.Run, CrateDamagePolicyVerification.Run, GeneratorPlacementVerification.Run, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data, PowerEffectVerification.Data를 실행한다. 안전한 메모리 경로를 확인하고 각각 실제 입력·응답/종료0·필수 FAIL0을 기록한다. 과거 증거는 백업·복원한다.
5. stage-20-progress.md에 변경·실측·보존·미검증·남은 문제를 작성하고 완료 보고한다. 결과에 맞춘 다음 한 단계 계획/목표/전체 복사용 명령문만 작성하며 다음 구현은 시작하지 않는다.

실행 담당: executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 제외

색 자물쇠/금속기둥/발전기의 피해 정책, Apply/Remove/Mission/턴 집계/예약 피해량·낙하/공급·발전기 충전/연결 실행·드론·봇 DTO·UI/MVVM·표현/풀·제작/배포·저장 포맷·원본 변환·팩 재생성은 변경하지 않는다. EF-05/09/11·원본/GUID/enum/기존 작업을 보존한다. 빌드·재패킹·이미지·임의 커밋·사용자 Unity 종료·씬 저장 금지. 실패를 숨기거나 범위를 축소하지 않는다.

# EF-19 — 고철 뭉치 피해 원인 허용 정책 연결 계획

상태: 완료. 큰 구간 C의 두 번째 읽기 전용 피해 정책 연결. 실측은 EF-19 결과 보고서 참조.

연결: [가이드라인](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-18 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-18-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-19-scrap-damage-policy-goal.md).

## 목적과 경계

EF-18에서 검증한 ElementDamageSourcePolicy를 고철1종에 재사용한다. `obstacle.scrap` 정의에 일반 인접 매칭·Power·Hammer 허용/MagnetAdjacent 거절 값을 한 번 준비하고 기존 Query의 읽기 전용 허용 조회만 연결한다. 새 정책 종류나 실행 등록표를 만들지 않는다.

고철의 고정 배치와 유지 공급/신규 생성은 같은 조회를 사용해야 한다. 공급 동작·수량·생성 순서·원본 저장은 변경하지 않는다. 본체별 턴당1피해·피해량1·고철 미션·예약 피해·벽/거리/활성/보호 경계는 그대로다. 고철은 발전기 연결 대상으로 지원되지 않으므로 연결 대상 fixture를 만들지 않는다. EF-18의 상자 정책과 비공개 문맥 지문 검사를 유지한다.

## 작업 단위와 검증

1. EF-18 결과와 고철 Query/Apply/Remove·Scrap 유지 공급·미션/예약 호출부, 기존 변경을 확인하고 원본/GUID를 보호한다. 고철 내구도1~5/네 원인/미정의 원인·동일턴/다음턴·제거 본체·벽/거리/비활성/보호와 고정/공급 런타임의 실제 전환 전 입력·응답을 저장한다. 외부 경계와 내부 조회를 구별한다.
2. 같은 불변 정책을 고철 정의에 한 번 조합하고 같은 Get→Require→Allows 경로로 연결한다. 상자 설정과 다른4종·미지원 의미를 유지한다. 누락은 ID 포함 오류이고 기본값 대체는 없다. 같은 메모리 카탈로그에서 정책 값이 조회를 바꾸는지도 확인한다.
3. 동일 저장 입력/시드의 조회·실제 피해/제거·미션·효과·예약 피해·규칙/전역 난수·MemoryPack 바이트/본체 ID·버전1/50구간을 비교한다. Query는 상태·비공개 턴 문맥까지 변경하지 않는다. 고철 공급 전후 결과를 함께 비교하되 공급 구현은 수정하지 않는다.
4. 별도 Editor에서 새 검사, CrateDamagePolicyVerification.Run, GeneratorPlacementVerification.Run, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data, PowerEffectVerification.Data를 실행한다. 진입점의 안전한 메모리 경로를 재확인하고 각 실제 결과/종료0·필수 FAIL0을 기록한다. 과거 검사 출력은 백업·복원한다.
5. stage-19-progress.md에 변경/실측/보존/미검증/남은 문제를 작성하고 완료 보고한다. 결과에 따른 다음 한 단계 계획·목표·전체 복사용 명령문만 작성하며 다음 구현은 시작하지 않는다.

실행 담당: executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 제외

캡슐/색 자물쇠/금속기둥/발전기의 피해 정책, Apply/Remove/Mission/턴 집계/예약 피해량·고철 공급·낙하·발전기 충전/연결·드론·봇 DTO·UI/MVVM·표현/풀·제작/배포·저장 포맷·원본 변환·팩 재생성은 변경하지 않는다. EF-05/09/11·기존 enum/GUID/작업을 보존한다. 빌드·재패킹·이미지 작업·임의 커밋·사용자 Unity 종료·씬 저장 금지. 실패를 숨기거나 범위를 축소하지 않는다.

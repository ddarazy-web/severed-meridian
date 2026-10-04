# EF-18 — 나무상자 피해 원인 허용 정책 연결 계획

상태: 완료. 큰 구간 C의 첫 읽기 전용 피해 정책 연결. 실측은 EF-18 결과 보고서 참조.

연결: [가이드라인](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-17 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-17-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-18-crate-damage-policy-goal.md).

## 목적과 경계

기존6종의 배치 정의 연결을 확보한 뒤 실행 조회로 한 경계만 전환한다. 나무상자의 피해 원인 허용 여부를 작은 불변 정책으로 정의에 조합하고 기존 읽기 전용 피해 조회에서 이 정책을 읽는다. 실제 피해 적용/제거/미션·턴 집계·피해량은 변경하지 않는다. 전체 피해 규칙/행동 등록표를 한 번에 교체하지 않는다.

현재 상자는 일반 인접 매칭·Power·Hammer 피해를 허용하고 MagnetAdjacent는 허용하지 않는다. 벽·비활성·거리·내용물/덮개 판정은 DamageReaction의 기존 바깥 경계에 남긴다. 본체별 턴당1피해·양1·제거/연결 철거·미션·예약 피해 계산은 그대로 둔다. 타입명과 원인 표현 방식은 실제 코드의 책임과 검증 결과에 따라 정하며 표시/저장/패키지 계층을 만들지 않는다.

## 작업 단위와 검증

1. EF-17 결과와 실제 DamageReaction/ObstacleDamageRules.Query·Apply·MissionProgressRules/ReservedDamage 호출부를 읽고 기존 작업/원본/GUID를 보호한다. 상자 내구도1~6/네 피해 원인/동일턴 반복·다음턴·인접벽/비활성·거리/보호·제거 본체 결과를 전환 전 메모리 입력으로 확보한다. 외부 경계와 내부 조회의 직접 호출 의미를 구분한다.
2. 허용 원인만 보유하는 작은 불변 피해 정책을 정의에 조합한다. 기존2/3/4인자 생성 계약·배치/충전 프로필은 보존한다. crate ID에 한 번 준비하고 같은 카탈로그의 다른 메모리 정책으로 허용 여부 조회가 달라짐을 확인한다. 정책 누락은 ID 포함 오류로 거절하며 기본 허용 정책을 대체하지 않는다.
3. 상자의 읽기 전용 원인 허용 조회만 연결한다. 다른5종·미지원 의미/외부 피해 경계/반응 enum·양·메시지·턴 제한/적용 경로를 유지한다. Query는 런타임/턴 문맥/난수를 변경하지 않는다. 허용과 실제 적용을 중복 수행하지 않는다.
4. 같은 입력과 시드의 실제 피해/제거/미션·발전기 연결 철거·규칙/전역 난수·상태/효과/예약 피해량·저장 바이트/배치 ID·버전1/50구간 결과를 전후 비교한다. 구조상 필요한 상자1종 경계 외의 주변 정리는 하지 않는다.
5. 별도 Editor에서 새 검사와 안전한 기존 GeneratorPlacementVerification.Run, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data를 실행한다. 상자 피해를 직접 다루는 PowerEffectVerification.Data도 안전한 메모리 경로인지 확인 후 실행한다. 각 종료0/필수 FAIL0과 실제 입력/반응/상태를 기록하고 과거 증거를 보존한다.
6. stage-18-progress.md와 완료 보고를 작성한다. 결과에 따른 다음 한 단계 계획·목표·전체 복사용 명령문만 작성하고 다음 구현은 시작하지 않는다.

실행 담당: executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 제외

다른 종류 피해 정책·피해 적용/제거/미션/턴 집계/예약 피해량/낙하/공급·발전기 충전·드론·봇 DTO·UI/MVVM·표현/풀·제작/배포 에셋·저장 포맷·원본 변환·팩 재생성은 전환하지 않는다. EF-05/09/11·원본/GUID/enum 숫자/기존 변경을 보존한다. 빌드·재패킹·이미지 작업·임의 커밋·사용자 Unity 종료·씬 저장 금지. 필수 실패를 숨기거나 목표를 축소하지 않는다.

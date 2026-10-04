# EF-21 — 색 자물쇠 피해 원인 허용 정책 연결 계획

상태: 완료. EF-21 검증 보고에 실측/보존/미검증을 기록했다. 큰 구간 C의 네 번째 읽기 전용 피해 정책 연결.

연결: [가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-20 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-20-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-21-color-lock-damage-policy-goal.md).

## 목적과 경계

기존 ElementDamageSourcePolicy를 재사용해 `obstacle.color-lock`의 네 원인 허용을 정의에 한 번 조합하고 읽기 전용 조회만 연결한다. 실제 인접 피해에는 별도의 기존 색 일치 조건이 필요하다. 이 조건·턴 제한·피해량을 허용 원인 정책에 합치지 않는다.

Power/Hammer는 출발색과 무관하고 AdjacentMatch/MagnetAdjacent는 색 일치를 검사한다. 미정의 원인도 기존 내부 조회에서는 색 조건을 검사한다. 내부 null 색과 외부 Evaluate의 sourceColor null→출발 칸 색 대체는 의미가 다르므로 구별한다. 색 자물쇠만 허용하는 외부 자석 인접 예외·벽/거리/비활성/보호 우선순위를 유지한다. 기존3종의 허용 정책·원인별 미정의 의미를 바꾸지 않는다.

## 작업 단위와 검증

1. EF-20 결과·색 자물쇠 Query/Apply/Remove·일반/자석 인접 호출부·미션/예약·발전기 연결 대상 철거와 기존 변경을 확인한다. 원본/GUID를 보호하고 내구도1~3·본체색Type1~5/출발색 일치·불일치·null, 네 원인/미정의 원인·동일턴/다음턴/제거·벽/거리/비활성/보호의 전환 전 실제 입력·응답을 저장한다. null 직접 조회와 외부 출발색 대체를 구분한다.
2. 같은 불변 정책을 색 자물쇠 정의에 한 번 등록하고 Get→Require→Allows를 연결한다. 누락은 ID 오류이고 기본값 대체는 없다. 다른 메모리 정책의 같은 카탈로그 조회를 확인한다. 색 일치 규칙은 기존 조회에 남기며 정책 타입/생성자/배치/충전·기존3종은 유지한다.
3. 동일 저장 입력/시드의 Query 상태/비공개 문맥/난수 무변경과 실제 일반 매칭·자석 인접·Power/Hammer 피해/제거·미션/효과·예약·연결 대상 파괴/발전기 철거·MemoryPack 바이트/본체 ID·버전1/50구간을 전후 비교한다. 반응/양/메시지와 색 조건/턴 제한을 보존하고 피해 적용 코드는 수정하지 않는다.
4. 별도 Editor에서 새 검사와 CapsuleDamagePolicyVerification.Run, ScrapDamagePolicyVerification.Run, CrateDamagePolicyVerification.Run, GeneratorPlacementVerification.Run, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data, PowerEffectVerification.Data를 실행한다. 안전한 메모리 경로와 각 실제 입력·응답/종료0·필수 FAIL0, 과거 출력 백업·복원을 기록한다.
5. stage-21-progress.md에 변경·실측·보존·미검증·남은 문제를 저장하고 완료 보고한다. 결과에 맞춘 다음 한 단계 계획/목표/전체 복사용 명령문만 작성하고 다음 구현은 시작하지 않는다.

실행 담당: executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 제외

금속기둥/발전기 피해 정책, 색 조건 자체의 정책 전환·Apply/Remove/Mission/턴 집계/예약 피해량·낙하/공급·발전기 실행·드론·봇 DTO·UI/MVVM·표현/풀·제작/배포·저장 포맷·원본 변환·팩 재생성은 변경하지 않는다. EF-05/09/11·원본/GUID/enum/기존 작업을 보존한다. 빌드·재패킹·이미지·임의 커밋·사용자 Unity 종료·씬 저장 금지. 실패를 숨기거나 목표를 축소하지 않는다.

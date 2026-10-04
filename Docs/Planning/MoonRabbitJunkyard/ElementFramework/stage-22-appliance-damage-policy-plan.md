# EF-22 — 금속기둥 상자 피해 원인 허용 정책 연결 계획

상태: 완료. EF-22 검증 보고에 실측/보존/미검증을 기록했다. EF-21에 의존하는 큰 구간 C의 다섯 번째 읽기 전용 피해 정책 연결.

연결: [통합 가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-21 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-21-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-22-appliance-damage-policy-goal.md).

## 목적과 경계

기존 ElementDamageSourcePolicy를 obstacle.metal-rod-box에 한 번 조합한다. AdjacentMatch/Power/Hammer 허용·MagnetAdjacent 거절을 같은 Get→Require→Allows 경로에 연결한다. 허용 원인 조회만 전환하며 2×2 점유 칸별 타격 집계나 피해량 정책 전환은 하지 않는다. 본체별 턴당1피해로 바꾸지 않는다.

현재 금속기둥은 같은 hit의 같은 칸만 중복 피해를 거절하고 다른 칸/다른 hit에는 다시 피해를 받는다. 미정의 원인은 이 기존 칸별 의미를 유지한다. 정책 누락은 ID 포함 오류이고 기본값 대체는 없다. 색 조건이 있는 자물쇠와 충전형 발전기의 실행 의미도 유지한다.

## 작업 단위와 검증

1. work와 미커밋 변경을 유지하고 Query/Apply/Remove·일반 인접/범위/미션/예약·연결 대상 철거 호출부를 조사한다. 내구도1~9/점유4칸/원인4개와 -1/4, hit0/같은hit 같은칸/같은hit 다른칸/다른hit/다음턴/null 문맥 및 벽/거리/비활성/보호/제거의 실제 전환 전 입력·결과를 저장한다. 원본/GUID와 기존 증거를 보호한다.
2. 불변 정책을 한 번 등록하고 기존 필수 허용 조회에 Appliance만 포함한다. 정책 누락 ID 오류·다른 메모리 조합의 동일 카탈로그 조회를 확인한다. 생성/배치/충전·기존4종 피해 정책·발전기 계약을 유지한다.
3. 동일 저장 입력/시드의 Query 상태·비공개 문맥·규칙/전역 난수 무변경과 실제 일반 매칭/Power/Hammer·제거/미션/효과·예약0/1/2/4칸·발전기 철거·MemoryPack 바이트/ID/버전1/50구간을 전후 비교한다. 로켓의 두 점유 칸·폭탄의 중첩1~4칸·조합 확대 범위·자석+자석4칸 피해/내구도 하한을 실제 경로로 검증한다. 중첩 검사는 기존 OverlapData의 안전한 메모리 헬퍼와 증거 소유권부터 확인해 새 검사에서 비교하거나 별도 실행한다.
4. 새 검사와 ColorLockDamagePolicyVerification.Run, CapsuleDamagePolicyVerification.Run, ScrapDamagePolicyVerification.Run, CrateDamagePolicyVerification.Run, GeneratorPlacementVerification.Run, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data, PowerEffectVerification.Data를 각각 별도 Editor에서 실행한다. 실제 입력/응답·종료0·필수 FAIL0과 과거 출력 백업/복원 바이트 동일을 기록한다.
5. stage-22-progress.md에 변경·실측·보존·미검증·남은 문제를 저장하고 완료 보고한다. 결과에 맞춘 다음 한 단계 계획/목표/전체 복사용 명령문만 작성하고 다음 구현은 시작하지 않는다.

실행 담당: executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 제외

칸별 집계·Apply/Remove/Mission/예약 피해량·발전기 피해/충전 정책·다른 종류/색 조건 정책·공급/낙하·드론·봇 DTO·UI/MVVM·표현/풀·제작/배포·저장 포맷/원본 변환/팩 재생성은 변경하지 않는다. EF-05/09/11·원본/GUID/enum 숫자/기존 작업을 보존한다. 빌드·재패킹·이미지·임의 커밋·사용자 Unity 종료·씬 저장 금지. 실패를 숨기거나 목표를 축소하지 않는다.

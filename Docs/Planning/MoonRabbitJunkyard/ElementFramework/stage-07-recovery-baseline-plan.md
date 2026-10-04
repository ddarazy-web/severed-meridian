# EF-07 — 회수 부품 수집·이동·공급 기준 확보 계획

상태: **완료**, 2026-10-04. 148 PASS / 0 FAIL, 실제 관찰16건.

목표: 정의/행동 이관 전 회수 부품의 비파괴·이동·출구 수집과 남은 미션 기반 보충 공급의 현재 계약을 확보한다.

연결: [가이드라인](integration-guideline.md) · [EF-06 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-06-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-07-recovery-baseline-goal.md).

실행 담당: superpowers:executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 작은 작업 단위

1. 보호 파일/기존 변경·Editor 상태를 기록한다. RecoveryRules, SettlementResolution의 수집·이동/공급, LevelRuntimeState·미션과 실제 호출부를 읽는다. RecoveryVerification.Data/Edges와 안전한 메모리 helper를 확인한다. UI/Start/Restart/Regression·에셋 저장/삭제 경로는 포함하지 않는다.
2. 기존 Data의 DataChecks/TargetChecks와 Edges의 MovementChecks/SwapChecks/SupplyEdges 및 내부 호출을 재사용한다. 초기 출구 수집·낙하/통로/벽/합류·교환·직접 파워 비파괴·출구 수집 미션 단일 집계와 원본 보존을 대응표로 정리한다.
3. 고정 목록 순서/소진, 유지 공급의 min(설정 목표, 남은 미션)-보드 수량, 완료 후 보충 중단·한 턴 수집 순서, 일반/고철 혼합 공급 및 실패 원자성을 확인한다. 드론의 길 열기 기여를 직접 수집 완료와 혼동하지 않는다. 기존 검사에 없는 핵심 사례와 실제 값 기록만 보완한다.
4. 필요한 경우 Missions/Editor/Tests/RecoveryVerification.Baseline.cs/meta만 추가한다. 시드·메모리 레벨·행동/이동/수집/공급 순서·좌표·실제 미션/보드 수량/필요량/커서/난수/RecoveryRecord를 기록한다. 별도 안전한 Editor 검사 종료 코드와 새 결과를 확인한다.
5. 해시/diff/GUID/문서 링크를 검사하고 stage-07-progress.md에 결과·변경·미검증·문제를 보고한다. 완료 결과에 맞춰 EF-08의 가장 작은 선행 작업(기본 후보: 저장·50레벨 팩 호환 기준)을 정해 계획·목표·복사용 실행문을 만든다. EF-08 구현은 시작하지 않는다.

## 제약·위험

생산 규칙·원본 에셋·저장 포맷·enum/meta GUID·현재 패키지와 기존 작업을 보존한다. EF-05 승인 원복 수정과 EF-06 기준을 유지한다. 빌드·커밋·사용자 Unity 종료·씬 저장 금지. 새 구조/드론/UI/팩 전환 제외. 필수 실패가 있으면 숨기지 않고 원인과 수정 경계를 보고하며 생산 수정까지 임의 확대하지 않는다.

위험: 수집 좌표 중복 집계, 출구를 단순 파괴 대상으로 취급, 원격 통로·합류 우선순위를 잘못 예측, 공급 목표와 남은 미션을 혼동한 과잉 보충, 실패 뒤 커서/난수/수집 기록 잔류. 저장·표현·봇 전체 경계는 별도 단계로 분리한다.

실제 결과: [EF-07 완료 보고](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-07-progress.md). 기존 Data/Edges 재사용, 신규 Baseline만 추가. 생산 코드·기존 파일 보존. 초안 fixture/기대 가정 실패는 보고에 기록했고 최종 검사는 통과했다.

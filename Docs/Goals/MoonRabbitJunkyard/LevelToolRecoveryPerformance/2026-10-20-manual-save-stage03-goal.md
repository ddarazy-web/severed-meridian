# 수동 저장 전환 3단계 — 목표와 실행문

상태: 3단계 측정·회귀 검증 완료(2026-10-20). 편집 100ms 목표 미달과 미검증 항목은 결과에 명시했다.

기준: [3단계 계획](../../../Planning/MoonRabbitJunkyard/LevelToolRecoveryPerformance/2026-10-20-manual-save-stage03-plan.md).

## 완료 조건

- [x] 동일한 38문서 fixture의 정상 표본 3회에서 편집·Undo/Redo·유휴·수동 JSON 저장·종료 비용을 분리해 기록한다.
- [x] 자동 checkpoint 쓰기, 전체 이력 Export와 종료 복구 Flush가 0회임을 확인한다.
- [x] 입력 응답·메모리 수치 및 목표 미달을 숨기지 않고 보고한다. 안전 한도 초과 표본은 성공으로 계산하지 않는다.
- [x] 저장 성공/실패·입력 보호·전환·Undo/Redo·공유 사본 회귀를 확인한다.
- [x] JSON/ZIP/MemoryPack/게임 시험의 데이터 전달 회귀를 확인한다.
- [x] 과거 복구본 명시적 열기와 사본 저장, 기존 파일 보존을 확인한다.
- [x] 관련 컴파일, 보호 해시, 검사 연결 해제, 최종 diff를 확인한다.
- [x] 검증 결과와 미검증 항목, 남은 병목 및 복사 가능한 커밋 메시지를 보고한다.

## 목표 실행 명령문

```text
Docs/Planning/MoonRabbitJunkyard/LevelToolRecoveryPerformance/2026-10-20-manual-save-stage03-plan.md와 Docs/Goals/MoonRabbitJunkyard/LevelToolRecoveryPerformance/2026-10-20-manual-save-stage03-goal.md를 기준으로 수동 저장 전환 3단계를 진행해.

먼저 2단계 완료 기록을 확인하고, 미완료 항목이 있으면 임의로 완료 처리하지 말고 보고해. 같은 38문서 fixture에서 편집·Undo/Redo·유휴·수동 JSON 저장·종료를 분리해 정상 표본 3회 측정하고, 자동 checkpoint 쓰기와 전체 이력 Export가 없음을 확인해. JSON·ZIP·MemoryPack·게임 시험 및 입력 보호 회귀도 검사해. 목표 미달과 실제 기기 미검증은 구분해 보고해.

기존 변경과 사용자 데이터를 보존하고 격리된 폴더에서 검사해. 제품 빌드·커밋·푸시·HTML 갱신·코드로 프리팹/씬 편집은 하지 마. 하위 에이전트는 별도 승인 없이 사용하지 마. 질문은 제한시간 없는 문답으로 해. 완료 후 측정 결과·검증·남은 병목과 복사 가능한 커밋 메시지를 제시하고, 추가 리팩토링은 자동 착수하지 마.
```

완료 근거: [검증·측정 결과](../../../Verification/MoonRabbitJunkyard/2026-10-20-level-tool-manual-save-stage03.md). 정상 3회 측정과 회귀를 완료했으며 성능 목표 달성을 의미하지 않는다. 후속 최적화는 별도 작업이다.

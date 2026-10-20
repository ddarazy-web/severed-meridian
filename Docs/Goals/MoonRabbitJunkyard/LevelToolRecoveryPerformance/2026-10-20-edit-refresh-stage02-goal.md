# 편집 성능 개선 2단계 — 목표와 실행문

상태: 2단계 완료(2026-10-20). 정상 3회 이름 편집 중앙값 62~65ms, 자동 파일 쓰기 0회. [2단계 계획](../../../Planning/MoonRabbitJunkyard/LevelToolRecoveryPerformance/2026-10-20-edit-refresh-stage02-plan.md), [검증 결과](../../../Verification/MoonRabbitJunkyard/2026-10-20-level-tool-edit-refresh-stage02.md). 아래 실행문은 이 단계의 기록이며, 다음 작업은 [3단계 실행문](2026-10-20-edit-refresh-stage03-goal.md)을 사용한다.

## 목표·완료 조건

- [x] 단일 레벨 편집에서 전체 문서의 깊은 초안 복사를 제거한다.
- [x] 무관한 문서의 불변 객체·버전은 유지하고 외부 참조와 게시 문서를 격리한다.
- [x] 콜백 예외 시 문서·선택·Undo/Redo가 원자적으로 유지된다.
- [x] 무변경 편집에는 Undo가 생기지 않으며, 저장 당시 값으로 돌아오면 dirty가 해제된다.
- [x] 다중 문서·추가/삭제·공유 적용의 의미를 유지한다. 임의 Apply 호환 경로를 남겼다면 위치와 이유를 보고한다.
- [x] 동일 38문서 정상 3회에서 데이터 편집 비용과 전체 응답 개선을 확인한다. 복사 대상 수로 범위 축소도 증명한다.
- [x] 저장·공유 사본·복구·JSON/ZIP/MemoryPack·게임 시험·화면 갱신 회귀를 확인한다.
- [x] 자동 프로젝트/복구 기록 0회, 사용자 데이터 보존, 검사 연결 해제와 최종 diff를 확인한다.
- [x] 최종 100ms 목표 달성 여부를 별도 보고하고, 3단계 문서·목표 실행문·간략한 커밋 메시지를 제시한다.

## 목표 실행 명령문

```text
Docs/Planning/MoonRabbitJunkyard/LevelToolRecoveryPerformance/2026-10-20-edit-refresh-stage02-plan.md와 Docs/Goals/MoonRabbitJunkyard/LevelToolRecoveryPerformance/2026-10-20-edit-refresh-stage02-goal.md를 기준으로 성능 개선 2단계 ‘변경 문서만 복사’를 진행해.

1단계 최종 검증 기록과 현재 작업 트리의 AuthoringEditSession 및 AuthoringTargetedEditVerification 초안을 먼저 확인하고 이어서 진행해. 단일 레벨 편집의 전체 문서 초안 복사를 제거하고, 변경 대상만 복사하도록 해. 외부 참조 격리·다중 문서 원자성·Undo/Redo·dirty·공유 사본과 화면 갱신을 보존해. 임의 Apply 호환 경로를 남기면 이유를 보고해.

같은 38문서 입력의 정상 3회로 데이터 편집과 전체 응답을 비교하고 관련 회귀를 검증해. 100ms 목표 미달과 실기기 미검증을 구분해 보고해.

기존 변경과 사용자 데이터를 보존해. 제품 빌드·커밋·푸시·HTML 갱신·코드로 씬/프리팹 편집은 하지 마. 하위 에이전트는 별도 승인 없이 사용하지 마. 질문은 제한시간 없는 문답으로 해.

완료 후 결과·남은 항목, 3단계 계획서·목표 문서·복사 가능한 실행문과 간략한 커밋 메시지를 제시하고 다음 단계는 자동으로 시작하지 마.
```

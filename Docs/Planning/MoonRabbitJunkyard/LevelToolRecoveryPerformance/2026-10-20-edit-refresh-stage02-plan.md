# 편집 성능 개선 2단계 — 변경 문서만 복사

> 실행 담당: `superpowers:executing-plans`로 구현·검증한다. 사용자 별도 승인 없이 하위 에이전트·커밋·푸시를 사용하지 않는다.

**Goal:** 단일 레벨 편집에서 전체 문서 초안 복사를 제거하면서 저장·실행 취소·참조 격리를 보존한다.

**Architecture:** 기존 불변 문서와 이력 공유를 유지하고, 편집할 문서 ID가 알려진 경로부터 대상 문서만 복사한다. 여러 문서를 바꾸는 작업은 한 번에 검증·게시하여 하나의 Undo 단위로 유지한다. 임의 변경 콜백의 기존 Apply는 필요한 호환 경로로 남긴다.

**Tech Stack:** C#, Newtonsoft JSON, AuthoringEditSession, 기존 Unity 검사 harness.

**Spec:** [통합 계획](2026-10-20-edit-refresh-performance-plan.md), [2단계 목표](../../../Goals/MoonRabbitJunkyard/LevelToolRecoveryPerformance/2026-10-20-edit-refresh-stage02-goal.md).

상태: 구현·회귀·측정 완료(2026-10-20). [검증 결과](../../../Verification/MoonRabbitJunkyard/2026-10-20-level-tool-edit-refresh-stage02.md). 이름 편집 중앙값 62~65ms로 목표를 충족했다. [3단계 계획](2026-10-20-edit-refresh-stage03-plan.md)은 별도 실행 지시 후 진행한다.

## 착수 당시 출발점

`AuthoringEditSession.cs`에 `ApplyToDocuments`와 복사 횟수 계측 초안, `Tests/Editor/Features/LevelAuthoring/AuthoringTargetedEditVerification.cs` 및 meta가 있었다. 파일 존재를 완료 근거로 삼지 않고 다음 검사부터 실행하여 동작을 판정했다.

```powershell
& Tools/Testing/ProjectTests.ps1 -Action Run -Method LevelAuthoring.Editor.AuthoringTargetedEditVerification.Run
```

검사가 성공해도 대상이 알려진 호출부 전환, UI·저장·데이터 전달 회귀, 정상 3회 측정이 남는다. 단일 대상 편집의 깊은 복사 수는 초안·게시 격리를 위한 2회로 제한하고 무관한 문서를 100개 추가해도 증가하지 않아야 한다. 얕은 사전 복사와 문서 순회 등 남는 O(N) 비용은 별도 보고한다.

## 배경과 경계

1단계 최종 정상 3회에서 편집 중앙값은 221~227ms, 명령·화면 처리는 57~58ms로 감소했다. 데이터 편집은 약 165ms이며 전체 문서 초안 복사와 dirty 비교가 남아 있다. 기준값·미검증·기존 화면 문제는 [1단계 검증 기록](../../../Verification/MoonRabbitJunkyard/2026-10-20-level-tool-edit-refresh-stage01.md)을 확인한다.

이번 단계는 데이터 편집 비용에 집중한다. JSON/ZIP/MemoryPack 형식, 게임 규칙, 수동 저장 원칙, UI 프레임워크를 변경하지 않는다. 제품 빌드·커밋·푸시·HTML 갱신·코드로 씬/프리팹 편집을 하지 않는다. 기존 사용자 데이터와 작업 트리를 보존한다.

## 작업 1 — 편집 대상과 원자성 계약 고정

대상 파일:

- `Assets/Scripts/Features/LevelAuthoring/Editing/AuthoringEditSession.cs`: EditLevel/Apply, dirty, 저장 기준 상태.
- `Assets/Scripts/Features/LevelAuthoring/Editing/AuthoringEditSession.State.cs`: Fork/복구/이력 공유.
- `Assets/Scripts/Features/LevelAuthoring/Runtime/SharedTutorialDraft.cs`: 공유 원본 적용.
- `Tests/Editor/Features/LevelAuthoring/LevelToolEditingBoundaryVerification.cs`: 기존 편집 경계.
- 신규 검사 `Tests/Editor/Features/LevelAuthoring/AuthoringTargetedEditVerification.cs`: 대상 편집, 격리, 예외, 버전 유지.

- [x] Apply와 EditLevel의 모든 호출부를 검색하여 단일 문서·여러 문서·추가/삭제·선택 전환으로 분류한다.
- [x] 단일 편집이 무관한 문서까지 복사하는 재현 근거를 확보한다. 타이밍만으로 복사 제거를 판정하지 않는다.
- [x] 콜백 예외, 외부에 보관한 초안의 후속 변경, 중첩 JSON 변경, 무변경, 원래 값 복귀, Undo/Redo, Fork 독립성 검사를 먼저 확보한다.

## 작업 2 — 대상 지정 편집과 dirty 비용 개선

기존 `EditLevel(string, Action<JObject>)`의 외부 계약을 유지한다. 내부에서 선택 레벨만 편집 사본으로 준비하고, 검증을 통과한 변경을 한 번 게시한다. 대상 지정 API가 필요하면 기존 API와 별도 이름을 사용해 임의 Apply 호출부의 의미를 바꾸지 않는다.

- [x] 단일 레벨 편집은 해당 문서만 복사한다. 무관한 문서와 과거 이력의 불변 객체를 공유한다.
- [x] 변경 초안은 게시할 때 외부 참조에서 격리한다. 복사 횟수를 줄이려고 콜백의 JObject를 그대로 저장하지 않는다.
- [x] 다중 문서 작업은 명시한 대상만 편집하게 하고 전체 변경이 성공했을 때만 하나의 Undo를 생성한다. 추가·삭제·선택 레벨 삭제 금지 검증을 유지한다.
- [x] 대상이 명확한 호출부부터 전환한다. 기존 임의 Apply는 호환 경로로 유지하되 남긴 호출부와 이유를 기록한다.
- [x] dirty 비교는 동일 불변 문서 참조를 우선 건너뛰고 필요한 내용만 비교한다. 저장 당시 값으로 돌아오면 해제되고, project의 세대별 documents 경로는 기존처럼 제외한다.
- [x] 버전 식별자는 실제 문서 변경에만 달라진다. 새 이력 구조나 영구 버전 저장 형식을 도입하지 않는다.

단일 편집을 빠르게 만들기 위해 전체 검증을 매 입력마다 추가하지 않는다. 기존 편집 시 검증과 저장/시험 시 전체 검증 경계를 보존한다. API 전환 범위가 커지면 전체 구조 재작성 대신 아직 전환하지 않은 호환 경로를 명확히 보고한다.

## 작업 3 — 성능·회귀와 다음 인계

- [x] 기존 38문서 고정 입력에서 정상 3회 비교한다. 데이터 편집·명령/화면·전체 응답을 분리하고 복사 대상 수와 최대 지연을 기록한다.
- [x] 무관한 문서 수를 늘린 격리 입력에서도 단일 문서 초안 복사 수가 증가하지 않는지 확인한다. 사전 복제 등 남는 O(N) 비용과 JSON 깊은 복사를 구분한다.
- [x] 저장 성공/실패, 같은 내용 Undo, 새 분기 Redo 폐기, 공유 사본 적용/취소, 복구본 읽기·사본 저장을 검사한다.
- [x] JSON/ZIP/MemoryPack/게임 시험 전달과 1단계 화면 캐시 갱신 회귀를 확인한다.
- [x] 기존 30초·8GiB 측정 보호와 자동 파일 쓰기 0회 조건을 유지한다. 지원되지 않는 누적 할당량을 0으로 보고하지 않는다.
- [x] 결과·미검증·남은 비용을 기록하고 3단계 검증 계획·목표·복사 가능한 실행문과 실제 변경에 맞는 커밋 메시지를 제공한다. 3단계 구현/검사는 자동 착수하지 않는다.

검사는 `Tools/Testing/ProjectTests.ps1`의 `Compile`/`Run`으로 순차 실행한다. 기존 또는 추가한 실제 검사 진입점을 확인하고 호출한다. 100ms 목표에 미달하면 원인과 수치를 보고하며 목표를 낮춰 통과 처리하지 않는다.

## 검토 중점

- 실패한 다중 편집의 부분 반영: 예외 전후 문서·선택·이력 비교.
- 콜백이 보관한 참조의 오염: 게시 후 초안 수정과 Undo 상태 검사.
- 값이 같은 새 객체의 불필요한 Undo/dirty: 동일 내용 편집 검사.
- 저장 후 Undo/Redo 및 Fork의 기준 상태: 저장·사본 분기 왕복 검사.
- 화면 버전과 데이터 변경 불일치: 1단계 갱신 회귀 및 신규 문서 버전 검사.

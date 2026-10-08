# 게임·제작 도구 분리 3단계 — 복사용 목표 실행문

> 후속 실행문 / 자동 착수하지 않음. 2단계 완료 보고 확인 후 사용한다.

[계획서](../../Planning/project-wide/game-authoring-stage-03-plan.md) · [목표](../../Goals/project-wide/game-authoring-stage-03-goal.md)

```text
ServeredMeridian의 게임·제작 도구 분리 3단계를 진행해.

Docs/Planning/project-wide/game-authoring-stage-03-plan.md와
Docs/Goals/project-wide/game-authoring-stage-03-goal.md를 읽고 구현·검증해.
2단계 검증 기록에서 미완료 항목이 있으면 먼저 해결해.

기존 Unity 레벨 에디터에 명시적으로 선택하는 JSON 작업 모드를 연결해.
문서·선택·미저장·Undo/Redo는 공통 편집 세션이 관리하고,
배치·교체·이동·복제·미션·공급·튜토리얼·샘플·모양 편집을 연결해.
공유 flowId와 레벨별 bindings, 안정 ID와 완료 기록 의미를 보존해.
현재 편집한 사본을 PuzzlePlayRequest/CreateTest로 실제 시험하고
복귀 시 선택·미저장·이력을 유지해. 기존 SO 모드도 보존해.

공통 레벨툴 씬은 4단계 시범, 5단계 기능 동등성 확인 후
Unity에서도 기본 편집 화면으로 사용할 방향을 유지해.
이번에 씬 이식이나 전체 JSON 원본 정식화를 앞당기지 마.

필요하면 하위 에이전트를 사용해.
커밋·푸시·Player/Addressables 빌드는 하지 마.
HTML 매뉴얼은 요청 전까지 갱신하지 마.
질문은 충분히 설명하고 제한시간 없는 일반 채팅 문답으로 해.

완료 후 변경·검증·남은 제약을 보고하고
4단계 계획서·목표 문서·복사 가능한 실행문을 작성해.
다음 단계 구현은 자동 시작하지 마.
실제 작업과 검증을 모두 완료한 마지막 보고에서만
완료한 변경에 맞는 커밋 메시지를 복사 가능한 코드 블록으로 제공해.
```

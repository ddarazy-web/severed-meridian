# 게임·제작 도구 분리 4단계 — 복사용 실행문

[계획서](../../Planning/project-wide/game-authoring-stage-04-plan.md) · [목표](../../Goals/project-wide/game-authoring-stage-04-goal.md)

```text
ServeredMeridian의 게임·제작 도구 분리 4단계를 진행해.
Docs/Planning/project-wide/game-authoring-stage-04-plan.md와
Docs/Goals/project-wide/game-authoring-stage-04-goal.md를 읽고 구현·검증해.
3단계 검증 기록에 미완료가 있으면 먼저 해결해.

공통 레벨툴 씬에서 JSON 열기·기본 편집·실제 퍼즐 시험·복귀·저장을 구현해.
기존 공통 편집 세션과 연산을 재사용하고 런타임 코드에 Editor API를 넣지 마.
기존 레벨 편집 메뉴에서 툴 씬으로 들어갈 수 있게 하되 기존 창은 유지해.
기능 동등성 확인 후 같은 씬을 Unity의 기본 제작 화면으로 쓰는 방향을 유지해.
미저장 내용·선택·Undo/Redo와 정식 게임 저장을 보호해.

필요하면 하위 에이전트를 사용해.
커밋·푸시·Player/Addressables 빌드를 하지 마.
HTML 매뉴얼은 요청 전까지 갱신하지 마.
질문은 제한시간 없는 일반 채팅 문답으로 해.
완료 후 변경·검증·제약을 보고하고 5단계 계획서·목표·복사용 실행문을 작성해.
다음 단계 구현은 자동 시작하지 마.
커밋 메시지는 실제 작업과 검증을 모두 끝낸 마지막 보고에서 코드 블록으로 제공해.
```

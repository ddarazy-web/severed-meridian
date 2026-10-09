# 레벨툴 UI 개선 1단계 — 복사용 목표 실행문

> 1단계 구현·Unity 검증 완료. 아래는 실행에 사용한 범위이며 후속 작업은 [2단계 실행문](level-tool-ui-stage-02-command.md)을 사용한다.

[통합 가이드](../../Planning/project-wide/level-tool-ui-overhaul-plan.md) · [계획](../../Planning/project-wide/level-tool-ui-stage-01-plan.md) · [목표](../../Goals/project-wide/level-tool-ui-stage-01-goal.md)

```text
ServeredMeridian의 레벨툴 UI 개선 1단계를 진행해.

Docs/Planning/project-wide/level-tool-ui-overhaul-plan.md와
Docs/Planning/project-wide/level-tool-ui-stage-01-plan.md,
Docs/Goals/project-wide/level-tool-ui-stage-01-goal.md를 읽고 구현·검증해.

실제 LevelTool 씬을 Windows 목업의 화면 구조로 변경해.
기존 편집·공급·연결·모양·튜토리얼·시험·기록·복구 기능은 모두 유지해.
파일/편집/보기/시험/도움말 메뉴와 공통 단축키를 같은 명령으로 연결하고,
메뉴 옆 단축키·툴팁·검색 가능한 한국어 단축키 안내를 제공해.
텍스트·숫자·한글 조합 입력과 복사/붙여넣기, 모달 우선권을 보존하고
입력 중 저장에서 마지막 값이 누락되지 않도록 해.
화면 크기에 맞는 보드/패널 배치와 도구 프로필만의 창 크기 조절 설정을 적용해.

보드 요소 복사·잘라내기·붙여넣기는 2단계 범위로 유지하고,
이번 단계에서 된다고 표시하지 마. 기존 텍스트 입력의 클립보드 동작은 보존해.
기존 JSON·MemoryPack·게임 규칙과 미저장·선택·Undo/Redo를 유지해.
편리한 UX를 위해 간격·문구·포커스 등은 개선하되,
데이터 의미나 기존 조작 규칙이 달라지는 변경은 먼저 설명하고 논의해.

필요하면 하위 에이전트를 사용해. Unity 검사는 순차 실행해.
커밋·푸시·Player/Addressables 빌드는 하지 마.
HTML 목업·매뉴얼은 수정하지 마.
질문은 충분히 설명하는 제한시간 없는 일반 채팅 문답으로 해.

완료 후 변경·검증·UX 개선점·남은 제한을 보고하고,
2단계 계획서·목표 문서·복사 가능한 목표 실행문을 작성해.
다음 단계 구현은 자동 시작하지 마.
실제 구현과 검증을 모두 완료한 마지막 보고에서만
완료한 작업의 커밋 메시지를 복사 가능한 코드 블록으로 제공해.
```

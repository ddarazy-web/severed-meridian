# 1단계 목표 명령문

아래 내용을 ServeredMeridian 프로젝트의 다음 작업 메시지에 복사한다. 특정 앱 슬래시 명령 구문에 의존하지 않는 자연어 목표 명령이다. 이 문서 작성만으로 목표를 활성화하거나 구현을 시작하지 않는다.

```text
목표를 설정하고 다음 작업을 완료해줘.

작업 프로젝트는 C:/Projects/Git/ServeredMeridian만 사용해.
먼저 AGENTS.md 및 적용되는 Unity 프로젝트 규칙과 다음 문서를 읽어:
1. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/2026-09-30-world-game-screen.md
2. Docs/Goals/MoonRabbitJunkyard/WorldGameScreen/stage-01-world-board-goal.md
3. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/stage-01-world-board-plan.md

목표: 1단계 ‘월드 보드 표시’를 완료한다. 독립 PuzzleGame 씬에서 기존 MemoryPack 레벨을 읽어 일반 블록·파워 블록·장애물·지형·장치를 SpriteRenderer로 표시하고, 기능별 게임 프리팹을 Assets/Prefabs/Game/Puzzle 아래 만든다. 기존 Addressables 아틀라스 분할과 로딩 방식을 유지한다.

계획을 순서대로 실행하고 각 작업의 검증까지 진행해. 초기 맵에 없던 로켓을 포함한 파워 블록의 사후 표시, 2×2 장애물, 덮개/먼지, 벽/포털/장치 연결선, 재표시와 씬 종료 시 리소스 정리를 확인해. 게임 씬이나 프리팹에 레벨 에셋을 직접 참조하지 마.

기존 사용자 변경과 .meta/GUID를 보존해. 이전에 제거한 미완성 코드가 구현되어 있다고 가정하지 마. 2~5단계의 게임 조작·에디터 실행 버튼·목업 UI·상세 효과 연출은 이번에 구현하지 마. 자동 커밋은 하지 마.

목표 문서의 완료 조건을 실제로 검증한 뒤에만 완료로 처리해. 마지막에 변경 파일, 씬 실행 방법, 검증 결과, 화면 캡처 경로, 미검증 사항을 보고하고 계획 문서의 실행 결과를 갱신해. 1단계가 끝나면 멈춰.
```

# 2단계 목표 명령문

아래 내용을 같은 프로젝트의 작업 메시지에 복사한다. 자연어 목표 명령이며, 이 문서 작성만으로 목표를 활성화하거나 구현을 시작하지 않는다.

```text
목표를 설정하고 2단계 ‘실제 게임 플레이’를 완료해줘.

작업 프로젝트는 C:/Projects/Git/ServeredMeridian만 사용해.
적용되는 AGENTS.md와 Unity 프로젝트 규칙을 확인하고 다음 문서를 읽어:
1. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/2026-09-30-world-game-screen.md
2. Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-01-progress.md
3. Docs/Goals/MoonRabbitJunkyard/WorldGameScreen/stage-02-gameplay-goal.md
4. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/stage-02-gameplay-plan.md

기존 1단계 월드 보드·아틀라스·게임 프리팹을 유지하고, StartingBoardSearch와 BoardActionExecutor를 연결해 PuzzleGame 씬에서 실제 플레이할 수 있게 해줘. Input System으로 마우스·터치 교환과 파워 발동을 처리하고 낙하·연쇄·이동 수·미션·승패·라스트팡을 기존 규칙대로 끝까지 실행해. 입력 중복과 로드/연쇄 중 입력을 차단하고 씬 종료 시 리소스를 반환해.

superpowers:executing-plans로 계획 순서대로 직접 구현하고 검증해. 사전 Warmup에만 의존하지 말고 콜드 로드와 매칭으로 생성된 로켓의 실제 이미지 표시를 확인해. 성공 판정만으로 라스트팡을 중단하지 마. 최소 상태 표시를 제공하고 실제 Play Mode 조작과 가로·세로 화면 증거를 남겨.

게임 프리팹은 Assets/Prefabs/Game/Puzzle 아래 기능별로 분리해. 50레벨 MemoryPack과 Addressables 분할을 유지하고 레벨 에셋을 씬/프리팹에 직접 연결하지 마. 기존 변경과 .meta/GUID를 보존하고 자동 커밋하지 마. 열린 프로젝트에 두 번째 Unity 배치를 실행하거나 다른 프로젝트의 에디터를 조작하지 마.

3단계 에디터 ‘게임 플레이’ 버튼, 4단계 목업 UI·아이템·일시정지·다시하기, 상세 애니메이션과 새 이미지 생성은 이번 범위에서 제외해.

목표의 완료 조건을 실제로 검증한 뒤에만 완료 처리해. Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-02-progress.md와 Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-02-gameplay-usage.md를 작성하고, 마지막에 변경 파일·실행 방법·검사 결과·캡처 경로·미검증 사항을 보고해. 2단계가 끝나면 멈춰.
```

# 3단계 목표 명령문

아래 내용을 같은 프로젝트 작업 메시지에 복사한다. 이 문서 작성만으로 목표를 활성화하거나 구현을 시작하지 않는다.

```text
목표를 설정하고 3단계 ‘레벨 에디터에서 게임 실행’을 완료해줘.

C:/Projects/Git/ServeredMeridian 프로젝트만 사용해. 적용되는 AGENTS.md와 Unity 프로젝트 규칙을 확인하고 다음 문서를 읽어:
1. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/2026-09-30-world-game-screen.md
2. Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-02-progress.md
3. Docs/Goals/MoonRabbitJunkyard/WorldGameScreen/stage-03-editor-launch-goal.md
4. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/stage-03-editor-launch-plan.md

superpowers:executing-plans로 계획 순서대로 구현·검증해. 정상인 1·2단계 구현과 편집 보드 이미지 수명 수정은 유지해.

레벨 편집창에 기존 플레이 테스트와 별도의 ‘게임 플레이’ 버튼, 에셋/MemoryPack 입력 선택과 시드를 추가해. 에셋 모드는 미저장 편집값의 독립 사본, MemoryPack 모드는 선택 번호의 마지막 생성 파일을 사용해. 실행 클릭으로 원본을 저장하거나 팩을 자동 갱신하지 마.

도메인 리로드를 지나 선택 데이터를 PuzzleGame 세션에 한 번만 전달하고 실제 월드 보드에서 플레이하게 해. Play Mode 종료·취소·실패 시 기존 편집 씬, 시작 씬 설정, 선택 레벨과 미저장 상태를 보존해. 요청·사본·아틀라스 리소스를 정리하고 편집 보드 이미지가 자동 복구되는지 확인해.

서로 다른 두 레벨과 두 입력 모드, 중복 실행, 누락/손상 팩, 로드 중 종료, 복귀 후 재실행을 실제 Editor에서 검증해. 기존 게임 플레이 및 편집 이미지 수명 검사도 실행하고 진입·조작·복귀 화면 증거를 남겨.

50레벨 MemoryPack과 Addressables 분할을 유지해. 씬/프리팹에 레벨 에셋을 직접 연결하지 말고 기존 변경과 .meta/GUID를 보존해. 자동 커밋하지 말고, 다른 프로젝트의 Editor를 조작하거나 열린 프로젝트에 두 번째 Unity 배치를 실행하지 마.

Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-03-progress.md와 Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-03-editor-launch-usage.md를 작성해. 완료 조건을 실제로 검증한 뒤에만 목표를 완료 처리하고 변경 내용·실행 방법·검사 결과·캡처·미검증 사항을 보고해. 4단계 목업 UI와 상세 애니메이션은 구현하지 말고 3단계가 끝나면 멈춰.
```

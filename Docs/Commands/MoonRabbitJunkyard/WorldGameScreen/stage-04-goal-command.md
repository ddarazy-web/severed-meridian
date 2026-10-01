# 4단계 목표 명령문

[목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-04-mockup-ui-goal.md) · [작업 계획](../../../Planning/MoonRabbitJunkyard/WorldGameScreen/stage-04-mockup-ui-plan.md)

아래 내용을 작업 메시지에 복사한다. 문서 작성만으로 목표를 활성화하거나 구현을 시작하지 않는다.

```text
목표를 설정하고 4단계 ‘목업 UI 구성’을 완료해줘.

C:/Projects/Git/ServeredMeridian 프로젝트만 사용해. 적용되는 AGENTS.md와 Unity 프로젝트 규칙을 확인하고 다음 문서를 읽어:
1. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/2026-09-30-world-game-screen.md
2. Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-03-progress.md
3. Docs/Goals/MoonRabbitJunkyard/WorldGameScreen/stage-04-mockup-ui-goal.md
4. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/stage-04-mockup-ui-plan.md
5. Docs/MoonRabbitJunkyard/Mockups/puzzle-screen.html

superpowers:executing-plans로 순서대로 직접 구현·검증해. 정상인 1~3단계를 유지하고 목업에 맞춰 이동 수·미션·아이템·일시정지·결과 화면을 uGUI로 구성해. 보드는 월드 SpriteRenderer를 유지해. UI 프리팹은 Assets/Prefabs/UI/Puzzle, 게임 프리팹은 Assets/Prefabs/Game/Puzzle 아래 기능별로 분리해.

세 아이템은 기존 실행기 규칙을 사용하고 시험용 무제한으로 표시해. UI 입력과 보드 입력이 겹치지 않게 하고, 일시정지 중에는 연쇄도 멈춰. 결과는 라스트팡 등 후속 처리 종료 후 표시해. 다시하기는 처음 실행한 데이터의 독립 스냅샷과 같은 시드를 사용하고, 에셋/MemoryPack 선택이 기본 레벨로 바뀌지 않게 해.

가로·세로·긴 세로·태블릿 비율과 안전 영역을 검증해. 회전 시 보드와 이동 수·미션·진행 상태를 유지해. 실제 Editor에서 두 입력 모드, 아이템 선택/취소, 일시정지/재개, 승리/실패, 반복 다시하기, 편집창 복귀와 이미지 수명을 확인하고 화면 증거를 남겨.

50레벨 MemoryPack과 Addressables 분할, 기존 변경과 .meta/GUID를 보존해. 레벨 에셋을 씬/프리팹에 직접 연결하거나 원본/팩을 자동 저장하지 마. 자동 커밋하지 말고 다른 프로젝트 Editor나 열린 프로젝트에 두 번째 Unity 배치를 실행하지 마.

Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-04-progress.md와 Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-04-mockup-ui-usage.md를 작성해. 실제 완료 조건을 충족한 뒤에만 목표를 완료 처리하고 결과·검증·캡처·미검증 사항을 보고해. 새 이미지·상세 애니메이션·광고/결제/보상·다음 레벨 이동 및 5단계 전체 통합 검증은 진행하지 말고 멈춰.
```

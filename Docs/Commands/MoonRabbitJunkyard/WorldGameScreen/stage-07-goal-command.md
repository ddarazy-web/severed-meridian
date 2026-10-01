# 7단계 목표 명령문

[작업 계획](../../../Planning/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-plan.md) · [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-goal.md)

아래 명령을 복사해 요청할 때 목표를 활성화한다. 현재 구현 및 검증 진행 상황은 [실행 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-07-progress.md)을 따른다. 재실행 시 검증된 작업을 유지하고 남은 조건부터 진행한다.

```text
목표를 설정하고 월드 게임 화면 7단계 ‘제거·낙하·채움 연출’을 완료해줘.

C:/Projects/Git/ServeredMeridian만 사용하고 적용되는 AGENTS.md와 Unity 프로젝트 규칙을 확인해. 다음 문서를 읽어:
1. Docs/Goals/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-goal.md
2. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-plan.md
3. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/2026-09-30-world-game-screen.md
4. Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-06-progress.md
5. Docs/Decisions/MoonRabbitJunkyard/2026-10-01-nine-by-nine-board.md

superpowers:executing-plans로 네 작업을 순서대로 직접 실행해. 9×9 보드, 기존 스와이프·교환, 2×2 장애물 확대, 가로 로켓 중앙 보정·12% 확대와 사용자 dirty 작업을 유지해.

교환 뒤 제거 → 실제 경로 이동·공급 → 착지 → 다음 연쇄 순서로 보여줘. 규칙은 한 번만 계산하고 계산 전 표시와 Changes/Effects/Settlement 기록으로 재생해. 같은 Batch의 이동은 함께, Batch 간에는 순서대로 재생하고 포털은 입구/출구를 분리해. 공급은 실제 공급원과 유입 방향을 따르고, 고철·회수 부품·파워 및 회수 도착도 처리해. 고정 레이어와 2×2 장애물을 이동시키지 마.

제거 0.12초, 이동 구간 0.12~0.24초, 공급 0.16초, 최종 착지 0.06초를 초기값으로 적용해. 재생 중 입력·아이템·다음 연쇄와 결과 UI 조기 표시를 막고, 일시정지·회전·다시하기·실패·씬 종료의 표시와 잠금을 정리해. 기록 없는 결과를 가짜 이동으로 추정하지 마.

같은 레벨·시드·입력의 직접 실행기와 보드·난수·이동 수·공급 커서·미션·회수·승패 결과가 같음을 검증해. 가로·세로 실제 게임 화면에서 중간 이동과 채움 증거를 남겨. Asset 사본/MemoryPack 진입과 관련 기존 검사를 유지해.

빌드는 하지 마. 플레이어·Addressables 콘텐츠 빌드와 이를 내부 호출하는 검사도 금지해. 기존 리소스의 Editor 컴파일·Play Mode 검사만 하고 번들이 없으면 빌드로 우회하지 마. 사용자 Editor 강제 종료, 미저장 씬·원본 레벨 자동 저장, 자동 커밋·푸시를 하지 마.

파워 상세 효과는 8단계, 소리·진동·미션 비행은 9단계이므로 구현하지 마. 새 이미지·패키지·저장 형식·영속 블록 ID를 추가하지 마. 필요한 새 프리팹은 Assets/Prefabs 아래 Game/Puzzle과 UI/Puzzle로 기능별 구분해.

검증 기록은 Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-07-progress.md에, 사용법은 Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-usage.md에 남겨. 목표 완료 조건을 실제 증거로 대조하고 미검증 항목을 명시해. 모든 필수 조건을 충족했을 때만 목표를 완료 처리해.
```

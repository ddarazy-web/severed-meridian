# 6단계 목표 명령문

[작업 계획](../../../Planning/MoonRabbitJunkyard/WorldGameScreen/stage-06-swipe-swap-plan.md) · [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-06-swipe-swap-goal.md)

아래 명령을 복사해 요청할 때 목표를 활성화한다. 문서 작성만으로 구현이나 목표 실행을 시작하지 않는다.

```text
목표를 설정하고 월드 게임 화면 6단계 ‘스와이프·교환 연출’을 완료해줘.

C:/Projects/Git/ServeredMeridian만 사용하고 적용되는 AGENTS.md와 Unity 프로젝트 규칙을 확인해. 다음 문서를 읽어:
1. Docs/Goals/MoonRabbitJunkyard/WorldGameScreen/stage-06-swipe-swap-goal.md
2. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/stage-06-swipe-swap-plan.md
3. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/2026-09-30-world-game-screen.md
4. Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-05-progress.md

superpowers:executing-plans로 네 작업을 순서대로 직접 실행해. 기존 정상 동작과 사용자 변경을 보존하고 요청 범위만 수정해.

마우스·터치에서 0.25칸 스와이프를 손을 떼기 전에 한 번 확정하고, 확정 전 미리보기와 두 블록의 0.15초 교환을 구현해. NoNewMatch만 왕복하고 벽·고정 장애물·덮개 등은 통과하지 않게 해. 기존 두 탭 교환, 파워 탭, 아이템 선택을 유지해. 이동 가능한 고철·회수 부품도 처리하고 바닥·덮개는 움직이지 않게 해.

규칙 결과를 한 번 계산한 뒤 원래 블록 모습으로 교환부터 보여주고 결과 반영 후 연쇄를 진행해. 재생 중 입력·아이템·다음 연쇄를 차단하고, 실패는 이동 수·난수·보드를 바꾸지 마. 일시정지·회전·포커스 상실·재시작·씬 종료의 입력/연출 수명을 검사해.

빌드는 하지 마. 플레이어 빌드, Addressables 콘텐츠 빌드와 이를 내부 호출하는 검사도 금지해. 기존 리소스를 이용한 Editor 컴파일·Play Mode 검사만 실행하고 필요한 번들이 없으면 빌드로 우회하지 마. 실제 움직임 증거와 같은 시드의 결과 동일성을 확인해.

낙하·채움은 7단계, 파워 상세 효과는 8단계이므로 이번에 구현하지 마. 새 이미지·패키지·데이터 포맷 변경을 하지 마. 원본 레벨·미저장 씬을 자동 저장하거나 Editor를 강제 종료하지 마. 자동 커밋·푸시하지 마.

검증 결과를 Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-06-progress.md에, 사용법을 Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-06-swipe-swap-usage.md에 남겨. 목표 완료 조건을 실제 증거와 대조하고 미검증 항목을 명시해. 필수 조건이 충족된 경우에만 목표를 완료 처리해.
```

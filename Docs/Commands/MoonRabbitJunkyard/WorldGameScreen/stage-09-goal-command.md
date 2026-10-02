# 9단계 목표 명령문

[작업 계획](../../../Planning/MoonRabbitJunkyard/WorldGameScreen/stage-09-progress-feedback-plan.md) · [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-09-progress-feedback-goal.md)

아래 명령문은 다음 구현 요청에 붙여 넣는다. 문서 작성만으로 목표를 생성하거나 구현을 시작하지 않는다.

```text
월드 게임 화면 9단계 ‘미션 수집·진행·승패 피드백’을 목표로 설정하고 완료해줘. 같은 목표가 이미 활성 상태이면 이어서 진행하고, 다른 활성 목표가 있으면 임의로 바꾸지 말고 먼저 현재 상태를 알려줘.

C:/Projects/Git/ServeredMeridian만 사용하고 적용되는 AGENTS.md와 Unity 프로젝트 규칙을 확인해. 다음 문서를 읽어:
1. Docs/Goals/MoonRabbitJunkyard/WorldGameScreen/stage-09-progress-feedback-goal.md
2. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/stage-09-progress-feedback-plan.md
3. Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-08-progress.md
4. Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-usage.md
5. Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-usage.md
6. Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/2026-10-01-drone-lift-hold.md

superpowers:executing-plans로 계획의 5개 작업을 순서대로 직접 실행해. 현재 dirty 작업과 원본 레벨·씬을 보존해. 일반 3기 이하 드론의 느린 상승·대기·돌파, 다른 파워와 조합한 드론의 대형 선회, 표적 예고 제거와 현재 낙하 시간 /1.44를 유지해. 9×9, 스와이프 정렬 순서, 2×2 칸별 피해, 동시 낙하·신규 공급 대기열·대각선 공급 규칙도 보존해.

실제 미션 증가에만 수집 비행과 HUD 반응을 연결하고 표시 진행을 실제 값과 일치시켜. 확실하지 않은 수집 원점은 만들지 마. 최대 8개 수집 객체로 묶어서 재사용하되 증가량은 누락하지 마. 보드 피해·낙하·연쇄를 수집 비행 때문에 직렬로 지연하지 마. 이동 수·연쇄·시작·라스트팡·승패를 실제 실행기 경계에 맞추고 최종 결과 패널은 보드와 수집 표시가 정리된 뒤 한 번만 표시해.

규칙은 한 번만 계산하고 실제 결과 기록을 사용해. 표적·피해·미션·난수를 다시 계산하지 마. 필요하면 결정 지점에 최소 일회성 표시 기록만 추가하고 저장 DTO는 변경하지 마. 기존 이미지·아틀라스·uGUI·UniTask를 재사용해. 새 UI 프리팹은 Assets/Prefabs/UI/Puzzle/Feedback, 게임 효과는 Assets/Prefabs/Game/Puzzle/Effects 아래 기능별로 구분해.

빌드는 하지 마. 플레이어·Addressables 콘텐츠 빌드 및 이를 내부 호출하는 검사도 금지해. 사용자 에디터를 강제 종료하거나 미저장 씬을 자동 저장하지 마. 자동 커밋·푸시도 하지 마. 새 이미지·음원·패키지, 소리·진동·새 게임 규칙·광고·과금은 이번 범위에 넣지 마.

실제 게임 씬과 Asset/MemoryPack에서 표시·pause·회전·재시작·취소·자원 반환 및 직접 실행기와 최종 결과 동등성을 검증해. 목표의 15개 완료 조건별 증거, 실제 검사 수, 캡처, 남은 한계를 기록해. 검증하지 못한 항목은 완료로 표시하지 말고 10단계로 자동 진행하지 마.
```

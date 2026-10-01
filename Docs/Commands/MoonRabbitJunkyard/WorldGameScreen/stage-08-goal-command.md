# 8단계 목표 명령문

[계획](../../../Planning/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-plan.md) · [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-goal.md)

2026-10-01 구현·Editor 검증을 완료했다. 아래는 실행에 사용한 목표 명령문이며 이력/재검증용으로 보존한다. 최신 상태는 검증 기록과 완료 조건을 우선한다. 이 문서를 읽는 것만으로 새 목표를 생성하지 않는다.

```text
월드 게임 화면 8단계 ‘파워·장애물 상세 연출’의 기존 목표를 이어서 완료해줘. 활성 목표가 없을 때만 이 내용으로 목표를 설정해.

C:/Projects/Git/ServeredMeridian만 사용하고 적용되는 AGENTS.md와 Unity 프로젝트 규칙을 확인해. 다음 문서를 읽어:
1. Docs/Goals/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-goal.md
2. Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-plan.md
3. Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-usage.md
4. Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/2026-10-01-simultaneous-fall.md
5. Docs/Decisions/MoonRabbitJunkyard/2026-10-01-fresh-supply-diagonal.md
6. Docs/Contents/MoonRabbitJunkyard/Art/Priority2/README.md
7. Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-08-progress.md

기존 구현과 진행 기록의 최신 검증을 먼저 확인하고 계획서의 남은 작업부터 진행해. BuildClips의 미정의 호출은 후속 구현에서 해결됐으므로 다시 만들지 마. 이전 순수 검사와 기본 재생 검사 통과를 전체 게임 세션 검증으로 간주하지 마. 8단계 완료 전 9단계로 넘어가지 마.

superpowers:executing-plans로 5개 작업을 순서대로 직접 실행해. 현재 dirty 작업, 9×9, 2×2 크기, 가로 로켓 중앙·크기 보정, 스와이프와 동시 낙하·신규 공급 대기열을 유지해. 기존 블록의 대각선 이동 금지와 예정된 상단 공급 우선 규칙도 유지해. 과거 7단계의 Batch 간 순차 낙하로 되돌리지 마.

기존 로켓 발사·드론 프로펠러·2차 효과 이미지를 사용해 로켓·드론·폭탄·자석, 파워 생성·매칭, 지원 조합과 연쇄, 장애물 손상·파괴·충전을 연결해. 로켓은 발사 전 불꽃이 없어야 해. 공격 도착 시 대상이 반응하고 모든 효과가 끝난 뒤 동시 낙하와 다음 연쇄가 진행되게 해.

규칙은 한 번만 계산하고 실행 전 스냅샷과 실제 결과 기록을 사용해. 표적·피해·난수를 다시 계산하지 마. 기록이 부족하면 결정 지점에 필요한 일회성 표시 기록만 추가하고 저장 형식은 바꾸지 마. 2×2 중복 반응을 막되 실제 반복 타격은 보존해.

효과 아틀라스는 종류별로 필요할 때 로드하고 종료·취소 시 반환해. 새 게임 효과 프리팹은 Assets/Prefabs/Game/Puzzle/Effects 아래 기능별로 나누고 UI용과 구분해. 새 이미지·패키지, 소리·진동·미션 비행·승패 상세 연출은 추가하지 마.

빌드는 하지 마. 플레이어·Addressables 콘텐츠 빌드와 이를 내부 호출하는 검사도 금지해. 번들이 없으면 누락을 기록하고 빌드로 우회하지 마. 사용자 Editor 강제 종료, 미저장 씬·원본 레벨 자동 저장, 자동 커밋·푸시를 하지 마.

실제 Editor/Play Mode에서 중간 공격·타격·손상 프레임, 모든 지원 조합, 입력 잠금, pause/resume·회전·다시하기·씬 재진입·자원 해제를 검증해. 직접 실행기와 최종 보드·난수·이동 수·공급·미션·회수·승패 일치를 확인하고 동시 낙하·신규 대각선·교환 회귀도 검사해.

검증 기록은 Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-08-progress.md에, 사용법은 Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-usage.md에 남겨. 실제 충족한 완료 조건만 체크하고 미검증 항목은 구분해. 모든 필수 조건을 충족했을 때만 목표를 완료 처리해.
```

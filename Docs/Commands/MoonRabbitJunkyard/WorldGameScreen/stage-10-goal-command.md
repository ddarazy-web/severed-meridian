# 10단계 목표 명령문 — 소리

아래는 복사해서 실행을 요청하는 명령문이다. 이 문서 작성만으로 구현·새 Goal을 시작하지 않는다. 9단계가 미완료이면 먼저 기존 목표를 마무리한다.

```text
ServeredMeridian 프로젝트만 대상으로 작업해.

Docs/Goals/MoonRabbitJunkyard/WorldGameScreen/stage-09-progress-feedback-goal.md와 최신 실행 기록·코드를 먼저 확인해. 9단계 완료 조건 15개 중 미완료가 있으면 기존 9단계 목표를 유지하고 필요한 검증·수정을 끝내. 완료 근거 없이 10단계를 시작하지 마.

9단계가 완료되면 Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/stage-10-audio-plan.md와 Docs/Goals/MoonRabbitJunkyard/WorldGameScreen/stage-10-audio-goal.md를 읽고, 10단계 소리 목표를 설정하여 계획의 4개 작업을 순서대로 직접 진행해.

외부 음원·패키지 없이 한 번 생성하는 검증용 합성 효과음과 최대 8개 AudioSource 재사용으로 시작해. 실제 교환·제거·파워 표시·착지·미션 도착·시작·승패에 연결하고, 같은 종류 0.06초 중첩 제한·결과음 우선을 적용해. 진동 기능·설정·플랫폼 호출은 추가하지 마. 드론 소리는 최신 실제 돌파·타격 시간에 맞추고 목표를 미리 알리지 마. UI Refresh마다 소리를 반복하지 마.

9×9와 최신 드론·낙하·2×2 피해·스와이프 order·미션 수집·라스트팡·50레벨 MemoryPack·Asset·필요한 이미지 종류만 Addressables 로드하는 구조를 보존해. 수정된 교환·섞기 아이콘의 256×256 투명 이미지와 기존 GUID 연결도 유지해. 게임용 재생 프리팹은 Assets/Prefabs/Game/Puzzle/Feedback에 기능별로 분리해. 전체 게임 씬·아이템 바를 재생성하지 마. 규칙·저장 DTO·BGM·설정 창·광고·과금은 추가하지 마.

pause/resume·백그라운드·다시하기 5회·준비 취소·오류·씬 종료/재진입에서 예약·음성·클립·늦은 콜백을 정리해. 실제 씬의 Asset/MemoryPack과 단독 4파워·10조합·아이템·연쇄·회수·2×2에서 표시 시각 및 직접 실행기와 최종 상태 동등성을 검증해.

목표의 12개 조건별 증거·실제 검사 수·청취 실적·미검증 항목을 stage-10-progress.md와 stage-10-audio-usage.md에 기록해. Editor의 요청 검사와 실제 소리 청취를 구분해. 수행하지 못한 항목은 완료로 표시하지 마.

플레이어·Addressables 콘텐츠 빌드 및 이를 내부 호출하는 검사도 하지 마. 자동 커밋·푸시하지 마. 사용자 Unity를 강제 종료하거나 미저장 씬을 자동 저장하지 마. 원본 레벨·씬·무관 dirty 변경을 보존하고 11단계로 자동 진행하지 마.
```

# 11단계 목표 명령문 — 반복 플레이와 성능 안정화

아래 코드 블록의 복사 버튼으로 명령문을 복사해 실행을 요청한다. 문서 작성만으로 새 Goal이나 구현을 시작하지 않는다.

```text
ServeredMeridian 프로젝트만 대상으로 작업해.

Docs/Commands/MoonRabbitJunkyard/WorldGameScreen/stage-11-goal-command.md, Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/stage-11-stability-plan.md와 Docs/Goals/MoonRabbitJunkyard/WorldGameScreen/stage-11-stability-goal.md를 읽고, 11단계 반복 플레이·성능 안정화 목표를 설정하여 완료해. 기존 11단계 목표가 진행 중이면 새로 만들지 말고 이어서 진행해.

10단계 완료 기록과 현재 코드·로그를 먼저 감사하고, 계획의 4개 작업을 순서대로 직접 수행해. 기준 감사 → 실제 반복/중단/회전/재사용 검증 → 실제 Editor 성능 관찰과 확인된 병목만 수정 → 회귀·완료 조건별 증거·사용 안내·최종 리뷰 순서로 진행해.

Asset/MemoryPack 실제 씬, 4파워·10조합·중첩 피해 2×2·3아이템·연쇄·회수·라스트팡을 직접 실행기 전체 상태와 비교해. 각 연출 중 pause/background/다시하기/실제 준비 중 취소·오류·씬 종료 및 가로·세로/안전 영역 전환을 검사해. 동일 fixture의 5회 워밍업+5회 반복에서 풀 증가와 잔류를 확인하고, 성능은 실제 Update 300프레임 이상으로 관찰해. 재현된 결함만 실패→최소 수정→통과로 처리하고 정상인 부분은 그대로 둬.

9×9·최신 블록/장애물/로켓 크기·스와이프 order·2×2 범위 피해·드론 상승/대기/돌파와 반지름1~4칸 선회·표적 예고 없음·낙하/공급 시간 /1.44·동시 낙하/공급 대기·신규 블록만 대각선/상단 우선을 보존해. 10단계 합성 효과음14종/최대8음성/.06초 제한/미션·결과 우선과 실제 타격 시점, 수정된 교환·섞기 256×256 투명 아이콘/GUID, 50레벨 MemoryPack·Asset·필요한 종류만 아틀라스 로드하는 구조도 보존해. 연출 속도를 임의로 바꾸지 마.

진동·새 규칙/저장 DTO/패키지/BGM/설정 창/광고/과금은 추가하지 마. 전체 게임 씬·아이템 바를 재생성하지 마. 플레이어·Addressables 콘텐츠 빌드 및 이를 내부 호출하는 검사, 커밋·푸시를 하지 마. 사용자 Unity 강제 종료·미저장 씬 자동 저장 없이 원본 레벨·씬·무관 dirty 변경을 보존해.

목표12개별 증거·이번 실제 검사 수·성능 전후 관찰·보존 감사·미검증 항목을 Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-11-progress.md에 기록하고, 사용 안내를 Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-11-stability-usage.md에 작성해. Editor 검사와 실제 청취/실기기 성능은 구분하고 수행하지 않은 항목을 완료로 표시하지 마. 전체 작업 후 독립 최종 리뷰를 한 번 수행해. 완료 조건 감사 후에만 목표를 완료하고 다음 목표로 자동 진행하지 마.
```

# 월드 보드 게임 화면 단계별 구현 계획

## 현재 상태

2026-10-02 최신 기준: **1~12단계 구현·Editor 검수 완료**. 12단계는 실제 화면/움직임·단일 리뷰 수정 후17개 회귀와 추가InsetUI67PASS를 통과했다. [최신 완료 근거](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-12-progress.md)를 따른다. 실제 청취/실기기는 미검증이다. 13단계는 계획만 작성했으며 미착수다. 아래 날짜별 상태는 과거 기록이다.

2026-10-01 8단계 완료 당시: **1~8단계 구현·Editor 검증 완료**. [8단계 계획](stage-08-power-effects-plan.md) · [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-goal.md) · [검증 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-08-progress.md) · [사용 안내](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-usage.md). 2052개 검사 통과, 빌드 미실행. 아래 날짜별 상태는 과거 기록이며 이 업데이트가 우선한다.

7단계 이후 신규 공급 블록만 대각선으로 채우고 상단 공급을 우선하도록 변경했으며, 화면은 기존·신규 블록의 동시 낙하와 공급 대기열로 개선했다. [최신 사용 안내](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-usage.md) · [대각선 검증 772 PASS](../../../Verification/MoonRabbitJunkyard/2026-10-01-fresh-supply-diagonal.md) · [동시 낙하 검증 367 PASS](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/2026-10-01-simultaneous-fall.md). 서로 다른 시점의 검사 수이며 합산한 단일 실행 결과가 아니다. 과거 계획의 Batch 간 순차 재생보다 최신 동작이 우선한다.

2026-10-01 최신 상태: **1~7단계 완료**. 7단계 제거·낙하·채움은 기존 규칙 기록을 재생하며 관련 Editor 검사 883개를 통과했다. [작업 계획](stage-07-settlement-plan.md) · [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-goal.md) · [검증 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-07-progress.md) · [사용 안내](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-usage.md). 다음은 **8단계 파워 상세 효과**다. 9×9, 장애물·로켓 표시 보정을 유지했다. **플레이어·Addressables 콘텐츠 빌드는 실행하지 않았다.** 아래 초기 상태는 과거 기록이다.

2026-09-30 업데이트: **1단계 완료**. [목표 및 완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-01-world-board-goal.md), [실행·검증 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-01-progress.md), [씬 실행 방법](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-01-world-board-usage.md). 실제 번들 기반 월드 검사 174개와 기존 에디터 회귀 검사 22개가 통과했다. 2단계도 완료했으며 [실행 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-02-progress.md)과 [사용법](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-02-gameplay-usage.md)에 결과를 남겼다. 3단계도 구현·검증을 완료했다(최종 자동 확인 202 PASS). 4단계도 2026-10-01 구현·검증을 완료했다. 5단계는 미시작이다. 아래 초기 정리 기록은 당시 상태다.

목업과 기존 게임 실행기·MemoryPack·아틀라스 구조 조사를 마쳤다. 구현 단계는 아직 완료하지 않았다.
2026-09-30: 신규 월드 코드 초안 3개는 존재하지 않는 BoardEdge 필드 참조 오류가 확인되어 제거했다. 기존 추적 파일은 수정하지 않았다. 목업에서 추출한 폰트와 라이선스는 원본과 해시가 같음을 확인하여 유지한다. Unity 컴파일 및 Play Mode 검증은 아직 수행하지 않았다.

사용자 요청에 따라 지금은 계획과 초안 정리까지만 수행한다. 각 단계는 별도 작업으로 진행하고, 변경 내용과 검증 결과를 기록한다.

## 목표와 구조

목업을 기준으로 게임 화면을 구성한다. 보드는 월드 공간의 SpriteRenderer로 구현하고, 레벨 에디터에서 선택한 맵을 `게임 플레이` 버튼으로 실행한다.
- UI 프리팹: `Assets/Prefabs/UI/Puzzle` — HUD, 미션, 아이템, 팝업
- 게임 프리팹: `Assets/Prefabs/Game/Puzzle` — 세션, 월드 보드, 칸, 장애물
- 코드: `GameScreen/Runtime/Session`, `World`, `UI` 및 `GameScreen/Editor`
- 독립 게임 씬을 사용하고 기존 편집 씬은 보존한다.

## 1단계 — 월드 보드 표시

독립 게임 씬과 월드 보드·칸·장애물 프리팹을 만든다. 기존 Addressables 아틀라스로 일반 블록, 파워 블록, 장애물, 덮개, 먼지, 벽, 포털을 표시한다. 활성 칸과 2×2 장애물, 그리기 순서를 처리한다. 플레이 중 새로 생기는 파워 블록도 로드 가능하도록 준비한다.

완료 조건: 지정한 테스트 레벨이 월드에 이미지로 표시되고 Unity 컴파일 및 화면 검증을 통과한다. 아직 조작은 제공하지 않는다.

## 2단계 — 실제 게임 플레이

상태: 구현·검증 완료. [실행 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-02-progress.md) · [목표 및 완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-02-gameplay-goal.md) · [세부 실행 계획](stage-02-gameplay-plan.md) · [복사용 목표 명령문](../../../Commands/MoonRabbitJunkyard/WorldGameScreen/stage-02-goal-command.md).

기존 StartingBoardSearch와 BoardActionExecutor를 연결한다. 마우스·터치 교환, 파워 발동, 낙하·연쇄, 이동 수, 미션, 승패와 마지막 파워 처리를 연결한다. 입력 중복 차단과 씬 종료 시 리소스 반환을 처리한다. 플레이 검증용 최소 표시를 사용한다.

완료 조건: 유효/무효 교환, 파워 생성·발동, 연쇄 종료와 승패를 실제 Play Mode에서 확인한다.

## 3단계 — 레벨 에디터에서 게임 실행

상태: 구현·검증 완료. [실행 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-03-progress.md) · [사용법](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-03-editor-launch-usage.md). [목표 및 완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-03-editor-launch-goal.md) · [4개 작업 실행 계획](stage-03-editor-launch-plan.md) · [복사용 목표 명령문](../../../Commands/MoonRabbitJunkyard/WorldGameScreen/stage-03-goal-command.md).

기존 플레이 테스트를 유지하며 `게임 플레이` 버튼을 추가한다. 선택한 레벨의 에셋 사본 또는 MemoryPack 데이터를 게임 씬으로 전달한다. 에셋 모드에서는 미저장 편집 내용도 사본에 반영한다. Play Mode 종료 후 편집으로 돌아오도록 한다.

완료 조건: 서로 다른 맵과 두 입력 소스에서 선택한 데이터가 정확히 실행되고 원본 에셋은 변하지 않는다. 게임 씬/프리팹에 레벨 에셋 직접 참조가 없어야 한다.

## 4단계 — 목업 UI 구성

상태: 구현·검증 완료 (2026-10-01). [검증 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-04-progress.md) · [사용법](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-04-mockup-ui-usage.md). [작업 계획](stage-04-mockup-ui-plan.md) · [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-04-mockup-ui-goal.md) · [목표 명령어](../../../Commands/MoonRabbitJunkyard/WorldGameScreen/stage-04-goal-command.md)

uGUI로 이동 수, 미션, 아이템, 일시정지, 결과 화면을 기능별 프리팹으로 만든다. 목업의 가로·세로 배치와 안전 영역에 맞춰 월드 보드 영역을 조절한다. 아이템 선택·취소, 일시정지·재개, 다시하기를 연결한다.

완료 조건: 가로/세로 화면에서 겹침 없이 표시되고 UI 터치가 보드 입력과 충돌하지 않는다. 회전 시 게임 상태가 유지된다.

## 5단계 — 통합 검증과 마무리

상태: 구현·통합 검증 완료 (2026-10-01). [네 작업 실행 계획](stage-05-integration-plan.md) · [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-05-integration-goal.md) · [복사용 목표 명령어](../../../Commands/MoonRabbitJunkyard/WorldGameScreen/stage-05-goal-command.md)

전체 실행 경로와 기존 에디터 기능의 회귀를 확인한다. Addressables 및 MemoryPack 빌드 경로, 레벨 에셋 제외, 재시작·씬 종료 리소스 정리를 검사한다. 대표 레벨의 가로/세로 화면을 캡처하여 목업과 비교하고 사용 방법을 기록한다.

완료 조건: 선택 맵 실행부터 승패·다시하기까지 검증 결과를 남긴다. 빌드 및 기기에서 확인하지 못한 부분은 명시한다.

## 후속 플레이 연출 (6~11단계)

| 단계 | 작업 | 완료 기준 |
| --- | --- | --- |
| 6 — 스와이프·교환 연출 | 손을 떼기 전 교환 확정, 점유자 미리보기·교환·무효 복귀, 입력/재생 경계 | 한 제스처 한 행동, 규칙 결과 보존, 중간 이동과 복귀가 보임 |
| 7 — 제거·낙하·채움 | 매칭 제거, SettlementRecord 기반 중력·대각선·경로·포털·공급, 착지와 다음 연쇄 | 실제 경로로 이동하며 표시 완료 후 다음 연쇄 진행 |
| 8 — 파워·장애물 연출 | 기존 로켓·드론·폭탄·자석 및 2차 효과 연결, 파워 조합·내구도 변화 | 공격 도착과 타격 표시가 맞고 효과 잔상이 없음 |
| 9 — 미션 수집·진행·승패 피드백 | 실제 미션 수집, 이동 수·연쇄 강조, 시작·승패·라스트팡 | 표시 진행과 실제 진행이 일치하고 결과가 명확히 전달됨 |
| 10 — 소리 | 음원 선정·소유권, 행동별 소리·중첩 제한 | 소리가 실제 행동에 대응하며 중복·pause·종료가 정리됨 |
| 11 — 속도 조정과 안정화 | 연쇄 시간·효과 재사용, 정지·회전·다시하기·중단, Editor 성능 관찰 | 입력 잠금/표시 잔류/수명 누수 없이 반복 플레이 |

2026-10-01 최신 상태: 1~9단계 구현·Editor 검증 완료. 9단계는 [5개 작업 계획](stage-09-progress-feedback-plan.md)과 [15개 완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-09-progress-feedback-goal.md)을 완료하고 독립 리뷰 수정 후 새 검사 349개·관련 회귀 1,526개를 통과했다. [조건별 증거·리뷰 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-09-progress.md) · [사용 안내](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-09-progress-feedback-usage.md). 최신 드론·낙하를 보존했다. 빌드·실기기 검증·커밋·푸시는 실행하지 않았다. 10단계도 효과음 연결·검증·최종 리뷰 수정을 완료했다.

10단계는 [4개 작업 계획](stage-10-audio-plan.md), [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-10-audio-goal.md), [목표 명령문](../../../Commands/MoonRabbitJunkyard/WorldGameScreen/stage-10-goal-command.md)을 작성했다. 검증용 합성 효과음·8음성 재사용·실제 표시 시각 연결·수명 검사를 완료했다. 새 효과음 검사 1,617 PASS와 이번 실행의 관련 회귀 1,875 PASS를 확인했다. [조건별 실행 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-10-progress.md)과 [사용 안내](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-10-audio-usage.md)를 참고한다. 실제 청취·실기기 출력은 미검증이다. 11단계는 [4개 작업 계획](stage-11-stability-plan.md), [목표·12개 완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-11-stability-goal.md), [복사용 목표 명령문](../../../Commands/MoonRabbitJunkyard/WorldGameScreen/stage-11-goal-command.md)을 작성했다. 기존 속도를 유지하며 반복 플레이·수명·화면 전환·성능을 관찰하고 확인된 결함만 안정화한다. 11단계 Goal·검증은 진행 중이며 완료로 표시하지 않는다.

## 12단계 — 실제 게임 화면·연출 품질 검수

상태: 완료. 실제20장·18사례·추가 inset UI·17개 회귀·보존과 단일 리뷰 조치를 감사했다. [완료 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-12-progress.md) · [3개 작업 계획](stage-12-presentation-plan.md) · [목표·10개 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-12-presentation-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/WorldGameScreen/stage-12-goal-command.md).

실제 Game View에서 네 해상도·플레이/일시정지/승리/실패·안전 영역 화면을 목업과 비교하고, 최신 블록/아이템의 식별성과 드론/낙하/타격/수집의 움직임을 관찰한다. 재현된 표시 결함만 최소 수정한다. 실제 청취 여부·실기기 미검증은 자동 검사와 구분한다. 빌드·새 진행/저장 기능은 추가하지 않는다.

## 13단계 — 승리 후 다음 레벨 연결

상태: 완료. 단일 독립 리뷰의2개 Important를 RED→GREEN으로 수정한 뒤 영향12개·필수7개·재현3개와 보존 gate·12개 완료 조건을 감사했다. 12단계 완료 근거를 현재 파일로 감사한 뒤 착수했다. [4개 작업 계획](stage-13-level-transition-plan.md) · [목표·12개 완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-13-level-transition-goal.md) · [복사용 목표 명령문](../../../Commands/MoonRabbitJunkyard/WorldGameScreen/stage-13-goal-command.md).

기존 에디터 레벨 선택을 유지하고 MemoryPack 승리 후 다음 번호로 이동한다. 데이터·시작 보드·아틀라스 준비 성공 시에만 교체하며 실패하면 기존 승리/Retry를 유지한다. Asset 실행은 미저장 사본 테스트를 유지한다. 저장·해금·메인 화면은 별도 단계로 둔다. 현재 결과 버튼과 에디터 소스 연결을 적용했다. 준비 실패/취소의 기존 Retry 보존과 네 해상도 화면을 검사했다. [실행 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-13-progress.md)과 [사용 안내](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-13-level-transition-usage.md)에 최종 상태를 남긴다.

## 공통 범위

기존 게임 규칙과 50레벨 단위 MemoryPack 저장 방식을 재사용한다. 새 광고·과금·상용 인벤토리·메인 화면·앱 종료 복구 기능은 추가하지 않는다. 기존 연결 대상이 없는 아이템 수량은 플레이 검증용으로만 다룬다. 생성된 2차 효과의 상세 연출 작업은 기본 게임 화면 완성 이후 별도 범위로 정한다.

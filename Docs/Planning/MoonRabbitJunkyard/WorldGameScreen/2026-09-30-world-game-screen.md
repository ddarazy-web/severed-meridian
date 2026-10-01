# 월드 보드 게임 화면 단계별 구현 계획

## 현재 상태

2026-10-01 최신 상태: **1~6단계 완료**. 6단계 스와이프·교환 연출은 Editor 검사 312개를 통과했다. [작업 계획](stage-06-swipe-swap-plan.md) · [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-06-swipe-swap-goal.md) · [검증 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-06-progress.md) · [사용 안내](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-06-swipe-swap-usage.md). 다음은 **7단계 제거·낙하·채움**이며 미시작이다. 사용자 최신 지시에 따라 **플레이어와 Addressables 콘텐츠 빌드를 모두 실행하지 않는다.** 이전 단계 문서의 빌드 절차는 이번 실행 권한이 아니다. 아래 초기 상태는 과거 기록이다.

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

## 후속 플레이 연출 (6~10단계)

| 단계 | 작업 | 완료 기준 |
| --- | --- | --- |
| 6 — 스와이프·교환 연출 | 손을 떼기 전 교환 확정, 점유자 미리보기·교환·무효 복귀, 입력/재생 경계 | 한 제스처 한 행동, 규칙 결과 보존, 중간 이동과 복귀가 보임 |
| 7 — 제거·낙하·채움 | 매칭 제거, SettlementRecord 기반 중력·대각선·경로·포털·공급, 착지와 다음 연쇄 | 실제 경로로 이동하며 표시 완료 후 다음 연쇄 진행 |
| 8 — 파워·장애물 연출 | 기존 로켓·드론·폭탄·자석 및 2차 효과 연결, 파워 조합·내구도 변화 | 공격 도착과 타격 표시가 맞고 효과 잔상이 없음 |
| 9 — 조작감과 진행 피드백 | 소리·진동, 미션 수집, 이동 수·연쇄 강조, 시작·승패·라스트팡 | 행동과 진행 결과가 명확히 전달됨 |
| 10 — 속도 조정과 안정화 | 연쇄 시간·효과 재사용, 정지·회전·다시하기·중단, Editor 성능 관찰 | 입력 잠금/표시 잔류/수명 누수 없이 반복 플레이 |

7~10단계는 순서와 범위만 정한 후속 로드맵이다. 구현 완료를 의미하지 않으며 앞 단계 결과를 반영해 각각 세부 계획·목표를 작성한다. 6단계는 기본 교환에 집중하고 교환 후 낙하/채움의 기존 즉시 갱신은 7단계까지 유지한다.

## 공통 범위

기존 게임 규칙과 50레벨 단위 MemoryPack 저장 방식을 재사용한다. 새 광고·과금·상용 인벤토리·메인 화면·앱 종료 복구 기능은 추가하지 않는다. 기존 연결 대상이 없는 아이템 수량은 플레이 검증용으로만 다룬다. 생성된 2차 효과의 상세 연출 작업은 기본 게임 화면 완성 이후 별도 범위로 정한다.

# 9단계 실행·검증 기록

상태: 5개 작업 및 15개 완료 조건의 Editor 검증·독립 최종 리뷰·수정 후 전체 재검사 완료 (2026-10-01). 빌드·커밋·푸시는 실행하지 않았다. 아래 초기·중간 기록은 당시 상태다.

계획: [5개 작업](../../../Planning/MoonRabbitJunkyard/WorldGameScreen/stage-09-progress-feedback-plan.md).

## 최종 검사 실적과 완료 조건 감사

| 실행 진입점 | 결과 | 로그 / 결과 파일 (`Logs/Stage09/` 기준) |
| --- | ---: | --- |
| `PuzzleProgressFeedbackVerification.Data` | 168 PASS, 0 FAIL, 종료 0 | `final-progress-data.log` / `final-progress-data-results.txt` |
| `PuzzleProgressFeedbackVerification.RunScene` | 181 PASS, 0 FAIL, 종료 0 | `final-progress-scene.log` / `final-progress-scene-results.txt` |
| `PuzzleSwapAnimationVerification.Run` | 251 PASS, 0 FAIL, 종료 0 | `final-swap.log` / `final-swap-results.txt` |
| `PuzzleSwapAnimationVerification.RunScene` | 26 PASS, 0 FAIL, 종료 0 | `final-swap-scene.log` / `final-swap-scene-results.txt` |
| `PuzzleSettlementAnimationVerification.Run` | 282 PASS, 0 FAIL, 종료 0 | `final-settlement.log` / `final-settlement-results.txt` |
| `PuzzleSettlementAnimationVerification.RunScene` | 106 PASS, 0 FAIL, 종료 0 | `final-settlement-scene.log` / `final-settlement-scene-results.txt` |
| `PuzzlePowerAnimationVerification.RunScene` | 852 PASS, 0 FAIL, 종료 0 | `final-power-scene.log` / `final-power-scene-results.txt` |
| `PuzzleEditorLaunchVerification.RunData` | 9 PASS, 0 FAIL, 프로세스 종료 0 | `final-editor-entry.log` / `final-editor-entry-results.txt` |

각 진입점 namespace는 `GameScreen.Editor`다. 9단계 새 검사 **349개**, 관련 회귀 **1,526개**를 구분한다. 서로 다른 실행의 실적이며 하나의 단일 실행 결과는 아니다. 초기 31/63/143/57/838 실적이나 이전 8단계 이력을 중복 합산하지 않는다. Editor RunData는 종료 코드만 믿지 않고 결과 파일의 FAIL 부재와 PASS 9줄을 확인했다.

| 목표 조건 | 판정 근거 | 증거 |
| --- | --- | --- |
| 1 보존 기준 | dirty 기준 기록. 드론 재생·시간표·낙하 수식·저장 DTO 변경 없음. 정착 파일에는 회수 시각 조회만 추가 | 소스 diff, 파워 852·낙하 282/106 |
| 2 전체 미션 근거 | 10종 실제 제거/소비/회수 및 발전기 미완충/완충 4종 경로에서 원점·본체·증가 확인 | Data 168, 아래 갱신 지점 표 |
| 3 실제 기록만 표시 | 실제 양의 진행 기록을 일회 소비. 실제 실행기 행동·조합·아이템·연쇄·회수와 직접 결과 비교 | Data 168, 씬 181, 파워 852 |
| 4 증가 0 | 거미줄 생존은 수집 0. 무효 교환 상태/이동 보존. 아이템은 가짜 이동 강조 없음 | Data, 스와이프 251/26, 씬 181 |
| 5 표시 수치 | 규칙 확정 후에도 도착 전 0 유지, 실제 도착 반영. 중복·포화·재소비 방지 | Data, 양 소스 실제 씬 반복 3회 |
| 6 최대 8·재사용 | 10개 묶음의 8개 슬롯 제한/대기/전량 도착. UI는 슬롯별 소유 이미지 재사용. 실제 수집 반복 시 개수 안정 | Data, 씬 181, HUD Frame 구현 |
| 7 완료 한 번 | 0/초기 완료/포화/다시 소비 및 실제 회수 2개·색 목표에서 완료 횟수 1 | Data, 씬 181 |
| 8 이동·연쇄 | 실제 감소 및 새 Matched 라운드에서만 강조. 무효/아이템/정착/동일 라운드에서 가짜 강조 없음 | Data, 스와이프 26, 아이템 씬 |
| 9 시작·라스트팡·결과 | 시작 입력 잠금·pause, 라스트팡 전후, 최종 반응 대기. 결과 제목을 바꾼 뒤 Refresh 3회에도 Show 미재호출 | 씬 181, 낙하 승리/이동 소진 106 |
| 10 pause | 수집 Progress·보드 위치·시작/종료 표시 시간 동결, 재개 후 완료 | Data, 씬 181, 관련 씬 회귀 |
| 11 화면·UI 입력 | 가로/세로 캡처, 비대칭 안전 영역 실제 아이콘 3px 이내 도착. UI raycast 양성인 위치에서 시작한 터치를 보드로 이동해도 선택/교환 없음 | 씬 181·스와이프 26, 도착 캡처 |
| 12 수명 | 실제 수집 중 취소, 로드 중 오류 후 pending 완료/AtlasCount 0, 정상 재시작, 이전 세션/HUD/수집 객체 파괴. 다시하기 5회·효과 준비 취소·씬 재진입 | 씬 181, 파워 852, 낙하 106 |
| 13 실제 소스·동등성 | Asset/MemoryPack 요청 사본을 실제 씬 Start 전에 전달. 초기 전체 상태와 수정 없이 읽은 입력을 확인하고 실제 교환 후 전체 상태/Phase/Outcome 비교. 원본 JSON 불변 | 씬 181, Editor 9, git 자산 diff |
| 14 회귀·캡처 | 6~8단계 영향받는 검사와 Editor 진입 검사 통과. 출발·중간·도착·완료·라스트팡·결과·시작 이미지 저장 및 시각 확인 | 위 실적 표, `Logs/Stage09/*.png` |
| 15 인계·책임 | 최신 실적과 초기 이력 분리, 사용 안내/목차·책임 표·미검증 항목 제공 | 이 문서, [사용 안내](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-09-progress-feedback-usage.md) |

판정은 현재 구현과 수행한 Editor 검증에 근거한다. 독립 최종 리뷰에서 지적한 문제는 아래 RED→GREEN 근거로 수정하고 전체 재검사를 통과했다. 플레이어/Addressables 콘텐츠 빌드·Android/iOS 실기기 터치/회전/성능은 미검증이다. 해당 범위는 빌드 금지 조건 때문에 실행하지 않았으며 Editor 증거로 대신했다고 표현하지 않는다.

Task 1: 실제 기록과 표시 수치 경계 구현·검증 완료 — 초기 기록/표시 RED→GREEN 및 최종 Data.

Task 2: 실제 시간표·회수 시각·8개 슬롯·UI 재사용·도착/완료·pause/회전/안전 영역 구현·검증 완료 — Data, 실제 씬, 비행 중간/도착 캡처.

Task 3: 이동/연쇄/시작 구현·검증 완료 — 실제 실행기 새 매칭, 중복 라운드/정착 부정 검사, 아이템 이동 수 보존, 실제 시작 pause·입력.

Task 4: 라스트팡/결과 준비 경계 구현·검증 완료 — 직접 실행기 결과 동등, 승리·이동 소진·마지막 수 회수, Refresh 중복 Show 방지.

Task 5: 실제 씬·소스·수명·회귀·캡처·인계 및 독립 최종 리뷰 수정 검증 완료 — 전체 조건 감사는 위 표를 따른다.

Ruling: 회귀 harness의 이동 수 HUD 대기 검사를 실제 소비 즉시 표시·강조 검사로 변경 — 9단계 요구를 따르며 미션 도착 전 수치는 계속 유지. 잘못 판단하면 교환 도중 이동 수 표시가 달라지는 UX 비용이 있다.

Ruling: 진행 중 아틀라스 로드의 안전한 반환 경계는 pending 완료 후로 검사 — 기존 소유권 구조를 유지하며 취소/오류 순간 사용 중인 핸들을 먼저 해제하지 않는다. 잘못 판단하면 로드 종료까지 일시 자원이 남는 비용이 있다.

Ruling: 무커밋 작업은 작업 트리와 신규 파일 전체를 최종 리뷰 대상으로 제공 — 사용자의 커밋 금지를 따르며 HEAD 간 빈 diff를 리뷰 근거로 쓰지 않는다. 잘못 판단하면 리뷰 범위 누락 위험이 있으므로 파일 목록과 목표 문서를 함께 제공한다.

## 독립 최종 리뷰와 수정 검증

리뷰 대상은 HEAD `075a29fbdccfa1c9cc602931c43e5a8af7cac965` 대비 작업 트리 변경과 신규 파일 전체다. `stage09_final_review`가 목표·계획·검증 기록 및 `Logs/Stage09/review-package.md`, `review-tracked.diff`, `review-new-files.txt`를 검토했다. 10단계 문서는 구현 리뷰 대상에서 제외했다. Critical 0, Important 3, Minor 1을 보고했으며 검사 수 오기는 완료 근거를 잘못 전달하는 문제로 Important로 재분류했다. 네 항목을 한 수정 패스에서 처리했다. 미뤄 둔 Minor는 없다.

| 지적 | 실제 실패 재현 | 수정·통과 근거 |
| --- | --- | --- |
| 직접 매칭 거미줄 제거는 반응 목록 없이 Changes에 기록되어 수치가 먼저 증가 | `review-runtime-red-results.txt`의 .12초 이전 수치 대기 FAIL | 실제 CoverBefore/After 제거 변경을 .12초에 연결. `review-data-results.txt`의 제거 전 대기·원점 비행·도착 후 완료 PASS |
| 이동을 소비하지 않는 아이템의 새 행동은 이전 연쇄 라운드 번호가 남음 | 같은 RED 파일의 아이템 새 라운드 강조 FAIL | executor.Turn 변화에서 라운드 초기화. 실제 Hammer 행동 후 새 라운드 강조·이동 강조 없음·중복 없음 PASS |
| 원본 Asset/MemoryPack 미션을 바꾸지 않은 최종 상태 증거 부족 | `review-source-red-results.txt`의 원본 맵 미션 유지 전체 동등 FAIL | 각 소스 실제 씬에서 원본 미션 유지 행동→직접 실행기 전체 상태/Phase/Outcome/HUD 비교 후 별도 제어 fixture 실행. `review-source-green.log` 및 `final-progress-scene-results.txt` 양 소스 PASS |
| 스와이프 검사 수 255 표기는 TRACE 4줄 포함 | `review-statistics-red.txt` declared 255 / actual 251 | `^PASS `만 집계, `review-statistics-green.txt` declared=251 actual=251. 최신 문서 표와 전체 결과 합계 일치 |

Final: fixed 직접 매칭 거미줄 — `ReviewWebMatch` RED→GREEN, 최종 8개 진입점 1,875 PASS / 0 FAIL.

Final: fixed 아이템 새 연쇄 — `ReviewItemCascade` RED→GREEN, 같은 전체 재검사 통과.

Final: fixed 원본 맵 최종 증거 — 실제 Asset/MemoryPack 씬 검사 RED→GREEN, 같은 전체 재검사 통과.

Final: fixed 검사 수 오기 — 실제 결과 파일 집계 RED→GREEN, 같은 전체 재검사 통과.

리뷰 수정 이후 `Logs/Stage09/run-final-suite.ps1`로 8개 진입점을 순서대로 실행했다. 프로세스 종료 0 및 각 결과 파일의 PASS/FAIL을 직접 재집계했다. `final-suite-results.txt`가 집계 원본이며 새 검사 349개(168+181), 관련 회귀 1,526개(251+26+282+106+852+9), 총 1,875개의 PASS 줄이다. 서로 다른 검사의 합계이며 고유 테스트 함수 1,875개 또는 실기기 검사 수를 뜻하지 않는다.

### 리뷰가 판단을 유보한 범위와 실행자 판단

Final: Ruling: Android/iOS 실기기 터치·회전·성능과 빌드는 미검증으로 유지 — 명시적인 빌드 금지 아래 Editor의 실제 UI raycast·레이아웃·pause·수명 검증까지 완료 — 잘못 판단하면 기기별 입력·성능 차이가 남을 수 있다.

Final: Ruling: 모든 파워 조합의 미션 비행 중간 프레임을 독립적으로 전수 검사했다고 주장하지 않음 — 4파워·10조합 실제 씬의 보드 타격·최종 표시 검증, 같은 재생 시간표의 예약 연결 소스 확인, 로켓·직접 매칭 거미줄·움직이는 회수의 도착 전후 검증으로 계획의 대표 사례 기준을 충족 — 잘못 판단하면 미검사 조합의 시각적 타이밍 차이를 놓칠 비용이 있다. 최종 진행 동등성과 미션 종류별 실제 기록 검사로 이를 중간 프레임 전수 검사라고 대체하지 않는다.

Final: Ruling: 10단계 문서와 무관 변경은 이번 구현 리뷰에서 제외 — 별도 사용자 요청의 문서 및 기존 작업을 보존하며 10단계를 실행하지 않음 — 잘못 판단하면 단계 간 후속 검토가 필요하다.

Final: Ruling: 작업 공간·검증 기록을 삭제하지 않고 미커밋 상태로 인계 — 사용자 커밋·푸시 금지에 따라 Git 이력으로 대체할 수 없는 증거와 변경을 보존 — 잘못 판단하면 로컬 기록이 누적되지만 증거 삭제보다 복구가 쉽다. `finishing-a-development-branch`의 통합 선택은 사용자가 이미 지정한 현 상태 유지로 처리한다.

### 마지막 보존 감사

`git diff -- Assets/Scenes Assets/Data Assets/Textures Packages ProjectSettings`는 비어 있다. 드론 경로·시간표 파일은 변경하지 않았으며 정착 표시기 diff는 회수 시각 조회와 Reset 정리뿐이다. 미션 갱신의 Math.Min 계산·규칙 난수·저장 DTO는 보존했다. 새 Unity 파일의 .meta 존재와 32자리 GUID 14개, 9·10단계 문서 8개의 링크, `git diff --check`를 확인했고 통과했다. 수집·도착·시작·라스트팡·결과 캡처는 `Logs/Stage09/`에 남기며 중간/도착 화면은 실제 HUD 목적지 검사와 함께 확인했다.

## 중간 진행 — 실제 HUD 연결 이후 (과거 기록)

- `PuzzleProgressFeedback`가 실제 기록을 미션별로 묶고 최대 8개 슬롯에서 0.32초 비행·0.15초 도착 강조를 처리한다. 모호한 원점/시간은 HUD만 반응한다. `PuzzleHudView.Feedback`가 기존 미션 이미지를 실제 월드 칸에서 HUD 아이콘으로 매 프레임 투영하며 UI 수집 프리팹을 재사용한다.
- 세션 `Progress` partial에서 준비 후 0.65초 시작 잠금, 실제 이동 감소 강조, 새 Matched 라운드 연쇄 표시, 라스트팡 시작/종료를 연결했다. 결과 패널은 `ResultReady`에서 보드·수집·HUD 정리 후 표시한다. 수집 대기를 보드 `IsPresenting`에 합치지 않았다.
- `PuzzleBoardSettlementPlayback.CollectionTime`은 실제 회수 기록의 이동 종료 시각을 제공한다. 기존 이동 시간·드론·낙하 계산은 변경하지 않았다. `PuzzleHUD.prefab`에는 수집 프리팹 참조만 추가했다.
- 재현·수정: 캐시된 효과 준비가 즉시 끝나는 교환 프레임에서 새 수집이 보드 매칭 제거보다 먼저 진행됐다. 실제 Update 검사가 `warm-swap-red-fixed.log`에서 실패했다. `Update`가 기존 수집 시간을 먼저 진행하고 이후 보드 재생/예약을 처리하도록 수정하여 `warm-swap-green.log`에서 통과했다.
- 회수 검사 fixture 보정: 부품을 도착점에 놓으면 실행기 생성 때 이미 회수·승리하여 이후 로켓 행동이 거절됐다(`mission-paths.log`). 도착점 바로 위에 부품을 두고 아래 블록을 실제 로켓으로 제거해 낙하 회수가 발생하도록 바꿨다. 생산 규칙은 변경하지 않았다. Data의 회수 검사는 실제 규칙 기록과 표시 도착 모델을 확인하며, 월드 회수 시각 통합 검증은 별도 실제 씬 검사 대상이다.

| 실행 진입점 | 확인 결과 | 증거 |
| --- | ---: | --- |
| `GameScreen.Editor.PuzzleProgressFeedbackVerification.Data` | 143 PASS, 0 FAIL, 종료 0 | `Logs/Stage09/mission-paths-green.log`, `mission-paths-results.txt` |
| `GameScreen.Editor.PuzzleProgressFeedbackVerification.RunScene` | 57 PASS, 0 FAIL, 종료 0 | `Logs/Stage09/warm-swap-green.log` (당시 결과 파일은 최신 검사로 덮어씀) |
| `GameScreen.Editor.PuzzlePowerAnimationVerification.RunScene` | 838 PASS, 0 FAIL, 종료 0 | `Logs/Stage09/power-scene-regression-green.log`, `power-scene-regression-results.txt` |

Data 143개에는 지원 미션 10종의 실제 제거/소비/회수 경로 및 살아남은 거미줄의 진행 0, 기록·중복·포화·풀 제한·표시 모델 검사가 포함된다. 씬 57개는 MemoryPack 시작, 실제 로켓 수집·도착·pause·회전·최종 공개 상태/승패 동등성, 시작 잠금, 다시하기 5회 및 캐시된 교환 타이밍을 다룬다. 파워 838개는 기존 8단계 회귀이며 9단계 새 검사 수에 합산하지 않는다.

캡처: `Logs/Stage09/collection-landscape.png`, `collection-portrait.png`, `mission-complete.png`, `last-pang.png`, `result.png`, `start.png`. 화면 이미지 확인을 수행했다. 실제 도착점과 안전 영역의 수치 검사·Asset 소유 진입·수집 중 취소/실패/재진입·반복 수집 재사용 및 6~7단계 회귀 검증은 아직 완료 근거가 충분하지 않다.

최신 남은 작업: 6~7단계 검사 harness를 실제 시작/진행 시간 경계에 맞추고 실행한다. 기존 ‘연출 중 이동 수 HUD 갱신 중단’ 검사는 9단계의 실제 이동 소비 즉시 표시·강조로 교체하되 미션 수집 전 수치 보존은 유지한다. 이어서 실제 Asset 진입, 회수·수명·안전 영역·조건별 증거와 사용 안내, 최종 리뷰를 완료한다. 15개 조건 전체 완료를 선언하지 않는다.

## 기준과 결정

- 시작 dirty: 9단계 계획·목표·명령문과 관련 목차·로드맵. 소스 변경은 없었다. 최신 드론 상승·대기·돌파와 낙하 시간 /1.44를 보존한다.
- Ruling: 현재 작업 공간에서 순서대로 직접 구현 — 최신 사용자 조정과 기존 연결을 보존하며 별도 구현 에이전트를 사용하지 않음 — 잘못 판단하면 변경 격리를 수동으로 관리하는 비용이 있다.
- Ruling: 실제 미션 갱신 지점에서 양의 증가만 기록하고 런타임 전용으로 유지 — 예측 기여량의 보호·포화·복수 미션 오차를 피하고 저장 DTO를 보존 — 잘못 판단하면 런타임 기록 메모리 비용과 표시 이력을 저장하지 않는 제약이 있다.
- 재현 검사: 동일 색 두 미션의 개별 증가, 포화 후 기록 없음, null 색 소비 없음, 실제 원점·본체 인덱스, 상태 사본 기록 독립성. `PuzzleProgressFeedbackVerification.Data`.

## 실제 진행 갱신 지점 조사

| 미션 | 결정 지점 | 원점 근거 | 표시 시각 연결 예정 |
| --- | --- | --- | --- |
| 색 | PowerEffectResolution의 매칭 소비·유효 제거, PowerCombinationResolution의 실제 변환 | 소비/타격/변환 칸 | 매칭 제거·실제 효과 도착·변환 |
| 상자·고물·회수캡슐·색 잠금·금속기둥 | ObstacleDamageRules.Remove, 발전기 연결 대상 간접 제거도 동일 경로 | 본체 배치 좌표와 본체 인덱스 | 본체 제거 반응 |
| 거미줄 | WebRules.Apply의 마지막 내구도 | 덮개 칸 | 덮개 제거 반응 |
| 먼지 | DustRules.ConsumeNormal의 마지막 내구도 | 바닥 칸 | 실제 일반 블록 소비/변환 |
| 곰팡이 | MoldRules.Remove | 덮개 칸 | 곰팡이 제거 반응 |
| 회수 | RecoveryRules.Collect | 실제 도착 칸 | 해당 정착 기록의 회수 |

시각 연결은 작업 2에서 검증하며 현재 완료 근거로 사용하지 않는다.

## 작업 1 — 실제 기록과 표시 모델

- `MissionProgressRecord`는 미션 인덱스·양의 증가량·실제 소비/제거/회수 좌표·본체 인덱스를 보존한다. `ConsumeColor`와 `Complete`의 기존 진행 계산은 유지하고 양의 증가가 있는 경우만 기록한다. 실제 호출부 8곳에서 원점을 전달한다.
- `LevelRuntimeState` 작업 사본은 불변 기록을 복사하되 누적 목록을 독립 소유한다. 저장 DTO·난수·피해 계산은 변경하지 않았다.
- `PuzzleMissionDisplay.Initialize/Collect/Arrive`는 실제 진행과 표시 진행을 분리한다. 같은 기록을 재수집하지 않고 도착 통지 시 수치를 반영한다. 현재 실제 세션·HUD·수집 비행에는 연결하지 않았으므로 작업 1 전체 완료로 판단하지 않는다.
- RED: `records-red.log`에서 ‘실제 미션 증가 기록 제공’, `display-red.log`에서 ‘미션 표시 진행을 실제 진행과 분리’가 의도한 실패였다.
- 검사 fixture 보정: 처음 만든 저장 레벨은 중복 미션 및 없는 상자 목표로 유효성 검사가 거절했다. `records-fixture.log`에서 확인했다. 저장 규칙을 우회해 실제 게임에 연결하지 않고, 런타임 생성자의 값 구성으로 중복 미션 단위 검사만 분리했다. 실제 로켓 행동 검사는 `LevelStateBuilder` 검증을 통과한 별도 레벨로 수행한다.

| 실행 진입점 | 실제 결과 | 증거 |
| --- | ---: | --- |
| `GameScreen.Editor.PuzzleProgressFeedbackVerification.Data` | 31 PASS, 0 FAIL, 종료 0 | `Logs/Stage09/records-action-green.log`, `records-action-results.txt` |
| `Levels.Editor.PowerEffectVerification.Data` | 96 PASS, 0 FAIL, 종료 0 | `Logs/Stage09/power-regression.log`, `power-regression-results.txt` |
| `Levels.Editor.CombinationVerification.Data` | 381 PASS, 0 FAIL, 종료 0 | `Logs/Stage09/combination-regression.log`, `combination-regression-results.txt` |

새 검사 31개는 지원 10종의 증가·포화, 중복 색 미션, null 색 소비, 원점·본체, 사본 독립성, 도착 전 수치 보존·재수집 방지와 실제 가로/세로 로켓의 진행·원점·원본 보존을 다룬다. 종류별 직접 완료 검사는 실제 종류별 피해/회수 경로의 통합 검증을 대신하지 않는다. 기존 규칙 회귀 477개는 9단계 화면 검증 실적으로 계산하지 않는다.

## 초기 구현 이후 당시 남은 작업 (과거 기록)

1. 작업 1의 `PuzzleMissionDisplay`를 세션 초기화·미션 HUD에 연결한다. 실제 변화 직후 수치가 먼저 뛰지 않도록 수집 예약과 도착 반영 경계를 마련한다.
2. 작업 2에서 실제 효과 시간표 및 정착 회수 시각에 수집을 예약하고 최대 8개 재사용 객체로 월드→실제 미션 아이콘 비행을 구현한다. 애매한 원점/시각은 가짜 비행 없이 HUD 반응만 사용한다.
3. 작업 3~4에서 실제 이동 소비·새 매칭 라운드·시작·라스트팡·결과를 연결한다. 수집 대기를 보드 `IsPresenting`에 합쳐 연쇄를 직렬 차단하지 않는다.
4. 작업 5의 실제 씬·Asset/MemoryPack·pause·회전·취소·다시하기 5회·최종 상태 동등성·캡처·사용 안내·최종 리뷰는 모두 미실행이다.

드론·낙하 표시 파일, 씬·프리팹·레벨·패키지는 이번 작업에서 변경하지 않았다. 활성 목표를 유지한다. 15개 전체 완료 조건을 확인하기 전 완료로 표시하지 않는다.

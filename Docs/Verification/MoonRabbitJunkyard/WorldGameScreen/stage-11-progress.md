# 11단계 실행·검증 기록 — 반복 플레이와 성능 안정화

상태: 작업1~4 및 완료 조건12개 감사 완료. 독립 리뷰 Important1건을 한 번의 수정 pass로 RED→GREEN 처리하고 수정 후 전체15개 회귀를 통과했다 (2026-10-02).

[계획](../../../Planning/MoonRabbitJunkyard/WorldGameScreen/stage-11-stability-plan.md) · [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-11-stability-goal.md)

## 실행 범위와 기준

ServeredMeridian 작업 트리의 최신 9~10단계 변경을 포함한 상태에서 검사한다. 커밋·푸시·플레이어/Addressables 빌드는 하지 않는다. 원본 레벨·씬·아이콘과 사용자 Unity를 보존한다. 별도 검사용 Unity 6000.3.10f1 Editor를 사용하며 검사용으로 직접 소유한 프로세스만 종료한다.

착수 증거: `Logs/Stage11/initial-status.txt`, `baseline-files.json`, `stage10-audit-at-start.txt`. Git HEAD는 `075a29fbdccfa1c9cc602931c43e5a8af7cac965`이다. 과거 10단계 실적을 새 11단계 검사 수로 합산하지 않는다.

## 작업 1

미구현 Data 진입점의 실제 Exit1과 결과 파일 부재를 확인한 뒤 Editor 검사를 추가했다. `task-1-green-results.txt`는 54 PASS / 0 FAIL, 프로세스 Exit0이다. 9×9·50레벨 팩, 현재 시간값·낙하/공급 /1.44, 최신 드론 소스, 14클립/8음성, 256×256 교환/섞기 아이콘과 원본 에셋 해시를 확인했다.

## 작업 2 중간 결과

첫 실제 RunScene 실행은 1684 PASS / 1 FAIL이었다. 실제 Asset/MemoryPack·파워/조합·아이템·회수·중첩 피해 및 반복 풀 사례 이후 화면 좌표 검사가 실패했다. 가상 ApplyLayout 크기만 지정하고 실제 Game View 크기를 바꾸지 않은 검사 전제 오류였다. 실제 크기를 전환하도록 검사만 변경했다.

경계 검사 재실행은 실제 화면 크기·입력·safe rect 검사를 통과했다. 이후 전체 고정 블록 맵에 유효한 시작 행동이 없는 fixture와 공급구 없는 fixture 때문에 무효 복귀/공급 관찰이 실패했다. 검사 맵의 시작 행동과 실제 상단 공급구를 보완했으며 제품 규칙을 변경하지 않았다.

회전의 기존 정책은 진행 중 제스처를 취소하고 선택·order를 복원하는 것이다. 이를 유지하고 잔류 없는 일관성으로 검증한다. 회전 중 손가락 제스처를 유지하는 새 동작은 추가하지 않는다.

실제 pending 검사는 미로드 효과의 `pending > 0`부터 단언한다. 준비 중 재시작·취소·오류 처리 경계와 늦은 완료 후 아틀라스/예약 반환을 확인한다. 오류는 검사에서 Fail 경계를 의도적으로 호출하는 방식이며 실제 배포 환경의 주소 누락이나 네트워크 실패를 재현했다고 주장하지 않는다.

## 검증 한계

경계/반복 검사는 Time.timeScale=0과 수동 표시 시계로 결정적 상태를 검사한다. 실제 Update의 성능은 작업 3에서 별도로 측정했다. 실제 청취 0회, 실기기 성능·출력 미검증이다. 최종 전체 회귀와 완료 조건 감사는 아래 최신 처리 기록을 따른다.

## 최종 검사 증거와 구분

리뷰 수정 전 15개 진입점의 결과는 [pre-review-suite-results.txt](../../../../Logs/Stage11/pre-review-suite-results.txt)에 있다. 진행 중 결과를 전체 성공으로 해석하지 않는다. 안정화 Data는 54 PASS, Scene은 2124 PASS(새 안정화 525 + 재사용 1599), 실제 Update 관찰은 74 PASS다. 새 안정화 단언은 총653개이며 Scene 안의 기존 검사 재실행과 별도 회귀를 섞어 고유 검사 수로 주장하지 않는다.

UI 상호작용 회귀의 이전 동기 Advance 루프는 현재 연출 잠금·수집 시간을 진행하지 못했다. fixture의 표시/수집 초기화와 실제 준비 대기·수동 표시 시계를 반영해 검사 코드만 수정했다. 실패 결과와 수정 후 97 PASS / 0 FAIL, Exit0을 `task-4-ui-regression-red-results.txt`, `task-4-ui-green-second-results.txt`에 보존했다. 이 UI 검사 보정에서는 runtime·프리팹·원본 데이터를 변경하지 않았다.

완료 감사에서 원본 맵의 한 행동 비교만으로 전체 판의 승패·다시하기를 주장할 수 없음을 확인하고 별도 검사를 추가했다. 비교용 실행기를 매 턴 재생성하면 CreatedOnTurn이 달라지므로 한 판에 하나의 독립 실행기를 유지하며 턴도 비교한다. [source-outcomes.csv](../../../../Logs/Stage11/source-outcomes.csv)는 Asset/MemoryPack 모두 시드12345, 11행동 후 Won/Stopped, 결과음1회, 같은 초기 상태로 다시하기와 원본 불변을 증명한다. 실패 승패는 별도 실제 이동 소진 fixture와 UI 결과 검사로 확인한다.

## 반복·리소스 관찰

[pool-observation.csv](../../../../Logs/Stage11/pool-observation.csv)는 드론/자석+드론 각각 워밍업5회와 추가5회의 용량을 기록한다. 추가5회에 용량 증가가 없고 종료 시 비사용 공급/효과/수집·음성 활성0을 단언했다. 상시 보드/UI 및 재사용용 비활성 객체는 남기는 것이 정상이다.

마지막 반복의 드론/다수 드론 용량은 cells81, bodies1, decorations1, effects248, collection2, voices8, clips14다. 공급 이미지/마스크는 드론65 → 다수 드론81로 필요한 최대 동시량만 확장한다. 아틀라스는 해당 fixture가 필요한 종류만 보유하며 재시작/종료 때 이전 소유권을 반환한다. 이 용량은 앞선 파워/장애물 사례로 확장된 같은 씬의 최대치이며 모든 레벨의 최소 사전 로드량이 아니다.

[pool-phase-observation.csv](../../../../Logs/Stage11/pool-phase-observation.csv)의 120개 행은 반복 중 파워·드론·제거·낙하/공급·수집의 총 용량과 활성 수를 구분한다. 효과·공급·HUD 이미지와 음성의 활성 수를 용량과 혼동하지 않는다. pending 중 재시작/취소/의도한 오류·씬 파괴와 실제 연출 중 다시하기5회도 확인한다.

## 리뷰 수정 전 실제 Update 관찰

이 관찰은 수동 표시 시계 검사와 별도 프로세스다. Time.timeScale=1과 활성 세션의 실제 Update, 중복 없는 NextFrame/LastPostLateUpdate 표본을 사용했다. 초기 준비·반복0 워밍업과 비교용 직접 실행은 측정에서 분리했다. 측정된 표본은 **19,342프레임**이다.

| 사례 | 프레임 | p50 ms | p95 ms | max ms | 최대 GC Alloc bytes | 최대 Main Thread ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 일반 연쇄 | 1461 | 0.601 | 0.791 | 17.900 | 647676 | 17.7832 |
| 공급 집중/로켓 | 1275 | 0.627 | 0.746 | 7.999 | 747702 | 8.0294 |
| 다수 드론/자석+드론 | 16606 | 0.569 | 0.707 | 20.223 | 4506623 | 20.2041 |

준비 시간은 약0.217~0.279초, 측정 행동 전체 시간은 일반0.940초/공급0.812초/다수 드론9.822초다. 입력 또는 결과 준비 시각은 약0.855/0.784/9.822초이며 남은 HUD 강조 시간과 입력 잠금을 구분한다. 수치는 [pre-review-action-observation.csv](../../../../Logs/Stage11/pre-review-action-observation.csv)에 있으며 `board_ms`는 준비를 포함한 보드 표시, `progress_only_ms`는 보드 표시가 끝난 뒤 수집/강조만 남은 표본 합이다. 게임 속도 개선 수치가 아니다.

[pre-review-observation-environment.txt](../../../../Logs/Stage11/pre-review-observation-environment.txt): Unity6000.3.10f1, Windows11, i5-13600K, RTX3060Ti, batchmode640×480, NCloud_Unit_CV 사용자 Editor 동시 실행 유지. Main Thread/GC Allocated In Frame/GC Used Memory recorder 모두 지원됐고 [pre-review-observation.csv](../../../../Logs/Stage11/pre-review-observation.csv)에 실제 값을 기록했다. CPU/GC는 이전 완료 프레임 값이어서 현재 표시 분류와 한 프레임 차이가 있다. 지원/표본 부재는 -1로 기록한다. 별도 GPU timing·객체별 할당 콜스택·실기기 FPS는 측정하지 않았다. recorder 수명/이전 프레임 값은 [Unity 공식 API 문서](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html) 기준으로 사용했다.

초기 관찰은 게임 입력 프레임에 비교용 직접 실행기·fixture 준비가 섞여 최대 할당이 과장됐다. 이를 분리한 관찰을 최종 증거로 사용하고 기존 값은 `probe-contaminated-*`에 남겼다. 이는 **검사 방식 보정**이며 제품 최적화 전후로 주장하지 않는다. 할당 피크와 연쇄 처리 시점은 여전히 존재한다. 현재 표본/소스만으로 특정 제품 결함의 원인과 수정 효과를 입증하지 못했으므로 광범위 캐시·풀·LINQ 변경이나 속도 조정은 하지 않았다. 반복 풀 안정성과 원본 상태 동등성은 별도 검사로 확인했다.

## 완료 조건별 감사

| 조건 | 근거 | 현재 판정 |
| --- | --- | --- |
| 1 기준 감사 | baseline-files.json, initial-status.txt, Data54, 10단계 감사 사본 | 확인 |
| 2 원본 입력/종료/재시작 | SourceChecks + source-outcomes.csv, 매 행동 전체 상태/Turn 동등 | 확인 |
| 3 각 표시/입력 경계 | swap/return/removal/fall/supply/power/drone/collection/lastpang 전부 관찰·종료 입력/결과 | 확인 |
| 4 규칙 결과 | 기존 실제 4파워/10조합/2×2 내구도9/3아이템/회수/연쇄의 State·Phase·Outcome 재실행 | 확인 |
| 5 중단 | 각 단계 pause/resume 시계·새 음성 정지, background 과거 예약0, 미래 Win/Lose·중복0 review 사례 및 background 결과완료 실제 Refresh/복귀4사례 | 확인 |
| 6 수명 | 실제 표시 중 다시하기5회, pending>0 전제 후 재시작/취소/오류/파괴·늦은 완료·새 입력 | 확인 |
| 7 화면 전환 | 실제1920×1080/1080×1920, inset 입력 좌표/order 복원/수집3px 도착·도착 후 증가 | 확인 |
| 8 재사용 | 5+5 반복2종·120개 단계별 관찰·종료 활성0·8음성/14클립·씬 반환 | 확인 |
| 9 성능 | 실제 Update 리뷰전19342/수정후19131프레임, 세 사례·준비/보드/수집/입력·CPU/GC/heap·환경/한계 | 확인 |
| 10 최소 변경 | 검사 전제/기존 UI 검사 보정·재현 결과음2파일 최소 수정, preservation-audit.txt 73개 중 결과음 수정2파일·나머지71개 불변 | 확인 |
| 11 회귀 | 최종15개 빌드 없는 진입점, 새 결과·프로세스 종료 | 확인 |
| 12 문서/리뷰 | 본 조건별 기록·사용 안내, 독립 리뷰 Important1 RED→GREEN+수정후15회귀 | 확인 |

실제 UI 회귀는 pause/아이템/승리/실패 화면을 촬영한다. 현재 `Logs/PuzzleUIVerification/won.png` 한 장을 직접 열어 실제 이미지 보드·결과 패널·수정 아이콘을 확인했다. 전체 해상도·움직임 품질의 사람 검수 완료나 동영상 촬영을 주장하지 않는다. 실제 청취0회, 실기기0회이며 빌드·커밋·푸시·12단계 실행은 하지 않았다.

### 리뷰 수정 전 빌드 없는 회귀 상세

아래는 리뷰 수정 전 전체 실행의 결과이며 FAIL0·프로세스 Exit0이다. 별도 기존 회귀 단언은 3607개다. 안정화 Scene 안의 재사용1599개와 겹치는 영역이 있으므로 고유 검사 총수로 합산하지 않는다.

| 진입점 | PASS | FAIL | Exit |
| --- | ---: | ---: | ---: |
| stability-data | 54 | 0 | 0 |
| stability-scene | 2124 | 0 | 0 |
| stability-observe | 74 | 0 | 0 |
| ui-layout | 18 | 0 | 0 |
| ui-interaction | 97 | 0 | 0 |
| audio-data | 26 | 0 | 0 |
| audio-scene | 1591 | 0 | 0 |
| progress-data | 168 | 0 | 0 |
| progress-scene | 181 | 0 | 0 |
| swap | 251 | 0 | 0 |
| swap-scene | 26 | 0 | 0 |
| settlement | 282 | 0 | 0 |
| settlement-scene | 106 | 0 | 0 |
| power-scene | 852 | 0 | 0 |
| editor-entry | 9 | 0 | 0 |

2026-10-02 리뷰 전 재감사: baseline SHA256 73개 변경0, 신규 Stability 검사5개와 각각의32자리 GUID .meta 유효, git diff --check 통과. 이 감사 시점의 제품 runtime·레벨·씬·프리팹·아이콘은 착수 시점과 동일했다. 이전9~10단계의 기존 dirty 변경은 그대로 유지한다.


## 독립 최종 리뷰와 한 번의 수정 pass

[독립 리뷰](../../../../Logs/Stage11/final-review.md): Critical0 / Important1 / Minor0. 리뷰어는 전체 tracked/untracked 변경과 실제15개 결과·pool/프레임 자료를 읽었다. Important는 결과 표시가 백그라운드에서 완료되면 audioOutcome과 화면 shownOutcome이 소리 없이 소비돼 복귀 후 결과음이 영구 누락되는 문제다. 이전 검사는 백그라운드에서 먼저 복귀한 뒤 결과를 완료해 이 경계를 놓쳤다.

`final-review-red-actual-results.txt`는 실제 결과 Refresh 완료·백그라운드 결과음0 후 복귀 Win1 기대에서 PASS9/FAIL1, 실제Exit1이었다. 신규 검사 최초의 enum 컴파일 오타는 별도 로그로 남겼고 제품 재현 실패로 세지 않았다.

Feedback.cs에서 background/pause 중 결과음을 소비하지 않고 foreground에서 미소비 결과음을 재시도한다. Controls.cs는 pause 재개 시 미소비 결과음만 전달한다. 타이밍·게임 규칙·이미지·원본은 바꾸지 않았다. `final-review-green-results.txt`는 승리/이동소진 실패 × pause겹침 유무4사례에서 실제 결과 Refresh→복귀1회→추가 Refresh/재복귀0, 전체 상태 동등을 확인해 PASS86/FAIL0/Exit0이다. 이 수는 baseline 등 중복 단언을 포함하며 전체 회귀의 고유 검사 수로 더하지 않는다. 수정 후 전체15개 진입점은 모두 PASS>0/FAIL0/실제Exit0으로 종료했다.

리뷰의 판단 제외 항목: 청취0회인 실제 음질, 기기0회인 FPS/메모리/출력·빌드, 실제 네트워크/배포 주소 장애,12단계 화면 미적 품질/움직임 사람 검수, UI 미연결 SoundEnabled setter의 pause중 외부 호출. 각각 범위를 확장하지 않고 미검증 또는 호출자 책임 한계를 유지한다. Minor 보류 없음, 독립 재리뷰 없음. 사용자 커밋 금지에 따라 현재 작업 트리와 실행 증거를 보존한다.


## 리뷰 수정 후 실제 Update 재관찰

수정 후 전체 회귀 안에서 Observe를 새로 실행해 PASS74/FAIL0/Exit0을 확인했다. 준비·워밍업을 제외한 실제 고유 프레임은 **19,131개**다. [최신 observation.csv](../../../../Logs/Stage11/observation.csv), [행동 구간](../../../../Logs/Stage11/action-observation.csv), [측정 환경](../../../../Logs/Stage11/observation-environment.txt)을 사용한다.

| 사례 | 프레임 | p50 ms | p95 ms | max ms | 최대 GC Alloc bytes | 최대 Main Thread ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 일반 연쇄 | 1486 | 0.601 | 0.736 | 18.136 | 647676 | 18.0607 |
| 공급 집중/로켓 | 1244 | 0.641 | 0.755 | 8.103 | 747702 | 8.0344 |
| 다수 드론/자석+드론 | 16401 | 0.576 | 0.717 | 20.229 | 4506623 | 20.1654 |

준비0.217~0.280초, 행동0.938/0.812/9.823초, 입력 또는 결과 준비0.854/0.784/9.823초다. 리뷰 전 행동0.940/0.812/9.822초와 같은 환경·fixture·시드에서 재관찰했고 타이밍 소스는 변경하지 않았다. 프레임수/분위수의 작은 차이는 Editor 관찰 편차로 기록한다. 결과음 소유권 수정은 성능 최적화가 아니며 이 값으로 속도 개선을 주장하지 않는다. GC 최대치도 동일하다. 이 측정은 정상 플레이의 회귀 관찰이고 background 복귀의 동작은 별도 결정적 재현 검사로 증명한다.



## 최종 완료 감사 — 수정 후 전체 회귀

[최신15개 실제 결과](../../../../Logs/Stage11/final-suite-results.txt)는 stability-data54, stability-scene2170, stability-observe74, ui-layout18, ui-interaction97, audio-data26, audio-scene1591, progress-data168, progress-scene181, swap251, swap-scene26, settlement282, settlement-scene106, power-scene852, editor-entry9 PASS이며 모든 FAIL0/Exit0이다. 전체 runner도 Exit0으로 끝났다. Scene2170은 새571+재사용1599다. 새 안정화 단언은 **699개**(54+571+74), 별도 기존 회귀 재실행은 **3607개**로 구분한다. 중복된 검사를 고유 총수로 합산하지 않는다.

baseline73개 중 재현한 결과음 소유권 수정의 Feedback.cs/Controls.cs2개만 달라졌고 나머지71개는 SHA256 불변이다. 레벨·씬·프리팹·아이콘/GUID는 모두 불변이며 최신 속도/드론/낙하 소스도 보존했다. 신규 검사5개 .meta유효·문서참조 유효·컴파일 오류0·git diff --check 통과, HEAD도 착수075a29f와 동일하다. 원본 Asset/MemoryPack 판 종료·다시하기 기록과 120개 풀 단계 관찰도 이번 수정 후 전체 실행에서 다시 생성했다.

[조건별 증거 감사](../../../../Logs/Stage11/requirement-audit.md)와 본문의12행을 확인했다. 독립 리뷰는 한 번, 중요 결함 수정 pass도 한 번이다. 수정 후 전체 suite로 회귀를 검증했고 재리뷰·보류Minor는 없다. 제품 성능 최적화나 연출 속도 변경은 하지 않았다. 사용자 Unity·무관dirty·원본을 보존하며 빌드/커밋/푸시/12단계 구현은 하지 않았다. 실제 청취0회·기기0회·네트워크 장애 재현0회, 화면/움직임 사용자 품질 검수 미수행은 명시된 한계로 유지한다.

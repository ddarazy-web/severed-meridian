# 12단계 실제 게임 화면·연출 품질 검수 Implementation Plan

> 실행 스킬: `superpowers:executing-plans`로 아래 3개 작업을 순서대로 직접 수행하고 독립 최종 리뷰를 한 번 진행한다. 사용자 요청에 따라 빌드·커밋·푸시는 하지 않는다.

**Goal:** 실제 Game View와 목업을 비교하고 화면·움직임의 식별성을 검수해 재현된 표시 결함만 수정한다.

**Architecture:** 기존 세션·월드 보드·HUD·기능별 프리팹을 유지한다. Editor 전용 검사가 기존 Game View 해상도 설정과 실제 씬을 사용해 증거를 수집한다. 제품 변경은 확인한 결함의 소유자에 한정한다.

**Tech Stack:** Unity 6000.3.10f1, C#·UniTask·uGUI·SpriteRenderer·기존 Addressables/MemoryPack. 새 패키지 없음.

**Spec:** [12단계 목표·10개 완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-12-presentation-goal.md).

상태: 2026-10-02, 진행 중. Task 1~2 완료 기록, Task 3 회귀·최종 리뷰 진행 중. 문서 작성 후 기존 활성 목표를 이어서 실행하고 있다.

## 재개 순서와 현재 남은 작업

전체는 기존 3개 작업을 유지한다. 아래는 재개 순서의 기록이며 현재 남은 작업은 Task 3이다. 완료된 촬영을 이유 없이 반복하거나 13단계를 먼저 만들지 않는다.

1. **Task 2: 움직임 검사 복구와 실제 연출·입력 검수.** `Logs/Stage12/task-2-motion.log`에 `PuzzlePresentationAcceptanceVerification.Motion.cs(12,7)`의 CS0246이 기록돼 있다. 해당 using과 실제 타입 소유 네임스페이스를 확인해 검사 코드만 최소 수정하고 컴파일·RunMotion을 재검증한다. 네임스페이스 수정만으로 통과했다고 보지 말고 기존 입력/fixture 전제도 확인한다. 18사례(4파워·10조합·유효 교환·무효 복귀·라스트팡·2×2 피해)의 실제 Update 기록과 연속 렌더를 관찰하고 직접 실행기와 비교한다. 선택 order, UI 입력 차단, 드론/수집 중 대상 소멸·회전, pause/재개·다시하기도 별도로 확인한다. 정지 PNG만으로 움직임 관찰을 대체하지 않는다.
2. **Task 3: 회귀와 최종 완료 감사.** Task 2 통과 후 기존 빌드 없는 회귀와 background/pause 결과음을 새로 검사한다. 기준83파일의 보존을 감사하고, 독립 최종 리뷰 한 번과 10개 완료 조건별 근거·사용 안내를 남긴다. 실제 청취와 기기 검증은 수행 여부를 구분한다.

Task 1 근거: `Logs/Stage12/task-1-capture-safe-results.txt` PASS237/FAIL0, `task-1-data-results.txt` PASS146/FAIL0; 실제20장과 manifest, `capture-observations.md`, 실행 ledger에 완료 기록이 있다. 착수 전 파일과 현재 코드의 일치 여부를 다시 확인한다. 화면/규칙 변경이 있으면 영향받은 촬영만 재검증한다. 전체 단계 완료와 이 문서 작성 완료는 구분한다.

시작 근거: [11단계 완료 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-11-progress.md). 새 안정화699개·별도 회귀3607개, 실제 Update19131프레임 관찰을 완료했으나 실제 청취와 전체 화면·움직임 품질 검수는 남아 있다. 이 단계는 그 화면·움직임 검수를 담당한다.

## Global Constraints

목표 문서의 보존·제외 기준 전체를 적용한다. 최신 9×9·크기·스와이프 order·2×2 피해·낙하/공급 /1.44·신규 대각선·드론·미션·14클립/8음성/.06·아이콘/GUID·50레벨 팩·선택 아틀라스를 보존한다. 진동·새 기능·빌드·커밋·푸시·사용자 Unity 종료/자동 저장·다음 목표 자동 진행 없음.

11단계에서 수정한 결과음 소유권도 보존한다. background/pause 중 미소비 결과음을 유지하고 복귀/재개 때 한 번 전달하며 이미 재생한 결과는 중복하지 않는다.

11단계 미완료이면 12단계 Goal·구현을 시작하지 않는다. 실제 렌더가 가능한 ServeredMeridian Editor를 사용한다. `-nographics` 또는 무음 자동 검사로 화면·움직임·청취를 확인했다고 주장하지 않는다. 사용자의 열려 있는 씬을 검사 씬으로 덮어쓰지 않는다.

## 파일과 책임

| 경로 | 책임/변경 여부 |
| --- | --- |
| Docs/MoonRabbitJunkyard/Mockups/puzzle-screen.html 및 preview*.png | 비교 자료, 수정하지 않음 |
| Assets/Scripts/Features/GameScreen/Editor/Tests/PuzzleUIRenderVerification.cs | 기존 SetSize/RememberSize/RestoreSize 재사용, 기존 출력과 검사는 보존 |
| Assets/Scripts/Features/GameScreen/Editor/Tests/PuzzlePresentationAcceptanceVerification.cs (신규 예정) | Data/RunScene 진입점, 결과·manifest, 씬 수명 |
| 같은 폴더/PuzzlePresentationAcceptanceVerification.Capture.cs (신규 예정) | 실제 렌더 촬영·상태 fixture·해상도·안전 영역·픽셀 검증 |
| Runtime/UI/PuzzleScreenLayout.cs, PuzzleScreenView.cs, PuzzleHudView*.cs | 레이아웃·팝업·수집 담당. 재현 결함에 한해 수정 |
| Runtime/World/PuzzleWorldBoard.cs, PuzzlePowerPlayback*.cs, PuzzleBoardSettlementPlayback.cs | 보드/연출 담당. 재현 결함에 한해 수정 |
| Runtime/Session/PuzzleGameSession*.cs, Runtime/Feedback/PuzzleAudioPlayback.cs | 입력/수명/소리 담당. 재현 결함에 한해 수정 |
| Assets/Prefabs/UI/Puzzle, Assets/Prefabs/Game/Puzzle | 기능별 프리팹 유지. 결함 관련 필드만 변경 |

새 Editor 검사에는 GUID가 유효한 .meta를 함께 생성한다. 단일 파일로 충분하면 Capture partial을 만들지 않는다. 기존 단계의 fixture/직접 실행기를 재사용하며 공개 런타임 진단 API·전역 Manager를 만들지 않는다.

## Review Focus

1. 촬영 API가 반환했지만 PNG가 아직 저장되지 않은 경우: 실제 파일·픽셀을 기다리고 검사한다(Task 1).
2. 임의 ApplyLayout 값과 실제 Game View 크기가 달라 보드만 잘린 경우: 실제 렌더 크기와 입력 좌표를 함께 검증한다(Task 1).
3. 정지 장면에서는 정상이나 드론/수집 중 대상이 사라지거나 회전한 경우: 실제 움직임과 최종 상태를 확인한다(Task 2).
4. UI 때문에 눌림이 가려지거나 한 동작이 두 행동이 된 경우: EventSystem 경로와 이동 수/실행기 결과로 검증한다(Task 2).
5. 화면 수정을 위해 최신 속도·아이콘·원본이나 복귀 결과음이 달라진 경우: 착수 해시·timing·규칙 및 background 결과음 회귀와 비교한다(Task 3).

## Task 1 — 완료 기준 감사와 실제 화면 촬영

**Interfaces:** 신규 `public static void Data()`는 보존/manifest 검사를 기록한다. 신규 `public static void RunScene()`은 실제 Play Mode 촬영·통합 검사를 수행한다. 출력은 `Logs/Stage12/data-results.txt`, `scene-results.txt`, `capture-manifest.csv`; 자동 실행 시 PASS>0/FAIL0/실제 종료0이 필요하다. 기존 `PuzzleUIRenderVerification.SetSize(int,int)`와 RememberSize/RestoreSize를 재사용하고 finally에서 이전 Game View 설정을 복원한다.

- [x] 11단계 목표/실행 기록/로그/현재 소스의 완료 여부를 감사한다. 미완료이면 남은 조건을 보고하고 착수하지 않는다.
- [x] 착수 dirty·레벨/씬·아이콘 SHA256/GUID·timing을 `Logs/Stage12/baseline` 증거로 저장한다. 사용자 Editor·현재 씬은 보존한다.
- [x] 미구현 진입점 또는 저장되지 않은 촬영을 통과시키는 문제의 실패 근거를 확보한다. 실제 PNG가 없거나 픽셀 크기가 다르면 실패하도록 작성한다.
- [x] 원본 사본/MemoryPack과 기존 승패 fixture를 실제 세션에서 실행한다. 네 해상도×네 화면 상태 16장, 네 inset 플레이 4장을 실제 렌더로 저장한다. UI 상태값만 강제로 바꿔 승패가 발생한 것처럼 꾸미지 않는다.
- [x] manifest에 입력/레벨/시드/상태/해상도/시각/파일을 기록한다. 저장 완료·디코딩 가능·픽셀 해상도·빈 화면 여부와 UI/보드 입력을 검증한다. 생성한 PNG를 모두 실제로 열어 목업과 비교한다.
- [x] Data/RunScene을 실행해 새 결과·FAIL0·실제 종료0을 확인한다. 기존 촬영 검사의 “captures requested” 문자열만으로 완료하지 않는다. 촬영 불가이면 미수행으로 남긴다.

## Task 2 — 실제 움직임·식별성·효과음 검수와 최소 수정

**Interfaces:** Task 1의 선택 맵·시드·actual session과 기존 직접 실행기를 소비한다. 기존 Swap/Activate/아이템/UI 입력 경로를 사용한다. 기존 초안 `PuzzlePresentationAcceptanceVerification.Motion.cs`의 `public static void RunMotion()`을 복구한다. 결과는 `Logs/Stage12/motion-results.txt`, `motion-manifest.csv`, `motion-summary.csv`, `motion-observations.md`와 사례별 전후 증거이며 제품 API를 추가하지 않는다. PASS>0/FAIL0/실제 종료0과 관찰 근거가 모두 필요하다.

- [x] 기존 CS0246 로그를 실패 근거로 보존하고 실제 타입 선언을 확인해 잘못된 using을 최소 수정한다. `Logs/Stage12/run-check.ps1 -Method RunMotion -Label task-2-motion-resume -Result Logs/Stage12/motion-results.txt`로 재실행한다. 실패하면 검사 전제와 제품 결함을 구분해 해결하며 결과가 없는 실행은 통과로 세지 않는다.

- [x] 교환/무효 복귀·제거/동시 낙하/신규 공급·4파워/10조합·수집/라스트팡/결과 전이를 실제 Game View에서 관찰한다. 4파워·10조합은 기존 검증 fixture를 재사용하고 사례별 판정을 기록한다.
- [x] 블록과 파워, 2×2 이미지·내구도, 미션 수치와 교환/섞기 아이콘을 플레이 배율에서 확인한다. 드론 표적을 미리 노출하지 않고 최신 상승/선회/돌파를 유지한다.
- [x] 드론·수집 중 대상 소멸/회전, UI 위 눌림, pause/다시하기를 실제 입력으로 확인한다. 최종 전체 상태·이동 수·승패는 직접 실행기와 비교하고 한 동작의 중복 실행을 검사한다.
- [x] 실제 청취 가능한 출력이 있으면 대표 행동·밀집·pause/복귀·결과음을 듣고 환경/사례를 기록한다. 불가능하면 청취0회·음질 미검증으로 기록하고 재생 수/예약 검사를 대체 청취로 주장하지 않는다.
- [x] 결함은 재현 시점·전후 이미지 또는 실제 관찰 기록을 남긴 뒤 실패 검사→담당 소유자 최소 수정→통과로 처리한다. 단순 선호에 따른 전면 디자인·속도 변경·이미지 재생성은 하지 않는다. 결함이 없으면 제품 코드는 유지한다.

## Task 3 — 회귀·최종 리뷰·완료 조건 감사

**Files:** 실행 후 `Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-12-progress.md`, `Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-12-presentation-usage.md`; 관련 목차/로드맵.

- [x] 변경 영향에 따라 기존 빌드 없는 진입점을 실행한다: PuzzleUILayoutVerification.Run, PuzzleUIInteractionVerification.Run, PuzzleEditorLaunchVerification.RunData 및 관련 교환/낙하/파워/미션/효과음/11단계 검사. 각 메서드의 내부 빌드 호출 여부를 먼저 확인한다.
- [x] `PuzzleStabilityVerification.ResultBoundaries` 또는 이를 포함하는 `RunScene`으로 승리/실패 × pause겹침 유무의 실제 background 결과완료→복귀음1회→추가 Refresh/재복귀0회를 재검증한다. 관련 세션/UI 변경 시 생략하지 않는다.
- [x] 이번 실제 실행의 PASS/FAIL/Exit·시각을 기록하고 과거 단계 실적을 새 검사 수로 합산하지 않는다. 원본·GUID·latest timing·규칙·무관 dirty 보존을 감사한다.
- [x] 전체 변경을 독립 최종 리뷰 한 번으로 검토하고 Important 이상은 재현→최소 수정→관련 회귀로 확인한다. 촬영 파일과 manifest의 일치 및 실제 관찰 여부도 리뷰한다.
- [x] 완료 조건10개별 증거, 목업 대비 의도된 차이, 수정 전후, 미청취/실기기 미검증, 사용 안내를 작성한다. 필수 화면·움직임 검수를 못 했거나 표시 결함이 남으면 완료로 표시하지 않는다.
- [x] 모든 조건 감사 후에만 Goal을 완료한다. 빌드·커밋·푸시·13단계 자동 착수 없이 현재 증거와 변경을 보존한다.

## 실행 시 검증 방법

Editor 검사 진입점은 `GameScreen.Editor.PuzzlePresentationAcceptanceVerification.Data`와 `.RunScene`이다. 데이터 검사는 별도 Editor에서, 촬영·움직임은 실제 그래픽 출력이 가능한 Editor에서 실행한다. 설치 Unity 경로와 사용자 Editor 상태는 착수 시 확인한다. 소유한 검사 프로세스만 종료하고 기존 사용자 프로세스는 종료하지 않는다. 이 문서 작성 과정에서는 어떤 검사나 빌드도 실행하지 않는다.

## 2026-10-02 최종 리뷰 조치·검증 완료

독립 리뷰 한 번의 Important1은 Game View 목록/선택 snapshot·추가 항목 추적·absolute 삭제·cleanup 실패 전파로 수정했다. RED Exit1→GREEN3PASS/Exit0, 세 실제 finally 성공과 negative cleanup Exit1을 확인했다. Minor1은 -Result 안내로 처리했다. 최종17개 회귀·보존 gate 종료0, 추가 InsetUI67PASS/Exit0이며 조건10개를 감사했다. 신규 Editor partial6개 외에 정확한 inset UI 경계를 검사하는 PuzzleInsetUIBoundsVerification.cs와 meta를 추가했다. 게임 런타임/이미지/프리팹은 유지했다. 13단계는 자동 진행하지 않는다.

# 3단계 레벨 에디터에서 게임 실행 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:executing-plans`로 작업별 구현·검증한다. 이 계획은 병렬 하위 에이전트 구현을 요구하지 않는다. 체크박스는 실제 검증 뒤 갱신한다.

**Goal:** 편집창의 선택 레벨을 독립 사본으로 게임 씬에서 실행하고 원래 편집 환경으로 복귀한다.

**Architecture:** Editor 실행기가 입력 bytes와 실행 설정을 SessionState에 보관하고 Play 시작 씬 override를 관리한다. 게임 씬의 sceneLoaded에서 요청을 한 번 소비해 기존 세션의 사본 초기화를 호출한다. 게임 규칙과 아틀라스 수명은 기존 세션이 계속 소유한다.

**Tech Stack:** Unity 6000.3.10f1, UI Toolkit(Editor), UniTask, MemoryPack, Addressables 4.1.0, Input System 1.18.0. 신규 패키지는 없다.

**Spec:** [목표 및 완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-03-editor-launch-goal.md). 상태: 구현·검증 완료 (최종 자동 확인 202 PASS).

## Global Constraints

- `C:/Projects/Git/ServeredMeridian`만 사용하고 기존 변경·`.meta/GUID`를 보존한다. 자동 커밋하지 않는다.
- 원본 레벨·편집 씬의 자동 저장/전체 교체, 팩 자동 갱신, 런타임의 UnityEditor 참조를 금지한다.
- 50레벨 MemoryPack과 Addressables 아틀라스 분할, 기존 1·2단계를 유지한다.
- 현재 프로젝트가 열린 동안 별도 Unity 배치를 실행하지 않는다. 다른 프로젝트의 Editor는 조작하지 않는다.
- 계획의 신규 파일이 이미 존재하면 내용을 조사하고 재사용한다. 4단계는 시작하지 않는다.

## Review Focus

1. `ToPacked/FromPacked`의 얕은 복사로 플레이가 원본을 변경하는 경우: 작업 1의 독립 사본·해시 검사.
2. 도메인 리로드로 요청이 사라지거나 기본 Start가 먼저 실행되는 경우: 작업 2의 실제 초기화 횟수 검사.
3. 누락된 팩을 에셋 또는 기본 레벨로 대체하는 경우: 작업 1·3의 실패 입력 검사.
4. 사용자의 미저장 씬/시작 씬 override를 잃는 경우: 작업 2·4의 취소·복귀 검사.
5. Play 종료 후 편집 이미지가 다시 색 상자로 남는 경우: 작업 4의 기존 수명 검사와 실제 복귀 캡처.

## 작업 1 — 입력 스냅샷과 세션 초기화

**Files:**
- 신규 `Assets/Scripts/Features/GameScreen/Editor/Launch/PuzzleEditorLaunchRequest.cs`
- 수정 `Assets/Scripts/Features/GameScreen/Runtime/Session/PuzzleGameSession.cs`
- 신규 `Assets/Scripts/Features/GameScreen/Editor/Tests/PuzzleEditorLaunchVerification.cs`

**Interfaces:** Editor 전용 `PuzzleEditorLevelSource { Asset, MemoryPack }`; `PuzzleEditorLaunchRequest.Capture(LevelDefinition source, PuzzleEditorLevelSource mode, int seed)`가 bytes·레벨 번호·시드를 소유한 요청을 반환한다. `LevelDefinition CreateDefinition()`은 `LevelPackCodec.ReadLevel`로 새 사본을 반환한다. 세션의 `UniTask InitializeAsync(LevelDefinition ownedDefinition, int randomSeed, CancellationToken token)`은 원본 에셋이 아닌 사본의 소유권을 받는다.

- [x] 원본에 저장값과 다른 이동 수를 적용한 fixture로 `AssetSnapshotIncludesUnsavedChanges`, `PackSnapshotUsesGeneratedBytes`, `SnapshotDoesNotShareCollections` 검사를 먼저 작성하고 실패를 확인한다. bytes 캡처 후 원본/팩을 바꿔도 요청이 변하지 않아야 한다.
- [x] 에셋은 `LevelPackCodec.Encode(new[] { source })`, 팩은 `File.ReadAllBytes(LevelPackBuild.FilePath(source.LevelNumber))`를 사용한다. Capture에서 선택 번호를 ReadLevel로 검증하고 검증에 쓴 사본은 즉시 해제한다. 예외는 호출자가 표시하며 대체 입력을 만들지 않는다.
- [x] 기존 번호 초기화와 사본 초기화가 같은 PrepareAsync/실패/취소/반환 경로를 사용하게 한다. 시작 전에 started를 설정하여 기본 Start와 중복 초기화를 차단한다. 소유권을 넘기기 전에는 Editor 측이, 넘긴 후에는 세션이 사본을 정확히 한 번 반환한다.
- [x] `MissingPackRejected`, `CorruptPackRejected`, `MissingNumberRejected`, `SnapshotMatchesDirectExecutor`, `SnapshotDestroyedOnExit`를 검사한다. 파일 사례는 검사 전용 경로/사본으로 구성하여 실제 생성 팩을 훼손하지 않는다.
- [x] `PuzzleEditorLaunchVerification.RunData` 결과를 `Logs/PuzzleEditorLaunchVerification/data-results.txt`에 남긴다. 기존 `PuzzleGameplayVerification.Run` 회귀도 확인한다.

완료 증거: 입력 사본·기존 실행기 결과 일치·원본 보존·실패와 취소 검사.

## 작업 2 — Play 진입·요청 소비·복귀

**Files:**
- 신규 `Assets/Scripts/Features/GameScreen/Editor/Launch/PuzzleEditorLauncher.cs`
- 신규 `Assets/Scripts/Features/GameScreen/Editor/Tests/PuzzleEditorLaunchLifecycleVerification.cs`

**Interfaces:** `bool PuzzleEditorLauncher.IsBusy`, `void Launch(PuzzleEditorLaunchRequest request, int ownerWindowId)`. UI는 Capture/Launch의 동기 예외를 편집창에 표시한다. `event Action<int, string> Finished`는 종료/실패 시 원래 창 InstanceID와 결과 메시지를 전달한다. 요청 ID·창 InstanceID·bytes(Base64)·번호·시드·이전 playModeStartScene 경로를 SessionState에 보관한다. UI 복귀 처리는 작업 3의 창이 소유한다.

- [x] `RequestSurvivesDomainReload`, `InitializeExactlyOnce`, `PlayStartSceneRestored`의 실제 Editor 왕복 검사를 먼저 작성한다. 시간 초과는 실패로 기록하고 사용자 Editor를 종료하지 않는다.
- [x] InitializeOnLoad에서 sceneLoaded/playModeStateChanged를 한 번 구독한다. 대상 PuzzleGame sceneLoaded(세션 Start 이전)에서 대기 요청을 소비·제거한 다음 사본 초기화를 호출한다. 다른 씬에서는 소비하지 않는다. 에셋 사본을 static 참조만으로 전달하지 않는다.
- [x] 기존 시작 씬 override를 보관한 뒤 PuzzleGame을 지정하고 EnterPlaymode한다. 씬 저장 에셋과 현재 편집 씬을 교체하지 않는다. EnteredEditMode, 취소, 대상 세션 누락 등 실패에서도 override와 요청을 정리한다.
- [x] `DoubleLaunchRejected`, `CancelledEntryLeavesNoRequest`, `ExitDuringLoad`, `NextOrdinaryPlayUsesDefault`, `ExistingStartSceneOverridePreserved`를 실행한다. 리로드 활성/비활성을 각각 검사하고 Editor 설정을 finally에서 원상복구한다.
- [x] `PuzzleEditorLaunchLifecycleVerification.Run` 결과를 `Logs/PuzzleEditorLaunchVerification/lifecycle-results.txt`에 기록한다. 종료 복귀 시 pending 요청/소유 사본/중복 콜백이 없음을 확인한다.

완료 증거: 실제 도메인 경계의 단일 초기화와 실패·취소·빠른 재실행 후 정상 복귀.

## 작업 3 — 편집창 게임 실행 컨트롤

**Files:**
- 신규 `Assets/Scripts/Features/LevelEditor/Editor/Presentation/LevelEditorWindow.GameLaunch.cs`
- 수정 `Assets/Scripts/Features/LevelEditor/Editor/Presentation/LevelEditorWindow.cs`
- 필요 시 수정 `Assets/Scripts/Features/LevelEditor/Editor/Presentation/LevelEditorWindow.Workspace.cs`
- 작업 1 검사 파일에 UI 검사를 추가한다.

**Interfaces:** `CreateGameLaunchControls(VisualElement parent)`, `LaunchSelectedGame()`을 partial 창에 둔다. 직렬화 필드로 게임 입력/시드를 유지한다. 클릭은 CancelStroke→ApplyModifiedProperties→Capture→Launch 순서다. Launch에는 창의 GetInstanceID()를 넘긴다. 창의 OnEnable/OnDisable에서 Finished를 구독/해제하고, 일치하는 InstanceID의 살아 있는 창만 편집 탭으로 복귀시킨다.

- [x] 기존 `play-level` 버튼 보존, 신규 `game-play-level` 버튼, `game-level-source` 선택, `game-level-seed` 필드의 실패 검사를 작성한다.
- [x] 기본 에셋/12345, 소스 설명과 실행 오류 표시를 구현한다. 선택 없음·컴파일·모드 전환·중복 요청 동안 버튼을 막고 1040px 최소 창 폭에서 컨트롤이 잘리지 않게 구성한다.
- [x] MemoryPack 실행 시 팩 재생성/SaveAssets가 호출되지 않고 에셋 실행의 미저장값이 전달되는지 확인한다. 실제 생성 팩과 에셋 차이를 만들어 두 소스의 구분을 증명한다.
- [x] 복귀 시 레벨·탭·편집값·dirty 상태를 확인한다. 사용자가 닫은 편집창을 자동 재생성하지 않는다. 기존 플레이 테스트 설정/진행 상태에 불필요한 변경을 만들지 않는다.

완료 증거: 편집창 실제 버튼 클릭으로 두 입력 소스를 실행하고 오류/복귀까지 관찰한다.

## 작업 4 — 통합 검사와 인계

- [x] 서로 다른 두 레벨 × 에셋/팩으로 실행한다. 50/51 경계는 임시 데이터의 codec 검사로 추가해 원본 팩을 바꾸지 않고 구간 선택을 확인한다.
- [x] 동일 입력·시드의 StartingBoardSearch/실행기 기준과 실제 선택 레벨을 비교한다. 실제 교환→파워 생성 이미지→발동을 확인하고 게임 화면과 복귀 편집 보드를 캡처한다.
- [x] 미저장 레벨과 미저장 편집 씬의 실행 전후 메모리 값/dirty 상태, 원본 파일 해시, 열린 씬 목록/활성 씬, 시작 씬 override를 비교한다.
- [x] `PuzzleGameplayVerification.Run`, `PuzzleGameSceneVerification.Run`, `BoardArtworkLifecycleVerification.Run`을 실행한다. 기존 배치 전용 검증의 무조건 EditorApplication.Exit 호출에 주의하고 열린 사용자 Editor를 종료하지 않는다.
- [x] 씬/프리팹 의존성과 Addressables 그룹을 검사해 LevelDefinition 직접 참조 추가가 없음을 확인한다. 기본 게임 씬 단독 실행도 유지한다.
- [x] `git diff --check`, 변경 파일·meta·패키지/설정 원상복구를 확인한다. 실행하지 않은 빌드/실기기는 미검증으로 기록한다.
- [x] `Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-03-progress.md`, `Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-03-editor-launch-usage.md`를 작성하고 목표/전체 계획의 상태를 실제 결과에 맞춰 갱신한다. 완료 조건이 모두 충족된 뒤 3단계를 종료한다.

검사 로그와 캡처는 `Logs/PuzzleEditorLaunchVerification/`에 둔다. 사용자 데이터 변경이나 에디터 종료 없이 검사할 수 없는 경우 이유와 필요한 수동 절차를 먼저 기록한다. 이 계획 작성 단계에서는 위 코드·검사를 실행하거나 목표를 시작하지 않는다.

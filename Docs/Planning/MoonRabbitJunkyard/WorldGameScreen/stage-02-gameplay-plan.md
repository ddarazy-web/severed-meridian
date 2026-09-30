# 2단계 실제 게임 플레이 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:executing-plans`를 사용해 작업별로 구현·검증한다. 체크박스는 실제 검증 뒤 갱신한다. 이 계획은 하위 에이전트 실행을 요구하지 않는다.

상태: 2026-09-30 완료. 작업별 증거와 실행 방식의 차이는 [실행 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-02-progress.md)에 기록했다.

**Goal:** 기존 월드 보드에서 교환부터 승패와 라스트팡까지 실제 플레이를 완결한다.

**Architecture:** `PuzzleGameSession`이 시작 탐색·실행기·아틀라스의 수명을 소유하고 `PuzzleWorldBoard`에 상태를 전달한다. `PuzzleBoardInput`은 화면 좌표를 보드 명령으로 바꾸며, `PuzzlePlayDebugView`는 검증용 상태만 표시한다. 규칙은 기존 `StartingBoardSearch`와 `BoardActionExecutor`를 재사용한다.

**Tech Stack:** Unity 6000.3.10f1, C#, UniTask, SpriteRenderer, Addressables 4.1.0, Input System 1.18.0. 신규 패키지는 추가하지 않는다.

**Spec:** [목표 및 완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-02-gameplay-goal.md)

## Global Constraints

- `C:/Projects/Git/ServeredMeridian`만 작업한다. 사용자 변경, 기존 `.meta/GUID`를 보존하고 자동 커밋하지 않는다.
- 게임 프리팹은 `Assets/Prefabs/Game/Puzzle`에 둔다. 이번 단계에는 정식 UI 프리팹을 만들지 않는다.
- 런타임에서 Editor API를 참조하지 않는다. 씬/프리팹에 `LevelDefinition` 에셋을 직접 연결하지 않는다.
- 50레벨 MemoryPack과 Addressables 아틀라스 분할을 유지한다. 이미지 크기와 생성 이미지도 변경하지 않는다.
- 이미 열린 프로젝트에 두 번째 Unity 배치를 실행하지 않는다. 다른 프로젝트의 에디터를 대상으로 검사하지 않는다.
- 신규 파일명은 계획이다. 실행 시 동명 구현이 생겼으면 내용을 확인해 재사용한다.

## Review Focus

1. 사전 이미지 로드 없이 생성한 파워가 상자로 남는 경우 — 작업 2·5의 콜드 로드/파워 생성 검사.
2. 터치의 마우스 중복 이벤트와 포커스 상실 — 작업 3의 입력 소비 횟수 검사.
3. 승리 `Outcome`이 라스트팡보다 먼저 생기는 경우 — 작업 2의 승리 후 연쇄 검사.
4. 로드가 끝나기 전 씬이 파괴되는 경우 — 작업 2·5의 수명주기 검사.
5. 좌표 변환과 화면 비율 변경으로 다른 칸이 선택되는 경우 — 작업 3·5의 이동/회전된 보드 및 가로·세로 검사.

## 파일과 인터페이스

신규 파일은 `Assets/Scripts/Features/GameScreen/` 기준이다.

| 파일 | 책임 / 계획 인터페이스 |
|---|---|
| `Runtime/Session/PuzzleGameSession.cs` | MonoBehaviour. `State: LevelRuntimeState`, `Outcome: BoardOutcome`, `CanAcceptInput: bool`, `Message: string`, `event Action Changed`. `UniTask InitializeAsync(int levelNumber, int seed, CancellationToken token)`, `bool TrySwap(BoardCoordinate first, BoardCoordinate second)`, `bool TryActivate(BoardCoordinate at)`. bool은 실행기의 행동 적용 여부다. |
| `Runtime/Input/PuzzleBoardInput.cs` | Input System 마우스/터치 처리, 선택과 제스처 수명. `bool TryGetCoordinate(Vector2 screenPosition, out BoardCoordinate coordinate)`. 카메라 ray와 보드 평면의 교점을 로컬 좌표로 바꾼다. |
| `Runtime/Presentation/PuzzlePlayDebugView.cs` | 세션 읽기 전용 최소 표시. 임시 `OnGUI` 텍스트로 구성하며 보드 명령/게임 규칙을 실행하지 않는다. |
| `Editor/Tests/PuzzleGameplayVerification.cs` | 기존 프로젝트의 검증 진입점 방식으로 실행기 연결·상태 보존·종료 사례 검사. |
| `Editor/Tests/PuzzleBoardInputVerification.cs` | 좌표와 제스처, 중복 입력 및 취소 검사. 새 테스트 프레임워크를 추가하지 않는다. |
| `Editor/PuzzleGameAssets.cs` (수정) | 세션 프리팹과 씬 참조 생성. 재실행해도 사용자 씬 전체를 덮어쓰지 않는다. |

기존 참조: `PuzzlePlay/Runtime/Setup/StartingBoardBuilder.cs`, `PuzzlePlay/Runtime/Actions/BoardActionExecutor*.cs`, `Levels/Data/LevelPackLoader.cs`, `GameScreen/Runtime/World/PuzzleArtwork.cs`, `PuzzleWorldBoard.cs`, `PuzzleBoardPreview.cs`.

## 작업 1 — 기존 계약과 회귀 기준 확보

- [x] 위 기존 파일, `Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-01-progress.md`, 설치 패키지와 프로젝트 규칙을 읽고 실제 API를 재확인한다.
- [x] 유효 교환/무효 교환/파워 생성/승패/진행 불가를 만드는 기존 검증 입력을 찾아 메모리 사본으로 재사용한다. 원본 레벨은 수정하지 않는다.
- [x] 동일 레벨·시드·행동에 대한 실행기 기준 결과를 기록한다. 셀 내용/색/내구도, 미션, 이동 수, 난수 횟수, Outcome과 Phase를 비교 대상으로 둔다.
- [x] 기존 `GameScreen.Editor.PuzzleWorldBoardVerification`의 실제 namespace·진입점을 확인하고 좁은 회귀 검사를 실행한다. 실행 환경상 불가하면 사유를 기록하고 과거 PASS를 이번 PASS로 대체하지 않는다.

완료 증거: 선택한 검증 입력 및 기준 결과, 현재 Unity 연결 프로젝트 경로.

## 작업 2 — 세션과 실행기 연결

파일: `PuzzleGameSession.cs`, `PuzzleGameplayVerification.cs`. 기존 World 파일은 연결에 필요한 부분만 변경한다.

- [x] 먼저 실패하는 연결 검사를 작성한다: `InvalidSwapPreservesState`는 무효 행동 전후 상태·이동 수·난수 동일, `ValidSwapConsumesOneMove`는 적용 횟수 1과 이동 수 -1을 확인한다.
- [x] `InitializeAsync`에서 `LevelPackLoader.LoadAsync` → `StartingBoardSearch` → 성공 상태로 `BoardActionExecutor` 생성 → `PuzzleArtwork.PrepareAsync` → `Draw` 순서를 구현한다. 중복 초기화를 거부하고, 로드된 정의 사본은 시작 탐색이 필요한 작업을 마친 뒤 반환한다.
- [x] 프레임마다 탐색 `Advance(128)` 또는 연쇄 `AdvanceCascade()`를 수행한다. 실행기 상태 변경 직후 Draw와 Changed를 호출하고 규칙을 추가하지 않는다.
- [x] `TrySwap`/`TryActivate`는 준비 상태에서만 실행한다. 같은 프레임 두 번째 요청도 첫 요청이 연쇄를 시작했다면 거부한다.
- [x] `WonFinishesLastPang` 검사: Won 발생 뒤에도 pending을 처리하여 Stopped에 도달한다. `FailureKindsRemainDistinct` 검사: 이동 소진/Blocked/Aborted를 성공이나 동일 패배로 합치지 않는다.
- [x] `ColdPowerSpawnHasArtwork`, `ExitDuringLoad`, `SearchFailureBlocksInput` 검사를 추가한다. 중단 토큰과 파괴 시점을 확인하고 로드 후 사라진 대상에 Draw하지 않는다.
- [x] 새 연결 검사들을 실행하고 작업 1의 직접 실행기 기준과 최종 상태가 동일한지 확인한다.

완료 증거: 성공·실패·라스트팡과 수명주기 검사 결과. 동기 반복문으로 탐색/연쇄 전체를 한 프레임에 처리하지 않는다.

## 작업 3 — 월드 보드 입력

파일: `PuzzleBoardInput.cs`, `PuzzleBoardInputVerification.cs`.

- [x] `ScreenPointMapsToCell` 검사를 먼저 작성한다. `PuzzleWorldBoard.CellPosition`의 역변환, 10×10 범위, 비활성 칸, 보드 Transform과 카메라 비율을 확인한다.
- [x] `TryGetCoordinate`를 구현한다. 좌표를 범위 안으로 강제 보정하지 않고 바깥 입력을 거부한다.
- [x] 목표 문서의 탭/25% 드래그/파워 탭 정책을 구현한다. 세션이 아닌 입력 컴포넌트가 선택 칸·포인터 ID를 보관한다.
- [x] `OneGestureOneCommand`, `SecondaryTouchIgnored`, `FocusLossCancelsGesture`, `UIStartIgnored`, `BusyInputIgnored` 검사를 추가한다. 실제 하드웨어 입력 채취 부분과 동일한 제스처 처리 경로를 테스트한다.
- [x] Input System의 터치가 존재하면 해당 프레임의 마우스 입력을 처리하지 않는다. 포인터 취소/포커스 상실/Disable에서 선택과 드래그를 초기화한다.
- [x] 입력 검사 PASS 후 Play Mode에서 마우스로 유효/무효 교환과 파워 탭을 직접 확인한다. 모바일 하드웨어 검증 여부는 따로 기록한다.

완료 증거: 좌표별 기대 칸과 실제 명령, 입력 1회당 실행 횟수, 실제 Play Mode 조작 기록.

## 작업 4 — 씬·프리팹 연결과 최소 표시

파일: `PuzzlePlayDebugView.cs`, `PuzzleGameAssets.cs`, `Assets/Prefabs/Game/Puzzle/PuzzleGameSession.prefab`, `Assets/Scenes/PuzzleGame.unity`.

- [x] 최소 표시를 세션 Changed에 연결하고 로딩/오류·이동 수·미션·선택 칸·라스트팡/최종 결과가 구분되는지 확인한다. 구독은 Enable/Disable에 맞춰 정리한다.
- [x] 세션 프리팹에는 게임 세션과 입력을 구성한다. 씬의 보드/카메라는 씬 인스턴스에서 연결하며 프리팹 에셋에 씬 오브젝트를 저장하지 않는다.
- [x] `PuzzleBoardPreview`와 세션이 동시에 보드를 그리지 않도록 기존 씬의 Preview 컴포넌트를 비활성화한다. 이전 표시 검증용 클래스와 GUID는 유지한다.
- [x] Preview가 담당하던 카메라 중심 및 화면 비율 처리를 게임 경로에 연결한다. 중복 카메라 갱신이 없고 10칸 기준 표시 크기를 유지한다.
- [x] 에셋 생성은 Unity API로 수행한다. 두 번 실행한 뒤 세션/입력/보드 중복이 없고, 씬에 레벨 에셋 직접 참조가 없는지 검사한다.

완료 증거: Play 가능한 씬, 정상 참조의 게임 프리팹, 기존 GUID 및 씬 내 컴포넌트 수 검사.

## 작업 5 — 실제 화면 검증과 인계

- [x] `PuzzleGameplayVerification.Run`과 `PuzzleBoardInputVerification.Run`을 새 검증 진입점으로 제공하고 결과를 각각 `Logs/PuzzleGameplayVerification/results.txt`, `Logs/PuzzleBoardInputVerification/results.txt`에 남긴다. 현재 실행 중인 사용자 에디터에서는 자동 종료하지 않는다.
- [x] 에디터를 사용할 수 없는 배치 검사는 프로젝트를 닫은 상태에서만 실행한다. 예: `& 'C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Projects/Git/ServeredMeridian' -executeMethod GameScreen.Editor.PuzzleGameplayVerification.Run -logFile 'Logs/PuzzleGameplayVerification/unity.log'`. 실제 namespace에 맞춰 확정하며 비동기 검사가 완료된 뒤 테스트 진입점에서 배치 종료 코드를 반환한다.
- [x] 콜드 로드부터 Play Mode에서 유효 교환 → 생성 파워 표시 → 발동 → 낙하/연쇄 → 승패/라스트팡 흐름을 확인한다. 가로·세로 캡처를 `Logs/PuzzleGameplayVerification/`에 남긴다.
- [x] 플레이 종료·재진입과 로드 중 종료를 확인한다. 콘솔 오류, 잔여 세션/입력 구독, 원본 레벨 변경이 없는지 확인한다.
- [x] 기존 월드 표시 검사와 편집 보드 이미지 회귀를 수행한다. 이전 색상 상자 증상이 재발하면 해당 상태와 로딩 오류를 기록하며 이번 범위의 변경이 원인일 때 수정한다.
- [x] `git diff --check`와 변경 파일 목록을 확인한다. `Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-02-progress.md`, `Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-02-gameplay-usage.md`를 작성하고 목표 체크박스를 실제 증거에 맞춰 갱신한다.

완료 기준: 목표 문서의 모든 필수 항목에 결과가 있고 실제 Play Mode 증거가 있다. 실행하지 못한 검증은 미검증으로 남기고 완료라고 주장하지 않는다. 3단계는 시작하지 않는다.

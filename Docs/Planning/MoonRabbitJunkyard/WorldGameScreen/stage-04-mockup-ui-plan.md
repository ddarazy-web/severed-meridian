# 4단계 목업 UI 구성 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:executing-plans`로 순서대로 구현·검증한다. 병렬 하위 에이전트 구현을 요구하지 않는다. 체크박스는 실제 검증 뒤 갱신한다.

**Goal:** 확정 목업의 uGUI 화면으로 월드 보드를 플레이하고 아이템·일시정지·같은 레벨 다시하기를 연결한다.

**Architecture:** 기존 세션이 실행기와 최초 입력 스냅샷을 소유한다. UI는 세션 상태를 구독하고 명령만 전달한다. 화면 배치 컴포넌트가 안전 영역 안의 보드 전용 RectTransform을 카메라 viewport로 변환한다. 게임 규칙은 기존 실행기를 사용한다.

**Tech Stack:** Unity 6000.3.10f1, uGUI, Input System 1.18.0, UniTask, MemoryPack, Addressables 4.1.0. 신규 패키지 없이 프로젝트의 기존 글꼴·UI 이미지를 조사해 재사용한다.

**Spec:** [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-04-mockup-ui-goal.md) · [목업](../../../MoonRabbitJunkyard/Mockups/puzzle-screen.html). 상태: 구현·검증 완료 (2026-10-01).

## Global Constraints

- ServeredMeridian만 사용한다. 기존 변경과 `.meta/GUID`, 정상인 1~3단계를 유지한다. 자동 커밋하지 않는다.
- 코드·프리팹 생성 전 실제 파일을 확인한다. 아래 신규 이름이 이미 있으면 재사용한다. 사용자 미저장 씬은 자동 저장/교체하지 않는다.
- UI는 `Assets/Prefabs/UI/Puzzle`, 게임은 `Assets/Prefabs/Game/Puzzle`. 런타임에서 UnityEditor를 참조하지 않는다.
- 레벨 에셋 직접 참조, 팩 자동 갱신, 모든 아틀라스 통합, 원본 데이터 변경을 하지 않는다.
- 열린 프로젝트에 두 번째 Unity 배치를 실행하거나 다른 프로젝트 Editor를 조작하지 않는다. 5단계는 시작하지 않는다.

## Review Focus

1. 다시하기가 기본 1레벨로 돌아가거나 변경된 팩을 읽음: 작업 1의 두 입력 소스 스냅샷 재현 검사.
2. 일시정지 중 연쇄가 진행되거나 결과가 조기 노출됨: 작업 1·4의 중간 연쇄/라스트팡 검사.
3. UI 클릭 또는 UI 위에서 끝난 드래그가 보드에 전달됨: 작업 3의 시작/종료 포인터 차단 검사.
4. 회전 시 카메라 계산이 서로 덮어쓰거나 상태를 재생성함: 작업 2의 viewport·상태 동일성 검사.
5. 재시작 연타/종료 중 로딩으로 구독·사본·아틀라스가 누적됨: 작업 1·5의 취소/반복 수명 검사.

## 작업 1 — 세션의 일시정지·아이템·재시작 계약

**Files:** 수정 `Assets/Scripts/Features/GameScreen/Runtime/Session/PuzzleGameSession.cs`; 신규 같은 폴더 `PuzzleGameSession.Controls.cs` (partial 분리 필요 시); 신규 `Assets/Scripts/Features/GameScreen/Editor/Tests/PuzzleUIStateVerification.cs`.

**Interfaces:** 세션에 `bool IsPaused`, `bool IsRestarting`, `bool CanUseItems`, `bool SetPaused(bool paused)`, `UniTask RestartAsync(CancellationToken token)`, `bool CanSelectItemTarget(BoardItem item, BoardCoordinate at)`, `bool TryUseItem(BoardItem item, BoardCoordinate? first = null, BoardCoordinate? second = null)`를 제공한다. 상태 변경은 기존 `Changed`로 전달한다. 모든 게임 명령은 세션의 입력 가능 상태를 먼저 확인한다.

- [x] `PauseFreezesCascade`, `ItemsMatchExecutor`, `RetryKeepsSnapshotAndSeed` 검사를 먼저 작성한다. 현재 누락 기능의 실패를 확인한다.
- [x] 최초 유효 레벨을 `LevelPackCodec.Encode`로 보존하고 같은 시드를 저장한다. 다시하기는 `ReadLevel`의 새 사본을 기존 준비 경로에 전달한다. `ToPacked/FromPacked` 얕은 복사를 스냅샷으로 사용하지 않는다.
- [x] 재시작 진입 시 즉시 입력을 막고 기존 판의 표시/아틀라스 참조를 안전하게 정리한다. 중복 호출을 무시하고, 비동기 작업 완료가 파괴된 세션을 다시 그리지 않게 한다. 초기화 실패는 UI 메시지로 노출하며 임의 레벨로 대체하지 않는다.
- [x] pause 시 AdvanceCascade와 모든 행동을 막고 재개 시 이어간다. 아이템은 `Assets/Scripts/Features/PuzzlePlay/Runtime/Actions/BoardActionExecutor.Items.cs`의 판정/UseItem에 위임한다. 별도 피해/소모 규칙을 만들지 않는다.
- [x] 두 입력 모드에서 초기화 후 원본/팩을 바꿔도 같은 시작 보드가 재현되는지, 재시작 연타/로드 중 종료가 안전한지 검사한다. 임시 데이터만 사용한다.

## 작업 2 — 프리팹·목업 배치·보드 카메라

**Files:** 신규 `Assets/Scripts/Features/GameScreen/Runtime/UI/PuzzleScreenView.cs`, `PuzzleScreenLayout.cs`, `PuzzleHudView.cs`, `PuzzleMissionView.cs`; 신규 `Assets/Scripts/Features/GameScreen/Editor/PuzzleUIAssets.cs`; 수정 `Assets/Scripts/Features/GameScreen/Editor/PuzzleGameAssets.cs`, 세션의 카메라 처리; 신규 `Assets/Scripts/Features/GameScreen/Editor/Tests/PuzzleUILayoutVerification.cs`.

**Interfaces:** `PuzzleScreenView.Configure(PuzzleGameSession session, PuzzleBoardInput input)`가 UI 연결/구독 수명을 관리한다. `PuzzleScreenLayout.ApplyLayout(Rect safeArea, Vector2 screenSize)`가 UI 방향과 보드 viewport를 계산한다. `PuzzleHudView.Refresh(PuzzleGameSession session)`가 실제 상태를 표시하고 미션 항목을 갱신한다. `PuzzleUIAssets.Generate()`는 반복 실행 시 중복 없이 프리팹/씬을 연결한다.

- [x] 네 기준 해상도와 비대칭 안전 영역에서 보드 viewport가 화면/안전 영역 안에 있고 HUD/아이템과 겹치지 않는 검사를 먼저 만든다.
- [x] 목업의 색·이미지·글꼴·가로/세로 관계를 추출한다. HTML의 base64 전체를 출력하지 않는다. 기존 프로젝트 에셋과 대응표를 작성하고 누락은 검증 기록에 명시한다. 한국어 미션 설명이 깨지거나 잘리지 않게 한다.
- [x] `Assets/Prefabs/UI/Puzzle/`에 `PuzzleScreen`, `PuzzleHUD`, `PuzzleMission`, `PuzzleItemBar`, `PuzzlePausePopup`, `PuzzleResultPopup` 프리팹을 만든다. 화면 루트 Canvas/CanvasScaler/GraphicRaycaster와 InputSystemUIInputModule EventSystem은 하나씩 연결한다. 장식의 raycastTarget은 끈다.
- [x] 보드 전용 RectTransform의 화면 사각형을 카메라 rect로 적용하고 그 영역에 활성 보드 전체가 들어오도록 맞춘다. 세션 Update의 기존 전체 화면 orthographicSize 덮어쓰기를 제거/위임해 계산 소유자를 하나로 만든다. 회전 시 좌표 변환·카메라 중심도 확인한다.
- [x] 미션 실제 종류/목표/진행과 이미지, 설명을 표시한다. 기존 이미지 로딩 경로를 재사용하고 UI 소유 핸들이 있다면 화면 종료에 반환한다. 로딩/오류 상태도 표시한다. `PuzzlePlayDebugView`를 실제 씬/생성기에서 제거해 중복 표시를 없앤다.

## 작업 3 — 아이템 선택과 UI 입력 경계

**Files:** 신규 `Assets/Scripts/Features/GameScreen/Runtime/UI/PuzzleItemBarView.cs`; 수정 `Assets/Scripts/Features/GameScreen/Runtime/Input/PuzzleBoardInput.cs`; 수정/확장 `Assets/Scripts/Features/GameScreen/Editor/Tests/PuzzleBoardInputVerification.cs`.

**Interfaces:** 입력에 `BoardItem? SelectedItem`, `void SelectItem(BoardItem item)`, `void CancelItemSelection()`을 추가한다. UI 버튼은 이를 호출한다. 좌표 선택은 작업 1의 대상 판정과 TryUseItem을 이용한다. 화면 상태 전환은 `CancelGesture()`와 아이템 취소를 함께 호출한다.

- [x] UI 위에서 시작, 보드에서 시작해 UI 위에서 종료, 서로 다른 포인터, 팝업 열린 상태의 행동 수가 0인지 먼저 검사한다. 기존 BeginPointer뿐 아니라 EndPointer에도 UI 차단을 적용한다.
- [x] 망치 한 칸/자리 바꾸기 두 칸/섞기 즉시 실행을 연결한다. 선택 중 보드 입력은 아이템 경로만 사용한다. 잘못된 대상은 상태를 바꾸지 않고 이유를 표시하며 취소/재선택이 가능해야 한다.
- [x] 선택 강조·안내·취소와 시험용 무제한 표시를 목업에 맞춰 구성한다. pause/결과/회전/재시작 때 남은 선택을 지운다. 전환 중 입력과 섞기 연타가 중복 행동을 만들지 않는지 검사한다.

## 작업 4 — 일시정지·결과·다시하기 UI

**Files:** 신규 `Assets/Scripts/Features/GameScreen/Runtime/UI/PuzzlePauseView.cs`, `PuzzleResultView.cs`; 작업 1·2의 세션/화면 루트/상태 검사를 확장한다.

**Interfaces:** 팝업 뷰는 `PuzzleScreenView`에 버튼 이벤트를 전달하고 직접 실행기를 조작하지 않는다. 루트는 pause/resume/retry를 작업 1에 위임한다. 결과 표시 조건은 `Outcome != null && Phase == BoardActionPhase.Stopped`다.

- [x] 진행 중 pause→여러 프레임 대기→resume, 승리 라스트팡, 이동 소진 실패 fixture 검사를 작성한다. pause 동안 보드 상태가 같고 결과는 종료 후 한 번만 나타나야 한다.
- [x] 목업의 배경 차단/팝업 형태로 재개·다시하기를 연결한다. 재시작 로딩 중 버튼을 잠그고 성공 시 팝업/선택을 초기화한다. 실패 이유는 지워지지 않게 표시한다.
- [x] 구현되지 않은 광고/보상/상점/다음 레벨 버튼을 동작 가능한 것처럼 만들지 않는다. 결과 값은 실제 판에서 얻는 값만 표시한다. 기존 Editor Play 종료/복귀를 유지한다.

## 작업 5 — 통합 검증·인계

- [x] 신규 `PuzzleUIStateVerification.Run`, `PuzzleUILayoutVerification.Run`을 실행해 `Logs/PuzzleUIVerification/`에 PASS/FAIL, 실제 실행 수, 실패 원인을 기록한다. 생성기를 두 번 실행해 중복 객체/컴포넌트와 GUID 변경이 없는지 확인한다.
- [x] 실제 에디터의 게임 플레이 버튼으로 에셋/MemoryPack을 실행하고 각각 아이템·일시정지·다시하기·Play 종료/편집 보드 복귀를 확인한다. 원본/dirty 상태 보존을 확인한다.
- [x] Game View 네 해상도와 안전 영역 모의 검사를 실행한다. 회전 전후 게임 상태를 비교하고 가로·세로·아이템·pause·승리·실패 캡처를 남긴다. Editor 모의 터치를 Android 실기기 검증으로 기록하지 않는다.
- [x] 기존 `PuzzleGameplayVerification.Run`, `PuzzleBoardInputVerification.Run`, `PuzzleGameSceneVerification.Run`, `PuzzleEditorLaunchVerification.RunData`, `PuzzleEditorLaunchLifecycleVerification.Run`, `BoardArtworkLifecycleVerification.Run`을 영향에 맞게 실행한다. 기존 씬 검사의 임시 DebugView 전제는 새 UI로 갱신한다. Editor 종료를 호출하는 배치 전용 검사는 열린 Editor에서 실행하지 않는다.
- [x] 사용자 씬/설정 복구, 임시 fixture 제거, `git diff --check`, meta·LevelDefinition 직접 참조·핸들/구독 수명을 확인한다. 5회 연속 재시작 후에도 단일 UI 구독/화면과 정상 보드를 유지해야 한다.
- [x] 목표 완료 조건을 실제 증거로 갱신하고 `Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-04-progress.md`, `Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-04-mockup-ui-usage.md`를 작성한다. 목차/로드맵을 갱신하고 4단계에서 멈춘다.

계획 자체 검토: 목표의 여섯 사용자 흐름을 작업 1~4에 연결했고, 완료 조건과 다섯 위험 항목을 작업 5까지의 검사에 배정했다. 본 문서 작성 중 구현·테스트 실행·목표 활성화는 하지 않는다.

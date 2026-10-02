# 공통 Popup UI 프레임워크 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for direct execution, or superpowers:subagent-driven-development if the user selects that method. Implement one selected stage task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 팝업 중첩·최상위 입력·선택적 씬 복원을 통일하고 등록/템플릿/관리 도구로 기존 세 팝업을 전환한다.

**Architecture:** 공통 서비스는 인스턴스 순서와 값 데이터만 소유하고 씬 표시 영역은 Unity 오브젝트를 소유한다. 게임 연결이 입력 차단/일시정지 이유와 논리 문맥을 제공한다. 복원은 후보 생성 후 일괄 적용한다.

**Tech Stack:** Unity 6000.3.10f1, 기존 uGUI/Input System, EditorWindow, 직접 프리팹 참조, 기존 UniTask 수명 처리. 신규 패키지 없음.

**Spec:** [승인한 설계](../../Systems/project-wide/2026-10-02-popup-framework-design.md). [ADR](../../Decisions/project-wide/2026-10-02-popup-framework.md) · [완료 조건](../../Goals/project-wide/2026-10-02-popup-framework-goal.md).

상태: 2026-10-02 1단계 구현·검증 완료, 2~3단계 미착수. 월드 게임 화면 14단계 번호를 임의 부여하지 않으며 공통 시스템의 독립 3단계로 관리한다.

## Global Constraints

- ServeredMeridian만 대상으로 한다. 기존 사용자 변경·레벨·게임 규칙·이미지·GUID·Addressables/MemoryPack을 보존한다.
- 플레이어/Addressables 콘텐츠 빌드·커밋·푸시·사용자 Unity 종료·사용자 씬 자동 저장을 하지 않는다.
- 기존 uGUI·직접 프리팹 참조를 유지한다. 신규 패키지·asmdef·DI 교체·디스크 저장·팝업 풀링을 도입하지 않는다.
- 오직 최상위 팝업만 클릭·터치·키·뒤로가기를 받는다. 외부 입력 차단/정지는 보존한다.
- 복원은 호출자가 보관을 선택한 이동에서 원래 씬/동일 게임 문맥에 한정한다. 게임 진행 자체를 저장하지 않는다.
- 미구현 문서·미실행 검사·미검증 물리 기기를 완료로 표현하지 않는다.
- 실행은 사용자가 선택한 단계만 수행한다. 다음 단계나 다른 목표를 자동 시작하지 않는다.
- 커밋 대신 각 단계의 diff·검증·완료 조건 증거를 남긴다. 사용자의 별도 커밋 요청이 없는 한 스킬의 커밋 예시는 실행하지 않는다.

## 시작 조사와 파일 지도

착수 시 `git status --short`, 현재 HEAD, 기존 세 팝업·입력·세션·생성 도구·검사 진입점을 다시 확인한다. 현재 기준 HEAD는 `680d4bc`이며 사용자 후속 커밋이 있으면 그것을 보존한다. 완료된 Stage13을 재구현하지 않는다. 필요한 기존 검증 증거는 현재 변경과의 관계를 확인하고 새 영향이 없으면 불필요하게 반복하지 않는다.

Runtime 기준: `Assets/Scripts/Systems/Popup/Runtime/`, namespace `PopupUI`. Editor 기준: `Assets/Scripts/Systems/Popup/Editor/`, namespace `PopupUI.Editor`. 게임 연결은 기존 `GameScreen` namespace와 폴더를 유지한다. 신규 Unity 파일과 실제 생성한 폴더에 고유 .meta를 생성한다.

| 단계 | 신규 파일 | 책임 |
| --- | --- | --- |
| 1 | Runtime/PopupHandle.cs, PopupContext.cs, PopupState.cs | 고유 인스턴스·문맥·복원 값 계약 |
| 1 | Runtime/PopupCatalog.cs, PopupView.cs, PopupHost.cs, PopupService.cs | 등록·뷰·표시/입력·관리 |
| 1 | Editor/Tests/PopupFrameworkVerification.cs, PopupFrameworkVerification.Input.cs | 규칙/Data와 실제 EventSystem 입력 검사 |
| 2 | Runtime/PopupSnapshotStore.cs, PopupExitTicket.cs, PopupRestoreResult.cs, PopupService.Restoration.cs | 보관·이동 성공/실패·복원 |
| 2 | Editor/Tests/PopupFrameworkVerification.Restoration.cs | 실제 씬 왕복과 실패 검사 |
| 3 | Editor/PopupManagementWindow.cs, PopupTemplateGenerator.cs, PopupCatalogValidation.cs, PuzzlePopupAssets.cs | 관리·생성·검증·기존 프리팹 연결 |
| 3 | GameScreen/Runtime/UI/PuzzlePopupBinding.cs, PuzzlePopupState.cs, PuzzleDescriptionView.cs | 게임 문맥·팝업 상태·설명 분리 |
| 3 | GameScreen/Editor/Tests/PuzzlePopupVerification.cs | 기존 기능과 프리팹 회귀 |

게임 경로의 기준은 `Assets/Scripts/Features/`다. 단계 3 수정 대상은 `GameScreen/Runtime/UI/PuzzleScreenView.cs`, `PuzzlePauseView.cs`, `PuzzleResultView.cs`, `GameScreen/Runtime/Session/PuzzleGameSession.Controls.cs`, `PuzzleGameSession.LevelTransition.cs`, `GameScreen/Editor/PuzzleUIAssets.cs`, `PuzzleLevelTransitionAssets.cs`이다. 기존 IsPaused 대입 호출부가 Controls 밖에도 있으므로 착수 시 전체 호출부를 확인하고 정지 소유권에 직접 필요한 지점만 변경한다.

에셋은 `Assets/Prefabs/UI/Popup/PopupHost.prefab`, `PopupTemplate.prefab`, `Assets/Data/UI/PopupCatalog.asset`를 새로 생성한다. 기존 `Assets/Prefabs/UI/Puzzle/PuzzleScreen.prefab`, `PuzzlePausePopup.prefab`, `PuzzleResultPopup.prefab`를 연결하고 `PuzzleDescriptionPopup.prefab`를 새로 만든다. 기존 씬을 재생성하지 않는다.

## 공통 인터페이스 계약

다음 계약은 세 단계 간 의존성을 고정한다. 필요한 본문만 구현하고 범용 로더/DI 계층을 추가하지 않는다.

- `readonly struct PopupHandle`: 서비스 수명 동안 재사용하지 않는 `long Value`; 닫힌 핸들은 무효. 복원은 새 핸들이다.
- `readonly struct PopupContext(string sceneKey, string featureKey, string sessionKey)`: 세 값 모두 비교한다. 게임 sessionKey는 논리 게임 식별자이며 같은 씬 이름/레벨만으로 대신하지 않는다.
- `abstract class PopupState`: `public abstract PopupState Copy()`; 값만 깊은 복사한다. 하위 상태는 Unity 참조/콜백을 소유하지 않는다.
- `PopupCatalog : ScriptableObject`: 항목은 `Id`, `PopupView Prefab`, `AllowMultiple`, `PauseGameplay`, `CloseOnCancel`, `Restorable`. 미지 ID와 중복 ID는 열기 전에 거부한다.
- `PopupView : MonoBehaviour`: `ApplyState(PopupState state)`, `CaptureState() : PopupState`, `HandleCancel() : void`, `DefaultSelection : GameObject`; 관리자가 최상위인 경우에만 취소를 전달한다. 게임 행동 연결은 게임 어댑터가 담당한다.
- `PopupHost : MonoBehaviour`: `Attach(PopupService service, PopupContext context)`, `Detach()`, `event Action<bool> InputBlockChanged`, `event Action<bool> PauseRequestChanged`. 외부 정지의 해제는 이 이벤트만으로 결정하지 않는다.
- `PopupService`: `Open(string id, PopupState state) : PopupHandle`, `Close(PopupHandle handle) : bool`, `CloseAll() : void`, `Top : PopupHandle?`, `Count : int`, `event Action Changed`. 상태 없는 팝업은 state=null을 허용하며 복원 가능 팝업의 CaptureState는 값 상태를 제공한다.
- 단계 2 진입점: `BeginSceneExit(bool preserve) : PopupExitTicket`, `CommitSceneExit(PopupExitTicket ticket) : void`, `RollbackSceneExit(PopupExitTicket ticket) : void`, `Restore(PopupHost host, PopupContext context) : PopupRestoreResult`, `Discard(PopupContext context) : void`.
- `PopupRestoreResult`: `Status`(None/Restored/ContextMismatch/Failed), `IReadOnlyList<PopupHandle> Handles`(아래→위), `string Error`. 실패 시 Handles는 비어 있고 후보가 정리된다.

Open은 현재 직접 프리팹 생성 방식에 따라 동기 처리한다. 초기 생성 실패는 기존 순서/입력을 보존한다. 비동기 로딩 추상화는 만들지 않는다. SceneManager의 비동기 씬 준비/소멸은 기존 UniTask 취소 규칙으로 연결한다.

## Review Focus

1. 최상위 닫기에서 동일 취소 입력이 아래 팝업으로 재전달되는 경우: 입력당 한 동작(Task 1).
2. 버튼 콜백이 자기/다른 팝업을 닫고 새 팝업을 여는 경우: 순서와 포커스 일관성(Task 1).
3. 상태 캡처 뒤 원본 상태를 수정하는 경우: 저장한 값 불변과 깊은 복사(Task 2).
4. 실제 씬 이동 실패·표시 영역 종료·복원 중 뷰 예외: 기존 상태 보존과 후보/구독 정리(Task 2).
5. 게임 재시작/다음 레벨과 외부 정지가 겹치는 경우: 오래된 결과 복원/불필요 재개/결과음 중복 방지(Task 3).

## 1단계 — 공통 관리와 최상위 입력

1단계만 실행할 때는 [독립 계획](popup-framework-stage-01-plan.md)과 [독립 목표](../../Goals/project-wide/popup-framework-stage-01-goal.md)를 사용한다. 기존 게임용 catalog 연결은 3단계로 두고 시험 catalog만 1단계에서 소유/정리한다.

**Files:** 파일 지도 단계 1과 공통 호스트/템플릿 프리팹. 실제 게임 전환은 하지 않는다.

**Consumes:** 기존 uGUI Canvas/EventSystem/Input System. **Produces:** 공통 인터페이스 중 Open/Close/Host/Catalog/State/Context, 단계 2가 사용할 순서·상태 접근 내부 경계.

- [x] 현재 작업 상태와 원본 관련 파일/GUID를 기록하고 기존 검증 실행법을 확인한다.
- [x] `PopupFrameworkVerification.Data`에 A→B→C, B 닫기 후 순서 A/C·Top=C·Count=2, 닫힌 B 재닫기 false·불변 검사를 작성한다. 단일 A 재열기는 같은 핸들/Count 불변/Top=A·새 내용 갱신, 복수 종류는 서로 다른 핸들임을 검사한다.
- [x] `RunInputScene`에 실제 Canvas/EventSystem/버튼을 만들고 비최상위 클릭/Submit/Cancel 호출 0, 최상위 Cancel 1, 새 최상위에 같은 입력 재전달 0을 검사한다. 닫기 금지 최상위가 취소를 소비하는지, 포커스 복귀/기본 선택을 검사한다.
- [x] 정지 요청 2개와 비정지 1개를 중간/최상위 순서로 제거하고 마지막 정지 요청까지만 true임을 검사한다. 콜백에서 닫고 여는 경우와 생성 실패 기존 순서 불변 검사를 추가한다.
- [x] 새 검사 실행에서 동작 미구현으로 실패하는 근거를 저장한다. 컴파일 오류는 동작 재현 실패와 구분한다.
- [x] 지정 Runtime 계약을 구현한다. 서비스 단일 초기화/명시적 host 연결, 핸들 유효성, 목록 갱신과 최상위 이벤트 전달, 전체 배경 입력 차단과 CanvasGroup/포커스를 적용한다.
- [x] 같은 검사를 다시 실행해 FAIL 0·실제 종료 코드 0을 확인한다. host 해제 뒤 살아 있는 뷰/구독이 없고 외부 화면에는 입력을 잘못 해제하지 않는지 검사한다.
- [x] 단계 1 목표 조건 증거·상태·제한을 `Docs/Verification/project-wide/popup-framework-stage-01.md`, 사용법을 `Docs/Guides/project-wide/popup-framework-usage.md`에 기록한다. 별도 기능 요구 없이 실제 게임에 연결하지 않는다.

## 2단계 — 필요한 이동의 상태 보관과 복원

**Files:** 파일 지도 단계 2. **Consumes:** 단계 1 계약과 값 복사. **Produces:** SceneExit/Restore/Discard 계약과 복원 결과.

- [ ] 단계 1 완료 근거를 현재 코드와 대조한다. 실제 미완료만 처리하고 완료 구현을 반복하지 않는다.
- [ ] `RunRestorationScene`에 소유한 임시 두 씬으로 A→B→C를 캡처하고 실제 SceneManager 왕복을 구성한다. 텍스트/입력/선택/탭/스크롤 값과 순서, 새 핸들·새 세션 연결·명령 호출 0을 검사한다.
- [ ] preserve=false, Restorable=false B 제외 후 A/C, 다른 scene/feature/session, 같은 레벨의 새 게임 문맥에서 복원 없음, 성공 후 재복원 None을 검사한다.
- [ ] 원본 상태 변경 후 저장 값 불변, 보관하지 않은 이동의 오래된 snapshot 폐기, 이동 rollback 시 기존 순서/입력 유지, 중복 commit/rollback 무효를 검사한다.
- [ ] 후보 두 번째 뷰의 ApplyState 예외에서 생성 후보 0·입력 누출 0·snapshot 재시도 가능, 씬 종료 후 늦은 콜백 0을 검사하고 실제 실패 근거를 저장한다.
- [ ] SnapshotStore와 ticket을 구현한다. Begin은 캡처만 하며 기존 표시를 즉시 파괴하지 않는다. Commit에서 표시 해제/보관 확정, Rollback에서 이전 표시 유지와 임시 데이터 폐기. 한 host당 이동 ticket 하나로 중복 요청을 거부한다.
- [ ] Restore는 비활성 후보를 생성/연결한 뒤 한 번 적용한다. 아직 다른 팝업이 열린 host에는 복원을 거부해 기존 순서를 보존한다. 문맥 mismatch 폐기, 실패 후보 정리, 성공 데이터 소비를 구현한다.
- [ ] 같은 검사를 통과시키고 단계 1 영향 검사를 실행한다. 임시 씬/에셋/메타를 정리하고 사용자 씬/빌드 설정은 보존한다.
- [ ] `Docs/Verification/project-wide/popup-framework-stage-02.md`와 사용 안내에 호출 예제·게임 문맥 책임·실패/재시도 정책을 기록한다.

## 3단계 — 관리 도구와 기존 세 팝업 전환

**Files:** 파일 지도 단계 3 및 기존 수정 대상/프리팹. **Consumes:** 단계 1~2 전체 계약. **Produces:** 실제 게임 연결·카탈로그·템플릿·관리 창.

- [ ] 단계 2 완료 근거를 확인하고 기존 결과/일시정지/설명 행동과 원본 프리팹 구조를 현재 코드에서 기록한다.
- [ ] `PopupFrameworkVerification.RunTools`에 중복 ID·없는 프리팹·필수 컴포넌트 누락 거부, 기존 파일 덮어쓰기 금지, 생성 템플릿의 실제 컴파일/열기/캡처 검사를 작성한다. 현재 목록의 Top/Count/정지/보관 상태가 서비스와 일치하는지도 검사한다.
- [ ] `PuzzlePopupVerification.RunScene`에 설명→일시정지 중첩·중간 제거·보드 스와이프/아이템 0, 닫은 뒤 다른 차단 이유 유지, 외부 정지와 팝업 정지 교차, 재시작·다음 레벨의 문맥 변경과 오래된 결과 폐기를 검사한다. 미구현 연결 실패를 재현한다.
- [ ] 관리 창과 검증/생성 도구를 구현한다. 메뉴는 `Tools/Popup/관리`, `Tools/Popup/템플릿 생성`, `Tools/Popup/등록 검사`로 통일한다. catalog 편집은 Undo/명시적 저장, 플레이 시험 상태는 저장하지 않는다.
- [ ] 게임 PopupBinding과 값 상태를 구현한다. 팝업 PauseRequest와 외부 정지 이유를 세션에서 따로 소유하고 IsPaused는 합성 결과를 사용한다. 백그라운드·기존 오디오 규칙을 보존한다. 결과를 복원할 때 새 세션을 연결하고 결과음/보상 재호출 없이 표시만 적용한다.
- [ ] 기존 뷰와 PuzzleScreenView를 관리 API로 전환한다. pause/description은 CloseOnCancel=true, result는 false로 등록한다. 세 종류는 AllowMultiple=false, Restorable=true로 등록하되 문맥 일치 검사를 항상 적용한다. 기존 결과의 Retry/Next 잠금·Asset 안내를 유지한다.
- [ ] `PuzzlePopupAssets.Apply`로 지정 프리팹과 catalog만 연결한다. 실제 씬 재생성/자동 저장은 하지 않는다. 기존 두 생성 도구를 최소 수정해 새 연결과 Next를 보존한다. 생성 재실행 검사는 사용자 에셋 대신 소유한 임시 복사본에서 진행한다.
- [ ] RunTools/RunScene 및 단계 1~2 검사를 통과시킨다. Stage13 전환·실패·취소·Asset 실행, 기존 진행/UI/오디오/스와이프 중 영향 검사만 새로 실행한다. 실제 EventSystem 버튼/키/포커스와 1280×720·450×800·450×975·600×800 화면을 캡처한다. 비영점 안전 영역에서도 팝업/버튼이 영역 안에 있고 최상위 버튼만 실제 raycast되는지 확인한다.
- [ ] 외부 정지/백그라운드 결과음 복귀 1회·중복 0, restart/next 입력 중복 0, 종료 시 뷰/구독 정리를 확인한다. 물리 Android 입력 미검증은 명시한다.
- [ ] 사용자가 선택한 실행 방식의 리뷰 절차를 적용한다. 직접 실행은 최종 독립 리뷰 한 번, 작업별 하위 에이전트 실행은 해당 스킬의 작업별 리뷰. 재현된 관련 결함만 수정하고 마지막 변경의 영향 검사를 수행한다.
- [ ] `Docs/Verification/project-wide/popup-framework-stage-03.md`, 사용 안내와 별도 목표의 조건별 증거를 갱신하고 diff/GUID/원본 보존을 감사한다. 모두 입증한 뒤에만 선택한 목표를 완료한다.

## 빌드 없는 실행·검증 명령

아래 진입점은 신규 검사 구현 후 존재한다. 지금 실행한 것으로 해석하지 않는다. 실행 중 사용자 Editor가 있으면 검사 충돌을 확인하고 임의 종료하지 않는다. 검사 프로세스는 자신이 소유한 숨김 프로세스이며 실패/통과와 무관하게 finally 정리 후 EditorApplication.Exit(exit)를 반환한다.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Projects/Git/ServeredMeridian' -executeMethod PopupUI.Editor.PopupFrameworkVerification.Data -logFile 'C:/Projects/Git/ServeredMeridian/Logs/PopupFramework/data-editor.log'
```

같은 인자에서 executeMethod/logFile을 아래 실행표에 맞춰 바꾼다. 출력 폴더는 실행 전 만들고, Play Mode 검사는 -nographics를 사용하지 않는다. 결과의 FAIL 0과 실제 프로세스 exit 0을 둘 다 확인한다.

| 단계 | executeMethod | 결과 파일 / logFile 파일명 |
| --- | --- | --- |
| 1 | PopupUI.Editor.PopupFrameworkVerification.Data | Logs/PopupFramework/data-results.txt / data-editor.log |
| 1 | PopupUI.Editor.PopupFrameworkVerification.RunInputScene | Logs/PopupFramework/input-results.txt / input-editor.log |
| 2 | PopupUI.Editor.PopupFrameworkVerification.RunRestorationScene | Logs/PopupFramework/restoration-results.txt / restoration-editor.log |
| 3 | PopupUI.Editor.PopupFrameworkVerification.RunTools | Logs/PopupFramework/tools-results.txt / tools-editor.log |
| 3 | GameScreen.Editor.PuzzlePopupVerification.RunScene | Logs/PopupFramework/game-results.txt / game-editor.log |

각 결과 파일은 검사명·현재 실행 시간·PASS/FAIL·정리 결과를 포함한다. 과거 결과와 임시 fixture가 실제 배포 증거로 혼동되지 않도록 구분한다. 실행 전 검사/에셋 변경 범위와 해시를 기록하고 소유한 테스트 자원만 정리한다.

## 자체 검토와 인계

설계의 관리/중첩/입력/중복/정지는 1단계, 씬 이동/복원/실패/문맥은 2단계, 도구/등록/템플릿/기존 호출부는 3단계로 대응했다. Review Focus 다섯 항목에 각각 검사를 배정했다. 단계 간 타입/반환 계약은 공통 인터페이스를 사용한다. 목표의 12개 조건과 단계 경계는 별도 목표 문서에서 추적한다.

권장 실행 방식은 직접 순차 구현(Native)이다. 세 단계가 같은 관리/복원 계약에 의존하므로 한 구현자가 이어서 작업하면 계약 변경과 문맥 전달을 추적하기 쉽다. 사용자가 작업별 하위 에이전트를 선택하면 그 방식으로 전환한다. 구현은 계획 검토와 실행 방식 선택 후 시작한다.


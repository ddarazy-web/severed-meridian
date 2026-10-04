# 공통 팝업 프레임워크 사용 안내

상태: 2026-10-03 공통 서비스·복원·관리/생성 도구와 게임 세 팝업 연결 구현. 리뷰 수정 후 최종22회 회귀와 완료 감사를 마쳤다.

[1단계 계획](../../Planning/project-wide/popup-framework-stage-01-plan.md) · [완료 조건](../../Goals/project-wide/popup-framework-stage-01-goal.md) · [검증 기록](../../Verification/project-wide/popup-framework-stage-01.md)

## 구성과 시작

Editor 도구는 `Tools/Popup/관리`, `Tools/Popup/등록 검사`, `Tools/Popup/템플릿 생성`에서 연다. 관리 창에서 catalog를 선택해 등록 목록을 편집하면 Undo/Redo를 사용할 수 있다. 저장은 `검사 후 카탈로그 저장` 버튼에서 명시적으로 수행하며 오류 등록은 저장을 거부한다. 등록 검사 메뉴는 Project에서 선택한 catalog의 항목별 진단을 Console에 표시한다. Unity가 보완한 저장 파일의 CanvasGroup 누락은 프리팹을 확인하고 다시 저장해 해결한다.

관리 창의 실행 Host를 선택하면 아래→위 목록·Top·핸들·정책·보관 문맥·이동 준비를 조회한다. Play Mode에서는 catalog 편집/저장을 막고 선택한 Host만 시험 열기/핸들 닫기할 수 있다. 이동 준비 중에는 시험 변경을 거부한다. 초기 상태가 필수인 종류는 게임의 실제 호출부에서 시험한다. 창을 닫아도 팝업 목록을 닫거나 원본 catalog를 저장하지 않는다.

템플릿 창에 기능 이름·팝업 이름·등록 ID를 입력한다. 이름은 ASCII C# 식별자이며 예약어/경로 이탈/Windows 장치 디렉터리 이름은 거부한다. `Assets/Scripts/Features/<기능>/Runtime/UI/<이름>State.cs`, `<이름>View.cs`를 만들고 실제 컴파일이 끝나면 `Assets/Prefabs/UI/<기능>/<이름>.prefab`을 연결한다. 출력 파일 또는 메타가 이미 있으면 전부 생성 전에 거부하며 덮어쓰지 않는다. 컴파일 실패는 Failed로 표시하고 프리팹을 만들지 않는다. 생성 완료 후 관리 창의 catalog에 입력했던 ID와 생성 프리팹을 직접 등록하고 검사·저장한다. 생성만으로 원본 catalog는 변경하지 않는다.

코드는 `Assets/Scripts/Systems/Popup/Runtime/`, 기본 프리팹은 `Assets/Prefabs/UI/Popup/`에 있다. `PopupHost.prefab`은 Canvas/Scaler/Raycaster/Host를, `PopupTemplate.prefab`은 배경·패널·기본 선택·닫기 버튼을 제공한다. 기존 게임 화면은 아래 3단계의 카탈로그/Binding 연결을 사용한다.

씬에는 InputSystemUIInputModule이 연결된 EventSystem을 하나 둔다. 프리팹을 직접 참조해 표시 영역을 생성하고 catalog를 연결한다. 종류는 PopupCatalog의 Inspector Entries 또는 Configure로 등록한다. 같은 종류의 ID를 중복 등록하거나 프리팹을 누락하면 서비스 생성이 거부된다.

```csharp
PopupService service = new PopupService(catalog);
host.Attach(service, new PopupContext("scene-key", "feature-key", "session-key"));
host.ApplySafeArea(Screen.safeArea, new Vector2(Screen.width, Screen.height));
PopupHandle notice = service.Open("notice", null);
PopupHandle confirm = service.Open("confirm", null);
service.Close(notice); // 중간 항목만 제거하며 confirm은 유지한다.
```

기능 소유자가 scene/feature/session 값을 제공한다. 보관/복원은 아래 2단계 API로 명시 호출하며 게임 연결은 3단계 절차를 따른다. 화면 크기/안전 영역이 바뀌면 ApplySafeArea를 다시 호출한다. 호스트 배경 입력 차단은 전체 영역을 덮고 팝업 내용은 안전 영역에 표시한다.

## 종류별 정책

| 항목 | 동작 |
| --- | --- |
| AllowMultiple=false | 기존 핸들을 유지하며 최상위로 이동, 명시적으로 전달한 새 내용 적용 |
| AllowMultiple=true | 새 핸들로 독립 인스턴스 생성 |
| PauseGameplay=true | 살아 있는 동안 정지 요청, 다른 팝업 아래에서도 유지 |
| CloseOnCancel=true | ESC/취소 입력을 해당 뷰 HandleCancel로 전달, 기본 구현은 닫기 |
| CloseOnCancel=false | 취소를 소비하며 아래 팝업을 닫지 않음 |
| Restorable | 후속 2단계 계약에 사용할 등록 값, 현재 복원 동작 없음 |

닫힌 핸들 Close는 false를 반환한다. CloseAll은 열린 목록과 뷰를 정리한다. host.Detach/비활성/파괴 시 해당 서비스의 팝업도 정리되고 정지/입력 차단 요청이 해제된다. 서비스는 다른 호스트에 다시 연결할 수 있다. 동시에 한 호스트만 연결한다.

## 내용과 행동

동적인 내용은 PopupState 하위 타입의 Copy로 깊은 복사하고 PopupView의 ApplyState/CaptureState를 재정의한다. 기본 PopupView는 정적 프리팹용으로 내용 상태가 없다. 이전 내용 적용에 실패하면 캡처한 이전 상태를 다시 적용하므로 CaptureState는 자신의 정상 상태를 재적용할 수 있는 값으로 반환해야 한다. 상태 객체에 Unity 오브젝트나 콜백을 넣지 않는다.

행동 버튼은 뷰에서 현재 기능을 연결한다. 뷰의 Close는 바인딩된 자기 핸들만 닫는다. 기본 템플릿 Close 버튼은 이 메서드에 직렬화 연결돼 있다. 키 입력을 각 뷰에서 별도로 polling하지 않는다. 최상위 아닌 뷰는 CanvasGroup 입력이 차단되며 취소는 선택된 UI의 PopupInputGate를 통해 한 번만 전달된다. 동적으로 추가한 컨트롤도 선택되면 Host의 입력 처리 전 갱신에서 Gate를 연결한다. 방향키 탐색은 최상위 내부로 제한하며 같은 프레임에 외부 UI로 Submit/Cancel을 전달하지 않는다. 이전 선택이 비활성화되면 유효한 기본 선택으로 복귀한다. 등록/템플릿 도구는 3단계에서 제공한다.

## 게임 연결의 책임

```csharp
System.Action<bool> blockHandler = blocked =>
{
    popupBlocked = blocked;
    // 외부 차단 이유는 유지하고 최초 차단 시 진행 중 제스처/선택을 취소한다.
    ApplyBlocked(externalBlocked || popupBlocked);
};
System.Action<bool> pauseHandler = paused =>
{
    popupPaused = paused;
    ApplyPaused(externalPaused || popupPaused);
};
host.InputBlockChanged += blockHandler;
host.PauseRequestChanged += pauseHandler;

// 기능 연결이 끝나면 소유한 구독만 해제한다.
// blockHandler/pauseHandler는 연결 소유자의 필드 등에 보관한다.
host.InputBlockChanged -= blockHandler;
host.PauseRequestChanged -= pauseHandler;
```

ApplyBlocked/ApplyPaused와 상태 필드는 호출 기능에서 구현하는 예시다. 프레임워크가 Time.timeScale이나 게임의 외부 정지를 직접 바꾸지 않는다. 연결 소유자는 종료 시 자신이 구독한 핸들러를 해제한다. 같은 람다 표현식을 새로 만들어 빼는 대신 보관한 delegate 또는 이름 있는 메서드를 사용한다.

알림 콜백에서 팝업을 닫거나 호스트를 해제할 수 있다. 콜백 중 요청 상태가 바뀌면 이전 상태의 남은 알림 전달을 중단하고 새 상태를 전달한다. Detach/비활성화는 요청을 해제하지만 외부 소유자의 이벤트 구독 자체를 임의 삭제하지 않는다.

실제 PuzzleGameSession/BoardInput 연결은 3단계에서 수행한다. 1단계 시험에서는 외부 차단/정지와 제스처 취소를 시험 연결로 검증한다.

## 검사와 한계

검사 진입점은 `PopupUI.Editor.PopupFrameworkVerification.Data`, `RunInputScene`이다. 소유한 별도 숨김 Unity 프로세스에서만 실행한다. 종료 시 EditorApplication.Exit를 호출하므로 사용자 작업 중인 에디터에서 호출하지 않는다. 상세 명령은 계획서를 따른다.

가상 Keyboard/Mouse/Touchscreen은 Editor의 실제 EventSystem 경로를 검사한다. 물리 Android 뒤로가기/터치와 동시에 사용되는 복수 물리 입력 장치의 품질을 입증하는 검사는 아니다. 플레이어/Addressables 콘텐츠 빌드는 실행하지 않는다.


## 2단계 — 필요한 이동에서만 상태 복원

상태: 2단계 구현·검증 완료. 3단계의 실제 게임 연결·관리 도구는 구현됐으며 전체 최종 검증/감사는 완료했다. [2단계 검증 기록](../../Verification/project-wide/popup-framework-stage-02.md)과 [3단계 진행 기록](../../Verification/project-wide/popup-framework-stage-03.md)을 따른다.

서비스는 이동 호출 기능이 앱 실행 중 소유한다. 기존 서비스/catalog를 유지하고 씬의 Canvas/EventSystem/PopupHost/팝업 객체는 새로 만든다. 게임 세션을 프레임워크가 유지하는 것은 아니다. 게임의 논리 sessionKey는 원래 게임에 돌아온 경우만 동일하게 제공하고 재시작/다음 레벨/새 게임은 새 값을 사용한다.

호출 순서:

```csharp
// 아래 이동 준비/차단 함수는 호출 기능에서 제공하는 예시다.
PopupExitTicket ticket = null;
SetTransitionInputBlocked(true);
try
{
    ticket = service.BeginSceneExit(preserve: true);
    await PrepareDestinationKeepingSourceAliveAsync();
    service.CommitSceneExit(ticket);
    await FinishSourceUnloadAsync();
}
catch
{
    if (ticket != null && ticket.IsPending) service.RollbackSceneExit(ticket);
    throw;
}
finally
{
    // 캡처/이동/확정/취소/언로드 실패에도 이번 이동 차단만 해제한다.
    SetTransitionInputBlocked(false);
}

// 목적지에 머무는 동안 이동 차단을 유지하지 않는다.
// 이후 원래 씬으로 돌아와 새 Host와 현재 연결이 준비된 별도 시점
newHost.Attach(service, sameContext);
PopupRestoreResult result = service.Restore(newHost, sameContext);
```

PrepareDestinationKeepingSourceAliveAsync/FinishSourceUnloadAsync/SetTransitionInputBlocked는 실제 API가 아닌 호출 기능 예시다. 차단 함수는 이번 이동 이유만 추가/제거하고 다른 차단 이유를 보존한다. Commit 전에 원래 Host가 살아 있어야 한다. 목적지 준비 성공 후 Commit하고 원래 씬을 언로드하는 Additive/준비 단계 순서를 사용한다. 단일 씬 로드로 원래 Host를 먼저 파괴하면 pending ticket은 무효화되며 뒤늦은 Commit으로 보관할 수 없다. ticket.IsPending은 읽기 전용 상태이며 소비/Host 종료 후 false다. Commit 후 언로드가 실패해도 Rollback하지 않고 확정된 보관을 유지하며 호출 기능이 이동 오류를 처리한다.

이동 대기 중 Open/Close/CloseAll은 거부한다. 호출 기능은 보드 입력과 팝업 EventSystem 입력도 별도로 막고 실패 시 원래 입력 정책으로 복귀한다. 프레임워크는 자동 이동/전역 입력 차단 이유/게임 정지를 대신 소유하지 않는다.

일반 캡처 예외는 기존 표시/보관을 유지하며 다음 Begin을 시도할 수 있다. 캡처 콜백 중 Host가 실제 종료된 경우에는 캡처를 중단하고 그 Host의 뷰/연결/요청을 정리한다. 종료된 Host의 표시를 보존하거나 중단된 캡처를 확정하지 않는다.

preserve=false도 Begin→성공 Commit/실패 Rollback을 따른다. Commit에서 해당 씬의 오래된 보관을 폐기하고 Rollback은 이전 보관까지 보존한다. 빈 캡처 Commit도 이전 묶음을 지운다. 소비된/다른 서비스/ticket Host 종료 이후 Commit·Rollback은 거부한다. 소비된 ticket의 Owner/Host/항목 참조는 해제된다.

Restore는 현재 service에 같은 context로 Attach한 활성 빈 Host에서 호출한다. None은 해당 씬의 보관 없음, ContextMismatch는 같은 sceneKey의 feature/session 불일치와 폐기, Restored는 새 핸들 목록/성공 소비, Failed는 Error/빈 Handles다. 이미 열린 Host에 복원하려 하면 Failed이며 기존 목록을 보존한다. 성공 후 목록을 닫아 빈 Host에서 재호출하면 None이다. 다른 씬에서 요청한 None은 원래 씬의 묶음을 지우지 않는다.

실패한 복원은 후보와 현재 연결을 정리하고 보관 값은 유지한다. 오류 원인을 고친 뒤 Restore를 명시 재호출하거나 Discard(exactContext)로 그 문맥만 버린다. 자동 재시도와 닫힌 팝업 재생성은 없다.

뷰 확장:

- CaptureState/Copy는 값만 깊게 복사한다. 입력값/탭/선택/스크롤은 해당 PopupState에 직접 정의한다.
- CaptureFocusKey/ResolveFocusKey는 기본 sibling-index 경로를 사용한다. 동적 구조가 바뀌는 뷰는 의미 있는 키를 재정의한다. 누락/비활성 선택은 유효 기본 선택으로 돌아간다.
- PrepareRestore(context)는 비활성 후보의 새 데이터/핸들 바인딩 후 현재 기능에 연결한다. 이전 delegate/Unity 객체를 보관 상태에 넣지 않는다.
- ReleaseRestore()는 준비 중 실패한 후보에도 호출된다. 부분 연결도 해제하고 반복 호출에 안전하며 예외를 던지지 않게 구현한다. 활성 뷰의 OnDestroy에서도 같은 해제를 호출할 수 있다.
- ApplyState/PrepareRestore/OnEnable은 표현·연결만 담당한다. 구매·재시작·Next·보상·결과음은 명시적 행동에서만 호출하며 복원으로 재실행하지 않는다.

비활성 후보에서는 OnDestroy만으로 해제를 보장할 수 없으므로 명시 해제를 제공한다. [Unity 6000.3 OnDestroy 문서](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/monobehaviour/ondestroy)

검사 진입점 RunRestorationData/RunRestorationScene은 소유한 검사 프로세스 전용이며 EditorApplication.Exit를 호출한다. 물리 Android/복수 물리 장치는 미검증이다.

## 3단계 — 현재 게임 연결

`PuzzleScreen` 프리팹의 `PuzzlePopupBinding`이 카탈로그와 SafeArea 아래 `PopupHost`를 연결한다. 세 종류는 `puzzle.pause`, `puzzle.result`, `puzzle.description`이며 중복 인스턴스를 허용하지 않는다. pause만 정지 요청을 가진다. pause/description은 취소로 닫히고 result는 취소로 닫히지 않는다. Screen의 Refresh는 기존 결과를 갱신하며 중첩 순서를 바꾸지 않는다.

게임 화면에서 설명은 `binding.OpenDescription(text)`, 일시정지는 `binding.OpenPause()`로 연다. 허용되지 않는 게임 상태에서 OpenPause는 거부되므로 일반 UI는 기존 pause 버튼의 활성 정책을 따른다. 결과는 게임 Outcome과 표시/수집 완료에 따라 Binding이 표시한다. View에는 값 State만 저장하고 게임 객체나 콜백을 보관하지 않는다.

게임 외부 정지는 `session.SetPaused(...)`, 외부 입력 차단은 `input.SetUIBlocked(...)`의 기존 소유권을 유지한다. 팝업 정지/차단과 Screen의 결과·로딩 차단은 별도 요청으로 합성한다. 외부 코드는 팝업 요청 해제 API로 자신의 정지를 해제하지 않는다. Retry 후보는 자신의 pausing popup 때문에 대기하지 않으며 외부 정지와 백그라운드는 보존한다.

재시작/Next는 후보 데이터·보드·이미지 준비와 표시가 성공했을 때만 새 `LogicalSessionId`를 확정한다. 실패/취소는 기존 플레이·이미지·문맥을 유지하고 기존 결과 버튼을 복구한다. 성공 시 이전 문맥의 열린 팝업과 보관을 폐기한다.

일반 게임 진행은 자동 보관/복원하지 않는다. 필요한 호출 기능이 위 2단계 이동 계약으로 service를 보관한 뒤 같은 scene/feature/session 문맥의 새 Host에 Attach한다. 현재 Screen/Binding을 현재 게임과 입력에 Configure한 다음 `service.Restore(host, sameContext)`를 명시 호출한다. Binding은 이 경우 `host.Service`를 채택하고 보관이 있으면 Restore 전 새 결과를 만들지 않는다. 게임 세션 유지·왕복 게임 기능·디스크 저장은 이 프레임워크의 기능이 아니다.

복원은 현재 부모 Binding의 PrepareRestore로 버튼을 연결하며 Retry/Next/보상/결과음을 실행하지 않는다. Binding이 없는 후보는 Failed로 정리되고 보관은 남아 재시도할 수 있다. 새 Binding을 구성한 뒤 오류 원인을 고쳐 명시 Restore를 다시 호출한다.

기존 프리팹 생성의 `PuzzleUIAssets.GeneratePrefabs(folder)`와 `PuzzleLevelTransitionAssets.ApplyToPrefab(path)`는 씬을 저장하지 않는 진입점이다. 전체 Generate는 기존 씬 연결 작업을 포함하므로 팝업 프리팹만 갱신할 때 대신 호출하지 않는다. `PuzzlePopupAssets.Apply()`는 지정 팝업과 화면의 공통 연결을 적용한다. 검증 전용 RunScene/RunRestart/RunResult/RunRestore/RunDesign/RunGenerators는 소유 검사 프로세스에서만 호출한다.

## 추가 등록과 재실행 주의점

`Tools/Popup/템플릿 생성`은 ASCII C# 식별자를 사용한다. 기능 이름과 팝업 이름 모두 Windows 예약 장치 이름(CON/PRN/AUX/NUL/COM1~9/LPT1~9)을 대소문자와 관계없이 거부한다. 이름/경로/출력 충돌은 파일 생성 전에 검사한다. 거부된 요청의 진단은 이전 생성 성공 표시로 덮지 않고 다음 실제 요청 진행이 바뀔 때 갱신한다. 실제 컴파일 오류는 생성 소스를 남겨 수정할 수 있게 하며 프리팹 완료로 표시하지 않는다.

게임 팝업 연결을 재실행하면 도구가 소유한 pause/result/description 세 ID의 프리팹과 고정 정책을 갱신한다. 관리 창에서 추가한 다른 ID와 프리팹/정책은 보존한다. 기존 full UI 생성기는 이전과 같이 팩토리의 시각 구성을 재생성하므로 커스텀 시각 편집이 있는 프리팹에는 그 동작 범위를 확인한다.

복원 전에는 현재 Screen/Binding을 게임 세션·입력에 Configure하고 활성 Host가 같은 service/context를 소유해야 한다. 컴포넌트가 존재해도 미구성/비활성이거나 다른 Host/문맥이면 PrepareRestore에서 거부한다. 실패한 후보는 정리하고 snapshot은 유지하므로 연결을 준비한 뒤 명시적으로 다시 Restore한다. 설명도 현재 게임 연결을 확인한다.

같은 Binding/세션의 반복 Configure는 기존 연결을 Refresh한다. 다른 catalog/Host로 바꾸려면 먼저 Release한 뒤 필요한 연결 인자를 지정해 Configure한다. Binding 컴포넌트 자체를 비활성화했다가 다시 켜는 호출자는 현재 게임/입력으로 다시 구성한다. Screen 전체 비활성화/재활성화는 기존 Screen의 Bind 흐름을 사용한다. 자동 게임 영속화나 자동 복원은 제공하지 않는다.

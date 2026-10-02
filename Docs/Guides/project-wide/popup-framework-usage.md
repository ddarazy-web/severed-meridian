# 공통 팝업 프레임워크 사용 안내

상태: 2026-10-02 1단계 구현·검증 완료. 씬 복원·관리 창·기존 게임 팝업 전환은 아직 제공하지 않는다.

[1단계 계획](../../Planning/project-wide/popup-framework-stage-01-plan.md) · [완료 조건](../../Goals/project-wide/popup-framework-stage-01-goal.md) · [검증 기록](../../Verification/project-wide/popup-framework-stage-01.md)

## 구성과 시작

코드는 `Assets/Scripts/Systems/Popup/Runtime/`, 기본 프리팹은 `Assets/Prefabs/UI/Popup/`에 있다. `PopupHost.prefab`은 Canvas/Scaler/Raycaster/Host를, `PopupTemplate.prefab`은 배경·패널·기본 선택·닫기 버튼을 제공한다. 기존 게임 화면은 이번 단계에서 연결하지 않았다.

씬에는 InputSystemUIInputModule이 연결된 EventSystem을 하나 둔다. 프리팹을 직접 참조해 표시 영역을 생성하고 catalog를 연결한다. 종류는 PopupCatalog의 Inspector Entries 또는 Configure로 등록한다. 같은 종류의 ID를 중복 등록하거나 프리팹을 누락하면 서비스 생성이 거부된다.

```csharp
PopupService service = new PopupService(catalog);
host.Attach(service, new PopupContext("scene-key", "feature-key", "session-key"));
host.ApplySafeArea(Screen.safeArea, new Vector2(Screen.width, Screen.height));
PopupHandle notice = service.Open("notice", null);
PopupHandle confirm = service.Open("confirm", null);
service.Close(notice); // 중간 항목만 제거하며 confirm은 유지한다.
```

기능 소유자가 scene/feature/session 값을 제공한다. 현재 단계는 이 값을 비교 가능한 계약으로 제공할 뿐 보관/복원을 구현하지 않는다. 화면 크기/안전 영역이 바뀌면 ApplySafeArea를 다시 호출한다. 호스트 배경 입력 차단은 전체 영역을 덮고 팝업 내용은 안전 영역에 표시한다.

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


# 팝업 프레임워크 1단계 — 공통 관리·입력 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking. 본 명령문으로 구현을 요청하면 직접 순차 실행을 선택한 것으로 본다.

**Goal:** 팝업을 핸들로 열고 닫으며 중첩 순서·최상위 입력·포커스·입력 차단/정지 요청을 공통 관리한다.

**Architecture:** PopupService가 인스턴스 순서와 정책을 관리하고 PopupHost가 씬 Canvas와 EventSystem을 연결한다. PopupCatalog는 종류별 프리팹과 정책을 등록한다. 게임의 외부 차단/정지는 연결 지점에서 합성하며 공통 시스템이 임의 해제하지 않는다.

**Tech Stack:** Unity 6000.3.10f1, 기존 uGUI/Input System, 직접 프리팹 참조. 새 패키지 없음.

**Spec:** [승인 설계](../../Systems/project-wide/2026-10-02-popup-framework-design.md). [전체 계획/타입 계약](2026-10-02-popup-framework-plan.md) · [1단계 목표](../../Goals/project-wide/popup-framework-stage-01-goal.md) · [복사용 명령문](../../Commands/project-wide/popup-framework-stage-01-command.md).

상태: 2026-10-02 계획 정리 완료 · 1단계 구현·검증 완료. 최종 8개 검사와 원본 보존 감사는 검증 문서를 따른다. 완료한 구현을 반복하지 않는다. 범위는 전체 계획의 1단계뿐이다. 명령문을 실행할 때 이 문서의 단계별 작업 경계를 우선 적용한다.

## Global Constraints

- ServeredMeridian만 대상으로 한다. 기존 사용자 변경·규칙·이미지·레벨·씬·GUID·완료 Stage13을 보존한다.
- 플레이어/Addressables 콘텐츠 빌드·커밋·푸시·사용자 Unity 종료·자동 씬 저장은 하지 않는다.
- 기존 uGUI와 직접 프리팹 참조를 사용한다. 패키지·asmdef·DI·풀링·별도 비동기 로더는 추가하지 않는다.
- 씬 복원 구현·관리 창·템플릿 생성 도구·기존 게임의 세 팝업 전환은 각각 2~3단계로 남긴다.
- PopupContext/PopupState는 합의된 후속 계약만 정의하고 저장소/디스크/게임 진행 저장은 구현하지 않는다.
- 각 작업의 RED/통과·정리·현재 diff를 기록한다. 커밋 단계는 기록으로 대체한다.

## 파일과 공개 계약

전체 계획의 공통 인터페이스를 그대로 사용한다. Runtime namespace는 PopupUI, Editor namespace는 PopupUI.Editor다.

| 생성할 파일 | 책임 |
| --- | --- |
| Assets/Scripts/Systems/Popup/Runtime/PopupHandle.cs | long Value 고유 핸들, 종료 후 재사용 금지 |
| 같은 폴더 PopupContext.cs | sceneKey/featureKey/sessionKey 값 비교 |
| 같은 폴더 PopupState.cs | 값 상태의 abstract Copy() 깊은 복사 계약 |
| 같은 폴더 PopupCatalog.cs | 종류 ID/Prefab/AllowMultiple/PauseGameplay/CloseOnCancel/Restorable |
| 같은 폴더 PopupView.cs | ApplyState/CaptureState/HandleCancel/DefaultSelection |
| 같은 폴더 PopupService.cs | Open/Close/CloseAll/Top/Count/Changed |
| 같은 폴더 PopupHost.cs | Attach/Detach, InputBlockChanged/PauseRequestChanged, CanvasGroup/포커스/입력 |
| 같은 폴더 PopupInputGate.cs | 선택된 컨트롤의 방향키/취소 입력을 최상위 팝업 안으로 제한 |
| Assets/Scripts/Systems/Popup/Editor/Tests/PopupFrameworkVerification.cs | Data 검사와 결과/종료 코드 기록 |
| 같은 폴더 PopupFrameworkVerification.Input.cs | 실제 EventSystem/표시/키/포인터/정리 검사 |
| 같은 폴더 PopupFrameworkVerification.Review.cs | 리뷰에서 재현한 입력/포커스/알림 결함의 회귀 검사 |
| 같은 폴더 PopupFrameworkVerification.Assets.cs | 지정 신규 프리팹의 생성·직렬화 검사, 관리 도구와 구분 |
| Assets/Prefabs/UI/Popup/PopupHost.prefab | 씬 표시 영역의 최소 공통 프리팹 |
| Assets/Prefabs/UI/Popup/PopupTemplate.prefab | 검사에서 사용할 최소 뷰 프리팹, 생성 도구는 미포함 |

모든 신규 Unity 파일/실제 폴더의 .meta는 고유 GUID로 생성한다. 시험 catalog는 임시 자원으로 소유/정리하고 실제 게임용 catalog 등록은 3단계에 둔다. 기존 게임 UI와 씬은 수정하지 않는다.

## Review Focus

1. 최상위 닫기 입력 한 번이 아래 팝업도 닫지 않는가(Task 2).
2. 클릭 콜백에서 자신/다른 팝업 제거 후 새 팝업을 열어도 순서가 일관적인가(Task 1~2).
3. 다시 연 단일 인스턴스의 새 내용 적용 실패가 기존 내용/순서를 깨뜨리는가(Task 1).
4. 닫기 금지 최상위에서 취소·Submit이 아래 UI/게임으로 누출되는가(Task 2).
5. 호스트 비활성/파괴·정지 팝업 중간 제거가 외부 정지를 해제하거나 구독을 남기는가(Task 3).

## Task 1 — 등록·핸들·순서

**Files:** PopupHandle/Context/State/Catalog/View/Service, Verification.Data.

**Consumes:** 기존 Unity 타입과 직접 프리팹. **Produces:** `Open(string id, PopupState state): PopupHandle`, `Close(PopupHandle): bool`, `CloseAll(): void`, `Top: PopupHandle?`, `Count: int`, `event Action Changed`와 전체 계획의 값 계약.

- [x] 착수 시 Git 상태/HEAD와 기존 관련 코드·프리팹·GUID를 기록한다. 무관한 dirty를 보존한다.
- [x] Data에 A→B→C 후 B만 닫아 A/C·Top=C·Count=2, B 재닫기 false·불변, CloseAll 후 Count=0을 검사한다.
- [x] 단일 A 재열기의 같은 핸들·Count 불변·맨 위 이동·새 내용 적용, 복수 종류의 다른 핸들, 미지/중복 ID와 누락 프리팹 거부 검사를 작성한다.
- [x] 신규/기존 뷰 ApplyState 실패에서 후보 정리·기존 순서/내용 불변 검사를 작성한다. 상태를 먼저 복사하고 적용 실패 시 이전 상태를 복구한다.
- [x] 검사 실행으로 미구현 동작의 실패를 기록한다. 타입 미존재 컴파일 실패와 동작 RED는 구분한다.
- [x] 계약대로 관리 구현을 추가한다. 단일 등록 종류의 재열기는 명시적 새 내용을 적용한 뒤 순서를 변경한다. 닫힌 핸들로 다른 인스턴스를 조작하지 않는다.
- [x] Data를 다시 실행해 FAIL 0·실제 exit 0과 후보/임시 자원 정리를 확인한다.

## Task 2 — 실제 표시·입력·포커스

**Files:** PopupHost/View/Service, Verification.Input, 공통 프리팹 2개.

**Consumes:** Task 1 관리 API. **Produces:** `Attach(PopupService, PopupContext): void`, `Detach(): void`와 최상위 전용 입력/포커스.

- [x] RunInputScene에 실제 Canvas/EventSystem/InputSystemUIInputModule과 A/B/C 버튼을 구성한다. 아래 클릭/Submit/Cancel 횟수 0, 최상위 횟수 1을 검사한다.
- [x] 취소로 C를 닫은 입력이 B로 재전달되지 않음, 닫기 금지 C가 취소를 소비함, 이전 유효 선택/기본 선택으로 포커스 복귀를 검사한다.
- [x] 아래 화면까지 덮는 입력 차단과 실제 pointer raycast를 검사한다. 키 반복 프레임·콜백 중 닫기/재열기에서도 입력당 동작 1회를 검사한다.
- [x] 실패 근거를 저장하고 sibling 표시 순서/CanvasGroup/포커스/단일 입력 경로를 구현한다. EventSystem Cancel과 별도 키 polling으로 중복 전달하지 않는다.
- [x] 1280×720·450×800·450×975·600×800에서 최상위 버튼 raycast와 화면 영역을 확인한다. 비영점 안전 영역 적용도 검사한다.
- [x] 같은 검사를 통과시키고 현재 실행의 UI 캡처를 열어 확인한다. 가상 입력 검사를 물리 Android 입력 검증으로 표현하지 않는다.

## Task 3 — 차단·정지·정리·완료 감사

**Files:** PopupHost/Service, 두 검사 파일, 단계 검증/사용 안내 문서.

**Consumes:** Task 1~2 계약. **Produces:** `event Action<bool> InputBlockChanged`, `event Action<bool> PauseRequestChanged`와 정리 보장.

- [x] 정지 요청 A/B와 비정지 C를 순서/중간 제거로 닫으며 마지막 정지 요청까지만 true인지 검사한다. 외부 정지 true + 팝업 정지 false일 때 합성 결과 true, 외부 입력 차단도 유지됨을 시험 연결에서 확인한다.
- [x] 열기 중 이전 보드 제스처/선택을 취소하는 연결 이벤트와 일반 UI 차단을 시험 연결에서 확인한다. 실제 PuzzleGameSession/BoardInput은 이번에 바꾸지 않는다.
- [x] host 비활성/파괴·Detach 반복·다시 Attach의 살아 있는 뷰/구독 수를 검사하고 정리 상태를 구현한다. `Time.timeScale`은 수정하지 않는다.
- [x] 마지막 변경 후 Data/RunInputScene을 통과시키고 변경 때문에 필요한 기존 컴파일/영향 검사를 확인한다.
- [x] 방향키와 같은 프레임의 Submit/Cancel이 외부 UI로 누출되지 않는지, 비활성 선택의 기본 선택 복귀, 동적 컨트롤의 취소 입력을 회귀 검사한다.
- [x] 차단/정지 알림 콜백에서 CloseAll/Detach/추가 열기를 수행해 최종 요청 상태와 각 구독자의 관찰 값이 일치하는지 검사한다. 중첩 알림 이후 오래된 값이 다른 구독자에게 전달되지 않아야 한다.
- [x] 직접 순차 구현 후 독립 최종 리뷰를 한 번 수행한다. 관련 재현 결함만 최소 수정하고 해당 영향 검사를 재실행한다.
- [x] `Docs/Verification/project-wide/popup-framework-stage-01.md`에 목표 조건별 검사명/시간/exit/화면/정리와 한계를, `Docs/Guides/project-wide/popup-framework-usage.md`에 현재 1단계 API 사용 예제를 남긴다.
- [x] diff·고유 신규 GUID·원본 보존을 감사한다. 1단계 조건 모두 입증한 뒤 목표를 완료하고 2단계는 시작하지 않는다.

## 검사 실행

아래 진입점은 기존 1단계 작업에서 생성되었다. 이전 통과 기록을 마지막 변경의 통과로 간주하지 않는다. 사용자 Editor가 열려 충돌하면 강제 종료하지 않는다. 소유한 숨김 검사 프로세스에서 결과를 기록하고 finally 정리 후 EditorApplication.Exit(exit)한다.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Projects/Git/ServeredMeridian' -executeMethod PopupUI.Editor.PopupFrameworkVerification.Data -logFile 'C:/Projects/Git/ServeredMeridian/Logs/PopupFramework/data-editor.log'
& 'C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Projects/Git/ServeredMeridian' -executeMethod PopupUI.Editor.PopupFrameworkVerification.RunInputScene -logFile 'C:/Projects/Git/ServeredMeridian/Logs/PopupFramework/input-editor.log'
```

실행 전 Logs/PopupFramework 폴더를 만들고 두 프로세스는 앞 실행이 실제 종료된 뒤 순차 실행한다. Play Mode 검사에는 -nographics를 쓰지 않는다. `data-results.txt`, `input-results.txt`의 FAIL 0과 실제 exit 0을 함께 확인한다. 빌드/사용자 씬 저장은 포함하지 않는다.

## 자체 검토

전체 설계의 1단계 범위는 Task 1 관리/중복, Task 2 최상위 입력/표시, Task 3 정지/정리에 대응한다. Review Focus 다섯 항목을 각 검사에 배정했다. 후속 복원 계약은 값 타입만 두며 실제 게임 전환은 제외했다. 상세 완료 판정은 별도 목표 문서를 따른다.


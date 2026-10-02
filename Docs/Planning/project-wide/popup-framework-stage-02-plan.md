# 팝업 프레임워크 2단계 — 선택적 씬 복원 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking. 복사용 명령문으로 구현을 요청하면 직접 순차 실행(Native)을 선택한 것으로 본다.

**Goal:** 이동 호출자가 선택한 팝업만 메모리에 보관하고, 원래 씬·같은 논리 문맥의 새 표시 영역에서 명시적으로 복원한다.

**Architecture:** 기존 PopupService를 partial로 확장하고 앱 실행 중 유지되는 서비스가 PopupSnapshotStore를 소유한다. 이동 ticket은 캡처와 확정/취소를 분리한다. 복원은 새 비활성 뷰의 데이터·현재 기능 연결을 모두 준비한 뒤 목록·입력을 한 번에 활성화한다.

**Tech Stack:** Unity 6000.3.10f1, 기존 uGUI/Input System/UniTask, 직접 프리팹 참조. 신규 패키지 없음.

**Spec:** [승인 설계](../../Systems/project-wide/2026-10-02-popup-framework-design.md) · [전체 계약](2026-10-02-popup-framework-plan.md) · [2단계 목표](../../Goals/project-wide/popup-framework-stage-02-goal.md) · [복사용 명령문](../../Commands/project-wide/popup-framework-stage-02-command.md)

작성일: 2026-10-02. 상태 갱신: 2026-10-03 · 2단계 구현·리뷰 처리·최종 8회/157 PASS 완료. [2단계 검증](../../Verification/project-wide/popup-framework-stage-02.md)이 최종 증거다. 1단계의 기존 완료와 동작을 보존했다.

## Global Constraints

- ServeredMeridian만 대상으로 한다. 기존 사용자 변경·1단계·Stage13·게임 규칙/레벨/이미지/씬/프리팹/패키지/GUID를 보존한다.
- 빌드·Addressables 콘텐츠 빌드·커밋·푸시·사용자 Unity 종료·사용자 씬 자동 저장은 하지 않는다.
- 새 패키지/asmdef/DI/범용 로더/풀링을 추가하지 않는다. 씬 왕복 비동기는 기존 UniTask와 취소 규칙을 따른다.
- 앱 실행 중 메모리 보관만 한다. 디스크/앱 재실행/게임 진행 저장은 제외한다. 서비스 수명은 호출 기능이 소유하며 새 전역 singleton/DontDestroyOnLoad 게임 세션은 만들지 않는다.
- 씬 이름만 보고 자동 복원하지 않는다. 호출자가 preserve를 선택하고 새 Host/현재 기능 연결이 준비된 뒤 Restore를 호출한다.
- 보관 상태에는 Unity 오브젝트/delegate/구독/진행 요청/취소 토큰을 넣지 않는다. 새 뷰에서 현재 기능에 다시 연결한다.
- 관리 창/템플릿 생성 도구/실제 게임의 결과·일시정지·설명 전환은 3단계다. 이번에는 시험 어댑터로만 검증한다.

## 확정할 동작과 계약

전체 계획의 SceneExit/Restore/Discard API 이름을 유지한다. Runtime namespace는 PopupUI, Editor는 PopupUI.Editor다.

| 동작 | 구체 계약 |
| --- | --- |
| BeginSceneExit(bool preserve): PopupExitTicket | 활성 Host에서 한 개의 pending ticket만 허용. 복원 가능 인스턴스를 아래→위로 값 복사하며 표시를 제거하지 않음. 일반 캡처 실패는 기존 목록/입력/보관 불변. 캡처 중 Host 자체가 종료되면 캡처 중단과 해당 뷰/구독/요청 정리, 이전 보관 불변 |
| CommitSceneExit(PopupExitTicket): void | 자기 서비스의 유효 pending ticket만 수락. preserve=true는 원래 sceneKey의 보관 상태를 교체, false는 그 씬의 오래된 상태도 폐기. 표시/Host 연결 정리 후 ticket 소비 |
| RollbackSceneExit(PopupExitTicket): void | pending 캡처만 폐기하고 현재 뷰/핸들/포커스/기존 보관 상태 유지. preserve=false의 폐기도 Commit 때만 확정해 실패한 이동은 이전 보관 상태를 잃지 않음 |
| pending 동안 | 표시·정지 소유권 유지, 목록 변경(Open/Close/CloseAll) 거부. 호출 기능은 이동 대기 동안 입력을 별도로 차단. Host가 먼저 소멸하면 ticket 무효화, 미확정 상태는 보관하지 않음 |
| Restore(PopupHost, PopupContext): PopupRestoreResult | 호출자가 먼저 Attach(service, context)한 활성 빈 Host만 허용. 다른 Host/문맥·이미 열린 목록이면 Failed, 기존 표시/보관 상태 불변 |
| 문맥 비교 | 보관은 sceneKey별 한 묶음. 다른 씬에서 Restore는 None이며 원래 씬 상태 유지. 같은 sceneKey의 featureKey/sessionKey 불일치는 ContextMismatch 후 해당 묶음 폐기 |
| 성공/실패 | 성공은 새 핸들 목록(아래→위)을 반환하고 보관 상태 소비. 실패는 Handles 비움/Error 제공·모든 후보 비활성/정리·명시 재시도 또는 Discard 전까지 보관 유지 |
| Discard(PopupContext): void | 그 정확한 scene/feature/session 묶음만 폐기. 살아 있는 뷰나 다른 문맥의 보관 상태는 변경하지 않음 |

PopupExitTicket은 외부가 생성/변경하지 못하는 service 소유 토큰이다. 소비된/다른 서비스/Host 종료 이전 ticket은 InvalidOperationException으로 거부하고 상태를 바꾸지 않는다. 빈 캡처를 Commit하면 이전 묶음을 폐기하며 Restore는 None이다. 이동 실패는 Commit 전에 Rollback한다. Commit은 이동 성공을 호출자가 확정하는 지점이며 프레임워크가 씬 이동 자체를 실행하지 않는다.

`PopupExitTicket.IsPending`은 읽기 전용 상태로 캡처 확정 대기 중 true, 소비/Host 종료 뒤 false다. 호출 예제는 Begin부터 입력 차단 해제 finally까지 보호하고, 유효 pending ticket만 Rollback한다. 확정 이후 언로드 실패는 Rollback하지 않는다.

각 보관 항목은 종류 ID, 원본 핸들 식별 값, 깊게 복사한 PopupState, 값 형태의 포커스 키를 가진다. 복수 허용 같은 종류는 각각 보관하며 순서를 유지한다. 복원은 Open을 반복 호출해 부분 활성화하지 않는다.

PopupView에 다음 책임 API를 추가한다:

- `public virtual string CaptureFocusKey(GameObject selection)`: 현재 뷰 소속 선택을 값 키로 표현. 기본은 루트 기준 자식 sibling-index 경로, 선택 없음은 null.
- `public virtual GameObject ResolveFocusKey(string key)`: 현재 뷰의 키를 해석. 잘못되거나 누락된 경로는 null, 유효성/기본 선택 복귀는 기존 Host 규칙 적용. 동적 컨트롤의 의미 있는 ID가 필요한 뷰는 재정의 가능.
- `public virtual void PrepareRestore(PopupContext context)`: 비활성 후보의 ApplyState/새 핸들 Bind 후 현재 기능 연결을 준비. 기본은 아무 작업 없음. Restore 호출 기능의 시험 어댑터가 새 연결을 제공하며 이전 delegate를 저장하지 않음. 생성/연결 후 발생한 후보별 구독은 명시적 ReleaseRestore와 활성 뷰의 소멸에서 소유자가 해제.

`public virtual void ReleaseRestore()`를 추가한다. 실패한 비활성 후보와 닫힌 복원 뷰의 연결을 해제하며 반복 호출에 안전하고 예외를 던지지 않아야 한다. OnDestroy는 한 번도 활성화되지 않은 후보에서 보장되지 않으므로 서비스가 명시 호출한다.

입력값/탭/스크롤/선택 데이터는 각 PopupState에 명시적으로 정의한다. 임의 Unity 컴포넌트를 반사로 통째 저장하지 않는다. 새 뷰/상태 연결에서 구매·재시작·다음 레벨·보상·결과음은 실행하지 않는다. 복원 처리 중 ApplyState/PrepareRestore에서 목록 변경은 거부한다.

## 파일 지도

| 생성/수정 | 책임 |
| --- | --- |
| Runtime/PopupSnapshotStore.cs 생성 | 씬별 묶음과 항목의 값 보관/깊은 복사/소비. 내부 타입, 서비스가 소유 |
| Runtime/PopupExitTicket.cs 생성 | 서비스/Host 소유 pending ticket과 소비 여부 |
| Runtime/PopupRestoreResult.cs 생성 | Status(None/Restored/ContextMismatch/Failed), IReadOnlyList<PopupHandle> Handles, string Error |
| Runtime/PopupService.Restoration.cs 생성 | Begin/Commit/Rollback/Restore/Discard, 비활성 후보 일괄 적용 |
| Runtime/PopupService.cs 수정 | partial 선언, pending/복원 변경 가드·Detach와 ticket 무효화 연결 |
| Runtime/PopupView.cs 수정 | 값 포커스 키와 복원 준비 API |
| Runtime/PopupHost.cs 필요한 부분만 수정 | 현재 포커스 캡처/복귀 및 일괄 목록 적용. 기존 입력/알림 회귀 보존 |
| Editor/Tests/PopupFrameworkVerification.Restoration.cs 생성 | RunRestorationData/RunRestorationScene, 값 상태·시험 연결·실제 두 씬 검사 |
| 기존 시험 진입점 필요한 부분만 수정 | 새 partial의 공통 결과 기록을 재사용 |
| Docs/Verification/project-wide/popup-framework-stage-02.md 구현 시 생성 | 조건별 증거/시간/exit/캡처/한계 |
| Docs/Guides/project-wide/popup-framework-usage.md 구현 시 갱신 | 이동 성공/실패·새 Host/문맥·재시도/폐기·연결 소유권 예제 |

Runtime 경로는 `Assets/Scripts/Systems/Popup/Runtime/`, 검사는 `Assets/Scripts/Systems/Popup/Editor/Tests/`다. 기존 에셋과 .meta를 변경하지 않고 신규 Unity 파일은 고유 .meta를 만든다. 시험 씬은 메모리 SceneManager.CreateScene으로 생성하고 실제 SetActiveScene/UnloadSceneAsync로 왕복한다. 사용자 씬 파일/Build Settings는 수정하지 않는다.

## Review Focus

1. 캡처한 뒤 원본 배열/중첩 상태를 수정해도 보관 값 불변(Task 1).
2. pending 중 중복 Begin/다른 서비스 ticket/중복 Commit·Rollback·Host 소멸이 상태를 깨뜨리지 않음(Task 1).
3. 다른 씬에서 Restore를 시도해 원래 씬의 보관 상태를 잃거나, 같은 레벨 새 session에서 오래된 결과가 복원되지 않음(Task 2).
4. 두 번째 후보의 ApplyState/PrepareRestore 예외가 부분 입력/구독/명령 실행을 남기지 않음(Task 2).
5. SceneManager 왕복 중 외부 정지·늦은 콜백·키 입력이 새/다른 문맥을 조작하지 않음(Task 3).

## Task 1 — 값 캡처·이동 확정/취소

**Files:** SnapshotStore/ExitTicket/Service.Restoration/Service, Verification.Restoration.
**Consumes:** 1단계 Instances/Context/CaptureState/Copy/CloseAll/Detach. **Produces:** BeginSceneExit/CommitSceneExit/RollbackSceneExit/Discard와 단일 pending 계약.

- [x] 착수 기준 HEAD/Git 상태·기존 파일 해시/GUID와 1단계 완료 증거를 현재 코드에서 확인한다. 사용자 변경을 되돌리지 않는다.
- [x] RunRestorationData에 A(restorable)→B(non-restorable)→C(restorable) 캡처의 A/C 순서, 같은 종류 복수 항목 구분, 원본 중첩 배열 수정 후 값 불변을 작성한다.
- [x] Begin 뒤 기존 뷰/핸들/정지 불변, pending 목록 변경/중복 Begin 거부, Rollback 뒤 이전 목록/선택과 보관 상태 불변을 검사한다.
- [x] preserve=false Commit의 오래된 상태 폐기, 빈 캡처 교체, 다른 서비스/소비 ticket 거부, Host 소멸 ticket 무효를 검사한다. 캡처 예외 때 pending/후보/기존 상태가 남거나 변하지 않는지도 검사한다.
- [x] 미구현 동작 RED를 실제 실행하고 기록한다. 컴파일 실패와 동작 실패를 구분한다.
- [x] 표의 계약대로 값 보관과 ticket 구현을 추가하고 RunRestorationData를 다시 실행해 FAIL0·실제 exit0을 확인한다. 변경 범위와 신규 메타를 기록한다.

## Task 2 — 문맥 검증·후보 일괄 복원

**Files:** RestoreResult/Service.Restoration/View/Host, Verification.Restoration.
**Consumes:** Task 1 확정된 묶음과 기존 Host/입력/정지. **Produces:** Restore 결과/새 핸들/보관 소비와 PrepareRestore/포커스 키 계약.

- [x] 원래 씬 같은 문맥에서 텍스트/입력값/선택/탭/스크롤/포커스·A/C 순서 복원, 새 핸들과 옛 핸들 Close 불변, 성공 후 Restore=None 검사를 작성한다.
- [x] 다른 씬 Restore=None 뒤 원래 씬에서 성공, 같은 씬 다른 feature/session=ContextMismatch와 폐기, 이미 열린 Host=Failed와 기존 목록/보관 보존을 검사한다.
- [x] 두 번째 후보 ApplyState 예외와 PrepareRestore 예외 각각에서 목록0/후보0/입력0/소유 구독0/Handles 빈 상태·Error를 검사한다. 실패 후 명시 재시도 성공과 Discard 후 None도 검사한다.
- [x] 준비된 새 기능 연결만 호출되는지와 복원 중 구매/재시작/Next/보상/결과음 횟수0을 시험 카운터로 검사한다. 누락/비활성 포커스 키는 기존 유효 기본 선택으로 복귀해야 한다.
- [x] RED를 기록한 뒤 비활성 후보 전체 준비/연결→한 번 목록 적용→입력 활성화→성공 소비를 구현한다. 예외 시 후보/구독을 정리하고 재시도 가능한 값만 유지한다.
- [x] RunRestorationData를 다시 실행해 FAIL0/exit0을 확인한다. 후보 준비 중 목록 변경과 입력 누출이 없는지 실제 EventSystem 검사를 연결한다.

## Task 3 — 실제 씬 왕복·정리·완료 감사

**Files:** Verification.Restoration, 필요한 기존 검사 연결, 검증/사용 문서.
**Consumes:** Task 1~2 계약. **Produces:** 실제 SceneManager 왕복 증거와 2단계 조건별 완료 감사.

- [x] RunRestorationScene에 소유한 메모리 씬 A/B와 실제 Host/EventSystem/시험 연결을 만든다. 서비스만 호출 기능이 유지하고 이전 Host/뷰/구독은 씬 소멸에서 정리한다.
- [x] A에서 Begin/Commit→B 활성화·A Unload→B에서는 A 팝업 표시0→새 A/Host/현재 연결 준비→Restore 순서로 왕복한다. 새 참조/핸들·내용/순서/포커스·top 전용 실제 입력을 확인한다.
- [x] 이동 실패를 Commit 전 Rollback으로 연결해 기존 표시/입력/정지 소유권 보존을 검사한다. Host 소멸 뒤 지연 콜백은 취소돼 다른 문맥 조작0이어야 한다. 외부 정지/입력 차단 OR와 Time.timeScale 불변을 확인한다.
- [x] 실패 RED와 수정 후 GREEN을 기록한다. 네 화면 크기(1280×720/450×800/450×975/600×800)와 비영점 safe area에서 복원 후 포커스/버튼 raycast·부분 입력0을 확인하고 새 캡처를 직접 연다.
- [x] 마지막 변경 후 RunRestorationData/RunRestorationScene 및 1단계 Data/Input/Navigation/Focus/Dynamic/Requests를 순차 실행한다. 게임 영향이 생겼을 때만 관련 기존 검사를 추가한다.
- [x] 독립 최종 리뷰를 한 번 수행하고 재현된 관련 결함만 최소 수정한 뒤 해당 검사와 마지막 전체 검사로 재확인한다.
- [x] 조건별 검사명/UTC/실제 exit/화면/정리·기기 한계를 검증 문서에 기록하고 사용 안내에 호출 순서와 소유 구독 해제를 작성한다.
- [x] 새 파일/고유 GUID·원본 보존·HEAD/diff·소유 프로세스 종료·메모리 씬/임시 자원 정리를 감사한다. [2단계 목표](../../Goals/project-wide/popup-framework-stage-02-goal.md)의 10조건을 입증한 뒤 이 단계만 완료한다.

## 검사 실행과 자체 검토

아래 진입점은 구현됐으며 최종 실행 증거는 검증 기록을 따른다. 소유한 숨김 Unity 검사 프로세스에서 순차 실행하고 실제 종료 코드와 결과를 함께 확인한다. 사용자 Editor에서 호출하지 않는다. Play Mode 검사는 -nographics를 사용하지 않는다.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Projects/Git/ServeredMeridian' -executeMethod PopupUI.Editor.PopupFrameworkVerification.RunRestorationData -logFile 'C:/Projects/Git/ServeredMeridian/Logs/PopupFramework/stage02-data.log'
& 'C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Projects/Git/ServeredMeridian' -executeMethod PopupUI.Editor.PopupFrameworkVerification.RunRestorationScene -logFile 'C:/Projects/Git/ServeredMeridian/Logs/PopupFramework/stage02-scene.log'
```

결과 파일은 `stage02-data-results.txt`, `stage02-scene-results.txt`다. PASS 양수/FAIL0/실제 exit0이 통과다. 소유한 자원은 finally 정리 후 EditorApplication.Exit(exit)한다. Editor 충돌 시 사용자 작업을 임의 종료/저장하지 않는다.

설계의 선택적 보관·깊은 복사·이동 실패는 Task1, 문맥·새 연결·일괄 복원/재시도는 Task2, 실제 씬/입력/소멸/회귀는 Task3에 대응한다. Review Focus 다섯 항목에 구체 검사를 배정했다. pending/포커스/새 연결 계약을 확정했고 3단계/게임 저장은 제외했다. 이번 문서 작성은 구현 실행 승인이 아니다.


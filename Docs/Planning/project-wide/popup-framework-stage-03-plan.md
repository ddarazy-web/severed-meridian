# 팝업 프레임워크 3단계 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. 직접 순차 구현을 기본으로 하며, 마지막에 독립 리뷰를 한 번 수행한다.

**Goal:** 관리 창과 등록·템플릿 도구를 제공하고 기존 일시정지·결과·설명 팝업을 공통 관리 API로 전환한다.

**Architecture:** PopupUI는 게임 타입에 의존하지 않는다. 게임 화면의 PuzzlePopupBinding이 서비스·Host·현재 게임 연결과 입력 차단을 소유하고, 세 팝업은 값 상태와 표시만 담당한다. Editor 도구는 같은 카탈로그와 읽기 전용 실행 상태를 사용한다.

**Tech Stack:** Unity 6000.3.10f1, 기존 uGUI/EventSystem/Input System, UniTask, UnityEditor 전용 관리 창. 새 패키지나 어셈블리는 추가하지 않는다.

**Spec:** [공통 설계](../../Systems/project-wide/2026-10-02-popup-framework-design.md), [전체 계획](2026-10-02-popup-framework-plan.md), [3단계 목표](../../Goals/project-wide/popup-framework-stage-03-goal.md).

작성일: 2026-10-03. 상태: 3단계 구현·최종 리뷰 처리·검증/감사 완료. 아래는 완료한 구현과 마무리 절차의 기록이다.

## 다음 작업과 현재 위치

3단계의 남은 검수까지 완료했다. 1~2단계를 보존했으며 새 단계는 시작하지 않았다.

| 순서 | 다음 산출물 | 통과 기준 |
| --- | --- | --- |
| 1 | 기존 증거와 현재 소스 대조 | 통과 기록의 적용 범위와 후속 변경 확인, 과거 실패 기록 구분 |
| 2 | 독립 최종 리뷰와 관련 수정 | 추적/미추적 변경 전체 리뷰, 관련 결함 재현·최소 수정·재검증 |
| 3 | 화면·보존 감사와 완료 판정 | 마지막 변경에 맞는 검증, 네 화면/안전 영역, 완료 조건 12개별 증거 |

현재 HEAD `89431f4e1cadc33347b037a894be19c729de1ac9`에서 원본 1,825개 기준을 보관했다. 도구/선행 회귀 11회·203 PASS, 별도 컴파일 실패 처리 4 PASS, 게임 묶음 첫 8회·319 PASS, 수정된 기존 회귀 2,132 PASS, UI 조작 97 PASS, 생성 도구 16 PASS는 각각 FAIL0·실제 exit0 기록이 있다. 도구·게임 연결·재시작/복원을 재구현하지 않는다. 최종 리뷰와 전체 완료 감사는 완료했으며 최신22회/2781 PASS·FAIL0·exit0 증거는 검증 문서에 정리했다.

RestartAsync의 선제 정리 문제는 실제 실패 재현 후 후보 준비 성공 시에만 확정하도록 수정했다. 실패/취소 보존·결과 팝업·실제 씬 왕복 복원은 통과 기록이 있다. 마지막 검사 뒤 Unity가 생성한 다섯 빈 m_Name 줄의 공백만 정리했으며 최종 시각/보존 감사에서 이를 대조한다. 초기 게임 묶음 회귀 실패와 계약 수정 근거는 원장에 기록했다. 마지막 동일 묶음9회는 모두 통과했으며 stage03-gate-stage13-regression-summary.txt도 최신2,132 PASS·FAIL0·exit0다.

근거 경로: `Logs/PopupFramework/Stage03/baseline-files.json`, `baseline-head.txt`, `baseline-status.txt`, `Logs/PopupFramework/stage03-tools-results.txt`, `.superpowers/sdd/popup-framework-stage-03-plan/progress.md`. 이전 구현과 사용자 변경을 복원하거나 덮어쓰지 않는다.

## 선행 조건

1~2단계는 완료 기록이 있다. [2단계 최종 검증](../../Verification/project-wide/popup-framework-stage-02.md)은 리뷰 처리와 마지막 8회/157 PASS·원본 보존 감사까지 확인했다. 아래 항목은 2단계에서 처리한 선행 조건이다. 3단계 실행 시 현재 코드와 증거에서 유지 여부를 확인하고 이미 완료한 구현을 반복하지 않는다.

- 캡처 중 Host.Detach/비활성화: 목록 정리 중 적용 가드와 충돌하지 않고, 캡처를 중단하며 뷰·구독·정지/입력 요청을 남기지 않는 재현 검사와 수정.
- 이동 사용 예제: Begin/CaptureState/Copy 예외까지 try/finally로 입력 차단을 해제하고, 유효한 미소비 ticket만 Rollback하며 Commit 뒤에는 Rollback하지 않도록 수정.
- 이전/현재 연결에 서로 다른 수신 카운터를 두고 복원 후 실제 버튼 호출이 현재=1·이전=0인지 검증. 연결 문자열만으로 수신자 검증을 대신하지 않는다.
- 마지막 수정 후 2단계와 1단계 검사를 다시 실행하고 리뷰 처리 기록·10개 완료 조건·원본 보존 감사를 확정한다. 기존 리뷰를 보존하고 불필요하게 같은 리뷰를 새로 반복하지 않는다.

근거: `Logs/PopupFramework/Stage02/final-review.md`, `review-resolution.md`, `final-gate-results.txt`. 로그가 없는 환경에서는 2단계 검증 문서와 소스에서 처리 여부를 확인한다. 선행 조건의 회귀가 확인되면 관련 부분만 해결한다. 현재 3단계는 최종 리뷰 처리와 완료 감사를 마쳤다.

## 공통 제약과 확정 정책

- ServeredMeridian만 대상으로 한다. 빌드·Addressables 콘텐츠 빌드·커밋·푸시·사용자 Unity 종료·자동 씬 저장은 금지한다.
- 1~2단계와 Stage13의 다음 레벨/실패/취소/Asset 안내, 기존 UI 디자인·오디오·보드 규칙·레벨 데이터를 보존한다. 관련 연결과 설명 팝업 분리만 변경한다.
- 세 종류의 ID는 `puzzle.pause`, `puzzle.result`, `puzzle.description`이다. AllowMultiple=false, Restorable=true. PauseGameplay는 pause만 true, CloseOnCancel은 pause/description=true, result=false다.
- 표시 가능과 실제 보관 선택은 다르다. 일반 재시작/다음 레벨에서는 보관하지 않는다. 새 게임 성공 시 새 논리 세션 키를 발급하고 이전 문맥을 폐기한다. 실패/취소 시 기존 플레이 문맥을 유지한다.
- 현재 게임에 없는 왕복 씬 기능이나 게임 세션 영속화는 만들지 않는다. 동일 문맥 복원 연결은 소유한 시험 씬/게임 연결로 검증한다.
- 팝업 정지와 외부 정지는 독립 소유한다. 마지막 팝업 종료는 외부 정지를 해제하지 않는다. Time.timeScale로 정지를 대체하지 않는다.
- 결과/로딩/전환 등 게임 차단과 팝업 차단을 OR로 합성한다. 아래 팝업뿐 아니라 일반 HUD/아이템에도 입력이 새지 않아야 한다.
- 기존 직접 프리팹 참조를 유지한다. 새 Addressables 정책·DI·풀링·디스크 저장·물리 기기 검증을 대신하는 빌드는 제외한다.

## 파일 지도와 인터페이스

아래는 3단계의 구현 파일 지도다. 실제 시그니처는 현재 소스를 기준으로 확인하며 이 표의 최초 설계 표현만으로 API 존재를 단정하지 않는다. 신규 Unity 파일에는 고유 .meta를 만들고 기존 GUID는 보존한다.

| 경로 | 책임 / 변경 |
| --- | --- |
| `Assets/Scripts/Systems/Popup/Runtime/PopupService.Inspection.cs` | `PopupService.Inspect(): PopupInspection` 값 복사 조회. 아래→위 ID/핸들/정책/Top, pending 이동 여부, 보관 문맥·항목 수. View/State/콜백은 노출하지 않는다 |
| `Assets/Scripts/Systems/Popup/Runtime/PopupInspection.cs` | 읽기 전용 조회 DTO와 항목 타입. 저장 데이터를 변경할 수 없는 반환값 |
| `Assets/Scripts/Systems/Popup/Runtime/PopupSnapshotStore.cs` | 조회에 필요한 내부 문맥/개수 읽기만 추가 |
| `Assets/Scripts/Systems/Popup/Editor/PopupManagementWindow.cs` | `Tools/Popup/관리`: catalog 선택/정책 편집·검사, 연결된 Host 선택, 순서/Top/요청/보관 조회와 시험 열기/핸들 닫기 |
| `Assets/Scripts/Systems/Popup/Editor/PopupCatalogValidation.cs` | `Validate(PopupCatalog catalog): string[]`. 중복 ID·빈 ID·누락 프리팹·RectTransform/CanvasGroup 누락 오류. `Tools/Popup/등록 검사` |
| `Assets/Scripts/Systems/Popup/Editor/PopupTemplateGenerator.cs` | `Tools/Popup/템플릿 생성`. 지정 ID/유효 C# 이름으로 값 State·View 스크립트와 프리팹 생성, 충돌 시 생성 전 전체 거부 |
| `Assets/Scripts/Systems/Popup/Editor/Tests/PopupFrameworkVerification.Tools.cs` | partial `RunTools()` 진입점, 도구·생성/컴파일·조회 검증 |
| `Assets/Scripts/Features/GameScreen/Runtime/UI/PuzzlePopupBinding.cs` | `Configure(PuzzleGameSession session, PuzzleBoardInput boardInput, PopupCatalog value, PopupHost surface), ConfigureAssets(PopupCatalog value, PopupHost surface)`; 세 팝업 열기/중복 방지·문맥과 요청/현재 명령 연결·해제 |
| `Assets/Scripts/Features/GameScreen/Runtime/UI/PuzzlePopupStates.cs` | pause/result/description의 `PopupState.Copy()` 값 상태. 제목/본문/Next 표시·사용 가능 여부와 설명 내용. 게임 참조/명령은 금지 |
| `Assets/Scripts/Features/GameScreen/Runtime/UI/PuzzlePauseView.cs`, `PuzzleResultView.cs` | 기존 Configure/Bind 호출 호환을 유지하며 PopupView로 전환. ApplyState/CaptureState/PrepareRestore/ReleaseRestore 구현 |
| `Assets/Scripts/Features/GameScreen/Runtime/UI/PuzzleDescriptionView.cs` | 설명 전용 PopupView. 기존 명시적 눌러서 닫기 표현/행동 유지 |
| `Assets/Scripts/Features/GameScreen/Runtime/UI/PuzzleScreenView.cs` | 직접 SetActive 팝업 관리 대신 Binding 호출. Refresh는 내용만 갱신하고 기존 순서를 매번 올리지 않는다 |
| `Assets/Scripts/Features/GameScreen/Runtime/Session/PuzzleGameSession.Controls.cs` | `SetPopupPaused(bool paused): bool` 추가. 기존 SetPaused는 외부 요청, IsPaused는 두 요청 OR. 기존 연출/오디오 적용 흐름 유지 |
| `Assets/Scripts/Features/GameScreen/Runtime/Session/PuzzleGameSession.LevelTransition.cs` | 성공한 새 플레이의 문맥 변경 시점 연결. 실패/취소 경로 보존 |
| `Assets/Scripts/Features/GameScreen/Editor/PuzzlePopupAssets.cs` | `Apply()`로 지정 UI 프리팹/catalog만 연결. 사용자 씬 저장 금지 |
| `Assets/Scripts/Features/GameScreen/Editor/PuzzleUIAssets.cs`, `PuzzleLevelTransitionAssets.cs` | 기존 생성 도구 재실행 시 공통 연결/Next 버튼을 보존 |
| `Assets/Scripts/Features/GameScreen/Editor/Tests/PuzzlePopupVerification.cs` | `RunScene()` 실제 게임/UI/입력/수명주기 검증 |
| `Assets/Prefabs/UI/Puzzle/PuzzleScreen.prefab`, `PuzzlePausePopup.prefab`, `PuzzleResultPopup.prefab` | 기존 디자인/GUID 보존, 공통 Host/Binding·뷰 연결 적용 |
| `Assets/Prefabs/UI/Puzzle/PuzzleDescriptionPopup.prefab` | 기존 인라인 설명을 분리한 신규 프리팹 |
| `Assets/Data/UI/PuzzlePopupCatalog.asset` | 세 게임 종류와 위 정책 등록 |

관리 창은 PopupHost에서 읽기 전용 Service 접근을 받아 선택한 실행 인스턴스를 조회한다. 새 전역 서비스 레지스트리는 만들지 않는다. 게임 Binding은 Host가 속한 현재 화면에서만 찾아 연결하며 이전 Binding 참조를 State에 보관하지 않는다.

조회 타입의 계약은 `PopupInspection.Items: IReadOnlyList<PopupInspectionItem>`, `Top: PopupHandle?`, `Count: int`, `HasPendingExit: bool`, `Stored: IReadOnlyList<PopupStoredInfo>`다. 항목은 `Id: string`, `Handle: PopupHandle`, `IsTop/AllowMultiple/PauseGameplay/CloseOnCancel/Restorable: bool`, 보관 정보는 `Context: PopupContext`, `Count: int`만 가진다. 모든 속성은 get-only이며 컬렉션도 읽기 전용 복사본이다. Host는 `Service: PopupService` get-only 접근을 추가하고 미연결이면 null을 반환한다.

## 리뷰 집중 항목

1. 등록 파일에 오류가 있을 때 실행 인스턴스를 만들거나 일부 생성 파일을 남기지 않는가(Task 1).
2. 창 재개/Host 교체·복원 대기 중 조회와 시험 동작이 이전 서비스나 원본 에셋을 변경하지 않는가(Task 1).
3. 결과 Refresh와 중첩/중간 제거에서 최상위가 바뀌거나 보드·HUD 입력이 누출되지 않는가(Task 2).
4. 외부 정지·백그라운드와 팝업 정지가 교차할 때 재개/결과음이 중복되지 않는가(Task 2~3).
5. 재시작/Next 실패·취소·화면 종료·복원 실패에서 오래된 문맥/버튼 구독/명령이 남지 않는가(Task 3).

## Task 1 — 등록·관리·템플릿 도구

**Consumes:** 기존 PopupCatalog.Entry/Configure, PopupService.Count/Top/Changed/Open/Close, 2단계 저장 문맥. **Produces:** Inspect/Validate 계약, 세 메뉴와 추가 패턴. 파일 지도 중 PopupUI Runtime 조회 및 Editor 도구/검사만 변경한다.

- [x] 작업 전 HEAD/status·수정 대상 원본 해시/GUID와 선행 조건 완료를 기록한다.
- [x] RunTools에 등록 오류별 진단, 순서 A/B/C→B 제거 후 A/C, Top/Count/정지/보관 조회 일치, 조회값 수정의 원본 영향0, Host 교체/Detach, pending 동안 시험 변경 거부 검사를 먼저 만든다. 현재 도구 부재로 실패하는 근거를 수집한다.
- [x] Inspect는 복사한 값만 반환한다. 관리 창의 catalog 편집은 SerializedProperty 바인딩의 Undo+명시적 저장, Play Mode 시험은 서비스만 변경한다. 초기 시험 상태는 null이며 특정 게임 State가 필수인 종류는 지원하지 않는 시험 열기임을 표시한다. 게임 동작 검증은 Task 2에서 한다.
- [x] Validate와 세 메뉴를 구현한다. 오류가 있으면 열기/등록을 중단하고 항목 ID/오류를 표시한다. 관리 창 종료 시 Changed 구독을 해제한다.
- [x] 생성은 `Assets/Scripts/Features/<기능>/Runtime/UI/`와 `Assets/Prefabs/UI/<기능>/`를 사용한다. 입력 경로/이름과 모든 출력 충돌을 먼저 검사하고 기존 파일 덮어쓰기 없이 생성한다. 생성 스크립트 컴파일 완료 후 뷰를 프리팹에 연결한다.
- [x] 소유한 임시 경로에서 템플릿 생성→스크립트 재컴파일→프리팹 열기→등록→열기→값 캡처/Copy/닫기를 실제 검증한다. 이름/경로 거부, 재실행 충돌, 컴파일 실패 시 미완료 표시/기존 파일 보존도 검사한다. 결과 FAIL0/실제 exit0와 임시 파일/GUID 정리를 기록한다.

## Task 2 — 실제 게임 세 팝업 전환

**Consumes:** 1~2단계 서비스/Host/View API와 Task 1 등록 검사. **Produces:** 현재 게임 Binding·값 State·프리팹/catalog 연결과 독립 정지 소유권. 파일 지도 중 게임 Runtime/UI/Session, 프리팹 연결/생성 도구 및 PuzzlePopupVerification을 변경한다.

- [x] 기존 재개/Retry/Next/설명/결과음/차단과 프리팹 디자인을 기록한다. RunScene에 설명→일시정지 중첩, 설명 핸들 제거 후 pause 유지, 하위 Submit/보드 스와이프/HUD·아이템 호출0, 취소 한 번에 닫기1, result 취소 닫기0 검사를 작성하고 미전환 실패를 확인한다.
- [x] SetPopupPaused를 구현하고 외부 SetPaused와 분리한다. 외부=true→popup=true→popup=false에서 IsPaused=true, 외부 해제 뒤 false를 검사한다. 중간 정지 팝업 제거/최상위 비정지 팝업도 함께 검사한다. 게임 종료 상태의 정지 제한은 기존 흐름을 따른다.
- [x] Restart 초기화의 기존 IsPaused=false 대입을 독립 요청 상태에 맞춰 갱신한다. 외부 정지는 보존하고 이전 팝업 요청만 해제하며, 재시작 실패/취소 복귀 시 이전 화면의 유효 요청을 다시 합성한다. 기존 SetPaused 호출부가 외부 요청만 소유하는지도 확인한다.
- [x] State/세 View/Binding을 구현한다. 새 Open과 복원은 현재 Binding에 버튼을 연결하되 명령은 사용자 입력 때만 실행한다. ReleaseRestore/OnDisable은 반복 호출에 안전하게 자신이 소유한 연결만 해제한다. 첫 표시 결과음은 기존 세션 책임을 유지한다.
- [x] ScreenView의 직접 활성화/단일 bool 차단을 교체한다. 최초 결과만 Open하고 같은 Outcome의 Refresh는 ApplyState로 내용/잠금만 갱신한다. pause 아래 result 내용 변경이 result를 최상위로 올리지 않는 검사를 추가한다. 레벨 변경 후 shownOutcome/핸들 추적은 새 문맥으로 초기화한다.
- [x] PuzzlePopupAssets.Apply로 지정 프리팹/catalog을 연결하고 설명만 별도 프리팹으로 분리한다. 원본 두 생성 도구의 재실행을 소유한 임시 복사본에서 확인해 Next/Host/Binding/정책을 보존한다. 프리팹 시각 디자인은 변경하지 않는다.
- [x] 실제 EventSystem/가상 키·포인터와 보드 입력 검사로 위 행동을 재검증한다. 원본 프리팹 메타 GUID·디자인 비교, 새 참조 유효성, 종료 후 뷰/구독0을 기록한다.

## Task 3 — 문맥·전환·복원과 최종 검증

**Consumes:** Task 2 Binding/현재 명령 연결, 기존 RestartAsync/AdvanceLevelAsync 및 Stage13 취소/실패 정책. **Produces:** 최종 조건별 증거와 사용 안내. 새로운 게임 진행 저장/왕복 기능을 추가하지 않는다.

- [x] RunScene에 재시작/Next 성공은 새 세션 키와 이전 보관 폐기, 실패/취소는 이전 키·플레이·결과/잠금 유지, 연속 입력 명령1, 늦은 이전 콜백0 검사를 추가하고 연결 전 실패를 기록한다.
- [x] 실제 게임 문맥을 레벨+논리 플레이 ID로 연결한다. 게임 준비 성공 시에만 새 ID를 확정한다. 소유한 두 시험 씬에서 동일 문맥의 새 Binding에 명시 복원하고 현재 명령 수신1·이전 수신0, 복원 자체의 Retry/Next/보상/결과음 호출0을 검사한다. 구매 기능은 새로 만들지 않는다.
- [x] 후보 Apply/Prepare 실패·캡처 중 Host 종료·View/Binding 종료·이동 실패에서도 입력 차단/정지 소유권과 구독 정리를 검사한다. 사용 예제는 Begin부터 finally까지 보호하고 실패를 사용자 씬 변경 없이 보고한다.
- [x] RunTools/RunScene과 1~2단계 회귀를 마지막 변경으로 실행한다. 기존 PuzzleLevelTransitionVerification의 데이터/씬/실패/취소/Asset 검증과 영향받는 UI/오디오/입력 검사를 선별해 실제 진입점·실행 시각·PASS/FAIL·프로세스 exit를 기록한다. PlayerBuildVerification은 실행하지 않는다.
- [x] 1280×720, 450×800, 450×975, 600×800의 새 캡처를 열어 비교한다. 비영점 안전 영역에서 버튼/팝업 경계·최상위 raycast·포커스를 검사하고, 외부 정지/백그라운드 교차 시 결과음 복귀1·중복0을 확인한다. 물리 Android 미검증은 명시한다.
- [x] 독립 최종 리뷰 한 번을 수행한다. 관련 결함은 재현→최소 수정→영향 검사로 처리하고 리뷰/판단 근거를 보존한다. 완료되지 않은 조건을 체크하지 않는다.
- [x] `Docs/Verification/project-wide/popup-framework-stage-03.md`, 사용 안내와 목표를 조건별 증거로 갱신한다. 원본 해시/GUID/diff/HEAD와 소유 프로세스·시험 씬/파일 정리를 감사한 뒤 3단계만 완료한다.

## 빌드 없는 검증 실행

아래 진입점은 현재 소스에서 확인했다. 기존 숨김 Unity 소유 프로세스 실행 방식을 재사용하고, 사용자 Editor가 프로젝트를 사용 중이면 임의 종료하지 않는다. 결과 파일을 실행 전에 정리해 오래된 PASS를 사용하지 않는다. Play Mode 검사는 -nographics를 사용하지 않는다.

| executeMethod | 결과 파일 |
| --- | --- |
| `PopupUI.Editor.PopupFrameworkVerification.RunTools` | `Logs/PopupFramework/stage03-tools-results.txt` |
| `GameScreen.Editor.PuzzlePopupVerification.RunScene` | `Logs/PopupFramework/stage03-game-results.txt` |

위 메서드를 Unity `-batchmode -projectPath ... -executeMethod ... -logFile ...`로 실행한다. 소유한 숨김 프로세스의 실제 종료를 기다려 exit0와 FAIL0을 함께 확인한다. 컴파일/생성 후 domain reload 검사는 SessionState에 재개 단계만 기록하며 사용자 설정을 저장하지 않는다. 빌드 메서드는 호출하지 않는다.

## 자체 검토와 인계

등록/조회/생성은 Task 1, 세 팝업/정지/입력/프리팹은 Task 2, 문맥/실패/복원/회귀는 Task 3으로 배정했다. 리뷰 집중 항목 다섯 개를 각 작업의 검사에 연결했다. 목표의 12개 완료 조건은 같은 작업을 참조한다. 추가 시스템/게임 규칙/배포는 범위 밖이다.

직접 순차 구현(Native)을 유지한다. 공통 조회 계약과 게임 연결이 이어지므로 구현자 한 명이 계약과 프리팹을 일관되게 관리하고 마지막 독립 리뷰로 확인한다. [복사용 명령문](../../Commands/project-wide/popup-framework-stage-03-command.md)은 활성 3단계 목표에서 완료된 작업을 보존하고 남은 최종 리뷰·관련 검증·보존 감사를 이어가는 명령이다. Task 1의 아래 체크리스트는 완료된 작업 기록이며 재구현 지시가 아니다. 최종 구현/검증과 조건별 감사는 연결한 검증 문서에 기록했다.

## 다음 실행 계획 — 3단계 최종 검수·완료 판정

**Goal:** 구현을 보존하며 남은 최종 리뷰·감사를 마치고 12개 완료 조건으로 3단계만 판정한다.
**Architecture:** 기존 서비스/Binding/프리팹 구조를 유지한다. 검수에서 재현된 관련 결함에만 최소 수정을 적용한다.
**Spec:** 승인한 설계와 이 단계 목표 문서의 12개 조건. 실행 방식은 기존 Native를 유지한다.

### 마무리 작업 A — 증거 정합성과 독립 리뷰

**Files:** 기존 Popup Runtime/Editor, GameScreen 연결/검사, 지정 UI 프리팹과 미추적 추가 파일을 읽는다. 기록은 `Logs/PopupFramework/Stage03/final-review.md`, `review-resolution.md`에 남긴다.
**Consumes:** 기존 구현, 기준 HEAD/해시, 검증 요약과 진행 원장.
**Produces:** 발견 사항의 영향·심각도·처리 근거. 위 리뷰 집중 항목 다섯 개를 그대로 포함한다.

- [x] 현재 HEAD/status와 원장, 요약/원본 결과의 PASS·FAIL·실제 exit 및 실행 시각을 대조한다. 319 PASS 묶음의 아홉 번째 과거 실패를 전체 통과로 합산하지 않는다.
- [x] 기존 최종 리뷰 유무를 먼저 확인한다. 없으면 최종 독립 리뷰 한 번만 수행한다. 비교 범위는 `git diff`와 미추적 파일 전체이며 동일 HEAD 간 비교로 변경을 누락하지 않는다.
- [x] 관련 결함은 재현 검사 실패 → 최소 수정 → 동일 검사 통과 순서로 처리한다. 검토자가 판단을 유보한 항목도 적용 범위를 기록한다. 미해결 완료 조건은 완료 처리하지 않는다.

### 마무리 작업 B — 마지막 변경 검증과 화면 감사

**Files:** 기존 검사 진입점을 재사용한다. 결과는 `Logs/PopupFramework/Stage03/final-gate-results.txt`에 출처와 함께 집계한다. 새 테스트 시스템을 만들지 않는다.
**Consumes:** 작업 A의 변경 목록과 기존 성공 증거.
**Produces:** 마지막 소스에 적용 가능한 성공 증거, 네 화면과 안전 영역 관찰 기록.

- [x] 변경이 있으면 영향받는 검사를 실행한다. 기존 증거의 적용 범위가 충분하면 불필요하게 모든 검사를 반복하지 않는다. 마지막 프리팹 공백 정리는 RunDesign과 원본 diff로 확인한다.
- [x] 필요 시 `run-task1.ps1`(11회)와 `run-game-gate.ps1`(9회)을 재사용한다. 로그가 없으면 아래 실제 진입점으로 복원하여 실행한다. 모든 결과는 FAIL0와 실제 프로세스 exit0를 함께 확인한다.
- [x] `PuzzlePopupVerification.RunGenerators`, `PuzzleStabilityVerification.UIInteraction`의 생성/기존 UI 증거를 현재 수정 범위와 대조한다. 관련 수정이 있으면 재실행한다.
- [x] 1280×720·450×800·450×975·600×800의 결과/로딩/오류 및 비영점 안전 영역 캡처를 실제 열어 확인한다. 최상위 입력·포커스·raycast 검사 결과를 함께 연결한다. 물리 Android는 실행하지 않았다면 미검증으로 기록한다.

기존 실행기는 `Logs/PopupFramework/run-check.ps1`이며 QualifiedMethod/Label/ResultPath를 받는다. 로그 폴더는 버전 관리 제외이므로 다른 환경에서는 이를 필수 의존성으로 삼지 않고 기존 Unity batchmode 진입점으로 실행한다. Unity는 현재 프로젝트 버전을 확인하고 소유한 숨김 프로세스만 관리한다. 사용자 Editor와 충돌하면 임의 종료하지 않는다.

| 실제 진입점 | 담당 검증 |
| --- | --- |
| PopupUI.Editor.PopupFrameworkVerification.RunTools / RunWindowControls / RunTemplateGeneration | 등록·관리 창·템플릿 |
| GameScreen.Editor.PuzzlePopupVerification.RunScene / RunRestart / RunResult / RunRestore | 게임 입력·재시작·결과·명시 복원 |
| GameScreen.Editor.PuzzlePopupVerification.RunDesign / RunGenerators | 시각 보존·생성 도구 |
| GameScreen.Editor.PuzzleLevelTransitionVerification.RunScene / RunAssetScene / RunBoundaries / RunProbe / RunRegression | Next·Asset·경계·안전 영역·기존 회귀 |
| GameScreen.Editor.PuzzleStabilityVerification.UIInteraction | 기존 UI 조작 회귀 |

### 마무리 작업 C — 보존 감사와 목표 종료

**Files:** 이 계획, 단계 목표/명령문, 검증 문서, 사용 안내, 전체 계획 및 문서 색인. 원본 기준 파일을 읽으며 에셋을 일괄 재저장하지 않는다.
**Consumes:** 작업 A/B 결과와 원본 1,825개 해시/GUID 기준.
**Produces:** 12조건 증거 표와 완료/미완료 판정.

- [x] HEAD·diff·기존 meta GUID, 씬/레벨/텍스처/Packages/ProjectSettings/Addressables 보존을 대조한다. 허용된 변경은 요청과 직접 연결되는 근거를 남긴다.
- [x] 소유한 임시 파일·씬·구독·프로세스 잔여를 확인한다. 사용자 파일/프로세스를 정리하지 않는다.
- [x] 검증 문서에 12개 조건별 검사명·출처·마지막 변경 적용 여부를 기록한다. 관련 리뷰 처리와 사용 안내/색인을 갱신한다.
- [x] 12개 모두 증명됐을 때만 기존 활성 목표를 complete로 변경한다. 증거가 부족하면 미완료 항목을 명시하고 목표를 중복 생성하지 않는다. 새 4단계/게임 화면 단계를 시작하지 않는다.

자체 검토: 등록/추가 패턴은 기존 Task 1, 게임 소유권은 Task 2, 문맥/복원은 Task 3 구현을 보존한다. 다음 실행의 A/B/C가 각각 리뷰·검증·최종 감사에 대응한다. 새 기능이나 신규 정책 결정은 포함하지 않는다. 기존 구현 체크리스트의 미체크 표시만으로 재구현하지 말고 증거에 따라 최종 감사 시 갱신한다.

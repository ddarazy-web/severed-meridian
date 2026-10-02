# 팝업 프레임워크 3단계 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. 직접 순차 구현을 기본으로 하며, 마지막에 독립 리뷰를 한 번 수행한다.

**Goal:** 관리 창과 등록·템플릿 도구를 제공하고 기존 일시정지·결과·설명 팝업을 공통 관리 API로 전환한다.

**Architecture:** PopupUI는 게임 타입에 의존하지 않는다. 게임 화면의 PuzzlePopupBinding이 서비스·Host·현재 게임 연결과 입력 차단을 소유하고, 세 팝업은 값 상태와 표시만 담당한다. Editor 도구는 같은 카탈로그와 읽기 전용 실행 상태를 사용한다.

**Tech Stack:** Unity 6000.3.10f1, 기존 uGUI/EventSystem/Input System, UniTask, UnityEditor 전용 관리 창. 새 패키지나 어셈블리는 추가하지 않는다.

**Spec:** [공통 설계](../../Systems/project-wide/2026-10-02-popup-framework-design.md), [전체 계획](2026-10-02-popup-framework-plan.md), [3단계 목표](../../Goals/project-wide/popup-framework-stage-03-goal.md).

작성일: 2026-10-03. 상태: 계획 작성 완료 · 구현 미착수. 문서 작성은 목표 시작이나 구현 승인이 아니다.

## 선행 조건

1~2단계는 완료 기록이 있다. [2단계 최종 검증](../../Verification/project-wide/popup-framework-stage-02.md)은 리뷰 처리와 마지막 8회/157 PASS·원본 보존 감사까지 확인했다. 아래 항목은 2단계에서 처리한 선행 조건이다. 3단계 실행 시 현재 코드와 증거에서 유지 여부를 확인하고 이미 완료한 구현을 반복하지 않는다.

- 캡처 중 Host.Detach/비활성화: 목록 정리 중 적용 가드와 충돌하지 않고, 캡처를 중단하며 뷰·구독·정지/입력 요청을 남기지 않는 재현 검사와 수정.
- 이동 사용 예제: Begin/CaptureState/Copy 예외까지 try/finally로 입력 차단을 해제하고, 유효한 미소비 ticket만 Rollback하며 Commit 뒤에는 Rollback하지 않도록 수정.
- 이전/현재 연결에 서로 다른 수신 카운터를 두고 복원 후 실제 버튼 호출이 현재=1·이전=0인지 검증. 연결 문자열만으로 수신자 검증을 대신하지 않는다.
- 마지막 수정 후 2단계와 1단계 검사를 다시 실행하고 리뷰 처리 기록·10개 완료 조건·원본 보존 감사를 확정한다. 기존 리뷰를 보존하고 불필요하게 같은 리뷰를 새로 반복하지 않는다.

근거: `Logs/PopupFramework/Stage02/final-review.md`, `review-resolution.md`, `final-gate-results.txt`. 로그가 없는 환경에서는 2단계 검증 문서와 소스에서 처리 여부를 확인한다. 선행 조건의 회귀/미완료가 확인되면 실제 게임 프리팹 전환 전에 해결한다. 현재 3단계는 문서 준비만 완료한 상태다.

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

아래 신규 타입/메서드는 구현 예정 계약이다. 기존 API로 표시하지 않는다. 신규 Unity 파일에는 고유 .meta를 만들고 기존 GUID는 보존한다.

| 경로 | 책임 / 변경 |
| --- | --- |
| `Assets/Scripts/Systems/Popup/Runtime/PopupService.Inspection.cs` | `PopupService.Inspect(): PopupInspection` 값 복사 조회. 아래→위 ID/핸들/정책/Top, pending 이동 여부, 보관 문맥·항목 수. View/State/콜백은 노출하지 않는다 |
| `Assets/Scripts/Systems/Popup/Runtime/PopupInspection.cs` | 읽기 전용 조회 DTO와 항목 타입. 저장 데이터를 변경할 수 없는 반환값 |
| `Assets/Scripts/Systems/Popup/Runtime/PopupSnapshotStore.cs` | 조회에 필요한 내부 문맥/개수 읽기만 추가 |
| `Assets/Scripts/Systems/Popup/Editor/PopupManagementWindow.cs` | `Tools/Popup/관리`: catalog 선택/정책 편집·검사, 연결된 Host 선택, 순서/Top/요청/보관 조회와 시험 열기/핸들 닫기 |
| `Assets/Scripts/Systems/Popup/Editor/PopupCatalogValidation.cs` | `Validate(PopupCatalog catalog): string[]`. 중복 ID·빈 ID·누락 프리팹·RectTransform/CanvasGroup 누락 오류. `Tools/Popup/등록 검사` |
| `Assets/Scripts/Systems/Popup/Editor/PopupTemplateGenerator.cs` | `Tools/Popup/템플릿 생성`. 지정 ID/유효 C# 이름으로 값 State·View 스크립트와 프리팹 생성, 충돌 시 생성 전 전체 거부 |
| `Assets/Scripts/Systems/Popup/Editor/Tests/PopupFrameworkVerification.Tools.cs` | partial `RunTools()` 진입점, 도구·생성/컴파일·조회 검증 |
| `Assets/Scripts/Features/GameScreen/Runtime/UI/PuzzlePopupBinding.cs` | `Configure(PuzzleGameSession session, PuzzleBoardInput input, PopupCatalog catalog, PopupHost host)`; 세 팝업 열기/중복 방지·문맥과 요청/현재 명령 연결·해제 |
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

- [ ] 작업 전 HEAD/status·수정 대상 원본 해시/GUID와 선행 조건 완료를 기록한다.
- [ ] RunTools에 등록 오류별 진단, 순서 A/B/C→B 제거 후 A/C, Top/Count/정지/보관 조회 일치, 조회값 수정의 원본 영향0, Host 교체/Detach, pending 동안 시험 변경 거부 검사를 먼저 만든다. 현재 도구 부재로 실패하는 근거를 수집한다.
- [ ] Inspect는 복사한 값만 반환한다. 관리 창의 catalog 편집은 Undo.RecordObject+명시적 저장, Play Mode 시험은 서비스만 변경한다. 초기 시험 상태는 null이며 특정 게임 State가 필수인 종류는 지원하지 않는 시험 열기임을 표시한다. 게임 동작 검증은 Task 2에서 한다.
- [ ] Validate와 세 메뉴를 구현한다. 오류가 있으면 열기/등록을 중단하고 항목 ID/오류를 표시한다. 관리 창 종료 시 Changed 구독을 해제한다.
- [ ] 생성은 `Assets/Scripts/Features/<기능>/Runtime/UI/`와 `Assets/Prefabs/UI/<기능>/`를 사용한다. 입력 경로/이름과 모든 출력 충돌을 먼저 검사하고 기존 파일 덮어쓰기 없이 생성한다. 생성 스크립트 컴파일 완료 후 뷰를 프리팹에 연결한다.
- [ ] 소유한 임시 경로에서 템플릿 생성→스크립트 재컴파일→프리팹 열기→등록→열기→값 캡처/Copy/닫기를 실제 검증한다. 이름/경로 거부, 재실행 충돌, 컴파일 실패 시 미완료 표시/기존 파일 보존도 검사한다. 결과 FAIL0/실제 exit0와 임시 파일/GUID 정리를 기록한다.

## Task 2 — 실제 게임 세 팝업 전환

**Consumes:** 1~2단계 서비스/Host/View API와 Task 1 등록 검사. **Produces:** 현재 게임 Binding·값 State·프리팹/catalog 연결과 독립 정지 소유권. 파일 지도 중 게임 Runtime/UI/Session, 프리팹 연결/생성 도구 및 PuzzlePopupVerification을 변경한다.

- [ ] 기존 재개/Retry/Next/설명/결과음/차단과 프리팹 디자인을 기록한다. RunScene에 설명→일시정지 중첩, 설명 핸들 제거 후 pause 유지, 하위 Submit/보드 스와이프/HUD·아이템 호출0, 취소 한 번에 닫기1, result 취소 닫기0 검사를 작성하고 미전환 실패를 확인한다.
- [ ] SetPopupPaused를 구현하고 외부 SetPaused와 분리한다. 외부=true→popup=true→popup=false에서 IsPaused=true, 외부 해제 뒤 false를 검사한다. 중간 정지 팝업 제거/최상위 비정지 팝업도 함께 검사한다. 게임 종료 상태의 정지 제한은 기존 흐름을 따른다.
- [ ] Restart 초기화의 기존 IsPaused=false 대입을 독립 요청 상태에 맞춰 갱신한다. 외부 정지는 보존하고 이전 팝업 요청만 해제하며, 재시작 실패/취소 복귀 시 이전 화면의 유효 요청을 다시 합성한다. 기존 SetPaused 호출부가 외부 요청만 소유하는지도 확인한다.
- [ ] State/세 View/Binding을 구현한다. 새 Open과 복원은 현재 Binding에 버튼을 연결하되 명령은 사용자 입력 때만 실행한다. ReleaseRestore/OnDisable은 반복 호출에 안전하게 자신이 소유한 연결만 해제한다. 첫 표시 결과음은 기존 세션 책임을 유지한다.
- [ ] ScreenView의 직접 활성화/단일 bool 차단을 교체한다. 최초 결과만 Open하고 같은 Outcome의 Refresh는 ApplyState로 내용/잠금만 갱신한다. pause 아래 result 내용 변경이 result를 최상위로 올리지 않는 검사를 추가한다. 레벨 변경 후 shownOutcome/핸들 추적은 새 문맥으로 초기화한다.
- [ ] PuzzlePopupAssets.Apply로 지정 프리팹/catalog을 연결하고 설명만 별도 프리팹으로 분리한다. 원본 두 생성 도구의 재실행을 소유한 임시 복사본에서 확인해 Next/Host/Binding/정책을 보존한다. 프리팹 시각 디자인은 변경하지 않는다.
- [ ] 실제 EventSystem/가상 키·포인터와 보드 입력 검사로 위 행동을 재검증한다. 원본 프리팹 메타 GUID·디자인 비교, 새 참조 유효성, 종료 후 뷰/구독0을 기록한다.

## Task 3 — 문맥·전환·복원과 최종 검증

**Consumes:** Task 2 Binding/현재 명령 연결, 기존 RestartAsync/AdvanceLevelAsync 및 Stage13 취소/실패 정책. **Produces:** 최종 조건별 증거와 사용 안내. 새로운 게임 진행 저장/왕복 기능을 추가하지 않는다.

- [ ] RunScene에 재시작/Next 성공은 새 세션 키와 이전 보관 폐기, 실패/취소는 이전 키·플레이·결과/잠금 유지, 연속 입력 명령1, 늦은 이전 콜백0 검사를 추가하고 연결 전 실패를 기록한다.
- [ ] 실제 게임 문맥을 레벨+논리 플레이 ID로 연결한다. 게임 준비 성공 시에만 새 ID를 확정한다. 소유한 두 시험 씬에서 동일 문맥의 새 Binding에 명시 복원하고 현재 명령 수신1·이전 수신0, 복원 자체의 Retry/Next/보상/결과음 호출0을 검사한다. 구매 기능은 새로 만들지 않는다.
- [ ] 후보 Apply/Prepare 실패·캡처 중 Host 종료·View/Binding 종료·이동 실패에서도 입력 차단/정지 소유권과 구독 정리를 검사한다. 사용 예제는 Begin부터 finally까지 보호하고 실패를 사용자 씬 변경 없이 보고한다.
- [ ] RunTools/RunScene과 1~2단계 회귀를 마지막 변경으로 실행한다. 기존 PuzzleLevelTransitionVerification의 데이터/씬/실패/취소/Asset 검증과 영향받는 UI/오디오/입력 검사를 선별해 실제 진입점·실행 시각·PASS/FAIL·프로세스 exit를 기록한다. PlayerBuildVerification은 실행하지 않는다.
- [ ] 1280×720, 450×800, 450×975, 600×800의 새 캡처를 열어 비교한다. 비영점 안전 영역에서 버튼/팝업 경계·최상위 raycast·포커스를 검사하고, 외부 정지/백그라운드 교차 시 결과음 복귀1·중복0을 확인한다. 물리 Android 미검증은 명시한다.
- [ ] 독립 최종 리뷰 한 번을 수행한다. 관련 결함은 재현→최소 수정→영향 검사로 처리하고 리뷰/판단 근거를 보존한다. 완료되지 않은 조건을 체크하지 않는다.
- [ ] `Docs/Verification/project-wide/popup-framework-stage-03.md`, 사용 안내와 목표를 조건별 증거로 갱신한다. 원본 해시/GUID/diff/HEAD와 소유 프로세스·시험 씬/파일 정리를 감사한 뒤 3단계만 완료한다.

## 빌드 없는 검증 실행

신규 진입점은 구현 후에만 존재한다. 기존 숨김 Unity 소유 프로세스 실행 방식을 재사용하고, 사용자 Editor가 프로젝트를 사용 중이면 임의 종료하지 않는다. 결과 파일을 실행 전에 정리해 오래된 PASS를 사용하지 않는다. Play Mode 검사는 -nographics를 사용하지 않는다.

| executeMethod | 결과 파일 |
| --- | --- |
| `PopupUI.Editor.PopupFrameworkVerification.RunTools` | `Logs/PopupFramework/stage03-tools-results.txt` |
| `GameScreen.Editor.PuzzlePopupVerification.RunScene` | `Logs/PopupFramework/stage03-game-results.txt` |

위 메서드를 Unity `-batchmode -projectPath ... -executeMethod ... -logFile ...`로 실행한다. 소유한 숨김 프로세스의 실제 종료를 기다려 exit0와 FAIL0을 함께 확인한다. 컴파일/생성 후 domain reload 검사는 SessionState에 재개 단계만 기록하며 사용자 설정을 저장하지 않는다. 빌드 메서드는 호출하지 않는다.

## 자체 검토와 인계

등록/조회/생성은 Task 1, 세 팝업/정지/입력/프리팹은 Task 2, 문맥/실패/복원/회귀는 Task 3으로 배정했다. 리뷰 집중 항목 다섯 개를 각 작업의 검사에 연결했다. 목표의 12개 완료 조건은 같은 작업을 참조한다. 추가 시스템/게임 규칙/배포는 범위 밖이다.

직접 순차 구현(Native)을 권장한다. 공통 조회 계약과 게임 연결이 이어지므로 구현자 한 명이 계약과 프리팹을 일관되게 관리하고 마지막 독립 리뷰로 확인한다. 사용자는 [복사용 명령문](../../Commands/project-wide/popup-framework-stage-03-command.md)으로 이 실행 방식을 승인할 수 있다. 계획만 요청한 현재 시점에는 구현하지 않는다.

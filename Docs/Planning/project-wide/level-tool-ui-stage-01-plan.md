# 레벨툴 UI 개선 1단계 — 화면 틀·메뉴·공통 단축키

> 2026-10-09 · 구현·Unity 검증 완료. 실행 근거는 [검증 기록](../../Verification/project-wide/level-tool-ui-stage-01-progress.md)에 정리한다.
> 실행 담당: `superpowers:executing-plans`로 작업 묶음별 구현·검증. 필요하면 하위 에이전트 사용 가능. 이번 문서 작성으로 자동 구현을 시작하지 않는다.

**Goal:** 실제 레벨툴 씬을 목업의 화면 구조로 재배치하고 메뉴·버튼·공통 단축키·도움말이 같은 명령과 상태를 사용하게 한다.
**Architecture:** 기존 LevelToolScreen의 편집/저장/시험 메서드와 세션을 유지한다. 화면 구성, 작업 이동, 명령/키 처리를 역할별 partial 파일로 분리한다. 매 입력마다 전체 트리를 재생성하지 않도록 변경 구역만 갱신하고 포커스·스크롤을 보존한다. 범용 앱 프레임워크를 새로 만들지 않는다.
**Tech Stack:** Unity 6000.3.10f1, Runtime UI Toolkit, 기존 USS·LevelTool 씬·Workspace/Session.
**Spec:** [4단계 통합 가이드 및 단축키 계약](level-tool-ui-overhaul-plan.md), [Windows 목업](../../MoonRabbitJunkyard/Mockups/level-tool-windows.html).
**완료 조건:** [목표 문서](../../Goals/project-wide/level-tool-ui-stage-01-goal.md) · [실행문](../../Commands/project-wide/level-tool-ui-stage-01-command.md).

## 공통 제약

- 기존 JSON·MemoryPack 형식, 게임 규칙, 안정 ID, 원본 채택/복구 정책을 변경하지 않는다.
- 모든 기존 기능에 도달할 수 있어야 한다. 목업의 가짜 시험 결과·저장 표시·샘플 수치를 제품에 옮기지 않는다.
- 기존 work 및 미커밋 변경, 씬·프리팹·데이터·메타를 보존한다. 변경 대상의 현재 상태를 먼저 확인한다.
- 커밋·푸시·Player/Addressables 빌드 금지. HTML 목업/매뉴얼은 이번 구현의 편집 대상이 아니다.
- UI Toolkit을 유지하고 새 패키지나 UnityEditor 의존을 Runtime에 넣지 않는다.
- 질문은 일반 채팅 문답으로 충분히 설명한다. 시간 제한 질문·무응답 자동 결정 금지.
- 1단계 내부는 아래 A/B/C 세 묶음이다. 묶음을 별도 대단계로 쪼개지 않는다.
- 완료 후 2단계 계획·목표·실행문을 작성하고 보고한다. 2단계 구현은 자동 시작하지 않는다.

## 현재 근거와 변경 위치

조사 시 `LevelToolScreen.OnEnable`이 상단 버튼/세 패널을 만들고, `Refresh`가 레벨 목록·팔레트·보드·속성을 모두 비운다. `DrawInspector`의 페이지 선택으로 기본/공급/흐름/연결/모양/튜토리얼/자동 시험을 전환한다. 기존 키 처리는 튜토리얼 지정의 Enter/Esc가 중심이다. 구현 전 최신 코드로 다시 확인한다.

| 위치 | 담당 변경 |
|---|---|
| `Assets/Scripts/Features/LevelTool/Runtime/LevelToolScreen.cs` | 수명주기와 상태 갱신, 새 화면/명령 연결. 같은 버튼을 중복 생성하지 않음 |
| 신규 `LevelToolScreen.Layout.cs` | 헤더·메뉴·탭·좌우 패널·하단 검사/상태·패널 접기와 맞춤 |
| 신규 `LevelToolScreen.Navigation.cs` | 기존 inspectorPage/시험 기록과 새 작업 탭의 대응, 전환 전 입력 정리 |
| 신규 `LevelToolScreen.Commands.cs` | 명령 ID·표시 이름·키·설명·가용성·실행 진입, 메뉴와 툴바 공유 |
| 신규 `LevelToolScreen.Shortcuts.cs` | 포커스/IME/모달/반복 키에 따른 dispatch, 등록·해제 |
| 신규 `LevelToolScreen.ShortcutHelp.cs` | 같은 명령 정의를 읽는 검색 가능한 한국어 도움말 |
| 기존 `LevelToolScreen.Board.cs`, `.Validation.cs`, `.Storage.cs`, `.TutorialPicking.cs`, `.Play.cs`, `.PackTrial.cs`, `.Handoff.cs` | 새 컨테이너로 연결, 직접 toolbar.Q 가정 제거, 입력·상태 보호. 업무 로직은 재사용 |
| `Assets/UI/LevelTool/LevelTool.uss` | 목업 계열 색·간격·위계, 창 크기 반응, 포커스·disabled 스타일 |
| `Assets/Scripts/Features/Products/Editor/ProductProfiles.cs` | 도구 프로필에만 창 모드/크기 조절/기본 크기 설정, 재구성 후에도 보존 |
| `Assets/Settings/BuildProfiles/Windows Level Editor.asset` | 기존 프로필 API를 통한 도구 전용 창 설정. 직접 YAML 대량 재작성 금지 |
| 필요 시 `Assets/Scenes/LevelTool.unity`, 기존 도구 화면 프리팹 | 필요한 연결만 Unity 도구로 변경. 새 씬/중복 프리팹은 만들지 않음 |
| 외부 `Tests/Editor/Features/LevelAuthoring/` 및 `Tests/Editor/Features/Products/` | 아래 회귀 검사. Assets에 영구 테스트 코드 추가 금지 |

신규 파일 경로의 공통 루트는 `Assets/Scripts/Features/LevelTool/Runtime/`다. 같은 책임이 기존 파일에 충분히 들어가면 파일을 추가하지 않아도 되지만 책임·인터페이스를 유지한다.

## A. 화면 구조와 기존 기능 이동

**산출:** 목업 형태의 실제 작동 화면. 기존 세션과 파일/게임 경로를 그대로 사용한다.

- [x] 기존 기능/컨트롤 이름과 새 위치의 대응표를 `Docs/Verification/project-wide/level-tool-ui-stage-01-progress.md`에 기록한다. 기존 검사에서 찾는 name을 가능하면 유지하고, 실제 변경된 화면 위치에 맞춰 검사 선택자만 수정한다.
- [x] 신규 `LevelToolShellVerification.Run`을 작성한다. 다섯 작업 탭·레벨/소재 전환·선택 항목/레벨 설정·파일/편집/보기/시험/도움말·검사 결과 영역이 존재하고, 기존 공급/연결/모양/튜토리얼/봇/기록 진입이 가능해야 한다. 현재 화면에서 요구 구조 부재로 실패함을 확인한다.
- [x] 위 Layout/Navigation을 구현한다. `BuildEditorLayout()`은 한 번 컨테이너를 만들고, `SelectWorkspacePage(string page)`는 기존 기능과 연결한다. 같은 레벨에서 작업 탭 전환만으로 세션·선택·이력·미저장이 바뀌면 안 된다. 진행 중 칸 지정/흐름 그리기는 기존 취소 규칙으로 정리하고 상태를 안내한다.
- [x] 기존 Draw 계열을 해당 컨테이너로 옮긴다. 보드/속성/목록의 변경 필요 여부를 구분한다. 모달·검사 메시지 갱신만으로 작성 중인 필드와 보드 이미지 로딩을 재시작하지 않는다. 기존 artworkRevision·취소/Dispose 규칙은 유지한다.
- [x] 헤더에 실제 레벨/폴더/미저장/복구/공유 사본 상태, 저장·Undo/Redo·게임 시험을 표시한다. 작업 폴더 열기/다시 읽기/이전 정상본/종료는 파일 메뉴에서 실제 기존 동작에 연결한다.
- [x] 검사는 하단으로 옮기되 `ValidationIsCurrent`와 오류 클릭 이동을 보존한다. 단순 화면 변경으로 검사가 오래된 것으로 바뀌지 않고, 실제 문서 변경 시 다시 검사 필요로 표시한다.
- [x] 1366×768, 1920×1080, 1280×800에서 기본 화면과 하단 검사 펼침/모달 상태를 확인한다. 보드는 사용 가능 영역에 맞추고, 필요한 패널 내부 스크롤을 제공한다. 1024×768에서는 패널 접기/스크롤로 모든 필수 버튼에 도달할 수 있어야 한다.
- [x] `ToggleBoardFocus()`로 좌우 패널을 접고 복원한다. 단순 보기 상태는 문서 dirty/Undo에 기록하지 않는다. 색·기본 화면 틀은 목업을 따르되 소재 이미지 검색/상세 배치 도구는 기존 기능 유지 후 2단계에서 완성한다.
- [x] 신규 구조 검사와 기존 진입·공유 사본/복구 검사를 통과시킨다.

## B. 공통 명령·단축키·도움말

**산출:** 메뉴와 키가 같은 의미로 작동하고, 가능한/불가능한 이유가 일치하는 명령 체계.

**인터페이스:** 도구 내부 `ToolCommandId`, 명령 항목의 `Id/Label/Gesture/Description/Scope`와 `CanExecuteCommand(ToolCommandId, out string reason)`, `TryExecuteCommand(ToolCommandId)`를 사용한다. 비동기 동작은 기존 `Storage/BeginPlay`와 busy 경계로 연결한다. 기존 슬롯에 단순 구현하며 플러그인형 레지스트리·별도 DI/서비스 버스를 도입하지 않는다.

- [x] 신규 `LevelToolShortcutVerification.Run`으로 메뉴/버튼/키의 실행 횟수 일치, 없음/미저장/busy/공유 사본/모달 상태별 허용 여부, 같은 키 반복·Enable/Disable 후 중복 실행 방지를 먼저 고정한다.
- [x] 1단계 키: Ctrl+O, Ctrl+S, Ctrl+Shift+S, Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z, F1, F5, Esc, 보드 포커스의 Shift+Space. 메뉴 우측 키와 버튼 툴팁을 같은 정의에서 만든다. 세션 Undo/Redo는 실제 CanUndo/CanRedo를 사용하고 공유 사본 편집 시 그 세션을 따른다.
- [x] 키 처리를 최상위 모달/메뉴 → 텍스트/숫자/IME → 대상 지정 → 활성 제작 명령 순서로 분기한다. 기존 TutorialPicking의 Enter/Esc가 중복 처리되지 않게 한 소유자로 연결한다. Tab/Shift+Tab 이동은 보존한다.
- [x] 문자를 입력하는 동안 C/V/X/A/Z/Y/Delete/V/B/E와 Space가 보드에 전달되지 않는 검사를 추가한다. Ctrl+S는 값 확정이 완료된 경우만 문서 저장하고, 조합 중/유효하지 않은 숫자는 보류해 입력을 보존한다. 텍스트 필드의 기본 복사·붙여넣기·Undo를 바꾸지 않는다.
- [x] `TextField.isDelayed`와 IntegerField에 값을 입력하고 포커스를 옮기기 전 Ctrl+S를 누른 사례를 Play Mode에서 검증한다. 저장 파일에 새 값이 들어가며 마지막 글자를 잃지 않아야 한다. 잘못된 값/쓰기 실패/외부 충돌 시 저장됨으로 표시하지 않는다.
- [x] 도움말 창을 검색 가능하게 만든다. 각 키의 이름·적용 영역·주의점과 OS 기본 창 조절을 설명한다. 예: “Ctrl+C: 글을 입력할 때는 선택한 글을 복사합니다. 보드 요소 복사는 2단계에서 추가됩니다.” 앱 메뉴에는 아직 미구현인 보드 복사 키를 실행 가능으로 넣지 않는다.
- [x] 메뉴/도움말의 포커스 이동과 Esc 닫기를 검사한다. 가장 위 패널만 닫히고 배경 선택 취소나 지우기가 함께 발생하지 않아야 한다. 닫힌 뒤 이전 편집 위치로 돌아간다.
- [x] Unity Game View 포커스, 다른 Editor 창 포커스, 비활성화·다시 활성화에서 범위를 확인한다. 호스트가 먼저 처리하는 키를 전역 ShortcutManager 등록으로 강제로 덮어쓰지 않는다. 실제 Editor 제한은 메뉴 대안과 함께 보고한다.

## C. 창 설정·전체 회귀·인계

**산출:** 도구에서만 창 크기 조절을 지원할 설정, 검증된 화면/명령/자료 보존, 다음 단계 인계.

- [x] 신규 `LevelToolWindowProfileVerification.Run`: 도구 프로필만 resizable, 창 모드, 기본 1280×800이고 전역/다른 세 프로필의 의미 있는 설정과 GUID가 불변인 검사를 작성한다. 현재 프로필을 읽어 차이를 먼저 확인한다.
- [x] 기존 ProductProfiles의 프로필 PlayerSettings 어댑터로 Windows Level Editor만 변경한다. 구현에서 Unity enum/API로 값을 확인하고 숫자를 추측해 쓰지 않는다. Configure 재실행 후에도 도구 창 설정을 유지한다. 이미 사용자 설정이 있는 경우 의도한 세 속성 밖은 덮어쓰지 않는다.
- [x] 실제 OS 창 크기/최대화/F11은 이번 빌드 금지로 검증할 수 없는 배포 항목임을 남긴다. Editor에서는 뷰포트 변경에 따른 UI 적응만 검증하고 두 결과를 혼동하지 않는다.
- [x] 실제 Play Mode에서 입력→Undo/Redo→저장→재열기, 튜토리얼 대상 지정→취소, 공유 사본 유지, 게임 시험→복귀를 실행한다. 동일 작업 상태에서 선택·미저장·이력·스크롤·편집 포커스를 확인한다.
- [x] 메뉴 이동으로 기존 공급/연결/모양/샘플/공유/봇/기록/복구 기능이 사라지지 않았는지 대응표를 마감한다. 기존 회귀가 실패하면 테스트 기대만 낮춰 통과시키지 않는다.
- [x] 최종 스크린샷, 실행한 검사 이름/로그/결과, 실제 검증 못한 범위, 목업에서 개선한 항목을 검증 문서에 기록한다. 테스트 연결 해제와 diff 점검을 확인한다.
- [x] 1단계 목표의 모든 필수 항목에 근거를 붙인 후 완료 표시한다. 2단계 계획·목표·복사용 명령문을 작성한다. 마지막 보고에서만 완료한 변경에 맞는 커밋 메시지를 코드 블록으로 제공한다.

## 검증 실행

Unity 검사는 하나씩 실행한다. 실행 중 Assets C#을 동시에 수정하지 않는다. 아래 신규 메서드는 본 계획에서 작성할 검사이며 현재 이미 존재한다고 가정하지 않는다.

```powershell
& Tools/Testing/ProjectTests.ps1 -Action Run -Method LevelAuthoring.Editor.LevelToolShellVerification.Run
& Tools/Testing/ProjectTests.ps1 -Action Run -Method LevelAuthoring.Editor.LevelToolShortcutVerification.Run
& Tools/Testing/ProjectTests.ps1 -Action Run -Method Products.Editor.LevelToolWindowProfileVerification.Run
```

기존 회귀의 우선 대상: `LevelToolGatewayVerification.Run`, `LevelToolTrialLifecycleVerification.Run`, `PackedToolPlayVerification.Run`, `ProductBoundaryVerification.Run`. 실제 호출/선택자 변경 범위를 조사해 관련 공급·공유·튜토리얼 검사도 선택한다. 데이터 생성/검사와 Player/번들 빌드를 구분하며 빌드 메서드를 실행하지 않는다.

## 검토 초점

| 위험 | 책임·관찰할 결과 |
|---|---|
| 탭 변경으로 기존 기능/미저장 작업 유실 | A·C: 기능 대응표, 같은 문서/선택/이력, 공유 사본 상태 |
| 키 입력과 메뉴가 서로 다른 조건으로 실행 | B: 같은 명령과 가용성, 한 번 실행, disabled 이유 |
| 한글 조합·지연 입력이 저장에서 누락 | B: 실제 TextField/숫자 입력, 값 확정/보류 및 저장 파일 대조 |
| 모달 Esc/F5/Ctrl+S가 배경에 새어 나감 | B·C: 최상위 소유권, busy/공유/시험 중 제한 |
| 작은 창/검사 결과로 보드·필수 버튼 잘림 | A·C: 뷰포트별 측정/스크린샷/스크롤 및 도구만 창 설정 |

## 이 단계에서 미리 하지 않는 것

보드 요소 클립보드/새 검색 팔레트/팬·확대 키는 2단계, 튜토리얼 조립 화면의 본격 재설계는 3단계, 시험·기록 전체 화면 마감과 F11은 4단계다. 기존에 가능한 기능은 모두 유지한다. 사용자 키 재지정, UI 프레임워크 교체, 전면 MVVM 전환, 배포 빌드는 포함하지 않는다.

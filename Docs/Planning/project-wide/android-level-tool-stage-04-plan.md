# Android 레벨툴 4단계 — 파일 전송·복구

> **실행 절차:** `superpowers:executing-plans` 또는 `superpowers:subagent-driven-development`로 아래 네 작업을 순차 검증한다. Unity 검사·에셋 변경·프로필 전환은 겹치지 않는다. 커밋·푸시는 하지 않는다.

2026-10-11 · **실기기·빌드를 제외한 요청 범위의 구현·자동 검증 완료.** 근거는 [진행 기록](../../Verification/project-wide/android-level-tool-stage-04-progress.md)에 남겼다. 실제 Android·Windows 네이티브 실행은 미검증으로 보존한다.

[통합 가이드](2026-10-11-android-level-tool-plan.md) · [목표·완료 조건](../../Goals/project-wide/android-level-tool-stage-04-goal.md) · [복사용 실행문](../../Commands/project-wide/android-level-tool-stage-04-command.md)

**Goal:** 같은 JSON 프로젝트를 Windows와 Android 사이에서 ZIP으로 주고받고, 내부 작업본과 미저장 초안·Undo 이력을 안전하게 다시 연다.

**Architecture:** ZIP 읽기·검증·쓰기는 공통 저장 경계에서 수행한다. 시스템 선택기는 스트림만 전달하며 Android URI와 Windows 대화상자는 플랫폼 경계에 둔다. 기존 `AuthoringToolWorkspace`, `ContentSnapshotStore`, `ToolDraftStore`의 저장·복구 계약을 확장하고 편집 세션을 복제하지 않는다.

**Tech Stack:** Unity 6000.3.10f1, C#, 기존 Newtonsoft.Json·UniTask·Input System, .NET ZIP/Stream API, 런타임 UI Toolkit. Android 시스템 문서 접근과 Windows 시스템 파일 선택기는 기존 의존성을 우선한다.

**Spec:** [5단계 통합 가이드](2026-10-11-android-level-tool-plan.md)의 데이터와 파일 접근 계약 및 4단계. 구현 요청이 오면 실제 3단계 상태와 아래 설계값을 먼저 확인한다.

## 공통 제약

- 기본 편집 장소는 앱 내부 프로젝트 폴더다. 외부 문서의 원본을 직접 편집하지 않는다.
- Windows/Android의 같은 JSON 스키마·문서 ID·개체 ID·정의/튜토리얼 참조를 유지한다. ScriptableObject를 Android 저장소로 사용하지 않는다.
- ZIP은 정상 저장본의 `project.json`과 그 공개 목록에 포함된 JSON 문서만 담는다. 다른 저장 세대·복구 기록·Undo·PC 절대 경로·임시 파일을 포함하지 않는다. 기존 리소스 ID/상대 경로 매핑은 유지하며 게임 리소스 번들을 새로 만들지 않는다.
- Android `content://`를 파일 경로로 변환하지 않는다. 어댑터에서 스트림으로 접근하며 URI 재사용 권한은 이번 단계의 최근 내부 프로젝트 기록과 분리한다.
- 가져오기는 임시 폴더에서 형식·전체 참조를 검증하고 새 내부 프로젝트로만 등록한다. 기존 프로젝트 병합·덮어쓰기·단일 레벨 전송·클라우드 동기화는 제외한다.
- 내부 저장 성공과 외부 내보내기 성공을 구분한다. 취소·권한 거부·쓰기 실패로 내부 저장본·편집 세션·Undo를 잃지 않는다.
- 플러그인·SDK·Android 모듈을 임의 설치하지 않는다. 기존 경계에서 구현할 수 없는 플랫폼 기능은 근거와 필요한 선택지를 보고한다.
- 커밋·푸시·Player/APK/번들/Addressables 빌드·HTML 매뉴얼 갱신은 별도 요청 없이 하지 않는다. 질문은 제한시간 없는 일반 채팅으로 진행한다.
- 실제 시스템 선택기·문서 제공자·OS 뒤로가기·프로세스 종료 검증은 별도 빌드/기기 요청 전까지 미검증이다. Editor 모의 스트림 검사를 해당 검증으로 대체하지 않는다.

## 확인한 파일과 변경 경계

| 파일 | 현재 책임과 4단계 경계 |
|---|---|
| `Assets/Scripts/Features/LevelAuthoring/Storage/ContentSnapshotStore.cs` | 저장 세대의 JSON을 쓰고 `project.json`을 마지막에 공개한다. ZIP에서 복구/임시 파일을 제외하고 검증된 저장본을 읽는 기준으로 재사용한다. |
| `Assets/Scripts/Features/LevelAuthoring/Storage/JsonContentStore.cs` | 상대 경로·JSON 저장 검증을 유지한다. ZIP 전용 경로 검사는 이를 보완한다. |
| `Assets/Scripts/Features/LevelAuthoring/Editing/AuthoringToolWorkspace.cs` | 프로젝트 전환 실패 시 기존 세션을 유지하고 저장/버리기/취소를 처리한다. 가져오기 성공 후 전환도 이 경계를 따른다. |
| `Assets/Scripts/Features/LevelAuthoring/Editing/AuthoringEditSession.State.cs` | 미완성 current 문서와 saved 기준·Undo/Redo·선택을 직렬화한다. 복구에서 정상 저장본으로 강제 치환하지 않는다. |
| `Assets/Scripts/Features/LevelAuthoring/Storage/ToolDraftStore.cs` | 임시 파일·이전 복구본을 사용한 초안 기록이 이미 있다. 새 복구 엔진을 만들지 않는다. |
| `Assets/Scripts/Features/LevelTool/Runtime/LevelToolProjectAccess.cs` | Android 내부 프로젝트의 이름·경로·외부 폴더 연결을 제한한다. 가져오기 등록과 최근 프로젝트 식별에 사용한다. |
| `Assets/Scripts/Features/LevelTool/Runtime/LevelToolScreen.Storage.cs` | 파일 작업 잠금·dirty 확인·현재 내부/Windows 폴더 선택을 담당한다. ZIP 메뉴와 명확한 저장/전송 상태를 연결한다. |
| `Assets/Scripts/Features/LevelTool/Runtime/LevelToolScreen.Recovery.cs` | 자동 초안·복원 선택·종료 확인·화면 상태 보관이 이미 있다. pause/resume과 프로젝트별 복구 소유권을 보완한다. |
| `Assets/Scripts/Features/LevelTool/Runtime/LevelToolScreen.BoardTouch.cs`, `LevelToolScreen.Shortcuts.cs` | pause/focus/뒤로가기의 기존 제스처 취소 경로를 유지하며 중복 수명주기 콜백 없이 복구/종료 순서를 연결한다. |

신규 파일은 아래 작업의 패키지·플랫폼·최근 프로젝트 책임에 한정한다. 실제 선택기 의존성과 활동 결과 수신 경계는 작업 2의 조사 결과로 확정한다. Android Java/URI 호출을 공통 편집·JSON 저장 코드로 옮기지 않는다.

## 우선 검수할 실패 상황

1. ZIP의 `../`, 절대 경로, 역슬래시, 대소문자 충돌, 심볼릭 링크 엔트리 → 내부 임시 폴더 밖에 쓰지 않고 전체 가져오기를 거절한다. 작업 1에서 검사한다.
2. 알려진 길이가 없는/seek 불가능한 URI 스트림, 짧은 읽기, 매우 큰 압축 해제 결과 → 스트리밍 중 실제 크기를 제한하고 기존 프로젝트를 유지한다. 작업 1·2에서 검사한다.
3. 선택기 취소·반환 지연·일시 중단 뒤 오래된 결과 → 현재 화면/요청에 한 번만 결과를 적용하며 원본을 유지한다. 작업 2에서 검사한다.
4. 최근 프로젝트가 이동/삭제됐거나 복구 기록의 프로젝트와 다름 → 자동 기본 프로젝트 생성이나 다른 세션으로 덮지 않고 복구/다른 프로젝트 열기를 제공한다. 작업 3에서 검사한다.
5. IME 입력 중 뒤로가기·pause, 저장 실패, pause→focus 중복 이벤트 → 입력 확정 또는 미확정 입력 보존 후 한 번 기록하고 제스처 미리보기만 취소한다. 작업 3·4에서 검사한다.

## 작업 1. 공통 ZIP 전송과 원자적 가져오기

**파일:** 신규 `Assets/Scripts/Features/LevelAuthoring/Storage/ProjectPackageStore.cs`, 관련 기존 저장/작업공간 파일의 최소 연결, 신규 `Tests/Portable/LevelAuthoring/ProjectPackageChecks.cs`.

**인터페이스:** `ProjectPackageStore.Export(string savedProjectFolder, Stream destination, CancellationToken cancellationToken)`와 `ProjectPackageStore.Import(Stream source, string internalProjectsRoot, string projectName, CancellationToken cancellationToken): string`. 반환 경로는 검증 후 등록한 새 프로젝트 폴더다. 호출자가 스트림을 소유하고 닫는다. 공통 패키지는 화면/플랫폼 API를 참조하지 않는다.

- [x] 저장 fixture의 프로젝트·참조 레벨/정의/튜토리얼을 ZIP으로 내보내고 빈 내부 루트에 가져와 재열기하는 실패 검사를 먼저 추가한다. 문서 의미·ID·참조를 비교하며 저장 세대 경로·ZIP 타임스탬프의 바이트 동일성은 요구하지 않는다.
- [x] `System.IO.Compression`으로 정상 공개 목록의 JSON만 전송한다. 모든 엔트리를 먼저 검사하고 임시 폴더에 풀어 `ContentSnapshotStore.Read()`의 전체 검증을 통과한 뒤 같은 내부 루트에서 새 이름으로 등록한다. 등록 전 취소/실패는 현재 작업공간을 바꾸지 않는다. 등록 후 UI 전환 실패 시 검증된 새 프로젝트는 목록에서 다시 열 수 있게 남기고 기존 세션을 유지한다.
- [x] ZIP 경로는 `/` 상대 경로만 허용한다. `..`/`.` 세그먼트·절대/드라이브/UNC 경로·역슬래시·중복/대소문자 충돌·링크 엔트리를 거절한다. 정규화한 최종 경로가 임시 루트 안인지 확인한다. 루트 `project.json` 하나와 공개 목록의 문서만 허용하며 누락·추가 파일·잘못된 JSON/ID/참조를 거절한다.
- [x] 초기 상한은 압축 입력 128 MiB, 전체 해제 512 MiB, 엔트리 10,000개, 개별 JSON 16 MiB로 둔다. 엔트리 선언값뿐 아니라 실제 읽은 바이트에 적용하고 ZIP 폭탄·잘린 입력·seek 불가능한 스트림을 검사한다. fixture 크기로 정상 프로젝트의 여유를 확인하고 조정이 필요하면 값과 근거를 기록한다. 사용자 설정 화면은 추가하지 않는다.
- [x] 한글 프로젝트 이름·Unicode JSON, 잘못된 ZIP·참조·경로 탈출·이름 충돌·취소·디스크 쓰기 실패를 검사한다. 실패 전후 현재 JSON·dirty·선택·Undo/Redo와 기존 내부 프로젝트 목록이 동일하고 임시 폴더만 정리되는지 확인한다.

검증: `& ./Tools/Testing/JsonAuthoringChecks.ps1 -Action Portable`로 기존 fixture·JSON 라이브러리와 새 검사를 실행한다. 정상 종료 코드 0과 `PASS ... portable checks`를 확인한다. 새 검사가 실패하는 이유를 확인한 뒤 구현하고, 모두 통과한 로그를 4단계 기록에 남긴다. 폴더 정리 대상은 생성한 임시 폴더로 한정한다.

## 작업 2. Windows/Android 시스템 선택기와 스트림 경계

**파일:** 신규 `Assets/Scripts/Features/LevelTool/Runtime/LevelToolFileTransfer.cs`, 신규 플랫폼 어댑터 파일을 같은 기능 영역에 배치, 기존 `LevelToolScreen.Storage.cs`·`LevelToolScreen.Commands.cs`, 신규 `Tests/Editor/Features/LevelAuthoring/LevelToolTransferRecoveryVerification.cs`. 기존 패키지·Android 활동 구성은 필요한 부분만 읽고 조사 결과를 기록한다.

**인터페이스:** `ILevelToolFileTransfer.OpenProject(CancellationToken): UniTask<Stream>`와 `CreateProject(string suggestedName, CancellationToken): UniTask<Stream>`. 취소는 null 결과로 통일한다. 실패는 취소와 구분한다. 반환 스트림은 호출자가 dispose하며 어댑터가 화면에 완료 안내를 직접 쓰지 않는다. 한 번에 파일 선택 요청 하나만 활성화한다.

- [x] 현재 패키지·네이티브 코드·Unity Android 활동 구성에서 시스템 선택기 재사용 후보와 결과 전달 방식을 조사한다. Windows 파일 선택 API와 Android SAF 열기/생성·URI 읽기/쓰기 경계를 확정한다. 새 플러그인 설치가 꼭 필요하면 구현 가능한 공통 작업을 먼저 마친 뒤 이유와 선택지를 일반 채팅으로 제시한다.
- [x] 모의 선택기로 취소·권한 오류·스트림 읽기/쓰기 실패·오래된 콜백·중복 결과·선택기 표시 중 pause/resume을 재현한다. Android URI가 공통 `Path`/`File` API에 들어가지 않는지 검사한다.
- [x] Windows 시스템 파일 선택기와 Android 시스템 문서 선택기를 조건부 플랫폼 경계로 구현한다. Android는 열기/생성 결과 URI의 스트림을 사용하고 파일명만으로 실제 경로를 추정하지 않는다. 선택기 복귀 후 유효한 화면/요청인지 확인하고 임시 스트림·권한 자원을 해제한다.
- [x] 가져오기 메뉴는 이름 확인→작업 1 검증/등록→기존 dirty 전환 계약으로 연결한다. 내보내기는 내부 저장 후 정상 저장본을 전송한다. 상태 안내는 `내부 저장 완료`, `ZIP 내보내기 완료`, `전송 취소`, `내보내기 실패 · 내부 저장본 유지`를 구분한다. 외부 쓰기·flush·close 성공 전 내보내기 완료를 표시하지 않는다. 제공자가 원자적 외부 쓰기를 보장하지 않으면 실패한 외부 파일은 불완전할 수 있음을 안내한다.

검증: 신규 Editor 검사의 진입점을 `LevelAuthoring.Editor.LevelToolTransferRecoveryVerification.RunAll`으로 두고 `& ./Tools/Testing/ProjectTests.ps1 -Action Run -Method LevelAuthoring.Editor.LevelToolTransferRecoveryVerification.RunAll`을 순차 실행한다. 로그의 실패 없음·정상 종료를 확인하고 실제 UI 메뉴/작업 잠금·모의 스트림·Windows 기존 열기/저장/사본 저장을 검사한다. 가능하면 Android 타깃 스크립트 컴파일만 순차 수행한다. 네이티브 선택기·실제 SAF 제공자·Windows 실행 파일 대화상자는 각 실제 실행 전까지 미검증이다.

## 작업 3. 최근 프로젝트와 중단·재개·뒤로가기 복구

**파일:** 신규 `Assets/Scripts/Features/LevelAuthoring/Storage/RecentProjectStore.cs`, 기존 `AuthoringToolWorkspace.cs`·`ToolDraftStore.cs`·`LevelToolProjectAccess.cs`·`LevelToolScreen.Recovery.cs`·`LevelToolScreen.Storage.cs`·관련 pause/focus/뒤로가기 연결, 신규 `Tests/Portable/LevelAuthoring/ProjectRecoveryChecks.cs`와 작업 2 Editor 검사에 수명주기 케이스 추가.

**인터페이스:** `RecentProjectStore(string stateDirectory)`, `Read(): string[]`, `RecordOpened(string internalProjectName)`. 최근 목록은 내부 프로젝트 이름을 중복 없이 최신순 최대 10개 보관한다. 외부 URI·Windows 절대 경로를 Android 최근 기록으로 넣지 않는다. 기존 `Workspace.ExportState()/RestoreState()`와 `ToolDraftStore.Save()/Read()`를 복구 데이터 계약으로 재사용한다.

- [x] 최근 기록·프로젝트 전환·복구 실패 검사를 먼저 추가한다. 가져오기 등록 또는 내부 프로젝트 열기가 성공한 후에만 최근 목록을 갱신한다. 삭제/누락 프로젝트는 목록에서 경고하되 현재 세션과 복구 기록을 유지한다. 최근 목록 손상은 프로젝트 JSON을 손상시키지 않는다.
- [x] 기존 단일 `draft.json`의 소유권과 프로젝트 전환 순서를 정리한다. 다음 프로젝트 읽기·dirty 선택·등록이 실패하면 이전 초안·saved 기준·Undo/Redo를 유지한다. 성공한 전환 이후 현재 프로젝트를 식별한 복구 기록을 공개하고, 재실행에서는 복원/저장본으로 돌아가기 선택을 우선한다.
- [x] pause/focus loss에서 진행 중 터치를 취소하고 확정 세션·선택·층·도구·뷰와 입력 중 초안을 기록한다. 중복 콜백과 비동기 체크포인트의 역순 완료가 새 기록을 덮지 않게 기존 sequence 계약을 확인한다. resume에서 살아 있는 세션을 파일로 덮어쓰지 않는다. 미완성 편집 문서는 복구 가능해야 하며 저장/전송 전체 검증과 구분한다.
- [x] 뒤로가기는 진행 중 조작 취소→열린 작업 메뉴/패널 닫기→미저장 변경의 저장/버리기/취소→종료 순서로 연결한다. 현재 IME 입력과 공유 튜토리얼 초안의 기존 확정/취소 계약을 지킨다. 저장 실패는 종료를 막고 편집본·이력을 유지한다. 파일 선택 요청 중 뒤로가기는 그 요청 취소를 먼저 처리한다.
- [x] 복구본 손상 시 이전 복구본·정상 내부 저장본의 선택을 제공하고 자동 덮어쓰기를 막는다. OS가 pause 콜백 없이 프로세스를 종료할 수 있으므로 주기적 체크포인트 이후 변경 보존 한계를 명시한다. 종료 콜백만으로 모든 변경 보존을 보장한다고 보고하지 않는다.

검증: 새 세션으로 복원한 current/saved JSON·dirty·선택·Undo/Redo 결과를 비교한다. pause→resume·focus 중복·뒤로가기 취소/저장 실패·프로젝트 전환 실패·복구본 손상/이전본 사용을 Editor/Portable에서 재생한다. 실제 OS 프로세스 종료·저장 장치 동작은 미검증으로 남긴다.

## 작업 4. 통합 회귀·보고와 5단계 준비

**파일:** 기존 `Tests/Portable/LevelAuthoring/`·`Tests/Editor/Features/LevelAuthoring/` 검사 진입점의 최소 연결, 신규 `Docs/Verification/project-wide/android-level-tool-stage-04-progress.md`. 실제 완료 후 5단계 상세 계획·목표·복사용 실행문을 작성한다.

- [x] 대표 일반/파워 블록·2×2 장애물·바닥·덮개·발전기 내부 연결·튜토리얼 참조 프로젝트를 편집→Undo/Redo→내부 저장→ZIP 내보내기→새 프로젝트 가져오기→재열기로 검사한다. Windows/Android 모의 스트림 경로의 JSON 의미와 기존 시험 변환 결과를 비교한다. 실제 PC→Android→PC 왕복은 별도 미검증 항목이다.
- [x] 2·3단계의 좁은/넓은 화면·회전·키보드 초안·팝업·터치 취소·Windows 마우스/단축키·16:10 회귀를 필요한 범위에서 수행한다. 파일 처리 중 중복 입력과 완료 후 포커스 복원을 확인한다.
- [x] 가능한 Android 타깃 컴파일을 검사하고 임시 테스트 연결·제품 프로필·콘텐츠 선택을 복원한다. 모듈이 없으면 임의 설치하지 않는다. Player/APK/번들은 생성하지 않는다.
- [x] 변경·자동 검사 근거·플랫폼 어댑터 구현 상태·실기기 미검증·외부 제공자 쓰기 제약·복구 손실 가능 구간을 진행 기록에 남긴다. 실제 완료 조건을 충족한 항목만 체크한다. 5단계 문서를 작성하되 구현은 자동 시작하지 않는다.

## 완료 판정

사용자가 실기기·빌드를 제외한 진행을 요청했다. 공통 ZIP/복구·실제 Editor 화면 연결·관련 회귀·Android 스크립트 컴파일을 통과하여 이번 요청 범위의 구현·자동 검증을 완료했다. 체크 표시는 이 범위이며 실제 시스템 선택기·URI 제공자·OS 뒤로가기·프로세스 종료·PC/Android 왕복은 포함하지 않는다. 실제 플랫폼 검수와 출시 준비는 별도 미검증으로 유지한다. 근거는 [진행 기록](../../Verification/project-wide/android-level-tool-stage-04-progress.md)에 남긴다.

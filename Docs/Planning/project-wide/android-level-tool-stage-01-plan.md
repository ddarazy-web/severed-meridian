# Android 레벨툴 1단계 — 독립 제품·내부 JSON 작업 기반

2026-10-11 · 구현·자동 검사 완료. APK·실기기 검증은 수행하지 않음.

[통합 가이드](2026-10-11-android-level-tool-plan.md) · [목표·완료 조건](../../Goals/project-wide/android-level-tool-stage-01-goal.md) · [목표 실행문](../../Commands/project-wide/android-level-tool-stage-01-command.md)

**Goal:** 기존 Android 게임과 별도의 레벨툴 제품을 구성하고, 같은 LevelTool 씬과 공통 편집 로직으로 앱 내부 JSON 프로젝트를 생성·저장·다시 열 수 있게 한다.
**Architecture:** `PRODUCT_LEVEL_EDITOR`를 공통 도구 심볼로 유지하며 제품은 심볼과 BuildTarget으로 식별한다. Windows 파일·창 기능과 Android 내부 작업 폴더 접근만 분리한다. AuthoringToolWorkspace/AuthoringEditSession·JSON 저장소·배치 검증·Undo는 공유한다.
**Tech Stack:** 프로젝트의 Unity 6000.3.10f1, C#, UI Toolkit, 기존 JSON·MemoryPack·Addressables.
**Spec:** [Android 5단계 통합 가이드](2026-10-11-android-level-tool-plan.md)의 1단계. 이번 문서는 실행을 위한 계획이며 APK 생성이나 실기기 사용 완료를 의미하지 않는다.

## 이번 계획 요청과 기존 작업의 관계

이번 요청은 1단계 계획·목표·복사용 실행문 정리다. 새 구현이나 다음 단계 실행을 자동 시작하지 않는다. 기존 체크 항목과 검증 기록은 유지한다. 실행문을 다시 사용할 경우 현재 변경 사항과 진행 기록을 먼저 확인하고, 이미 구현한 기능은 재작성하지 않으며 부족한 구현·검증만 처리한다. 다른 단계의 미완료 작업을 이번 단계 완료로 간주하지 않는다.

## 1단계 결과와 제외 범위

결과는 Android 레벨툴을 별도 제품으로 선택할 수 있는 프로필과 내부 프로젝트 작업 기반이다. Windows판을 다른 앱으로 재작성하지 않는다.

- 포함: 제품 프로필 생성/선택/감사, 플랫폼별 콘텐츠 경로, 내부 프로젝트 생성·열기·저장·사본 저장, 복구 확인 이후 기본 프로젝트 진입, 플랫폼 컴파일 경계와 공통 회귀 검사.
- 후속: 휴대폰/태블릿 반응형 화면(2단계), 롱터치·다중 터치·문맥 메뉴(3단계), 외부 파일 선택기·프로젝트 ZIP 전송·중단/재개 보완(4단계), 고급 편집·전체 시험 흐름·실기기 검수(5단계).
- Android 기본 화면은 기존 씬 UI를 사용한다. 모바일 UX가 완성됐다고 보고하거나 Android 실제 설치·키보드·뒤로가기·접근 권한이 검증됐다고 보고하지 않는다.
- 기존 네 제품의 설정·식별자·시작 씬과 Windows Alt+Enter/F11·16:10 크기 복원 기능은 유지한다.
- 질문은 제한시간 없는 일반 채팅으로 진행한다. 필요한 경우 하위 에이전트를 사용할 수 있지만 동일 Unity 프로젝트의 검사·에셋 변경·프로필 전환은 순차 실행한다.
- 커밋·푸시, Player/APK/Addressables 번들 빌드, HTML 매뉴얼 갱신, SDK/플러그인 설치와 외부 서비스 설정은 하지 않는다.

## 제품 설정 계약

| 항목 | Android 레벨툴 |
|---|---|
| 프로필 | `Android Level Editor` |
| 대상 플랫폼 | `BuildTarget.Android` |
| 시작 씬 | `Assets/Scenes/LevelTool.unity` |
| 공통 도구 심볼 | `PRODUCT_LEVEL_EDITOR` |
| 플랫폼 분기 | 기존 Unity `UNITY_ANDROID`, `UNITY_STANDALONE_WIN` |
| 앱 이름 | `Moon Rabbit Level Tool` |
| Android 앱 식별자 | `com.twelvethwarriors.aos.moonrabbits.leveleditor` |
| 출력 위치 설정 | `Builds/Android/LevelTool.apk` — 경로만 설정, 실제 빌드 안 함 |
| 방향 설정 | Android 레벨툴 프로필에서 가로·세로 자동 회전 허용. 반응형 배치는 2단계 |

앱 식별자는 기존 Android 게임 `com.twelvethwarriors.aos.moonrabbits`와 다르다. Windows 레벨툴은 `com.twelvethwarriors.windows.moonrabbits.leveleditor`로 플랫폼도 구분한다. 게임 앱 설정과 전역 PlayerSettings를 Android 레벨툴 설정으로 덮지 않는다.

## A. 제품 선택과 콘텐츠 경계

- [x] `ProductProfiles.cs`에 다섯 번째 제품을 추가하고 프로필을 기존 생성/갱신 경로로 만든다. 기존 프로필 GUID를 보존한다.
- [x] `ValidateSelection`, `Select`, 프로필 재로드 동기화와 호출부를 조사하고 **심볼+플랫폼**으로 정확히 하나의 제품을 찾도록 수정한다. 기존 Windows 레벨툴 선택 메뉴는 계속 Windows를 선택하고 Android 레벨툴 메뉴는 Android를 선택해야 한다.
- [x] 공유 도구 심볼을 Android·Windows에 쓰더라도 제품 심볼을 전역 PlayerSettings에 추가하지 않는다. 잘못된 심볼 조합·잘못된 시작 씬은 기존과 같이 거절한다.
- [x] Android용 방향·식별자 설정과 Windows용 창 설정을 대상별로 적용한다. `IsTool`만으로 Android에 Windows 창 설정을 적용하지 않는다.
- [x] Addressables의 출력/로드 경로뿐 아니라 Library·카탈로그 초기화·content-state·보고서 경로에도 제품/대상 식별을 반영한다. Android/Windows 레벨툴 사이에서 결과가 섞이지 않아야 한다. 새 프로필은 기존 콘텐츠 그룹을 참조하며 그룹을 복제하지 않는다.
- [x] `Level Tool UI` 포함은 두 레벨툴 제품에서만 활성화하고 Android 게임·iOS 게임·Steam 게임에서 제외한다. 프로필 선택·도메인 재로드·빌드 전 감사가 같은 제품 판정을 사용한다.

검증: 두 플랫폼의 `PRODUCT_LEVEL_EDITOR` 선택, 게임/툴 구분, 다섯 제품의 경로 분리, 기존 제품 회귀, 프로필별 설정과 전역 설정 보존. 실제 콘텐츠 번들을 만들지 않는다.

## B. 앱 내부 프로젝트와 첫 진입

- [x] Android 작업 루트를 `Application.persistentDataPath/LevelTool/Projects`로 제한한다. Windows 기존 작업 폴더 선택은 유지한다.
- [x] Android의 새 프로젝트·열기·사본 저장에서는 드라이브 목록이나 PC 절대 경로를 요구하지 않는다. 내부 프로젝트 목록과 새 프로젝트 이름을 기존 모달에 연결하는 최소 UI만 제공한다. 2단계 화면 디자인을 선행하지 않는다.
- [x] 프로젝트 이름의 경로 구분자·상위 경로 이동·중복 이름을 검사한다. 같은 이름이 있으면 덮어쓰지 않고 이유를 표시한다. 저장 및 복구 루트와 실제 프로젝트 폴더를 섞지 않는다.
- [x] 새 프로젝트는 `LevelToolDocuments.NewProject()`와 `Workspace.Create`를 재사용한다. 레벨·정의는 공통 JSON이며 기본 정의는 기존 런타임 경로로 보충한다. SO 에셋을 편집 저장소로 사용하지 않는다. 기존 변환 과정의 임시 SO 객체를 없애는 리팩토링은 하지 않는다.
- [x] Android 최초 진입은 복구 확인을 끝낸 뒤에만 내부 `Default` 프로젝트를 연다. 정상 `project.json`이 없고 폴더도 비어 있을 때 새로 만들며, 기존 정상 프로젝트는 다시 생성하지 않는다. 부분 생성·손상 파일이 있으면 안내하고 자동 덮어쓰기하지 않는다.
- [x] 기존 세션, 미저장 복구 선택, 명시적인 인계 작업은 기본 프로젝트보다 우선한다. 재활성화 시 메모리의 편집본·선택·Undo 이력을 기본 프로젝트로 교체하지 않는다.
- [x] 내부 프로젝트를 저장하고 같은 폴더를 다시 열어 데이터·ID·참조가 유지되는 것을 확인한다. 미저장 상태의 열기/새 프로젝트/사본 저장은 기존 저장·버리기·취소 정책을 따른다.

검증: 빈 루트, 최초 생성, 재열기, 두 번째 프로젝트, 사본 저장, 중복 이름·경로 탈출·손상 JSON·저장 실패·복구 선택과 인계 우선순위. 테스트는 임시 폴더를 사용하고 실제 제작 원본을 변경하지 않는다.

## C. 런타임·검사·보고 마감

- [x] Windows 네이티브 창 제어는 Android에서 실행되지 않아야 한다. 창 모드 메뉴는 Windows 실행 파일에만 적용하며 Android에서는 불필요한 창 기능을 일반 명령처럼 제공하지 않는다.
- [x] Android 실행은 UnityEditor/PC 제작 원본 경로/EditorWindow 인계를 필수로 요구하지 않는다. 실행에 필요한 기본 템플릿·정의·UI는 런타임 경계에서 제공한다.
- [x] 동일한 내부 프로젝트로 일반 블록·2×2 장애물 데이터를 공통 명령으로 변경하고 저장→재열기 결과를 확인한다. JSON으로 공통 게임 시험 요청을 생성해 레벨 데이터가 전달되는 것까지 검사한다.
- [x] Android 타깃 컴파일이 가능한 모듈인지 먼저 확인한다. 설치되어 있으면 Android 타깃의 스크립트 컴파일만 검사하고 APK는 생성하지 않는다. 모듈이 없으면 임의 설치하지 않고 미검증 조건을 보고한다.
- [x] 검사 중 변경한 활성 프로필·Addressables 선택·출력 경로·임시 테스트 연결을 복원하고, Windows 기존 메뉴·창 설정·내부 편집 검사의 회귀 근거를 남긴다.
- [x] 완료된 항목과 미검증 항목을 구분해 보고한다. 완료 후 2단계 상세 계획·목표·복사용 목표 실행문을 작성하고 자동 구현하지 않는다. 실제 변경에 대한 짧은 커밋 메시지만 마지막에 코드 블록으로 제공한다.

## 수정 위치와 책임

| 위치 | 책임 |
|---|---|
| `Assets/Scripts/Features/Products/Editor/ProductProfiles.cs` | 제품 정의·대상 식별·프로필별 설정·선택 메뉴 |
| `Assets/Scripts/Features/Products/Editor/ProductProfiles.ContentPaths.cs` | 프로필 재로드와 제품/플랫폼별 콘텐츠 경로 |
| `Assets/Scripts/Features/Products/Editor/ProductProfiles.Audit.cs` | 다섯 제품과 게임/툴 의존 경계 감사 |
| `Assets/Settings/BuildProfiles/Android Level Editor.asset` 및 `.meta` | 신규 제품 프로필, 기존 프로필과 GUID 독립 |
| `Assets/Scripts/Features/LevelTool/Runtime/LevelToolProjectAccess.cs` (신규 제안) | 플랫폼별 내부 작업 루트·프로젝트 목록·안전한 이름 해석 |
| `LevelToolScreen.Storage.cs`, `Handoff.cs`, `Recovery.cs` | 새 파일 접근 진입·복구 우선순위·기본 프로젝트 연결. 기존 공통 세션 재사용 |
| `LevelToolFolderAccess.cs` | Windows 폴더 탐색 유지, Android에서 드라이브 탐색 차단 |
| `LevelToolScreen.Commands.cs`, `ShortcutHelp.cs` | 플랫폼에 맞는 명령 노출과 실제 지원 범위 안내 |
| `Assets/Scripts/Features/LevelTool/Editor/LevelToolAtlasBuild.cs` | 기존 포함 판정이 두 도구/세 게임을 구분하는지 확인. 불필요한 아틀라스 재생성 없음 |
| `Tests/Editor/Features/Products/`, `Tests/Editor/Features/LevelAuthoring/`, `Tests/Portable/LevelAuthoring/` | 기존 제품 검사 보완, 내부 프로젝트·플랫폼 경계·저장/재열기 회귀 |

정확한 신규 파일 분리는 구현 전 호출부 조사 결과를 따른다. 범용 플랫폼 프레임워크나 신규 asmdef를 만들지 않으며, 이번 단계에서 편집 규칙·JSON 스키마·게임 규칙을 바꾸지 않는다.

## 검증 실행과 완료 판정

- 기존 `Tools/Testing/ProjectTests.ps1`로 필요한 Unity 메서드를 **순차** 실행한다. 테스트는 외부 `Tests`에 두고 실행 시에만 임시 연결한다.
- `ProductBoundaryVerification.Run`, `ProductSelectionVerification.Run`, `LevelToolWindowProfileVerification.Run`, `LevelToolWindowVerification.Run`, `LevelToolShortcutVerification.Run`의 실제 namespace와 진입점을 실행 전에 확인한다. 파일명만으로 검사가 작동한다고 가정하지 않는다.
- 신규 내부 프로젝트 검사는 앱 폴더 정책을 임시 루트로 검증하고 생성→수정→저장→재열기의 문서 의미와 참조를 비교한다. 모바일 터치·실기기 검사를 흉내 낸 로그로 대체하지 않는다.
- 필요하면 순수 JSON 검사를 실행하되 검사 도구가 실제 APK/번들 빌드를 호출하지 않는지 확인한다.
- Android 모듈이 없거나 Android 컴파일이 실패하면 해당 완료 조건은 미완료다. Windows/순수 데이터 검사만 통과한 경우 결과를 그 범위로 제한한다.
- 위 항목의 성공 근거를 `Docs/Verification/project-wide/android-level-tool-stage-01-progress.md`에 정리한다. 빌드·실기기 검증이 없다는 점은 별도로 기록한다.

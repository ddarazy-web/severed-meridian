# Android 레벨툴 1단계 — 구현·검증 기록

2026-10-11 · 1단계 구현·자동 검사 완료. APK·실기기 검증은 수행하지 않음.

## 구현 범위

- Android Level Editor 제품 정의와 전용 프로필 생성 경로를 추가했다. 제품은 공통 도구 심볼과 대상 플랫폼으로 선택한다.
- Addressables의 출력·로드·Library·초기화/카탈로그·content-state·보고서 경로에 플랫폼을 포함했다. 도구 UI 포함 감사도 제품별로 판단한다.
- Android 파일 작업은 앱 내부 프로젝트 목록·이름 입력으로 연결했다. Windows 폴더 선택은 유지한다.
- 내부 프로젝트 이름 검증, 중복 보호, 정상 기본 프로젝트 재열기와 부분/손상 기본 프로젝트 보호를 추가했다.
- 복구 확인 뒤 빈 세션에서만 기본 프로젝트를 연다. 기존 세션·명시적 인계·복구 선택을 우선한다.
- Android에서 Windows 창 모드 메뉴와 관련 도움말을 제외했다.

## 검증 진행

- 변경 diff 공백 검사 통과.
- Android 모듈 폴더 존재 확인. 실제 지원 판정과 플레이어 스크립트 컴파일은 Unity 검사 결과로 판단한다.
- 최초 검사 `Logs/TestHarness/20261011-014952-907-Run.log`에서 새 검사 코드의 `ReadOnlyCollection.Length` 컴파일 오류를 확인했다. `Count`로 수정했다. 이 실행은 성공 근거로 사용하지 않는다.
- 컴파일 중단 후 남은 테스트 연결은 소유 기록과 폴더 경로를 확인한 뒤 생성된 폴더 메타 형식을 보정하고 정상 해제했다. 제작 원본은 삭제하지 않았다.
- `Products.Editor.AndroidLevelToolProfileVerification.Run` 통과(exit 0): `Logs/TestHarness/20261011-015156-938-Run.log`. 새 프로필·GUID·앱 이름/식별자·자동 회전, 2회 구성 갱신, 기존 네 프로필/meta 및 전역 설정 byte 보존, 콘텐츠 그룹 재사용과 현재 선택 보존을 확인했다.
- 내부 저장 첫 실행은 빈 새 레벨의 미션 미설정으로 게임 요청 검증이 거절됐다(`Logs/TestHarness/20261011-015242-388-Run.log`). 테스트 입력에 공통 미션 편집 명령으로 유효 미션을 지정했다. 기본 템플릿은 변경하지 않았다.
- `LevelAuthoring.Editor.LevelToolProjectAccessVerification.Run` 통과(exit 0): `Logs/TestHarness/20261011-015410-342-Run.log`. 내부 이름·중복·부분/손상 기본 프로젝트 보호, 일반 블록·2×2 배치 저장/재열기·사본 재열기·게임 요청의 ID/참조/좌표/내구도, 취소·쓰기 실패·현재 세션/선택/Undo/Redo 보존을 확인했다.
- `LevelAuthoring.Editor.AndroidLevelToolScreenVerification.Run` 통과(exit 0): `Logs/TestHarness/20261011-015448-191-Run.log`. 실제 PlayMode 복구 모달, 기본 진입 우선순위, 내부 목록/이름/취소 콜백, 미저장 전환 취소, 재활성화의 메모리 세션/선택/편집 층/Undo/Redo 보존을 확인했다.
- 제품 경계 감사 첫 실행은 기존 Windows 네이티브 어댑터의 복합 전처리 조건을 기존 감사기가 인식하지 못해 실패했다(`Logs/TestHarness/20261011-015513-828-Run.log`). 감사가 정확한 공통 경계 또는 공통 경계+Windows 제한 첫 줄만 인정하도록 보완했다. 플랫폼 제한 자체는 변경하지 않았다.
- 제품 경계 감사 재실행 및 제품 선택·Windows 회귀·Android 플레이어 스크립트 검사가 모두 통과했다. 아래 표는 최종 성공 실행이다.

| 검사 | 최종 로그 (`Logs/TestHarness/`) | 확인한 범위 |
|---|---|---|
| AndroidLevelToolProfileVerification | `20261011-015156-938-Run.log` | 신규 프로필 구성, 기존 네 프로필/meta·전역 설정 보존 |
| LevelToolProjectAccessVerification | `20261011-015410-342-Run.log` | 내부 프로젝트 생성/열기/저장/사본, 기본 재열기, 실패 보호, JSON→게임 요청 |
| AndroidLevelToolScreenVerification | `20261011-015448-191-Run.log` | 내부 선택 UI·취소·복구·인계 우선순위·재활성화 상태 보존 |
| ProductBoundaryVerification | `20261011-015647-832-Run.log` | 다섯 제품의 프로필/시작 씬/자산 의존 경계와 두 플랫폼의 도구 식별 |
| ProductSelectionVerification | `20261011-015720-464-Run.log` | 현재 Windows 타깃 제품 실제 선택, 다섯 콘텐츠 루트, 동기화 함수·UI 그룹 포함, 이전 선택 복원 |
| LevelToolWindowProfileVerification | `20261011-015746-419-Run.log` | 기존 Windows 창 프로필 계약·전역 설정 보존 |
| LevelToolWindowVerification | `20261011-015812-720-Run.log` | 최소 크기/16:10·저장 크기 복원·전체 화면 정책·Editor 격리 |
| LevelToolShortcutVerification | `20261011-015835-480-Run.log` | 기존 공통 명령·Alt+Enter/F11·입력 문맥 경계 |
| AndroidLevelToolCompilationVerification | `20261011-015857-962-Run.log` | Android Player 조건+도구 심볼의 38개 스크립트 어셈블리, Editor 검사 어셈블리 제외·기존 선택 보존 |
| LevelToolViewportVerification | `20261011-020005-372-Run.log` | Windows 기존 보드/속성/폴더 모달과 상태 보존, 대표 화면 크기 회귀 |

모두 종료 코드 0과 성공 로그를 확인했고 검사 사이 임시 연결을 해제했다. Android 스크립트 검사는 `PlayerBuildInterface.CompilePlayerScripts`만 호출했다. 컴파일 산출물 목록은 `Logs/AndroidLevelToolStage01/android-player-script-assemblies.txt`다. Player/APK·씬·번들 빌드는 호출하지 않았다.

## 완료 조건별 근거

- 제품/전역 설정/기존 네 제품: 실제 신규 `.asset`/`.meta`와 프로필 보존 검사, 제품 경계 검사. Android 앱 식별자는 게임과 별도다.
- 제품+플랫폼 식별과 콘텐츠 격리: `ProductProfiles.ValidateSelection/Select`, `ContentKey`, 재로드 연결 함수 및 제품별 감사와 위 선택 검사. Android UI/플랫폼 실제 실행을 검증한 것으로 확대하지 않는다.
- 내부 저장과 기본 진입: `LevelToolProjectAccess`, 화면의 Android 파일 분기 및 복구 확인 후 호출, 임시 폴더 JSON 왕복·실패 보호와 화면 우선순위 검사. 공통 JSON 저장소/편집 세션을 재사용한다.
- 원본/ID/참조/Undo 보존: 저장·사본 재열기·게임 요청 비교, 취소/손상/쓰기 실패 검사, PlayMode 재활성화 검사.
- Windows 및 플랫폼 컴파일 경계: Windows 회귀와 Android Player 스크립트 컴파일. 네이티브 어댑터는 Windows 전처리 조건에 한정되며 생성도 Windows Player에만 허용된다.
- 검사 복원·산출물: 모든 검사 정상 종료/연결 해제, 선택 검사 및 컴파일의 이전 설정 보존 검증. 아래 다음 단계 문서는 작성했으나 구현하지 않았다.

## 제한과 후속

APK·Player·Addressables 번들 빌드, 커밋·푸시, HTML 매뉴얼 갱신은 수행하지 않았다. Android 실제 설치·최초 실행·OS 키보드/뒤로가기·파일 접근과 Windows 실제 OS 창 동작은 미검증이다. Android 실제 Build Profile 선택 전환이나 실제 도메인 재로드 실행도 이번 검사의 범위가 아니다. 모바일 화면·터치 편집·외부 파일 전송·실기기 사용 검증은 후속 단계다.

다음 단계: [2단계 계획](../../Planning/project-wide/android-level-tool-stage-02-plan.md), [목표](../../Goals/project-wide/android-level-tool-stage-02-goal.md), [복사용 실행문](../../Commands/project-wide/android-level-tool-stage-02-command.md). 2단계는 태블릿/휴대폰 반응형 화면이며 구현은 자동 시작하지 않았다.

읽기 전용 코드 검토에서는 확정적인 버그를 발견하지 못했다. 화면 검사는 Editor에서 내부 선택 화면과 부트스트랩 우선순위 가드를 검사하므로 실제 Android 플랫폼 최초 실행의 증거로 확대하지 않는다.

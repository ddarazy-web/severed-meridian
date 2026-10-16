# 제품 빌드 도구 1단계 — 구현·검증 기록

2026-10-16 · Unity 6000.3.10f1 / Windows / 브랜치 `work`

[통합 계획](../../Planning/project-wide/2026-10-15-build-tool-plan.md) · [1단계 계획](../../Planning/project-wide/build-tool-stage-01-plan.md) · [목표](../../Goals/project-wide/build-tool-stage-01-goal.md) · [2단계 계획](../../Planning/project-wide/build-tool-stage-02-plan.md)

## 구현 결과

- `Match → Build → 빌드 도구`에서 같은 UI Toolkit 창을 열고 실제 Build Profile 목록·제품 정보·사전 검사 결과를 확인한다. 기존 `Tools/Products` 메뉴는 보존했다.
- 프로필 GUID와 기존 `ProductProfiles.Definitions`를 사용한다. Android/Windows 레벨툴의 공통 심볼은 플랫폼까지 함께 판정한다.
- `BuildToolProfiles`가 프로필·씬·심볼·실제 제품 이름/식별자를 읽는다. 제품 정의를 생성·보정하거나 원본 설정을 저장하지 않는다.
- `BuildToolPreflight`가 공통 제품·Addressables·모듈 상태를 검사하고, 등록된 Android/iOS/Windows 처리기가 플랫폼별 설정 키와 고유 미확인 범위를 제공한다.
- 목록 선택과 활성 프로필을 구분하며, 미확인 SDK·실제 빌드·iOS Xcode/서명·Steam 실행은 완료로 표시하지 않는다. 등록되지 않은 플랫폼·처리기 중복·잘못된 제품 조합은 차단 오류다.
- 읽기 실패 항목도 목록에 남긴다. 개별 에셋 로드 예외가 정상 항목을 제거하지 않는다. 플랫폼 자체가 읽히지 않으면 임의 기본 플랫폼 대신 미확인으로 표시한다.
- 빌드 버튼은 2단계 예정으로 비활성이다. 추가 옵션은 ‘등록된 추가 옵션 없음’이다. 빈 실행기나 옵션용 전후처리 프레임워크는 만들지 않았다.
- 마지막 선택 GUID만 `UserSettings/MatchBuildTool.json`에 기억한다. 기존 `.gitignore`에서 해당 경로가 제외됨을 확인했다. 잘못된 기록·삭제된 GUID는 현재 목록에서 복원한다.

## 파일과 책임

신규 제품 코드는 `Assets/Scripts/Features/Products/Editor/BuildTool/`의 7개 C# 파일과 대응 `.meta`에 한정된다. 공통 조회·검사·창, 플랫폼 등록, 세 플랫폼 처리를 분리했다. 기존 제품 정의·프로필·씬·프리팹·전역 설정·HTML 매뉴얼은 수정하지 않았다.

검사는 외부 `Tests/Editor/Features/Products/BuildToolPreflightVerification.cs`와 `BuildToolWindowVerification.cs`에서 관리한다. 임시 연결은 기존 `Tools/Testing/ProjectTests.ps1`을 사용한다.

## 실행·검증 근거

| 구분 | 명령 / 로그 | 실제 결과 |
|---|---|---|
| 최초 RED | `BuildToolPreflightVerification.Run` / `Logs/TestHarness/20261015-235237-497-Run.log` | 읽기 전용 사전 검사 구현이 없다는 예외를 확인했다. 최초 새 검사 메타가 복사 목록에 없어 연결 해제가 보류됐고, 생성된 메타를 Tests 원본에 보존·동일성 확인 후 자신의 임시 메타만 정리해 정상 해제했다. |
| 최초 공통 검사 GREEN | `Logs/TestHarness/20261015-235848-704-Run.log` | 다섯 제품·심볼/씬/식별자 오류·설정 미생성·처리기 등록·무변경 검사 통과, exit 0. |
| 최초 창 검사 GREEN | `Logs/TestHarness/20261015-235930-337-Run.log` | 메뉴·창 재사용·선택 기억·이름 변경·삭제·축소·무변경 검사 통과, exit 0. |
| 보류 표시 회귀 RED | `Logs/TestHarness/20261016-000502-579-Run.log` | 보류 결과가 완료로 표시됨을 실패 검사로 확인했다. |
| 보완 공통 검사 GREEN | `Logs/TestHarness/20261016-000636-491-Run.log` | 보류 요약 수정, 에셋 로드 예외 격리와 기존 공통 검사 통과, exit 0. |
| 실제 재로드 검사 GREEN | `Logs/TestHarness/20261016-000713-166-Run.log` | `EditorUtility.RequestScriptReload` 이후 GUID 선택·활성 제품/콘텐츠 경로·프로젝트 파일 보존, 빈 목록·오류 표시 검사 통과, exit 0. |
| 최종 공통 검사 | `& ./Tools/Testing/ProjectTests.ps1 -Action Run -Method Products.Editor.BuildToolPreflightVerification.Run` / `Logs/TestHarness/20261016-000820-193-Run.log` | 최종 소스 컴파일 및 공통 검사 통과, exit 0. |
| 최종 창 검사 | `& ./Tools/Testing/ProjectTests.ps1 -Action Run -Method Products.Editor.BuildToolWindowVerification.Run` / `Logs/TestHarness/20261016-000920-746-Run.log` | 메뉴·빈 목록·오류 표시·선택 기억·에셋 이름 변경/삭제·폭 축소·스크롤·실제 스크립트 재로드·설정 보존 검사 통과, exit 0. 임시 테스트 연결 해제 완료. |

### 검사 범위

1. 실제 다섯 프로필의 GUID·제품·씬·실제 앱 이름/식별자를 확인했다. 모듈 지원 여부는 현재 Unity의 조회 결과와 비교하고, 비활성 플랫폼은 전환하지 않고 미확인으로 남긴다.
2. 임시 객체로 미등록·여러 제품 심볼·플랫폼 불일치·잘못된 씬·누락 PlayerSettings를 확인했다. 누락 설정은 검사 후에도 생성되지 않았다. 식별자 불일치는 분리된 조회 데이터로 검사했다.
3. 로더 예외 주입으로 한 항목의 실패가 나머지 항목을 제거하지 않음을 확인했다. 실제 원본 에셋을 손상시키지 않았다.
4. 등록 누락·중복, 같은 플랫폼의 게임/툴이 같은 처리기 인스턴스를 사용하는 것을 확인했다.
5. 창·메뉴 재사용, 실제 목록 선택, 잘못된 로컬 JSON, 선택 기억·창 재생성, 임시 복제 에셋의 이름 변경·삭제, 빈 목록과 오류 항목 UI를 확인했다.
6. 폭 640의 창에서 상세 영역이 화면 안에 있으며 긴 검사 결과를 스크롤할 수 있음을 확인했다. 실제 스크립트 재로드 후 선택과 설정 파일도 확인한다.

### 무변경 근거의 정확한 범위

- 파일 전체 해시: `Assets/Settings/BuildProfiles`, `Assets/AddressableAssetsData`, `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/EditorBuildSettings.asset`.
- 메모리·전역 조회: 활성 프로필·대상 플랫폼·제품별 출력 위치·전역 제품 심볼, 각 프로필과 해당 PlayerSettings 전체 직렬화, Addressables 설정·활성 프로필·Library/보고서/content-state 경로·도구 UI 그룹 직렬화.
- 전역 PlayerSettings는 파일 전체 해시와 전역 심볼 조회를 비교했다. 전역 PlayerSettings의 모든 메모리 필드까지 비교했다고 주장하지 않는다.
- 창 검사는 고유 이름의 임시 복제 프로필만 생성·이름 변경·삭제하고 원본 파일이 돌아왔음을 검사한다. 실제 스크립트 재로드 검사를 위해 사용한 개인 선택 기록은 이전 내용으로 복원한다.
- 실제 SDK/Player/Addressables 빌드는 호출하지 않는다. `BuildPipeline.BuildPlayer`, 프로필/플랫폼 전환, `Configure`/`Select`/콘텐츠 동기화·설정 저장 API가 신규 제품 코드에 없는지 함께 확인한다.

## 검토·판정 기록

- 기준 HEAD: `dfcb2bcdb822883d2fb963c8c40dee5a04aac037`. 커밋하지 않았으므로 신규 파일을 직접 검토했다.
- `executing-plans`의 최종 검토 요구에 따라 별도 읽기 전용 검토를 수행했다. 검토자는 에디터 실행·소스 수정·빌드·커밋을 하지 않았다.
- Important 1: 보류 결과의 완료 오표시 → 실패 검사 확인 후 보류 요약으로 수정하고 GREEN 확인.
- Important 2: 개별 로더 예외가 전체 목록으로 전파 → 항목별 로드 경계를 두고 주입한 로더 예외에서도 정상 항목을 유지하는 검사로 확인.
- 읽기 실패 프로필의 null 비교로 활성 프로필을 잘못 표시할 수 있는 부분도 null 제외 조건으로 수정했다. 읽히지 않은 플랫폼에는 `NoTarget`을 사용한다.
- 검토 중 지적된 빈 목록·오류 UI·실제 재로드·스크롤의 검사 누락은 창 검사에 추가했다. 전역 메모리 비교 범위는 위와 같이 좁혀 정확하게 보고한다.
- Ruling: 사용자 금지에 따라 스킬의 자동 커밋·브랜치 마무리 절차는 적용하지 않고 현재 `work` 작업 경로와 미커밋 변경을 유지한다. 비용: 커밋 간 diff 대신 현재 파일과 검사 로그로 검토해야 한다.
- Ruling: 에셋 로드 실패를 원본 손상 없이 검사하기 위해 내부 조회 경계에 로더를 전달할 수 있게 했다. 운영 경로는 항상 실제 AssetDatabase 로더를 사용한다. 비용: 내부 검사 연결용 매개변수 하나가 추가된다.

## 미검증·후속 범위

실제 Player/APK/AAB/iOS 출력·Addressables 번들 생성, 플랫폼 전환 후 컴파일, SDK·서명·스토어 연결·실기 실행은 미수행이다. Play Mode에 들어가 화면을 직접 조작하는 수동 시나리오는 실행하지 않았고, 보류 상태의 요약은 회귀 검사와 조건부 경로 검토로 확인했다. 실제 플랫폼별 실행·옵션 적용은 각각 2·3단계다.

HTML 매뉴얼·기존 씬/프리팹·프로필·SDK 패키지를 변경하지 않았고 커밋·푸시·빌드를 수행하지 않았다. 다음 단계 문서만 작성하며 구현은 자동 시작하지 않는다.

## 최종 정리

최종 두 실행 모두 exit 0이며 테스트 연결은 해제됐다. Git 변경은 신규 도구·검사·계획/목표/명령/검증 문서로 한정되고 기존 프로필·설정·HTML은 변경되지 않았다. 최종 창 검사 종료 로그에 Unity 내부 JobTempAlloc 경고가 남았으며 원인은 이번 범위에서 확정하지 않았다. 제품 코드에는 네이티브 할당·잡 실행을 추가하지 않았고 기능·무변경 검사는 통과했다. 실제 제품 빌드 검증으로 확대하지 않았다.

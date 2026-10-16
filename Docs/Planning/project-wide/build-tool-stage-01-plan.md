# 제품 빌드 도구 1단계 — 조회·사전 검사 구현 계획

> 2026-10-15 최초 계획 · 2026-10-16 구현·자동 검사 완료
> 구현 담당: `superpowers:executing-plans`로 아래 세 묶음을 순차 진행한다. 실제 제품 빌드·번들 생성·커밋·푸시는 금지한다.

**Goal:** `Match → Build → 빌드 도구`에서 프로젝트의 Build Profile과 제품 정보를 조회하고, 플랫폼별 준비 상태와 오류를 설정 변경 없이 확인한다.

**Architecture:** UI → 프로필 조회·공통 사전 검사 → 등록된 플랫폼 처리기로 이어지는 읽기 전용 흐름을 만든다. 기존 `ProductProfiles`를 제품 정보 원본으로 사용한다. 실행기와 옵션 수명주기는 구현하지 않는다.

**Tech Stack:** Unity 6000.3.10f1 / Editor UI Toolkit / Build Profiles / 외부 `Tests/Editor` 검사 구조.

**Spec:** [통합 계획](2026-10-15-build-tool-plan.md) · [목표·완료 조건](../../Goals/project-wide/build-tool-stage-01-goal.md) · [실행 명령문](../../Commands/project-wide/build-tool-stage-01-command.md)

## 이번 문서 정리와 기존 구현의 관계

2026-10-16 문서 정리: 아래 계획과 완료 체크는 기존 1단계 구현 기록을 보존한다. 현재 작업 폴더에는 후속 실행 코드도 있으므로, 이 계획을 다시 실행할 때는 먼저 이미 충족한 항목과 누락을 구분한다. 후속 단계의 실행 버튼·복구 코드를 제거하거나 기존 소스를 원복하지 않는다. 이번 요청에서는 문서만 수정하며 소스·프로필·씬·프리팹을 변경하지 않는다.

전체 3단계 중 이번 단계의 산출물은 **설정을 바꾸지 않는 조회·사전 검사 화면**이다. GPG/플러그인 옵션은 3단계에 등록 방식으로 연결하며, 1단계에 비어 있는 전후처리 계층을 미리 구현하지 않는다. 실제 구현 시에는 아래 세 작업 묶음을 각각 검증하고 한 번의 단계 완료 보고로 정리한다.

## 1. 단계 범위

포함: 신규 에디터 창·메뉴, 프로젝트 프로필 발견, 선택 정보, 읽기 전용 검사, 플랫폼 처리기 등록, 문제 안내, 마지막 선택 기억, 관련 검사.

제외: 프로필 활성화, 플랫폼 전환, 설정 자동 갱신, 실행 요청 재개 시스템, 실제 빌드, 옵션 적용·전후처리, SDK/패키지 설치, HTML 매뉴얼 갱신. ‘빌드’는 비활성 표시와 다음 단계 안내만 제공한다. 조회 단계에 비활성 실행기·옵션용 빈 메서드를 미리 만들지 않는다.

## 2. 구현 전 확인·보존

- 작업 시작 시 git 상태와 현재 소스를 대조하고 기존 변경을 보존한다. 아래 이름은 구현 기준이며 이미 같은 책임의 코드가 있다면 재사용하고 차이를 보고한다.
- Unity 버전·활성 프로필/플랫폼·Addressables 선택·Library/보고서/content-state 경로·도구 UI 그룹 포함·프로필 및 전역 PlayerSettings 파일 상태를 확인한다.
- `ProductProfiles.ValidateProfile`와 `ProfileSettings(profile, false)` 같은 조회 경로를 재사용한다. 실제 조회 중 내부 API가 현재 Unity에서 읽히지 않으면 항목 오류로 남긴다. 자동 생성·추정값으로 보충하지 않는다.
- `Configure`, `Select`, `SynchronizeContentPaths`, `ApplyContentPaths`, `PrepareContentBuild`, 빌드 API를 조회·선택·검사의 호출 경로에 연결하지 않는다.
- 일반 전역 상태 ‘프로젝트 준비됨’과 실제 플랫폼 빌드 가능 여부를 동일시하지 않는다. 현재 대상이 아니라 확인할 수 없는 항목은 ‘미확인’으로 둔다.

## 3. 파일과 책임

생성 위치는 `Assets/Scripts/Features/Products/Editor/BuildTool/` 아래로 모은다. 런타임 씬·프리팹이나 새 asmdef는 만들지 않는다. 신규 에셋의 `.meta`는 함께 관리한다.

| 예정 파일 | 책임 |
|---|---|
| `MatchBuildToolWindow.cs` | `Match/Build/빌드 도구` 진입, 목록·선택 정보·검사 결과·미지원 안내 표시 |
| `BuildToolProfiles.cs` | Build Profile 발견과 GUID 식별, 기존 제품 정의 연결, 읽기 전용 표시 정보 |
| `BuildToolPreflight.cs` | 공통 검사·문제 결과 모델과 플랫폼 검사 조정 |
| `Platforms/BuildToolPlatforms.cs` | 플랫폼 조회 계약·명시적 등록표. 플랫폼별 switch 없음 |
| `Platforms/AndroidBuildPlatform.cs` | Android 고유 조회·지원 상태 검사 |
| `Platforms/IosBuildPlatform.cs` | iOS 고유 조회·호스트/후속 검증 범위 안내 |
| `Platforms/WindowsBuildPlatform.cs` | Windows 대상 조회·지원 상태 검사. Steam 게임·레벨툴 공용 |
| `Tests/Editor/Features/Products/BuildToolPreflightVerification.cs` | 임시 프로필·검사 데이터로 판정과 원본 보존 확인 |
| `Tests/Editor/Features/Products/BuildToolWindowVerification.cs` | 메뉴·목록·선택·갱신·오류 표시와 실행 불가 확인 |

타입은 기능 단위 책임으로 묶는다. 작은 값 타입마다 파일을 만들거나 파일 수를 맞추기 위해 의미 없는 계층을 늘리지 않는다. `ProductProfiles*.cs`는 필요한 읽기 진입 보완이 있을 때만 최소 변경한다.

### 연결 계약

- `BuildToolProfiles`는 프로필 GUID, 경로, 에셋, 대상 플랫폼, 연결된 `ProductDefinition`, 읽기 오류를 가진 항목을 반환한다. 에셋 참조를 사용자 설정에 직렬화하지 않는다.
- `BuildToolPreflight`는 선택 항목의 공통 검사와 등록된 플랫폼 처리기 검사를 합쳐 문제 목록을 반환한다. 문제에는 수준·위치·설명·사용자가 할 다음 행동을 담는다.
- 플랫폼 처리기는 조회 계약만 제공한다. 플랫폼별 구현을 `BuildTarget` 키로 등록하고, 등록 누락·중복을 명확한 문제로 표시한다. 대상 조회 이후의 공통 실행 코드에서 플랫폼 switch를 만들지 않는다.
- 조회 결과는 UI가 렌더링한다. 플랫폼 처리기가 UI·팝업을 직접 만들거나 제품 프로필 값을 수정하지 않는다.
- 플랫폼 모듈 유무는 현재 설치 상태로 검사하되 설치하지 않는다. 활성 플랫폼과 다르면 후속 컴파일·SDK 검증이 필요함을 표시한다.

## 4. 작업 묶음 A — 프로필 조회·검사

- [x] 프로젝트의 Build Profile 에셋을 발견해 목록화한다. 제품 연결은 기존 정의의 심볼+플랫폼 계약으로 판단하고 프로필 자체의 GUID는 따로 유지한다.
- [x] 다섯 제품, 복제 프로필, 미등록 심볼, 복수 제품 심볼, 제품/플랫폼 불일치, 시작 씬 오류, 프로필 PlayerSettings 누락·식별자 불일치를 구분한다.
- [x] 손상·미등록 항목 하나 때문에 전체 조회를 중단하지 않는다. 읽히는 항목은 계속 제공하고 오류 항목은 문제와 실제 경로를 표시한다.
- [x] 공통 검사와 Android/iOS/Windows 처리기를 등록 방식으로 연결한다. 공통 지원 모듈 조회 등 실제 공통 부분만 공유한다.
- [x] 상태는 ‘확인한 항목 통과 / 차단 오류 / 주의 / 미확인’을 구분한다. 데이터가 올바르다는 이유로 미설치 모듈이나 SDK까지 준비 완료로 표시하지 않는다.
- [x] 출력 경로는 기존 `ProductDefinition.Output`을 표시하고 변경하지 않는다. 필수 제품 심볼·씬·앱 식별자도 읽기 전용이다.

검증: 임시 객체·임시 검사 입력으로 정상/오류/등록 누락을 확인한다. 두 레벨툴이 같은 심볼이어도 플랫폼별 처리기로 연결되는지 확인한다. 대상 프로필이 비활성이라는 이유만으로 잘못된 제품이라고 처리하지 않는다.

## 5. 작업 묶음 B — 창·메뉴·기억

- [x] UI Toolkit 창을 `Match → Build → 빌드 도구`에서 열고 이미 열려 있으면 같은 창을 표시한다. 기존 `Tools/Products` 메뉴는 보존한다.
- [x] 목록/상세/검사/실행 예정 영역을 분리한다. 대상 플랫폼, 식별자, 씬, 제품 심볼, 기본 출력, 현재 활성 여부를 표시한다.
- [x] 새로고침과 선택 항목 사전 검사 버튼을 제공한다. 프로필 에셋 삭제·이름 변경·재로드 후 오래된 선택을 사용하지 않게 GUID로 다시 확인한다.
- [x] 선택한 프로필과 활성 프로필이 다름을 설명하고, 선택·검사만으로 전환하지 않는다. Play Mode·컴파일 중처럼 확인을 미뤄야 하는 상태는 이유를 표시하고 잘못된 준비 완료를 보여주지 않는다.
- [x] 빌드 버튼은 비활성으로 두고 ‘2단계에서 실행 연결 예정’을 표시한다. 옵션은 ‘등록된 추가 옵션 없음’으로 표시하며 가짜 체크박스를 만들지 않는다.
- [x] 마지막 선택 GUID를 Git 제외 로컬 사용자 설정에만 기억한다. 파일이 없거나 잘못됐으면 안전한 기본 선택으로 열고 원본 프로필을 수정하지 않는다. 아직 없는 옵션 저장 체계를 미리 구현하지 않는다.

검증: 빈 목록·오류 항목·삭제된 선택·창 재열기·폭 축소·스크롤에서 사용자가 이유와 다음 행동을 확인할 수 있는지 검사한다. 개인 로컬 설정 기록 외에 프로젝트 파일 변경이 없어야 한다.

## 6. 작업 묶음 C — 무변경 검증·마감

- [x] 새 검사는 `Tests/Editor/Features/Products`에서 관리하고 기존 `Tools/Testing/ProjectTests.ps1`의 임시 연결을 사용한다. 테스트를 제품 소스나 배포 어셈블리에 상시 넣지 않는다.
- [x] 조회·목록 선택·새로고침·사전 검사 전후의 프로필 에셋, 전역 PlayerSettings, 활성 프로필·플랫폼, Addressables 설정/경로와 도구 그룹 포함 상태를 대조한다. 검사 중 자동 동기화로 생긴 변경도 숨기지 않는다.
- [x] 원본을 손상시켜 오류를 재현하지 않는다. 임시 객체·전용 임시 파일을 사용하고 자신의 검사 자료만 정리한다.
- [x] `ProductBoundaryVerification.Run`의 `Configure` 호출 및 기존 선택 검사의 상태 변경을 주의한다. 이 단계에서는 읽기 검사를 새로 구성하거나 필요한 부분만 검증하고, 광범위한 설정 갱신 검사를 무조건 실행하지 않는다.
- [x] Unity 컴파일과 위 신규 검사를 수행한다. 에디터가 열려 있으면 임의 종료하지 않으며 동일 프로젝트의 Unity 작업은 순차 실행한다. 필요한 확인은 일반 채팅으로 요청한다.
- [x] 변경·검사·미검증·실행한 명령을 `Docs/Verification/project-wide/build-tool-stage-01-progress.md`에 기록한다. 실행하지 않은 검사는 완료 표시하지 않는다.
- [x] 2단계 상세 계획·목표·복사용 실행문을 작성하고 실제 구현을 자동 시작하지 않는다. 실제 구현 완료에 대한 커밋 메시지만 마지막에 코드 블록으로 제공한다.

예정 검사 명령(메서드 구현 후 사용, Player/Addressables 빌드 명령이 아님):

```powershell
& ./Tools/Testing/ProjectTests.ps1 -Action Run -Method Products.Editor.BuildToolPreflightVerification.Run
& ./Tools/Testing/ProjectTests.ps1 -Action Run -Method Products.Editor.BuildToolWindowVerification.Run
```

## 7. 단계 완료의 의미

이번 완료는 **설정을 바꾸지 않는 빌드 준비 화면**이 완성됐다는 뜻이다. 제품 산출물 생성, Android SDK 동작, iOS 서명, Steam 출시, GPG 포함/제외 또는 플러그인 전후처리 검증 완료를 뜻하지 않는다. 이들을 완료라고 보고하지 않는다.

## 8. 완료 기록

2026-10-16 구현과 최종 공통·창 검사를 통과했다. [검증 기록](../../Verification/project-wide/build-tool-stage-01-progress.md)과 [2단계 계획](build-tool-stage-02-plan.md)을 확인한다. 2단계는 문서만 작성했으며 구현하지 않았다.

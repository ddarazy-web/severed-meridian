# 5단계 통합 검증과 마무리 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:executing-plans`로 네 작업을 순서대로 직접 실행한다. 기존의 직접 실행 방식을 유지한다. 체크박스는 실제 증거 확보 후 갱신한다.

**Goal:** 선택 맵 플레이부터 실제 번들·Android 개발 빌드까지 검증하고 재현되는 결함만 수정하여 인계한다.

**Architecture:** 기존 세션·입력·UI·MemoryPack·Addressables 경계를 유지한다. Editor 검증 코드는 GameScreen의 기존 Tests 영역에서 재사용한다. 빌드 검증은 명시적 씬 목록과 결과 보고서를 사용하며 실행 환경/설정 복원을 책임진다.

**Tech Stack:** Unity 6000.3.10f1, C#, uGUI, Input System, UniTask, MemoryPack, Addressables. 설치된 프로젝트 패키지 버전을 실행 시 확인하고 신규 의존성을 추가하지 않는다.

**Spec:** [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-05-integration-goal.md). 상태: 구현·통합 검증 완료 (2026-10-01).

## Global Constraints

- `C:/Projects/Git/ServeredMeridian`만 사용한다. 자동 커밋하지 않는다. 정상인 1~4단계 및 기존 변경을 유지한다.
- 50레벨 MemoryPack, 분할 Addressables, UI/Game 기능별 프리팹, .meta/GUID를 보존한다.
- 같은 프로젝트의 Editor가 열려 있으면 두 번째 Unity 프로세스를 실행하지 않는다. Windows에서는 CIM의 프로젝트 경로/PID로 확인하며 명령줄의 토큰을 출력하지 않는다.
- 미저장 씬/레벨을 자동 저장하거나 Editor를 임의 강제 종료하지 않는다. 배치 종료용 검사는 열린 Editor에서 종료하지 않는 경로로 실행한다.
- 빌드 전처리의 팩/설정 변경을 예상하고 기준 해시/설정을 보존한다. 파일 충돌은 덮어쓰지 않는다. 원본 레벨이 dirty면 그 상태를 저장해 빌드하지 않는다.
- 이번 문서 작성은 실행 승인이 아니다. 실행 명령을 받은 뒤 검사·필요 수정·로컬 Android 빌드를 진행한다. 기기 설치·상용 서명·업로드는 자동 수행하지 않는다.

## Review Focus

1. 빠른 재생에서는 보이나 실제 번들에는 이미지/팩이 없음 → 작업 2의 번들 공급자·콘텐츠 검사.
2. 비활성 빌드 씬 목록 때문에 원본 배제 검사가 실제 빌드 씬을 놓침 → 작업 3의 씬 명시 및 빌드 결과 대조.
3. 전처리가 미저장 레벨·기존 팩·사용자 Addressables 설정을 덮어씀 → 작업 2·3의 해시/dirty/설정 전후 비교.
4. 재시작·씬 종료 경합으로 핸들/사본이 누적되거나 선택 레벨이 기본값으로 바뀜 → 작업 1의 두 소스·반복/취소 검사.
5. Editor 화면/가상 터치를 기기 검증으로 오인 → 작업 4의 실행 환경별 증거와 미검증 표.

## 작업 1 — 사용자 흐름과 수명 회귀

**Files:** 기존 `Assets/Scripts/Features/GameScreen/Editor/Tests/`의 PuzzleUIInteractionVerification, PuzzleUIPlaybackVerification, PuzzleGameplayVerification, PuzzleBoardInputVerification, PuzzleGameSceneVerification, PuzzleEditorLaunchVerification, PuzzleEditorLaunchLifecycleVerification. 편집 이미지 검사는 `Assets/Scripts/Features/LevelEditor/Editor/Tests/BoardArtworkLifecycleVerification.cs`.

**Interfaces:** 기존 `Run()` 및 `PuzzleEditorLaunchVerification.RunData()`를 소비한다. 로그 위치와 실행 환경을 `Logs/PuzzleIntegrationVerification/`의 요약에 연결한다. 기존 진입점/출력 파일 이름은 유지한다.

- [x] 현재 AGENTS·계획·4단계 기록·git diff·실행 Editor를 확인하고 기준 파일 해시와 사용자 설정을 기록한다.
- [x] 기존 검증 진입점의 종료/저장 부작용을 확인하고 현재 Editor에서 안전하게 실행 가능한 경로를 결정한다. 필요한 경우 기존 검사에 `Application.isBatchMode` 종료 분기만 추가한다.
- [x] 두 소스·서로 다른 레벨/시드, 미저장 사본과 팩 차이, 아이템 실패/취소, pause/라스트팡/결과/다시하기/편집 복귀 검사를 실행한다. Expected: 각 결과에 FAIL 0, 원본/dirty 보존.
- [x] 각 소스에서 5회 이상 재시작 및 씬 왕복·로드 중 종료를 검사한다. Expected: 초기 스냅샷/시드 재현, 단일 UI, 구독/사본/소유 핸들 누적 없음.
- [x] 결함이 재현될 때만 관련 Runtime/Editor 파일을 최소 수정한다. 동일 재현 검사 RED→GREEN과 영향받는 회귀 결과를 기록한다. 실패가 없으면 기존 코드를 유지한다.

## 작업 2 — 실제 번들과 MemoryPack 경계

**Files:** `Assets/Scripts/Features/Levels/Editor/Persistence/LevelPackBuild.cs`, `LevelPackAddressablesBuilder.cs`; `Assets/Scripts/Features/Levels/Editor/Tests/LevelPackVerification.cs`; `Assets/Scripts/Features/Board/Editor/BoardAtlasContentBuild.cs`; GameScreen/Editor/Tests의 PuzzleArtworkVerification, PuzzleWorldBoardVerification. 수정은 확인된 결함/안전한 실행 경계에 한한다.

**Interfaces:** `LevelPackBuild.Generate()`, `LevelPackBuild.ValidateExclusion(AddressableAssetSettings settings)`, `LevelPackCodec.FirstLevel(int number)`, `LevelPackLoader.LoadAsync(int number)` 및 기존 검사 `Run()`을 재사용한다. 기존 Addressables 빌더를 사용하고 별도 로더를 만들지 않는다.

- [x] 저장된 원본·생성 팩·Addressables 그룹/프로필/빌더의 기준 상태를 확보하고 Generate 및 콘텐츠 빌드 부작용을 확인한다. 오래된 팩 갱신이 필요하면 변화 이유를 기록한다. 사용자 dirty 데이터를 저장하지 않는다.
- [x] 실제 콘텐츠 빌드를 실행한 뒤 Use Existing Build 경로에서 기존 레벨 팩·아트·월드 검사를 실행한다. Expected: BundledAssetProvider 기반 로드와 실제 Sprite/atlas 확인. 종료용 진입점은 작업 1의 안전 경로를 따른다.
- [x] 1/50/51/100/101, 누락·손상·중복·다른 구간 요청 및 원본 간접 참조 차단을 검사한다. 임시 번호는 기존 파일 부재를 확인하고 사용한다. Expected: 정상 왕복, 오류 요청 거절, 기본 레벨로 묵시적 대체 없음.
- [x] 빌드 레이아웃/카탈로그와 실제 번들 콘텐츠를 검사한다. Expected: MemoryPack TextAsset과 분할 atlas가 존재하고 LevelDefinition 원본은 없음. 단순 Addressables 항목 부재만으로 판정하지 않는다.
- [x] 대표 일반/파워/장애물/덮개/바닥/장치/2×2 및 매칭 생성 파워를 캡처하고 수명 검사를 완료한다. 생성 팩의 의도한 갱신과 무관한 차이를 분리해 기록하고 설정/fixture를 복원한다.

## 작업 3 — Android 로컬 개발 빌드

**Files:** 신규 필요 시 `Assets/Scripts/Features/GameScreen/Editor/Tests/PuzzlePlayerBuildVerification.cs`; 기존 `Assets/Scenes/PuzzleGame.unity`, LevelPackBuild 및 Addressables 빌더는 검증 대상이다. `ProjectSettings` 영구 변경은 계획하지 않는다.

**Interfaces:** 추가할 경우 `public static void RunAndroid()` 단일 메뉴 진입점. `BuildPipeline.BuildPlayer(BuildPlayerOptions)`의 scenes를 PuzzleGame 하나로 명시하고 Android/Development APK를 `Builds/Stage05/Android/PuzzleStage05.apk`에 출력한다. 실패는 보고서에 남기고 성공으로 처리하지 않는다.

- [x] Android 모듈·SDK/JDK·기존 서명 설정과 출력 경로 충돌을 확인한다. 미설치/권한 문제는 구체적으로 보고하고 자동 설치·기존 서명 변경으로 우회하지 않는다.
- [x] 원래 EditorBuildSettings.scenes, 현재 빌드 대상/옵션, Addressables 설정을 보존한다. 실제 빌드 씬 목록이 ValidateExclusion에 반영되는 검사부터 작성한다. Expected: 임시 직접/간접 LevelDefinition 참조가 있으면 빌드 전에 실패.
- [x] 검사 전용 씬 참조 fixture를 제거한 뒤 정상 빌드를 실행한다. 필요한 최소 빌드 검사만 구현하며 테스트가 없는 새 배포 시스템은 만들지 않는다.
- [x] BuildReport 성공, APK 존재/해시, 실제 사용된 씬·번들 목록을 기록한다. 포함 에셋 보고서와 번들 구성 증거로 원본 LevelDefinition 제외를 교차 확인한다. 증거가 부족하면 빌드 성공만 기록하고 제외 조건은 미완료로 남긴다.
- [x] 성공·실패 모두 finally에서 임시 씬 목록/옵션/재생 설정을 복원한다. 빌드 대상 전환이 필요했다면 복원 여부까지 확인한다. 원본 파일/dirty/GUID·팩 전후 차이를 대조한다.

## 작업 4 — 화면·선택적 기기 확인·최종 인계

**Files:** 생성 `Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-05-progress.md`, `Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-05-integration-usage.md`; 갱신 본 계획·목표·전체 로드맵 및 분류 목차.

**Interfaces:** 작업 1~3 결과·BuildReport·APK 해시·번들 구성·화면 증거를 소비한다. 검증 기록에 환경/명령/결과/미검증 사유를 연결한다.

- [x] 최종 소스 기준 네 비율과 안전 영역·회전 상태 보존을 확인한다. 실제 캡처를 열어 잘림/겹침/가독성을 검토한다. 이전 단계 캡처를 재실행 증거로 바꾸어 쓰지 않는다.
- [x] 기기가 연결되어 있고 설치/실행 승인이 있으면 APK 실행, 터치/다중 터치, 회전/노치, pause/retry, 백그라운드 복귀의 결과를 기록한다. 없으면 미검증 표와 사용자가 따라 할 절차를 작성한다. iOS는 별도 미검증으로 명시한다.
- [x] 최종 독립 리뷰 1회와 필요한 결함 수정/재검증을 마친다. 새 기능이나 상세 연출 개선으로 범위를 확장하지 않는다.
- [x] 임시 검사 도구/fixture를 정리하고 사용자 설정을 복원한다. `git diff --check`, meta/GUID, 원본/팩 전후 차이, 문서 링크를 검사한다.
- [x] 목표의 완료 조건을 항목별 실제 증거와 대조한다. 필수 번들·Android 빌드가 막히면 미완료를 유지한다. 충족했을 때만 5단계를 완료 처리하고 사용법/미검증 실기기 항목을 인계한다. 자동 커밋하지 않는다.

## 계획 자체 검토

목표의 전체 흐름·수명은 작업 1, 팩/번들 경계는 작업 2, 필수 Android 빌드는 작업 3, 비율·기기 상태·인계는 작업 4에 연결했다. 이미 존재하는 진입점을 재사용하고 새 빌드 진입점은 필요할 때 하나만 추가한다. 네 작업은 같은 Editor·설정을 공유하므로 병렬 실행하지 않는다. 계획 작성 당시에는 실행하지 않았으며, 이후 사용자 목표 명령으로 실행한 최종 증거는 [검증 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-05-progress.md)에 있다.

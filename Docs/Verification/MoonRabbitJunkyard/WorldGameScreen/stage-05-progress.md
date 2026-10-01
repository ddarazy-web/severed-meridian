# 5단계 통합 검증 기록

계획: `Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/stage-05-integration-plan.md`

상태: 구현·통합 검증 완료 (2026-10-01). Editor·실제 번들·Android 개발 APK 조건을 충족했다. Android/iOS 실기기 실행은 미검증이다.

최종 APK: `Builds/Stage05/Android/PuzzleStage05.apk` (74,181,551바이트), SHA256 `3FD1435618BBA99451D24A96BB5924735A072058B9C6EA1A417A58727C005520`. 01:16:08 재빌드 Succeeded, 오류/경고 0, 40.73초. 아래 시간순 기록의 첫 APK는 설정 복원 수정 전 결과이며 `PuzzleStage05-before-settings-fix.apk`로 별도 보존했다.

## 기준과 판단

- Ruling: 사용자 지정 ServeredMeridian/work 체크아웃에서 직접 실행한다. 현재의 4단계 변경과 복구 씬 사본을 보존한다. 별도 worktree/자동 커밋은 만들지 않는다 — 기존 변경 혼재 위험을 기준 diff/해시로 관리한다.
- Ruling: 커밋 기반 스킬 장부 대신 이 기록과 Logs/PuzzleIntegrationVerification을 사용한다 — 자동 커밋 금지에 따름 — 파일 증거를 인계 때 유지해야 한다.
- 시작 시 CIM으로 ServeredMeridian Editor PID 17832를 확인했다. 해당 Editor를 재사용하고 두 번째 Unity를 실행하지 않는다.
- Assets/Data, Assets/AddressableAssetsData, ProjectSettings의 시작 사본과 SHA256, git status를 Logs/PuzzleIntegrationVerification에 저장했다. 기준 사본이 존재하면 덮어쓰지 않는다.

## 사전 인터페이스 점검

| 연결 | 확인 내용 |
| --- | --- |
| 회귀 → 번들 | 기존 배치 검사 일부가 EditorApplication.Exit를 호출한다. 열린 Editor 실행에는 종료 분기가 필요하다. |
| 번들 → 플레이어 빌드 | LevelPackAddressablesBuilder와 LevelPackBuild 전처리가 Generate를 호출한다. 기준 팩/설정과 비교하고 원본 dirty 데이터 저장을 금지한다. |
| 실제 빌드 씬 → 제외 검사 | ValidateExclusion은 EditorBuildSettings.scenes를 읽는다. 명시적 BuildPlayerOptions 씬과 일치하는 임시 목록 및 복원 검사가 필요하다. |
| 모든 작업 → 인계 | 기존 결과 파일은 실행 시각과 함께 연결하며 과거 PASS를 재실행으로 간주하지 않는다. |

## 작업 상태

1. 사용자 흐름/수명 회귀: 관련 검사 통과. 결과 표 참조.
2. 실제 번들/팩 경계: 완료. 대표 캡처 및 최종 APK 번들 해시·원본 제외 대조 통과.
3. Android 개발 빌드: 완료. 빌드 시점/설정 복원 결함 수정 후 재빌드 통과.
4. 최종 화면/독립 리뷰/인계: 완료. 네 비율 캡처, 독립 리뷰 수정, 정리/컴파일/문서 감사 통과.

## 재실행 증거 (2026-10-01, KST)

결과 파일은 `Logs/PuzzleIntegrationVerification/`에 있다. 모두 현재 Editor에서 실행한 결과이며 실기기 결과가 아니다.

| 결과 파일 | 시각 | PASS | 확인 범위 |
| --- | --- | --- | --- |
| interaction-results.txt | 00:40:31 | 94 | 실제 UI, 아이템 실패/취소, pause·라스트팡·승패, 네 비율·비대칭 안전 영역·회전 상태, 5회 다시하기 |
| playback-results.txt | 00:40:45 | 48 | 두 입력 소스, 동일 스냅샷/시드, 5회 재시작, 취소/종료 수명 |
| gameplay-results.txt | 00:41:01 | 29 | 교환·매칭 생성 파워·연쇄·승패·라스트팡 |
| input-results.txt | 00:41:00 | 23 | Editor 마우스/모의 터치 및 중복 입력 차단 |
| scene-results.txt | 00:41:16 | 16 | 게임 씬/프리팹 구성과 원본 참조 |
| launch-data-results.txt | 00:41:21 | 9 | 선택 데이터와 미저장 사본 보존 |
| launch-lifecycle-results.txt | 00:42:24 | 137 | 두 소스·다른 맵/시드·편집 왕복·반복/취소 |
| artwork-lifecycle-results.txt | 00:42:37 | 5 | 편집 보드 이미지 수명 |
| layout-results.txt | 00:42:42 | 18 | 네 비율/비대칭 안전 영역 계산 |
| world-results.txt | 00:47:15 | 174 | BundledAssetProvider, 대표 콘텐츠·이미지 변형, 로드/취소/해제 |
| pack-results.txt | 00:47:38 | 32 | 실제 번들, 1/50/51/100/101, 잘못된 팩, 원본 제외 |
| build-exclusion-results.txt | 00:50:56 | 3 | 실제 지정 씬의 원본 직접/프리팹 간접 참조 차단 및 정상 씬 허용 |

월드 fixture는 5색 일반 블록, 가로/세로 로켓·폭탄·드론·자석, 나무상자·고철·회수캡슐·색상잠금·철근상자·2×2 발전기, 거미줄·곰팡이·먼지, 바닥·벽·포털·전선/단자를 포함한다. 각 내구도/색상/발전기 충전 이미지도 검사한다. 최종 대표 캡처는 보완 후 별도 기록한다.

작업 1 재실행: 조작 94, 세션 48, 게임 29, 씬 16, 입력 데이터 9, 편집 왕복 137, 편집 이미지 5, 배치 18 PASS. 결과를 Logs/PuzzleIntegrationVerification/*-results.txt에 별도 보존했다. 시작 Editor의 dirtyLevels는 비어 있고 Android/빠른 재생 빌더 0/플레이어 빌더 4였다. 배치 전용 세 검사에 Application.isBatchMode 종료 분기를 추가해 열린 Editor의 강제 종료를 피한다. 런타임 동작은 아직 변경하지 않았다.

번들 사전 검사에서 dirty 객체를 감지해 빌드를 중단했다. 진단 결과 path 없음/persistent=False/DontSave인 Level_1 사본이었다. Generate가 실제로 읽는 영속 레벨 에셋의 dirty 여부로 조건을 한정했다. 해당 사본은 저장/삭제하지 않고 유지한다. dirty-levels.txt에 진단을 기록했다.

작업 2: Android 대상 Addressables 콘텐츠 빌드 성공(00:45:36). buildlayout.json을 별도 보존했다. 월드 검사에서 실제 번들/토끼 Sprite는 준비됐으나 캡처가 배경 카메라를 선택하여 색상 검사가 실패했다. world-camera-red.txt와 level-01.png로 확인했고 검사만 Preview에 연결된 boardCamera를 사용하도록 수정했다. 런타임 카메라 코드는 변경하지 않는다.

작업 2 월드 검사 수정 후 174 PASS. MemoryPack 검사도 실제 번들 공급자/50레벨 경계/원본 제외/에디터 입력 경로를 통과했다. bundle-layout-audit.json: Board Artwork PackSeparately 17번들, Level Packs PackSeparately 1번들, 에셋 176개, 레벨 원본 경로 없음. 작업 3 명시적 빌드 씬의 직접/프리팹 간접 원본 참조를 기존 비활성 씬 목록에서 놓치는 RED를 확보했다. 빌드 도구가 실제 씬 목록을 임시 적용하고 finally로 복구하는 검사를 추가해 두 차단 및 정상 씬 허용 GREEN을 확인했다. Android Development APK 빌드를 요청했으며 아직 성공 판정은 하지 않는다.

작업 3 RED: Android 빌드가 Cannot build asset bundles while a build is in progress로 실패했다(android-build-red.txt). Ruling: 스킬의 기본 IPreprocessBuildWithReport 대신 설치된 Addressables 4.1과 같은 BuildPlayerProcessor 준비 단계를 사용한다 — Unity 6 실제 빌드에서 중첩 빌드가 금지됨 — 잘못되면 APK 재빌드로 검출한다. atlas 생성(-1000)→팩 생성(-950)→콘텐츠 빌드(-900) 순서와 공식 스트리밍 에셋 복사(1)를 유지한다.

Android 재빌드 GREEN: BuildReport Succeeded, 오류 0/경고 0, 4분 33.92초. android.buildreport 및 android-addressables-buildlayout.json을 보존했다. 월드 캡처의 기존 부분 viewport 잘림은 world-viewport-red.png로 보존하고, 검사 카메라의 rect/aspect만 캡처 동안 전체 출력 크기로 설정·복원하도록 보완했다. 게임 화면 런타임은 변경하지 않았다.

최종 번들 재검증: 월드 177 PASS(01:02:46), 실제 매칭 로켓을 만드는 씬 16 PASS(01:03:46). Android 빌드 번들을 Use Existing Build로 사용했다. 01:04에 네 비율 screen-*.png를 다시 캡처해 직접 열었다. 보드·HUD·미션·아이템/일시정지 버튼의 잘림/겹침 없이 표시됨을 확인했다. representative-portrait/landscape와 gameplay-created-rocket.png는 UI를 제외한 월드 전용 증거다. 실기기 실행은 승인되지 않아 수행하지 않았다.

빌드 산출물 감사: APK 74,181,257바이트, SHA256 143AD7C18F18239BEF6531B5BADC5AC87796857B66264972B5181511410DE871. final-bundle-audit.json에서 APK의 29개 번들 모두 실제 빌드 파일의 길이/SHA256과 일치한다. Board Artwork 28개 + Level Packs 1개, 모두 PackSeparately. 전체 번들 에셋 263개와 플레이어 포함 경로 2541개를 전체 LevelDefinition 검색 경로와 대조하여 원본 포함 0개. 기존 생성기가 기존 Animations PNG로 추가 생성한 효과 아틀라스 11개 및 그룹 등록은 재현 가능한 빌드 산출물로 유지한다(새 이미지/상세 애니메이션 구현 아님). 기존 17개 atlas와 meta는 변경하지 않았다. content_state.bin은 이 빌드에 대응하는 갱신이다.

독립 최종 리뷰(gpt-6-astra)는 APK/29번들 해시를 별도로 확인했고 Critical 0/Important 1/Minor 0을 보고했다. Important: RunAndroid 메뉴 자체가 다른 Addressables 초기 설정을 복원하지 못함. 성공·실패·미저장 설정 3개 RED(build-settings-red.txt)를 확인했다. 변경되는 빌더/재생 빌더/프로필/동시 빌드/카탈로그 경로만 finally에서 복원하고 dirty 설정은 사전 거절하도록 수정했다. 생성 콘텐츠 등록은 유지한다. GREEN 및 전체 회귀를 이어서 확인한다.

리뷰 수정 후 build-settings 3 GREEN. 검증 fixture의 다른 빌더는 실제 플레이어 콘텐츠를 지원하는 기본 BuildScriptPackedMode를 사용했다. 수정된 메뉴로 실제 Android APK를 다시 빌드했으며, 최종 BuildReport와 번들 레이아웃·APK 해시 대조도 다시 통과했다. 전체 회귀 결과는 final-regression-summary.json에 순차 기록한다.

## 최종 리뷰에서 제외한 항목의 판정

- Final: Ruling: 실기기 터치/회전/백그라운드는 미검증으로 인계한다 — 기기 실행 승인이 없고 목표가 별도 미검증 기록을 허용함 — 기기에서만 발생하는 문제는 아직 검출하지 못했다.
- Final: Ruling: iOS·상용 서명/스토어·새 기능/성능 목표는 추가하지 않는다 — 사용자 지정 제외 범위 — 해당 배포와 성능 판정은 별도 작업이다.
- Final: Ruling: 임의 씬을 쓰는 외부 자동화까지 확장하지 않는다 — 이번 메뉴는 PuzzleGame을 명시하고 검증한다 — 다른 빌드 경로는 실제 씬 검사 연결을 별도 확인해야 한다.
- 임시 정리/문서/컴파일은 제외하지 않고 완료 감사의 필수 항목으로 유지했다. 리뷰의 이 보류 항목은 마무리 증거를 확보한 뒤에만 완료로 표시한다.
- 미해결 Minor: 없음. 두 번째 리뷰는 하지 않고 수정의 재현 검사 및 전체 회귀로 확인한다.

Final: fixed Addressables 옵션 복원 — build-settings 성공/실패/dirty 거절 3건 RED→GREEN, 실제 Android 재빌드 GREEN, 전체 회귀 12묶음 571/571 및 포함 입력 검사 23/23 PASS(총 594). 최종 실행 시각·원본 결과 경로는 `final-regression-summary.json`을 따른다. 앞 표는 첫 통합 회귀 시각이며 최종 회귀는 01:16~01:22에 수행했다.

## 목표 완료 감사

| 요구 | 최종 증거 | 판정 |
| --- | --- | --- |
| 두 입력 소스·서로 다른 맵/시드·미저장 사본 | launch-data 9, launch-lifecycle 137, playback 48; 원본 파일/dirty 보존 항목 | PASS |
| 아이템·pause·연쇄·라스트팡·승패·다시하기·편집 복귀 | interaction 94, gameplay 29, scene 16, input 23 | PASS |
| 5회 반복·재진입·취소·로드 중 종료 | playback, launch-lifecycle, world 177의 핸들/사본/구독/화면 단일성 | PASS |
| 일반/생성 파워/대표 장애물·덮개/바닥/장치/2×2 | world 177, scene-bundled 16, 대표 캡처·실제 생성 로켓 캡처 | PASS |
| 50레벨 구간·손상/누락·간접 원본 참조 | pack 32, build-exclusion 3 | PASS |
| 실제 번들·APK 내 원본 제외 | final-bundle-audit.json, 전체 LevelDefinition 목록, Android packed-assets 2541경로, 29개 번들 SHA256 | PASS |
| Android Development APK·명시적 씬 | 최종 android.buildreport/summary/files/sha256, PuzzleGame 단일 씬 | PASS |
| 비율/비대칭 안전 영역/회전 상태 | layout 18, interaction 실제 UI 경계 및 pause/보드/난수 유지, 네 screen 캡처 | PASS |
| 실기기 결과 구분 | 사용 안내의 미검증 사유·재현 절차 표; Android/iOS 기기 실행 없음 | 미검증 명시 완료 |
| 독립 리뷰 | final-review.md; Important 1건 위 RED→GREEN 수정, 미해결 없음 | PASS |
| 임시 정리·설정/데이터/GUID·최종 컴파일·문서 링크 | cleanup.txt, final-compile.log, final-files-audit.json, scene-final.txt | PASS |

## 최종 정리와 인계

- 01:23:57 최종 네 비율을 다시 캡처하고 직접 열어 보드/HUD/미션/버튼의 잘림·겹침과 한국어 표시를 확인했다. `screen-1280x720.png`, `screen-450x800.png`, `screen-450x975.png`, `screen-600x800.png`가 최종 화면이다.
- 01:25:35 임시 StageFiveLiveRunner와 meta를 Unity AssetDatabase로 제거했다. 임시 참조 fixture 폴더도 없다. 이후 Editor 어셈블리 재컴파일 Tundra success 및 domain reload 완료, error CS 없음(`final-compile.log`).
- 씬은 PuzzleGame, dirty=False, Play=False다. Android/빠른 재생 빌더0/플레이어 빌더4/프로필/비활성 SampleScene 빌드 목록이 시작 기록과 일치한다. 검증용 입력·레이아웃 설정은 각 검사의 finally로 복원했다.
- 시작 SHA256과 비교하여 원본 레벨·MemoryPack·ProjectSettings·AddressableAssetSettings가 일치한다. 빌드가 만든 URP prefilter/runtime 목록, preloadedAssets/Android batching, 미사용 생성 파이프라인 설정도 복원/제거했다. 유지한 차이는 기존 소스에서 생성한 효과 atlas11개/그룹 등록과 이 빌드의 content_state.bin이다. 기존 meta/GUID 변경 0개.
- `git diff --check` 통과. 문서156개/로컬 링크949개 검사 실패0. 기존 사용자 변경·복구 씬은 유지했다. 자동 커밋/푸시/업로드·다른 프로젝트 조작·기기 설치는 하지 않았다.
- APK·BuildReport·해시·번들 목록·실행 결과·캡처·기준 사본·빌드 결과 감사 스크립트는 `Logs/PuzzleIntegrationVerification` 및 `Builds/Stage05/Android`에 보존한다. Git 제외 로컬 산출물이므로 인계 시 따로 보관한다.
- 사용법 및 실기기 미검증 절차: [5단계 실행 안내](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-05-integration-usage.md). 최종 리뷰 미해결 사항 없음. 출시 가능 판정은 하지 않았다.

회귀 실행 보완: 외부 대기 스크립트가 검사 시작 시 빈 결과 파일을 null로 읽어 중단되어 빈 파일은 계속 기다리도록 보완했다. 해당 편집 이미지 검사는 최초 UI 표시 단계에서 60초 시간 초과를 남겼다(artwork-lifecycle-timeout.txt). 같은 Unity 창을 활성화한 뒤 소스 변경 없이 같은 검사를 재실행하여 5 PASS를 확인했다. 백그라운드 창 상태에서의 최초 표시 대기 실패를 숨기지 않으며, 최종 PASS는 활성 Editor 환경의 결과다. 나머지 검사는 앞서 완료한 항목을 재시작하지 않고 이어서 실행한다.

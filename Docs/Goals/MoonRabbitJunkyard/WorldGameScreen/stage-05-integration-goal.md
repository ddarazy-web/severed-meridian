# 5단계 통합 검증과 마무리 — 목표 및 완료 조건

상태: 구현·통합 검증 완료 (2026-10-01).

[전체 로드맵](../../../Planning/MoonRabbitJunkyard/WorldGameScreen/2026-09-30-world-game-screen.md) · [4단계 완료 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-04-progress.md) · [작업 계획](../../../Planning/MoonRabbitJunkyard/WorldGameScreen/stage-05-integration-plan.md) · [목표 명령어](../../../Commands/MoonRabbitJunkyard/WorldGameScreen/stage-05-goal-command.md)

## 목표

완료된 1~4단계의 선택 맵 실행·월드 보드·UI를 하나의 흐름으로 검증하고, 실제 Addressables 번들과 Android 개발 빌드에서도 레벨 원본 대신 50레벨 MemoryPack을 사용하는지 증명한다. 발견한 결함만 최소 수정하고 재현 검사로 확인한다.

## 확인한 기반

- 4단계는 Editor 동작·회귀 386개와 에셋 구성 3개 검사를 통과했다. 과거 PASS 수를 이번 단계의 합격 기준으로 대체하지 않는다.
- `Assets/Scenes/PuzzleGame.unity`가 게임 진입 씬이다. 확인 당시 EditorBuildSettings에는 활성 씬이 없으므로 현재 설정 그대로 빌드하면 게임 씬이 포함된다는 보장이 없다.
- `LevelPackBuild.Generate()`는 저장된 레벨을 변환하고 기존 팩·Addressables 설정을 갱신한다. `LevelPackAddressablesBuilder`와 플레이어 빌드 전처리도 관련되므로 읽기 전용 검사처럼 취급하지 않는다.
- 기존 `PuzzleWorldBoardVerification`, `PuzzleArtworkVerification` 등에는 Editor 종료 호출이 있다. 열린 사용자 Editor에서 배치 전용 진입점을 그대로 호출하지 않는다.

## 범위와 실행 원칙

1. ServeredMeridian만 사용하고 기존 변경·미저장 데이터·GUID를 보존한다. 자동 커밋·푸시·스토어 업로드는 하지 않는다.
2. Editor에서 에셋/MemoryPack 각각 맵·시드 선택 → 플레이 → 아이템/연쇄 → pause/resume → 승리/실패 → 같은 판 다시하기 → 편집 복귀를 확인한다. 미저장 에셋 사본과 저장된 팩 입력의 차이도 유지한다.
3. 일반 블록, 매칭 생성 파워, 장애물, 덮개, 바닥, 장치, 2×2 점유·그리기 순서를 대표 fixture로 검사한다. 실제 콘텐츠 목록을 확인해 대표 조합을 선정하고 목록을 기록한다.
4. 1/50/51/100/101 구간 경계, 누락·손상 팩, 원본 레벨 배제, 필요한 아틀라스의 로딩·해제를 검사한다. 테스트 데이터는 기존 파일과 충돌하지 않는 임시 영역에 만든 뒤 제거한다.
5. 실제 번들을 빌드해 BundledAssetProvider를 사용하는 로드임을 확인한다. AssetDatabase 기반 빠른 재생만으로 합격시키지 않는다. 기존 분할 그룹을 합치거나 배포 방식을 바꾸지 않는다.
6. Android Development APK를 로컬 `Builds/Stage05/Android/`에 만든다. 상용 서명·스토어 배포는 범위 밖이다. SDK/모듈/서명 문제를 임의 설치·설정 변경으로 우회하지 않고 필요한 조치를 보고한다. Windows 빌드를 Android 증거로 대신하지 않는다.
7. 빌드 씬은 PuzzleGame으로 명시하고 검증기가 실제 빌드 씬 목록을 검사하도록 한다. 임시 변경한 빌드 씬 목록·재생 모드·Addressables 프로필/빌더·빌드 옵션은 복원한다. 원본 레벨을 저장하지 않는다. 팩 갱신 전후 차이와 이유를 기록하고 무관한 생성 변경을 남기지 않는다.
8. 실기기가 사용 가능하고 설치/실행이 승인된 경우 기기 검증을 수행한다. 사용할 수 없으면 별도 미검증 표를 남긴다. Editor 모의 입력과 Android 빌드 성공을 실기기 성공으로 표현하지 않는다.

## 완료 조건

- [x] 현재 소스의 Unity 컴파일과 영향받는 기존 검사들이 통과하고 각 결과 파일·실행 시각·범위를 기록한다.
- [x] 두 입력 소스의 전체 사용자 흐름과 서로 다른 레벨 번호/시드, 원본·dirty 상태 보존을 검증한다.
- [x] 대표 콘텐츠와 매칭 생성 파워 이미지가 실제 번들 로딩에서도 유지된다.
- [x] 50레벨 경계·잘못된 팩·직접/간접 원본 참조 차단을 검사하고 실제 번들 구성에서 LevelDefinition 원본이 배제됨을 증명한다.
- [x] 반복 다시하기·씬 왕복·로딩 중 종료 후 소유 아틀라스 핸들·레벨 사본·UI 구독/화면 객체가 누적되지 않는다. 공유 캐시는 세션 소유 자원과 구분한다.
- [x] 네 비율(1280×720, 450×800, 450×975, 600×800)·비대칭 안전 영역·상태 보존을 최종 변경 기준으로 확인하고 대표 화면을 남긴다.
- [x] Android 개발 빌드가 성공하고 APK 경로·해시·BuildReport·Addressables 빌드 구성 증거를 기록한다. 원본 레벨이 플레이어/번들에 들어가지 않음을 빌드 결과로 확인한다.
- [x] 실기기 확인 결과 또는 기기 미검증 항목/이유/재현 절차가 별도 표에 있다. 실기기 미검증이면 출시 가능 판정은 하지 않는다.
- [x] 임시 fixture/검증 도구 제거, 변경 설정 복원, 데이터/GUID 보존, diff 및 문서 링크 검사를 마친다.
- [x] 독립 최종 리뷰와 필요한 수정 검증 후 검증 기록·인계/실행 안내·로드맵을 갱신한다.

필수 Editor·번들·Android 빌드 조건이 막히면 해당 작업은 미완료다. 실기기 실행은 가용성에 따른 별도 판정이며 필수 빌드 조건을 대신하지 않는다.

## 산출물과 제외 범위

실행 시 `Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-05-progress.md`, `Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-05-integration-usage.md`, `Logs/PuzzleIntegrationVerification/`의 결과·빌드 보고서·캡처를 작성한다. 이 계획 작성만으로 생성하거나 완료 처리하지 않는다.

신규 게임 규칙, 새 이미지/상세 애니메이션, 상용 재고·광고·결제·보상, 다음 레벨·메인 메뉴, 성능 목표 신설, 원격 배포, iOS 빌드 및 플랫폼 정책 변경은 제외한다.

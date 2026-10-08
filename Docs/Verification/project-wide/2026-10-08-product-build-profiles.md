# 제품별 Build Profile 준비 기록

2026-10-08 · Unity 6000.3.10f1

## 생성한 설정

위치: Assets/Settings/BuildProfiles

| 프로필 | 대상 | 프로필 전용 심볼 | 현재 씬 |
|---|---|---|---|
| Android Game | Android | PRODUCT_ANDROID_GAME | PuzzleGame |
| iOS Game | iOS | PRODUCT_IOS_GAME | PuzzleGame |
| Windows Steam Game | Windows x64 Player | PRODUCT_STEAM_GAME | PuzzleGame |
| Windows Level Editor | Windows x64 Player | PRODUCT_LEVEL_EDITOR | 없음 — 독립 도구 씬 구현 대기 |

각 프로필은 자체 씬 목록을 사용한다. 제품 심볼은 프로필마다 하나이며 전역 PlayerSettings에는 추가하지 않았다. UNITY_ANDROID, UNITY_IOS, UNITY_STANDALONE_WIN 같은 Unity 기본 심볼은 수동으로 추가하지 않는다. 두 Windows 제품은 플랫폼 심볼이 같으므로 제품 심볼로 구분한다.

## 사용 방법

Unity의 File > Build Profiles에서 원하는 프로필을 선택하고 Switch Profile로 활성화한다. 프로젝트 창에서 해당 프로필 에셋을 선택해 설정을 확인할 수도 있다. 프로필 전용 심볼은 해당 프로필 활성화/빌드 시 적용된다. 아직 실제 빌드는 실행하지 않는다.

## 완료 범위와 남은 작업

- 프로필 및 심볼 준비만 완료했다. 심볼을 소비하는 제품 시작 구성, 서비스·어셈블리·리소스 분리 구현은 통합 계획에서 진행한다.
- 게임용 프로필은 기존 PuzzleGame 씬을 사용한다. GameBootstrap은 아직 없다.
- 레벨툴 프로필은 빈 씬 목록이다. Windows 독립 레벨툴은 아직 실행/빌드 준비 완료 상태가 아니다. AuthoringBootstrap과 제작 UI를 구현한 뒤 연결한다.
- Steam 프로필은 Windows 게임 구분 설정이며 Steam SDK/상점/업로드 구성을 구현한 것은 아니다.
- 기존 PlayerSettings를 상속하며 서명, 앱 ID, 제품별 저장 경로, 배포 설정은 이번에 변경하지 않았다.
- Addressables의 프로필/그룹/출력 경로는 별도로 연결해야 한다. Unity Build Profile 생성만으로 제품별 콘텐츠가 격리되지 않는다.
- 실제 Player 빌드, Addressables 콘텐츠 빌드, 기기 실행은 수행하지 않았다. 플랫폼별 컴파일 성공을 보장하는 검증도 아직 아니다.

## 검증

Unity Editor 배치 실행에서 공식 엔진의 플랫폼 팩터리를 사용해 프로필을 생성했다. 생성용 임시 스크립트는 제품 소스에 남기지 않는다.

프로필을 저장 후 다시 로드하여 아래 항목을 확인했다.

- 네 프로필의 대상 플랫폼과 플랫폼별 설정 객체가 유효하다.
- 각각의 전용 심볼, 심볼 사용 설정, 독립 씬 목록이 기대값과 일치한다.
- 기존 활성 프로필·활성 빌드 대상·전역 씬 목록·PlayerSettings가 변경되지 않았다.
- 에셋에는 고유 GUID와 Unity 생성 메타데이터가 있다.

검증 로그: Logs/BuildProfiles/verification.txt. 테스트 연결은 검사 후 해제한다.

## 다음 연결 지점

[게임·Windows 제작 도구 6단계 통합 계획](../../Planning/project-wide/2026-10-08-game-and-authoring-products-plan.md)의 제품 수를 Android/iOS/Steam/레벨툴 네 제품으로 변경했다. 기본 프로필 생성이 6단계 전체 완료를 의미하지 않는다.

참고: [Unity Build Profile 설정](https://docs.unity3d.com/6000.3/Documentation/Manual/build-profiles-reference.html), [Unity 6000.3 프로필 생성 API 구현](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/BuildProfile/BuildProfileCreate.cs).
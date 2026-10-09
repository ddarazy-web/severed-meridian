# 6단계 제품 구성 경계

제품 선택은 `Tools > Products > 선택`에서 한다. 이 명령은 Unity Build Profile, Addressables Profile, 출력 위치를 함께 선택하며 빌드를 시작하지 않는다. 구성값 갱신은 `제품 구성 갱신 (빌드 안 함)`을 사용한다.

| 제품 | 제품 심볼 | 시작 씬 | 출력 위치 | 제품 이름 |
|---|---|---|---|---|
| Android Game | PRODUCT_ANDROID_GAME | PuzzleGame | Builds/Android/Game.apk | Moon Rabbit Junk Shop |
| iOS Game | PRODUCT_IOS_GAME | PuzzleGame | Builds/iOS | Moon Rabbit Junk Shop |
| Windows Steam Game | PRODUCT_STEAM_GAME | PuzzleGame | Builds/Steam/MoonRabbit.exe | Moon Rabbit Junk Shop |
| Windows Level Editor | PRODUCT_LEVEL_EDITOR | LevelTool | Builds/LevelTool/MoonRabbitLevelTool.exe | Moon Rabbit Level Editor |

- 게임과 Windows 도구는 제품 이름이 다르므로 Unity의 기본 사용자 저장 위치가 분리된다. 제작 JSON은 사용자가 선택한 별도 폴더에 저장하며 게임의 플레이 기록과 섞지 않는다.
- Android의 기존 앱 식별자를 게임 제품의 프로젝트 구성값으로 사용한다. iOS/Steam에 남아 있던 Unity 템플릿 식별자는 이 값으로 교체하고, 도구는 `.leveleditor`를 붙인다. 이 값은 스토어 등록·예약·인증 완료를 뜻하지 않는다.
- 제품별 Player Settings는 기존 전역 설정에서 최초 복사한다. 이미 만든 프로필은 이름·식별자 등 이번 계약 항목만 갱신하며 다른 설정을 전역 값으로 다시 덮어쓰지 않는다. 이후 공통 Player Settings를 변경할 때는 네 프로필에 적용할지도 확인해야 한다.
- 네 Addressables Profile은 같은 현재 게임 표현/팩 그룹을 사용한다. 도구도 실제 게임 시험에 같은 표현이 필요하다. Profile 이름만으로 그룹이 제외되는 것으로 취급하지 않는다.
- 각 콘텐츠 Profile의 Local.BuildPath/Local.LoadPath에 제품 심볼을 포함한다. 추가로 Addressables.LibraryPath/BuildReportPath/ContentStateBuildPath를 제품별 루트로 라우팅하여 번들뿐 아니라 settings·catalog·업데이트 상태·보고서도 분리한다. 선택, 도메인 재로드 후 초기화, 제품/콘텐츠 빌드 진입에서 적용한다. 기본/미선택 프로필로 돌아오면 기본 Addressables 경로를 복원한다.
- 제품 심볼은 전역 Player Settings에 넣지 않는다. 각 Build Profile에 하나만 둔다. 선택하지 않은 제품, 플랫폼 불일치, 다른 제품의 콘텐츠 Profile/출력 위치는 빌드 전 검사에서 거절한다.
- 제작 전용 코드(`LevelAuthoring`, `LevelTool`의 Editor 밖 코드)는 `UNITY_EDITOR || PRODUCT_LEVEL_EDITOR`로 제한한다. 공통 퍼즐 실행과 런타임 임시 데이터 타입은 게임에도 남는다.
- 제작 SO, 테스트, 게임의 도구 화면 참조는 씬·Resources·사전 로드·Addressables 의존성 검사에서 거절한다. StreamingAssets에 제작 JSON을 넣어 우회하지 않는다.
- 프로필별 Player Settings는 Unity 6000.3의 내부 프로필 API를 좁은 어댑터로 사용한다. Unity 업그레이드로 해당 API/직렬화 형식이 달라지면 추측하여 기록하지 않고 오류로 중단한다.

## 검증과 한계

실행 메서드: `Products.Editor.ProductBoundaryVerification.Run`.

실행 결과는 `Logs/GameAuthoringStage06/product-audit.txt`와 해당 TestHarness 로그를 확인한다. 플러그인 목록은 플랫폼 호환 후보이며 DefineConstraints·명시적 참조 조건을 함께 기록한다. 후보 목록에 이름이 있다는 것만으로 최종 Player 포함을 확정하지 않는다. 현재 활성 플랫폼의 PlayerWithoutTestAssemblies 목록에서 테스트 소스가 없는지 검사한다. 이 목록의 컴파일 참조에는 자동 참조된 NUnit 후보가 있을 수 있으므로 최종 포함으로 단정하지 않는다. 설치 Unity Test Framework의 TestBuildAssemblyFilter에 일반 빌드 옵션을 전달하여 NUnit/TestRunner를 제외하는지 직접 검사한다. 제품 빌드 가드는 IncludeTestAssemblies 및 Play Mode Test Runner 모드를 거절한다. 검사는 제품 계약의 허용/거절, 전역 Player Settings 불변, 실제 프로필 설정 및 참조 경계에 관한 것이다. 설치 모듈/호환 플러그인 목록을 보고하되 SDK와 실기 배포 준비 완료로 해석하지 않는다.

Player/Addressables 빌드, iOS Xcode 생성·서명, Steam SDK 통합·스토어 인증, 다른 PC·모바일 실행은 이번 승인 범위 밖이며 수행하지 않는다. 네 제품의 실제 배포 검증은 별도 승인 후 필요하다.

제품 선택/복원 검사: `Products.Editor.ProductSelectionVerification.Run`. 현재 활성 타깃과 호환되는 제품만 선택하며 Build Profile·콘텐츠 Profile·출력 위치·전체 콘텐츠 루트가 일치하는지 확인한 후 기존 선택과 경로를 복원한다. 네 제품의 카탈로그 루트가 서로 다른지도 실제 정적 경로 평가로 확인한다. 이번 실행에서 선택하지 않은 플랫폼/제품은 로그에 별도로 기록한다. 빌드를 시작하거나 검사 때문에 플랫폼을 변경하지 않는다.
선택 검사는 API 호출 직후의 연결 상태를 확인하고 같은 호출에서 복원한다. 심볼 변경 후 도메인 재로드를 완료한 제품 실행까지 검증한 것은 아니다. 해당 재로드·배포 실행 확인은 후속 제품 실행 게이트에 남는다.

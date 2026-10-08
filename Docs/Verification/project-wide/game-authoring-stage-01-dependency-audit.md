# 게임·제작 도구 분리 1단계 — 의존성 및 저장 경계 조사

> 2026-10-08 · 묶음 A 코드 조사 · 구현 전 기준. 주 담당의 통합 검증 결과는 별도 진행 기록을 따른다.

## 범위와 판정 기준

ServeredMeridian의 Assets/Scripts, PuzzleGame 씬과 연결 프리팹, Packages/manifest.json을 확인했다. 이 문서는 제품 실행 결과나 플랫폼 빌드 성공을 주장하지 않는다. 아래 ‘사용’은 현재 PuzzleGame 진입 코드의 연결 상태이며 Android/iOS/Steam 실제 기기 검증을 뜻하지 않는다. Windows 레벨툴은 독립 씬이 아직 없어 예정 상태를 따로 표시한다.

## 기능별 경계

모든 코드 경로는 Assets/Scripts/Features 기준이다.

| 기능 | 공유 가능한 현재 코드 | Editor에 묶인 부분 | 후속 단계 |
|---|---|---|---|
| 보드/레벨 편집 | Levels/Data/LevelDefinition.cs, Levels/Validation/LevelDefinitionValidator.cs, Board 관련 데이터·규칙 | LevelEditor/Editor/Application/LevelCommonEditing.cs는 UnityEditor, SerializedObject 및 Elements.Editor 편집기에 의존. LevelEditorWindow와 보드 UI는 Editor 폴더 | 2단계 JSON 문서, 3단계 공통 편집 명령·Undo 경계, 4단계 기본 도구 UI |
| 미션 | Missions/Data/LevelMissionDefinition.cs, Missions/Rules/LevelMissionRules.cs, Missions/Runtime/MissionProgressRules.cs | Missions/Editor/Application/LevelMissionEditing.cs의 SerializedObject, LevelEditorMissionPanel | 3단계 편집 분리, 4단계 기본 미션 편집 |
| 튜토리얼 | Tutorial/Runtime/TutorialBoardAdapter.cs, TutorialExecutionContext.cs, Validation/TutorialFlowResolver.cs | Tutorial/Editor/LevelTutorialEditorPanel 및 TutorialFlowAuthoring.cs, 샘플/공유 흐름 저장·사용처 검색 | 2단계 흐름·샘플 ID/JSON 계약, 3단계 저장 전환, 5단계 조립 화면 이식 |
| 공급 | BlockSupply/Data/LevelSupplyDefinition.cs, Rules/LevelSupplyRules.cs, Elements/Runtime/ElementSupplyBehaviorRegistry.cs | BlockSupply/Editor/Application/LevelSupplyEditing.cs는 후보 SO 사본과 SerializedObject로 변경 검증·반영 | 3단계 공통 편집, 5단계 고급 공급 UI |
| 흐름/연결 | BoardFlow/Rules/LevelFlowRules.cs, LevelConnectionRules.cs, Runtime/SettlementResolution.cs | BoardFlow/Editor/Application/LevelFlowEditing.cs, LevelConnectionEditing.cs 및 Routing partial의 SerializedObject | 3단계 편집 분리, 5단계 흐름 도구 |
| 모양 카탈로그 | 최종 BoardDefinition은 실행이 사용 | ShapeCatalog/Editor/Data/LevelShapePreset.cs 자체가 Editor 전용. LevelShapeUsage/Recommendations는 AssetDatabase 검색·생성·이름 변경·휴지통 이동 | 2단계 모양 문서 계약, 3단계 저장 경계, 5단계 카탈로그 UI |
| 자동 플레이 | AutoPlay/Runtime의 BotPlaySession, BotBatchSession, Strategy, Analysis는 기존 실행 엔진 재사용 | AutoPlay/Editor의 다중 레벨 화면, 작업 관리, 기록 저장·정리. TestRecordPaths는 저장 루트 밖/정션 차단 | 3단계 기록 경계 검토, 5단계 자동 플레이 작업/결과 UI |
| 게임 플레이 | GameScreen/Runtime/Session/PuzzleGameSession, PuzzlePlay/Runtime/Actions/BoardActionExecutor 및 LevelRuntimeState | GameScreen/Editor/Launch의 AssetDatabase, EditorSceneManager, SessionState, 소스 선택 | 1단계 요청·문맥·소유권 공통화, 4단계 독립 도구에서 호출 |
| 표현/입력/팝업 | GameScreen/Runtime 월드보드·uGUI 화면·입력·오디오, Systems의 공통 팝업 | 에디터 보드 그림은 별도 Editor 표현. 독립 도구 UI는 아직 없음 | 1단계 작은 Runtime UI 기술 확인, 4/5단계 실제 제작 화면 |
| 콘텐츠 배포 | Levels/Data/LevelPackCodec, LevelPackLoader 및 Elements 콘텐츠 로딩 | Levels/Editor/Persistence/LevelPackBuild.cs, LevelPackAddressablesBuilder.cs 등 제작/빌드 어댑터 | 2/3단계 JSON→기존 팩 연결, 6단계 제품별 배포 검증 |

## 실제 저장·서비스 연결

| 기능 | Android 게임 | iOS 게임 | Steam 게임 | Windows 레벨툴 |
|---|---|---|---|---|
| 퍼즐 진행 상태/이동 수/미션 | 사용: 세션 메모리 | 사용: 동일 | 사용: 동일 | 독립 진입 미구현, 공통 실행 재사용 대상 |
| 튜토리얼 완료/학습 ID | 사용: 정식 PlayerPrefs 정책 | 사용: 동일 | 사용: 동일 | 정식 기록 미사용 원칙. 기존 Unity 시험은 SessionState 콜백 사용, 독립 시험은 메모리 문맥 필요 |
| 아이템 행동 | 사용: 보드 상태 변경 | 사용: 동일 | 사용: 동일 | 동일 행동 재사용 대상 |
| 영속 아이템 수량/재화 차감/지급 | 미구현: 호출 없음 | 미구현 | 미구현 | 미사용, 가짜 경제 저장소 불필요 |
| 정식 레벨 해금/별/진행 저장 | 미구현: 호출 없음 | 미구현 | 미구현 | 미사용 |
| 광고/IAP/플레이어 분석 | 미연결 | 미연결 | 미연결 | 미사용 |
| Firebase/Steam SDK 초기화 | 미연결 | 미연결 | 미연결: Steam 프로필만으로 SDK가 생기지 않음 | 미사용 |
| MemoryPack/Addressables 콘텐츠 | 사용 | 사용 | 사용 | 시험 표현 재사용 대상, 배포 그룹 격리는 6단계 |
| 제작 원본 저장/자동플레이 기록 | 미사용: Editor 전용 | 미사용 | 미사용 | 독립 저장 어댑터 미구현; 현재 Unity Editor 안에서만 사용 |

구체적 호출 근거:

- GameScreen/Runtime/Session/PuzzleGameSession.cs의 Start → InitializeAsync → PrepareAsync → StartingBoardSearch/BoardActionExecutor 흐름이다. 진행 피드백 partial은 HUD용 카운터와 오디오를 갱신하며 정식 진행 저장을 호출하지 않는다.
- PuzzleGameSession.Controls.cs의 TryUseItem → BoardActionExecutor.UseItem 또는 UseApprovedFreeItem은 보드 사본/행동 기록만 변경한다. PuzzlePlay/Runtime/Actions/BoardActionExecutor.Items.cs에서 수량 저장소를 조회하거나 차감하지 않는다. PuzzleItemBarView도 상태에 따른 버튼 활성화만 담당한다.
- PuzzleGameSession.LevelTransition.cs의 AdvanceLevelAsync는 다음 번호 팩을 읽고 후보 세션을 교체한다. 레벨 해금이나 최고 기록 저장 호출은 없다.
- Tutorial/Runtime/TutorialExecutionContext.cs의 CreatePlayer가 실제 영속 쓰기 경계다. 키는 MoonRabbit.Tutorial.Completed.<level>, MoonRabbit.Tutorial.Identity.<id>이며 PlayerPrefs.Save를 호출한다. ShouldRun(LevelDefinition)의 이전 레벨 승계도 Identity 키를 쓸 수 있어 ‘완료 시에만’ 차단하면 불충분하다.
- 구현 전 PuzzleGameSession.Tutorial.cs는 문맥이 없으면 CreatePlayer로 대체한다. 도구 시험이 문맥을 빠뜨려도 정식 기록으로 내려가는 위험이므로 이번 공통 진입이 정식/시험 문맥을 명시해야 한다.
- PuzzleEditorLauncher.cs는 SessionState의 Puzzle.EditorLaunch.tutorialCompleted 및 tutorialIdentity 콜백을 전달한다. 씬 진입/복귀와 원본 데이터 읽기는 이 Editor 어댑터에 남겨야 한다.
- Assets/Scripts 전체의 Firebase/Steam/Advertisement/Analytics/RuntimeInitializeOnLoadMethod 조회에서 연결 호출은 없었다. PlayerPrefs 조회 결과도 TutorialExecutionContext 한 곳이다. Assets 안의 SDK 명칭 C# 파일 및 Assets/Packages 코드 추가 조회에서도 해당 초기화 소스를 찾지 못했다. 현재 manifest에 Unity 기본 unityanalytics 모듈이 있으나 이것은 게임의 분석 SDK 초기화 호출 증거가 아니다. 네이티브 플러그인/최종 패키지 수준 제외 여부는 6단계 검증 사항이다.
- PuzzleGame.unity는 PuzzleGameSession, PuzzleWorldBoard, PuzzleScreen 프리팹을 참조한다. 별도 상점/SDK 부트스트랩을 연결하는 현재 게임 호출 경로는 확인되지 않았다.

따라서 1단계에서 실제 차단·검증할 영속 부수효과는 튜토리얼 완료 및 학습 ID 승계다. 아이템/재시작은 정식 경제를 가상으로 만든 뒤 격리하는 대신 현재 메모리 동작과 관련 PlayerPrefs 불변을 검사한다.

## 어셈블리 경계 결정

이번 단계에서는 새 asmdef를 만들지 않는 것이 적절하다. Assets/Scripts 아래 asmdef는 현재 없으며 대부분 기본 어셈블리와 Editor 폴더 구분을 사용한다. GameScreen 계약 하나만 새 어셈블리에 넣으면 아직 Assembly-CSharp에 남은 LevelDefinition, Simulation, Elements 타입을 참조할 수 없어 광범위 이동이 필요하다.

현재 데이터/실행 의존도 단방향 작은 기능으로 단순 분리되지 않는다. LevelDefinition은 Elements 카탈로그·Tutorial 정의를 보유하고, TutorialBoardAdapter는 Levels·Elements·Simulation을 이용하며 Elements/Runtime/ElementBehaviorRegistry.cs는 Levels·Simulation을 참조한다. 이는 현재 같은 어셈블리 안에서 유효하지만 폴더별 asmdef를 즉시 만들면 순환 참조 또는 기본 어셈블리 참조 제약을 만든다.

- 1단계: 공개 실행 계약에 UnityEditor·경로·SessionState를 넣지 않고 현행 Editor 폴더 경계를 유지한다.
- 2단계: JSON 문서 DTO가 필요한 참조만 확인해 분리 가능한 데이터 경계를 마련한다. UnityEngine 제거를 이번 단계 목표로 확대하지 않는다.
- 3단계: 제작 명령/저장 어댑터를 분리할 때 참조 그래프와 internal 접근을 다시 검사한다. 공통 데이터·실행을 묶는 경계를 먼저 확보하고 Editor 어셈블리가 이를 참조하도록 한다.
- 4단계: 독립 도구 UI/진입 어셈블리의 의존을 공통 계약으로 한정한다. 게임 UI와 별도 UI 기술을 선택하더라도 퍼즐 실행 엔진을 복제하지 않는다.
- 6단계: 프로필 심볼, 제품 전용 코드/씬/플러그인/Addressables를 실제 배포 조건으로 격리한다. 현재 프로필 존재를 컴파일·콘텐츠 격리 완료로 표현하지 않는다.

## 조사 제한 및 인계

이 문서는 코드와 에셋 참조의 읽기 전용 조사다. UI Toolkit 런타임 시제품, 동등성/수명주기 검증, 기존 파일 해시·미저장 상태 보존 증거는 주 담당의 별도 검사 대상이다. 패키지 프로필 참고 문서에는 과거 SDK/버전 정보가 남아 있으므로 현재 manifest 및 소스를 우선했으며 참고 문서 자체는 요청 범위 밖이므로 수정하지 않았다.

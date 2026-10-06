# 큰 구간 3단계 — 정의 ID 저장과 레벨 제작 도구

> 실행자는 `superpowers:executing-plans`로 직접 진행한다. 하위 에이전트·커밋·푸시는 사용하지 않는다.

상태: 완료. 2026-10-06. A~D 구현·최종54종690245 PASS/0 FAIL·전량 논리 비교·보존 감사·4단계 문서 인계를 마쳤다.

**Goal:** 제작 카탈로그의 정의 ID로 요소를 선택·저장하고, 구형 레벨과 새로운 레벨을 실제 보드에서 실행하며, 50레벨 MemoryPack과 편집기의 검색·선택·검사를 연결한다.

**Architecture:** 제작 원본 ScriptableObject와 불변 `ElementDefinition`/`ElementCatalog`, 배포 DTO를 분리한다. 구형 enum 목록은 버전별 호환 경계에서 읽고 새 목록은 정의 ID를 기준으로 한다. 실제 편집은 기존 SerializedObject/Undo에 맡기고 카탈로그 화면 상태에만 MVVM을 시범 적용한다.

**Tech Stack:** Unity 6000.3.10f1, 기존 C#·UI Toolkit Editor·MemoryPack·Addressables·asmdef.

**Spec:** [통합 설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md), [통합 가이드](integration-guideline.md).

연결: [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/phase-03-storage-authoring-goal.md) · [실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/phase-03-command.md) · [2단계 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/phase-02-progress.md).

## 시작 기준과 결정

- 2단계는 기존30종687011 + 신규9종651 + 그래픽2종1303 = **688965 PASS/0 FAIL**이다. 전체18182행·188팩·225상태의 기존 논리 필드는 동일하고 추가 비행 이력1263행은 별도 기록됐다. 반복 실제 재선택의 승인된 명중 변화는 유지한다.
- 현재 `LevelDefinition.CurrentSchemaVersion=4`, `LevelPackCodec.FormatVersion=1`, 묶음 크기50이다. `PackedLevel`은 명시적 순서0~13을 사용한다. 현재 `RuntimeObstacle.Definition`과 편집 도구의 선택은 구형 배치/enum에 연결되어 있다.
- 새 ID 목록은 **레벨 스키마5 / 팩 포맷2**로 구분한다. 구형 DTO의 순서와 숫자는 그대로 보관하고 신형 DTO를 별도로 정의한다. v1 바이너리를 신형 DTO로 무조건 역직렬화하지 않는다. 기존 디스크 팩을 계속 테스트할 수 있도록 명시적 v1 읽기 경계를 유지한다.
- 팩2는 그50레벨 구간의 배치·공급·행동·연결에서 필요한 정의 DTO 표를 함께 담는다. 런타임에는 불변 카탈로그를 구성하고 제작 원본 에셋을 참조하지 않는다. 그림·프리팹·아틀라스의 준비는 4단계 범위다.
- 구형 원본은 읽을 수 있게 유지한다. 자동 로드·검색·검사에서 원본을 저장하지 않는다. 실제 에셋 변환은 **미리보기 후 사용자가 선택한 레벨에만** 적용하는 도구로 제공한다. 새로운 목록과 구형 목록을 합쳐 중복 실행하지 않는다.

## 공통 제약

- ServeredMeridian `work`의 현재 HEAD·완료분·기존 WIP를 시작 시 기록하고 보존한다. 다른 프로젝트는 작업하지 않는다.
- 9×9·기존 영구 ID·enum 숫자·본체 인스턴스 ID·발전기 연결·색·내구도·공급 순서·미션·규칙 난수와 2단계 드론 동작을 유지한다.
- 새 콘텐츠마다 종류 enum이나 실행 분기를 추가하지 않는다. 동일 행동의 새 ID는 기존 정의 프로필과 등록 행동을 재사용한다.
- 빌드·Addressables 콘텐츠 빌드·디스크 팩 재생성·이미지 생성·커밋·푸시·사용자 Editor 종료·임의 씬 저장을 하지 않는다. 인코딩과 비교는 메모리/독립 검증 폴더에서 수행한다.
- 원본 레벨·제작 카탈로그가 씬/Resources/Addressables 의존성을 통해 배포에 들어가지 않는 검사와 생성 코드는 마련하되 실제 빌드/생성기를 실행하지 않는다.
- 새 패키지·asmdef·범용 정책 언어·생산 리플렉션·전면 MVVM을 도입하지 않는다. 보드/HUD/팝업/고물탑/튜토리얼 구현은 포함하지 않는다.

## Review Focus

1. 정의 ID와 본체 인스턴스 ID를 혼동해 발전기 연결이 끊어지는 경우: B/C에서 연결·중복·참조 검증.
2. 구형/신형 목록이 동시에 적용되어 요소가 두 번 배치되는 경우: B의 버전별 단일 원본 검사.
3. 같은 행동의 다른 ID가 저장만 되고 실제 규칙은 구형 종류를 사용하는 경우: B에서 두 신규 ID의 실제 배치·타격·미션·드론 조회 검사.
4. 원본 버전/DTO가 바뀌어 기존 팩 테스트나 다시하기가 깨지는 경우: C의 명시적 v1/v2 읽기·Asset/MemoryPack 동등 검사.
5. 필터·선택 갱신이 원본 저장/Undo 손실을 일으키는 경우: D의 무변경 조회·Undo/Redo·선택 복원·구독 해제 검사.

## A. 제작 정의와 배포 카탈로그

대상: `Assets/Scripts/Features/Elements/{Data,Runtime,Editor}/`, 기존 `ElementDefinition.cs`, `ElementCatalog.cs`, `LegacyElementMap.cs`, `LegacyElementDefinitions.cs`.

새 책임의 권장 파일: `Data/ElementDefinitionAsset.cs`, `Data/ElementCatalogAsset.cs`, `Data/PackedElementDefinition.cs`, `Editor/ElementCatalogValidation.cs`, `Editor/Tests/ElementAuthoringVerification.cs`. 실행 시 기존 기능과 겹치면 이름/분할을 조정하고 이유를 기록한다.

인터페이스: `ElementDefinitionAsset.ToDefinition()`은 불변 정의를 반환하고, `ElementCatalogAsset.CreateCatalog()`는 검증된 읽기 전용 카탈로그를 구성한다. 기존 `ElementCatalog.Get(ElementId)` 계약을 유지한다. 제작 분류/층은 콘텐츠 ID와 별개이며 기존 `PlacementLayer` 등 작은 의미 집합을 재사용한다.

- [x] 기존 행동으로 설명되는 서로 다른 ID 두 개를 제작 입력으로 구성하는 실패 검사를 확보한다. 중복/빈 ID, 누락 프로필, 잘못된 행동 조합을 정의 ID가 드러나는 오류로 거절한다.
- [x] 제작 원본→불변 정의→명시적 DTO 변환과 카탈로그 조회를 연결한다. 원본/입력 컬렉션 변경이 구성된 카탈로그를 바꾸지 않고, 표시명·정렬 변경도 ID를 바꾸지 않게 한다.
- [x] 동일 ID 충돌을 조용한 덮어쓰기나 종류 추론으로 숨기지 않는지 관련 검사를 실행한다. 새 아트나 수백 개의 실제 콘텐츠 제작은 하지 않는다.

## B. 버전별 배치와 선택 변환, 실제 ID 실행

대상: `Levels/Data/LevelDefinition.cs`, `Levels/Validation/LevelDefinitionValidator.cs`, `Obstacles/{Data,Rules,Editor/Application}/`, `PuzzlePlay/Runtime/{State,Setup}/`, 요소/미션/드론의 정의 조회 호출부.

새 책임의 권장 파일: `Levels/Data/ElementPlacementDefinition.cs`, `Elements/Editor/LevelElementMigration.cs`, `Elements/Editor/Tests/ElementLevelMigrationVerification.cs`.

인터페이스: 새 배치 값은 정의 ID·층·기준 좌표·본체 인스턴스 ID·현재 색/내구도/충전 등을 보유한다. 정의 수치와 파생 점유 칸은 중복 저장하지 않는다. `LevelElementMigration.Preview(...)`와 선택 적용을 분리한다. `LevelStateBuilder.Build(LevelDefinition, int, ElementCatalog)` 오버로드로 선택 정의를 실제 상태에 연결하고 기존 두 인자 호출은 구형 입력 호환 경계로 유지한다.

- [x] 구형6장애물·덮개/먼지·일반/파워 배치, 2×2 점유·연결·미션·고정/유지 공급이 원본을 바꾸지 않고 읽히는 사례와 잘못된 ID/참조의 실패 사례를 확보한다.
- [x] 스키마4와5의 원본 선택을 명시하고, 미리보기·선택 변환·Undo/Redo를 연결한다. 전체 에셋을 일괄 변환하거나 검사 중 자동 저장하지 않는다.
- [x] 실제 상태와 공통 규칙은 카탈로그에서 해결한 정의를 사용한다. 새로운 ID를 구형 종류로 바꾼 뒤 ID를 버리거나, 모든 ID를 enum으로 역변환해야만 실행되는 구조로 마무리하지 않는다.
- [x] 같은 행동의 서로 다른 두 ID가 배치·피해·제거 미션·드론 정책을 재사용하는 실제 실행을 검증한다. 구형 입력의 본체 ID·연결·효과 순서·난수 결과는 고정 입력으로 대조한다. 누락 정의를 일반 블록으로 대체하지 않는다.

## C. 팩2와 Asset/MemoryPack 실행 경로

대상: `Levels/Data/{PackedLevel,LevelPackCodec,LevelPackLoader}.cs`, `Levels/Editor/Persistence/{LevelPackBuild,LevelPackAddressablesBuilder}.cs`, `Levels/Editor/Tests/LevelPackVerification.cs`, `LevelEditor/Editor/Presentation/LevelEditorWindow.GameLaunch.cs`, 플레이 테스트/게임 진입 호출부.

새 책임의 권장 파일: `Levels/Data/PackedElementLevel.cs`, 버전1 읽기 DTO/어댑터, `Elements/Editor/Tests/ElementPackVerification.cs`. 기존 v1 타입/순서를 변형하지 않고 분리한다.

인터페이스: `LevelPackCodec.Encode(IEnumerable<LevelDefinition>, ElementCatalog)`는 구간별 정의 표를 포함한 팩2를 만든다. `ReadLevelWithCatalog(byte[], int)`는 임시 레벨과 해당 불변 카탈로그를 함께 반환한다. 기존 번호/주소/50레벨 구간 계약과 구형 읽기 진입점을 유지한다.

- [x] 레벨1/50/51/100/101 경계, 누락/중복 번호·정의·연결, 미지원 버전, 잘못된 구간, 공급/행동이 참조하는 정의 누락의 실패 검사를 먼저 확보한다.
- [x] v1/v2를 명시적으로 구분해 읽고 새 원본은 팩2로 인코딩한다. 배포 DTO와 실제 상태에 ScriptableObject 원본 참조가 남지 않게 한다. 원본 제외·선검증 후 생성 경로를 구현하되 `Generate`/`Prepare`/빌드는 실행하지 않는다.
- [x] 기존 디스크 팩은 그대로 두고 v1 읽기, 메모리에서 만든 v2, Asset 입력을 같은 고정 레벨/시드로 실행한다. 배치·교환·연쇄·공급·연결·미션·난수·다시하기를 비교하고 버전/ID 메타데이터와 실제 논리 차이를 분리한다. 원문을 보존하며 버전2의 바이트가 버전1과 같다고 요구하지 않는다.

## D. 카탈로그 검색·선택·검사의 MVVM 시범과 통합

대상: `LevelEditor/Editor/Presentation/LevelEditorToolPanel.cs`, `LevelEditorPlacementPanel.cs`, 보드 입력/속성/사용 목록과 기존 편집 응용 계층. 새 화면 상태 책임은 `Elements/Editor/Presentation/ElementCatalogViewModel.cs`와 얇은 View로 분리한다.

인터페이스: ViewModel은 검색어·층 필터·선택 `ElementId`·검사 결과·명령 가능 여부와 변경 통지만 소유한다. 실제 에셋 필드를 복사한 별도 저장 모델은 만들지 않는다. 배치/교체/삭제/속성 편집은 기존 SerializedObject/Undo 경로를 호출한다.

- [x] UI 없는 검색·선택·필터·검사 상태와 실제 UI에서의 선택 복원·검색 초기화·창 닫기/구독 해제 실패 검사를 확보한다.
- [x] 기존 툴 영역에 카탈로그 선택을 연결하고 층별 허용·같은 층 교체·2×2 이동/삭제·연결·사용 목록을 유지한다. 검색/선택만으로 원본이 dirty/저장되지 않게 한다.
- [x] 실제 편집기에서 정의 선택→배치→저장/재로드→Undo/Redo→플레이 테스트/게임 플레이를 확인한다. Asset과 MemoryPack 모두에서 선택한 맵/카탈로그를 사용하고 기존 보드 이미지·2단계 드론 연출을 유지한다.
- [x] 최종 변경에서 신규 검사와 관련 저장/편집/배치/미션/드론/게임 씬 회귀를 실행한다. 내부 체크포인트마다 전체 회귀를 반복하지 않는다. 기존30종·드론9종을 최종 논리 기준으로 확인하고 범위가 넓어진 실제 실패가 있을 때만 검증을 확대한다.

## 완료와 인계

증거는 `Logs/ElementFramework/Phase03`, 보고는 `Docs/Verification/MoonRabbitJunkyard/ElementFramework/phase-03-progress.md`에 둔다. 원래 출력/팩/에셋/GUID/과거 증거를 보존하고 실제 종료/FAIL/원문 비교를 기록한다. 변환 전 원본과 새 ID/버전의 추가 필드를 구분하며 기존 의미의 차이를 정규화해 숨기지 않는다.

완료 후 변경 결과·검증·제약을 보고하고 **4단계 표현·리소스·풀·HUD·공개 관찰**의 계획/목표/전체 복사용 명령문만 작성한다. 다음 구현은 시작하지 않는다. 구현 세부의 합리적인 조정은 기록하고 진행하되, 원본 일괄 변환·새 밸런스·외부 저장 계약 변경 등 목표 의미가 바뀌면 그 변경만 논의한다.

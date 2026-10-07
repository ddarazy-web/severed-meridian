# 레벨 튜토리얼 1단계 — 데이터·저장·레벨 에디터

> 실행 지침: superpowers:executing-plans로 현재 세션에서 직접 수행한다. 하위 에이전트와 자동 커밋은 사용하지 않는다.

**목표:** 레벨에 튜토리얼을 작성·검사·저장하고 MemoryPack에서 같은 값을 복원한다. 게임 내 진행과 안내 화면은 다음 단계다.

**구조:** 레벨이 제작 값을 소유하고 튜토리얼 기능이 정의/정적 검사를 담당한다. 팩2 DTO를 보존한 외부 팩3으로 전달한다. 편집은 기존 SerializedObject/Undo와 보드 선택을 사용한다.

**기술:** Unity6000.3.10f1, C#, MemoryPack, UI Toolkit Editor, 기존 Addressables와 ProjectTests.ps1.

**기준:** [기획](../../../Contents/MoonRabbitJunkyard/13_레벨튜토리얼.md) · [통합 계획](integration-guideline.md) · [목표](../../../Goals/MoonRabbitJunkyard/Tutorial/stage-01-data-editor-goal.md).

## 파일과 인터페이스 경계

- 신규 Assets/Scripts/Features/Tutorial/Data/LevelTutorialDefinition.cs와 TutorialStepDefinition.cs: 단계 목록·재현용 고정 시드·기존 ID 기반 고정 공급 목록, 네 단계 종류·설명·강조/행동 대상·아이템·생성/발동/제거 조건. 결과 대상은 기존 ElementId/좌표를 사용한다.
- 신규 Tutorial/Validation/LevelTutorialValidator.cs: Validate(LevelDefinition)에서 기존 List<LevelValidationIssue> 형식으로 단계 경로와 원인을 반환한다. 미래 상태 재생은2단계 책임이다.
- 신규 Tutorial/Editor/LevelTutorialEditorPanel.cs: 기존 단계 편집·선택 수명을 소유한다. 별도 범용 UI 프레임워크를 만들지 않는다.
- 수정 Assets/Scripts/Features/Levels/Data/LevelDefinition.cs: 읽기 전용 Tutorial 접근자, 제작 값과 복원 연결.
- 수정 같은 폴더의 LevelPackCodec.cs, LevelPackCodec.Elements.cs와 신규 LevelPackCodec.Tutorial.cs, PackedTutorialLevelPack.cs: 공통 읽기·Snapshot·팩3 봉투. PackedLevel/PackedElementLevel 직렬화 필드는 유지한다.
- 수정 Levels/Editor/Persistence/LevelPackBuild.cs와 Levels/Validation/LevelDefinitionValidator.cs:50레벨 출력·선검증과 정적 오류 연결.
- 수정 Assets/Scripts/Features/LevelEditor/Editor/Presentation/LevelEditorWindow.cs 및 Board/LevelBoardView.cs: 패널과 대상 칸 선택 연결.
- 조사 GameScreen/Editor/Launch/PuzzleEditorLaunchRequest.cs 및 Snapshot/Copy/JSON/입력 지문 호출부: 제작 메타데이터 보존. 필요한 호출부만 변경한다.
- 신규 Tests/Editor/Features/Tutorial: 데이터/에디터 검증. 기존 어셈블리와 namespace 규칙을 따른다.

세부 파일 분할은 실제 호출부에 맞춰 최소 조정하고 이 계획에 기록한다. 튜토리얼 단계 종류 외의 기존 블록/장애물 enum·행동 등록은 늘리지 않는다.

## A. 정의와 정적 검사

- [x] 현재 HEAD/WIP, 원본/GUID와 튜토리얼 없는 대표 레벨의 결과·지문·팩 바이트를 기록한다.
- [x] 제작 정의와 네 단계 종류·조건·고정 시드/공급을 추가한다. 빈 단계 목록은 튜토리얼 없음이다.
- [x] 정적 실패 사례를 먼저 확보한다. 범위 밖/비활성 칸, 비인접 교환, 벽, 잘못된 아이템 대상, 누락 ID, 부족한 이동 수, 무작위 튜토리얼 공급을 단계 경로와 함께 거절한다.
- [x] 최초 실제 조작 대상과 나중에 생성될 대상의 차이를 구분한다. 미래 파워가 처음부터 있어야 한다는 잘못된 검사를 추가하지 않는다.

## B. 저장과 왕복

- [x] 팩3에 기존 팩2 바이트와 레벨별 튜토리얼 DTO를 담는다. 구간·중복/누락 메타데이터·헤더/버전·잘린 데이터 오류를 거절한다.
- [x] 튜토리얼 고정 공급과 결과 조건에서 참조하는 정의 및 생성 참조도 팩의 정의 폐쇄에 포함한다. 초기 보드에 없다는 이유로 새 ID를 누락하지 않고 팩 복원 시 모든 참조를 검증한다.
- [x] 팩1/2는 튜토리얼 없이 읽는다. 튜토리얼 없는 입력의 바이트/지문을 유지하며 구형 쓰기로 정보를 버리는 경우를 거절한다.
- [x] Copy/Snapshot/JSON/실행 요청·기록·입력 지문을 감사한다. 튜토리얼 편집은 새 입력으로 식별되고 사본/재로드에서 보존된다.
- [x] 설명/조작/후속 생성 조건과 공급 전체 값,1/50/51/100/101 및 혼합 구간을 왕복 검사한다. 기존50레벨 주소와 요소 콘텐츠 팩을 유지하며 실제 출시 팩은 검사로 덮어쓰지 않는다.

## C. 에디터 연결

- [x] 단계 추가·삭제·정렬·설명·대상·조건·고정 시드/공급을 편집할 수 있게 한다.
- [x] 보드에서 강조·첫/둘째 교환 칸·아이템 대상을 선택하고 방향을 표시한다. 선택이 일반 배치/삭제나 자동 저장으로 처리되지 않게 한다.
- [x] SerializedObject/Undo로 편집하고 저장/재로드·Undo/Redo·선택 취소·레벨 전환·창 재생성과 구독 해제를 확인한다.
- [x] 단계별 정적 오류를 표시하고 실행/내보내기 전에 확인한다. 미래 상태 재생까지 검증했다고 표시하지 않는다.

## D. 검증과 인계

- [x] 소유한 시험 입력으로 실제 작성→저장/재로드→팩 복원→실행 요청 캡처를 확인한다. 자동 진행/안내 UI는 구현하지 않는다.
- [x] 관련 저장·편집·요소 콘텐츠/표현 검사, 테스트 연결 해제 상태의 게임 소스 컴파일과 최종 diff/GUID/원본 보존을 확인한다.
- [x] 실제 결과와 제약을 Docs/Verification/MoonRabbitJunkyard/Tutorial/stage-01-progress.md에 기록하고 목표를 갱신한다.
- [x] 다음2단계 계획·목표·전체 복사용 실행문을 작성하고 보고한다.2단계 구현은 자동 시작하지 않는다.

검사 진입점은 public static Run 계약의 Tutorial.Editor.LevelTutorialDataVerification.Run과 Tutorial.Editor.LevelTutorialEditorVerification.Run으로 작성한다. 저장소 루트에서 다음 형식으로 실행하고 실제 결과/로그 경로를 기록한다.

```powershell
./Tools/Testing/ProjectTests.ps1 -Action Run -Method Tutorial.Editor.LevelTutorialDataVerification.Run
./Tools/Testing/ProjectTests.ps1 -Action Run -Method Tutorial.Editor.LevelTutorialEditorVerification.Run
```

플레이어/번들 빌드·커밋·푸시·전체 레벨 자동 변환·출시 팩 덮어쓰기·사용자 Unity 종료·임의 씬 저장·2단계 구현은 하지 않는다. 실제 새 저장 계약의 ADR은 구현 검증과 함께 기록한다.

상태: 완료. 실제 확인 결과는 [1단계 검증](../../../Verification/MoonRabbitJunkyard/Tutorial/stage-01-progress.md)에 기록했다. 세부 조정: Board 뷰에는 입력 우선순위/표시를 위한 전용 partial을 추가했고, 진행 상태나 안내 UI는 구현하지 않았다. explicit 카탈로그를 실행 검사에도 전달한다.

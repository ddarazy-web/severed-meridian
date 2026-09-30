# 3단계 레벨 에디터에서 게임 실행 — 목표 및 완료 조건

상태: 2026-09-30 구현·검증 완료 (최종 자동 확인 202 PASS). [전체 계획](../../../Planning/MoonRabbitJunkyard/WorldGameScreen/2026-09-30-world-game-screen.md) · [2단계 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-02-progress.md).

## 목표

레벨 편집창에서 맵과 입력 소스를 선택하고 `게임 플레이`를 누르면, 해당 데이터의 독립 사본으로 `PuzzleGame` 씬을 실행한다. Unity Play Mode를 종료하면 원래 편집 환경으로 돌아온다. 기존 `플레이 테스트`는 유지한다.

## 구현 전 확인한 기반

- 편집창은 `LevelEditorWindow.CurrentLevel`을 보유한다. 기존 플레이 테스트 버튼은 드래그를 취소하고 `SerializedObject.ApplyModifiedProperties()` 후 `LevelInitialStateWindow.OpenManualLevel(level)`을 호출한다.
- `PuzzleGameSession`은 현재 레벨 번호로 `LevelPackLoader.LoadAsync`를 호출한다. 임의의 편집 사본을 전달하는 공개 진입점은 아직 없다.
- `LevelPackCodec.Encode/ReadLevel`로 직렬화 왕복하면 독립된 레벨을 만들 수 있다. `ToPacked()`와 `FromPacked()`만 연결하면 내부 List/Board 참조를 공유하므로 깊은 사본이 아니다.
- 기존 편집기 MemoryPack 테스트는 `LevelPackBuild.FilePath(number)`의 마지막 생성 파일을 읽는다. 빌드 시에는 기존 Addressables 팩 로딩을 유지한다.
- Play 종료 후 편집 이미지가 사라지던 결함은 `LevelBoardArtwork`의 모드 전환 캐시 해제로 수정했다. `BoardArtworkLifecycleVerification`의 실제 왕복 검사를 보존한다.

## 사용자 흐름과 데이터 계약

1. 편집 탭에 기존 버튼과 구분되는 `게임 플레이`, `게임 입력` 선택(에셋 / MemoryPack), `게임 시드`를 둔다. 기본 입력은 에셋, 시드는 12345다. 실행 소스와 시드는 기존 플레이 테스트 설정과 독립적이며 창 재컴파일 후에도 유지한다.
2. 에셋 모드는 클릭 시 드래그를 취소하고 입력 중인 직렬화 속성을 적용한 다음, 현재 메모리의 레벨을 직렬화하여 사본을 만든다. 미저장 값도 반영하되 `SaveAssets` 등으로 원본 파일을 자동 저장하지 않는다.
3. MemoryPack 모드는 선택 레벨의 번호로 마지막 생성 `.bytes` 파일을 읽는다. 에셋의 미저장 값은 포함하지 않는다. `마지막 생성 MemoryPack 사용` 안내와 선택 번호/소스/시드를 표시한다. 실행 클릭으로 팩이나 Addressables를 자동 재생성하지 않는다. 갱신은 기존 플레이 테스트의 `MemoryPack 갱신` 기능을 이용한다.
4. 두 모드 모두 실행 버튼을 누른 시점의 bytes/번호/시드를 고정한다. 실행 직전 추가 편집이나 팩 파일 변경이 진행 중 요청에 섞이지 않는다. 전달용 bytes는 Editor의 `SessionState`에만 일시 보관하며 Assets/Resources/Addressables에 새 레벨 에셋을 만들지 않는다.
5. 컴파일 중, Play 진입/실행/종료 중, 이미 요청이 대기 중일 때 중복 실행을 막는다. 선택 레벨 없음, 잘못된 번호, 누락/손상 팩, 해당 번호 누락, 게임 씬 누락이면 편집창에 이유를 표시하고 진입하지 않는다. 다른 레벨이나 에셋 모드로 조용히 대체하지 않는다.
6. 실제 시작 조건 탐색과 규칙 검사는 기존 `StartingBoardSearch`/`BoardActionExecutor`를 사용한다. 시작판은 재배치될 수 있으므로 화면과 편집 배치가 항상 같다고 가정하지 않고 동일 입력 사본·시드의 기준 결과와 비교한다.
7. `EditorSceneManager.playModeStartScene`으로 이번 실행의 시작 씬만 지정한다. 편집 중인 씬을 `OpenScene(...Single)`로 닫거나 저장하지 않는다. 기존 시작 씬 override가 있으면 보관하고 성공/실패/취소 후 복원한다. Unity가 저장 확인을 표시하면 취소 선택을 존중한다.
8. 도메인 리로드를 거쳐 `PuzzleGame`의 `sceneLoaded` 시점에 요청을 한 번만 소비하고 세션을 초기화한다. 세션의 기본 `Start` 초기화와 경쟁하지 않는다. 요청 없이 게임 씬을 직접 실행하면 기존 MemoryPack 레벨 1/시드 12345 기본 경로가 유지된다.
9. 실행 사본의 소유권은 세션에 넘긴 뒤 세션이 반환한다. 세션에 넘기기 전 실패하면 Editor 실행기가 반환한다. 로드 중 종료, 빠른 재실행, 취소에서도 사본·아틀라스·이벤트가 남지 않는다.
10. Play Mode 종료 후 기존 씬 구성, 레벨 선택, 편집값과 dirty 상태를 보존하고 편집 탭에 포커스를 돌린다. 편집창을 사용자가 닫았다면 강제로 다시 만들지 않는다. 종료 후 편집 보드 이미지는 재컴파일이나 데이터 재로드 없이 복구돼야 한다.
11. 요청 bytes와 진행 상태는 소비/종료/취소 시 정리한다. 이전 요청이 다음 일반 Play 실행으로 넘어가지 않는다. 전체 Editor 종료/비정상 종료 후 작업 복구는 이번 범위가 아니다.

## 산출물

- 편집창 실행 컨트롤과 Editor 전용 요청/씬 전환 코드.
- 기존 `PuzzleGameSession`의 독립 레벨 사본 초기화 진입점. 런타임 코드에는 UnityEditor 의존성을 넣지 않는다.
- 실제 Editor Play Mode 진입·복귀 자동 검사 및 실제 버튼 조작 화면.
- `Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-03-progress.md`: 변경·실패/수정·검사 결과·캡처·미검증 사항.
- `Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-03-editor-launch-usage.md`: 소스 차이, 실행/종료, 팩 갱신과 오류 확인 방법.

## 완료 조건

- [x] Unity 컴파일 오류가 없고 기존 플레이 테스트 버튼이 그대로 동작한다.
- [x] 서로 다른 두 레벨을 에셋/MemoryPack 두 모드로 실행하여 선택 번호·시드·원본 데이터 전달을 확인한다.
- [x] 에셋 모드의 미저장 편집값은 반영되고 MemoryPack 모드에서는 마지막 생성 파일 값이 반영된다.
- [x] 입력 사본이 원본과 Board/List 참조를 공유하지 않으며 플레이 후 원본 파일 해시·메모리 편집값·dirty 상태가 보존된다.
- [x] 도메인 리로드를 사용한 실제 실행에서 세션 초기화가 한 번만 발생한다. 리로드를 끈 경우도 기존 설정을 복구하면서 검사한다.
- [x] 누락/손상/번호 불일치와 취소는 설명 가능한 실패로 처리하고 원래 씬·시작 씬 override를 보존한다.
- [x] 연타·종료 중 재실행·로드 중 종료 후 요청과 사본이 남지 않으며 다음 일반 Play에 이전 요청이 적용되지 않는다.
- [x] 편집 중 씬과 미저장 레벨을 보존한 채 복귀하고, 편집 보드 이미지가 자동 복구된다.
- [x] 사본으로 실제 교환→생성 파워 이미지→발동까지 동작하고 2단계 검사 및 편집 이미지 수명 회귀를 통과한다.
- [x] 씬/프리팹/Resources/Addressables에 LevelDefinition 직접 참조가 추가되지 않고 50레벨 팩·아틀라스 분할이 유지된다.
- [x] 실제 버튼 진입·게임 조작·복귀 캡처와 사용 문서가 있다. 미검증 플랫폼은 명시한다.

## 제외 범위

4단계 목업 HUD·아이템·일시정지·다시하기, 상세 애니메이션, 새 이미지, 규칙 변경, 전체 플레이어 빌드/실기기 검증은 제외한다. 프리팹 추가는 필요하지 않으며 기존 Game/UI 구분과 GUID를 유지한다. 자동 커밋하지 않고 3단계 완료 후 멈춘다. 이 문서 작성만으로 목표를 활성화하거나 구현하지 않는다.

# 1단계 월드 보드 표시 Implementation Plan

> 2026-10-01 변경: 현재 보드는 **9×9(81칸)**이다. 아래 10×10·100칸 계획과 검증 수치는 당시 기록으로 보존하며 9×9 검증 결과가 아니다. 현재 크기·표시·데이터 전환 기준은 [9×9 보드 결정](../../../Decisions/MoonRabbitJunkyard/2026-10-01-nine-by-nine-board.md)을 따른다.

> **For agentic workers:** `superpowers:executing-plans`를 사용하여 아래 작업을 순서대로 수행한다. 이 문서는 하위 에이전트 실행을 요구하지 않는다.

**Goal:** 기존 레벨을 Addressables 이미지로 월드 공간에 표시하는 독립 게임 씬을 완성한다.

**Architecture:** 기존 LevelRuntimeState를 표시 입력으로 사용한다. 이미지 로딩, 보드 렌더링, 씬 진입, Editor 에셋 생성을 분리하고 기존 게임 실행기는 수정하지 않는다.

**Tech Stack:** 프로젝트에 설치된 Unity, C#, SpriteRenderer, Addressables, UniTask. 패키지를 새로 추가하지 않는다.

**Spec:** [1단계 목표 및 완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-01-world-board-goal.md)

## 공통 제약

ServeredMeridian만 작업한다. 게임 프리팹은 `Assets/Prefabs/Game/Puzzle` 아래 생성한다. UI는 이번 단계에서 만들지 않는다. 기존 아틀라스 분할 및 50레벨 MemoryPack을 유지한다. 에셋 생성은 Unity Editor API를 사용하고 기존 GUID를 보존한다. 기존 씬을 덮어쓰거나 기본 실행 씬/빌드 목록을 임의로 교체하지 않는다. 다음 경로는 신규 산출물의 계획이며, 실행 시작 시 동명 파일이 생겼다면 먼저 확인한다.

## 주요 검토 지점

1. 초기 맵에 없는 파워 종류의 사후 표시 — 작업 2, 5에서 검사.
2. 2×2 장애물의 중복과 레이어 순서 — 작업 3, 5에서 검사.
3. 로드 중 씬 종료와 재진입 — 작업 2, 4, 5에서 검사.
4. 잘못된 레벨/누락 팩을 다른 맵으로 대체하는 문제 — 작업 4에서 오류 확인.
5. 비활성 칸·벽·포털·연결선 좌표 — 작업 3, 5에서 대표 레벨 비교.

## 작업 1 — 계약과 검증 입력 확인

- [x] 프로젝트 규칙과 `Docs/Contents/MoonRabbitJunkyard/06_플랫폼과화면.md`, `Mockups/preview.png`를 확인한다. HTML의 대용량 내장 폰트는 전체 출력하지 않는다.
- [x] 아래 실제 API와 현재 이미지 이름을 확인한다.
  - `Assets/Scripts/Features/Board/Runtime/BoardSpriteAtlas.cs`
  - `Assets/Scripts/Features/Board/Data/BoardEdge.cs` — 필드는 `A`, `B`다.
  - `Assets/Scripts/Features/LevelEditor/Editor/Presentation/Board/LevelBoardArtwork.cs`
  - `Assets/Scripts/Features/PuzzlePlay/Runtime/Setup/LevelStateBuilder.cs`
  - `Assets/Scripts/Features/Levels/Data/LevelPackLoader.cs`
  - `Assets/Scripts/Features/Board/Editor/BoardAtlasPrebuild.cs`
- [x] `Assets/Data/Levels/Level_01.asset` 및 대응 MemoryPack의 존재와 유효성을 확인한다. 검증용 추가 상태는 원본 수정 없이 기존 검증 패턴으로 구성한다.
- [x] 대표 이미지/내구도, 비활성 칸, 2×2, 벽·포털·배선, 사후 파워 생성에 대한 검증 사례를 정한다.

완료 증거: 사용할 레벨 번호, 실제 팩 경로, 이미지 매핑과 검증 사례 목록.

## 작업 2 — 이미지 매핑과 아틀라스 수명주기

신규: `Assets/Scripts/Features/GameScreen/Runtime/World/PuzzleArtwork.cs`

계획 인터페이스: `UniTask PrepareAsync(LevelRuntimeState state, CancellationToken cancellationToken)`, `Sprite Get(string relativePath)`, `void Dispose()`.

- [x] 기존 Editor 표시와 일치하는 런타임 이미지 매핑을 구성한다. Editor 타입을 런타임에서 참조하지 않는다.
- [x] BoardSpriteAtlas를 재사용해 필요한 종류만 로드하고, 모든 기본 파워 종류를 사후 표시할 수 있도록 준비한다.
- [x] 소유자가 로드 완료 전에 파괴되는 경우를 처리한다. 기존 로더의 취소 지원 여부를 확인하여 핸들을 이중 해제하지 않는다.
- [x] 대표 경로가 실제 스프라이트로 해석되는 검사와 로켓 추가 상태 검사를 수행한다. 누락 시 경로가 드러나는 오류를 제공한다.

완료 증거: 매핑 검사 결과와 로드/종료 검사 결과.

## 작업 3 — 월드 표시와 프리팹 생성

신규: `Runtime/World/PuzzleCellView.cs`, `Runtime/World/PuzzleWorldBoard.cs`, `Editor/PuzzleGameAssets.cs` — 모두 `Assets/Scripts/Features/GameScreen/` 기준.

계획 인터페이스: `PuzzleWorldBoard.Draw(LevelRuntimeState state, PuzzleArtwork artwork)`는 기존 표시를 갱신한다. 입력이나 게임 규칙을 실행하지 않는다. `PuzzleGameAssets.Generate()`는 Editor에서 프리팹/씬을 생성한다.

- [x] 셀 중심과 레이어 순서를 정의하고 SpriteRenderer로 활성 칸만 표시한다.
- [x] 큰 장애물은 기준 칸에서 한 번만 생성한다. 제거·내구도 변경 후 재표시도 처리한다.
- [x] 벽은 두 칸의 경계, 포털/회수 장치는 지정 칸, 연결선은 정의된 경로에 표시한다.
- [x] `Assets/Prefabs/Game/Puzzle/PuzzleCell.prefab`, `PuzzleObstacle.prefab`, `PuzzleDecoration.prefab`, `PuzzleWorldBoard.prefab`을 Unity API로 생성하고 참조를 연결한다. 기능상 불필요한 프리팹은 만들지 않는다.
- [x] 비활성 칸과 반복 Draw에서 중복 표시가 없는지 확인한다.

완료 증거: 정상 연결된 프리팹과 대표 상태 화면.

## 작업 4 — 독립 씬의 정적 레벨 로드

신규: `Assets/Scripts/Features/GameScreen/Runtime/World/PuzzleBoardPreview.cs`, `Assets/Scenes/PuzzleGame.unity`.

- [x] 레벨 번호로 기존 LevelPackLoader를 호출하고 LevelStateBuilder.Build의 검증 결과를 확인한다. 이 단계의 정적 배치는 플레이 가능한 시작 조건 보장을 의미하지 않는다.
- [x] 로드 → 상태 구성 → 이미지 준비 → Draw 순서로 연결한다. 실패하면 원인을 출력하고 다른 레벨로 자동 대체하지 않는다.
- [x] 카메라를 설정해 10×10 논리 영역이 보이도록 한다. 목업 HUD를 미리 구현하지 않는다.
- [x] 실행 종료 시 임시 LevelDefinition과 이미지 리소스를 정리한다.
- [x] 누락 팩, 잘못된 레벨, 로드 중 종료, 정상 재진입을 검사한다. 팩/아틀라스가 없을 때 기존 생성 절차를 사용하고 생성 변경 내역을 확인한다.

완료 증거: 씬 단독 실행 및 실패 경로 로그. 레벨 에셋 직접 참조 없음 확인.

## 작업 5 — 완료 검증과 인계

신규 검증이 필요한 경우 `Assets/Scripts/Features/GameScreen/Editor/Tests/PuzzleWorldBoardVerification.cs`에 기존 프로젝트 검증 패턴을 적용한다. 새 테스트 프레임워크는 설치하지 않는다.

- [x] Unity 컴파일 결과를 확인한다. 배치 실행 시 프로세스 시작 성공과 컴파일 성공을 구분한다.
- [x] 목표 문서의 완료 조건을 실제 실행으로 확인한다. 자동화 가능한 상태/참조 검사는 자동화하고 시각 품질은 캡처로 확인한다.
- [x] 기존 레벨 편집 보드와 플레이 테스트에 관련된 좁은 회귀 검증을 수행한다.
- [x] `Logs/WorldBoardVerification/`에 로그와 캡처를 보관하고, 이 문서에 실행 환경·결과·남은 제약을 기록한다. 해당 경로가 Git 제외라면 최종 인계에서 로컬 자료임을 명시한다.
- [x] 변경 파일과 완료 조건을 대조한다. 통과하지 않은 항목을 체크하지 않는다. 2단계는 시작하지 않는다.

## 실행 결과

2026-09-30 완료. Unity 6000.3.10f1 / URP 17.3.0 / Addressables 4.1.0의 실제 번들 Play Mode에서 174개 검사 PASS, 실패 0. 기존 편집 보드·플레이 테스트 회귀 검사 22개 PASS. 최종 로그: Logs/WorldBoard-final-verification.log 및 Logs/WorldBoardVerification/results.txt. 목표 조건별 증거와 남은 범위는 stage-01-progress.md, 실행 방법은 stage-01-world-board-usage.md를 참조한다. 모바일 실기기/플레이어 빌드는 미검증이며 2단계는 시작하지 않았다.


# 스크립트 구조

기능을 먼저 찾고, 해당 기능 안에서 역할을 찾습니다. 공통 게임 실행 코드는 실제 게임과 에디터 플레이 테스트가 함께 사용합니다.

| 기능 폴더 | 담당 내용 |
|---|---|
| Board | 칸·좌표·보드 데이터, 활성 칸과 초기 블록 편집 |
| Levels | 레벨 저장 데이터, 전체 검사, 파일 생성·이름 변경·복제·형식 전환 |
| Matching | 매칭 패턴 조회, 겹친 패턴 우선순위와 파워 생성 위치 판정 |
| PowerBlocks | 파워 효과, 조합, 드론 목표 조회와 예약 |
| Obstacles | 장애물·덮개·먼지 데이터와 배치, 피격·곰팡이·발전기 처리 |
| BoardFlow | 중력·낙하 경로·통로·장치 연결, 낙하 정착 |
| BlockSupply | 생성구 데이터, 고정 공급 목록·유지 수량과 편집 |
| Missions | 미션 데이터·진행, 부품 회수 |
| PuzzlePlay | 실행 상태, 시작 보드 구성, 교환·아이템·연쇄·종료 처리 |
| LevelEditor | 통합 작업창, 배치 도구와 보드 표시, 선택·컨텍스트 메뉴 |
| ShapeCatalog | 사용자가 등록한 맵 모양, 중복 검사와 장애물 사용 기록 |
| PlayTesting | 에디터의 수동 플레이·초기 보드 진단 화면과 통합 검증 |

## 기능 내부의 역할

필요한 역할만 만듭니다. 각 기능에 모든 폴더가 있어야 하는 것은 아닙니다.

- `Data`: 저장하는 값과 데이터 형식.
- `Rules`: 레벨 구성의 규칙과 판정.
- `Validation`: 레벨 전체의 오류 검사와 검사 결과.
- `Runtime`: 실제 게임과 테스트가 공유하는 실행 처리.
- `Editor`: Unity 에디터에서만 사용하는 코드.
  - `Application`: 편집 명령의 검증과 데이터 반영.
  - `Presentation`: 화면, 입력, 보드 표시. `Board` 하위 폴더에는 보드 위의 표시와 연결 입력이 있습니다.
  - `Persistence`: 에셋 저장·복제·이름 변경·형식 전환.
  - `Inspection`: Unity Inspector와 필드 표시.
  - `Styles`: 편집기 스타일 파일.
  - `Tests`: 기존 검증 코드. `Levels/Editor/Tests/Fixtures`는 여러 검사가 공유하는 이전 형식의 저장 자료입니다.

`PuzzlePlay/Runtime`은 역할을 더 구분합니다. `State`는 실행 상태와 난수, `Setup`은 시작 보드 구성, `Actions`는 사용자 행동과 후속 처리를 담당합니다.

## 파일을 찾는 예

- 맵 모양 등록: [ShapeCatalog/Editor/Application/LevelShapeRecommendations.cs](ShapeCatalog/Editor/Application/LevelShapeRecommendations.cs)
- 맵 모양 저장 형식: [ShapeCatalog/Editor/Data/LevelShapePreset.cs](ShapeCatalog/Editor/Data/LevelShapePreset.cs)
- 통합 작업창: [LevelEditor/Editor/Presentation/LevelEditorWindow.Workspace.cs](LevelEditor/Editor/Presentation/LevelEditorWindow.Workspace.cs)
- 매칭 우선순위: [Matching/Runtime/MatchResolution.cs](Matching/Runtime/MatchResolution.cs)
- 드론 목표: [PowerBlocks/Runtime/DroneTargetManager.cs](PowerBlocks/Runtime/DroneTargetManager.cs)
- 낙하: [BoardFlow/Runtime/SettlementResolution.cs](BoardFlow/Runtime/SettlementResolution.cs)

## 유지할 경계

에디터 전용 C#은 반드시 `Editor` 폴더 아래에 둡니다. 검증 코드도 `Editor/Tests` 안에 두어 플레이어 빌드에 들어가지 않게 합니다. 이번 정리에서는 `.meta` GUID, 타입 이름, 네임스페이스와 Unity 기본 어셈블리를 유지했습니다. 저장된 레벨·모양 에셋의 위치도 바꾸지 않았습니다.

`LevelEditorWindow`의 partial 파일은 통합 창의 화면을 함께 구성하므로 `LevelEditor/Editor/Presentation`에 모았습니다. 모양 등록 처리나 공급 편집처럼 기능에 속하는 데이터 처리 코드는 각 기능의 `Editor/Application`에 있습니다. 파일 이동만으로 기존 의존 관계가 새로 분리된 것은 아닙니다.

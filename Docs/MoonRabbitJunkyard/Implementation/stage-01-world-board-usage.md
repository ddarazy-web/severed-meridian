# 월드 보드 미리보기 사용 방법

## 실행

1. ServeredMeridian Unity 프로젝트에서 `Assets/Scenes/PuzzleGame.unity`를 연다.
2. Hierarchy의 `Puzzle Board Preview`를 선택한다.
3. `Level Number`를 기존 MemoryPack에 들어 있는 레벨 번호로 지정한다. 기본값은 1이다. `Seed`는 정적 배치의 난수 시드다.
4. Unity Play를 누른다. 선택한 번호의 MemoryPack 데이터를 읽어 월드에 표시한다.

이 단계에서는 블록을 누르거나 교환할 수 없다. 에디터의 게임 플레이 버튼은 3단계, HUD와 아이템 UI는 4단계에 연결한다. 기존 플레이 테스트는 계속 기존 도구에서 사용한다.

## 데이터와 리소스

- 레벨 입력: `LevelPackLoader` → `Levels/levels-000001` 등의 50레벨 구간 Addressable → 임시 LevelDefinition → LevelRuntimeState.
- 현재 첫 팩: `Assets/Data/LevelPacks/levels-000001.bytes`.
- 이미지: 기존 `MoonRabbitBoard-*` Addressables 아틀라스. PNG별 런타임 로드는 하지 않는다.
- 에디터 Play Mode의 Addressables 모드 선택은 그대로 따른다. 실제 번들로 시험하려면 Addressables의 기존 빌드 사용 모드를 선택하고 해당 콘텐츠가 빌드되어 있어야 한다.
- 번호 오류, 없는 팩/레벨, 이미지 누락은 Console에 원인을 출력한다. 다른 맵이나 색 블록으로 자동 대체하지 않는다.
- 작성 에셋을 수정한 뒤에는 기존 MemoryPack 생성 절차로 갱신해야 이 씬에 반영된다. 이 씬은 미저장 편집 내용을 직접 읽지 않는다.

## 산출물

| 위치 | 역할 |
| --- | --- |
| `Assets/Scenes/PuzzleGame.unity` | 전용 카메라, 레벨 로더, 보드 인스턴스 |
| `Assets/Prefabs/Game/Puzzle/PuzzleWorldBoard.prefab` | 셀·본체·장치 표시의 소유자 |
| `Assets/Prefabs/Game/Puzzle/PuzzleCell.prefab` | 바닥·먼지·내용물·덮개 레이어 |
| `Assets/Prefabs/Game/Puzzle/PuzzleObstacle.prefab` | 1×1/2×2 장애물 이미지 |
| `Assets/Prefabs/Game/Puzzle/PuzzleDecoration.prefab` | 벽·포털·배선·단자·회수 도착점 |
| `Assets/Scripts/Features/GameScreen/Runtime/World` | 경로 매핑, 아틀라스 수명주기, 렌더링, 정적 미리보기 |
| `Assets/Scripts/Features/GameScreen/Editor` | Unity API 기반 생성과 검증 |

프리팹 재생성 메뉴는 `Tools > Match > 월드 보드 프리팹 생성`이다. 생성기는 기존 PuzzleGame 씬의 사용자 배치를 덮어쓰지 않는다. 대화형 에디터에서는 이름 없는 편집 씬을 먼저 저장한 뒤 생성한다.

## 검증 자료

`Logs/WorldBoardVerification/` 아래 `results.txt`, `level-01.png`, `representative-landscape.png`, `representative-portrait.png`를 기록한다. Logs는 Git 제외 경로이므로 다른 PC로 전달할 때는 따로 복사한다.

자동 검증 진입점:

```text
GameScreen.Editor.PuzzleArtworkVerification.Run
GameScreen.Editor.PuzzleWorldBoardVerification.Run
Levels.Editor.RabbitArtworkVerification.Run
```

이 메서드들은 배치 검증 후 Unity 프로세스를 종료하므로 작업 중인 대화형 에디터에서 직접 호출하지 않는다. 프로젝트를 다른 Unity 프로세스에서 열고 있지 않을 때 전용 배치 프로세스로 실행한다. 비동기 검사에는 `-quit`를 붙이지 않는다.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Projects/Git/ServeredMeridian' -executeMethod GameScreen.Editor.PuzzleWorldBoardVerification.Run -logFile 'C:/Projects/Git/ServeredMeridian/Logs/WorldBoard-bundles.log'
```

검증기는 Addressables Play Mode를 기존 번들 사용으로 일시 변경하고 완료 시 원래 설정을 복원한다. 레벨 에셋과 빌드 씬 목록을 변경하지 않는다. 최종 성공 여부는 최신 results.txt와 해당 실행 로그를 함께 확인한다.

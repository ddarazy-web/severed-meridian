# 13단계 사용 안내 — 승리 후 다음 레벨

상태: 2026-10-02 구현·Editor 검증 완료. 단일 독립 리뷰 수정과 최종 gate를 통과했다. 조건별 증거와 미검증 한계는 [검증 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-13-progress.md)을 따른다.

## 플레이

1. 레벨 에디터에서 맵을 선택하고 게임 실행 입력을 **MemoryPack**으로 선택한다.
2. `게임 플레이`로 시작한다. 실제 승리와 라스트팡·수집 표시가 끝나면 `다음 레벨`이 나타난다.
3. 버튼을 누르면 현재 승리 화면에서 다음 번호의 데이터를 준비한다. 이때 보드·아이템·다시하기·추가 클릭이 차단된다.
4. 준비가 끝나면 같은 시드로 새 판을 시작한다. `다시하기`는 이제 새 레벨의 시작 상태로 돌아간다.

**Asset** 입력은 클릭 시점의 미저장 사본을 테스트한다. 승리 화면에는 MemoryPack 안내와 다시하기만 나타나며, 다시하기도 같은 사본을 사용한다. 게임을 종료하면 기존 편집 흐름으로 돌아간다.

현재 원본 MemoryPack에는 1레벨만 있다. 다음 번호를 포함한 팩이 준비돼 있지 않으면 오류 안내가 나타나는 것이 정상이다. 번호를 건너뛰거나 누락을 마지막 레벨이라고 판단하지 않는다. 필요한 팩 갱신은 에디터의 기존 MemoryPack 갱신 기능을 사용자가 실행한다. 게임이 원본 에셋/팩을 저장하거나 갱신하지 않는다.

## 오류와 취소

다음 데이터·아틀라스·필수 미션 그림을 불러오지 못하면 현재 승리·레벨 번호·시드·다시하기 기준을 유지한다. 안내를 확인한 뒤 다음 버튼으로 재시도하거나 현재 판을 다시 시작할 수 있다.

background에서는 후보를 준비하더라도 복귀 전에는 새 판으로 교체하지 않는다. 준비 중 종료/취소하거나 세션을 비활성화하면 후보 자원을 반환한다. 비활성 상태에서 취소된 화면을 다시 켜면 Next/Retry 잠금이 해제되고 기존 판을 재시작할 수 있다. 준비 중 UI 입력 잠금과 기존 일시정지/복귀 동작은 별도로 검사한다.

50레벨씩 나뉜 팩에서 50→51은 기존 Codec의 다음 주소를 사용한다. 현재 검사는 임시 팩과 Editor AssetDatabase Addressables provider를 사용했으며, 배포된 실제 번들은 빌드/로드하지 않았다.

## 빌드 없는 재검사

ServeredMeridian Unity를 사용 중이지 않을 때 프로젝트 루트에서 실행한다. 스크립트는 숨김 검사 프로세스를 실행하며 사용자 에디터를 강제로 닫지 않는다. 각 실행은 새 결과와 실제 종료 코드를 확인한다.

```powershell
& pwsh.exe -NoProfile -File Logs/Stage13/run-check.ps1 -Method Data -Label data
& pwsh.exe -NoProfile -File Logs/Stage13/run-check.ps1 -Method RunScene -Label transition -Result scene-results.txt
& pwsh.exe -NoProfile -File Logs/Stage13/run-check.ps1 -Method RunBoundaries -Label boundaries -Result scene-results.txt
& pwsh.exe -NoProfile -File Logs/Stage13/run-check.ps1 -Method RunAssetScene -Label asset -Result scene-results.txt
& pwsh.exe -NoProfile -File Logs/Stage13/run-check.ps1 -Method RunLoss -Label loss -Result scene-results.txt
& pwsh.exe -NoProfile -File Logs/Stage13/run-check.ps1 -Method RunProbe -Label inset-probe -Result scene-results.txt
& pwsh.exe -NoProfile -File Logs/Stage13/run-check.ps1 -Method RunEditorLifecycle -Label editor-lifecycle -Result editor-lifecycle-results.txt
& pwsh.exe -NoProfile -File Logs/Stage13/run-check.ps1 -Method RunRegression -Label regression -Result scene-results.txt
& pwsh.exe -NoProfile -File Logs/Stage13/run-final-suite.ps1
```

`RunScene`은 실제 버튼→다음 판→한 행동→Retry와 승리/준비/오류 네 해상도를 확인한다. `RunBoundaries`는 팩 로드 중/후보 준비 후 취소·캐시 반환·파일/아틀라스 오류·50→51·세션 파괴를 검사한다. 임시 자원은 각 검사에서 소유하고 정리한다. `RunAssetScene`과 `RunEditorLifecycle`은 실제 launcher와 편집 왕복을 사용한다.

검사 코드는 Assets/Scripts/Features/GameScreen/Editor 아래에 있고 런타임에 Editor API를 추가하지 않았다. UI 프리팹은 Assets/Prefabs/UI/Puzzle, 월드 프리팹은 Assets/Prefabs/Game/Puzzle의 기존 분리를 유지한다. 게임 화면 전체를 재생성하는 제작 메뉴는 이번 검사 명령에 포함하지 않는다.

실제 청취와 실기기 검증은 수행하지 않았다. Editor의 재생/예약·화면·입력 검사는 음질·기기 성능·물리 notch 검증을 대신하지 않는다. 빌드·저장/해금·광고/과금·진동·커밋/푸시는 이번 범위에 포함하지 않는다.

## 기존 전체 UI 제작 도구

`Tools/Match/목업 UI 프리팹 연결`은 이전 단계의 전체 제작 도구로, 다시 실행하면 결과 팝업이 Retry 전용으로 재생성된다. 이번 단계는 전체 재생성을 하지 않았으며 해당 도구와의 호환 완료를 주장하지 않는다. 이미 제작 도구를 실행했다면 사용자 에디터를 종료한 상태에서 소유한 검사 프로세스로 `GameScreen.Editor.PuzzleLevelTransitionAssets.Apply`를 실행해 기존 결과 팝업에 다음 버튼만 다시 연결할 수 있다. 이 진입점은 작업 완료 후 검사 에디터를 종료하므로 사용 중인 에디터에서는 호출하지 않는다.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Projects/Git/ServeredMeridian' -executeMethod GameScreen.Editor.PuzzleLevelTransitionAssets.Apply -logFile 'C:/Projects/Git/ServeredMeridian/Logs/Stage13/popup-reapply.log'
```

검증용 레벨과 일부 그림을 제외한 임시 아틀라스는 소유한 검사에서만 생성·정리한다. 실제 배포용 아틀라스는 기존 Addressables·prebuild 구조를 유지한다.

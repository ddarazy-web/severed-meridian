# 11단계 반복 플레이·성능 안정화 사용 안내

상태: 11단계 완료. 독립 리뷰 결과음 결함 수정·재현 검사 및 전체15개 회귀 통과. [실행 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-11-progress.md)과 [목표](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-11-stability-goal.md)를 확인한다.

## 게임 실행

ServeredMeridian 레벨 에디터에서 맵과 Asset/MemoryPack 입력을 선택하고 `게임 플레이`를 실행한다. 준비 후 교환·파워·아이템을 사용한다. 기존 게임 화면의 일시정지·재개·다시하기를 사용한다. 이번 작업에서는 새 게임 기능·설정 창·진동을 추가하지 않는다.

표시 중에는 기존 입력 잠금이 유지되고 완료 후 입력 또는 결과 화면으로 복귀한다. 회전하면 진행 중 제스처와 아이템 선택은 기존 정책대로 취소되며 선택 블록의 그리기 순서가 복원된다. 회전 전의 손가락 위치로 새 교환을 확정하지 않는다.

소리는 pause에서 정지·재개하고 앱 background에서는 과거 음성/예약을 버린다. 결과 표시가 백그라운드에서 완료됐더라도 복귀 후 미소비 결과음은 한 번 재생한다. 이미 재생한 결과음은 화면 갱신·재복귀에서 중복하지 않는다. pause가 겹쳤다면 일시정지를 재개한 뒤 전달한다. 다시하기는 이전 표시/수집/예약을 정리하고 새 보드를 준비한다.

## 이미지·소리 재사용

보드 cells/bodies/decorations/supplyImages/supplyClips/effects, HUD 수집 이미지, 8음성/14클립을 재사용한다. 더 많은 동시 표시가 필요한 맵에서는 정상적으로 풀 용량이 늘 수 있다. 같은 시드·행동의 5회 워밍업+5회 반복에서 계속 늘어나는지와 종료 후 활성 잔류를 따로 검사한다. 풀 용량이 0으로 줄어드는 것이 정상 재사용 조건은 아니다.

아틀라스는 해당 맵/공급·파워 연출에 필요한 종류를 로드한다. 준비 중 종료해도 실제 pending이 완료되기 전 핸들을 강제로 해제하지 않고 완료 뒤 이전 소유권을 반환한다. 씬 종료 후 이전 예약·클립·아틀라스가 새 씬에 남지 않아야 한다.

## 빌드 없는 검사

`GameScreen.Editor.PuzzleStabilityVerification`의 세 진입점을 별도 검사용 Unity 6000.3.10f1 Editor에서 실행한다.

| 메서드 | 대상 | 출력 |
| --- | --- | --- |
| Data | 9×9·50레벨 팩·현재 시간·아이콘/GUID·원본 해시 | Logs/Stage11/data-results.txt |
| RunScene | 실제 입력 소스·파워/조합·반복 풀·pause/background·회전·다시하기·pending 취소/오류/파괴 | Logs/Stage11/scene-results.txt, pool-observation.csv |
| Observe | Time.timeScale=1, 실제 Update의 일반 연쇄·공급·다수 드론 | Logs/Stage11/observation-results.txt, observation.csv, action-observation.csv, observation-environment.txt |

검사는 제품 빌드가 아니며 플레이어/Addressables 콘텐츠 빌드를 호출하지 않는다. 사용자 Editor가 같은 프로젝트를 점유하면 임의로 종료하거나 씬을 저장하지 않는다. 검사 결과가 필요할 때 열려 있는 프로젝트 상태부터 확인한다. 비동기 검사는 `-quit`을 붙이지 않고 결과 기록 후 자체 종료한다.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Projects/Git/ServeredMeridian' -executeMethod GameScreen.Editor.PuzzleStabilityVerification.Data -logFile 'C:/Projects/Git/ServeredMeridian/Logs/Stage11/manual-data.log'
```

Data를 RunScene 또는 Observe로 바꿔 해당 검사를 실행할 수 있다. 검사는 원본 데이터 대신 소유 사본/임시 fixture를 사용한다. RunScene은 검사 중 Game View를 1920×1080/1080×1920으로 바꾸고 종료 시 이전 설정을 복원한다. UIInteraction은 기존 UI 조작 검사를 호출하고 결과에 따라 별도 검사 프로세스를 종료하는 Editor 전용 래퍼다. ResultBoundaries는 승리/실패 × pause겹침 유무의 백그라운드 결과음 재현만 실행한다. RunScene에도 같은 검사가 포함돼 있다. 전체 관련 회귀는 Logs/Stage11/run-final-suite.ps1로 실행하며 각 결과 파일을 먼저 지우고 새 PASS/FAIL/프로세스 종료를 확인한다.

## 관찰값 해석과 한계

결정적인 경계 검사는 Time.timeScale=0과 수동 표시 시계를 사용한다. 성능 관찰은 활성 세션의 실제 Update/고유 프레임만 사용하며 준비·워밍업과 비교용 직접 실행기를 측정에서 분리한다. 프레임 시간은 실제 unscaledDeltaTime, CPU/GC는 ProfilerRecorder의 이전 완료 프레임 값이다. 미지원/미수집 값은 -1이며 0으로 성공 처리하지 않는다.

batchmode Editor의 렌더·검사 사본·계측·다른 프로젝트 Editor 부하가 포함된다. p50/p95/max와 할당 수치는 이 환경의 관찰이며 모바일 FPS·메모리 예산을 보장하지 않는다. 실제 청취 0회, 실기기 출력·성능 미검증이다. 음질과 완성 화면의 사용자 검수는 별도 범위다. 12단계 계획이 있어도 자동 실행하지 않는다.




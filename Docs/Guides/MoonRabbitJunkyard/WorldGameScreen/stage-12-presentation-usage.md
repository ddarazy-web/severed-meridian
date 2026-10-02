# 12단계 실제 화면·연출 검수 사용 안내

[계획](../../../Planning/MoonRabbitJunkyard/WorldGameScreen/stage-12-presentation-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-12-presentation-goal.md) · [현재 실행 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-12-progress.md)

상태: 12단계 완료. 최종17개 회귀·추가InsetUI67PASS와 단일 리뷰 조치 결과를 감사했다. 청취/실기기는 미검증이다.

## 확인 순서

1. ServeredMeridian에서 기존 Match 레벨 에디터로 레벨을 선택하고 게임 플레이를 실행한다. Asset/MemoryPack은 기존 선택 경로를 그대로 쓴다.
2. 실제 화면 자료는 `Logs/Stage12/captures/`의 네 해상도×플레이/pause/won/lost16장과 inset-play4장이다. `capture-manifest.csv`의 소스·레벨·시드·해상도와 `capture-observations.md`의 목업 차이를 함께 읽는다.
3. 움직임 자료는 `motion/`의18사례 연속 렌더와 `motion-summary.csv`, `motion-trajectories.csv`, `motion-observations.md`이다. `motion-review/`의 시간 순서 비교 및 GIF는 보조 자료다. GIF 재생 관찰은 브라우저 보안 정책 때문에 미수행으로 기록됐다.
4. `case-18-*`는 선택 order/손 놓기·pause·수집 회전·Retry 입력 경계다. `case-19-000.png`는6종 장애물·캡슐 열린 상태·기둥1/9·덮개·먼지·발전기 연결의 실제 fixture 화면이다. 원본 저장 레벨과 혼동하지 않는다.
5. 최종 회귀 결과는 `Logs/Stage12/final-suite-results.txt`에서 각 진입점의 새 PASS/FAIL/Exit를 확인한다. 실행 중인 목록은 전체 완료를 뜻하지 않는다.

## 검사를 다시 실행할 때

사용자가 작업하는 Unity를 임의로 닫거나 씬을 저장하지 않는다. 프로젝트에 같은 Unity가 이미 열려 있으면 별도 프로세스를 중복 실행하지 않고 소유/실행 상태를 확인한다. 원본 레벨/씬을 보존하고 Editor 검사는 별도 숨김 graphics Editor로 순차 실행한다. `-nographics`로 화면 확인을 대체하지 않는다.

PowerShell 진입점은 `Logs/Stage12/run-check.ps1`의 `-Method Data`, `RunScene`, `RunMotion`, `RunInteraction`이다. `-Label`로 새 로그 이름을 지정한다. Data는 저장 파일/보존 검사, RunScene은 화면20장, RunMotion은 실제 Update18사례, RunInteraction은 입력/갤러리 검사다. 검사 실행은 현재 작업 트리의 Editor 코드가 컴파일돼 있어야 한다.


각 메서드에는 해당 결과 경로를 반드시 지정한다. 이전 복원 오류가 있는 로그와 최신 수정 후 결과를 구분한다. PASS/Exit뿐 아니라 cleanup 복원 결과도 확인한다.

```powershell
pwsh.exe -NoProfile -File Logs/Stage12/run-check.ps1 -Method Data -Label rerun-data -Result Logs/Stage12/data-results.txt
pwsh.exe -NoProfile -File Logs/Stage12/run-check.ps1 -Method RunScene -Label rerun-capture -Result Logs/Stage12/scene-results.txt
pwsh.exe -NoProfile -File Logs/Stage12/run-check.ps1 -Method RunMotion -Label rerun-motion -Result Logs/Stage12/motion-results.txt
pwsh.exe -NoProfile -File Logs/Stage12/run-check.ps1 -Method RunInteraction -Label rerun-interaction -Result Logs/Stage12/interaction-results.txt
```

최초 낙하 준비 실패에 대한 실제 준비/30초 래퍼를 포함한 기존 빌드 없는 회귀는 `Logs/Stage12/run-final-suite.ps1`로 순차 실행한다. 플레이어/Addressables 콘텐츠 빌드 메서드는 이 목록에 넣지 않는다. 정상 종료0과 실제 결과 파일의 PASS>0/FAIL0을 함께 확인한다.

## 범위와 한계

게임 규칙·연출·아트·프리팹은 이번 검수에서 변경하지 않았다. 진동 기능은 추가하지 않는다. 시험용 아이템 수량 무제한 표시와 목업의 보상/다음 레벨 예시를 구분한다.

실제 청취0회·실기기0회다. 실제 음질·기기 음량/FPS·notch는 미검증이며 Editor의 파일·좌표·재생 횟수 검사와 분리해 판단한다. 움직임 검수는 실제 Update의 연속 렌더/좌표 자료를 사용하며 사람의 기기 플레이 체감을 대체하지 않는다. 이 안내만 읽고 전체 완료로 판단하지 말고 실행 기록의10조건과 최종 리뷰를 확인한다.

## 최종 리뷰 조치와 추가 검사

Important1의 Game View 복원은 기존 항목 보존·추가 항목만 삭제·전후 목록/선택 동등성으로 해결했다. 세 finally는 정리 실패를 결과/Exit에 반영한다. Minor1의 -Result 예시는 위에 반영했다. 단일 리뷰의 조치 결과를 실행자가 감사했다.

```powershell
pwsh.exe -NoProfile -File Logs/Stage12/run-check.ps1 -Method GameViewRestoration -Label rerun-game-view -Result Logs/Stage12/game-view-results.txt
pwsh.exe -NoProfile -File Logs/Stage12/run-inset-ui.ps1
pwsh.exe -NoProfile -File Logs/Stage12/final-gate.ps1
```

InsetUI는 정확한 Rect(24,36,w-48,h-72)의32개 실제 UI/팝업/글자 경계와4개 pause PNG를 검사한다. HUD가 보드와 맞닿은 행렬 오차는0.000061px다. 안내의 빈 컨테이너 여백을 실제 표시 겹침으로 세지 않고 CanvasRenderer의 현재 mesh와 raycastTarget을 확인한다. 결과Panel은 비활성 상태의 실제 경계 검사이며 활성 승패는 기본16장으로 확인한다.

GameViewCleanupFailure는  정리 실패 주입 검사다. 실제 설정을 바꾸지 않고 기대 목록만 훼손하며 FAIL/Exit1이 정상 기대값이다. 최종17개 성공 목록에 넣거나 일반 플레이 성공으로 세지 않는다.

현재 상세 증거는 stage-12-progress.md와 Logs/Stage12/requirement-audit.md에 있다. 제품 코드/이미지/프리팹 변경0이며83파일 기준과 추가 Editor helper 수정의 전후 해시를 구분한다. 새 Editor 검사7개/meta7개를 보존한다.

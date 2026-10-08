# 튜토리얼 후속7단계 — 진행 기록

상태: 완료. 2026-10-07. 신규4레벨 실제 게임 검증329 PASS, 회귀20개 통과/기존 종합 검사1개 실패 재현, 테스트 없는 게임 소스 컴파일 통과.

실제4레벨 수거 드론의 생성·교환 발동 콘텐츠를 적용했다. 기존1~3레벨과 이전 작업, 현재 드론 표적/비행 구현을 보존했다. 빌드·커밋·푸시·하위 에이전트는 사용하지 않았다.

## 작업 기록

- 현재 HEAD/WIP, 기존 데이터·게임 소스·프리팹·설정·검사 파일의 해시와 기존 구간 팩을 `Logs/Tutorial/Stage07/baseline-*`에 확보했다.
- 외부 테스트 프로젝트에 `TutorialDroneLevelVerification` 두 파일과 메타를 추가했다. 실제4레벨 부재를 검사하는 Run을 실행했고 예상대로 실패했다. `missing-level-red.txt`.
- 첫 Preview에서 기존 폭탄 후보의 보조 색상 변경이 새 드론 후보에 초기3매칭을 만든 것을 실제 시작 검사로 발견했다. 해당 후보 변경만 제거했다. `preview-initial-match-red.txt`.
- Preview/Apply 모두 native exit0. 전체9×9/schema5 실제 Level_04를 저장했다. GUID `8340d75c714de7f4984e693a01968742`.
- 0기준 지정 교환(2,4)→(3,4)이 (3,3)/(3,4)/(4,3)/(4,4)의2×2를 완성한다. 드론 생성(3,4)→낙하(4,4)→교환 발동(4,5)을 실제 연속 재생으로 확정했다. 시드12345, 이동20→19→18, 분홍/노랑30개씩 수집 미션이다.
- 고정 공급 실제 소비는 열0~8 기준 `0,0,0,6,2,3,1,0,0`이다. 각 생성구에 소비량+2개만 남기고 재검사했다. 완료 뒤 일반 무작위9생성구로 돌아간다.
- 단독 직접+5칸 발사와 비행 표적1개/실제 착탄 제거를 발사 묶음으로 구분했다. 현재 미션 우선 예약과 착탄1회, 실제 상승→호버→돌진 시간표를 재사용했다. 이후 연쇄를 직접 범위에 포함하지 않는다.
- 실제 Run native exit0/329 PASS/런타임 예외0. Asset/MemoryPack·항상/실행 안 함/완료 자동 생략·중단 첫 단계 고정 재현·완료 기록1회/중복0·일반 교환·실제3→4 전환을 확인했다. 미완료1/2·기존3·신규4 및 실제 플레이어 키1~4를 분리/복원했다.
- 실제 드론 재생 객체의 프레임을 관찰하여 상승·호버·돌진 각각 동안 단계3 유지/늦은 Next 차단/완료 기록 보존을 확인했다. 관련 재탐색 검사는 별도 기존 드론 회귀로 수행한다.
- 1280×720/720×1280 생성/발동4캡처를 저장했다. 가로 생성/발동과 세로 발동 캡처를 직접 열어 대상2칸 투명·전체 어둠·손가락/말풍선 비차폐·표적 사전 강조 부재를 확인했다. 캡처 경로는 `Logs/Tutorial/Stage07/drone-step1|3-가로x세로.png`이다.
- 현재 기존 기준1542개 파일 중 변경은 승인한 구간 팩 하나다. 신규4레벨·외부 검사·단계 문서 외 게임 소스/프리팹/설정을 변경하지 않았다. 최종 감사/컴파일은 회귀 뒤 다시 수행한다.
- 실제3→4 전환은 시험 인스턴스의3레벨 미션만 강제로 완료하여 유도했다. 기존3 안내·출시 팩 전환·늦은 신호 격리의 근거이며 실제 승리 밸런스를 끝까지 검증한 결과는 아니다.
- 과거 회귀 출력1277개와 추가 드론/종합 검사 출력170개를 백업했다. 넓은 Logs 전체 백업의 탐색 비용/접근 제한을 확인한 뒤 소유한 백업 프로세스만 종료하고 실제 회귀 경로로 축소했다. 게임/사용자 Unity는 종료하지 않았다. 부분 백업은 검증 근거로 사용하지 않는다.

## 결정 기록

- Ruling: 사용자가 현재 워크 브랜치·기존 WIP 보존과 직접 실행을 요구하므로 현재 작업 공간을 사용한다. 새 워크트리·커밋·하위 에이전트를 사용하는 스킬 기본 절차는 적용하지 않는다. 잘못 적용하면 사용자가 확인한 데이터와 작업 위치가 달라질 수 있다.
- A→B: 저장 전 실제 후보 재생이 통과해야 한다. Apply는 현재 구간의 모든 기존 레벨을 포함한다.
- B→C: 출시 데이터의 실제 에디터·Asset/MemoryPack·표시 완료·기록 격리를 확인한 뒤 회귀·소스 단독 컴파일을 수행한다.

## 완료 근거

| 요구 사항 | 실제 근거 |
| --- | --- |
| 후보/저장/출시 데이터 | Preview/Apply native exit0, preview-results.txt/apply-results.txt. 신규 Level_04와 동일 구간 팩만 추가/갱신 |
| 정확한2×2·드론1개·Generated/Activated·좌표/공급 | 실제 조회/생성 결정/연속 재생, 공급 부족/누락/무작위 대체 및 비지정 입력 거절 |
| 직접+5와 추가 표적1개·미션/이동/연쇄 분리 | PowerAttackRecord/DroneFlightRecord/EffectRecord의 실제 착탄 제거와 미션 기록 합계. 일반 공급 복귀/남은 미션 |
| 실제 에디터/두 데이터 경로/세 모드/기록 | run-results.txt 329 PASS/native exit0, runtime-exceptions.txt 0바이트. 실제 플레이어1~4 키 존재/값 보존 |
| 가로/세로 포커스·표시 완료·자유 플레이 | 생성/발동4캡처와 좌표/투명/어둠/비차폐/입력 검사. 실제 드론 상승·호버·돌진 프레임 동안 단계3/완료 기록 대기 |
| 실제3→4 및 늦은 신호 격리 | 실제3 안내 완료/팩 전환/이전 티켓·결과·표시 신호를 전달해 신규4 보드/단계/기록 보존. 전환 유도만 시험 미션 강제 완료 |
| 기존 콘텐츠 보존/재적용 차단 | 기존 구간1/2/3 스냅샷 동등. existing-apply-rejected.txt 및 existing-apply-audit.txt의 에셋/메타/팩 해시 보존 |
| 회귀 | regression-command-results.txt에21개 실행 결과. 20개 PASS, 기존 종합 시간 기대값1개 FAIL. 바뀐 출력은 regression-results 아래 보관 |
| 출력/임시 상태/단독 컴파일 | final-audit.json: 과거1447출력 해시 모두 복원, 테스트 해제. game-only-compile-result.txt ExitCode=0; TestsAttached=False |
| 승인 변경/다음 문서 | 기존1542기준 파일 중 팩1개만 변경, 신규 데이터/메타2개와 외부 검사/메타4개 추가. HEAD 유지, 신규5 없음. next-doc-links-audit.json 연결 오류0 |

기존1~3와 다른 팩/ECPK/Addressables/카탈로그 제작 자산·게임 소스·프리팹·씬·설정·이전 외부 검사의 기준 해시를 유지했다. 저장된 Level_04를 직접 참조하도록 씬/프리팹을 변경하지 않았으며 실제 MemoryPack 경로로 진입을 확인했다.

## 수행한 회귀

TutorialBombLevelVerification.Run/BombPresentation, TutorialRocketLevelVerification.Run, TutorialGuidanceVerification.Run, TutorialGameIntegrationVerification.Run, Tutorial.Editor.LevelTutorialDataVerification.Run, GameScreen.Editor.PuzzleGameplayVerification.Run, PuzzlePopupVerification.RunScene/RunRestore, Levels.Editor.BoardActionVerification.Data/PowerEffectVerification.Data, TutorialFocusVerification.Run은 모두 native exit0이다.

Elements.Editor.DroneFrameworkVerification의 Policies/RiseHover/RetargetFlight/Motion/LossMotion/NoTargetMotion/RepeatSelection과 Levels.Editor.TargetPowerVerification.Data도 모두 exit0이다. 정책 조회·상승/호버/돌진·실제 목표 소실 기록·재탐색 시간표/연속성·무표적·반복 선택을 확인했다. 목표 소실 fixture는 기존 논리/표시 시간표 검사이며 신규4의 실제 단독 드론 화면에서 소실을 강제한 검사는 아니다.

## 기존 실패와 제약

- GameScreen.Editor.PuzzlePowerAnimationVerification.Data는 기존과 동일한 “재탐색 드론은 공중 이동·선행 공격 대기 후0.18초 추가 선회하고 돌진” 기대값에서 exit1이다. 현재 `.85f + HoverDelay`와 호버·재돌진에 맞지 않는 이전 기대값이다. 상세는 regression-results/Logs/Stage08/data-results.txt와 native 로그20261007-220041-490-Run.log. 관련 게임 소스와 기존 검사 코드는 기준 해시 그대로다. 전체 회귀 통과로 표현하지 않는다.
- native Unity 종료의 기존 JobTempAlloc 경고가 이번에도 남았다. 신규 실제 게임 런타임 예외0과 경고 해결은 구분한다. 이 작업에서 경고나 종합 검사 기대값을 임의 수정하지 않았다.
- 실제 에디터 Game View의 두 화면 크기/마우스 포인터 경로를 확인했다. Android/iOS 실기기 터치·노치·성능과 실제 승리 밸런스는 검사하지 않았다.
- 최종 검토는 사용자 하위 에이전트 금지에 따라 작성자가 별도 읽기 검토로 수행했다. 새로운 엔진/런타임 분기·비행/우선순위 변경 없이 기존 모듈과 데이터만 사용했으며 요구 사항별 근거를 위 표와 현재 파일에서 확인했다. 신규 코드의 실패 설명을 PASS 라벨로도 기록하는 기존 검사 형식을 따르므로 첫 줄의 에셋 부재 문구는 assertion 설명이고 에셋이 없다는 최종 판정이 아니다.

## 다음 단계

[8단계 계획](../../../Planning/MoonRabbitJunkyard/Tutorial/stage-08-magnet-content-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/Tutorial/stage-08-magnet-content-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/Tutorial/stage-08-command.md).

다음은 실제5레벨 직선5매칭→무지개 자석 생성→일반 색 블록과 교환 발동 콘텐츠다. 이번에는 문서만 작성했다. 다음 실행 지시 전까지5레벨은 구현하지 않는다.

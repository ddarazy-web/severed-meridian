# 레벨 튜토리얼 3단계 — 실제 게임 연결·재생 검사

> 실행 방식: 이 세션에서 직접 수행하며 executing-plans 절차를 적용한다. 빌드·커밋·푸시·하위 에이전트는 사용하지 않는다.

상태: 완료. 2026-10-07 실제 게임/재생·수명/회귀 및 게임 소스 단독 컴파일 검증 완료. [검증 기록](../../../Verification/MoonRabbitJunkyard/Tutorial/stage-03-progress.md).

목표: 등록식 엔진을 기존 실제 퍼즐에 연결하고 지정 행동·고정 공급·결과·연출·종료를 제어한다. 같은 보드를 유지한 채 일반 플레이로 복귀한다.

구조: GameScreen의 세션 연결부가 입력과 표현 수명을 소유하고, Tutorial의 보드 연결부가 엔진과 실제 실행 기록 사이를 연결한다. BoardActionExecutor는 튜토리얼 종류를 알지 않고 실제 퍼즐 규칙과 필요한 종료 보류 경계만 제공한다.

기술: Unity6000.3.10f1, 기존 C#/UniTask/MemoryPack, 9×9, 기존 패키지/asmdef/Addressables 정책.

[기획](../../../Contents/MoonRabbitJunkyard/13_레벨튜토리얼.md) · [가이드라인](integration-guideline.md) · [2단계 계약](stage-02-runtime-plan.md) · [2단계 검증](../../../Verification/MoonRabbitJunkyard/Tutorial/stage-02-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/Tutorial/stage-03-game-integration-goal.md) · [실행문](../../../Commands/MoonRabbitJunkyard/Tutorial/stage-03-command.md).

## 확정된 엔진 계약

- TutorialProgress는 제작 단계/조건을 실행용으로 복사한다. State/StepIndex와 Snapshot은 읽기 전용이며 Snapshot은 제작 데이터 변경 권한을 주지 않는다.
- TryApprove(TutorialInput, out TutorialActionTicket)는 실제 실행 전 승인이다. Next는 설명을 한 단계 진행하고 다른 명령은 성공을 확정하지 않는다.
- ReportCompletion(ticket, succeeded, definitionId)는 실제 실행 성공/실패를 전달한다. 파워 교환은 실제 발동한 파워 ID가 일치해야 한다. 양방향 교환을 허용하므로 입력 첫 칸만 파워 원점이라고 가정하지 않는다.
- ReportResults(ticket, records)는 해당 행동/연쇄의 실제 기록만 누적한다. 기록은 종류·정의 ID·발생 식별값·선택 좌표를 가진다.
- ReportPresentationComplete(ticket)는 관련 연쇄와 보드·드론·낙하·피해·수집 표시가 모두 끝난 후 전달한다. 단순한 IsPresenting == false만 사용하지 않는다.
- SetPaused, Fail, Cancel/Dispose로 일시정지·오류·수명을 연결한다. 지난 시도/다른 세션/취소 신호는 엔진이 거절한다.
- 엔진은 실제 공급·종료·재고를 변경하지 않는다. 현재 부족한 결과 조건은 대기한다. 실제 재생 종료 후 불충족은 연결부가 단계별 오류로 진단한다.

## 현재 연결 지점과 파일 책임

| 영역 | 작업 |
|---|---|
| Tutorial/Runtime/TutorialBoardAdapter.cs — 신규 | 엔진 승인과 실제 실행의 연결, 관련 기록 변환, 완료/복귀 경계 |
| GameScreen/Runtime/Session/PuzzleGameSession.Tutorial.cs — 신규 | 튜토리얼 준비·다음 요청·읽기 전용 상태·신호 전달·파기 |
| GameScreen/Runtime/Session/PuzzleGameSession.cs / .Presentation.cs / .Controls.cs / .LevelTransition.cs / .Progress.cs | 준비·교환/발동/아이템·연쇄·표시 완료·재시작/전환·팝업/일시정지 호출부 |
| GameScreen/Runtime/Input/PuzzleBoardInput.cs | 선택/드래그/아이템 대상 조회를 같은 허용 정책에 연결 |
| PuzzlePlay/Runtime/Actions/BoardActionExecutor.cs / .Ending.cs / .Items.cs | 필요한 종료 보류/재평가·이동 0 무료 체험 경계만 추가 |
| PuzzlePlay/Runtime/State/LevelRuntimeState.cs 및 기존 공급 준비 경계 | 고정 공급 적용·일반 공급 전환; 상태 재생성 금지 |
| Tutorial/Validation/LevelTutorialReplayValidator.cs — 신규 | 실제 행동/연쇄를 시험 사본에서 재생하고 단계별 실패 진단 |
| Tutorial/Editor/LevelTutorialEditorPanel.cs | 재생 검사 결과를 기존 패널에 표시 |
| Tests/Editor/Features/Tutorial/TutorialGameIntegrationVerification.cs — 신규 | 실제 퍼즐/공급/종료/연출/수명·재생 검사, 필요 .meta |

Features 아래의 현재 폴더 경계를 유지한다. 파일명은 구현 전 호출부 조사에서 책임에 맞춰 조정할 수 있지만 변경 이유를 계획/검증 기록에 남긴다. 주변 리팩터링·새 asmdef/DI/전역 관리자는 추가하지 않는다.

## 작업 순서

A~C는 내부 묶음이다. 실패 검사를 확보하고 각 묶음을 통과시킨 뒤 진행한다.

### A. 준비·명령·실제 결과

- [x] 레벨 파기 전 튜토리얼/일반 공급 사본을 확보한다. 활성 튜토리얼은 고정 시드/공급으로 준비하고 재진입/재시작을 재현한다. 일반 레벨은 기존 준비 경로를 유지한다.
- [x] 연결 전에 보드/카탈로그를 포함한 전체 레벨 검사를 수행한다. 2단계 엔진의 보드 없는 형식 검사를 실제 범위/벽/초기 대상 검사의 대체로 사용하지 않는다.
- [x] 시작 부스터·임의 아이템을 차단한다. 입력 조회와 직접 세션 명령에 같은 엔진 승인을 적용한다. 선택 미리보기는 승인 실행을 소비하지 않도록 분리한다.
- [x] 승인 ticket으로 실제 Swap/Activate/UseItem을 연결한다. 실패는 재시도 가능하고 이동·미션·난수·체험 권한이 변경되지 않아야 한다. 지정되지 않은 제자리 발동은 거절한다.
- [x] 실제 정의 ID·생성/발동/제거를 실행 기록에서 수집한다. 현재 RuntimeCell/MatchedBlockChange/PowerAttackRecord/EffectRecord에 필요한 실제 ID/개체 구분이 부족한 부분만 보완한다. 튜토리얼 오브젝트 종류 분기를 새로 만들지 않는다.
- [x] 발생 식별값은 실제 발생/본체를 구분해야 한다. 같은 2×2 본체의 여러 footprint 좌표에는 같은 제거 식별값을 사용하고 다른 개체/재생성/연쇄 발생은 구별한다. 풀링된 Unity 인스턴스 ID나 위치만으로 개체를 식별하지 않는다.
- [x] 기본 교환·로켓 생성 후 교환 발동·망치/교환/섞기의 실제 보드 fixture로 승인/거절/미션/이동/결과를 검사한다. Asset/팩3 입력의 동일 진행을 확인한다.

### B. 공급·연출·종료·수명

- [x] 고정 공급 커서를 실제 낙하에 연결한다. 미등록 생성구·소진·후속 대상 소실·불가능한 행동을 단계 경로로 진단한다. 무작위 기본 공급·임의 시드·자동 섞기로 설정 오류를 숨기지 않는다.
- [x] 연쇄가 안정되고 실제 IsPresenting·HasProgressFeedback·IsStartingFeedback 및 관련 효과 준비/드론/낙하/수집이 끝나야 연출 완료를 전달한다. 팝업/일시정지 중 진행을 보류하고 나가기는 유지한다.
- [x] EndStableTurn에서 퍼즐 턴 처리와 종료 결정을 구분한다. 조기 승리/라스트팡·이동 소진/자동 섞기를 필요한 동안 보류하고 완료 후 종료 결정만 한 번 재평가한다. MoldRules.FinishTurn을 재실행하지 않는다.
- [x] 이동 0에서도 마지막 설명/완료를 허용하고 추가 교환은 거절한다. 필요한 마지막 무료 아이템을 허용할 좁은 실행 경계를 실제 CanUseItems 규칙에 맞춰 구현한다. 일반 아이템의 이동 0 허용으로 확대하지 않는다.
- [x] 무료 권한은 엔진의 해당 단계 성공 시 1회 소진을 따른다. 재고 시스템은 만들지 않고 보유량 연동을 가정하지 않는다.
- [x] 완료 후 현재 보드/내구도/미션/이동/턴/난수를 유지하고 일반 공급으로 전환한다. 전환 자체로 블록을 만들지 않는다.
- [x] 재시작/다음 레벨/시작 실패/취소/파기에 엔진·ticket·사본·구독·비동기 대기를 해제한다. 예전 세션 신호가 새 세션에 영향을 주지 않게 한다.
- [x] 조기 승리·이동 0·공급 소진·연출 지연·팝업·재진입·취소·일반 레벨 회귀를 실제 실행/표시 경계에서 검사한다. 엔진 모의 검사만으로 통과를 주장하지 않는다.

### C. 실제 재생 검사·인계

- [x] 실제 BoardActionExecutor와 동일 등록 처리기를 사용하는 재생 검사를 작성한다. 행동 실패·미래 대상 소실·결과 불일치·공급 부족을 레벨/단계/대상으로 반환한다. 정상 종료된 행동에서도 조건 불충족이면 대기 성공으로 오인하지 않는다.
- [x] 소유한 레벨 사본에서 재생하여 원본 배치/팩을 변경하지 않는다. 패널과 실행 전 검사에 연결하고 정적 검사/논리 재생/실제 연출 검사 범위를 구분한다.
- [x] 다음 요청용 API와 튜토리얼 상태를 제공한다. 이번에는 말풍선·손가락·완료 저장을 만들지 않는다. 설명 다음 요청은 검사 도구에서 호출하여 검증하고 실제 안내 화면은 4단계에 연결한다.
- [x] 새 엔트리 TutorialGameIntegrationVerification.Run으로 실제 검사한다. 기존 TutorialRuntimeVerification.Run, 1단계 데이터/에디터/팩 검사와 일반 플레이·저장·드론·결과/팝업 회귀를 수행한다.
- [x] 테스트 연결 해제 후 게임 소스만 컴파일하고 원본/GUID·HEAD/WIP를 확인한다.
- [x] stage-03-progress.md에 변경·실제 검증·제약을 보고하고 4단계 계획·목표·복사용 실행문을 작성한다. 4단계 구현은 자동 시작하지 않는다.

## 검토 중점·제약

- 연출 표시가 잠깐 비는 프레임에서 다음 단계로 넘어가지 않는지 검사한다.
- 반대 방향 파워 교환과 뒤늦은 드론 결과를 원래 행동 ticket에 연결한다.
- 결과 부족/공급 오류를 무작위나 성공으로 대체하지 않는다.
- 종료 보류 해제에서 곰팡이/미션/턴을 중복 처리하지 않는다.
- 초기 시드 적용과 정상 공급 복귀가 누적 상태를 초기화하지 않는지 확인한다.

팩1/2/3·필드 순서·50레벨 주소·ECPK·제작 SO 배포 제외를 보존한다. 기존 WIP·원본 레벨/팩/이미지/GUID·과거 검사 출력을 보존한다. 빌드·번들·커밋·푸시·하위 에이전트·사용자 Unity 종료·임의 씬 저장·출시 데이터 덮어쓰기는 하지 않는다. 새 UI·영구 기록·시험 3모드·대표 레벨 적용·상세 매뉴얼·고물탑은 다음 범위다.

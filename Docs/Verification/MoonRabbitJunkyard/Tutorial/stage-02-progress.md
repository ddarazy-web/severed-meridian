# 레벨 튜토리얼 2단계 — 진행·검증 기록

날짜: 2026-10-07. 상태: 완료.

[계획](../../../Planning/MoonRabbitJunkyard/Tutorial/stage-02-runtime-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/Tutorial/stage-02-runtime-goal.md).

## 실행 기준

- 현재 HEAD와 work, 기존 WIP·완료된 1단계·원본 데이터/GUID를 보존한다.
- A 등록/규칙, B 진행/결과, C 검사 공유/확장 검증 순서로 직접 수행한다.
- 빌드·커밋·푸시·하위 에이전트와 실제 게임 연결은 하지 않는다.
- Ruling: 사용자와 실행문의 하위 에이전트/커밋 금지에 따라 executing-plans의 리뷰·기록은 자기 검토와 이 문서로 수행한다. 비용: 독립 리뷰 없이 검증해야 하므로 확장/수명/호환성 검사를 명시적으로 확인한다.
- 현재 HEAD는 과거 1단계 기록 이후 변경됐으므로 이번 실행 시작의 실제 HEAD를 기준으로 보존한다.

## 검사 기록

등록 구조와 공통 진행 엔진의 부재를 먼저 검사한다. 이후 실제 계약 검사와 관련 회귀 결과를 기록한다.

- RED: 20261007-140722-007-Run.log, exit1. runtime-results의 실제 실패는 공통 TutorialProgress 부재다. red.txt에 원문 보관.
- GREEN: 20261007-141300-260-Run.log, exit0. 등록/진행/결과/수명/확장/검사 공유 102개 확인 통과.
- 최초 샌드박스 Unity 검사 프로세스는 로그를 생성하지 못했다. 명령줄로 소유 PID를 확인해 그 검사만 종료하고 연결을 해제한 후, 승인된 실행 환경에서 RED와 GREEN을 수행했다. 사용자 에디터는 건드리지 않았다.
- Ruling: 지정 두 칸의 양방향 교환을 허용한다. 실제 퍼즐 교환의 대칭성과 맞추고 안내의 첫→둘째 표시는 유지한다. 비용: 일방향 강제가 필요해지면 해당 처리기의 허용 정책을 변경해야 한다.
- 아이템 단계는 RegisterItem에서 아이템 종류로 직접 등록한다. 중앙 진행부에 아이템 분기를 두지 않으며 네 단계 종류를 지원한다.
- 관련 신호 API: TryApprove, ReportCompletion, ReportResults, ReportPresentationComplete, SetPaused, Fail, Cancel/Dispose. 실제 퍼즐 실행·공급·연출 연결은 3단계다.
- 자기 검토 RED: 20261007-141859-792-Run.log, exit1. 설명에 실행 결과 조건을 붙이는 설정이 조용히 무시되는 실패를 재현했다. description-red.txt에 보관했다.
- 최종 엔진 GREEN: 20261007-142014-230-Run.log, exit0, PASS139/FAIL0. 설명 조건 모순을 정적 검사와 엔진 준비에서 진단하도록 수정했고 신호 6가지 순서·원본 조건 편집 격리·공유 등록 목록의 권한 격리·외부 설정 오류 후 지연 신호도 검사했다.
- Ruling: 설명 단계에는 실제 행동이 없으므로 실행 결과 조건은 설정 오류로 진단한다. 기획의 ‘설명은 다음 버튼으로 진행’과 맞추며 정상 1단계 fixture는 유지한다. 비용: 이전에 이 모순된 조합을 저장했다면 조건 제거 또는 조작 단계로 이동해야 한다.

## 최종 구현과 검증

TutorialHandlerRegistry는 종류별 직접 등록/조회를 소유하고 실행 전 Freeze한다. TutorialStepHandlers는 설명/교환/파워/아이템의 허용·성공·설정 검사·참조를, TutorialResultEvaluator는 생성/발동/제거의 일치를 소유한다. TutorialProgress는 실행별 단계·ticket·권한·결과 집계·연출 대기만 관리한다. TutorialValidationContext와 기존 validator는 공통 배치/공급 및 최초 대상 검사를 유지하며 종류별 규칙을 공유한다.

엔진은 Unity 오브젝트·GameScreen·UI·Addressables를 직접 사용하지 않는다. 제작 단계/조건을 복사하고 표현용 Snapshot은 불변이다. 선택 실행의 조건만 조회하며 본체 발생 식별값으로 중복을 제외한다. 실제 결과·연출 전달은 아직 모의 신호다.

| 검사 엔트리 | PASS | 최종 Logs/TestHarness 로그 |
|---|---:|---|
| TutorialRuntimeVerification.Run | 141 | 20261007-142507-704-Run.log |
| Tutorial.Editor.LevelTutorialDataVerification.Run | 63 | 20261007-142124-506-Run.log |
| Tutorial.Editor.LevelTutorialEditorVerification.Run | 27 | 20261007-142151-350-Run.log |
| Levels.Editor.LevelStorageBaselineVerification.Run | 82 | 20261007-142233-246-Run.log |
| Elements.Editor.ElementPackVerification.Run | 53 | 20261007-142303-851-Run.log |
| Elements.Editor.ElementContentAuthoringVerification.Run | 74 | 20261007-142337-206-Run.log |
| 합계 | 440 | 각 exit0, FAIL0 |

141개는 승인 검사 등을 포함한 assertion 수다. 최신 원문은 Logs/Tutorial/Stage02/runtime-results.txt, 회귀 결과와 실행 기록은 regressions 아래다. 회귀가 쓰는 기존 결과 파일은 실행 전 바이트를 보관해 finally에서 복원했다.

최종 테스트 연결 해제 후 -batchmode -quit -projectPath -logFile로 게임 C#만 컴파일했다. game-only-final-compile.log 및 game-only-final-compile-result.json: exit0, testsDetached=true. 플레이어·Addressables 번들 빌드는 하지 않았다.

## 요구사항별 완료 근거와 자기 검토

| 요구사항 | 소스와 실제 검사 근거 |
|---|---|
| 등록·누락·중복·등록 순서 | Registry의 직접 사전 조회/Freeze, VerifyRegistry와 VerifyBoundaryCases |
| 새 종류/기존 종류 교체 | VerifyExtensions: 새 enum 값의 등록 실행, 기존 행동/조건 정책 교체, 미사용 호출0 |
| 공통 진행·성공/연출 대기 | VerifySteps/VerifyResults/VerifyBoundaryCases: 승인≠성공, 결과/성공/연출 6순서 |
| ID/좌표/수량과 2×2·재보충 | VerifyResults: 본체4칸→1개, 다른 개체, 서로 다른 ID/좌표 |
| 무료 권한·실패·취소·격리 | VerifySteps/VerifyLifetime/VerifyBoundaryCases: 각 아이템 성공1회·실패 보존·공유 등록의 실행 격리 |
| 원본 사본·지연/중복/일시정지 | VerifyLifetime/VerifyBoundaryCases: 원본 편집 격리·과거/취소/타세션 신호 거절·재개 |
| 실행/검사/참조 공유·팩/에디터 | VerifyValidation 및 1단계 데이터/에디터/저장/팩 회귀 |
| 보존·컴파일·다음 문서 | final-audit.json, 최종 게임 소스 컴파일, 3단계 계획/목표/실행문 링크 검사 |

Final review: self-review. 사용자 실행문의 하위 에이전트 금지에 따라 독립 리뷰는 하지 않았다. 종류별 중앙 분기의 이동, 상태 소유권, 공개 계약, 결과 집계와 취소, 원본·직렬화 경계, 테스트 연결 해제 및 다음 단계 범위를 별도로 읽고 검토했다. 발견한 설명 조건 모순은 실패 검사→수정→전체 엔진 검사 및 관련 회귀로 확인했다. 미해결 지적은 없다.

## 보존·한계·다음 단계

실행 기준 HEAD: a5878ef7451b3dfa988d14e0d7b3d83024346f86, work. final-audit.json에서 HEAD/브랜치 유지, 원본 Assets/Data의 제작·팩·메타데이터54개 SHA256 동일, 관련 소스 .meta 존재, 테스트 연결 해제와 index.lock 없음을 확인했다. 기존 WIP를 보존했다. 소스 변경은 Tutorial의 신규 런타임/공통 검사와 기존 validator에 한정했다. enum·MemoryPack 필드·기존 팩 DTO는 바꾸지 않았다.

새 종류는 해당 처리기/판정기와 명시적 등록을 추가한다. 같은 특성의 새 장애물은 ID 설정으로 기존 판정기를 공유한다. 새 저장 필드/제작 입력이 필요한 기능은 팩 호환성·에디터를 별도로 검토해야 한다. 보드 없는 엔진 준비 검사는 실제 보드 범위·벽·카탈로그 검사를 대체하지 않는다.

실제 게임 입력·시드/공급·승리/라스트팡 보류·실제 결과 수집·연출 검증·재생 검사 연결은 3단계 미착수다. 안내 화면·완료 영구 기록·시험3모드·대표 레벨 적용은 4단계다. 빌드·커밋·푸시·출시 데이터 덮어쓰기·사용자 Unity 종료·임의 씬 저장은 수행하지 않았다.

[3단계 계획](../../../Planning/MoonRabbitJunkyard/Tutorial/stage-03-game-integration-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/Tutorial/stage-03-game-integration-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/Tutorial/stage-03-command.md). 3단계 구현은 자동 시작하지 않는다.

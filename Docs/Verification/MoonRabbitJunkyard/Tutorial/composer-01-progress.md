# 조합형 튜토리얼 1단계 진행 기록

계획: Docs/Planning/MoonRabbitJunkyard/Tutorial/composer-01-plan.md

## 시작 상태

- 2026-10-08, work 브랜치. 기존 Level_03/04와 팩, 이전 단계 문서·테스트 WIP 보존.
- 실제 레벨·팩 해시는 Logs/Tutorial/Composer01에 기록. 팩3 fixture는 같은 경로 legacy-v3.bytes에 복사.
- ServeredMeridian Unity는 닫혀 있음. 다른 프로젝트의 Unity는 작업 대상이 아니며 변경하지 않음.
- Ruling: 사용자 지시에 따라 기존 work 체크아웃에서 진행하고 하위 에이전트·빌드·커밋·푸시 없이 자체 검토한다. 스킬의 커밋/별도 리뷰어 절차보다 사용자 지시 우선.
- 사전 연결 검토: A의 저장 조건 데이터가 B 판정과 C 편집의 공통 계약이다. 실제 매칭 기록은 퍼즐 실행 위치를 조사해 연결하고 제거 기록으로 추정하지 않는다.

## 현재 결과

1단계 구현과 Unity 실제 편집·플레이 검증 완료. 아래 초기 묶음의 미착수/남은 항목은 당시 이력이며 최종 결과는 마지막 완료 감사 표를 따른다. 실기기는 확인하지 않았다.
## 2026-10-08 첫 구현 묶음

- A: 기존 팩3 바이트 동일성, 새 교환 조건 팩4 왕복 통과. 영속 fixture는 Tests/Fixtures/Tutorial/legacy-v3.bytes. 버전4는 기존 팩3+확장 봉투로 구현. 조건/자동 강조를 MemoryPackIgnore로 두고 LevelPackCodec이 확장을 소유한다.
- B: 성공 교환 누적, 실패 시 누적 보존, 지난 시도 신호 무시, 조건 검증, 매칭 필터를 등록 기반으로 구현. 실제 BoardActionResult.Decisions와 CascadeStepResult.Decisions를 연결하므로 퍼즐 규칙은 재구현하지 않는다.
- RED: 074242 검사에서 새 조건 저장 소실 재현. 074503에서 0회 조건 미검출과 첫 교환 후 잘못 완료 재현. 074725에서 실제 매칭 사건 연결 누락 재현.
- GREEN: Logs/TestHarness/20261008-074821-588-Run.log, native exit 0. Logs/Tutorial/Composer01/results.txt: 저장/누적/필터/실제 3매칭과 연출 대기 21개 PASS. 플레이어 빌드 아님. 테스트 임시 연결 해제 확인.
- 실제 Level_01~04 및 팩의 시작 해시 유지 확인. HEAD/기존 WIP 유지.

## 다음 작업 — 목표 유지

- C 제작 UI 미착수. 다음에는 실제 LevelEditorWindow를 임시 테스트 레벨로 띄워 셀 선택→샘플 미리보기→적용→조건 편집 UI 실패 검사를 먼저 작성한다. 기존 LevelTutorialEditorVerification의 Pointer/NavigationSubmit 방식 재사용 가능.
- 현재 패널은 우측 레벨 설정 안이다. 목표의 왼쪽 단계/중앙 보드/오른쪽 설정/하단 검사에 맞춰 LevelEditorWindow의 전용 튜토리얼 편집 모드를 연결해야 한다. 소스: Features/LevelEditor/Editor/Presentation/LevelEditorWindow.cs, Tutorial/Editor/LevelTutorialEditorPanel.cs.
- B 검증 추가 필요: 분리 2묶음/T·L 묶음 실제 게임 검사, All/Any, 새 단계 초기화, 실제 연쇄 필터, 다중 행동 재생 검사. 현재 기본 실제 3매칭 검사만 통과했으며 폭넓은 완료로 취급하지 않는다.
- A 검증 추가 필요: 잘못된 팩4 확장(중복/누락/범위/미등록), 무튜토리얼 레벨, JSON/Asset 재로드와 복사 경로. 최소 조건 수 변경에 따른 정적 이동 수 검사 보강 고려.
- 자동 강조는 필드만 있으며 UI/런타임 반영 미구현. 조건 대상/조작/강조 구분, 샘플 독립 복사, Undo/Redo, 오류 이동 모두 남음.
- D: 실제 게임 화면 Asset/MemoryPack 실행, 포커스·입력·연출 검증, 관련 기존 회귀, 독립된 자체 리뷰, 2단계 문서/실행문 남음. 기존 Stage02/03 검사 로그 등은 회귀 실행 전 보존할 것.
- 실행 중인 검사 프로세스 없음. 마지막 세션 34474 exit 0. 목표 완료 아님.

## 2026-10-08 두 번째 구현 묶음

- C: `TutorialSampleCatalog.CreateSwap`은 교환/직접3매칭 샘플을 독립된 데이터로 만든다. 기존 실제 레벨 생성/교체 없음.
- `LevelTutorialEditorPanel.Composer.cs`: 셀 두 개 선택, 샘플 목록/미리보기, 새 단계 적용, 교환/매칭 조건 추가·삭제, All/Any, 자동 강조 토글, 단계 복제, 오류 위치 이동.
- `LevelEditorWindow`의 `tutorial-composer-mode` 버튼으로 전용 모드 진입. stageHost에는 왼쪽 단계/샘플, 기존 보드는 중앙, 조건은 오른쪽, testHost는 보드 아래. 원시 필드는 고급 펼침으로, 공급도 펼침으로 배치.
- 자동 강조 snapshot은 저장된 낡은 수동 좌표 대신 현재 first/second를 사용한다. 수동 강조 선택 시 자동 모드 해제.
- RED: 에디터 075011 진입 버튼 없음 → 구현. 075314 전용 모드 없음 → 구현. 핵심 075511 자동 강조 좌표 불일치 → 수정. 에디터 075546 오류 이동 버튼 없음 → 구현. 075838 취소 뒤 샘플 적용 버튼 잔류 → CancelPicking에서 미리보기 해제.
- 초기 UI 검사는 비연결 VisualElement라 입력이 전달되지 않았음(075136). 실제 EditorWindow에 연결하여 검사하도록 수정(075229 exit0). 이 환경 문제를 런타임 성공으로 계산하지 않음.
- 컴파일 075638에서 UI 포커스 null 병합 타입 오류를 수정. 중단 중 Unity가 만든 임시 폴더 메타 4개는 Logs/Tutorial/Composer01/interrupted-metas에 보존한 뒤 검증된 연결 해제 실행. 현재 테스트 하네스는 정상 해제 상태.
- GREEN 핵심: Logs/TestHarness/20261008-075724-656-Run.log exit0, results.txt 22 PASS.
- GREEN 에디터: Logs/TestHarness/20261008-075917-100-Run.log exit0. 실제 창에 연결된 패널의 선택·미리보기·취소·적용·Undo/Redo, 실제 LevelEditorWindow의 전용 모드/영역 생성, 오류 이동 버튼 존재 검사 통과. 검사 진입점 `Tutorial.Editor.TutorialComposerEditorVerification.Run`.
- 에디터 검사 중 보드 선택은 실제 포인터 대신 TutorialTargetPicked 콜백을 호출했다. 오류 이동도 버튼 존재까지만 검사했다. 시각 배치/포인터/이동 결과를 아직 검증했다고 주장하지 않는다.

## 다음에 우선 수행

1. 실제 포인터 이벤트로 구성→조건 수정→저장/재로드→Asset/MemoryPack 게임 화면까지 검사. 기존 TutorialDroneLevelVerification.Play.cs의 launcher·게임 입력·캡처 패턴 활용. 출시 레벨 대신 임시 시험 에셋/소유 씬만 사용/정리.
2. 샘플·복제 독립성, 오류 클릭 후 필드/셀 이동, 단계 전환 취소, 정렬/Undo, 패널 닫힘/리로드 구독 확인. 편집 화면 실제 렌더 캡처 필요.
3. 기존 핵심 경계 검사(All/Any·실제 분리/TL·연쇄·손상된 팩 등)와 영향 회귀, 구형 회귀 결과 로그는 먼저 보존.
4. `Run`은 현재 핵심 검사만 실행한다. 계획의 단일 실행문이 필요한 전체 조립 검사까지 수행하도록 연결하고 UI/Play 전용 진입점도 명시할 것.
5. source inspect 후 자체 리뷰와 수정, 2단계 계획·목표·실행문. 기존 소스·에셋 baseline 보존 최종 확인. 모든 완료 조건이 증명되기 전 goal complete 금지.

두 번째 묶음 마지막 세션 27662 exit0. 현재 실행 중인 검사 없음. 빌드·커밋·푸시 없음.

## 2026-10-08 세 번째 구현 묶음 — 실제 작성과 게임 실행

- `TutorialComposerWorkflowVerification.Run`: 실제 LevelEditorWindow와 임시 시험 에셋에서 PointerDown/Up으로 셀 선택, 매칭 샘플 선택/적용, 조건 수치 필드 수정, 복제 독립성, 삭제 Undo/Redo, 저장 버튼, 언로드/재로드, MemoryPack 왕복, 논리 재생을 검사한다. 콜백 직접 호출 검사와 구분한다. 17 PASS.
- UI로 만든 데이터만 Logs/Tutorial/Composer01/workflow.bytes로 보관한다. 실제 출시 레벨을 만들거나 편집하지 않는다.
- `TutorialComposerPlayVerification.Run`: 위 작성 데이터를 임시 100001번 에셋/팩으로 준비하고 에디터 ‘게임 플레이’ 버튼으로 Asset/MemoryPack 두 사례를 실행한다. 실제 게임 입력의 Begin/Update/EndPointer로 스와이프, 조작 제한, 매칭/낙하 후 완료, 일반 조작 복귀를 확인했다. 실행 당시 해당 팩 경로가 있으면 검사 중단하며 끝나면 생성한 팩/에셋을 정리한다.
- 실제 게임은 1280×720, 720×1280 양쪽에서 검사했다. `composer-play-0-720x1280.png`와 `composer-play-1-1280x720.png`를 직접 열어 두 선택 셀의 투명 강조와 주변 감광, 손가락/설명을 확인했다. 캡처4장은 Logs/Tutorial/Composer01에 있다. 런타임 예외 파일은 비어 있다.
- 초기 Play 검사에서 테스트의 잘못된 속성 HasCascade를 실제 IsPresenting으로 수정했다. 컴파일 실패 후 임시 폴더 메타는 보존/정리했다. 다음 실행은 레벨 선택 직후 비활성 버튼을 누르는 검사 준비 오류였으며 기존 검사처럼 CreateGUI를 다시 적용해 해결했다. 제품 코드를 우회하여 Launch를 직접 호출하지 않았다.
- 기존 회귀 3개 native exit0: LevelTutorialDataVerification.Run, TutorialRuntimeVerification.Run, TutorialGameIntegrationVerification.Run. 결과는 regression-results/Stage01~03, 요약은 regression-summary.txt. 기존 Stage01~03 로그는 regression-backup에서 복원했다.
- All/Any 조건과 다음 단계 초기화/지난 단계 지연 기록 무시 검사를 추가했다. 핵심 27 PASS.
- 계획의 `Tutorial.Editor.TutorialComposerVerification.Run`은 이제 핵심 → 실제 에디터 workflow → 실제 게임 Play를 하나의 Unity 실행에서 연속 검사한다. 빠른 핵심 전용 진입점은 RunCore. FullRun SessionState는 성공/실패 시 정리한다.
- 전체 연결 검사 GREEN: Logs/TestHarness/20261008-080832-388-Run.log native exit0, 마지막 세션4670 완료, 임시 테스트 연결 해제. 런타임 예외0. 기존 종료 시 JobTempAlloc 경고는 여전히 나타나며 성공과 별도로 기록한다.

## 최종 완료 감사에 남은 항목

- 새 팩4 손상/미등록 조건·무튜토리얼 혼합 경계와 실제 분리/T·L/연쇄 매칭 사례 추가. 기존 데이터 회귀만으로 새 경계까지 검증했다고 하지 않는다.
- 정적 이동 예산이 다중 교환 요구량을 고려하는지 확인/필요시 수정. 오류 클릭 후 실제 필드/셀 이동까지 확인(현재 버튼 존재만 확인).
- 편집 창 시각 배치 검토, 단계 전환 취소/정렬/새 샘플 독립성 등 계획과 대응 점검.
- 자체 코드 리뷰, 실제 레벨/팩/HEAD 보존 최종 감사, 필요한 후속2단계 문서와 실행문 작성. 목표 완료는 아직 표시하지 않음.


## 2026-10-08 최종 완료 감사

### 구현과 검사 보강

- 팩4 확장 누락/중복/잘못된 단계/미등록 조건 거절, 무튜토리얼 보존을 검사했다.
- 실제 퍼즐 실행의 분리된 3매칭 두 묶음, 연결 T/L 한 묶음, 자동 연쇄 매칭의 직접 전용 제외/포함 및 중복 제외를 검사했다. 최종 빈칸 수로 추정하지 않는다.
- 이동 수보다 많은 교환 요구를 검출하지 못하는 실패를 081055 로그에서 확인하고 정적 검사에 반영했다. All은 필요한 교환의 최댓값, Any는 가능한 대안의 최솟값으로 판단하며 매칭 묶음 수를 이동 수로 오인하지 않는다. 081138 이후 통과했다.
- 실제 창에서 단계 위/아래 정렬, 단계 변경 시 선택 취소, 오류 클릭 후 횟수 필드 포커스와 셀 표시를 확인했다. 편집 창의 좌/중앙/우/하단 배치는 실제 UI layout bounds로 검사했다. 편집 창 스크린샷 육안 검수는 수행하지 않았다.
- 최종 Play 검사에 선택한 Asset/MemoryPack이 실행 요청의 source에 반영되는 검사를 추가했다. 즉시 읽은 안내 Label은 주기 갱신 전 값일 수 있어 입력 경로의 증거로 사용하지 않는다.

### 요구사항과 직접 증거

| 계획/완료 조건 | 확인한 근거 |
| --- | --- |
| A 구버전/새 데이터 저장 계약 | legacy-v3 fixture 전체 레벨 byte 동일, 팩4 조건/자동 강조 확장, 잘못된 확장 거절. LevelPackCodec.Composer 및 저장 ADR |
| B 실제 교환·매칭·누적·All/Any | core results 45 PASS: 2회 누적/실패 취소/이전 ticket 제외/단계 초기화/정확히·이상·색상·직접·연쇄/실제 묶음 검사 |
| B 논리·연출 대기 | core의 미충족/완료 모두 연출 대기, Play의 실제 교환·매칭·낙하 종료 후 완료/일반 입력 복귀 |
| C 샘플·셀 선택·조건 편집 | Workflow 실제 창 PointerDown/Up → 미리보기 원본 유지 → 샘플 적용 → 조건 필드 수정. 별도 EditorVerification은 취소/샘플 Undo/Redo 경로 확인 |
| C 단계/강조/오류 | Workflow 복제 독립성·정렬·단계 전환 취소·삭제 Undo/Redo·오류 필드/셀 이동; core 현재 조작 셀 자동 강조. 수동 강조는 기존 선택 경로 유지 |
| C 데이터/샘플 보존 | TutorialSampleCatalog 매 호출 새 데이터, 복제 조건 독립 검사, 시험은 소유한 임시 에셋으로만 수행 |
| D 저장→실행 연결 | Workflow 실제 저장 버튼/언로드/재로드→workflow.bytes→Play에서 게임 플레이 버튼으로 Asset/MemoryPack 실행 |
| D 실제 안내 화면 | 가로1280×720/세로720×1280 캡처, 선택 셀 alpha0·입력 비차단, 미지정 조작 차단, 손가락/설명, 종료 후 해제. 0-세로와 1-가로 캡처 육안 확인 |
| D 회귀 | LevelTutorialDataVerification, TutorialRuntimeVerification, TutorialGameIntegrationVerification 각각 native0. 별도 regression-results/Stage01~03에 결과 보존, 기존 출력 복원 |
| 보존/인계 | 시작 레벨/메타/팩 해시 확인, HEAD 유지, 다음 composer-02 계획/목표/명령 작성. 실제 출시 튜토리얼은 작성하지 않음 |

### 최종 실행 결과

- `Tutorial.Editor.TutorialComposerVerification.Run`: `Logs/TestHarness/20261008-082322-863-Run.log`, native exit 0, 테스트 연결 해제 완료.
- `Logs/Tutorial/Composer01/results.txt` 45 PASS, workflow-results.txt 32 PASS, play-results.txt 24 PASS. 합계 101개 검증 항목 PASS/0 FAIL이며 독립된 101개 테스트 케이스라는 뜻은 아니다.
- `play-exceptions.txt` 0 bytes. 기존 Unity 종료 시 JobTempAlloc 경고는 별도이며 모든 경고가 없다는 주장은 하지 않는다.
- 회귀 로그 `regression-summary.txt`와 `regression-results/Stage01~03` 참조. 전 프로젝트 테스트/모든 드론 회귀를 실행한 것은 아니다.

### 자체 검토와 범위

하위 에이전트 없이 작성자가 코드와 호출부를 재검토했다. 독립 리뷰 완료로 표현하지 않는다. 저장 확장은 기존 팩3 layout을 변경하지 않고, 조건 판정은 등록 목록과 실제 사건을 사용하며, ticket/단계 경계에서 지연 사건을 제외한다. 편집 선택은 배치 입력과 분리하고 Undo 및 창 재구성 시 구독/미리보기를 정리한다.

이번 조건 대상은 교환/매칭 종류·색상·크기·원인이다. 조작 셀과 수동/자동 강조는 별도 데이터다. 일반화한 개체·영역 대상/내구도 등은 2단계이며, 공유 흐름·사용자 샘플·학습 ID·기존 전체 전환/구형 경로 제거는 3단계다. 보존 중인 구형 결과 조건을 제거했다고 주장하지 않는다.

실제 Android/iOS 터치·노치·실기기 성능은 미검증이다. 이번 증거는 Windows Unity 편집 창과 Play Mode의 실제 게임 화면/입력 경로에 한정한다. 플레이어/번들 빌드·커밋·푸시는 하지 않았다.

[2단계 계획](../../../Planning/MoonRabbitJunkyard/Tutorial/composer-02-plan.md) · [2단계 목표](../../../Goals/MoonRabbitJunkyard/Tutorial/composer-02-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/Tutorial/composer-02-command.md).

최종 정리: 테스트 포함 컴파일 `20261008-082454-415-Compile.log` exit0 후 연결을 해제했다. 이어 테스트 폴더가 없는 상태에서 Unity `-batchmode -quit` 단독 컴파일을 실행했고 `Logs/Tutorial/Composer01/game-only-compile-result.txt` ExitCode=0, 컴파일 오류0을 확인했다. `final-preservation.txt`에 레벨/메타/팩9개 해시·HEAD 보존과 임시 검사 에셋/팩/연결 없음 결과를 기록했다. 문서 상대 링크와 `git diff --check`도 통과했다. Addressables 설정은 Unity import로 줄바꿈 경고가 발생하지만 의미상 diff/numstat는 없다.

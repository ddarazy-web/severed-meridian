# 게임·제작 도구 분리 5단계 진행 기록

> 2026-10-09 · 구현·검증 완료. 아래는 시간순 이력이며 마지막 완료 감사를 최종 상태로 따른다.

[계획](../../Planning/project-wide/game-authoring-stage-05-plan.md) · [기능 대응표](game-authoring-stage-05-feature-matrix.md)

## 현재 진행

- 기능 17항목을 기존 Editor 화면/공통 API/새 화면/검증 조건에 대응시켰다. 기본 경로 전환은 전 항목과 종료 보호 검증 뒤 수행한다.
- 4단계 UX 후속: 팔레트 선택 표시 즉시 갱신, 거절된 미션 수량을 실제 초안 값으로 복원. 실제 UI 검사 ux-red에서 선택 표시 실패를 재현하고 ux-green exit0에서 두 항목 및 기존 게임/저장 왕복 확인.
- 작업 공간 복구 직렬화: 폴더·복구본 보호 여부·초안·선택·Undo/Redo·저장 revision 보존. 잘못된 복구본은 현재 작업을 바꾸지 않는다. 복구 뒤 외부 수정 충돌 검사를 유지한다.
- ToolDraftStore: 제작 JSON과 별도 원자적 복구 기록, 이전 정상본, 늦은 이전 쓰기의 최신 상태 덮어쓰기 방지. journal-red에서 미구현을 확인한 뒤 journal-green **238 PASS**.
- 실행 화면의 자동 기록·복원 안내·저장/버리기/취소 종료를 연결했다. 초기 lifecycle-green/reload-first 검사는 복구 경로 격리가 불완전하여 최종 근거로 사용하지 않는다. SerializedObject와 프리팹 override로 검사 경로를 지정하고 Play 안에서도 경로를 확인하도록 보완했다. reload-isolated-red에서 미완성 흐름 입력 유실을 재현하고 수정한 뒤 **reload-isolated-green exit0**에서 실제 스크립트 재로드·Play 종료/재진입 후 초안/선택/Undo/Redo/미완성 흐름 입력을 확인했다. play-isolated-green exit0에서 실제 게임 왕복과 저장 UI·복구·저장/버리기/취소·작은 창 검사를 재확인했다.

- 공급 편집 UI를 추가했다. 다중 생성구 방식, 고정 목록·순서·수량·색·방향·내구도, 소진 정책과 유지 수량/정의를 공통 연산으로 연결했다. supply-green exit0 이후 개별 항목/순서/소진·삭제·실제 문서 Undo 검사를 확대했다.
- 흐름·포털·벽·도착점·합류 우선순위, 발전기/목표 연결·전선 꼭짓점, 보드 모양 등록/사용처/새 레벨 편집을 공통 API와 연결했다. flow-green/connection-green/shape-green exit0. advanced-isolated-green exit0에서 확대된 공급/합류/충전/모양 사례와 중복 이름 선택을 확인했다. 레벨 전환에 미완성 입력이 넘어가는 실패를 재현하고 취소 처리 후 통과했다. 폴더 교체에서도 세션 소유자가 바뀌면 입력을 취소한다.
- 이전 검사에서 실제 앱 복구 경로에 남긴 두 파일은 알려진 fixture 폴더와 일치함을 확인하고 Logs/GameAuthoringStage05/isolated-legacy-test-journal에 해시가 같은 사본으로 보관 후 앱 경로에서 제거했다. 제품 원본 JSON을 삭제하지 않았다.

## 튜토리얼 공통화와 실행용 화면

- Tutorial/Authoring의 TutorialAuthoringRules에 기존 공유 구성 연결/동기화/독립화 및 샘플의 생성 연결 이름 재작성·독립 ID 규칙을 추출했다. 기존 Editor 창은 같은 규칙을 호출하며 Unity Undo만 어댑터에 남긴다. SampleCatalog/SampleBoards도 GUID를 보존하여 같은 공통 영역으로 옮겼다.
- LevelAuthoring/Runtime의 AuthoringObjectGraph는 JSON 참조를 임시 제작 객체로 변환하고 수명을 관리한다. 기존 JsonPuzzlePlayAdapter와 새 TutorialDraftEditing이 이를 공유한다. JSON 저장 원본/Undo는 AuthoringEditSession에 남으며 SO 파일을 생성하거나 저장하지 않는다.
- TutorialDraftEditing은 튜토리얼 영역만 변경한다. 공유 구성 생성+연결, 독립 복사, 공유 원본 편집, 샘플 등록/적용의 트랜잭션을 제공한다. 이 API가 있다는 사실만으로 관련 화면이 모두 완성된 것은 아니다.
- 실행용 튜토리얼 페이지에 단계 추가/복사/삭제/순서·안내문·행동·선택 칸/영역 적용·자동 강조 설정·이전 생성 연결 이름·무료 아이템·등록된 10종 조건 필드를 연결했다. 조건 대상/원인 설정은 기존 capabilities를 사용한다. 남은 내구도는 정확한 값만 제공하며, 미션 선택은 번호와 종류/색상 식별값을 함께 기록한다.

| 검사 | 결과 |
|---|---|
| 공통 제작 규칙 | tutorial-common-red에서 런타임 규칙 누락 → tutorial-common-green exit0. 공유 값 보존, 실패 원자성, 샘플 연결명/ID·입력 불변 |
| 기존 Editor 공유/샘플 | tutorial-editor-regression exit0 |
| JSON 튜토리얼 트랜잭션 | tutorial-json-red에서 API 누락 → tutorial-json-green exit0. 다른 필드 불변·실패/Undo·공유 구성·독립화·샘플 |
| 실행용 실제 UI | tutorial-ui-red에서 페이지 누락 → tutorial-ui-green-2 및 tutorial-ui-expanded exit0. 단계·선택 칸·매칭·10종 조건·정확한 내구도·생성 연결/영역·미션·Undo |
| 기존 조건/샘플/재생 | tutorial-conditions-regression exit0, Logs/Tutorial/Composer02/results.txt의 352 PASS |
| 공유 객체 변환의 실제 게임 회귀 | json-play-graph-regression exit0. 기존 JSON 게임 요청/정식 기록/실패·취소 객체 정리 |

초기 UI 검사 컴파일에서 Simulation namespace 누락을 수정했다. 당시 소유 기록이 있는 테스트 폴더 메타만 복구 후 연결 해제했다. 모든 로그는 검사 범위의 근거이며 튜토리얼 기능 동등성 전체 완료 근거가 아니다.

## 공유 사본·레벨 설정·샘플 화면

- SharedTutorialDraft는 기존 AuthoringEditSession의 독립 사본을 사용한다. 부모 이력은 이어받지 않고, 원본 적용 시 부모에는 한 번의 변경만 기록한다. 편집 도중 원본이나 연결이 바뀌면 적용을 거절한다. 현재 사본을 게임 시험용 snapshot으로 합치는 경로도 연결했으나 사본 상태의 실제 게임 왕복 확대 검증은 남아 있다.
- 공유 사용처, 구성 생성/연결·명시적 원본 적용/사본 버리기, 12종 설정 선언과 레벨별 값, 목록 동기화·독립 복사를 실행용 화면에 연결했다. 원본 편집 중 폴더/레벨/팔레트 전환을 잠그고 Undo/Redo는 사본에 적용한다. 사본의 선택 상태와 이력은 복구 기록에 함께 저장한다.
- 공유 사본을 버린 뒤 부모 종료를 취소할 때 화면이 남는 문제를 tutorial-shared-exit-red에서 재현했다. 사본을 닫은 뒤 부모 화면을 갱신하여 tutorial-shared-exit-green exit0으로 확인했다.
- 선택한 두 칸으로 매칭/교환 단계 생성, 조건 조합 샘플과 대상 셀 적용, 현재 단계/전체 흐름의 JSON 샘플 등록·미리보기·독립 적용을 제공한다. 샘플로 등록할 단계도 따로 선택할 수 있다. 시험 보드는 기존 레벨을 바꾸지 않고 새 레벨로 추가하며, 저장하면 작업 폴더에 포함됨을 툴팁에 명시했다.
- Tools/Testing/LevelToolChecks.ps1에 현재 추가된 공통 규칙·공유 사본·고급 UI·재로드 검사를 포함했다. 이 확장된 All 전체 실행은 최종 통합 검증에서 수행한다.

| 검사 | 결과 |
|---|---|
| 공유 사본 API | tutorial-shared-red → tutorial-shared-green exit0. 독립 Undo·직렬화 복원·명시 적용·원본 충돌 거절 |
| 공유 UI | tutorial-shared-ui-red → tutorial-shared-ui-green exit0. 생성/사용처·사본 편집/적용·선언·레벨 값·취소·독립화 |
| 모든 설정 종류 | tutorial-all-bindings-ui exit0. 12종 값 각각 UI로 변경한 결과와 원본 불변 확인 |
| 실제 사본 재로드 | tutorial-shared-reload exit0. 스크립트 재로드 및 Play 종료/재진입 후 미적용 내용·사본 Undo/Redo·화면 복원, 원본 미적용 확인 |
| 샘플 UI | tutorial-samples-ui-red → tutorial-samples-ui-green 및 tutorial-sample-selection-expanded exit0. 두 단계 중 선택한 단계만 등록·미리보기/독립 사본·선택 대상·새 시험 보드·Undo |
| 독립 JSON 회귀 | portable-shared-fork exit0, 239 PASS |

## 아직 완료하지 않은 범위

- 씬 교체 및 종료 시 저장 실패·파일 처리 중 중단 검사 확대.
- 튜토리얼 생성 공급 및 논리 재생/오류 위치 연결. 공유 사본 실제 게임 시험·기존 Editor와의 결과 대조 및 샘플의 최종 통합 검증.
- 튜토리얼 보드 강조 미리보기/2×2 대상, 선택 확정·취소와 새 UI 상태 재로드, 조건별 세부 조합·실제 게임 재생 및 시각 검수. 추가된 공급/흐름/연결/모양의 최종 통합 회귀.
- 자동 시험·시드 비교·여러 레벨 시험·결과/난이도 기록과 위치별 오류 안내.
- 기능 동등성 검증 후 기본 메뉴 전환 및 기존 중복 UI 정리.
- 최종 독립 리뷰·전체 검증과 6단계 계획/목표/복사용 실행문·완료 커밋 메시지.

커밋·푸시·Player/Addressables 빌드·HTML 매뉴얼 수정은 하지 않는다. 5단계 전체 목표는 진행 중이며 완료 처리하지 않는다.

## 튜토리얼 고정 공급 편집

- ‘튜토리얼 공급’ 편집 항목에서 생성구 추가/삭제, 항목 종류·색·방향·내구도·개수, 생성 순서 및 항목 삭제를 제공한다. 일반 공급 화면과 공통 SupplyDocumentEditing을 재사용한다.
- EditTutorial은 복제한 레벨에서 공통 공급 연산을 수행하고 tutorial.supply만 반영한다. 고정 순서/소진 후 중단을 강제하며 무작위 일반·파워 공급은 거절한다. 일반 공급 설정은 변경하지 않는다.
- 기존 SourceCellError는 활성 칸과 도착점 충돌을 검사하며 기존 일반 생성구 존재를 요구하지 않는다. 튜토리얼도 이 규칙을 따른다.
- tutorial-supply-red에서 범위 편집 API 부재를 확인했다. 최초 후속 검사에서는 fixture에 이미 있던 생성구에 추가하여 거절 조건을 만들지 못했으므로 시험용 튜토리얼 공급을 명시적으로 비운 뒤 재검사했다.
- tutorial-supply-green: 독립 JSON 검사 242 PASS, exit0. tutorial-supply-ui: Unity 고급 화면 검사 exit0(TestHarness/20261009-033929-708-Run.log). 고정/중단, 일반 설정 보존, 순서 변경·Undo, 삭제와 기존 공급/흐름/연결/모양 회귀를 확인했다. 테스트 연결 해제 완료.
- 논리 재생/오류 위치 이동, 강조·선택 UX, 공유 사본 게임 왕복, 자동 시험/결과/여러 레벨 기능 및 최종 전환 검증은 계속 남아 있다. 5단계 전체 완료가 아니다.

## 논리 재생 검사와 오류 위치 안내

- 레벨 검사/튜토리얼 재생 검사 버튼을 추가했다. 문서 구조·snapshot 참조 검사를 거친 뒤 기존 LevelDefinitionValidator 또는 LevelTutorialReplayValidator를 사용한다. 공유 사본은 게임 시험과 같은 Preview 충돌 검사를 거친다.
- 오류의 문서 ID·PropertyPath·Coordinate를 보존하여 단계/공급/흐름/연결 화면 및 셀 선택에 연결했다. 편집 문서가 검사 당시와 다르면 이전 오류 링크를 비활성화하고 재검사를 안내한다. 비레벨 문서는 현재 ID와 사유를 보여주며 직접 편집 화면 이동까지 완성한 것은 아니다.
- tutorial-validation-red에서 검사 버튼 누락을 확인했다. tutorial-validation-green exit0은 정상 샘플, 오류 단계 이동, 변경 후 결과 만료와 전체 편집 상태 불변을 확인했다.
- tutorial-validation-expanded의 Unity 로그(TestHarness/20261009-034514-137-Run.log)는 3매칭, 고정 공급의 두 번 교환, 생성 로켓 후속 행동 재생과 원본/이력 불변, 지정 오류 셀 선택을 통과했다. 일반 화면과 실제 손 입력/연출 검수는 이 논리 검사와 구분한다.
- 자동 시험·여러 레벨 결과, 공유 사본의 실제 게임 왕복, 강조/선택 UX, 저장 실패·씬 이동, 최종 기능 동등성 및 기본 메뉴 전환은 남아 있다.

## 튜토리얼 강조 미리보기

- 기존 Editor의 대상 강조 계산을 TutorialAuthoringRules.PreviewTargetHighlights로 옮겨 공통화했다. 첫 행동의 개체/종류 대상은 실제 배치 크기를 사용하므로 2×2 내부 칸을 지정해도 전체를 강조한다. 후속 행동은 움직인 위치를 추측하지 않으며, 보드 전체 조건은 전체 화면을 밝히지 않는다.
- 실행용 튜토리얼 페이지에 강조 미리보기 토글을 추가했다. 강조 칸에는 덮개를 올리지 않고 나머지 칸만 검정 65% 가림막으로 표시한다. PickingMode.Ignore로 보드 입력을 막지 않으며, 비동기 이미지가 추가된 후 가림막을 다시 앞으로 올린다. 미리보기 설정은 복구 view에 저장한다.
- 공통 규칙은 tutorial-highlight-red(API 없음) → tutorial-highlight-green exit0으로 확인했다. 시험 준비 코드의 읽기 전용 목록 접근 컴파일 오류는 List 캐스팅으로 수정했고, 소유 기록에 있는 임시 테스트 폴더만 복구/해제했다.
- tutorial-preview-red에서 UI 토글 누락을 확인했고 tutorial-preview-green exit0은 가림막 생성·표시 해제·원본 불변과 기존 10종 조건 편집을 통과했다. 추가 투명/입력/복구 검사 결과는 후속 기록을 따른다.
- 실제 화면 시각 검수와 선택 확정/취소 UX 및 pending 선택 재로드 검사는 아직 남아 있다.
- tutorial-preview-expanded exit0(TestHarness/20261009-035441-619-Run.log): 강조 조작 칸에 가림막 없음·입력 비차단·복구 view 설정 포함 확인. 테스트 연결 해제, git diff --check 통과. 실제 도메인 재로드/시각 검수 완료를 뜻하지 않는다.

## 선택 확정·취소 작업

- 조작 위치·강조/자유 영역·조건 대상과 레벨별 좌표/영역 설정에 ‘보드에서 선택’ 진입을 추가했다. 기존 현재 선택 적용 버튼도 유지한다. 선택 중에는 다른 편집/저장을 잠그고 클릭 순서를 유지한다. 재클릭 제외, 확정/취소, Enter/Esc를 지원한다.
- pending 선택은 문서와 이력에 적용하지 않는다. 원본 사본·레벨/공유 사본 식별값·단계·조건/설정 키를 함께 저장하고, 확정 시 원본이 달라졌으면 거절한다. 원본 사본 일치 확인으로 단계 인덱스가 다른 단계로 변하는 문제를 막는다. 재로드 기록에 pending 상태도 보관한다.
- tutorial-pick-red: 선택 버튼 누락. 초기 green 검사에서 빈 설정 키를 문자열 타입으로만 구분하여 잘못된 binding 경로로 들어가는 오류를 발견했다. tutorial-pick-diagnose에서 null Enum.Parse를 확인하고, 키 값이 실제로 있을 때만 binding 경로를 사용하도록 수정했다.
- tutorial-pick-fixed 로그의 선택 취소/원본·이력 불변/잘못된 세 칸 거절/클릭 순서/단일 Undo 검사는 통과했다. 실제 재로드·레벨별 설정 선택 검사 결과는 후속 기록을 따른다.
- tutorial-pick-fixed exit0(TestHarness/20261009-040352-295-Run.log): 잘못된 세 칸 조작은 pending 유지·원본 불변, 클릭 순서·단일 Undo 확인. tutorial-pick-reload exit0(TestHarness/20261009-040603-982-Run.log): 실제 스크립트 재로드와 Play 종료/재진입 후 pending 선택 복원, 복원 후 확정은 공유 사본에만 반영. 테스트 연결 해제 완료.
- tutorial-pick-bindings exit0(TestHarness/20261009-040730-344-Run.log): 레벨별 First/Second/ActionArea/Highlights/Target 보드 선택 확정과 공유 원본 불변 확인. 테스트 연결 해제 및 git diff --check 통과.

## 자동 시험 연결 시작

- AutoPlay.BotBatchStore를 Runtime/Storage로 이동하고 기존 파일 GUID를 보존했다. Editor 저장 위치 기본값과 호출부는 Levels.Editor의 얇은 어댑터로 유지한다. 런타임 호출자는 저장 루트를 명시한다. 판 파일→요약 원자 교체, 누락·손상·중단 복구, 버전 비교 규칙은 공유한다.
- bot-storage-red에서 런타임 저장소 부재를 확인한 뒤 bot-storage-green exit0으로 기존 반복 실행/일시정지/중단·저장/복구·잠금 실패 검사를 통과했다.
- 실행용 ‘자동 시험’ 페이지에 시드/전략, 새 시험·직전 시작 사본으로 재시험, 한 수·한 판·중지와 최근 행동/이유를 연결했다. 기존 BotPlaySession을 프레임별 단위로 진행한다. 편집 문서를 바꿔도 진행 중 시험의 사본을 변경하지 않는다. 실제 게임 진입 또는 화면 비활성화 시 실행기를 정리한다.
- bot-ui-red에서 화면 누락을 확인했고 bot-ui-green exit0(TestHarness/20261009-041458-992-Run.log)은 한 수의 후속 처리 완료, 같은 시드/사본의 첫 행동 재현, 판 종료, 중단과 편집 내용·선택·Undo 이력 불변을 확인했다.
- 아직 반복 묶음의 실행용 화면/비동기 저장 연동, 기록 조회·난이도/시드 비교·여러 레벨 시험, 봇 보드 표시와 시각 검수, 재로드 결과 보존을 완료한 것은 아니다. 새 화면은 현재 한 판 제어와 텍스트 행동 기록을 제공한다. 기존 에디터 기본 진입은 그대로다.
- bot-ui-final exit0: 대기 중 매 프레임 화면 문자열을 갱신하지 않도록 정리한 실행 경로도 같은 검사를 통과했다. 테스트 연결 해제 및 git diff --check 통과.

## 반복 시험의 비동기 저장 경계

- BotBatchStore.PrepareSave는 메인 스레드에서 기록을 불변 문자열로 만들고, 반환한 파일 작업만 백그라운드에서 실행할 수 있게 한다. 기존 Save/Editor 어댑터는 같은 작업을 동기 호출하여 기존 계약을 보존한다.
- PersistedBotBatch가 기존 BotBatchSession을 소유하며 체크포인트 저장이 끝나기 전 다음 판을 진행하지 않는다. 저장 확인 후에만 판 요약을 공개하고, 마지막 저장도 HasWork에 포함한다. 저장 중 중지는 경계에서 처리한다. StopAndDisposeAsync는 종료 시 남은 기록을 마친 뒤 실행기를 정리하는 진입점이다.
- 실제 저장 실패는 PersistenceError/UnsavedGame과 오류 상태로 보존한다. 기록을 정상 성공으로 숨기거나 자동 재시도로 중복 판 파일을 만들지 않는다.
- bot-persisted-red에서 실행 소유자 부재를 확인했고 bot-persisted-green exit0(TestHarness/20261009-042210-774-Run.log)은 초기 저장 전 실행 차단, 일시정지/재개, 두 전략 저장 완료 후 집계, 디스크 재조회, 원본 불변, 저장 중 중지, 파일이 폴더 자리를 차지한 실제 IO 실패와 기존 반복 시험 회귀를 통과했다. 테스트 연결 해제 완료.
- 이 변경은 실행·저장 경계다. 반복 시험 화면, 결과/난이도 조회, 종료 시 비동기 정리의 실제 Play 검증은 아직 연결/검증해야 한다. JSON 작업 폴더의 카탈로그 참조까지 독립 재생할 수 있도록 원본 snapshot 동반 보관 방식도 다음 연결에서 처리한다.

## 반복 시험 화면과 독립 원본 기록

- 자동 시험에 전략별 횟수, 새 반복/직전 사본·시드 묶음 재시험, 일시정지·재개·중지, 저장 상태와 최근 판 결과를 연결했다. 단일 봇/반복 시험의 동시 실행을 막고, 반복 저장이 끝나기 전 실제 게임 시험을 시작하지 않는다.
- AuthoringTrialSource는 선택 레벨 ID와 참조 문서 전체를 독립 JSON으로 저장한다. BotBatchStore는 source.context를 첫 판/요약보다 먼저 원자 저장한다. 기존 판 JSON 순회와 충돌하지 않는다. Unity 임시 객체 ID 대신 이 원본에서 카탈로그까지 다시 구성할 수 있다.
- 화면 비활성화는 StopAndDisposeAsync로 기록 완료 뒤 해제한다. 앱/Play 종료처럼 다음 프레임을 보장하지 않는 경로에는 마지막 저장만 동기 확정하는 StopAndDisposeAtShutdown을 제공하고 OnApplicationQuit에 연결했다. 강제 프로세스 종료까지 완료 보장은 하지 않으며 기존 원자 파일 복구 규칙을 따른다.
- trial-source-red(형식 부재) → trial-source-green: 독립 검사245 PASS. batch-ui-red(화면 부재) → batch-ui-green exit0: 일시정지/재개·두 전략 저장 완료와 전체 편집 상태 불변.
- batch-source-ui exit0(TestHarness/20261009-043056-413-Run.log): 디스크 원본 전체 JSON 일치 및 새 AuthoringObjectGraph로 카탈로그를 포함한 실행 fingerprint 일치 확인.
- batch-source-core exit0(TestHarness/20261009-043149-539-Run.log): sidecar/판 조회 공존, 종료 저장 확정과 중단 표시, 실제 IO 실패·기존 반복 회귀. 테스트 연결 해제와 git diff --check 통과.
- 아직 저장된 이력 조회/난이도·시드 비교·여러 레벨 및 이동 수 추천 화면, 실제 종료 이벤트·재로드의 반복 기록 회복 확대, 봇 보드 표현/시각 검수 및 최종 기본 진입 전환은 남아 있다.

## 저장된 시험 조회·분석·재생 연결

- BotAnalysisReader/Catalog/Export를 AutoPlay/Runtime/Storage로 이동하고 기존 .meta GUID를 보존했다. 기존 Editor 화면도 같은 로더를 사용한다. 선택적인 복원 정의 입력으로 독립 JSON에서 재구성한 카탈로그를 검증하며, 로더가 호출자 정의를 수정하거나 파괴하지 않는다.
- LevelToolScreen.History.cs에서 저장 이력 조회, 판별 검증, 두 전략 성공률·동일 시드 비교·난이도 보류 이유, 20판씩 목록, 행동 재생/일시정지/닫기, 새 폴더 보관을 연결했다. 보관본에 source.context도 포함한다. 재생은 기존 BotRecordReplay를 사용한다.
- analysis-runtime-red: 실행용 공개 로더 부재 재현. analysis-runtime-green exit0(TestHarness/20261009-043803-043-Run.log): 기존 반복 검사 및 복원 정의로 읽기·원본 동반 내보내기 통과.
- history-ui-red: history-refresh 버튼 부재 재현. history-ui-green exit0(TestHarness/20261009-044058-008-Run.log): 실제 UI 콜백으로 저장 이력 조회/통계 표시, 디스크 원본을 복원한 전체 행동 재생이 행동·이동 수·난수·종료·미션과 일치, 전체 편집 세션 불변. 테스트 연결 해제 완료.
- 기존 200판 분석 검사에서 과거 Library 캐시의 첫 기록을 고르는 방식이 현재 레벨 지문과 불일치했다(analysis-legacy-regression). 원래 로더에도 동일 지문 검사가 있었으며 제품 검증을 완화하지 않았다. 검사 자료를 현재 규칙으로 직접 생성하도록 바꿨다. 첫 4×4 자료는 시드 776에서 재배치 탐색 한도에 걸려 중단되었고, 정상 완료 기록의 분석 검사용으로 표준 9×9/이동 3/100시드 자료로 조정했다. 오류 처리 규칙은 유지한다.
- **진행 중 검사:** analysis-current-standard.log / TestHarness/20261009-044440-362-Run.log. 200판 생성 후 기존 보관·손상 검사·전체 행동 재생을 수행한다. 아직 종료하지 않았으므로 통과로 계산하지 않는다. 이 검사를 LevelToolChecks 전체 목록에도 추가했다.
- 남은 결과 화면 동등성: 기준 기록과의 비교, 전략/결과 필터, 상세 이동/잔여 미션 통계·행동 설명, 편집 원본과 기록의 동일성 표시, 보드 시각 재생. 여러 레벨/이동 수 추천, 실제 종료·재로드 기록 검증, 공유 튜토리얼 게임 왕복·시각 검수, 전체 기능 대응 및 기본 진입 전환도 아직 남아 있다.
## 결과 상세와 기존 분석 회귀 완료

- analysis-current-standard.log / TestHarness/20261009-044440-362-Run.log **exit0**. 표준 9×9에서 100시드×두 전략의 200판을 정상 생성했고, 기록 복사·손상/누락/변경 거절 및 200판 전체 행동 재생이 통과했다. 재생 후 정의 JSON/dirty 불변, 엔진 버전 거절과 부분 기록 종료도 확인했다. 이전 ‘진행 중 검사’는 이 결과로 종료됐다.
- history-detail-red에서 전략 필터 부재를 재현했다. HistoryDisplay.cs에 전략/종료 결과 필터, 기준 기록 비교, 규칙 버전이 같은 공통 시드 비교, 성공 판 이동 통계와 패배 잔여 미션, 저장된 행동/난수 소비 설명, 현재 편집 정의와 기록의 관계 표시를 연결했다.
- history-detail-green.log / TestHarness/20261009-050422-163-Run.log **exit0**. 계획 전략 필터의 원래 판 번호 보존, 같은 정의 비교 표시, 미션 통계·행동 상세, 디스크 원본 재생 결과 및 전체 편집 상태 불변을 확인했다. 테스트 연결 해제, diff check 통과.
- 결과 화면의 보드 시각 재생과 상세 UX 확대(다른 기록/버전 간 비교·종료 필터·보관 실패 화면 검증)는 남아 있다. 여러 레벨 및 이동 수 추천, 실제 종료/재로드 기록 보호, 공유 튜토리얼 실제 게임·전체 기능 동등성·기본 편집 진입 전환도 아직 완료하지 않았다.
## 봇·반복·기록 재생의 이미지 보드

- TrialBoardView가 시험 사본의 ElementVisualLookup/PuzzleArtwork를 사용해 실행 상태를 읽기 전용으로 표시한다. 일반/파워/장애물·바닥·덮개 프레임, 2×2 기준점/크기와 이미지 방향·오프셋을 동일한 시각 정의에서 가져온다. 현재 편집 카탈로그로 과거 기록의 모양을 바꾸지 않는다.
- 상태가 같은 동안 셀을 다시 만들거나 이미지를 다시 요청하지 않는다. 비동기 로딩 중에는 최신 요청만 표시하며, 화면 해제 시 보드가 소유한 이미지 리소스를 반환한다. 단일 봇·반복 현재 판·기록 재생에 연결했다.
- trial-board-red: 보드 부재 재현. trial-board-green exit0(TestHarness/20261009-051104-334-Run.log): 실행/재생 상태의 모든 셀 표시와 기존 봇/결과/편집 불변 검사 통과. trial-board-artwork exit0(20261009-051259-076): 다음 프레임까지 기다린 뒤 실제 Sprite와 대체 문자 제거 확인.
- 실제 렌더링에서 오른쪽 패널의 너비 때문에 열이 가려져 중앙 보드 영역으로 이동했다. 자동 시험 페이지에서는 편집 보드를 숨기고 실행 보드만 표시한다. 다른 편집 페이지로 돌아오면 원래 보드를 다시 표시한다. trial-board-layout exit0(20261009-051657-780)에서 9열 표시와 캡처 확인.
- 캡처에서 드러난 상단 버튼/보드 겹침을 tool-header-red에서 좌표 검사로 재현했다. 제목·도구줄·상태줄 높이의 flex 축소를 막았고 tool-header-green exit0(20261009-051953-788)에서 겹침 해소를 확인했다. 최종 캡처: Logs/GameAuthoringStage05/trial-board-screen.png. 이미지/셀 표시와 상단 버튼이 보이는 화면을 직접 확인했다. 테스트 연결 해제 완료.
- 위 이미지 검사는 일반 블록과 생성된 파워를 포함한 실제 실행/재생 화면이다. 2×2·커스텀 시각 정의의 전용 사례, 비교/종료 필터·실패 화면 확대, 여러 레벨/이동 수 추천, 실제 종료·재로드·공유 튜토리얼 게임 왕복, 전체 대응표 및 기본 진입 전환은 추가 검증/구현이 남아 있다.
## 여러 레벨·이동 수 시험의 런타임 분리

- MultiLevelTestSession/BotMoveBalanceSession을 Runtime/Execution으로, MultiLevelTestStore/BotMoveBalanceStore/BotMoveBalanceReader/TestRecordPaths를 Runtime/Storage로 이동했다. 이동한 원본 스크립트의 .meta GUID는 보존했다.
- 기존 에디터의 MultiLevelTestSession은 AssetDatabase로 GUID만 조회하는 얇은 어댑터로 남았다. 경로 검사의 Editor 어댑터도 같은 런타임 규칙에 위임한다. 기존 호출부에서 달라진 namespace를 명시했다. 새로운 봇·퍼즐 실행 규칙은 만들지 않았다.
- 여러 레벨 실행은 시작 순간의 LevelPackCodec.Copy 사본을 소유하고 레벨별 실행을 이 사본에서 만든다. 기존 JSON-only 재구성으로 비직렬화 런타임 카탈로그를 놓치는 경로를 없앴다. 사본은 초기화 실패/Dispose에서 정리한다.
- multi-runtime-red에서 런타임 접근 부재를 재현했다. multi-runtime-green exit0(TestHarness/20261009-052505-893): 공개 런타임 타입, 세 레벨 순서/중간 오류 분리/다음 레벨 완료, 일시정지/중지, 개별 두 전략 기록과 재조회, 원본 불변 통과.
- balance-runtime-ranges exit0(20261009-052758-482): 난이도 범위 계산, 미완료/없는 등급 연결 금지, 원시 판 없는 합성 완료 거절. queue-runtime-storage exit0(20261009-052823-813): 기존 격리된 기록 관리 검사 통과. 새 검사를 LevelToolChecks 전체 실행 목록에 추가했다. 테스트 연결 해제 및 diff check 통과.
- **남은 연결:** 이번 이동만으로 독립 JSON 여러 레벨 시험을 완료한 것은 아니다. 현재 저장 경로는 기존 동기 방식이며, JSON 원본/안정 ID를 함께 보관하고 저장 완료 전 다음 실행을 막는 연결, 이동 수별 원본 복원, 레벨툴 UI와 기록 조회가 필요하다. 기존 기본 편집 메뉴는 아직 전환하지 않았다.
## 여러 레벨·이동 횟수 시험 비동기 저장 경계

- PersistedMultiLevelTest가 목록·추천 요약·개별 판의 파일 저장을 순서대로 수행하고, 저장 확인 전에는 다음 실행 단위를 진행하지 않는다. Unity 직렬화는 호출 스레드에서 마치고 작업 스레드는 고정된 문자열과 경로만 사용한다. 기존 Editor 호출은 기본 동기 저장 방식을 유지한다.
- 일시정지/재개/중지 요청도 같은 저장 경계를 사용한다. 종료 시 마지막 기록을 확정하며, 저장 오류는 해당 레벨의 오류와 나머지 중단으로 표시한다. 추천 시험 내부의 최초 판 목록도 저장 완료 후 게임을 생성한다.
- multi-persistence-green.log / TestHarness/20261009-053642-490-Run.log exit0. multi-persistence-nested.log / TestHarness/20261009-053738-347-Run.log exit0. 정상/오류/정상 레벨의 격리, 개별 판과 최종 목록의 저장, 최초 저장 중 중지, 추천 시험 종료, 실제 파일 경로 오류 및 원본 불변을 확인했다. 각 실행은 총 8판이며 추천 20,000판 전체 검사는 아니다.
- batch-after-multi-persistence.log / TestHarness/20261009-053818-382-Run.log exit0. 기존 반복 시험과 비동기 기록/분석/보관 경계 회귀 검사 통과. 검사 후 테스트 연결을 해제했고 diff check 통과.
- 이 실행 소유자는 아직 레벨툴 화면에 연결하지 않았다. 여러 레벨/추천 기록의 안정 JSON ID·독립 원본, 이동 횟수별 원본 재구성, 화면/이력 연결 및 실제 씬 종료/재로드 검증이 남아 있다. 5단계 전체 완료 또는 기본 편집 화면 전환을 의미하지 않는다.

## 여러 레벨·추천 시험의 JSON 원본과 실행용 UI

- BotTrialSourceContext로 안정 문서 ID, 독립 원본 문자열, 이동 횟수별 원본 생성을 전달한다. AutoPlay는 제작 문서 형식을 해석하지 않는다. 일괄 목록의 sourceDocumentId와 구형 assetGuid는 구분한다. 목록·개별 반복·추천 대표·추천 이동 횟수별 기록에 source.context를 보관한다.
- AuthoringTrialSource.EncodeWithMoves는 해당 레벨의 이동 횟수만 바꾼 사본을 만든다. 추천 로더는 호출자가 JSON으로 복원한 정의를 받아 카탈로그를 보존하고, 이동 횟수별 예상 정의와 실제 판 기록 지문을 대조한다.
- multi-source-red exit1: 독립 원본 전달 API 부재. multi-source-green / TestHarness/20261009-054314-817-Run.log exit0: 저장된 문서 ID, 개별 원본 및 추천 변형 원본, 중지 조회와 기존 저장 경계 통과. balance-source-portable.log: 280개 검사 통과(모든 문서에서 선택 레벨의 이동 횟수 외 원문 동일).
- MultiTrial 화면에 레벨 체크, 현재 횟수 반복/1~100회 추천, 일시정지/재개/중지, 지난 일괄 이력, 레벨별 원시 결과, 검증 완료한 추천 구간을 연결했다. 실행 중 다른 봇/반복/게임 시험을 막는다. 제어 가능한 종료는 기록 저장을 기다린다. 취소 불가능한 종료·재로드의 실제 회귀는 별도 확대 검증이 남아 있다.
- multi-ui-red / 20261009-054415-522 exit1: 버튼 부재 재현. multi-ui-green / TestHarness/20261009-054713-790-Run.log exit0: 실제 씬의 두 JSON 레벨 완료, 일시정지, 안정 ID, 결과 원본 복원, 지난 목록, 추천 중지, 편집 내용/선택/이력 불변 확인.
- bot-after-multi-ui / TestHarness/20261009-054808-558-Run.log exit0: 기존 한 수·한 판·반복·저장 이력 재생과 이미지 보드/배치 회귀 통과. 캡처에서 보드 전 열과 툴바 비중첩 확인.
- 진행 중: BalanceJsonVerification.Run의 실제 추천 첫 구간 200판을 완료하고 초기 그래프 해제 후 JSON으로 재구성해 조회하는 검사. balance-json-measured.log / TestHarness/20261009-055010-904-Run.log. 아직 종료 결과 전이므로 완료로 계산하지 않는다.
- 5단계 전체 완료 조건은 유지한다. 실제 수명주기 복구, 공유 튜토리얼 게임 왕복, 확대된 시각/결과 UX·전체 대응표와 기본 진입 전환, 전체 회귀·최종 리뷰·6단계 문서가 남아 있다.

### 추천 실제 200판 원본 재구성 검증 완료

- balance-json-measured / TestHarness/20261009-055010-904-Run.log **exit0**, 테스트 연결 해제. 앞의 진행 중 검사는 완료됐다.
- JSON 카탈로그가 있는 9×9 레벨에서 이동 1회/두 전략 각 100판을 실제 실행했다. 시작 그래프를 해제한 뒤 저장된 대표 JSON으로 새 그래프를 구성해 BotMoveBalanceReader가 완료 구간과 원시 200판을 대조했다. 이동 횟수별 source.context로도 다시 구성하여 200판·초기 이동 1회를 확인했다. 대표 원본 이동 7회 및 전체 JSON은 불변이다.
- 검증 대상은 첫 완료 구간과 중단된 추천 실행이다. 100구간 전체 20,000판 완료나 배포 앱 검증을 주장하지 않는다. 최종 diff check 통과.

## 실제 시험 중 재로드·Play 종료 보호

- trial-lifecycle-red / TestHarness/20261009-055356-781-Run.log에서 실제 스크립트 재로드 후 시험 횟수와 선택 레벨이 복원되지 않는 문제를 재현했다.
- CaptureView/RestoreRecovery에 봇 시드/전략, 반복 횟수, 여러 레벨 선택/방식/횟수를 포함하고 UI 변경 시 복구 체크포인트를 예약한다. 실행 객체는 자동 재개하지 않는다.
- 제어 가능한 종료는 기존 비동기 저장 완료를 기다린다. OnDisable(강제 씬 해제·스크립트 재로드)과 OnApplicationQuit은 다음 프레임이 없을 수 있으므로 마지막 파일 작업을 확정하는 공통 경로를 사용한다.
- trial-lifecycle-green / TestHarness/20261009-055516-355-Run.log exit0. 실제 실행 도중 스크립트를 재로드하고 원시 batch.json의 Interrupted 상태 및 완료 판 수·판 파일을 대조했다. 새로 복원된 초안/선택/Undo/Redo/시험 설정도 동일했다. 이어 여러 레벨 시험 중 Play를 종료하고 다시 시작해 원시 queue.json 중단 상태와 편집 복원을 확인했다. 테스트 연결 해제.

## 공유 튜토리얼 사본의 실제 게임 왕복

- LevelToolPlayCases에 ‘고정 공급과 두 번 교환’ 샘플을 공유 흐름으로 만들고, 미적용 사본의 안내를 변경한 뒤 게임을 실행하는 검사를 추가했다. 사본에서 Redo가 가능한 상태도 함께 검증한다.
- shared-draft-real-game 첫 실행(20261009-055742-522)은 공유 게임 검사는 통과했으나 검사 직전 Refresh의 지연 파괴 객체를 너무 일찍 세어 실패했다. 프레임 끝 파괴를 기다리도록 검사 시점을 수정했다. 두 번째 실행(20261009-055952-288)은 객체 수 0을 확인했고, 다음 저장 검사가 기본 탭을 가정해 실패했다. 새 검사 종료 시 원래 탭으로 돌아가도록 검사 간 상태를 정리했다. 제품 동작을 우회하거나 실패 판정을 완화하지 않았다.
- shared-draft-real-game-green / TestHarness/20261009-060215-486-Run.log **exit0**, 테스트 연결 해제. 실제 게임에서 미적용 사본 안내, 고정 공급을 포함한 교환 두 번과 내구도 조건 완료, 부모·사본의 ExportState 불변, 복귀 UI와 Undo/Redo, 정식 학습 기록 불변을 확인했다. 임시 LevelDefinition은 예상 0/실제 0.
- 같은 실행에서 기존 대표 4개 레벨 게임 왕복·잘못된 데이터와 이미지 실패·취소 직후 재시험, JSON/SO 불변, 저장/재열기/충돌/사본/백업·미저장 복구, 작은 화면 확대/스크롤도 통과했다.

## 씬 해제·사용자 시각 설정·결과 화면 확대 검사

- trial-scene-unload / TestHarness/20261009-060610-866-Run.log exit0. 실제 재로드/Play 종료 검사에 시험 중 빈 목적 씬 생성→기존 레벨툴 씬 UnloadSceneAsync를 추가했다. 도구 객체 제거 후 원시 batch.json의 Interrupted와 복구 기록의 초안·선택·Undo/Redo 동일성을 확인했다.
- trial-custom-visual / TestHarness/20261009-060910-088-Run.log exit0. 실제 2×2 장애물 하나와 일반 블록 77개를 준비해 Sprite 총 78개·본체 한 번 표시를 확인했다. 시각 크기 1.73/피벗/오프셋/30도 회전을 사본에 지정하고 시험 시작 후 편집 원본 크기를 0.83으로 바꿔도 시험 보드는 시작 사본의 값으로 표시됐다. 81칸의 대체 문자도 모두 제거됐다.
- history-ux-red / TestHarness/20261009-061122-239-Run.log: 다른 기록을 열어도 이전 선택 판 상세가 남는 문제 재현. OpenHistory 성공 시 상세 안내를 초기화했다.
- history-ux-green / TestHarness/20261009-061240-170-Run.log exit0. 성공/실패/오류/중단 필터, 원본 덮어쓰기 거절, 독립 원본 포함 보관, 동일 기록 비교, 다른 규칙 버전의 통계 유지·추천/쌍 비교 보류·재생 거절, 미지원 형식 오류 표시, 원본 파일과 편집 상태 불변 및 정상 원본 재생 복귀를 확인했다.
- 세 검사는 모두 연결 해제됐다. 최종 diff check 통과. 이제 전체 기능 대응표와 통합 회귀를 대조한다. 기본 편집 경로는 아직 전환하지 않았다.

## 통합 회귀 및 기록 정리 보완

- aggregate-before-default-entry 실행의 앞 18개 Unity 검사가 정상 종료됐다. BotAnalysisVerification.Core(20261009-062932-917)는 현재 규칙으로 200판을 생성하고 모든 저장 행동을 재생·대조했다. 이어 MultiLevelVerification.Start(064624-596), 추천 범위 검사(064649-511)도 exit0.
- 19번째 TestRecordManagementVerification(064714-164)은 JSON 시험의 source.context/.tmp를 포함한 격리 자료에서 실패했다. 세 종류·중첩 기록 삭제와 빈 목록 포인터 제거가 실패하고 후속 동일 자료 검사에도 영향을 줬다. 통합 실행은 exit1로 끝났으며 테스트 연결은 해제됐다. 뒤의 재로드/수명주기/기존 SO 교체와 Portable 검사는 이 실행에서 아직 도달하지 않았다.
- TestRecordCleanup을 GUID를 유지해 AutoPlay/Runtime/Storage로 이동했다. 기존 Editor 화면도 같은 실행기를 사용한다. 삭제 허용 목록에 반복·추천·여러 레벨의 레벨별 원본 사본과 원자적 쓰기 임시 파일만 추가했다. 모르는 파일·정션·루트 밖 경로 보호는 유지한다.
- 첫 수정 검사(064829-260)는 Runtime에서 Editor 전용 BotBatchStore.DefaultRoot를 참조해 컴파일 실패했다. 공통 실행기는 저장소 세 경로를 필수 인자로 받고 Editor 화면이 기존 기본 경로를 전달하도록 수정했다. 컴파일 실패로 불완전 생성된 테스트 폴더 메타는 소유 경로 검사 후 복구하고 연결 해제했다.
- record-cleanup-json-green / 20261009-064921-915 재검사 진행 중. 실제 통과는 종료 결과 확인 후 기록한다. 새 레벨툴 화면의 확인·취소·정리 UI는 아직 연결 전이다.
- record-cleanup-json-green(064921-915)은 exit0로 정상 종료했고 테스트 연결을 해제했다. JSON 원본 사본을 포함한 정리와 기존 보호 검사가 통과했다.

## 실행용 기록 관리 화면

- record-ui-red / 065017-388 exit1: 새 화면에 records-count가 없어 실패했다.
- LevelToolScreen.Records에 수량 조회, 명시 확인/취소, 프레임마다 나눠 정리, 실패·보존 표시를 연결했다. 정리 중 편집 화면과 새 봇/반복/여러 레벨/게임 시험·정상 종료 진입을 차단한다. 확인 전에는 조회 상태도 그대로 유지하고 승인 후에만 참조를 해제한다.
- record-ui-green / 065200-742 exit0, 연결 해제. 실제 버튼을 통해 조회/취소의 파일 보존, 확인 대기 중 시험 차단, JSON 원본 사본과 중첩 추천 정리, 별도 보관 파일 및 편집 ExportState 보존, 알 수 없는 파일/요약 보존과 실패 표시를 확인했다.
- 실제 반복 시험 일시정지 상태의 삭제 차단을 추가 검사 중이다. 기본 메뉴 전환 및 전체 완료 판정은 아직 하지 않았다.

## 잔여 통합 검사 완료

- record-ui-active-lock / 065312-445 exit0. 실제 반복 시험 생성·일시정지 상태에서 정리 진입이 차단됨을 확인했다.
- aggregate-tail의 RunBot(065407-671), 일반 재로드(065509-118), 공유 재로드(065604-224), 실제 시험 수명주기(065718-213), 튜토리얼 조건(065825-373), 기존 SO 교체(065859-581)가 모두 exit0로 종료됐다.
- JsonPlayVerification(065934-199)에서 현재 SO/JSON의 같은 시드·같은 행동으로 초기 보드, 공급/RNG, 미션, 연쇄·파워·튜토리얼을 비교했다. 재시작, 취소/실패 객체 정리, SO 및 정식 학습 기록 불변도 통과했다.
- Portable 280개 검사 통과. 통합 앞부분의 기록 정리 실패는 별도 수정·회귀로 확인했으며, 그 뒤 도달하지 않았던 검사를 위 순서로 완료했다. 기본 편집 경로는 아직 변경 전이며 새 메뉴 검증부터 진행한다.

## 기본 메뉴와 자료 인계

- default-entry-red / 070131-997에서 실제 기본 메뉴가 옛 창을 여는 상태를 재현했다. 기본 ‘레벨 에디터’·‘통합 작업창’을 LevelToolLauncher로 연결했다.
- default-entry-green / 070214-790 exit0. 실제 메뉴 실행 후 공통 씬 진입, 미저장 씬 객체/dirty 상태, 이전 시작 씬 설정 복원을 확인했다.
- handoff-red / 070331-636에서 인계 API 누락 확인 후 OfferWorkspace/LaunchWorkspace를 추가했다. 새 화면은 전달된 작업을 별도 버튼으로 제시하며 기존 복구/초안을 자동 덮어쓰지 않는다.
- handoff-green / 070458-792 exit0. 인계 제시·취소 시 현재 초안 유지, 명시적 버리기 후 선택 칸·미저장·Undo/Redo 동일 복원, Redo 실행과 디스크 원본 불변을 확인했다. 테스트 연결 해제.
- Inspector·새 레벨·기존 창에 남은 자료의 인계 생산자 및 중복 편집 화면 비활성화는 아직 남아 있다. 위 메뉴 두 개 전환만으로 G8 완료로 판정하지 않는다.

## 기존 창의 초안 인계 검증

- 기존 JSON 창은 중복 편집 화면 대신 안내·인계 창으로 전환했다. 부모 작업의 선택·미저장·Undo/Redo는 그대로 전달한다.
- SO 임시 튜토리얼은 독립 JSON 초안으로 가져온다. 유효하지 않은 입력도 수정할 수 있게 보존하며 자동 공개 저장하지 않는다. `20261009-071247-104-Run.log` exit 0.
- JSON 공유 사본은 부모와 분리된 미적용 사본으로 전달한다. 원본 연결을 확인하고 부모를 수정하지 않는다. 이름 복제 접미사 문제를 수정한 후 `20261009-072111-517-Run.log` exit 0.
- 새 화면 인계는 불완전한 입력을 포함한 부모 상태와 공유 사본을 복원하고, 실제 원본 적용 버튼을 누를 때만 반영한다. `20261009-072220-106-Run.log` exit 0.
- `JsonAuthoringChecks`의 구형 창 UI 검사는 현재 공통 씬의 고급 편집·샘플·공유·인계·복구·실행 검사로 교체했다. 3단계 창 전용 검사 소스는 과거 구현 기록으로 남아 있으나 현재 통합 검사에서 호출하지 않는다. 공통 저장소·편집·JSON/SO 게임 결과 비교 검사는 유지한다.


## 최종 완료 감사 — 2026-10-09

- 실제 씬 인계: `launch-incoming-green` / 072321-851 exit0. 전달 버튼 생성과 인계 상태, 기존 미저장 씬/시작 씬 설정 복원 확인. 이 검사는 private 수락 호출로 전달을 검사하며, 사용자 선택/취소 경로는 별도 HandoffExercise에서 검사한다.
- 2×2 연결: `connection-move-final` / 072520-313 exit0. 이동 후 대상 ID·작성된 전선 보존, 삭제 시 끊어진 연결 제거, 두 번 Undo의 정확한 복원 확인.
- 독립 최종 리뷰에서 공유 사본 인계 시 이전 배치 도구가 남는 P2 하나를 확인했다. `handoff-brush-red` / 072649-813에서 재현, 공유 사본 수락 시 brush 해제 후 `handoff-brush-green` / 072803-339 exit0. 부모 상태 보존과 명시 적용도 재확인했다. 같은 리뷰어의 수정 확인 결과 미해결 지적 없음.
- 실행용 LevelTool/LevelAuthoring/AutoPlay의 Editor/Tests 참조 검색 결과 없음. 새/이동된 스크립트 메타 존재, 두 통합 PowerShell 검사 스크립트 구문, diff 공백 검사 통과. 검사 연결 해제 확인.
- 제작 SO/Addressables/패키지 설정을 이번 전환으로 변경하지 않았다. 작업 트리에 있던 Windows Level Editor 프로필의 LevelTool 씬 지정은 유지했다. 실제 Player/Addressables 빌드와 배포 앱 검사는 수행하지 않았다.

| 완료 조건 | 최종 근거 |
|---|---|
| G1 기능 대조 | 갱신한 기능 대응표: 기존 소유자·공통 API·새 화면·실제 검사 범위 |
| G2 튜토리얼 제작 | RunTutorial/SharedTutorial/Samples, 12종 바인딩, SharedDraftGame, 공유 사본 인계 회귀 |
| G3 고급 제작 | Advanced 공급/흐름/연결/모양, UIExercise 기본/복제, Visual 및 연결 이동 최종 검사 |
| G4 ID·공유·Undo | 공통 편집/튜토리얼 검사, 사본 게임 왕복, 연결 이동/삭제/Undo, 정확한 ExportState 비교 |
| G5 검사/봇 | Bot/Batch/Multi/Balance/History 실제 실행·중지·재생, 200판 원시 결과 검증, Runtime 의존 검색 |
| G6 게임·격리 | 실제 Play, JsonPlay065934-199 동일 입력·시드·행동 비교, 취소/실패 정리·정식 기록 불변 |
| G7 자료 보호 | 일반/공유 재로드, 실제 씬 해제/Play 종료, 저장/버리기/취소, 인계·현재 작업 충돌 보호 |
| G8 기본 전환 | 기본 메뉴070214-790, Gateway072111-517, 실제 인계072321-851, 기존 SO 원본 해시 불변 |
| G9 안내·의존 | 실제 오류 단계 선택·재검사, 샘플/설정 툴팁, 실행/논리/봇 설명, Runtime Editor/Tests 참조 없음 |
| G10 인계 | 아래 6단계 문서·복사용 명령문·완료 커밋 메시지. 다음 단계 미착수 |

[6단계 계획](../../Planning/project-wide/game-authoring-stage-06-plan.md) · [6단계 목표](../../Goals/project-wide/game-authoring-stage-06-goal.md) · [6단계 실행문](../../Commands/project-wide/game-authoring-stage-06-command.md)

다음 단계는 전체 제작 원본의 검증된 JSON 채택, 같은 스냅샷의 MemoryPack 변환, 네 제품 프로필/리소스 경계 확인이다. 이번 단계의 필수 기능을 뒤로 미루지 않았다. 실제 Windows 배포 앱·모바일 실기·제품 빌드는 별도 승인 전 미검증으로 남는다.

복사용 커밋 메시지(커밋·푸시 실행하지 않음):

```text
feat: 공통 레벨툴 제작·시험 기능 완성 및 기본 편집 경로 전환

- 튜토리얼·공유 구성·공급·흐름·연결·모양 편집 통합
- 봇·여러 레벨 시험·기록 조회와 정리 기능 공통화
- 미저장 복구와 구형 자료·공유 사본 인계 보호
- 기능 회귀 검증 및 6단계 계획·목표·실행문 정리
```

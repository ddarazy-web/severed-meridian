# 레벨툴 리팩토링 3단계 작업·검증 기록

상태: 3단계 구현·관련 자동 회귀·최종 컴파일 완료. 아래 중간 진행 기록은 당시 상태이며 최종 판정은 문서 끝을 따른다.

## 범위와 기준

- [계획](../../Planning/MoonRabbitJunkyard/LevelToolRefactoring/2026-10-19-stage03-plan.md), [목표](../../Goals/MoonRabbitJunkyard/LevelToolRefactoring/2026-10-19-stage03-goal.md).
- work 브랜치에서 시작했고 시작 시 작업 트리는 깨끗했다. 커밋·푸시·제품 빌드·HTML·프리팹 에셋 변경은 하지 않는다.
- 진행 순서: 선행 튜토리얼 종료 조사 → A 보드 표시 → B 속성·공급 → C 튜토리얼·통합 → 4단계 문서. 다음 단계 구현은 시작하지 않는다.

## 선행 튜토리얼 종료 조사

1. `20261019-174906-261-Run.log`: 모든 개별 검사 PASS. 종료 요청 → ExitingPlayMode → 종료 요청 반환까지 기록됐지만 EnteredEditMode는 관찰되지 않았다. 확인된 검사 프로세스만 종료하고 하네스 연결 해제 확인.
2. `20261019-175241-300-Run.log`: 화면 OnDisable/OnDestroy 구간의 임시 진단 추가. 같은 지연이 재현됐고 OnDisable 첫 로그에도 도달하지 않았다. 따라서 복구 파일 flush 또는 화면 리소스 해제가 원인이라고 단정하지 않는다. 해당 진단 검사만 종료·연결 해제 확인.
3. `20261019-175707-292-Run.log`: delayCall로 종료 요청을 미뤄도 ExitingPlayMode 이후 지연이 재현됐다. 종료 호출 시점만의 문제라는 가설은 기각했다.
4. `20261019-180239-665-Run.log`: UI 검사 종료 시 이미지 요청 35개가 남아 있었고 모두 완료된 뒤에도 전환 지연이 재현됐다. 미완료 이미지 요청만으로는 설명되지 않는다.
5. `20261019-180816-710-Run.log`: 격리 테스트에서 종료 콜백을 원래 순서로 계측했다. Addressables/UniTask/Search 등 모든 콜백의 반환 로그가 있고 OnDisable에는 아직 도달하지 않았다. 임시 콜백 계측은 제거했다. 외부 패키지는 읽기만 했다.
6. `20261019-181336-754-Run.log`: IEnumerator로 조작 묶음 사이에 실제 게임 프레임을 진행시켰다. 전체 240초 제한으로 중단됐으나 OnDisable 진입과 FlushRecovery 직전까지 관찰됐다. 검사 복구 기록은 약 60~83MB, 검사 Unity 프로세스 메모리는 약 32GB였다. 저장 검증이 전체 Undo/Redo 문서를 다시 복원하는 코드 경로를 확인했다. 해당 격리 프로세스를 정리했다.
7. `20261019-182329-158-Run.log`: **모든 기존 튜토리얼 assertion, 화면 최종 복구 저장, OnDisable/OnDestroy, EnteredEditMode, exit 0 정상 종료 확인.** 진행이 있을 때 제한시간을 갱신하고 UI 검사 중간 자동 체크포인트는 미룬다. Undo 원본 복원 assertion 후에만 격리 테스트 사본의 이력을 Discard한다. 사용자 데이터와 제품 복구 로직은 바꾸지 않았다. 임시 진단 로그와 이벤트 래퍼는 제거했다.

주의: 이것은 UI 검사와 대량 복구 부하를 분리한 테스트 종료 보완이다. 실제 앱에서 긴 편집 이력의 복구 저장 비용을 해결했다고 주장하지 않는다. 제품의 대량 이력 성능은 확인된 잔여 위험이며 최종 보고와 4단계 검증 계획에 명시한다. 일반 복구 의미·저장 호환성 검사는 별도로 유지한다.

이벤트 계측 구조 확인에는 Unity 공식 참조 소스 [EditorApplication](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/EditorApplication.cs), [EventWithPerformanceTracker](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/EventWithPerformanceTracker.cs)를 참고했으며 실제 설치본의 reflection으로 확인했다.

## A 단위 사전 검사

| 검사 | 결과 | 로그 (`Logs/TestHarness/`) |
|---|---|---|
| 분리 전 LevelToolViewportVerification.Run | 정상 통과, 16 캡처 생성 | `20261019-182717-770-Run.log` |
| LevelToolBoardViewVerification.Run (RED) | View 미구현으로 의도한 실패 | `20261019-182906-175-Run.log` |
| LevelToolBoardViewVerification.Run (GREEN) | 11항목 정상 통과 | `20261019-183140-254-Run.log` |
| 분리 후 LevelToolInputVerification.Run | 정상 통과 | `20261019-183319-982-Run.log` |
| 분리 후 LevelToolViewportVerification.Run | 정상 통과 | `20261019-183402-830-Run.log` |
| 분리 후 LevelToolAdvancedVerification.RunVisual | 기존 시험 보드 assertion 및 exit 0 정상 통과 | `20261019-183539-635-Run.log` |
| 편집 보드 assertion 보강 후 RunVisual | 편집 보드 최신 크기·피벗·회전·2×2 단일 표시와 시험 보드 고정 설정, exit 0 정상 통과 | `20261019-183714-247-Run.log` |

View의 Root·전체 81셀·선택 표시·크기·렌더/해제 시 편집 상태 불변·Dispose 후 재사용 거절을 검증했다. 보드 표시·Artwork·FlowOverlay·ConnectionOverlay를 Presentation의 별도 객체로 이동했고 기존 파일 meta GUID는 보존했다. 화면은 표시 입력·연결 꼭짓점 편집·입력 조립을 담당한다. View 해제 시 이미지 로드 토큰과 소유 리소스를 해제하며 지연된 화면 맞춤은 비활성화된 화면을 다시 그리지 않는다.

단순 대기 시간 초과만으로 제품 오류 또는 정상 통과를 주장하지 않는다. A 보드 View 구현과 입력·뷰포트·이미지 회귀는 통과했다. 비활성화/레벨 전환 도중 이미지 결과의 추가 경계 검사와 전체 회귀는 남아 있다. 이 기록 시점에는 B·C가 미착수였으며, 이후 B 진행 결과는 아래 별도 기록을 따른다. 4단계 문서는 전체 3단계 완료 후 작성한다.

## 다음 진행 위치

- B: `LevelToolScreen.Board.cs`의 기본 속성/미션 및 `LevelToolScreen.Supply.cs`를 패널로 이동한다. 공급 추가용 임시 입력은 현재 해당 파일에만 소유되므로 재활성화 시 값 유지 계약을 먼저 명시한다.
- 지연 입력/포커스는 `CaptureFocusedInput`·`RestoreFocusedInput`·`CommitCommandInput`의 기존 순서를 유지한다. 패널 전체/다른 패널을 받는 거대 context는 만들지 않는다.
- C: Tutorial 8개 파일의 표시/공유 사본/피커 상태 소유권을 분리한다. 복구에 저장되는 tutorialStep/newTutorialCondition/sharedTutorialDraft 의미를 유지한다.
- 최종: 깨끗해진 검사 코드로 RunTutorial 재실행, A/B/C·모바일·단축키·재활성화·비동기 이미지 회귀, 자체 검토, 4단계 계획/목표/실행문과 최종 커밋 메시지. 현재 목표를 완료 처리하지 않는다.

## B 공급 패널 분리

- `LevelToolSupplyPanel`은 일반/튜토리얼 공급의 표시·필드 콜백을 소유한다. `LevelToolSupplyState`는 추가할 항목의 종류·수량·색·방향·내구도만 보관한다. 화면 재활성화 시 이 상태를 재사용하며 세션이나 문서를 복제하지 않는다.
- API: 생성자에 현재 세션 조회, 기존 Change 명령, 임시 입력 상태만 전달한다. `Refresh(level, definitions, tutorial)`, `Root`, `Clear`, `Dispose`를 제공한다. 화면은 매 갱신에서 이전 콜백을 무효화하고 표시할 때만 Root를 연결한다.
- 세대·세션·레벨 ID를 검사해 이전 컨트롤이 뒤늦게 변경 이벤트를 보내도 편집하지 않는다. 기존 이름·문구·필드 형식과 공급 규칙은 보존한다.
- 기존 공급/흐름/연결/모양/튜토리얼에서 쓰는 Choice·Number만 `LevelToolPanelFields`로 모았다. 남은 화면 호출은 얇은 전달 함수로 유지한다.

| 검사 | 결과 | 로그 (`Logs/TestHarness/`) |
|---|---|---|
| SupplyPanelVerification RED | 타입 미구현으로 의도한 실패 | `20261019-184502-973-Run.log` |
| SupplyPanelVerification 최초 GREEN 시도 | UI 요소가 실제 패널에 연결되지 않아 이벤트 미전달. 검사 Host 보완 | `20261019-184646-411-Run.log` |
| SupplyPanelVerification | 9항목 통과. 이전 컨트롤을 실제 패널에 재연결해 콜백 차단 확인 | `20261019-184745-279-Run.log` |
| AdvancedVerification.Run | 공급·흐름·연결·모양 UI 및 Undo 통과, Edit Mode 복귀·exit 0 | `20261019-184832-198-Run.log` |

기본 속성 패널 분리를 이어 진행한다. 공급의 터치·모바일 지연 입력 통합 검사는 최종 회귀에 포함한다.

## B 기본 속성 패널 분리

- 기본 속성·선택 요소·미션 렌더링을 `LevelToolPropertiesPanel`로 옮겼다. 화면은 표시 경로를 선택하고 보드와 공유하는 배치 색/방향·줌·시험 설정을 계속 소유한다. 패널은 최신 옵션 조회와 좁은 변경 콜백만 받는다.
- 기존 settings/selection 이름, 입력 필드 명칭, 문구, 지연 입력 및 문서 편집 명령을 보존했다. 이전 세대·세션·레벨의 콜백은 차단한다.

| 검사 | 결과 | 로그 (`Logs/TestHarness/`) |
|---|---|---|
| PropertiesPanelVerification RED | 타입 미구현으로 의도한 실패 | `20261019-185136-257-Run.log` |
| PropertiesPanelVerification GREEN | 6항목 통과: UI 경계 이름, 편집·Undo, 이전/해제 콜백 차단 | `20261019-185317-688-Run.log` |
| LevelToolInputVerification.Run | 정상 종료. 지연 입력·저장·숫자 오류 보존·포커스·키보드 편집 회귀 통과 | `20261019-185406-270-Run.log` |

C는 아직 미착수다. `sharedTutorialDraft`는 저장·이탈·Undo 명령에서도 사용하므로 화면의 편집 조정 책임으로 유지하고, 튜토리얼 패널에는 필요한 사본 조회와 명령만 연결할 예정이다. 피커의 보드 입력/확정과 미리보기는 화면 연결 책임을 유지하되 패널의 조건·대상 UI와 직접 내부 참조하지 않도록 분리해야 한다.

- `LevelToolAdvancedTouchVerification.Run`: 네 화면 크기(360×780, 780×360, 975×800, 1280×800)에서 흐름·연결·튜토리얼 피커·모양·일반/튜토리얼 공급 편집과 Undo/Redo 통과, exit 0 및 하네스 해제 확인. 로그: `20261019-185447-269-Run.log`. 근거 폴더: `Logs/AndroidLevelToolStage05/advanced-touch-b65da7f52b304c0d9e0ed2f2cff60bfa`. 실제 Android 기기 검증은 포함하지 않는다.

## B 보강 검사와 현재 마무리 상태

| 검사 | 결과 | 로그 (`Logs/TestHarness/`) |
|---|---|---|
| PropertiesPanelVerification 보강 | 14항목 통과. 내구도·미션 수량 Undo/Redo, 갱신 없는 최신 배치 색/방향 적용 포함 | `20261019-190233-907-Run.log` |
| SupplyPanelVerification 보강 | 10항목 통과. 패널 재생성 후 임시 입력 유지 포함 | `20261019-190318-816-Run.log` |
| LevelToolViewportVerification.Run | 네 해상도 배치·보드 맞춤·검사 영역·모달·문서 상태 보존 통과, exit 0 | `20261019-190401-318-Run.log` |

공급 검사 후 다음 검사를 연결하는 임시 PowerShell 루프에서 `$LASTEXITCODE`를 읽어 실행 래퍼가 실패로 표시했다. Unity 검사 자체는 exit 0이고 하네스 해제까지 완료됐다. Viewport는 별도 실행하여 정상 종료를 확인했다. 제품 코드나 테스트 assertion 문제로 분류하지 않는다.

1280×800 `Logs/LevelToolUiStage01/viewport-1280x800-layout-review.png`를 직접 확인했다. 보드·상단·양옆 영역의 위치는 유지됐지만 배치 실행에서 스킨 일부가 흰색으로 렌더되는 기존 제한이 남아 있어 세부 필드의 실제 시각 품질까지 검증 완료로 판단하지 않는다. 화면 배치 측정과 이미지 육안 확인의 범위를 구분한다.

현재 모든 실행한 Unity 검사 프로세스가 종료됐고 하네스는 해제됐다. `git diff --check` 통과. prefab/scene/HTML/ProjectSettings/Packages 변경 없음. 제품 빌드·커밋·푸시 없음. 하위 에이전트 없이 자체 검토했다.

남은 작업: B 모바일 패널 이동 중 지연 입력의 좁은 확인, C 튜토리얼 화면/조건/대상 패널 분리, A 비동기 전환 경계 추가 검사, 최종 RunTutorial 및 관련 회귀, 4단계 계획·목표·실행문. 목표는 아직 진행 중이며 최종 커밋 메시지는 전체 완료 후 제시한다.

## C 튜토리얼 패널 분리 진행

- 조건·대상 UI를 독립 ConditionsPanel과 공통 TargetFields로 분리했다. 이후 단계·공유 구성·바인딩·샘플·파라미터 UI를 TutorialPanel로 이동했다.
- 기존 화면은 공유 사본·피커·미리보기 연결과 수명주기를 담당한다. 기존 복구 키는 유지하고 테스트에서 tutorialStep을 읽는 reflection 한 곳은 필드 대신 상태 전달 프로퍼티를 읽도록 변경했다.
- Bindings/Parameters/Samples 및 기존 Conditions 파일의 meta GUID는 이동 경로에 유지했다. 새 조립 패널과 추가 책임 파일만 새 meta를 생성했다.

| 검사 | 결과 | 로그 (`Logs/TestHarness/`) |
|---|---|---|
| ConditionsPanel RED | 독립 패널 타입 미구현으로 실패 | `20261019-190802-807-Run.log` |
| 첫 컴파일 | 공통 필드로 옮긴 BoardItem의 Simulation using 누락. 수정 | `20261019-190943-044-Run.log` |
| ConditionsPanel GREEN | 조건 생성·수정·Undo·이전/해제 콜백 등 7항목 통과 | `20261019-191108-876-Run.log` |
| TutorialPanel RED | 조립 패널 미구현으로 실패 | `20261019-191229-559-Run.log` |
| 조립 분리 후 단위 검사 | 8항목 통과 | `20261019-191428-269-Run.log` |
| 복구 단계 범위 보정 경계 RED | 렌더 중 단계 보정으로 새 추가 버튼까지 무효화되는 문제 재현 | `20261019-191537-447-Run.log` |
| 보정 경계 GREEN | 세대/세션/레벨/사본 기반 무효화로 수정 후 정상 종료 | `20261019-191625-290-Run.log` |

컴파일 실패 실행에서 Unity가 검사 임시 폴더의 meta를 GUID까지만 작성해 하네스가 폴더로 판별하지 못했다. connection.json 소유 기록과 실제 폴더 경계를 확인한 뒤, 기록된 임시 폴더 meta에만 folderAsset 표시를 보완하여 기존 Detach를 완료했다. 원본 Tests/프로젝트 에셋의 meta와 하네스 구현은 변경하지 않았다.

현재 일반·공유·샘플 튜토리얼의 통합 회귀를 실행 중이다. 단위 검사 통과만으로 C 또는 전체 3단계 완료를 주장하지 않는다.

- 분리 후 `LevelToolAdvancedVerification.RunTutorial` 통과: `20261019-191714-754-Run.log`. 조건 종류별 필드, 정확한 남은 내구도, 강조 미리보기, 피커 취소/확정·한 번의 Undo·원본 복원 및 Edit Mode 복귀·exit 0 확인. 이전 종료 문제에 대한 최종 분리 코드의 재검증이다.
- A 잔여 경계를 위해 `LevelToolBoardLifetimeVerification.Run`을 추가했다. 실제 미완료 이미지 요청을 확인한 직후 레벨 전환/Clear/Dispose를 수행하고 완료 후 이전 이미지 반영 방지·Atlas 반환·문서 보존·재방문 표시를 확인하도록 구성했다. 아직 실행 전이다.

- `RunSharedTutorial` 최초 분리 후 실행 `20261019-192009-069-Run.log`: 공유 생성/사본 적용·취소, 12종 레벨별 바인딩, 피커와 공유 원본 보존 assertion은 모두 통과했으나 종료가 지연됐다. 해당 검사 PID의 메모리가 약 15GB→19GB로 증가해 이 격리 프로세스만 중단했고 하네스 해제를 확인했다. 종료 통과로 계산하지 않는다.
- 일반 튜토리얼과 동일하게 공유/샘플 UI 검사도 중간 자동 체크포인트를 미루고, 모든 기존 assertion이 끝난 뒤에만 격리 Session을 Discard하여 대량 Undo 복구 저장 부하를 분리했다. 마지막 이미지 요청 완료 후 종료하며 화면의 최종 복구 저장은 유지한다. 제품 코드·실제 사용자 이력은 변경하지 않는다.
- 보완 후 `RunSharedTutorial`은 `20261019-192436-738-Run.log`에서 전체 assertion, Edit Mode 복귀·exit 0·하네스 해제를 확인했다. 대량 편집 이력 성능은 여전히 제품 측 잔여 위험이다.
- 기존 `LevelToolControlLayoutVerification.Main`은 열린 Play Mode를 전제로 하므로, 동일 검사 로직을 격리된 시험 프로젝트로 시작하고 정상 종료시키는 Run 진입점을 추가했다. 아직 실행 전이며 기존 interactive Main 경로도 유지한다.

| 최종 경계 검사 | 결과 | 로그 (`Logs/TestHarness/`) |
|---|---|---|
| RunTutorialSamples | 샘플/조건 사본/시험 보드/문제 위치/재생·원본 보존·Undo 및 exit 0 | `20261019-192653-922-Run.log` |
| LevelToolBoardLifetimeVerification.Run | 8항목 통과. 실제 pending 로드 확인 후 전환·Clear·Dispose·Atlas 해제·재방문·상태 보존 | `20261019-192934-669-Run.log` |
| LevelToolMobileVerification.RunPendingCommands | 숨긴 지연 입력 저장, 잘못된 숫자 입력 거절, Undo/Redo 및 후속 정상 입력/저장, exit 0 | `20261019-193025-254-Run.log` |

샘플 검사 종료 시 ControlLayout 테스트 원본 수정과 다음 하네스 연결이 겹쳐 소유 해시 검사로 해제가 중단됐다. 임시 복사본과 현재 Tests 원본의 SHA256이 동일함을 확인하고 해당 파일의 임시 연결 기록만 맞춘 뒤 Detach했다. 이후 연결 중에는 Tests 원본도 수정하지 않는다. 실행된 샘플 assertion과 Unity exit 0은 확인됐으며 소유 확인 없이 파일을 삭제하지 않았다.

## 최종 판정

3단계 완료. 보드·속성·공급·튜토리얼·조건 패널의 책임과 수명주기를 분리했고 기존 세션의 문서/선택/Undo 소유권을 유지했다. 다음 4단계 계획·목표·실행문을 작성했으며 구현은 시작하지 않았다.

| 최종 검사 | 결과 | 로그 (`Logs/TestHarness/`) |
|---|---|---|
| MobileTutorialViewport | 세로/가로 공유 사본·피커·게임 복귀·Undo/Redo 통과, exit 0 | `20261019-193117-688-Run.log` |
| ControlLayout | 1280×800, 1920×1200 드롭다운·포커스 후 영역 유지, exit 0 | `20261019-193203-789-Run.log` |
| Input | 지연 입력·잘못된 값·저장 포커스·재활성화·공유 이력 회귀 통과, exit 0 | `20261019-193252-782-Run.log` |
| Shortcut | 모달·Esc·키 유지·등록/해제 경계 통과, exit 0 | `20261019-193329-678-Run.log` |
| Compile | 최종 using/서식 정리 후 컴파일 정상 종료 | `20261019-193449-544-Compile.log` |

`Logs/LevelToolControls-0-properties.png`, `Logs/LevelToolControls-1-properties.png`를 직접 확인했다. 마지막 ControlLayout 캡처는 정상 어두운 스킨에서 필드와 드롭다운이 표시되며 보드 영역 압축이 없었다. 앞선 배치 viewport 캡처의 흰색 스킨 제한과 구분한다.

최종 점검: work 브랜치 유지, 검사 연결 기록 해제, 이동한 7개 파일의 meta GUID 보존, Presentation에서 LevelToolScreen/UnityEditor 직접 참조 없음, git diff --check 통과(줄바꿈 안내만 존재). 씬·프리팹·HTML·ProjectSettings·Packages·ContentData의 추적 파일 변경 없음. 제품 빌드·커밋·푸시는 하지 않았다.

잔여: Android 실기기와 독립 실행 파일은 미검증이다. 대량 Undo 이력의 복구 저장·검증 부하는 제품 측 위험으로 남아 있으며 UI 검사에서 격리 이력을 정리한 것은 성능 해결이 아니다. 4단계에서 별도 측정한다.

커밋 메시지 제안:

```text
refactor: 레벨툴 보드와 편집 패널 책임 분리

- 보드·속성·공급·튜토리얼 패널 수명주기와 편집 연결 분리
- 비동기 전환·입력·튜토리얼·레이아웃 회귀 검증 보강
- 3단계 검증 기록 및 4단계 계획·목표 정리
```

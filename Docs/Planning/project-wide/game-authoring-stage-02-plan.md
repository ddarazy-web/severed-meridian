# 게임·제작 도구 분리 2단계 — JSON 계약과 안전한 변환

> 2026-10-08 · 구현·검증 완료
> 실행 담당은 superpowers:executing-plans로 아래 세 묶음을 진행한다. 필요하면 파일 소유권을 나누어 하위 에이전트를 사용한다. 완료 근거는 [검증 기록](../../Verification/project-wide/game-authoring-stage-02-progress.md)을 따른다.

**Goal:** 기존 제작 SO를 수정하지 않고 대상 콘텐츠를 JSON으로 저장·읽기하여 같은 퍼즐 시험 입력으로 실행한다.
**Architecture:** Unity 없는 제작 문서·파일 저장·참조 검증과 Unity 원본을 읽는 Editor 내보내기를 분리한다. JSON은 실행 어댑터와 PuzzlePlayRequest/PuzzlePlayContext를 통해 시험한다. 공유 제작 의도를 잃는 실행 팩 역변환은 금지한다.
**Tech Stack:** Unity 6000.3.10f1, C#, 기존 MemoryPack/UniTask, 실제 호환성을 확인한 JSON 도구.
**Spec:** [6단계 통합 계획](2026-10-08-game-and-authoring-products-plan.md), [JSON 저장 계약](2026-10-08-json-authoring-transition-plan.md).
**완료 조건:** [2단계 목표](../../Goals/project-wide/game-authoring-stage-02-goal.md)
**실행문:** [복사용 명령문](../../Commands/project-wide/game-authoring-stage-02-command.md)

## 운영 경계

- ServeredMeridian만 작업하며 기존 변경·에셋 GUID를 보존한다.
- 커밋·푸시 및 Player/Addressables 빌드는 금지한다. HTML 매뉴얼은 요청 전까지 갱신하지 않는다.
- 질문은 충분히 설명하는 제한시간 없는 일반 채팅 문답으로 한다.
- 기존 SO가 제작 기준이다. JSON은 Assets 밖 ContentData/Trials/game-authoring-stage-02/의 명시적 시험 작업 폴더에 생성한다. 기존 시험 자료가 있으면 새 하위 폴더로 분리한다.
- 3단계는 명시 선택한 JSON 작업 폴더 편집, 6단계는 전체 제작 원본 JSON 정식화다. 이번에 기본 저장 방식을 변경하지 않는다.
- 4단계에서 공통 레벨툴 씬을 시범 적용하고, 5단계 동등성 검증 후 Unity 기본 편집도 같은 씬으로 전환한다. 기존 EditorWindow의 중복 편집 UI를 영구 유지하지 않는다.
- 기존 50레벨 팩·요소 팩·Addressables 등록·빌드 프로필·기본 게임 입력을 유지한다. 배포 Generate()를 검증용으로 호출하지 않는다.
- 기존 편집 UI/Undo 교체, 공통 도구 씬 구현, 새 콘텐츠·기획 생성은 이번 범위가 아니다.

## 확인한 근거와 파일 책임

- Tutorial/Data/LevelTutorialDefinition.cs의 flow/bindings/completionId/previousLevelNumbers는 MemoryPackIgnore다. 실행 팩을 풀어서는 제작 관계를 보존할 수 없다.
- Elements/Data/ElementDefinitionAsset.cs는 정의 외에 PlanningDocument/PlanningSection을 가진다.
- ElementVisualCatalogAsset.ToDto()는 기본 표현을 보충한다. 명시 제작 설정과 기본값을 구분하도록 원본 제작 필드를 대응해야 한다.
- GameScreen/Runtime/Session/PuzzlePlayRequest는 실행 사본을 소유하고 CreateDefinition 반환 객체의 소유권을 호출자/세션에 넘긴다. PuzzlePlayContext.CreateTest는 정식 학습 기록을 쓰는 문맥을 거절한다.
- Levels/Editor/Persistence/LevelPackBuild.Generate는 팩 기록과 Addressables 변경을 수행하므로 왕복 검사용으로 호출하지 않는다.
- Tools/Testing/ProjectTests.ps1의 Run/Compile/Status가 기존 검증 경로이며 원본 테스트는 Tests/Editor에 둔다.
- Packages/manifest.json과 packages-lock.json 검색에서 Newtonsoft 직접 의존을 확인하지 못했다. 프로젝트 프로필 문구만으로 사용 가능하다고 가정하지 말고 실제 DLL·독립 실행 호환성을 A에서 확인한다.

아래는 신규 후보 경로다. 같은 책임의 기존 코드는 재사용하고 변경 이유를 기록한다.

| 위치 | 책임 |
|---|---|
| Assets/Scripts/Features/LevelAuthoring/Documents/ContentDocument.cs 및 종류별 파일 | 순수 제작 모델·문자열 키·버전·ID |
| Assets/Scripts/Features/LevelAuthoring/Storage/JsonContentStore.cs | UTF-8 읽기·해시 충돌·원자 저장 |
| Assets/Scripts/Features/LevelAuthoring/Validation/ContentDocumentValidator.cs | 구조·종류·참조·ID·경로 검사 |
| Assets/Scripts/Features/LevelAuthoring/Editor/Import/LegacyContentExporter.cs | 제작 SO 직접 읽기·안정 ID 매핑·내보내기 |
| Assets/Scripts/Features/LevelAuthoring/Runtime/JsonPuzzlePlayAdapter.cs | 검증된 JSON 스냅샷→기존 실행 입력 |
| Tests/Editor/Features/LevelAuthoring/JsonAuthoringVerification.cs | 원본 보존·왕복·실행 동등성 |
| Tests/Portable/LevelAuthoring/ 및 Tools/Testing/JsonAuthoringChecks.ps1 | Unity 참조 없는 파싱·검증·저장 검사 |
| Docs/Verification/project-wide/game-authoring-stage-02-progress.md | 대응표·검증 근거·제약·인계 |

## A. 제작 계약과 읽기·참조 검사

**결과:** 대상 문서 모두를 독립적으로 읽고 검사하는 모델과 검증기.

- [x] 실제 타입·직렬화 필드·에셋 목록·참조 그래프를 조사한다. 숨겨진 제작 필드를 포함해 JSON 대응표를 작성한다.
- [x] JSON 도구와 Unity 없는 검사 환경을 확인한다. 기존 도구를 우선하며 새 의존성은 필요성과 호환성을 명시한다. JsonUtility/UnityEngine.Object를 외부 문서 계약으로 삼지 않는다.
- [x] version 1 envelope(kind/schemaVersion/id), 명시 문자열 enum 키, 누락/null/빈 목록 정책을 검사로 고정한 뒤 모델·파서·검증기를 구현한다.
- [x] UnityEngine/UnityEditor 어셈블리 참조 없이 동일 문서·파서·검증 소스를 실제 실행한다. 소스 검색만으로 독립 실행 검사를 대체하지 않는다.

| 문서 종류 | 필수 보존 정보 |
|---|---|
| project | 프로젝트 ID·버전·기본 카탈로그·리소스 논리 ID 매핑 |
| level | 9×9 보드·모든 레이어 배치·연결/흐름/공급·미션·튜토리얼 |
| element | 모든 기존 프로필·행동 설정·기존 ID·기획 문서/절 |
| catalog | 정의 ID 목록·표현 카탈로그 참조·포함 관계 |
| visual | 명시 상태/프레임/시트/피벗/크기/효과/생성 연결·기획 출처 |
| tutorialFlow | 공유 단계·안내·조건/행동·authoringId·파라미터 key |
| tutorialSample | 샘플 이름/설명·재사용 동작/조건·참조 |
| shape | 활성 칸·원본 정보·장애물 사용 기록 |

필수 계약:

- 문서 ID는 파일명·표시명·레벨 번호와 별개다. element ID, instanceId, authoringId, 파라미터 key, 생성 결과 이름, completionId의 의미를 유지한다.
- ID 없는 구형 자료는 최초 한 번만 발급하고 GUID→문서 ID 대응표를 재사용한다. 외부 앱은 .meta를 필수로 요구하지 않는다.
- flowId와 bindings를 분리 보존한다. 레벨별 펼친 복사본을 제작 원본으로 저장하지 않는다. previousLevelNumbers와 완료 기록도 유지한다.
- 좌표는 0 기준, 화면 의미는 1~9다. 순서에 의미가 있는 공급·단계·조건 목록은 임의 정렬하지 않는다.
- 리소스는 논리 ID로 참조하며 기존 Unity 경로/주소는 별도 매핑으로 보존한다. 외부 문서 읽기에 AssetDatabase를 요구하지 않는다.
- UTF-8/LF/2칸 들여쓰기·일정한 속성 순서를 사용한다. 상위 버전·알 수 없는 종류/키/미지원 필드는 구체적 오류를 내고 수정 저장을 막는다.
- 절대경로·상위 탈출·링크를 통한 루트 탈출을 거절하고 한글 폴더를 지원한다.

**검사:** 모든 종류 왕복, 누락/null/빈 목록, 중복 ID/레벨 번호, 없는 참조/종류 불일치, 미지원 버전/키, 경로 탈출, 한글 경로, 파일명 변경 후 ID 참조 유지. PopupCatalog·외부 SDK SO·플레이어 저장은 제외 목록에 기록한다.

## B. 원본 불변 내보내기와 안전한 저장

**결과:** 구형 자료를 시험 JSON 폴더로 변환하고, 실패에도 이전 정상 문서를 보존한다.

제안 API:
- LegacyContentExporter.Export(trialRoot): 원본 목록·ID 대응·변환 보고서를 반환한다.
- JsonContentStore.Read(relativePath): 문서와 원본 바이트 해시를 반환한다.
- JsonContentStore.Save(document, relativePath, expectedHash): 기대 해시 불일치를 거절한다. 새 파일은 기존 파일이 없어야 한다.

- [x] 원본·메타·팩·Addressables 설정 해시와 dirty 상태를 기록하고 제작 필드 기반 왕복 검사를 확보한다.
- [x] SO 직접 읽기 Editor 어댑터를 구현한다. 영구 SO 재생성·JSON/SO 자동 동기화·실행 팩 역변환은 하지 않는다.
- [x] 임시 스냅샷에 전체 변환 후 모든 문서/참조를 검사하여 시험 결과를 공개한다. 실패한 부분 스냅샷을 성공 결과로 선택하지 않는다.
- [x] 같은 폴더 임시 파일 기록→flush→재읽기 검증→기대 해시 재확인→교체를 구현한다. 이전 정상 복구본과 중단 임시 파일 처리 규칙을 둔다. 외부 프로세스와 완전한 동시 트랜잭션을 보장한다고 과장하지 않는다.
- [x] 쓰기·검증·교체 실패, 외부 수정, 중단 후 재열기, 재내보내기 실패를 주입하여 이전 파일과 매핑을 확인한다. 다른 작업의 자료는 정리하지 않는다.

**검사:** 재내보내기 ID 안정성, 동일 flowId의 두 레벨 bindings 차이, 공유/독립 복사 의미, 명시 표현/기본값 차이, 기획 출처, 원본 해시·dirty 불변, 오류 뒤 정상 파일 재읽기.

## C. 공통 시험 입력 연결과 인계

**결과:** JSON 입력을 실제 기존 게임 세션으로 시험하고 후속 편집 전환에 인계한다.

제안 API: JsonPuzzlePlayAdapter.CreateRequest(validatedSnapshot, levelId, seed) → PuzzlePlayRequest.
실행은 PuzzleGameSession.InitializeAsync(request, PuzzlePlayContext.CreateTest(...), token)을 재사용한다. 시그니처는 착수 시 소스로 다시 확인한다.

- [x] 제작 문서와 실행 사본 사이에서만 공유 흐름을 해석한다. 임시 LevelDefinition/카탈로그의 생성·해제 주체를 명시하며 영구 .asset을 만들지 않는다.
- [x] 실제 레벨과 모든 필수 종류를 포함한 대표 스냅샷에서 동일 시드·행동의 기존 입력과 JSON 입력을 비교한다. 초기/정산 보드, 이동 수, 미션, 튜토리얼 단계, 공급/파워 결과, 표현 DTO 의미를 확인한다.
- [x] 문서 수정과 실행 사본을 격리하고 취소/실패/반복 실행에서 임시 객체를 해제한다. 정식 학습/진행 저장과 SO·팩·Addressables 설정 불변을 검사한다.
- [x] 종류별 결과·수행 명령·제약을 기록하고 테스트 연결을 해제한다. JSON→배포 팩 등록은 후속 범위임을 명시한다.
- [x] 3단계 계획·목표·복사용 실행문을 작성하고 실제 변경·검증·제약을 보고한다. 3단계 구현은 자동 시작하지 않는다. 커밋 메시지는 구현·검증 완료 후에만 제공한다.

다음 검사 실행기를 구현·실행했다. 전체 검사에는 JsonAuthoringChecks.ps1 -Action All을 사용한다.

    & Tools/Testing/JsonAuthoringChecks.ps1 -Action Portable
    & Tools/Testing/ProjectTests.ps1 -Action Run -Method LevelAuthoring.Editor.JsonAuthoringVerification.Run
    & Tools/Testing/ProjectTests.ps1 -Action Status

독립 C# 검사 프로그램 컴파일은 Player/Addressables 빌드와 다르다. 실행환경이 없어 실제 검사를 못 하면 완료로 판정하지 않고 제약을 보고한다.

## 검토 초점

| 실패 조건 | 담당 검사 |
|---|---|
| 실행 값만 저장해 공유 제작 관계 소실 | A flowId/bindings 왕복, B 두 레벨 공유 |
| 기본 표현 보충으로 명시 설정·기획 출처 소실 | B 제작 필드 대응/왕복 |
| 구버전 도구가 상위 버전·미지원 필드 삭제 저장 | A 저장 차단, B 정상 파일 보존 |
| 외부 수정·교체 실패가 정상 파일 손상 | B 실패 주입·해시 충돌·재열기 |
| JSON 시험에서 배포 팩·정식 저장 갱신 | C 전후 불변 |

완료 판정은 각 목표 항목에 실제 검증 증거를 연결하여 수행한다.

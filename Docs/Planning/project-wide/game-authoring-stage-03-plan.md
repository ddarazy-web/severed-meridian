# 게임·제작 도구 분리 3단계 — 공통 편집과 JSON 작업 모드

> 2026-10-09 구현·검증 완료. [검증 기록](../../Verification/project-wide/game-authoring-stage-03-progress.md) 및 명시한 지원 경계를 따른다.
> 실행 담당은 superpowers:executing-plans로 아래 세 작업 묶음을 진행한다. 필요한 독립 조사·검증에는 하위 에이전트를 사용할 수 있다.

**Goal:** 기존 Unity 레벨 에디터에서 명시적으로 선택한 JSON 작업 폴더를 열어 편집·저장·플레이 시험하고, 이 편집 기능을 이후 레벨툴 씬에서도 재사용한다.
**Architecture:** 편집 세션이 문서·선택·변경 이력·미저장 상태를 소유한다. 기존 UI는 공통 편집 연산과 저장/실행 어댑터를 호출한다. SO 편집과 JSON 편집을 자동 동기화하지 않는다.
**Tech Stack:** Unity 6000.3.10f1, 기존 UI Toolkit/Editor UI, 2단계 제작 JSON 계약·ContentSnapshotStore·JsonPuzzlePlayAdapter.
**Spec:** [통합 6단계 계획](2026-10-08-game-and-authoring-products-plan.md), [JSON 전환 계약](2026-10-08-json-authoring-transition-plan.md).
**완료 조건:** [목표](../../Goals/project-wide/game-authoring-stage-03-goal.md) · [실행문](../../Commands/project-wide/game-authoring-stage-03-command.md).
**착수 근거:** [2단계 검증 기록](../../Verification/project-wide/game-authoring-stage-02-progress.md). 기록의 미완료 항목이 있으면 먼저 해결하고 착수한다.

## 운영 경계

- ServeredMeridian의 work 작업 환경을 유지한다. 커밋·푸시·Player/Addressables 빌드 금지. HTML 매뉴얼은 요청 전까지 변경하지 않는다.
- 질문은 제한시간 없는 일반 채팅 문답으로 충분히 설명한다.
- JSON 작업 모드는 사용자가 폴더를 선택하여 진입한다. 기존 SO 전체 이관·기본 원본 전환은 6단계다.
- 50레벨 MemoryPack·Addressables·프로필·정식 게임 입력과 저장을 유지한다. JSON 문서를 편집하고도 옛 SO로 팩을 생성하는 경로는 차단한다.
- 공통 레벨툴 씬과 Windows 실행 UI는 4단계 시범, 5단계 동등성 확인 후 Unity 기본 편집 화면으로도 사용한다. 이번에 기존 창을 없애지 않으며 중복 UI를 장기 유지하는 구조도 만들지 않는다.
- 편집의 저장소와 실행 경계에 기존 코드를 재사용한다. 모든 기능에 새 인터페이스·명령 클래스·MVVM 계층을 일괄 도입하지 않는다.
- UI가 ScriptableObject를 읽는 임시 어댑터를 사용할 수 있으나 공통 편집 API에 SO/SerializedObject/AssetDatabase를 노출하지 않는다.

## 현재 연결 지점과 파일 책임

착수 때 실제 소스를 다시 확인한다. 같은 역할의 파일이 있으면 확장하고 빈 계층을 만들지 않는다.

| 경로 | 역할 |
|---|---|
| LevelAuthoring/Documents, Storage, Validation | 2단계 문서·참조 검사·충돌/복구 저장 계약 재사용 |
| 신규 LevelAuthoring/Editing/AuthoringEditSession.cs | 열린 문서·선택·미저장·Undo/Redo 상태의 단일 소유자 |
| 신규 LevelAuthoring/Editing/LevelDocumentEditing.cs | 배치/교체/이동/복제/삭제와 미션·공급 등 실제 공통 편집 연산 |
| LevelEditor/Editor/Application/LevelCommonEditing.cs 및 기존 배치 편집 코드 | SerializedObject/Unity Undo와 공통 연산 사이 어댑터 |
| LevelEditor/Editor/Presentation/LevelEditorWindow.Workspace.cs 및 관련 패널 | SO/JSON 작업 모드 표시·열기·저장·레벨 선택 |
| Levels/Editor/Persistence/LevelAssetOperations*.cs | 기존 SO 흐름 유지 및 JSON 모드에서 잘못된 SO 저장 차단 |
| Tutorial/Editor/LevelTutorialEditorPanel*.cs, TutorialUserSampleStore.cs, TutorialFlowAuthoring.cs | 공통 흐름·레벨별 값·샘플의 JSON 저장 연결 |
| LevelEditor/Editor/Presentation/LevelEditorWindow.GameLaunch.cs | 현재 편집 사본을 공통 플레이 요청에 전달 |
| Tests/Editor/Features/LevelAuthoring 및 Tests/Portable/LevelAuthoring | 공통 편집, 창 연결, 저장/복귀 회귀 검사 |

경로는 Assets/Scripts/Features/ 아래이며 신규 테스트 원본은 Tests 아래에 둔다.

## A. 편집 상태와 공통 변경 연산

**결과:** Unity Editor 밖에서도 호출할 수 있는 편집 기능과 단일 변경 이력.

- [x] 1단계 기능 목록을 기준으로 실제 UI 이벤트→변경→저장 호출을 대조한다. 기본 보드, 층, 2×2, 연결/흐름, 미션, 공급, 튜토리얼, 샘플, 모양 기능의 연결 위치를 기록한다.
- [x] 대표 실패 검사를 먼저 만든다: 같은 층 블록↔장애물 교체, 2×2 점유 충돌, 이동/삭제의 연결 ID 보존, 복제 ID 재발급, 레벨 번호 변경과 학습 ID 의미.
- [x] AuthoringEditSession은 ContentSnapshot/선택 레벨 ID/저장 당시 스냅샷 revision(project와 하위 문서 해시)으로 연다. Apply/Undo/Redo 후 문서 사본·선택·미저장 상태를 조회할 수 있게 한다. API의 구체적인 연산 인자는 현재 UI 호출에 맞춰 확정한다.
- [x] 저장할 수 있는 완전한 문서와 편집 중 임시로 불완전한 입력을 구분한다. 오류를 표시하고 게임 시험/공개 저장을 차단하되 사용자가 값을 수정할 수 있게 한다.
- [x] 한 사용자 동작을 한 이력으로 묶고 취소된 입력은 이력에 넣지 않는다. JSON 모드의 공통 이력과 Unity Undo를 중복 실행하지 않는다.
- [x] 공통 연산에 UnityEditor 참조가 없고 현재 단축키·다중 선택·취소 동작과 충돌하지 않는지 검사한다.

**검증:** 연산 전후 문서 비교, Undo/Redo 왕복, 동일 문서 독립 세션, 선택/미저장 상태 유지, 실패한 편집의 부분 변경 없음.

## B. 기존 에디터의 JSON 작업 폴더·저장 연결

**결과:** 사용자가 기존 창에서 JSON 레벨을 직접 제작하고 다시 열 수 있다.

- [x] 작업 모드를 명시한다. 화면에 SO/JSON, 현재 폴더·레벨·미저장 상태를 표시하고 잘못된 원본에 저장하지 않게 한다.
- [x] 폴더 열기, 레벨 목록, 새 레벨, 복제, 저장, 다시 읽기를 연결한다. 기존 문서 ID는 번호·이름·파일명 변경으로 재발급하지 않는다.
- [x] ContentSnapshotStore.Read/Publish(snapshot, expectedHash)와 저장 실패/충돌/이전 정상본 처리 규칙을 사용한다. 외부 수정은 조용히 덮어쓰지 않고 다시 읽기/별도 사본 선택을 안내한다.
- [x] 폴더·레벨 변경, 창 닫기, Play Mode 전환 시 미저장 내용의 저장/버리기/취소를 제공한다. 취소 시 선택과 편집 내용이 남아야 한다.
- [x] 보드 배치뿐 아니라 공통 튜토리얼·레벨 bindings·사용자 샘플·모양 저장을 같은 작업 폴더에 연결한다. 공유 수정의 영향 레벨과 독립 복사를 구분해 표시한다.
- [x] 기존 SO 모드의 저장·복제·Undo 동작이 유지되는지 회귀 검사한다.

**검증:** 새로 만들기→편집→저장→재열기, 여러 레벨 왕복, 공유/사본 수정 범위, 충돌·권한/잠금 실패·미지원 버전·복구, 미저장 종료의 세 선택.

## C. 현재 편집 입력의 플레이 시험과 인계

**결과:** 아직 SO나 배포 팩으로 바꾸지 않은 JSON 편집 내용을 실제 게임에서 시험한다.

- [x] 게임 플레이/플레이 테스트/진단 실행이 현재 편집 스냅샷·시드·시험 튜토리얼 정책을 사용하게 한다. JsonPuzzlePlayAdapter.CreateRequest와 PuzzlePlayContext.CreateTest를 사용한다.
- [x] 시험 시작 시 문서를 고정한 사본을 만들고, 시험 도중 편집 원본을 수정하지 않는다. 돌아오면 선택·스크롤·작업 폴더·미저장·Undo 상태를 복구한다.
- [x] JSON 모드에서 배포 팩 생성이나 옛 SO 기반 MemoryPack 시험을 혼동해 실행하지 않게 한다. 미구현 버튼은 정확한 이유를 표시하고 잘못된 대체 실행을 하지 않는다.
- [x] 같은 데이터·시드·행동의 SO/JSON 편집·실행 결과와 튜토리얼/고정 공급/2×2/파워 블록을 비교한다.
- [x] 실제 창 조작과 플레이 시험 후 원본/정식 저장/배포 리소스 불변 및 임시 객체 정리를 확인한다.
- [x] 4단계 계획·목표·복사용 실행문을 작성하고 변경·검증·남은 제약을 보고한다. 커밋 메시지는 실제 완료한 변경에 맞춰 마지막 보고에만 제공한다.

**권장 검사 경로:** 기존 Tools/Testing/ProjectTests.ps1과 JsonAuthoringChecks.ps1을 확장한다. 새 명령은 파일을 만든 후 실행한다. 테스트 연결은 완료 시 해제한다. 실제 Player 빌드는 수행하지 않는다.

## 검토 초점

| 실패 위험 | 담당 증거 |
|---|---|
| 화면은 JSON인데 SO를 저장/시험 | B 모드별 실제 저장 경로 + C 입력 동등성 |
| 공유 흐름 편집이 의도치 않게 모든 레벨 변경 | B 영향 목록·독립 복사 검사 |
| Editor Undo와 공통 이력이 두 번 적용 | A 단일 동작 왕복 검사 |
| 시험 복귀/창 닫기에서 미저장 내용 유실 | B 취소·복구, C 시험 복귀 |
| 미완성 조건을 고치지도 저장하지도 못함 | A 편집 중 오류 표시와 정상화 후 저장 검사 |

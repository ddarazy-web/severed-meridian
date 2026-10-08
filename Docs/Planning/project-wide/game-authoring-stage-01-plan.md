# 게임·제작 도구 분리 1단계 — 제품 경계와 공통 플레이 진입

> 2026-10-08 · 구현·검증 완료
> 실행: superpowers:executing-plans를 기본으로 사용한다. 필요하면 하위 에이전트를 조사·검토 또는 파일 소유권이 겹치지 않는 구현에 사용한다.

**Goal:** 기존 게임과 Unity 레벨 에디터의 동작을 유지하면서, Windows 제작 도구가 재사용할 플레이 진입·시험 상태 경계를 확보한다.
**Architecture:** 기존 PuzzleGameSession 및 실행 엔진을 재사용한다. 편집기 전용 씬 전환과 파일 선택은 Editor에 남기고, 레벨 사본·표현 설정·튜토리얼 정책을 전달하는 실행 계약은 Runtime에 둔다. 제품 종류와 시험 실행 여부를 구분한다.
**Tech Stack:** Unity 6000.3.10f1, C#, UniTask, 기존 MemoryPack·Addressables·튜토리얼 실행 문맥.
**Spec:** [6단계 통합 계획](2026-10-08-game-and-authoring-products-plan.md)
**완료 조건:** [1단계 목표](../../Goals/project-wide/game-authoring-stage-01-goal.md)
**실행문:** [복사용 목표 명령문](../../Commands/project-wide/game-authoring-stage-01-command.md)

## 운영 제약

- ServeredMeridian만 작업한다. 기존 변경·씬·프리팹·데이터와 GUID를 보존한다.
- 커밋·푸시는 하지 않는다. 마지막 보고에 실제 변경을 요약한 커밋 메시지만 코드 블록으로 제공한다.
- Player/Addressables 빌드를 하지 않는다. Editor 컴파일과 필요한 Play Mode 검증은 가능하다.
- HTML 매뉴얼은 별도 요청 전까지 갱신하지 않는다.
- 질문은 일반 채팅 문답으로 설명하고 번호 선택을 제공한다. 제한시간 있는 질문 도구나 무응답 자동 선택을 사용하지 않는다.
- 하위 에이전트 사용은 허용되지만 필수가 아니다. Unity 실행·공용 파일 수정은 주 담당이 통합한다.
- 전체 6단계를 유지한다. 이번 단계 내부는 아래 세 묶음이며, 각 묶음을 별도 단계 문서로 쪼개지 않는다.
- 완료 후 2단계 계획·목표·복사 가능한 실행문을 작성하고 보고한다. 2단계 구현은 자동 착수하지 않는다.

## 확인한 기반과 구현 범위

- 네 Build Profile과 제품 심볼은 이미 있다. 다시 만들지 않는다. 레벨툴 프로필의 씬은 아직 비어 있다.
- PuzzleGameSession.InitializeAsync(LevelDefinition ownedDefinition, int randomSeed, CancellationToken token)은 사본 소유권을 받아 성공·실패·취소 후 해제한다.
- ConfigureVisuals와 ConfigureTutorial은 시작 전에 설정을 받는다. Start의 기본 레벨 로딩과 외부 초기화의 중복 실행을 피해야 한다.
- PuzzleEditorLaunchRequest는 Asset/MemoryPack 입력을 스냅샷으로 만들지만 Levels.Editor와 파일 경로에 의존한다. 클래스 전체를 Runtime으로 이동하지 않는다.
- TutorialExecutionContext는 PlayerPrefs 기반 정식 실행과 콜백 기반 Editor 실행을 이미 지원한다. 기존 학습 ID와 완료 기록 의미를 유지한다.
- 정식 게임의 진행·아이템·광고·분석 등 실제 연결 범위는 묶음 A에서 호출부로 확인한다. 존재하지 않는 서비스를 새로 구현하지 않는다.
- JSON 전환은 2단계, 기본 도구 화면은 4단계, 고급 기능 이식은 5단계, 완전한 배포 격리는 6단계다.

## 변경 위치와 책임

경로는 저장소 루트 기준이다. 신규 파일은 이번 계획의 제안이며, 동일 책임의 기존 타입이 있으면 재사용하고 변경 이유를 기록한다.

| 위치 | 역할 |
|---|---|
| Assets/Scripts/Features/GameScreen/Runtime/Session/PuzzlePlayRequest.cs (신규 후보) | 레벨 스냅샷·번호·시드·표현 DTO를 소유하는 실행 입력 |
| Assets/Scripts/Features/GameScreen/Runtime/Session/PuzzlePlayContext.cs (신규 후보) | 정식/시험 실행 구분, 기존 튜토리얼 문맥 등 실제 필요한 정책 |
| Assets/Scripts/Features/GameScreen/Runtime/Session/PuzzleGameSession.cs 및 관련 partial | 공통 진입 연결, 실패·취소·종료 처리 |
| Assets/Scripts/Features/GameScreen/Editor/Launch/PuzzleEditorLaunchRequest.cs | 에셋/팩 읽기와 검증을 담당하는 Editor 어댑터 |
| Assets/Scripts/Features/GameScreen/Editor/Launch/PuzzleEditorLauncher.cs | 씬 저장/복원·SessionState 전달을 유지하고 공통 진입 호출 |
| Assets/Scripts/Features/Tutorial/Runtime/TutorialExecutionContext.cs | 필요한 경우에만 환경 중립 팩터리 추가; 기존 완료 기록 호환 유지 |
| Tests/Editor/Features/GameScreen/PuzzlePlayBoundaryVerification.cs (신규) | 사본·정책·실행 동등성·취소·저장 격리 검사 |
| Tests/Editor/Features/GameScreen/PuzzleEditorLaunchVerification.cs 및 PuzzleEditorLaunchLifecycleVerification.cs | 기존 에디터 실행 회귀 검사 재사용 |
| Docs/Verification/project-wide/game-authoring-stage-01-progress.md (구현 중 생성) | 기능/의존 표, UI 기술 결정, 검증 증거, 남은 제한 |

제품 분기용 클래스·asmdef·서비스 인터페이스는 실제 호출 경계가 필요할 때만 추가한다. 공통 퍼즐 로직에 제품 심볼 분기를 추가하지 않는다.

## A. 제품·기능 경계와 기준 결과 확보

- [x] 레벨 편집·미션·튜토리얼·공급/흐름·모양·자동 플레이·게임 진입을 목록화하고 공유 로직/Editor 전용 UI/외부 서비스로 나눈다. 각 항목을 1~6단계 담당에 대응한다.
- [x] 게임 진입에서 호출하는 저장·아이템·튜토리얼 완료·외부 SDK 초기화의 실제 위치를 조사한다. 네 제품별 사용/미사용/미구현 표를 남긴다. 플랫폼과 제품을 혼동하는 초기화가 있으면 이번 실행 경로에서 분리한다.
- [x] 같은 레벨·시드·행동에 대한 초기 보드, 이동 수, 미션, 튜토리얼 상태의 기준 결과를 기존 검사로 확보한다. 미저장 편집 상태와 원본 파일 해시도 기록한다.
- [x] 독립 도구 UI의 기술 확인은 보드 셀 선택·속성 변경·긴 목록 스크롤의 작은 런타임 시제품으로 제한한다. 기존 UI Toolkit 요소의 Runtime 지원 여부를 확인하고 현재 게임 표현 연결 비용과 비교한다. 결과와 선택 근거를 기록하며, 시제품을 완성된 레벨툴 씬으로 등록하지 않는다.
- [x] asmdef는 순환 의존과 기본 Assembly-CSharp 참조 제약을 확인하여 필요 경계를 결정한다. 이 단계에서 광범위 이동 없이 분리할 수 없으면 근거와 3/4단계 적용 위치를 기록한다.

**산출:** 구현/재사용 범위가 표시된 기능·의존 표, 기준 결과, 도구 UI 기술 및 어셈블리 경계 결정.

## B. 공통 진입과 시험 상태 연결

**제안 계약:** GameScreen.PuzzleGameSession에
UniTask InitializeAsync(PuzzlePlayRequest request, PuzzlePlayContext context, CancellationToken token)
진입을 추가하고 기존 오버로드는 호환 경로로 유지한다. Request는 UnityEditor 타입·파일 시스템 위치를 포함하지 않는다. 현재 단계의 스냅샷 형식은 기존 MemoryPack을 사용하며 JSON 문서 형식으로 확정하지 않는다.

- [x] 기존 코드에서 독립 실행이 불가능한 호출 또는 저장 오염을 재현하는 검사를 먼저 작성한다. 이미 충족하는 요구는 재구현하지 않고 검증으로 고정한다.
- [x] Request의 입력 바이트/DTO 변경이 실행 중 자료에 영향을 주지 않도록 사본 소유권을 명시한다. 세션이 디코딩한 LevelDefinition만 해제하며 원본 SO를 파괴하지 않는다.
- [x] Request·Context를 적용한 뒤 실행하는 순서를 공통 진입으로 묶는다. 레벨/시드/표현/튜토리얼 정책 중 일부만 설정된 상태로 Start가 먼저 실행되지 않게 한다.
- [x] 정식 실행은 기존 정책을 유지하고, 시험 실행은 기존 Editor 튜토리얼 콜백 또는 세션 전용 메모리 저장을 사용한다. 시험 문맥 누락 시 PlayerPrefs 기반 정식 실행으로 조용히 전환하지 않는다.
- [x] 실제 연결된 게임 진행·아이템·외부 서비스의 부수효과가 있으면 시험에서 차단하거나 기존 시험 공급자로 연결한다. 존재하지 않는 경제/서비스 저장소는 만들지 않는다.
- [x] Editor 요청의 파일 읽기·SessionState 전달·씬 왕복은 유지하고 공통 진입으로 연결한다. Asset/MemoryPack 선택 및 Automatic/Always/Never 튜토리얼 선택을 보존한다.
- [x] 실패·취소·중복 초기화·세션 파괴에서 요청 사본/이벤트/리소스를 정리한다. 초기화 완료 UniTask만으로 성공을 추정하지 않고 실제 세션 상태와 오류를 검사한다.
- [x] 구현한 Runtime 계약은 UnityEditor 없이 호출 가능해야 한다. LevelDefinition 등 UnityEngine 의존을 제거하기 위한 전체 재작성은 하지 않는다.

**산출:** 기존 에디터가 사용하는 공통 실행 경로와 정식/시험 상태 경계. 도구 파일 열기 UI나 전체 제작 화면은 만들지 않는다.

## C. 동등성·격리·수명주기 검증과 인계

- [x] 같은 입력·시드·행동을 기존 기준과 비교한다. 초기 보드, 유효 교환 후 정산, 이동 수, 미션, 튜토리얼 단계가 일치해야 한다. 시각 프레임 시간 자체를 동일성 조건으로 삼지 않는다.
- [x] 시험에서 튜토리얼 완료·아이템 사용·재시작을 수행해 실제 연결된 정식 저장 경로와 관련 PlayerPrefs 키가 변하지 않는지 확인한다. 관련 없는 사용자 설정 전체를 삭제하거나 초기화하지 않는다.
- [x] 잘못된 입력, 시작 전 취소, 로딩 중 취소, 실행 중 종료, 반복 왕복, 중복 초기화를 검사한다. 원본 사본 수·이벤트 중복·리소스 누적과 미저장 편집 상태 유실이 없어야 한다.
- [x] 정식 게임의 기존 튜토리얼 완료 정책과 일시정지→재개 동작도 회귀 확인한다.
- [x] 기존 프로필/GUID/전역 설정 불변, 테스트 연결 해제, 의도하지 않은 소스·에셋 변경 유무를 확인한다.
- [x] 진행 기록에 수행 명령·결과·미검증 항목을 남기고 목표 체크리스트를 판정한다. 완료 후 2단계 문서와 실행문을 작성한다.

**검증 실행:** 신규 검증기에 public static void Run()과 명시적 배치 종료를 제공한 뒤 다음 명령을 실행한다.

```powershell
& Tools/Testing/ProjectTests.ps1 -Action Run -Method GameScreen.Editor.PuzzlePlayBoundaryVerification.Run
& Tools/Testing/ProjectTests.ps1 -Action Run -Method GameScreen.Editor.PuzzlePlayBoundaryVerification.RunLifecycle
& Tools/Testing/ProjectTests.ps1 -Action Status
```

위 검증기는 구현되었다. RunLifecycle 래퍼는 기존 왕복 검사의 완료/정리를 기다리고 배치 프로세스를 명시적으로 종료한다. 기존 수명주기 검사가 현재 범위에 불필요한 데이터 생성·빌드를 수행하는지 먼저 읽고, Player/Addressables 빌드 호출이 있으면 해당 검사를 분리한다. Unity가 열려 있으면 임의로 닫지 말고 가능한 검증을 먼저 진행한 뒤 필요한 수동 조작을 시간 제한 없이 요청한다.

## 검토 초점

| 실패 조건 | 검증 담당 |
|---|---|
| Start와 외부 초기화 경합으로 두 번 시작 | B 중복 초기화 / C 반복 왕복 |
| 시험에서 학습 ID 이관이 정식 기록을 변경 | B 문맥 분리 / C 관련 저장 불변 |
| 시작 후 원본/요청 수정이 실행 사본에 반영 | B 사본 소유 / C 원본 해시·상태 비교 |
| 취소/실패 후 artwork·이벤트·LevelDefinition 누적 | B 정리 / C 수명주기 |
| Windows 도구가 Steam용 서비스로 진입 | A 실제 연결 조사 / B 시험 정책 / C 초기화 호출 검사 |

실제 제품별 컴파일·네이티브 플러그인 및 배포 콘텐츠 격리는 6단계에서 검사한다. 이번에는 공통 실행 진입과 실제 연결된 시험 경계를 완료 기준으로 삼는다.

실제 구현·검증 증거: [1단계 진행 기록](../../Verification/project-wide/game-authoring-stage-01-progress.md). 공통 레벨툴 씬을 Unity 기본 편집 화면으로 전환하는 추가 결정은 통합 계획의 4/5단계에 반영했다.

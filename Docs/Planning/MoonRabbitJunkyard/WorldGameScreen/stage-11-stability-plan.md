# 11단계 반복 플레이와 성능 안정화 Implementation Plan

> 실행 스킬: `superpowers:executing-plans`로 4개 작업을 순서대로 직접 수행한다. 작업별 구현 에이전트는 만들지 않고 전체 작업 후 독립 최종 리뷰 한 번을 수행한다. 자동 커밋하지 않는다.

**Goal:** 최신 게임 연출을 보존하며 반복 플레이·중단·화면 전환·리소스 재사용을 검증하고 재현된 결함만 안정화한다.

**Architecture:** 기존 세션/월드/HUD/소리의 수명 경계를 유지한다. Editor 전용 검사가 실제 씬과 기존 직접 실행기를 비교하고 소유 풀을 관찰한다. 제품 코드 수정은 검사로 확인한 결함의 담당 파일에 한정한다.

**Tech Stack:** Unity 6000.3.10f1, 기존 C#·UniTask·uGUI·SpriteRenderer·Addressables·MemoryPack·AudioSource. 새 패키지 없음.

**Spec:** [11단계 목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-11-stability-goal.md).

상태: 작업1~4·12개 완료 조건 감사·독립 리뷰 수정 pass 완료 (2026-10-02).

## Global Constraints

명세의 보존·제외 기준 전체를 적용한다. 특히 9×9, 낙하/공급 시간 /1.44, 최신 드론, 동시 공급·신규 대각선·상단 우선, 2×2 피해·스와이프 order, 미션/라스트팡/10단계 소리, 아이콘/GUID, 50레벨 MemoryPack·선택적 아틀라스 로드를 유지한다.

진동·속도 재설계·새 규칙/DTO/패키지/설정 화면을 추가하지 않는다. 플레이어 및 Addressables 콘텐츠 빌드·내부 빌드 호출 검사·커밋/푸시·사용자 Editor 강제 종료/씬 저장은 금지한다. 원본과 무관 dirty를 보존한다. 다음 단계 자동 실행 없음.

## 현재 확인된 코드와 책임

경로 기준은 `Assets/Scripts/Features/GameScreen/`다. 아래 기존 파일은 모두 수정 대상이 아니라 조사/검증 대상이며 결함이 재현된 책임만 수정한다.

| 경로 | 책임 |
| --- | --- |
| Runtime/Session/PuzzleGameSession.cs, .Presentation.cs, .Controls.cs, .Progress.cs, .Feedback.cs | 게임 준비/입력/표시/수집/소리/중단·재시작 소유권 |
| Runtime/World/PuzzleWorldBoard.cs | cells/bodies/decorations/supplyImages/supplyClips/effects 재사용·스와이프 order |
| Runtime/World/PuzzleBoardSettlementPlayback.cs, PuzzlePowerPlayback*.cs | 실제 낙하/공급/파워·드론 시간표·타격 재생 |
| Runtime/World/PuzzleArtwork.cs, Board/Runtime/BoardSpriteAtlas.cs | 필요한 아틀라스 로드·pending 완료 후 반환 |
| Runtime/UI/PuzzleHudView*.cs, PuzzleProgressFeedback.cs | 수집 표시와 재사용·실제 도착 |
| Runtime/UI/PuzzleScreenLayout.cs, PuzzleScreenView.cs | 안전 영역·결과·UI 입력 차단 |
| Runtime/Feedback/PuzzleAudioPlayback.cs, PuzzleFeedbackSchedule.cs | 14클립/8음성과 예약 소유권 |
| Editor/Tests/PuzzleStabilityVerification.cs (신규 예정) | Data 진입점·결과 기록 |
| Editor/Tests/PuzzleStabilityVerification.Scene.cs (신규 예정) | RunScene, 실제 상태/반복/중단/레이아웃 통합 검사 |
| Editor/Tests/PuzzleStabilityVerification.Observation.cs (신규 예정) | Observe, 실제 Update의 구간·풀·Editor 성능 관찰 |

BoardSpriteAtlas는 `Assets/Scripts/Features/Board/Runtime/`에 있다. 역할 분리가 실제로 필요할 때만 테스트 partial을 추가한다. 공개 제품 진단 API나 범용 풀은 만들지 않는다. 기존 private 풀은 Editor 검사에서 현재 검증 관례의 reflection으로 관찰하고 필드명 변경 시 검사가 명확히 실패하게 한다.

## Review Focus

1. 같은 시드 반복에서는 안정되지만 다른 동시 표시량에서 풀 확장이 필요한 경우: 정상 최대 용량 증가와 누수를 구분한다. Task 2의 반복/추가 동시량 검사로 고정한다.
2. pause와 앱 background가 준비 완료·결과 확정 사이에 겹침: 과거 소리 제거와 미래 결과음을 동시에 보존한다. Task 2의 표시 전/후 복귀 검사로 고정한다.
3. 회전 중 스와이프 preview 또는 HUD 수집 비행: 좌표를 재계산하고 order/최종 상태를 보존한다. Task 2의 연출 중 레이아웃 검사로 고정한다.
4. 실제 Update 관찰과 수동 deltaTime 검사가 혼용됨: 성능은 실제 Update, 결정적 타이밍은 수동 시계로 기록하고 청취/기기 성능과 구분한다. Task 3의 별도 관찰 진입점으로 고정한다.
5. 캐시가 준비돼 비동기 검사가 실제 대기 없이 끝남: 준비 중 취소/파괴의 전제부터 검증하며 미로드 아틀라스의 실제 pending을 확인한다. Task 2의 pending 전제 단언으로 고정한다.

## Task 1 — 기준 감사와 측정 준비

**Files:** 신규 Editor/Tests/PuzzleStabilityVerification.cs; 기존 단계 검증·최신 runtime은 읽기만 한다.

**Interfaces:** `public static void Data()`는 Editor 데이터 검사를 실행하고 `Logs/Stage11/data-results.txt`에 PASS/FAIL을 기록한 후 성공 0/실패 1로 종료한다. 기존 `LevelInitialStateVerification`의 전체 Snapshot과 기존 fixture/직접 실행기를 재사용한다. Task 2/3은 이 증거 기준과 입력 목록을 소비한다.

- [x] 10단계 12개 조건·실행 기록·현재 소스/로그를 감사하고 시작 상태, 이미지 SHA256/GUID, 속도 필드, 선택 맵/시드, Editor 버전·측정 환경을 Logs/Stage11에 기록한다. 미완료가 있으면 기존 목표를 먼저 처리한다.
- [x] Data 검사 `BaselineInvariantChecks`를 작성한다: 9×9, 확정 timing/드론 기준, 원본 레벨 직렬화 불변, 아이콘 해시/GUID, 소리14/8/.06 기준. 수치 기준은 현재 소스/10단계 증거에서 캡처하고 임의 새 기본값을 만들지 않는다.
- [x] 검사 진입점 미구현의 실패와 구현 후 통과를 확인한다. 기존 동작에 결함이 없으면 baseline PASS를 그대로 기록하며 제품 코드를 바꾸기 위해 실패를 만들지 않는다.
- [x] 실행: 아래 공통 명령에서 METHOD=`Data`. Expected: 결과 파일 생성, 실제 PASS>0/FAIL0/Exit0. 검사 수는 실행 후 기록한다.

## Task 2 — 반복 플레이·중단·화면 전환 검증

**Files:** 신규 Editor/Tests/PuzzleStabilityVerification.Scene.cs. 재현 결함만 Session/World/UI/Feedback의 해당 소유자 수정. 원본 씬/레벨·전체 프리팹 생성기 사용 금지.

**Interfaces:** `public static void RunScene()`은 기존 PuzzleGameAssets.ScenePath를 별도 검사용 Editor에서 열고 Play Mode 검사 후 `scene-results.txt`와 종료 0/1을 기록한다. Task 1 기준, `InitializeAsync(LevelDefinition,int,CancellationToken)`, `RestartAsync(CancellationToken)`, `SetPaused(bool)`, `ApplyLayout(Rect,Vector2)`, 기존 Snapshot/ActionQuery/직접 실행기를 소비한다.

- [x] `SourceAndActionChecks`: 원본 Level_01 Asset/MemoryPack, 단독 4파워/10조합, 2×2 내구도9 중첩, 3아이템·연쇄·회수 fixture를 실제 세션에서 실행한다. 각 완료 후 전체 State/Phase/Outcome을 직접 실행기와 비교하며 원본 불변도 검사한다.
- [x] `InterruptionBoundaryChecks`: 교환·복귀·제거·낙하·공급·드론·수집·라스트팡 각 진행 중 pause→resume 및 background→resume을 검사한다. pause 시간/음성/잠금 유지, 완료 후 입력 또는 결과 정상. 결과 표시 전 복귀 Win/Lose1, 표시 후 복귀 중복0을 단언한다.
- [x] `LifetimeChecks`: 표시 중 다시하기5회, 실제 pending 효과 로드 중 취소/오류/씬 종료 후 완료를 기다린다. 이전 cue/예약/활성 표시/소유 클립·아틀라스 잔류0과 새 씬 입력 정상을 단언한다. pending 상태 자체를 먼저 확인한다.
- [x] `LayoutDuringPlaybackChecks`: 1920×1080↔1080×1920, 각각 Rect(24,36,width-48,height-72) 안전 영역을 스와이프·낙하·드론·수집 중 적용한다. 선택 order/복원, 입력 좌표와 수집 도착 위치, 전체 규칙 상태 동등을 검사한다.
- [x] `PoolReuseChecks`: 동일 fixture/시드/행동 시퀀스를 실제 같은 세션에서 워밍업5회+추가5회 실행한다. 단계별 총 용량/활성 수를 기록하고 추가5회에 동일 최대 용량·종료 활성0(상시 보드/UI 제외)·소리≤8/클립14를 검사한다. 더 많은 동시 표시 fixture의 확장은 허용하되 그 fixture 반복에서도 안정됨을 확인한다.
- [x] 재현한 결함마다 실패 검사→담당 소유자의 최소 수정→통과를 남긴다. 버그가 없으면 기존 코드는 그대로 둔다. 실행 METHOD=`RunScene`. Expected: 모든 named checks PASS, FAIL0/Exit0, 입력·잔류·최종 상태 단언 포함.

## Task 3 — 실제 Editor 성능 관찰과 확인된 병목 수정

**Files:** 신규 Editor/Tests/PuzzleStabilityVerification.Observation.cs. 측정으로 확인된 담당 hot path만 필요한 경우 수정한다.

**Interfaces:** `public static void Observe()`는 Time.timeScale=1의 실제 Update 플레이를 관찰하고 `Logs/Stage11/observation-results.txt`와 `observation.csv`에 환경/시드/구간/표본을 기록한 후 종료한다. Task 1의 기준과 Task 2의 검증된 fixture를 재사용한다. 제품 속도 API·설정값을 추가하지 않는다.

- [x] `ObservationChecks`: 일반 연쇄·공급 집중·다수 드론/조합을 실제 Update로 실행한다. 초기 준비와 워밍업은 별도 구간으로 표시하고 준비/보드 표시/수집/입력 가능 대기의 소요시간·풀 용량을 수집한다. 정상 플레이 표본 총300프레임 이상, 환경/수집 방법/표본 수를 검증한다.
- [x] Unity Editor Profiler에서 가능하면 CPU/GC Alloc/메모리 관찰을 함께 기록한다. 프레임 p50/p95/max를 남기되 수동 deltaTime이나 batchmode FPS를 모바일 FPS로 주장하지 않는다. 수집 불가능한 profiler 항목은 이유를 기록하고 수집한 시간/객체 수 증거와 구분한다.
- [x] 같은 환경/fixture에서 불필요한 Instantiate/Destroy, 반복 아틀라스 요청, 큰 프레임 할당·멈춤을 조사한다. 재현 가능한 원인만 수정하며 일반 캐시/전역 풀·전체 LINQ 교체 같은 범위 확장은 하지 않는다. 결과가 정상이라면 성능 수정을 만들지 않는다.
- [x] 수정했다면 해당 결함의 재현 실패→수정 통과와 동일 관찰을 다시 실행해 전후 수치·표본·환경 차이를 기록한다. 체감 개선·속도 변경을 측정 없이 확정하지 않는다. METHOD=`Observe`. Expected: ≥300 실제 프레임 증거, 요청 빌드 없이 정상 종료0, 수명/규칙 회귀 통과. 성능 숫자 자체의 절대 합격선은 설정하지 않는다.

## Task 4 — 회귀·최종 감사·사용 안내

**Files:** 실행 후 Docs/Verification/.../stage-11-progress.md 및 Docs/Guides/.../stage-11-stability-usage.md 작성. 관련 목차/로드맵 상태 갱신.

- [x] 실제 수정 영향에 맞춰 빌드 없는 기존 진입점을 실행한다: PuzzleSwapAnimationVerification.Run/RunScene, PuzzleSettlementAnimationVerification.Run/RunScene, PuzzlePowerAnimationVerification.RunScene, PuzzleProgressFeedbackVerification.Data/RunScene, PuzzleAudioFeedbackVerification.Data/RunScene, PuzzleEditorLaunchVerification.RunData, PuzzleUILayoutVerification.Run, PuzzleUIInteractionVerification.Run. 내부 빌드 호출이 있는 진입점은 제외한다.
- [x] Expected: 선정한 각 검사 결과 PASS>0/FAIL0/Exit0. 이전 단계 실적을 새 실적으로 합산하지 않고 이번 실제 실행의 새 안정화/관련 회귀 수를 구분한다. 파일 생성 시각·실제 프로세스 종료·컴파일 오류를 확인하며 오래된 결과를 성공 근거로 쓰지 않는다.
- [x] 목표12개별 로그/범위/화면(실제 수행 시)/성능 관찰·보존 audit·청취/기기 한계와 사용법을 기록한다. 실제 청취가 가능하면 실시하고 아니면0회/미검증으로 기록한다. 완성 화면·기기 성능 확인 없이 상용 출시 준비 완료로 표현하지 않는다.
- [x] 전체 현재 작업 트리를 독립 최종 리뷰1회 진행하고 Important 이상은 재현→수정→관련 전체 회귀로 검증한다. 완료 조건 감사 후에만 목표 완료. 커밋/푸시·다음 목표 없이 현재 변경과 증거를 보존한다.

## 빌드 없는 실행 예시

새 검사 작성 이후에만 사용한다. 실행 전 해당 ServeredMeridian 프로젝트가 사용자 Editor에 열려 있으면 강제 종료하거나 자동 저장하지 않는다. 설치 Unity 버전을 확인하고 별도 프로세스가 가능할 때 실행한다. 아래 METHOD를 Data/RunScene/Observe로 각각 바꾼다. `-quit`은 비동기 Play Mode 검사에 붙이지 않으며 각 검사 자체가 결과 기록 후 종료해야 한다.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Projects/Git/ServeredMeridian' -executeMethod GameScreen.Editor.PuzzleStabilityVerification.METHOD -logFile 'C:/Projects/Git/ServeredMeridian/Logs/Stage11/METHOD.log'
```

## 계획 자체 검토

목표1→Task1, 2~8→Task2, 9~10→Task3(결함 재현은Task2도 담당), 11~12→Task4. 기존 API 이름과 경로를 확인했다. 계획 작성 당시 신규였던 검증 진입점을 구현·실행했고 결과를 실행 기록에 남겼다. 다른 맵 풀 증가·background 결과·회전·실제 프레임·캐시 대기5개 Review Focus는 해당 작업의 검사에 연결했다. 작성 당시의 문서 제안에서 실제11단계 검증·완료 감사까지 진행했다. 다음 목표는 자동 시작하지 않는다.

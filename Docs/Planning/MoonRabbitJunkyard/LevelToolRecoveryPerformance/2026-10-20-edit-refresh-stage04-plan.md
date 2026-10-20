# 편집 성능 개선 4단계 — 튜토리얼·공유 적용·보드 조작

> 실행 담당: `superpowers:executing-plans`로 직접 진행한다. 하위 에이전트·커밋·푸시는 별도 승인 없이 사용하지 않는다.

**Goal:** 튜토리얼 수정·공유 적용의 중복 변환과 보드 조작의 전체 재생성을 줄이면서 편집 결과와 입력 상태를 보존한다.

**Architecture:** 1~3단계의 대상 문서 편집·버전 기반 표시 캐시를 이어 사용한다. 튜토리얼의 읽기·편집 수명과 보드의 내용·선택·배율 갱신을 구분하되, 새로운 UI 프레임워크나 전역 캐시는 도입하지 않는다.

**Tech Stack:** C#, Unity UI Toolkit, Newtonsoft JSON, UniTask, 기존 외부 테스트 harness.

**Spec:** [통합 계획](2026-10-20-edit-refresh-performance-plan.md), [3단계 검증](../../../Verification/MoonRabbitJunkyard/2026-10-20-level-tool-edit-refresh-stage03.md), [4단계 목표·실행문](../../../Goals/MoonRabbitJunkyard/LevelToolRecoveryPerformance/2026-10-20-edit-refresh-stage04-goal.md).

상태: 구현·검증 완료(2026-10-20). 기존 1~3단계는 완료 상태를 유지한다. [작업 기록](../../../Verification/MoonRabbitJunkyard/2026-10-20-level-tool-edit-refresh-stage04.md).

## 범위와 진행 단위

한 단계 안에서 **두 구현 묶음과 최종 검증**으로 진행한다. 튜토리얼과 공유 적용은 같은 그래프·패널 경로를 사용하므로 함께 처리하고, 보드는 독립적으로 검증한다. 세부 항목마다 별도 단계 문서를 만들지 않는다.

포함: 튜토리얼 필드 편집·강조 미리보기, 공유 사본 적용, 보드 선택·확대/축소·셀 배치/삭제.

제외: 붙여넣기 전체 복사, 저장 파이프라인, 대량 이력 구조 재설계, UI 디자인 개편, 기존 가로 화면 높이 문제. 이들은 현재 비용을 기록만 하고 이번 단계에서 확장하지 않는다.

## 확인된 근거와 가설

| 조작 | 3단계 동기 중앙값 | 이번 개선 방향 |
| --- | ---: | --- |
| 튜토리얼 수정 | 1,048.65ms | 반복 그래프 생성과 패널 재구성 축소 |
| 공유 적용 | 891.98ms | 적용 후 표시 갱신·공유 목록 조회 비용 축소 |
| 선택 / 줌 | 220.35 / 213.86ms | 셀·이미지 재생성 없이 선택 표시·크기 갱신 |
| 셀 배치 / 삭제 | 248.28 / 242.26ms | 내용 변경 때 필요한 표시만 갱신 |

위 수치는 기존 검사 환경의 잔여 비용이며 실기기 보장값이 아니다. 원인별 기여도는 구현 직전 계측으로 확정한다.

- `TutorialDraftEditing`은 편집마다 `session.Documents`를 이용해 그래프를 만들고 전후 데이터를 인코딩한다. 패널과 강조 미리보기에도 별도 그래프 생성 경로가 있다.
- `LevelToolBoardView.Refresh`는 셀을 다시 구성한다. 그림 키 계산도 `session.Documents`에서 프로젝트 리소스를 조회한다.
- 그래프 무조건 장기 보관은 수정 가능한 임시 오브젝트가 공유 사본·Undo 상태를 오염시킬 위험이 있다. 먼저 중복 조회 제거와 필요한 참조 범위 축소를 적용하고, 여전히 필요한 읽기 전용 결과만 명시적 수명으로 보관한다.

## 공통 제약

- 제품 빌드·커밋·푸시·HTML 갱신·코드로 씬/프리팹 편집 금지. 기존 사용자 변경 보존.
- 편집은 메모리와 dirty 상태에만 반영한다. 자동 저장·자동 복구 기록을 재도입하지 않는다.
- JSON/ZIP/MemoryPack 형식, 게임 규칙, 공유 적용 원자성, Undo/Redo 의미를 유지한다.
- 세션 내부의 수정 가능한 JSON을 외부에 노출하여 복사량을 줄이지 않는다.
- 새 의존성·asmdef·전역 서비스는 도입하지 않는다. 런타임 경로에 UnityEditor 의존성을 추가하지 않는다.
- 검사는 격리 사본에서 수행한다. 실제 기기·물리 IME·GPU 완료 미검증을 명시한다.

## 작업 1 — 튜토리얼·공유 적용

주요 파일:

- `Assets/Scripts/Features/LevelAuthoring/Runtime/TutorialDraftEditing.cs`
- `Assets/Scripts/Features/LevelAuthoring/Runtime/AuthoringObjectGraph.cs`
- `Assets/Scripts/Features/LevelTool/Runtime/Presentation/LevelToolTutorialPanel.cs` 및 `.Sharing.cs`
- `Assets/Scripts/Features/LevelTool/Runtime/LevelToolScreen.TutorialSharing.cs`, `.TutorialPreview.cs`, `.Refresh.cs`, `LevelToolScreen.cs`

계약: 기존 `EditLevel`, `EditFlow`, `EditFlowSteps`와 공유 사본 적용의 외부 동작을 유지한다. 캐시는 세션 식별자·문서 버전·공유 사본 상태를 반영한다. 의존 문서를 줄이는 경우 카탈로그·바인딩·샘플·리소스 등 실제 참조를 빠뜨리지 않는다.

- [x] 고정 입력으로 튜토리얼 편집과 공유 적용을 재측정하고 그래프 생성·인코딩·패널·미리보기 비용을 분리한다. 테스트 준비 시간을 편집 시간에 섞지 않는다.
- [x] 읽기 목록 조회 때문에 전체 문서를 복사하는 경로와 같은 갱신 안의 중복 그래프 구성을 줄인다. 도메인 검증을 생략하거나 JSON 필드를 임의 변경하는 우회는 하지 않는다.
- [x] 일반 필드 편집과 단계 추가/삭제/재정렬을 구분한다. 구조가 그대로면 입력 컨트롤과 포커스를 유지하고, 구조 변경은 필요한 패널만 재구성한다.
- [x] 강조 계산과 강조 영역 크기 변경을 분리한다. 이름 변경·단순 배율 변경이 불필요하게 그래프를 다시 만들지 않도록 한다.
- [x] 공유 사본 적용/취소, 미확정 입력 확정, 부모 변경 충돌, 재진입, 연결/독립 복사, Undo/Redo, 동일 ID 재로드를 검사한다.
- [x] 공유 적용 후 메뉴로 돌아오는 기존 동작을 유지하고 사본을 다시 열어 최신 값을 확인한다. 숨긴 패널·공급·샘플·바인딩도 최신 상태인지 확인한다.
- [x] 캐시 교체·세션 종료 때 그래프가 소유한 임시 오브젝트와 구독이 해제되는지 확인한다.

## 작업 2 — 보드 선택·배율·내용 갱신

주요 파일:

- `Assets/Scripts/Features/LevelTool/Runtime/Presentation/LevelToolBoardView.cs`, `.Artwork.cs` 및 기존 오버레이 partial
- `Assets/Scripts/Features/LevelTool/Runtime/LevelToolScreen.cs`, `.Refresh.cs`, `.Layout.cs`, `.TutorialPreview.cs`

계약: 선택만 변경하면 선택 스타일만, 배율만 변경하면 셀·그림·오버레이의 기하만 갱신한다. 데이터 변경은 내용 버전으로 식별한다. 세션·카탈로그 교체와 구조 변경에는 필요한 전체 갱신을 허용한다.

- [x] 선택·줌에서 셀/이미지 인스턴스 유지와 문서 버전 불변을 검사하는 회귀 검사를 먼저 추가한다.
- [x] 81개 셀의 수명을 유지하고 선택 표시와 크기 갱신을 내용 재구성과 분리한다. 셀 배치/삭제는 관련 표시를 갱신하며, 영향 셀을 신뢰할 수 없는 경우 내용 갱신 경로를 유지한다.
- [x] 그림 키의 프로젝트·리소스 조회에 전체 문서 복사를 반복하지 않는다. 정의·비주얼 변경 시 정상 무효화한다.
- [x] 비동기 이미지 로딩 중 연속 줌·선택·레벨 전환을 검사한다. 오래된 요청이 새 화면에 이미지를 추가하지 않고 최종 배율을 적용하도록 revision·취소·소유권을 유지한다.
- [x] 2×2 장애물 위치/크기, 공급 표시, 흐름/연결선, 튜토리얼 가림막의 순서·좌표를 확인한다.
- [x] Ctrl+휠 확대/축소, 자동 맞춤, 터치 선택/롱터치, 영역 선택, 패닝의 좌표 판정이 시각 표시와 일치하는지 검사한다.

## 작업 3 — 통합 측정과 완료 판정

검사는 `Tests/Editor/Features/LevelAuthoring/`의 기존 검사에 추가한다. 주 대상은 `LevelToolManualSavePerformanceVerification.Extended.cs`, `LevelToolRefreshPerformanceVerification.cs`, `AuthoringTargetedEditVerification.cs`, `LevelToolMobileVerification.cs`다. 기존 검사에서 의도한 전체 재생성을 가정했다면 새 동작 계약에 맞게 검사를 수정하되 검사 범위를 줄이지 않는다.

- [x] 수정 전후 같은 입력·같은 준비 조건에서 정상 측정 3회를 기록한다. 튜토리얼/공유/선택/줌/배치/삭제를 각각 구분한다.
- [x] 이름 편집 중앙값 100ms 이하와 대상 문서 복사·참조 격리·dirty·Undo 검사를 유지한다.
- [x] 100회 반복을 10회씩 나눠 응답·관리 힙·working set·UI/임시 객체 수·이력 수를 기록하고 세션 교체 후 해제를 확인한다.
- [x] 1280×800·360×780·412×915·800×1280의 입력·스크롤·포커스와 숨김 패널 재진입을 검사한다.
- [x] 관련 컴파일 및 JSON/ZIP/MemoryPack/게임 시험 회귀를 변경 영향에 맞게 수행한다. 제품 빌드를 검증 대신 사용하지 않는다.
- [x] 목표별 달성/미달, 최대 지연, 미검증, 보호 파일 diff와 검사 연결 해제 여부를 결과 문서에 기록한다.

기존 실행 진입점은 실행 전 선언을 확인하고 순차 호출한다:

```powershell
& Tools/Testing/ProjectTests.ps1 -Action Compile
& Tools/Testing/ProjectTests.ps1 -Action Run -Method LevelAuthoring.Editor.LevelToolManualSavePerformanceVerification.RunRepresentative
& Tools/Testing/ProjectTests.ps1 -Action Run -Method LevelAuthoring.Editor.LevelToolManualSavePerformanceVerification.RunFocusedRefresh
& Tools/Testing/ProjectTests.ps1 -Action Run -Method LevelAuthoring.Editor.LevelToolRefreshPerformanceVerification.Run
& Tools/Testing/ProjectTests.ps1 -Action Run -Method LevelAuthoring.Editor.LevelToolMobileVerification.RunRefreshViewports
& Tools/Testing/ProjectTests.ps1 -Action Status
```

결과 파일: `Docs/Verification/MoonRabbitJunkyard/<실제 작업일>-level-tool-edit-refresh-stage04.md`.

## 검토 중점

1. 공유 사본과 부모 캐시가 섞이는 문제 — 작업 1의 적용/취소/충돌 검사.
2. 미확정 입력·포커스 소실 — 작업 1 및 작업 3의 입력 유지 검사.
3. Undo/동일 ID 재로드 후 오래된 표시 — 두 구현 묶음에서 버전 무효화 검사.
4. 늦은 그림 로딩이 배율·레벨 전환을 덮어쓰는 문제 — 작업 2의 연속 조작 검사.
5. 작업을 다음 프레임으로 넘겨 측정만 빨라지는 문제 — 작업 3에서 동기 시간과 표시 준비 관측값을 함께 비교.

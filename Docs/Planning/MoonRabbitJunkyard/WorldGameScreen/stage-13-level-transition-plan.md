# 13단계 — 승리 후 다음 레벨 연결 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 선택한 레벨의 승리 후 같은 게임 화면에서 다음 번호의 MemoryPack 레벨로 이동한다.

**Architecture:** 기존 PuzzleGameSession과 기능별 uGUI/월드 프리팹을 유지한다. 다음 레벨의 데이터·시작 보드·필요 아틀라스를 먼저 준비한 뒤 세션 상태를 교체한다. 실패하면 현재 승리 화면을 유지한다. 에디터 Asset 실행은 기존 선택 맵의 테스트 경로로 유지한다.

**Tech Stack:** Unity 6000.3.10f1, SpriteRenderer 월드 보드, uGUI, UniTask, Addressables, 50레벨 MemoryPack.

**Spec:** [목표·동작 결정·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-13-level-transition-goal.md)

상태: 2026-10-02 계획 작성, 구현 미착수. 이번 문서 작성은 실행 승인이 아니다. 다음 기능의 제안이며 저장·해금 시스템은 포함하지 않는다.

## 시작 조건

12단계는 단일 리뷰의 Game View 복원 오류를 수정하고17개 회귀·추가InsetUI67PASS·10개 완료 조건을 감사했다. [완료 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-12-progress.md)을 기준으로 착수 때 현재 코드와 목표 상태를 확인한다. 완료한 복원 수정이나 유효한 화면/움직임 검사를 이유 없이 반복하지 않는다.

12단계가 실제 미완료이거나 회귀가 재현되면 해당 단계에서 먼저 처리한다. 다른 활성 목표가 남으면 새 목표를 만들지 않는다. 과거 PASS/Exit0만으로 현재 완료를 단정하지 않는다.

## Global Constraints

- ServeredMeridian만 대상으로 한다. 사용자 Unity·원본 레벨/씬·무관 dirty·GUID를 보존한다.
- 플레이어 및 Addressables 콘텐츠 빌드, 커밋·푸시, 사용자 Editor 강제 종료·자동 씬 저장을 하지 않는다.
- 기존 9×9·블록/2×2/로켓 크기와 중심·스와이프 order·중첩 칸 피해·낙하/공급 시간 /1.44·동시 낙하·신규 대각선/상단 우선을 유지한다.
- 최신 드론 상승/대기/돌파·조합 반지름1~4칸·표적 예고 없음, 미션·라스트팡, 14종 효과음·8음성·.06초 제한 및 background/pause 결과음 복귀1회·중복0을 유지한다.
- 교환/섞기 256×256 아이콘과 GUID, 필요한 종류만 아틀라스 로드/풀 재사용, 50레벨 MemoryPack 포맷을 유지한다. 레벨 Asset을 런타임 직접 참조하지 않는다.
- 새 메인 화면·전체 레벨 목록·진행 저장/해금·별/보상·광고·과금·진동·BGM·패키지는 추가하지 않는다.
- Task는 직접 순서대로 구현한다. 최종 독립 리뷰는 한 번만 수행한다. 실행 후 다음 목표는 자동 시작하지 않는다.

## 확인한 기존 코드와 변경 위치

| 파일 | 현재 동작 / 계획한 책임 |
| --- | --- |
| `Assets/Scripts/Features/GameScreen/Runtime/Session/PuzzleGameSession.cs` | InitializeCoreAsync는 이미 시작한 세션의 재초기화를 거부한다. started를 임의 초기화하거나 InitializeAsync를 두 번 호출하지 않는다. |
| `Runtime/Session/PuzzleGameSession.Controls.cs` | RestartAsync는 현재 initialBytes·번호·시드로 재시작한다. 다음 레벨 성공 시 이 기준을 새 레벨로 갱신한다. |
| `Runtime/Session/PuzzleGameSession.LevelTransition.cs` (신규) | 다음 레벨 준비·전환 가드·정리·취소를 담당하는 partial. 전환 때문에 원본 규칙 실행기를 바꾸지 않는다. |
| `Assets/Scripts/Features/Levels/Data/LevelPackLoader.cs`, `LevelPackCodec.cs` | LoadAsync(number), Address(number), LevelsPerPack=50을 재사용한다. 번들 핸들은 loader가 finally에서 해제한다. |
| `Runtime/UI/PuzzleResultView.cs`, `PuzzleScreenView.cs` | 현재 결과 화면은 Retry만 연결한다. 다음 레벨 버튼·전환 상태/오류 안내를 추가한다. |
| `Assets/Prefabs/UI/Puzzle/PuzzleResultPopup.prefab`, `PuzzleScreen.prefab` | 기존 레이아웃 안에서 버튼 연결만 추가한다. Game 프리팹과 UI 프리팹의 구분을 유지한다. |
| `Assets/Scripts/Features/GameScreen/Editor/Launch/PuzzleEditorLaunchRequest.cs`, `PuzzleEditorLauncher.cs` | 현재 Asset/MemoryPack 사본 전달 경로를 유지한다. 실행 소스만 런타임에 전달하며 AssetDatabase 코드는 Editor에 남긴다. |
| `Assets/Scripts/Features/GameScreen/Editor/Tests/PuzzleLevelTransitionVerification.cs` (신규) | Data와 RunScene 진입점, 실제 세션/UI 전환·실패·경계·정리를 검사한다. |

Runtime 상대 경로의 기준은 `Assets/Scripts/Features/GameScreen/`이다. 신규 Unity 파일은 .meta를 함께 생성한다. 새 전역 Manager·asmdef·범용 로더 추상화는 만들지 않는다.

## Review Focus

1. 다음 레벨 버튼을 연속 누름: 로드·전환·시작음이 각각 한 번만 발생한다(Task 1~2).
2. 50→51 구간 또는 다음 번호 누락/파일 오류: 주소를 올바르게 선택하고 실패 시 기존 승리/재시작 기준을 보존한다(Task 1).
3. 로드 중 취소·씬 종료·background: 늦은 완료가 파괴된 화면을 갱신하지 않고 후보 리소스를 반환한다(Task 1/4).
4. 이전 판의 드론·수집·선택·결과음이 다음 판에 남음: 전환 성공 시 표시와 예약을 정리하고 새 판의 자원만 유지한다(Task 1/4).
5. 미저장 Asset을 선택한 에디터 테스트: 재시작은 해당 사본을 유지하고 다음 레벨을 임의 MemoryPack으로 바꾸지 않는다(Task 3).

## Task 1 — 다음 레벨 준비와 세션 전환

**Interfaces:** `public bool IsChangingLevel { get; private set; }`, `public bool CanAdvanceLevel`, `public UniTask<bool> AdvanceLevelAsync(CancellationToken token)`을 세션에 추가한다. CanAdvanceLevel은 MemoryPack 실행이고 ResultReady이며 Outcome.Kind가 Won인 때만 참이다. 진행 중에는 모든 보드/아이템/다시하기/추가 전환 입력을 막는다. 반환값 true는 새 레벨의 준비와 상태 교체 성공을 뜻하며 시작 피드백 중 입력 잠금은 기존 방식대로 유지한다.

- [ ] 현재 코드·원본/프리팹·이미지·timing 해시와 입력 소스 전달을 기록하고 12단계 완료 근거를 감사한다.
- [ ] 다음 레벨 성공/중복 요청/누락/취소/50→51 실패 검사를 먼저 확보한다. 재시작할 번호·시드·bytes가 실패 시 바뀌지 않는지도 검사한다.
- [ ] 새 partial에서 number+1의 정의·StartingBoardSearch·PuzzleArtwork를 로컬 후보로 준비한다. 현재 PrepareAsync가 즉시 세션 필드를 바꾸는 점을 고려해 준비 도중 기존 승리 상태를 훼손하지 않는다. 새 로딩 경로는 기존 loader와 검색 규칙을 재사용한다.
- [ ] 성공할 때만 이전 진행/선택/연출/음성 예약을 정리하고 executor·artwork·initialBytes·번호를 교체한다. 이전 artwork는 새 보드 표시가 적용된 뒤 반환한다. 시드는 기존 세션 시드를 유지한다. 실패·취소 후보는 반환하며 전환 가드는 finally에서 해제한다.
- [ ] 단순 로딩 실패는 기존 세션의 Fail()로 승리 상태를 파괴하지 않는다. 오류를 UI에 전달하고 기존 Retry를 유지한다. 소멸 취소는 오류 팝업으로 노출하지 않는다.
- [ ] Data 검사에서 성공/실패 전후 전체 상태·레벨 번호·원본 불변·후보 정리·한 번 전환을 검증한다. 준비 데이터의 의미 검증은 기존 실행기를 활용한다.

## Task 2 — 승리 팝업의 다음 레벨 버튼

**Interfaces:** PuzzleResultView는 기존 Retry 연결을 보존하며 다음 레벨 action과 표시/활성 상태를 받을 수 있게 확장한다. PuzzleScreenView는 Task 1의 AdvanceLevelAsync만 호출한다. 실패/진행 중/Asset 실행에서는 동작 불가 상태와 안내를 명확히 표시한다.

- [ ] 실제 EventSystem 버튼 경로에서 승리 전/실패/승리 후/중복 클릭의 실패 검사를 확보한다.
- [ ] 기존 팝업에 `다음 레벨` 버튼을 추가하고 승리·MemoryPack에서만 표시한다. 전환 중에는 버튼과 Retry를 비활성화하고 `다음 레벨 준비 중`을 표시한다.
- [ ] 다음 번호가 없으면 현재 승리 화면에 `다음 레벨을 불러올 수 없습니다`와 진단 메시지를 표시하고 Retry를 유지한다. 누락만으로 마지막 레벨이라고 단정하거나 번호를 건너뛰지 않는다.
- [ ] 새 레벨 성공 시 이전 팝업·shownOutcome·선택 표시를 정리하고 HUD 번호/이동/미션을 새 상태로 갱신한다. 회전 중에도 로드/전환은 한 번이다.
- [ ] RunScene에서 실제 승리→클릭→새 보드→한 행동→Retry 경로와 네 해상도 팝업을 확인한다. 기존 Retry 전용 연결과 선택 order도 회귀 검사한다.

## Task 3 — 에디터 실행 소스와 선택 맵 보존

**Interfaces:** 세션에 `public void SetLevelAdvanceEnabled(bool enabled)`를 추가한다. 씬 기본값은 MemoryPack 이동 허용이다. Editor launcher는 InitializeAsync 전에 Asset 모드 false, MemoryPack 모드 true를 전달한다. Editor 요청의 소스 필드와 SessionState 저장/정리는 기존 요청 수명에 맞춰 추가한다.

- [ ] 미저장 Asset 사본·MemoryPack 선택 번호의 전달과 Play Mode 종료 복귀를 먼저 검사한다.
- [ ] Asset 실행에는 다음 레벨 버튼을 표시하지 않고 결과 안내에 `다음 레벨은 MemoryPack 모드에서 이어서 플레이할 수 있습니다`를 표시한다. Asset 재시작은 초기 선택 사본을 그대로 사용한다.
- [ ] MemoryPack 실행은 선택 번호에서 시작해 n+1로 전환한다. 파일 읽기는 기존 에디터/런타임 경계를 유지하고 AssetDatabase를 Runtime에 추가하지 않는다.
- [ ] 실행 종료/취소에서 신규 소스 SessionState도 제거한다. 원본 저장·팩 갱신·Addressables 빌드를 자동 실행하지 않는다.
- [ ] 기존 EditorLaunch Data/Lifecycle 검사와 새 RunScene으로 미저장 사본 보존, 소스 구분, 선택 맵·시드 정확성, 돌아오기 및 원본 해시를 확인한다.

## Task 4 — 전환 수명·회귀·완료 감사

- [ ] 준비 취소·씬 종료·background/pause·회전·로드 실패 재시도와 서로 다른 장애물이 있는 맵 전환을 실제 Play Mode에서 검사한다. 늦은 완료·음성/풀/아틀라스/선택 잔류를 확인한다.
- [ ] 50→51 경계는 원본 레벨/팩 대신 검사 소유 데이터와 기존 Codec의 주소를 사용한다. 실제 기존 번들이 제공되지 않으면 그 경로를 미검증으로 기록하며 빌드로 해결하지 않는다.
- [ ] 새 Data/RunScene과 변경 영향의 기존 UI·교환·낙하·파워·미션·효과음·background 결과음·EditorLaunch 검사를 실행한다. 결과 파일과 실제 종료 코드 및 정리 로그를 함께 감사한다.
- [ ] 독립 최종 리뷰 한 번을 수행하고 Important 이상을 재현→최소 수정→관련 회귀로 처리한다. 리뷰에서 판단할 수 없는 청취/실기기 항목은 한계로 남긴다.
- [ ] `Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-13-progress.md`와 `Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-13-level-transition-usage.md`에 조건별 증거·입력 모드 차이·오류 복구·미검증을 기록하고 목차/로드맵을 갱신한다.
- [ ] 완료 조건을 전부 감사한 뒤에만 목표를 완료한다. 빌드·커밋·푸시·다음 목표 착수는 하지 않는다.

## 실행 검증 계약

신규 진입점은 `GameScreen.Editor.PuzzleLevelTransitionVerification.Data`, `.RunScene`이다. 구현 후 설치된 Unity 경로와 사용자 Editor 상태를 확인해 소유한 검사 프로세스만 사용한다. PASS>0·FAIL0·실제 Exit0 외에 필수 cleanup 오류가 없어야 한다. 촬영은 저장된 파일·픽셀과 실제 화면을 확인한다. 플레이어/콘텐츠 빌드 호출이 포함된 검사 진입점은 제외한다. 현재 문서 작성에서는 Unity·검사·빌드를 실행하지 않는다.

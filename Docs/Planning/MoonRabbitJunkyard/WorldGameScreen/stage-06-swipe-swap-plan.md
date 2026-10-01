# 6단계 스와이프·교환 연출 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:executing-plans`로 아래 네 작업을 순서대로 직접 실행한다. 체크박스는 실제 검증 후 갱신한다.

**Goal:** 손을 떼기 전 스와이프 확정, 점유자 교환 애니메이션, 무효 교환 복귀를 기존 게임 규칙과 연결한다.

**Architecture:** 규칙은 BoardActionExecutor가 소유한다. 세션이 재생 완료까지 다음 연쇄와 입력을 차단하고, 월드 표시가 점유자만 이동한다. 규칙 결과를 즉시 계산하는 기존 계약을 유지하되 화면 반영을 지연한다. 영속 블록 ID나 낙하 경로 재생은 이번에 도입하지 않는다.

**Tech Stack:** 기존 Unity, C#, Input System, UniTask, SpriteRenderer. 새 의존성 없음.

**Spec:** [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-06-swipe-swap-goal.md). 상태: 구현·Editor 검증 완료 (2026-10-01).

## Global Constraints

- ServeredMeridian만 사용하고 기존 변경·dirty 데이터·GUID를 보존한다. 자동 커밋·푸시하지 않는다.
- **플레이어/Addressables 콘텐츠 빌드 및 이를 내부 호출하는 검사는 실행하지 않는다.** 일반 Editor 컴파일·Play Mode 검사만 한다. 검사 진입점의 부작용을 먼저 읽는다.
- 같은 프로젝트의 Editor가 열려 있으면 두 번째 Unity를 띄우거나 기존 Editor를 강제 종료하지 않는다. 테스트 후 임시 객체·입력 장치·사용자 설정을 복원한다.
- 0.25칸 확정, 0.24칸 미리보기 상한, 교환 0.15초·복귀 0.15초 초기값을 사용한다. 속도는 직렬화된 최소 설정으로 조절하되 사용자 설정 화면을 추가하지 않는다.
- 데이터·규칙·아틀라스·MemoryPack·프리팹 분류를 유지한다. 7단계 낙하·채움, 8단계 파워 효과를 앞당겨 구현하지 않는다.

## Review Focus

1. 터치 확정 뒤 Ended/합성 마우스가 또 실행됨 → 작업 3의 실제 Input System 이벤트 검사.
2. 실행기의 매칭 후 스프라이트로 교환하여 블록이 미리 사라짐 → 작업 1·2의 계산 전 표시 및 중간 프레임 검사.
3. 벽·덮개를 넘거나 고철 본체가 제자리에 남음 → 작업 1·2의 점유자/거절 사유별 검사.
4. 규칙 Ready이지만 실패 복귀 중 다시 입력됨 → 작업 2·3의 재생 잠금 검사.
5. 재시작·회전·정지 후 위치/잠금/이전 콜백 잔류 → 작업 4의 시간·수명 검사.

## 파일과 계약

기존 파일은 Assets/Scripts/Features/GameScreen 아래 상대 경로다.

| 파일 | 책임과 변경 |
| --- | --- |
| Runtime/Input/PuzzleBoardInput.cs | 기존 BeginPointer/EndPointer 사이에 UpdatePointer(int id, Vector2 position)를 추가한다. Moved와 누른 마우스 위치를 전달하고 확정된 포인터는 놓을 때까지 소비한다. |
| Runtime/World/PuzzleWorldBoard.cs, PuzzleCellView.cs | 좌표의 점유자 렌더러를 찾고 미리보기/교환 변위를 적용·초기화한다. 별도 장애물 본체와 일반 content를 구분한다. 바닥/덮개는 고정한다. |
| Runtime/World/PuzzleBoardSwapPlayback.cs (신규) | 두 점유자 렌더러·원래 위치를 소유하는 작은 재생 객체. Begin, Tick(float deltaTime), Reset과 IsPlaying으로 성공 이동 또는 왕복을 관리한다. UniTask fire-and-forget 이동을 블록마다 만들지 않는다. |
| Runtime/Session/PuzzleGameSession.cs, PuzzleGameSession.Controls.cs | TrySwap의 bool=규칙 적용 여부 계약을 유지한다. 입력/연쇄/Changed의 재생 장벽, 일시정지·재시작·종료 정리를 담당한다. 필요하면 같은 폴더의 Presentation partial로 분리한다. |
| Editor/Tests/PuzzleBoardInputVerification.cs | 기존 입력 검사를 보존하고 이동 중 확정·소비·UI 차단 검사를 추가한다. |
| Editor/Tests/PuzzleSwapAnimationVerification.cs (신규) | 제어한 deltaTime으로 중간 위치, 종료, pause/reset, 결과 동일성을 검사한다. 새 테스트 프레임워크를 추가하지 않는다. |

Begin은 월드 표시에서 얻은 두 렌더러와 원래 로컬 위치, 성공/왕복 여부를 받는다. Tick은 완료 여부를 반환하고 규칙에 접근하지 않는다. Reset은 위치와 그리기 순서를 복원한다. 구체 인자 타입은 기존 SpriteRenderer/Vector3를 사용한다. 재생 중 보드 전체 Draw를 호출하지 않는다. 기존 셀 구조로 점유자만 움직일 수 있으면 새 프리팹을 만들지 않는다.

## 작업 1 — 점유자 이동 표시

- [x] 기존 월드 프리팹 계층과 장애물 본체 목록을 확인한다. 원본 dirty 상태를 보존하고 이동 가능한 고철·회수 부품 fixture를 기존 레벨과 겹치지 않게 준비한다.
- [x] 중간 프레임에서 점유자만 움직이고 바닥·덮개·2×2 본체는 고정되는 검사를 먼저 추가한다. 기존 코드의 실패를 확인한다.
- [x] 월드 좌표→점유자 렌더러 조회와 위치 복원을 구현한다. 별도 본체는 그리기 때 만든 좌표 매핑을 재사용하며 프레임마다 장면을 검색하지 않는다.
- [x] PuzzleBoardSwapPlayback으로 0.15초 성공 이동/왕복을 구현한다. 미리보기 시작 위치에서 이어지고 완료 시 정확한 좌표·sorting order를 복원한다.
- [x] 시작/중간/완료/Reset 검사와 관련 월드 표시 검사를 실행한다. Expected: 레이어 불변, 잔류 변위 없음, 매번 새 GameObject 생성 없음.

## 작업 2 — 규칙 결과와 재생 순서

**Interfaces:** ActionQuery.Swap의 ActionReason, executor.Swap의 BoardActionResult를 소비한다. TrySwap의 반환 의미와 기존 데이터 모델을 변경하지 않는다. 세션에 읽기 전용 IsPresenting을 두고 CanAcceptInput 및 Advance가 이를 확인한다.

- [x] 성공 직후 첫 연쇄가 재생 완료 전에 진행되지 않는 검사와 NoNewMatch 결과 불변 검사를 추가하고 기존 실패를 확인한다.
- [x] TrySwap에서 계산 전 점유자 표시를 확보하고 실행기를 한 번만 호출한다. 성공은 원래 스프라이트로 교환 재생 후 Draw/Changed, 그다음 프레임부터 기존 Advance를 허용한다. 첫 매칭 후 결과를 교환 시작 그림으로 사용하지 않는다.
- [x] 실패 사유는 ActionQuery의 enum으로 구분한다. NoNewMatch만 왕복하고 벽·고정/덮개·자석 제한 등은 작은 거절 반응을 재생한다. 거절 중에도 입력 잠금을 적용한다. 오류/거절 상태를 성공 이동으로 표시하지 않는다.
- [x] IsPaused일 때 재생 Tick을 멈춘다. 재생 도중 Changed가 발생해도 HUD가 결과를 조기 공개하지 않도록 표시 갱신 경계를 확인한다. 규칙 상태를 위조하거나 되돌리지 않는다.
- [x] 일반 교환·파워 교환/조합·고철·회수 부품·무효 교환을 같은 시드의 직접 실행기 결과와 대조한다. Expected: 이동 수/난수/보드/미션 동일, 표시 완료 전 연쇄 없음, 완료 후 기존 연쇄 정상.

## 작업 3 — 스와이프 확정과 입력 소비

**Interfaces:** UpdatePointer는 드래그 미리보기와 0.25칸 확정을 담당한다. BeginPointer는 소유 포인터 하나만 등록한다. EndPointer는 미확정 제스처만 마지막 위치 평가 또는 기존 탭 경로로 보낸다. CancelGesture는 미확정 미리보기를 복원하되 진행 중 확정 행동을 롤백하지 않는다.

- [x] 4방향·0.24/0.25 경계·대각선 동률·End-only 이동·놓지 않은 상태의 확정 검사를 추가하고 현재 실패를 확인한다.
- [x] touch Moved 및 mouse held를 UpdatePointer에 연결한다. 보드 로컬 좌표로 판정하고 직교 한 칸만 요청한다. UI 위/카메라 밖으로 나간 미확정 제스처는 취소한다.
- [x] CanAcceptInput이 false가 되었다고 소비한 포인터 기록을 조기에 잊지 않도록 수명 처리를 분리한다. 확정 후 놓을 때까지 재확정/탭 발동을 막는다. 같은 프레임의 touch와 합성 mouse도 한 번만 처리한다.
- [x] 기존 두 탭, 파워 탭, 아이템 대상 선택은 유지한다. 아이템 모드에서는 새 스와이프 교환을 실행하지 않는다. SelectionChanged와 선택 표시 정리도 확인한다.
- [x] 실제 Input System 큐로 마우스/터치·두 번째 손가락·UI 진입·연타를 재생한다. Expected: 한 제스처 한 명령, UI 입력 교환 0, 아이템 중 교환 0. 직접 private 메서드 호출만으로 입력 검증을 끝내지 않는다.

## 작업 4 — 수명·회귀·화면 검증과 인계

- [x] 확정 전 취소, 확정 후 회전/포커스 상실, pause/resume, 재생 중 다시하기/씬 종료를 검사한다. 확정된 규칙 결과는 유지하고 새 세션에는 이전 재생 결과가 적용되지 않아야 한다.
- [x] 다시하기 5회 후 표시 객체·세션 자원·이벤트 구독·입력 잠금이 누적되지 않는지 확인한다. 임시 시험 장치·설정은 finally에서 복원한다.
- [x] 관련 기존 입력/게임플레이/UI/씬 검사를 검토해 빌드 호출 없는 진입점만 실행한다. 기존 즉시 표시 가정은 재생 완료를 기다리도록 바꾸되 규칙 결과 assertion은 약화하지 않는다.
- [x] 가로 1280×720, 세로 450×800에서 실제 Play Mode 유효/무효 교환의 시작·중간·끝을 연속 캡처 또는 영상으로 남겨 움직임, 레이어, UI 충돌을 확인한다. 이미지 단독 성공을 애니메이션 검증으로 취급하지 않는다.
- [x] Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-06-progress.md와 Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-06-swipe-swap-usage.md에 결과·환경·조작·미검증 항목을 기록한다. Logs/PuzzleSwipeSwapVerification/에 증거를 둔다.
- [x] diff/링크/meta·데이터·dirty·설정 보존을 확인한다. 목표 완료 조건별 증거를 대조하고 충족한 항목만 체크한다. 빌드와 실기기 검증은 수행했다고 표시하지 않는다.

## 계획 자체 검토

입력 요구는 작업 3, 점유자 범위는 작업 1, 결과·거절·시간 경계는 작업 2, 취소·회귀·증거는 작업 4에 연결했다. 네 작업은 같은 입력/세션/월드 계약을 공유하므로 순서대로 직접 실행했다. 낙하 재생용 영속 ID나 범용 타임라인을 먼저 만들지 않고 7단계에서 SettlementRecord의 Source/Target/Batch를 활용한다. 실행 결과는 [검증 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-06-progress.md)을 따른다. 빌드는 실행하지 않았다.

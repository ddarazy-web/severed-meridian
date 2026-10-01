# 7단계 제거·낙하·채움 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:executing-plans`로 아래 네 작업을 순서대로 직접 실행한다. 체크박스는 실제 검증 뒤 갱신한다.

**Goal:** 규칙 결과를 유지하며 제거·이동·공급·착지를 보여주고 완료 뒤 다음 연쇄를 진행한다.

**Architecture:** 실행기가 규칙을 한 번 계산하고, 세션은 계산 전 표시와 결과 기록을 이용해 재생을 예약한다. 월드 표시가 단계 동안의 점유자 사본을 소유한다. 최종 Draw는 재생 완료 때만 수행하며 기존 IsPresenting 경계를 확장한다.

**Tech Stack:** 기존 Unity/C#, SpriteRenderer, Input System, UniTask. 새 의존성 없음.

**Spec:** [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-goal.md). 상태: 구현·Editor 검증 완료 (2026-10-01).

## Global Constraints

- ServeredMeridian만 사용한다. 9×9, 기존 이미지 보정, 원본/dirty/GUID, 50레벨 MemoryPack과 아틀라스를 유지한다.
- 플레이어·Addressables 콘텐츠 빌드 및 내부 빌드 호출 검사 금지. Editor 컴파일·Play Mode만 허용하며 실행 진입점을 먼저 읽는다.
- 열린 사용자 Editor를 강제 종료하거나 같은 프로젝트 Editor를 중복 실행하지 않는다. 미저장 씬·레벨을 자동 저장하지 않는다. 커밋·푸시 금지.
- 제거 0.12초, 이동 구간 0.12~0.24초, 공급 0.16초, 최종 착지 0.06초를 초기값으로 한다. 시간은 규칙 결과에 영향을 주지 않는다.
- 상세 파워 효과·소리·진동·미션 수집 비행은 후속 단계다. 편집기 배치 보드에 새 애니메이션을 넣지 않는다.

## Review Focus

1. 실행기가 이미 제거한 블록을 교환 직후 Draw하여 사라짐 → 작업 1·3에서 계산 전 표시/교환 도착 시점 검사.
2. 동일 Batch 출발·도착 충돌 또는 여러 Batch에서 같은 블록 중복 생성 → 작업 2의 동시 이동과 좌표 소유권 검사.
3. Supply/Portal/회수를 일반 낙하로 처리해 순간이동·재등장 → 작업 2의 Kind별 경로와 회수 결과 검사.
4. 규칙은 끝났지만 재생 중인 상태에서 다음 연쇄·승패가 노출 → 작업 3의 입력·HUD·결과 장벽 검사.
5. 재생 Reset이 최근 로켓 축척·중앙 보정까지 지움 → 작업 1·4의 원래 변환/색 복원과 수명 검사.

## 파일과 계약

아래 경로의 앞부분은 `Assets/Scripts/Features/`이다. 신규 파일명은 계획이며 실제 구조에 더 작은 책임 단위가 이미 있으면 재사용한다.

| 파일 | 책임 |
| --- | --- |
| GameScreen/Runtime/World/PuzzleWorldBoard.cs, PuzzleCellView.cs | 재생 시작 시 점유자 표시 캡처, 임시 이동 표시 소유·반환, 완료 시 실제 상태 Draw. 바닥/덮개는 고정 |
| GameScreen/Runtime/World/PuzzleBoardSettlementPlayback.cs (신규) | 기록의 Batch·Kind별 이동 및 공급, Tick/Reset, 최종 착지 |
| GameScreen/Runtime/World/PuzzleBoardRemovalPlayback.cs (신규) | 제거/교체 대상의 짧은 축소·알파 재생. 원래 scale/color/position 보존 |
| GameScreen/Runtime/Session/PuzzleGameSession.cs, PuzzleGameSession.Presentation.cs, PuzzleGameSession.Controls.cs | 규칙 실행 전 캡처, 결과 연결, 재생 순서·입력·UI·수명 경계 |
| GameScreen/Editor/Tests/PuzzleSettlementAnimationVerification.cs (신규) | 제어한 시간, 규칙 결과 대조, 실제 씬·화면·수명 검사 |
| GameScreen/Editor/Tests/PuzzleSwapAnimationVerification.cs 및 Scene/Contents partial | 기존 교환 보존과 새 완료 시점 연결 검사 |

참조할 규칙 원본: `BoardFlow/Runtime/SettlementResolution.cs`, `PuzzlePlay/Runtime/Actions/BoardActionExecutor.cs`, `BoardActionExecutor.Cascade.cs`. 저장 형식이나 규칙을 연출에 맞춰 바꾸지 않는다.

재생 인터페이스는 기존 교환과 같은 `Begin(...)`, `Tick(float deltaTime)`, `Reset()`, `IsPlaying` 형태로 한다. Begin의 자료는 계산 전 표시 스냅샷, 계산 후 상태, 이번 단계의 Changes/Effects/Settlement다. 정확한 인자 구조는 작업 1에서 기존 타입을 재사용하여 최소화한다. 스냅샷은 스프라이트·좌표·변환·색·렌더 순서 등 표시에 필요한 정보만 보유한다. 원본 상태를 역으로 변경하거나 영속 ID를 만들지 않는다.

## 작업 1 — 제거 표시와 단계 스냅샷

- [x] TrySwap/TryActivate/아이템/AdvanceCascade 호출부와 Changes·Effects 의미를 읽고, 실행 전 모습이 사라지는 지점을 확인한다. 현재 dirty 기준을 기록한다.
- [x] 기존 테스트 방식으로 유효 교환 도착 후 원래 이미지가 제거 중간까지 남는 검사, 생성 파워가 잘못 제거되지 않는 검사를 작성하고 기존 실패를 확인한다.
- [x] 스냅샷과 제거 재생을 구현한다. 성공 교환의 출발 이미지가 아닌 도착 위치에서 제거한다. 미변경 셀·고정 레이어는 움직이지 않는다.
- [x] 0/중간/완료 시간에 크기·알파·좌표를 검사한다. 일반 블록·가로 로켓의 보정된 기본 변환과 Reset 이후 색/축척을 확인한다.

## 작업 2 — 기록 기반 낙하·공급

- [x] SettlementRecord의 Batch/Kind와 공급 생성 순서, 회수 처리 시점·결과 기록을 읽는다. 회수는 SettlementRecord만으로 충분하다고 가정하지 않고 이번 단계의 회수 결과 차이를 함께 확인한다.
- [x] 동일 Batch 이동과 다중 Batch 연속 이동, Portal/Supply, 회수 도착·공급 소진 fixture를 추가한다. 현재 즉시 표시가 중간 경로 검사를 실패하는지 확인한다.
- [x] Batch 시작 시 모든 출발 표시를 확보한 후 소유권을 이동한다. 같은 Batch 병렬, Batch 간 순차로 실행한다. 포털은 출입구 분리 재생, 공급은 실제 유입 방향으로 표시한다.
- [x] 움직이는 고철·회수 부품과 파워 종류를 유지한다. 회수되어 사라진 표시를 최종 Draw 전에 재생성하지 않는다. 끝난 블록만 최종 착지한다.
- [x] 중간 위치·동시 개수·최종 위치·고정 레이어·보드 경계/내부 공급 잘림을 검사한다. 매 프레임 새 GameObject를 만들지 않고 재생 소유 표시를 정리한다.

## 작업 3 — 세션·연쇄·UI 연결

- [x] 기존 IsPresenting/CanAcceptInput/AdvancePresentation을 제거·정착까지 확장하는 검사를 먼저 작성한다. 재생 중 다음 executor 호출과 결과 팝업이 없는지 확인한다.
- [x] 규칙 실행 전 표시를 확보하고 각 행동·연쇄 결과를 한 번만 소비한다. 순서는 교환 → 제거·교체 → 정착 Batch → 착지 → Draw/Changed → 다음 단계다. 다음 규칙 호출은 앞 단계 완료 뒤에만 허용한다.
- [x] 직접 파워 발동·아이템 결과에도 기본 제거·정착을 연결한다. 상세 공격 효과는 만들지 않는다. 자동 섞기·초기 보드·기록 없는 결과·정착 실패는 기존 규칙에 따른 정확한 표시와 잠금 해제를 유지한다.
- [x] 같은 시드의 직접 실행기와 애니메이션 세션에서 최종 보드, 난수, 이동 수, 미션, 공급 커서, 회수·승패를 비교한다. pause·재생 시간 변경에도 결과가 같다.

## 작업 4 — 수명·화면 검증과 인계

- [x] pause/resume, 회전, 재생 중 다시하기 5회, 실패, 씬 종료/재진입을 검사한다. 임시 표시·이벤트·잠금·이전 콜백이 남지 않고 아틀라스 소유권이 유지돼야 한다.
- [x] 현재 9×9에서 Asset 사본/MemoryPack 진입을 확인한다. 기존 입력·교환·월드·UI 검사 중 관련되고 내부 빌드 호출 없는 진입점만 실행한다. 실행기 결과 assertion을 약화하지 않는다.
- [x] 실제 씬의 가로 1280×720·세로 450×800에서 제거/낙하/공급/착지/다음 연쇄의 연속 증거를 `Logs/PuzzleSettlementAnimationVerification/`에 남기고 직접 확인한다.
- [x] `Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-07-progress.md`에 환경·실행 진입점·결과·미검증 항목·완료 조건별 근거를 작성한다. `Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-usage.md`에 동작과 확인 방법을 기록한다.
- [x] diff/문서 링크/meta와 사용자 데이터·설정 보존을 검사한다. 실제 충족한 완료 조건만 체크하고 로드맵과 인덱스를 갱신한다. 빌드·실기기 검증을 했다고 표시하지 않는다.

## 계획 검토

네 작업은 같은 표시 소유권과 세션 경계에 의존하므로 순서대로 실행한다. 기존 기록으로 해결하며 범용 이벤트 버스·영속 블록 ID·별도 규칙 시뮬레이터는 추가하지 않는다. 문서 작성만으로 구현이나 목표 실행을 시작하지 않는다.

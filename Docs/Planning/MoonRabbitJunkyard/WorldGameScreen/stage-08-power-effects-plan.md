# 8단계 파워·장애물 상세 연출 Implementation Plan

> 실행 스킬: `superpowers:executing-plans`로 아래 작업을 순서대로 직접 실행한다. 자동 커밋하지 않는다.

**목표:** 기존 파워·타격 기록을 화면 효과와 연결하여 공격 도착과 손상 표시를 일치시킨다.
**구조:** 규칙 실행 전 표시 스냅샷과 계산 결과에서 일회성 표시 시간표를 만든다. 세션은 효과 완료 후 기존 제거·동시 정착 재생을 이어가며, 규칙을 재실행하지 않는다.
**기술:** Unity 6000.3.10f1, C#, SpriteRenderer, 기존 Addressables 아틀라스·UniTask·Editor 검사.
**명세:** [목표 및 완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-goal.md).

상태: 구현·Editor 검증 완료 (2026-10-01). 플레이어·Addressables 콘텐츠 빌드 미실행.

## 실행 결과

아래 5개 작업을 완료했다. 기존 파워·피해 기록에 표시용 인과 정보를 보완하고, 실제 아틀라스 재생기와 세션의 행동·연쇄·아이템·라스트팡 경계를 연결했다. 로켓 도착과 본체 반응을 분리하고 자석 변환 순서를 수정했다.

[최종 완료 조건 감사와 실행 결과](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-08-progress.md)가 증거의 기준이다. 네 파워·10개 조합·다섯 색상·네 생성 파워, 장애물/레이어, 자원 실패·종료, 기존 낙하·스와이프·Asset/MemoryPack 진입을 검증했다. 사용 방법과 표시 시간은 [사용 안내](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-usage.md)를 따른다.

전체 검사 2052 PASS, FAIL 0. 반복 프레임 검사도 포함한다. 실제 배포 번들과 실기기는 검증하지 않았으며 Editor 결과로 대신하지 않는다. 9단계는 별도 후속 범위다.

## 공통 제약과 검토 지점

명세의 보존·제외 조건을 모든 작업에 적용한다. 빌드, 사용자 Editor 강제 종료, 미저장 씬·원본 레벨 자동 저장, 자동 커밋·푸시는 금지한다. 코드 변경 전 dirty 기준을 기록한다.

특히 다음 다섯 조건을 검증한다: 출발 블록이 계산 후 이미 사라진 경우(작업 1), 드론 표적이 선행 공격으로 달라진 경우(작업 2), 변환·조합·반복 타격의 원인 순서(작업 3), 2×2와 여러 레이어의 중복 반응(작업 4), 취소·재시작 중 비동기 로드와 잔상(작업 5).

## 변경 위치와 책임

아래 경로는 `Assets/Scripts/Features/` 기준이다. 신규 파일명은 이번 단계의 제안이며, 같은 책임의 기존 구현이 발견되면 중복 생성하지 않는다.

| 위치 | 역할 |
| --- | --- |
| `GameScreen/Runtime/World/PuzzlePowerPlayback.cs` (신규) | Begin/Tick/Reset/IsPlaying 형태의 효과 재생·표시 소유권 |
| `GameScreen/Runtime/World/PuzzleEffectTimeline.cs` (신규) | 출발·도착·타격·반응의 표시 시간표. 규칙 계산 없음 |
| `GameScreen/Runtime/World/PuzzleArtwork.cs`, `PuzzleArtworkPaths.cs` | 효과 프레임 경로와 필요한 아틀라스 준비·해제 |
| `GameScreen/Runtime/World/PuzzleWorldBoard.cs`, `PuzzleBoardSnapshot.cs`, `PuzzleBoardRemovalPlayback.cs` | 효과 표시 재사용, 계산 전 모습 보존, 타격 시점의 제거/단계 변경 |
| `GameScreen/Runtime/Session/PuzzleGameSession.cs`, `.Presentation.cs`, `.Controls.cs` | 행동·자동 연쇄·아이템·종료 경계와 효과 재생 연결 |
| `GameScreen/Editor/Tests/PuzzlePowerAnimationVerification.cs` 및 책임별 partial (신규) | 중간 프레임·상태 동등성·실제 씬·수명 검사 |

참조 원본: `PowerBlocks/Runtime/PowerEffectResolution.cs`, `PowerCombinationResolution.cs`, `PuzzlePlay/Runtime/Actions/BoardActionExecutor.cs` 및 연쇄 처리. `EffectRecord`의 Source/Target/HitGroup/전후 값은 근거 자료이지 시간표 자체가 아니다. 기록에 인과 정보가 빠진 경우 규칙이 실제 결정을 내리는 지점에서 필요한 **일회성 표시 기록만** 보충한다. 피해·표적·난수 계산 및 저장 DTO를 변경하지 않는다. 이 결정과 필드 근거는 작업 1 결과에 남긴다.

## 작업 1 — 기록·리소스와 재생 경계

- [x] 실행 전 스냅샷, Changes/Effects, 조합·드론·마지막 파워 결과를 조사하고 발동 주체·순서·타격 묶음이 어디서 확정되는지 표로 남긴다. 이미 생성한 `Assets/Textures/PowerBlocks/`와 `Effects/` 및 [2차 이미지 목록](../../../Contents/MoonRabbitJunkyard/Art/Priority2/README.md)을 실제 import/아틀라스 항목과 대조한다.
- [x] 기존 검증 방식에 맞춰 `PuzzlePowerAnimationVerification.Run` 진입점을 만든다. 계산 후 출발점이 지워져도 발사 전 표시를 보존하고, 타격 전 대상의 내구도 이미지가 바뀌지 않는 실패 검사를 먼저 확보한다.
- [x] 위 자료로 최소 표시 시간표를 작성하고 Begin/Tick/Reset/IsPlaying 수명 경계를 구현한다. 공격에 맞춘 제거가 끝난 대상을 일반 제거가 다시 축소하지 않도록 소유권을 정한다.
- [x] 필요한 효과 종류의 아틀라스만 준비한다. 로드 취소·누락 실패 시 핸들과 입력 잠금을 정리한다. 누락 번들 때문에 빌드를 실행하지 않는다.
- [x] 규칙 결과 무변경·표시 초기/중간/완료·Reset·자원 해제 검사를 통과시킨다.

## 작업 2 — 로켓과 드론

- [x] 로켓 가로/세로·양방향·비활성 칸·벽 통과와 드론 비행 대상의 실제 기록을 fixture로 만든다. 도착 전에 대상이 숨겨지면 실패하게 한다.
- [x] 대기 불꽃 없는 본체에서 발사 프레임·궤적·타격을 연결한다. 가로 로켓 크기와 중앙 보정을 발사 중에도 유지한다.
- [x] 드론 로터 4프레임 반복, 이륙·표적·비행·착탄을 연결한다. 선행 공격 이후 결정된 실제 표적을 그대로 사용한다.
- [x] 0/중간/타격/완료 프레임, 화면 밖 잔상, 최종 상태·난수 동등성을 검사한다.

## 작업 3 — 폭탄·자석·생성·조합

- [x] 지원 조합 목록을 코드에서 추출하여 누락 없는 검증 표를 만든다. 같은 칸 연속 발동·색 변환·다중 드론·체인 파워를 포함한다.
- [x] 폭발, 자석 흡인/변환, 5색 매칭 및 파워 생성 효과를 연결한다. 목표 문서의 초기 시간을 사용하고 원인 공격 이전에 후속 파워가 발동하지 않게 한다.
- [x] 독립 공격은 겹쳐 재생하되 동일 대상의 서로 다른 유효 타격은 보존한다. 표시를 위해 Range/표적 선정/난수를 다시 실행하지 않는다.
- [x] 조합 전체에서 공격·제거·후속 발동 순서와 직접 실행기 동등성을 검사한다. 실패·빈 효과 결과도 끝나고 잠금이 해제돼야 한다.

## 작업 4 — 장애물과 레이어 반응

- [x] 1×1/2×2, 1단계 손상/완전 파괴, 보호/무효 반응, 반복 유효 타격, 덮개·먼지 동시 변화, 발전기 충전 fixture를 추가한다.
- [x] 계산 전 모습과 EffectRecord의 전후 값을 사용해 해당 타격 시점에 내구도·충전·제거 이미지를 갱신한다. 나무·금속·거미줄·먼지·곰팡이 효과를 재사용한다.
- [x] 2×2는 본체 기준으로 타격을 묶는다. 장치·덮개·바닥 등 고정 표시를 낙하 스냅샷에 섞지 않는다.
- [x] 이미지 상태, 동일 타격 중복 없음, 서로 다른 유효 타격 보존, 마지막 잔상 정리를 검사한다.

## 작업 5 — 통합·수명 검증 및 인계

- [x] 교환·파워 탭·아이템·자동 연쇄·마지막 파워가 같은 재생 경계를 사용하도록 연결한다. 공격 → 대상 반응 → 동시 낙하·대기열 → 착지 → 다음 연쇄를 확인한다.
- [x] 입력/아이템/결과 UI 조기 표시 차단, pause/resume, 재생 속도 변경, 회전, 다시하기 5회, 실패, 씬 종료/재진입을 검사한다. 효과 객체는 재사용하며 프레임마다 생성하지 않는다.
- [x] `PuzzlePowerAnimationVerification.Run`과 실제 씬용 `RunScene`을 실행한다. 기존 `PuzzleSettlementAnimationVerification.Run/RunScene`, 신규 대각선 `Levels.Editor.SettlementVerification.FreshSupply`, 교환 검사 중 내부 빌드를 하지 않는 관련 진입점을 실행한다. 새 검사도 빌드 API를 호출하지 않아야 한다.
- [x] Asset 사본/MemoryPack 경로, 직접 실행기 대비 보드·난수·이동 수·공급·미션·회수·승패 일치를 확인한다. 가로 1280×720/세로 450×800에서 발사·도착·손상·동시 낙하 중간 증거를 남겨 직접 본다.
- [x] `Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-08-progress.md`에 명세 각 완료 조건의 증거·미검증 항목을, `Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-usage.md`에 재생 동작·최종 시간을 기록한다. 링크·diff·meta·사용자 데이터 보존을 확인한다.

## 실행 원칙

작업별로 실패 검사 → 최소 구현 → 관련 검사 → 결과 기록 순서다. 모든 작업은 같은 표시 시간표·스냅샷에 의존하므로 순차 진행한다. 실제 검증 전 완료 체크를 하지 않는다. 빌드나 모바일 기기 검수를 한 것처럼 기록하지 않는다.

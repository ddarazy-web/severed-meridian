# EF-02 — 드론 선택·예약·효과 타임라인 기준 확보 결과

상태: **완료**, 2026-10-04(KST). EF-03 문서 준비 완료, 다음 단계 구현 미착수.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-02-drone-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-02-drone-baseline-goal.md) · [가이드라인](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 실제 실행

Unity 6000.3.10f1 별도 배치 에디터의 데이터 검사 **352 PASS / 0 FAIL**, 네 최종 진입점 모두 종료 코드 0. 실제 관찰 16건을 저장했다. 생산 코드·원본 에셋·저장 포맷은 변경하지 않았고 새 정책/비행도 구현하지 않았다. 플레이어/Addressables 빌드, Play Mode와 실기기 검사는 미실행이다.

| 진입점 | PASS | 증거: `Logs/ElementFramework/Stage02/` 아래 |
| --- | ---: | --- |
| `Levels.Editor.TargetPowerVerification.Data` | 116 | `target-data-results.txt`, `target-data.log`, `target-data-execution.json` |
| `Levels.Editor.TargetPowerVerification.Supplemental` | 102 | `target-supplemental-results.txt`, `target-supplemental.log`, `target-supplemental-execution.json` |
| `GameScreen.Editor.PuzzlePowerAnimationVerification.Data` | 102 | `timeline-data-results.txt`, `timeline-data.log`, `timeline-data-execution.json` |
| `Levels.Editor.TargetPowerVerification.Baseline` | 32 | `baseline-results.txt`, `baseline-final-editor.log`, `baseline-final-execution.json` |

Baseline 최종 실행: 02:06:18~02:06:41(KST), PID 94156. 원시 입력·후보·상태·턴 문맥·예약 기록·난수·효과·타임라인은 `drone-observations.jsonl`에 있다. `input`은 JSON 문자열이고 `state/context/candidates/effects/attacks/reactions`는 기존 검사 Snapshot의 읽기용 속성 문자열이다. 규칙 난수 소비 수, 후보 수, 예약/예상 기여, 실제/표시 미션 진행, 전체 시간은 별도 숫자 필드로 남겼다. `displayedProgress=-1`은 표시 미측정이며 관리자 없이 기록한 타임라인 행의 예약/기여/캐시 필드도 -1로 미측정을 구분한다. 범위 후보는 해당 조회에 실제 사용한 새 관리자와 턴 문맥으로 기록했다.

## 현재 책임과 순서

| 경계 | 현재 책임/계약 |
| --- | --- |
| `MissionProgressRules.Query/Project` | 반응 가능한 미션 기여·피해/충전/예상 제거 계산. 실제 진행을 증가시키지 않음 |
| `DroneTargetManager.QueryArea` | 영역별 후보 캐시 + 현재 예약/남은 미션 필터. 조회는 캐시를 만들 수 있지만 보드·미션·예약·기록·규칙 난수는 변경하지 않음 |
| `Reserve/RequestArea` | 공통 후보 중 하나 선택, 2개 이상이면 규칙 난수 1회; 예약/선택 기록 소유 |
| `Land` | 자기 요청을 제외해 후보 유효성 확인, 무효이면 해제→재선정→예약→착탄 해제. 실제 피해는 수행하지 않음 |
| `PowerEffectResolution` | 같은 작업 사본에서 요청·다른 공격·착탄 검증·피해/미션 순서를 처리하고 최종 PowerPresentationTrace 생성 |
| `PuzzleEffectTimeline` | 이미 확정된 공격/효과를 표시 시작·접촉·반응 시각으로 변환. 목표나 규칙 난수 재선정 없음 |
| `PuzzlePowerPlayback` | 클립을 생성하고 프레임별 위치/프로펠러/효과를 표현. 규칙 선택을 소유하지 않음 |
| `PuzzleGameSession.ScheduleProgress` / `PuzzleProgressFeedback` | 실제 진행 기록을 반응 시각에 연결하고 수집 도착 후 표시 진행 증가. 최종 State의 숫자를 즉시 표시하지 않음 |

핵심 흐름은 **후보 조회 → 선택·예약 → 다른 효과 처리 → 착탄 재검증/재선정 → 실제 타격 → 최종 효과 기록 → 표시 시간표/재생**이다. 후보를 점수 최대값으로 정렬하는 새 밸런스는 없다. 노출 색의 직접 기여가 있으면 같은 미션의 덮개 대체만 제외하고, 다른 미션과 같은 우선순위 후보는 균등 선택한다. 후보가 없으면 일반 블록을 사용하고 그것도 없으면 종료한다.

## 관찰 결과

좌표는 0부터 센다. 보드 9×9, 모든 입력은 메모리 fixture이며 저장하지 않았다.

| 사례/시드 | 실제 결과 |
| --- | --- |
| 색 정책, 7 | (0,0) 노출 Type1과 (8,8) 거미줄 Type1 중 직접 후보 1개. 반복 조회 캐시 1회, 난수/예약 0 |
| 같은 후보 예약 | 완료 예상 1, 실제 진행 0, 규칙 난수 0. 남은 미션 예상 충족 뒤 일반 후보 80개 |
| 예약 대상 소실 | (0,0) 제거/Invalidate 후 덮개 후보 1개 복구. 예약 수는 착탄 전 1이지만 예상 완료는 0 |
| 착탄 재선정 | (8,8) 덮개로 재선정, 자기 예약 0. 덮개 제거 뒤 노출 색 직접 후보 복구, 실제 색 진행은 0 |
| 미션 완료 | 실제 진행 1 설정 후 미션 후보 제외, 일반 후보 80개 |
| 2×2 내구도2, 19 | 점 후보 4개→예약1 후 3개. 예약2 후 예상 피해2/완료1, 실제 진행0, 추가 미션 후보 제외. 선택 난수 소비 1→2 |
| 범위 조준 | Blast3 후보 16개. 일반 칸 (3,3)이 장애물 (4,4)에 피해1을 주는 조준 중심. (4,4) 중심의 기여는 남은 내구도2로 제한 |
| 발전기 미완충, 12345 | 충전0/3 예약은 예상 충전1/완료0. 발전기 4칸 중복 예약 제외, 연결 본체 직접 후보 유지 |
| 완충 직전 | 충전2/3 예약은 연결 본체 완료1 예측, 발전기/연결 본체 추가 조준 제외. 취소하면 예상 충전0·후보 복구 |
| 실제 부모 로켓 재선정, 1 | (4,0) 로켓이 (4,2) 드론을 발동하고 예약 표적 (4,8)을 제거. 드론 최종 표적은 (8,8), 실제 색 진행2, 난수 소비1 |
| 같은 타임라인 | 전체 표시 길이 약1.8545초. 생성 전후 상태/공격 기록/턴 문맥/난수 동일. 선행 공격 이후 비행, 착탄 이후 반응 |
| 표시 진행 | 규칙 진행2일 때 예약된 수집 표시0, 시간 진행 후 표시2. 데이터 검사로 실제 피드백 클래스를 확인 |

기존 Supplemental은 80개 시드의 색/상자 동등 선택, 단일 후보 무난수, 실제 연쇄 재선정과 모든 후보 소실 시 예약 해제/타격 종료도 확인한다. 기존 ReservationChecks는 다른 예약 소실·미션 완료·턴 피해 뒤 후보/캐시 재평가를 다룬다. 이번 Baseline은 기존 ReservationChecks를 재사용하고 위 실제 값과 추가 미확보 계약만 확인한다. EF-01 피해 검사를 복제하지 않았다.

## 신규 비행 요구와 현재 구현의 차이

- 현재는 3기 이하이고 로켓/폭탄/자석+드론이 아닌 경우만 상승·대기한다. 나머지는 선회한다. 타임라인 기본 대기는 해당 구분에 따라 1초/1.8초이고 Retargeted이면 0.18초를 추가한다.
- `Retargeted`는 효과 계산에서 발생한 재선정 표시이고, `WaitForAttacks`는 표시상 먼저 완료되어야 할 공격 수다. 최종 공격 기록에는 최종 Center와 재선정 여부가 있고, 사라진 목표로의 중간 비행 구간/중단 시점/호버 위치는 없다.
- 재생 클립은 미리 정한 출발/도착/곡선으로 이동한다. 화면에서 돌진 도중 유효성을 다시 검사하고 현재 위치에서 멈추는 상태 흐름은 아직 없다.
- 요청된 모든 드론 상승→개별 호버→돌진, 돌진 중 중단→호버→재선정→현재 위치 재돌진은 이후 구현한다. 이때 효과 타임라인 소유자가 중단/재선정 사건과 타격 순서를 결정하고 표시가 최종 상태를 임의 재조회하지 않게 해야 한다.
- 이번 검증은 그 변경의 선행 기준이다. 현재 연출이 신규 요구를 이미 충족한다고 판정하지 않는다.

## 보존·안전·작업 판단

시작 시 보호 파일 1,093개(Scripts/Data/Prefabs/Scenes/Packages/ProjectSettings) 해시와 기존 git 상태를 저장했다. 최종 비교에서 기존 파일 변경/삭제 0, 신규 파일은 `TargetPowerVerification.Baseline.cs`와 `.meta` 2개뿐이다. 기존 팝업 작업·EF-01 검사·생산 코드·원본·패키지/설정·기존 GUID/enum 숫자를 보존했다. 증거는 `initial-hashes.json`, `initial-git-status.txt`, `integrity-audit.json`이다. 최종 결과/관찰 수치·파일 보존·문서 링크 감사는 `completion-audit.json`에 기록했다.

ServeredMeridian Editor가 실행 중이 아니므로 전용 숨김 배치를 사용했다. 다른 프로젝트는 조작하지 않았다. 안전성을 확인한 Data/Supplemental만 실행했고 UI/Assets/FinalChecks/전체 Regression은 제외했다. 빌드·원본 저장·씬 저장·임의 커밋은 하지 않았다.

초기 신규 `.meta`의 잘못된 GUID 길이를 정상 32자리로 수정했다. 기존 GUID는 변경하지 않았다. 다음 기록 fixture는 색을 통일해 시작 매칭이 있었으므로 사용자 행동 Activate가 거절됐다. 기존 재선정 검사와 같은 실제 PowerEffectResolution 경로로 수정해 효과/타임라인을 분리 검사했다. 기대 결과를 완화하거나 생산 코드의 행동 검사를 제거하지 않았다. 실패 기록은 `invalid-new-meta-*`, `fixture-action-error-*`에 보존하고 최종 결과와 구분한다.

Pre-flight: 선택/예약 관찰이 타임라인의 최종 공격 기록으로 이어지고 표시 피드백은 별도 상태를 소비한다. 작업 판단: 같은 선행 계약을 공유하지만 생산 코드를 바꾸지 않는 기준 단계로 묶었다. 신규 구현의 RED/GREEN은 이번 범위가 아니다. 사용자 제약에 따라 임의 커밋/워크트리 이동/에이전트 위임 없이 진행했고 자체 검토 및 최종 diff/실제 기록/보존 감사를 수행했다.

다음은 작은 작업을 유지하기 위해 **거미줄·먼지 피해 기준만** 확보한다. 발전기 전체 피해/연결과 곰팡이 번식은 후속 단계로 분리한다. 선택 이유는 덮개·바닥의 피해 순서가 공통 반응 설계의 선행 계약이기 때문이다.

- [EF-03 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-03-layer-baseline-plan.md)
- [EF-03 목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-03-layer-baseline-goal.md)
- [EF-03 복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-03-command.md)

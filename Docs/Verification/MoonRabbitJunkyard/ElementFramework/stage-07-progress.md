# EF-07 — 회수 부품 수집·이동·공급 기준 확보 결과

상태: **완료**, 2026-10-04(KST). EF-08 문서 준비, 구현 미착수.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-07-recovery-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-07-recovery-baseline-goal.md) · [가이드라인](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경·실제 실행

Editor 전용 Assets/Scripts/Features/Missions/Editor/Tests/RecoveryVerification.Baseline.cs와 meta만 추가했다. 기존 Data/Edges와 메모리 helper를 재사용했다. 생산 규칙·원본 에셋·저장 포맷·기존 검사는 변경하지 않았다. EF-05 예외 원복 수정과 EF-06 기준을 유지했다.

Unity 6000.3.10f1 별도 배치 Editor에서 최종 **148 PASS / 0 FAIL**, 세 진입점 모두 종료 코드0이다.

| 진입점 | PASS | Logs/ElementFramework/Stage07 증거 |
| --- | ---: | --- |
| Levels.Editor.RecoveryVerification.Data | 75 | data-results.txt, data.log, data-execution.json |
| Levels.Editor.RecoveryVerification.Edges | 53 | edge-results.txt, edges.log, edges-execution.json |
| Levels.Editor.RecoveryVerification.Baseline | 20 | baseline-results.txt, baseline.log, baseline-execution.json |

recovery-observations.jsonl에 실제 관찰 **16건**을 저장했다. 시드12345·메모리 레벨 JSON·행동/공급/수집 순서·0부터 시작하는 좌표·수집 턴/묶음·미션 진행/남은 수·보드 수량/필요량·고정 커서/소비 수·난수 소비를 기록한다. beforeProperties/afterProperties/contextProperties/recordProperties는 읽기용 Snapshot 문자열이고 내부 JSON이 아니다. 실패 결과의 State=null이므로 보존된 입력을 기록하며 실패 context는 미측정이다. 중복 출구는 런타임 메모리 주입 사례이며 제작 원본의 유효성을 보장하는 사례가 아니다.

## 요구사항 대응

| 요구사항 | 실제 근거 |
| --- | --- |
| 초기/실제 출구 수집·단일 집계·원본 보존 | Data와 Baseline. 초기 실행 수집, 재정착 중복 없음, 동일 좌표 중복/반복 Collect, 연속 낙하 수집 |
| 이동/통로/벽/합류 | Edges.MovementChecks. 4방향·꺾인 경로·점유 통로 대기·합류 순서·신규 부품 대각선 두 출구 경쟁·벽 차단·복수 도착 |
| 교환·실제 파워 비파괴 | Edges.SwapChecks의 양방향 파워/자석 취소·일반 매칭/미매칭·마지막 수 연쇄. Baseline의 실제 로켓/폭탄 범위 적용 후 부품 보존·미수집 |
| 길 열기와 수집 구분 | Data.TargetChecks/Edges. 상자 피해와 발전기 충전의 간접 기여는 ExpectedComplete=0, 예약은 실제 수집 아님, 이동 뒤 후보 재검색, 벽 뒤 대상 제외 |
| 고정 공급·커서/소진 | Data/Edges/Baseline. Recovery3→Bomb 순서와 소비 커서, Stop/Random 소진, 미션 상한을 넘어도 고정 공급 순서 유지 |
| 유지 공급·남은 미션·중단 | Data의 12시드 다중 생성구 부족량, 완료 후 기존 부품 보존. Baseline의 필요량 조회·동일 정착4개 수집·완료 후 일반 공급만 실행 |
| 혼합 공급 | Edges.MixedSupplyChecks. 회수/고철 유지 수량 독립·막힌 보드 부족량 대기·생성 카운터 독립·재시작 초기화 |
| 실패 원자성 | Edges의 순환/미지원 공급·초기 정규화 실패. Baseline의 고정/유지 순환 거절 Repeating/State=null, 입력·커서·난수·수집 기록 보존 |

## 실제 계약·값

- 초기 출구 부품은 실행기 생성 시 Turn0/Batch0으로1개 수집한다. 입력 raw에는 부품이 남고 기록0이다. 이동 수20과 난수0을 보존한다. 재정착은 재집계하지 않는다.
- 중복 런타임 출구 Collect(turn7,batch3)는1개, 이어 batch4 재수집은0개다. 수집 기록은 최초 턴/묶음을 유지한다.
- 로켓/폭탄의 범위 안 부품은 Content=Recovery, 미션0, 수집0이다. **DamageResponse.None은 효과 기록에서 생략한다.** 범위 포함·실제 발동/주변 제거·부품 상태를 함께 검증했다.
- 고정 Recovery3→Bomb은 같은 출구(8,0)에서 Batch9/18/27에3개 수집한다. 미션 목표2의 진행은2로 제한되지만 고정 목록은3개를 공급한다. 각 공급 후 커서(index,consumed)는 (0,1)→(0,2)→(1,0)→(2,0), 난수0이다.
- 유지 target2/mission4/3칸 세로 보드는 Batch3/6/9/12에4개를 수집한다. 진행4·보드 부품0·필요량0, 이후 일반3개/난수3이다. 완료 상태에서 다시 비우고 정착하면 수집4를 유지하고 일반만 공급하며 누적 난수6이다.
- 유지 필요량은 max(0,min(설정 목표,남은 미션)-보드 부품 수)다. target3/onBoard2/progress0~4에서 remaining4→0, needed1/1/0/0/0을 읽기 전용으로 확인했다.
- 고정/유지 순환 실패는 Repeating, State=null이며 입력 부품0·커서(0,0)·난수0·수집0을 보존한다. 실패 사본의 내부 수치는 측정하지 않았다.

RecoveryRules.Collect가 실제 도착 점유 해제와 MissionProgressRules.Complete/RecoveryRecord를 같은 작업 사본에 적용한다. SettlementResolution은 초기 Batch0 수집 후 직선 이동·공급·신규 공급만 대각선 이동과 각 묶음 수집을 반복한다. 공급 부족량은 실제 수집 후 남은 미션으로 다시 계산된다. 드론 후보는 다음 이동의 방해 요소를 조회할 뿐 수집 완료를 보장하지 않는다.

## 수정 이력·검토·보존

추가 검사 초안 실패4회를 숨기지 않고 baseline-attempt1~4 로그/결과/종료 코드로 보존했다. 원인은 생산 오류가 아니라 fixture/기대 가정이었다: 공급량보다 큰 초기 목표, 무반응 효과 기록을 기대한 검사, 유지 미션의 유효 출구 누락, 고정 목록을 남긴 채 유지 모드 변경 시도였다. 최종 fixture는 유효한 분리 출구와 새 유지 생성구를 사용한다. 최종20개 검사를 모두 재실행했다. 인위적 생산 RED나 동작 변경은 하지 않았다.

시작 보호 파일1103개의 SHA256 비교에서 기존 변경0건이다. 신규 파일은 Baseline.cs/meta2개다. GUID 유효성·중복·diff/문서 링크·결과 범위/실제 값과 finally 메모리 fixture 해제를 자체 검토했다. 계획의 별도 요청 시에만 병렬 에이전트를 사용한다는 규칙에 따라 자체 검토했다. 빌드·커밋·사용자 Unity 종료·씬 저장 없음.

Play Mode·실기기·UI/시각 연출·광역 Regression은 미실행이다. UI/Regression은 에셋 생성/저장 경로를 포함해 이번 안전한 데이터 검사 범위에서 제외했다. 검증 범위의 미해결 실패는 없다. 새 프레임워크/드론 비행은 미구현이다.

## 다음 단계

[EF-08 저장·50레벨 팩 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-08-storage-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-08-storage-baseline-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-08-command.md).

기존 LevelPackVerification.Run/BoardNineVerification.Run은 생성·저장 경로를 포함하므로 그대로 실행하지 않는다. 다음에는 메모리 왕복/구간/호환 거절과 읽기 전용 원본 기준만 확보한다. 새 저장 형식이나 변환 적용은 하지 않는다. EF-08 구현 미착수.

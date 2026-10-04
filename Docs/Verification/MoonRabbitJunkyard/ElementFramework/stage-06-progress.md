# EF-06 — 이동형 고철 피해·낙하·공급 기준 확보 결과

상태: **완료**, 2026-10-04(KST). EF-07 문서 준비 완료, 구현 미착수.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-06-scrap-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-06-scrap-baseline-goal.md) · [가이드라인](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경·실제 실행

Editor 전용 `Assets/Scripts/Features/Obstacles/Editor/Tests/ScrapVerification.Baseline.cs`와 meta만 추가했다. 기존 Data/Supplemental/Edges를 재사용하고 실제 NextTurn(2)·정의/점유 좌표 구분과 실제 값 기록을 보완했다. 생산 코드·원본·저장 포맷·기존 검사 파일 변경 없음.

Unity 6000.3.10f1 별도 배치 Editor에서 **341 PASS / 0 FAIL**, 네 진입점 모두 종료 코드 0.

| 진입점 | PASS | 증거: Logs/ElementFramework/Stage06 아래 |
| --- | ---: | --- |
| Levels.Editor.ScrapVerification.Data | 186 | data-results.txt, data.log, data-execution.json |
| Levels.Editor.ScrapVerification.Supplemental | 102 | supplemental-results.txt, supplemental.log, supplemental-execution.json |
| Levels.Editor.ScrapVerification.Edges | 12 | edge-results.txt, edges.log, edges-execution.json |
| Levels.Editor.ScrapVerification.Baseline | 41 | baseline-results.txt, baseline.log, baseline-execution.json |

실제 관찰 **33건**은 scrap-observations.jsonl에 기록했다. 시드12345, 메모리 레벨 JSON, 타격/낙하/공급 순서와 실제 본체 내구도·점유 좌표/키·미션·공급 커서/항목 소비 수·누적 생성/잔여/생존 수·난수 소비를 저장했다. beforeProperties/afterProperties/contextProperties/recordsProperties는 읽기용 Snapshot 문자열이며 그 내부는 JSON 구조가 아니다. 실패 관찰은 결과에 포함된 작업 사본이 없으므로 보존된 입력 상태를 기록하며, context는 미측정 null로 구분한다.

## 기준 대응표

| 요구사항 | 검사와 확인 결과 |
| --- | --- |
| 내구도1~5·피해·제거 미션·바닥 | Data/Baseline. 인접 매칭과 실제 파워 피해1, 완전 제거만 미션1, 먼지3 보존 |
| 동일 턴/다음 턴 | Data/Baseline. 낙하 뒤 같은 본체 추가 피해 금지, 실제 NextTurn(2) 후 재피해 허용 |
| 본체·점유·좌표 | Supplemental/Baseline. 네 방향/경로/통로/합류 이동, 살아 있는 고철의 키·단일 점유 유지, 입력 사본 독립 |
| 벽·대각선·덮개 | Supplemental. 직선 벽 차단, 신규 공급 고철만 대각선 경쟁, 거미줄 배치 금지, 이동해도 먼지는 좌표에 잔류 |
| 일반 교환/파워/자석/조합 | Data/Supplemental/Edges. 유효 매칭·도착 파워 발동, 미매칭/고철끼리/빈칸/자석 교환 거절, 10개 조합의 턴 제한, 자석 색 변환 고철 제외 |
| 고정 공급·커서·소진 | Data/Baseline/Edges. Scrap2개→Bomb, 매 공급마다 소비 커서 증가, Stop 유지·Random 전환, 새 본체가 이전 키 피해를 상속하지 않음 |
| 유지 공급·목표·한도 | Data/Supplemental/Baseline. 부족량만 공급, 초기 배치는 생성 한도에서 제외, 누적 한도 소진 뒤 일반 공급, 제거된 키 재사용 안 함 |
| 조회/예약/실패 원자성 | Data/Supplemental/Edges/Baseline. 조회 무변경·드론 후보/예약 분리. 순환 실패/혼용 거절 시 본체/커서/카운터/난수 원본 보존 |
| 마지막 수/종료 | Edges. 정착·자동 매칭 후 종료, 추가 행동 차단. EF-05 원복 수정의 정상 경로 보존 |

## 실제 값과 중요한 좌표 계약

- (4,4)의 내구도2~5 고철은 첫 피해 후 (8,4)로 낙하한다. 점유 칸의 ObstacleIndex는0이며 내구도는 유지한다. 같은 턴 타격은 무피해, 다음 턴은 추가1피해다.
- **RuntimeObstacle.Definition.Coordinate는 초기 배치/생성 좌표다. 이동 뒤 현재 위치가 아니다.** 현재 고철 위치는 RuntimeCell.Coordinate와 ObstacleIndex 점유로 읽는다. Baseline은 초기 정의(4,4)와 현재 점유(8,4)를 함께 확인했다. 이를 불일치 버그로 바꾸거나 생산 정의를 수정하지 않았다.
- 고정 Scrap(count2,durability2)→Bomb: 첫 생성 본체키0, 커서(index0,consumed1); 다음은 본체키1, 커서(1,0); Bomb 후 커서(2,0). 제거된 본체는 내구도0으로 목록에 남고, ScrapGenerated는 계속0이다.
- 유지 target2/limit3/durability1: 세 번 공급의 live 수2→1→0, 누적 생성2→3→3, 잔여1→0→0. 실제 누적 난수 소비1→3→5로, 한도 소진 뒤 일반 색 공급 난수도 포함한다.
- 고정/유지 두 포털 순환 실패는 Repeating이며 결과 State=null이다. 원본 본체0·커서(0,0)·생성0·난수0을 보존한다.

## 현재 책임과 순서

ObstacleDamageRules가 고철의 본체별 턴 피해를 등록하고, 내구도0에서 미션과 전체 점유를 제거한다. MovementQuery는 덮개 없는 고철을 이동 가능 대상으로 조회한다. SettlementResolution은 작업 사본에서 이동 가능한 칸의 Content/색/방향/ObstacleIndex를 옮기며 정의와 바닥은 이동하지 않는다. 턴 문맥을 사본으로 이어 피해 기록을 유지한다.

공급은 유지 모드의 부족량/누적 잔여 한도를 먼저 배정한 뒤 고정 목록/일반 공급을 처리한다. LevelRuntimeState.SupplyScrap은 본체 목록 끝에 새 키를 붙인다. 제거된 본체를 남겨 같은 턴의 이전 피해 기록과 새 공급이 충돌하지 않게 한다. 고정 항목 커서는 공급 개수에 따라 전진하고 유지 누적 생성 카운터와 별개다. 정착이 순환·미지원 등의 이유로 실패하면 작업 사본을 폐기한다.

## 보존·검토·한계

작업 시작 보호 파일1101개의 SHA256을 비교했고 기존 파일 변경0건이다. 신규 Baseline.cs/meta 외 에셋·프리팹·씬·패키지·GUID·저장/enum·기존 작업 변경 없음. 빌드·커밋·사용자 Unity 종료·씬 저장 없음.

기존 검사 재사용·생산 규칙 보존의 특성 검사이므로 실패하는 생산 구현이나 인위적 RED를 만들지 않았다. 검사 범위/기대 값/메모리 fixture의 finally 정리를 자체 검토했으며 실제 종료 코드·로그·결과·GUID·문서 링크를 확인했다. 병렬 에이전트는 사용하지 않았다.

Play Mode·실기기·시각 연출은 미실행이다. 새 프레임워크·정책·드론 비행은 미구현이고 검증 범위의 미해결 실패는 없다. 회수 부품 수집 전체와 저장·표현·봇 경계는 이번 단계에 구현/검증 완료로 포함하지 않았다.

## 다음 단계

[EF-07 회수 부품 기준 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-07-recovery-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-07-recovery-baseline-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-07-command.md).

출구 수집·미션 잔여량과 보충 공급이 연결되므로 회수 부품만 독립 기준으로 확보한다. EF-07 구현은 시작하지 않았다.

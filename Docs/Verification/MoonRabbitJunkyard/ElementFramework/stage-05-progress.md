# EF-05 — 곰팡이 제거·턴 종료 번식 기준 확보 결과

상태: **완료**, 2026-10-04(KST). EF-06 문서 준비 완료, 구현 미착수.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-05-mold-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-05-mold-baseline-goal.md) · [가이드라인](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경과 범위 승인

기존 곰팡이 메모리 검사를 재사용하고 MoldVerification.Baseline.cs/meta에 실제 번식 기록·누락된 미션 진행 보존·예외 원복 검사를 추가했다.

기존 Edges의 행동 조회 예외 사례가 실패해 원인을 재현했다. 사용자가 **“예외 원복만 최소 수정하고 진행해”**라고 허용한 뒤 BoardActionExecutor.Ending.cs의 다음 행동 조회/자동 재배치 예외 원복만 수정했다. 정상 번식·미션·피해·선택·난수 순서는 유지했다. Edges의 구형 NeedsShuffle 기대값은 이미 구현된 자동 재배치 불가 시 Blocked/Outcome.Blocked 계약으로 정정했다.

## 실제 최종 실행

Unity 6000.3.10f1 별도 배치 Editor에서 **234 PASS / 0 FAIL**. 곰팡이/원복 필수 검사 213 PASS와 정상 아이템/부스터 회귀 21 PASS이며 여섯 진입점 모두 종료 코드 0이다.

| 진입점 | PASS | 증거: Logs/ElementFramework/Stage05 아래 |
| --- | ---: | --- |
| Levels.Editor.MoldVerification.Rollback | 2 | rollback-green-results.txt, rollback-green.log, rollback-green-execution.json |
| Levels.Editor.MoldVerification.Data | 143 | data-green-results.txt, data-green.log, data-green-execution.json |
| Levels.Editor.MoldVerification.Supplemental | 26 | supplemental-green-results.txt, supplemental-green.log, supplemental-green-execution.json |
| Levels.Editor.MoldVerification.Edges | 13 | edges-green-results.txt, edges-green.log, edges-green-execution.json |
| Levels.Editor.MoldVerification.Baseline | 29 | baseline-green-results.txt, baseline-green.log, baseline-green-execution.json |
| Levels.Editor.ItemBoosterVerification.Data | 21 | booster-data-green-results.txt, booster-data-green.log, booster-data-green-execution.json |

실제 관찰 14건은 mold-observations.jsonl에 저장했다. 시드·초기 메모리 레벨·행동/턴·번식 사유/후보/좌표·난수 전후·미션 목표/진행·전후 상태를 기록했다. 내부 *Properties는 읽기용 Snapshot 속성 문자열이며 JSON 구조가 아니다. remove-hit 행은 효과 직후 관찰이므로 종료 관련 숫자 기본값은 미측정이며 판정에 사용하지 않는다. 원복 두 사례의 실제 전후 문자열은 rollback-True-observation.txt / rollback-False-observation.txt에 있다.

## 기준 대응표

| 요구사항 | 근거와 실측 |
| --- | --- |
| 일반/4종 파워 내용물·방향·먼지 보존, 조작/낙하 차단 | Data. 덮개만 제거하고 별도 타격에서 소비/발동 |
| 매칭/직접 파워/자석 차이 | Data/Supplemental/Edges. 인접 매칭 제거, 일반 칸 파워 제거만으로 인접 곰팡이 무피해, 숨은 색 변환 제외·자석자석 덮개만 제거 |
| 제거된 턴·비소비 턴·완료·곰팡이 없음·후보 없음 | Data/Baseline. RemovedThisTurn/NoTurn/MissionsComplete/NoMold/NoCandidate, 억제 시 상태/난수 보존 |
| 후보 제외 | Supplemental. 벽/다른 덮개/장애물/회수/고철 및 원격 통로 후보 제외 |
| 복수 후보·같은 시드·반복 종료 | Data 16개 시드 + Baseline 7/12345. 후보7, 난수1회, 같은 시드 전체 상태 재현, 종료 재호출 무변경 |
| 기존 진행 보존·다음 턴 | Baseline. 목표2→3→4, 곰팡이 진행1·색 진행3 유지. 다음 턴 후보9·난수 추가1회, 제거/번식 이력 초기화 |
| 단일 후보 | Baseline. Rocket 칸 후보1, 난수0회, 내용물·먼지3 유지 |
| 실제 마지막 수 종료 | Supplemental/Baseline. 미달성은 Spread 뒤 MovesExhausted, 완료는 MissionsComplete 뒤 Won |
| 번식 뒤 행동 없음·제거 후 낙하 | Edges/Baseline. 자동 재배치 불가 시 Blocked. 덮개 고정 후 제거하면 Type3 실제 낙하 |
| 효과/턴 종료 실패 원복 | Supplemental/Edges/Rollback. 실패 때 덮개·미션·난수·문맥·대기 부스터/배치 기록 보존. 부스터 유무 모두 검증 |
| 정상 아이템/부스터 경로 | ItemBoosterVerification.Data. 아이템 비소비 턴·덮개 보존·종료, 정상 배치 순서/동일 시드/원본 보존·대기/종료 우선 통과 |

## 예외 원복의 원인·수정·RED→GREEN

기존 Edges는 (0,1)에 장애물 인덱스999를 주입한 후 ResolveAutomaticMatch를 호출한다. DiagnoseEdges에서 ActionQuery.Movable→Swap→Find→EndStableTurn의 ArgumentOutOfRangeException과 `unchanged=False`를 재현했다. 정상 레벨에서 이 잘못된 인덱스가 생성된다고 주장하지 않는다.

EndStableTurn은 작업 사본에 번식을 수행하고 State/TurnEffects를 반영한 뒤 행동을 조회했다. 그래서 조회 예외에 번식·난수가 남았다. 부스터 배치도 대기 목록을 소비하고 배치 기록을 추가하므로 두 상태 필드만 복원하면 부족하다.

수정은 다음 행동 조회/재배치 구간을 try/catch로 감싸고 예외 시 원래 State/TurnEffects 참조, Phase/Outcome/LastShuffle, 대기 부스터 목록과 배치 기록 길이를 복원한 후 같은 예외를 다시 던진다. 정상 경로와 승리/이동 소진의 기존 처리 순서를 바꾸지 않았다. 이 원복 범위는 해당 구간에 한정하며 모든 게임 예외를 처리하는 새 프레임워크가 아니다.

부스터가 있는 원복 검사를 먼저 실행해 FAIL을 확인했다(rollback-red-results.txt, rollback-red.log, 종료1). 수정 뒤 부스터 유무 두 사례 모두 PASS, 기존 Edges의 원복 검사도 PASS다. 원래 Edges 실패·진단·독립 검사 구형 기대값 실패와 수정 전 보고를 모두 Logs/ElementFramework/Stage05에 보존했다. 실패를 삭제하거나 제외해 통과로 바꾸지 않았다.

## 보존·검토·한계

보호 파일 1099개 중 승인 범위의 기존 파일 두 개만 변경했다: 런타임 Ending.cs와 Editor Edges.cs. 그 외 기존 파일 1097개는 SHA256 동일이다. 신규 Baseline.cs/meta 외 원본 에셋·팩·프리팹·씬·패키지·enum/GUID·기존 사용자 작업 변경 없음. 빌드·커밋·사용자 Editor 종료·씬 저장 없음.

최종 diff/원복 소유 상태/예외 재전파/메모리 fixture 정리를 자체 검토했고, 실제 결과 파일·종료 코드·로그·GUID·문서 링크를 확인했다. 병렬 에이전트는 사용하지 않았다. Play Mode·실기기·시각 연출 검사는 미실행이다. 새 프레임워크/드론 비행은 미구현이며 검증 범위의 미해결 실패는 없다.

## 다음 단계

[EF-06 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-06-scrap-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-06-scrap-baseline-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-06-command.md).

이동형 고철의 본체 이동과 공급 카운터는 함께 검증하고 회수 부품의 출구 수집은 후속 단계 후보로 분리했다. EF-06 구현은 시작하지 않았다.

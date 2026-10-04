# EF-19 — 고철 뭉치 피해 원인 허용 정책 연결 결과

상태: 완료. 2026-10-04, Unity6000.3.10f1, `work`/`598ba08`. 기존 EF-18 변경을 유지하고 고철1종의 읽기 전용 허용 조회만 연결했다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-19-scrap-damage-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-19-scrap-damage-policy-goal.md) · [가이드](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경과 경계

기존 ElementDamageSourcePolicy를 재사용한다. `LegacyElementDefinitions`의 `obstacle.scrap` 항목에 일반 인접 매칭·Power·Hammer 허용/MagnetAdjacent 거절 정책을 한 번 조합했다. `ObstacleDamageRules.Query`의 기존 상자 정책 조회 분기를 상자 또는 고철로 확장했다. Get→RequireDamageSourcePolicy→Allows 경로는 동일하다.

이번 생산 변경은 기존2개 파일의 두 줄뿐이다. 정책 타입·정의 생성자·배치/충전 계약·카탈로그 구조를 다시 만들지 않았다. 기존 상자 정책 및 다른4종에는 추가 변경이 없다. Apply/Remove/Mission/턴 집계/예약 피해량·고철 공급·낙하·발전기·드론·봇·UI/표현/풀·저장 코드는 수정하지 않았다.

정의된 네 원인만 정책을 조회하고, 미정의 DamageCause -1/4는 기존 고철의 Damage/1 의미를 유지한다. 미지원 ObstacleKind -1/6도 기존 의미를 보존했다. 외부 자석 경계는 여전히 색 자물쇠만 허용하고 내부 고철 Query는 MagnetAdjacent를 거절한다. 누락 정책은 ID 포함 오류이며 기본 허용 값으로 대체하지 않는다.

## 전환 전후 입력·실측

생산 코드 변경 전에 메모리 레벨47개를 저장했다. Before와 After는 같은 저장 JSON/본체 ID 및 시드12345를 사용했다. After에서 제작 입력 UUID를 다시 생성하지 않았다. 실제 기록688건의 전체 행이 전후 동일하다.

| 기록 | 건수 | 범위 |
| --- | ---: | --- |
| 외부/내부 Query·미션·예약 피해 |434|고철1~5내구도, 네 원인/-1/4, null/새 문맥/동일턴/다음턴/제거 본체, 벽/거리/비활성/보호, 다른 종류·공급 본체 |
| 다른 종류 내부 색 조건 |84|상자/캡슐/색 자물쇠/금속기둥/발전기/미지원2종 × 원인6개 × Type1/Type2 |
| 실제 Power/Hammer 피해 |60|내구도1~5, 매 턴2회 반복·다음턴·제거·미션·효과 |
| 실제 일반 블록 교환 매칭 |10|내구도1~5 × 벽 유무 |
| 실제 정착·공급 |20|내구도1~5 × 고정/유지 공급 × 두 본체 생성 |
| 공급 본체 실제 피해 |60|각 새 본체를 끝까지 파괴, 반복 타격·미션·효과·문맥 |
| 메모리 팩 바이트 |20|직접 타격 입력10개와 공급 입력10개 |

434개 Query 중 고정/경계/다른 종류는314개, 실제 공급 본체는120개다. 각 Query에서 전체 공개 상태와 비공개 TurnEffectContext 필드 지문, 규칙 난수 Seed/DrawCount/Version, Unity 전역 Random.state를 전후 비교했다. HashSet/Dictionary/List와 타격 튜플 필드도 비교하며 조회는 기록을 추가하지 않았다. 실제 색 조건의 Type2 비교는 별도 내부 조회84건에 있고 외부/내부 묶음 Query는 명시적으로 Type1을 사용한다.

벽은 저장 메모리 레벨의 실제 Flow 설정이다. 비활성/보호/미지원 종류·점유를 남긴 제거 본체는 테스트 메모리에만 명시적으로 주입했다. 제작 에셋이나 생산 소스를 변조해 검사 입력을 만들지 않았다. 내부 직접 조회와 외부 경계는 다른 책임이며 아래 차이를 전후 그대로 유지한다.

| 고철 입력 | 외부 Evaluate | 내부 Query |
| --- | --- | --- |
| 새 문맥, AdjacentMatch/Power/Hammer |Damage/1 · 본체 내구도 감소|동일 |
| MagnetAdjacent |None/0 · 자석 인접 예외는 색깔 자물쇠만 적용|None/0 · 인접 피해 대상 아님 |
| 피해가 기록된 본체, 허용 원인 |AlreadyDamaged/0 · 본체별 턴당 최대 1 피해|동일 |
| 다음 턴, 허용 원인 |Damage/1|동일 |
| 제거 본체, 허용 원인 |None/0 · 제거된 본체|동일 |
| 인접벽, AdjacentMatch |Wall/0 · 벽이 인접 매칭 피해를 차단|Damage/1 |
| 먼 출발, AdjacentMatch |None/0 · 인접하지 않음|Damage/1 |
| 비활성, 허용 원인 |None/0 · 보드 밖 또는 비활성 칸|Damage/1 |
| 보호, 허용 원인 |Protected/0 · 이번 턴에 생성된 파워 보호|Damage/1 |

Power/Hammer는 기존처럼 벽·거리에 제한되지 않는다. 비활성은 네 원인 모두 외부에서 거절한다. 보호에서도 MagnetAdjacent는 먼저 자석 경계에서 거절한다. 예약 칸 수0/1/2/4의 고철 피해는 살아 있는 본체에서0/1/1/1이고 제거 본체에서는 모두0이다. 미션 조회도 문맥을 변경하지 않는다. 실제 직접 타격은 한 턴의 첫 호출만1 감소하며 마지막 피해에서 점유가 제거되고 Scrap 미션1을 달성한다. 일반 매칭의 벽 없음은1 피해, 벽 있음은0 피해다.

### 실제 고정·유지 공급

고정 공급은 고철2개짜리 목록, 유지 공급은 목표1/한도2이며 내구도1~5를 각각 사용했다. 초기 런타임의(0,0)을 테스트에서 비운 뒤 실제 SettlementResolution.Resolve를 호출했다. Resolve는 작업 사본을 반환하고 입력 상태/문맥은 그대로다.

각 경로에서 두 고철이 순서대로 생성됐다. 본체 인덱스0/1, 런타임 ID `supply-scrap-0`/`supply-scrap-1`, 내구도 및 LiveScrapCount1을 확인했다. 고정 공급의 ScrapGenerated는0을 유지하고 유지 공급은1→2로 증가한다. 기존 본체의 피해 문맥이 남아 있어도 새 본체의 Query는 Damage/1이며 이전 본체의 피해 기록을 상속하지 않는다.

각 새 본체에 네 원인과 미정의 원인을 조회하고 실제 타격/반복 타격으로 제거했다. 제거 후 LiveScrapCount0이며 미션은 첫 본체 후1, 두 번째 후2다. 정착 기록/공급 상태/난수 진행·효과·문맥·생성 ID 모두 전후 동일하다. 규칙 난수는 공급이 원래 소비하는 진행을 기록해 전후 비교했고, Query 자체는 무소비다. Unity 전역 난수는 공급·피해 전체에서도 그대로다. 공급 순서·수량·생성·본체 ID 알고리즘은 수정하지 않았다.

### 정의·저장

실제 고철 정책의 네 허용값과 반복 조회 객체 동일성을 확인했다. 기존 불변 정책의16개 bool 조합을 메모리 정의로 만들어 같은 ElementCatalog.Get→Require→Allows 경로64개를 확인했다. 기존2/3/4인자 정의의 정책 누락3개는 각각 ID 포함 InvalidOperationException이다. 카탈로그/배치/충전의 기존 검사는 계속 통과했다.

실제 Encode 바이트는 직접 타격10개 각각1895바이트, 고정 공급5개 각각1891바이트, 유지 공급5개 각각1871바이트다. 원본 JSON/배치 ID와 재 Encode 바이트를 실행 안에서 비교하고 base64 전체를 전후 기록으로 비교했다. 공급 생성 ID는 런타임 기록에 포함된다. FormatVersion1·LevelsPerPack50을 유지했다. 실제 입력·SHA256은 input-*.json/pack-summary.json에 있다. 배포 팩 파일은 생성하지 않았다.

## 별도 Editor 결과

| 검사 | PASS | FAIL | 종료 코드 |
| --- | ---: | ---: | ---: |
| ScrapDamagePolicyVerification.Before, 생산 전 |895|0|0|
| ScrapDamagePolicyVerification.Run, 최종 |960|0|0|
| CrateDamagePolicyVerification.Run |696|0|0|
| GeneratorPlacementVerification.Run |144|0|0|
| DurablePlacementVerification.Run |210|0|0|
| CratePlacementVerification.Run |75|0|0|
| ElementCatalogVerification.Run |1541|0|0|
| ElementIdVerification.Run |49|0|0|
| FixedObstacleVerification.Data |142|0|0|
| GeneratorVerification.Data |246|0|0|
| BotObservationVerification.Run |32|0|0|
| ScrapVerification.Data |186|0|0|
| PowerEffectVerification.Data |96|0|0|
| 최종 필수12종 합계 |4377|0|각0 |

Before 첫 실행895 PASS/0 FAIL·종료0으로 기준을 확보했다. RED는 전후688개 결과가 동일한 뒤 `obstacle.scrap` 정책 누락으로 실패했다(888 PASS/1 FAIL·종료1, ID 포함 InvalidOperationException). Before의 입력 준비10개 검사는 After에서 저장 입력을 읽으므로 반복하지 않는다. 실패 증거를 red.log/execution/results/values에 보존했다. GREEN 이후 필수 실패는 없다.

새 검사의 Make/배치/고정·유지 공급 준비 헬퍼와 기존 ScrapVerification.Data/PowerEffectVerification.Data의 DataChecks 호출 경로를 재확인했다. 메모리 입력·실행기·Logs 출력만 사용하고 UI Prepare나 에셋 저장을 호출하지 않는다. 각 검사는 별도 batchmode/nographics Editor이며 사용자 Editor에서 Exit 진입점을 실행하지 않았다. 기존11종의 결과·값17개를 백업→새 실측 복사→원래 파일 복원했고 바이트 동일을 확인했다.

증거: Logs/ElementFramework/Stage19의 protected-before.json/existing-work-before.json/git-before.txt/progress.md, input-*.json, before/red/after.log 및 execution/results/values, baseline-values.jsonl, boundary-summary.json, pack-summary.json, run-regression.ps1, 각 기존 검사 log/execution/results/values, verified-results.json/historic-evidence-audit.json/preservation-audit.json/guid-audit.json/source-final.json/completion-audit.json. 로그는 Git 제외 대상이다.

## 보존·미검증·남은 문제

보호1828개 중 기존 생산 파일2개만 변경되고 나머지1826개는 SHA256 동일하며 삭제0이다. 기존 meta/GUID 변경0, 새 검사 GUID1개 중복0이다. 정책/정의/상자 검사 등 EF-18 소스와 과거 보고서를 보존했다. EF-05 원복, EF-09 원본1254px3개·Editor/월드 바닥 차이, EF-11 Draw/Reset/해제 책임·원본 레벨/팩/씬/프리팹/리소스·Packages/ProjectSettings도 보존했다. enum 숫자는 변경하지 않았다.

`work`/598ba08을 유지하고 EF-18/19 변경은 미커밋이다. 빌드·재패킹·이미지·팩 재생성·커밋·사용자 Unity 종료·씬 저장은 하지 않았다. NCloud_Unit_CV 등 다른 프로젝트는 건드리지 않았다.

소스/호출부/diff와 검사 입력을 직접 검토했다. 별도 에이전트 요청이 없어서 독립 리뷰는 수행하지 않았으며 자체 검토는 검토 강도가 낮다. 전체 플레이·렌더링·실기기·IL2CPP·성능은 검사하지 않았다. 고철 허용 조회의 필수 실패는 없고 다른4종의 피해 정책·공통 실행·저장·드론 전환은 미착수다. 상자·고철의 미정의 피해 원인 허용은 기존 호환 의미로 남겼으며 새 정책 API의 미정의 원인 오류와 구별한다.

## 다음 한 단계

**EF-20 고물 회수 캡슐 피해 원인 허용 정책 연결**만 준비했다. 같은 정책의 Power/Hammer 허용·일반/자석 인접 거절을 캡슐1종 조회에 연결하되 미정의 원인 거절·연결 대상 철거·미션 등 기존 의미를 유지한다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-20-capsule-damage-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-20-capsule-damage-policy-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-20-command.md). 다음 구현은 시작하지 않았다.

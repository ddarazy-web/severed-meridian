# EF-18 — 나무상자 피해 원인 허용 정책 연결 결과

상태: 완료. 2026-10-04, Unity 6000.3.10f1, `work`/`598ba08` 기준. 상자1종의 읽기 전용 허용 조회만 연결했다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-18-crate-damage-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-18-crate-damage-policy-goal.md) · [통합 가이드](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경과 책임

`Elements/Data/ElementDamageSourcePolicy`는 네 피해 원인에 대한 get-only bool만 보유하는 sealed 불변 값이다. `ElementDefinition`에 선택적인 정책과5인자 생성자를 조합했다. 기존2/3/4인자 생성자는 유지하고 필수 조회 `RequireDamageSourcePolicy`는 누락 시 정의 ID를 포함한 InvalidOperationException을 발생시킨다. 기본 허용 값으로 대체하지 않는다.

`LegacyElementDefinitions`의 `obstacle.crate.wood` 항목에서 일반 인접 매칭·Power·Hammer 허용/MagnetAdjacent 거절 정책을 한 번 준비한다. `ObstacleDamageRules.Query`의 상자 분기에서 같은 Get→Require→Allows 경로를 읽는다. 외부 DamageReaction 경계와 내부 턴 제한·피해량은 그대로 유지했다. 다른5종에는 피해 정책을 등록하지 않았다.

기존 코드의 미정의 DamageCause -1/4는 상자에서 Damage/1이었다. 이번 연결은 정의된 네 원인만 정책에 전달해 이 기존 의미를 보존했다. 새 정책 API에 미정의 원인을 직접 전달하면 ArgumentOutOfRangeException이다. 이는 정책 누락을 기본값으로 대체하는 경로와 다르다. 미지원 ObstacleKind -1/6의 Unsupported 의미도 보존했다.

기존 파일 변경은 ElementDefinition, LegacyElementDefinitions, ObstacleDamageRules, ElementCatalogVerification의4개다. 카탈로그 검사는 새 불변 정책 필드를 허용하면서 정책의 sealed/get-only/readonly bool 검사1개를 추가했다. 기존500개 카탈로그/배치/충전 검사를 삭제하지 않았다. 신규 파일은 정책과 CrateDamagePolicyVerification 및 각각의 meta다. Apply·Remove·Mission·ReservedDamage는 수정하지 않았다.

## 전환 전후 실제 입력과 비교

새 Before는 생산 코드 변경 전에 메모리 레벨44개를 준비하여 JSON으로 저장했다. After는 같은 파일을 읽고 시드12345로 다시 만들었다. 본체 배치 ID를 다시 생성하지 않았다. 전환 전후 실제 기록559건이 행별로 동일하다.

| 기록 | 건수 | 실제 확인 범위 |
| --- | ---: | --- |
| 외부/내부 Query·미션 조회·예약 피해 |360|상자 내구도1~6, 네 원인과 미정의 -1/4, null/새 문맥/같은 턴/다음 턴/제거 본체, 벽/거리/비활성/보호, 다른5종/미지원 종류 |
| 다른 종류의 내부 색 조건 조회 |84|종류7개 × 원인6개 × Type1/Type2. 위 외부 Query는 명시적인 Type1을 사용하고 이 별도 기록에서 내부 색 불일치를 확인 |
| 실제 Power/Hammer 적용 |84|내구도1~6 × 두 경로, 매 턴2회 반복·다음 턴 진행·최종 제거/미션 |
| 실제 일반 블록 교환 매칭 |12|내구도1~6 × 벽 유무, BoardActionExecutor.Swap의 실제 피해/미션/효과 |
| 발전기 연결 대상 직접 파괴 |6|상자6내구도를 새 턴마다 파괴, 무충전 발전기 자동 철거·연결 해제·미션 |
| 실제 메모리 팩 바이트 |13|Power/Hammer 입력12개와 발전기 연결 입력1개 |

벽은 저장 메모리 레벨의 실제 Flow 설정이다. 내부에서만 의미가 있는 제거 본체는 테스트 메모리 내구도를0으로 설정하고 점유를 남겼다. 비활성·보호·미지원 종류는 테스트에서만 메모리 상태/문맥에 명시적으로 주입했다. 제작 에셋이나 생산 코드를 바꿔 인위적인 입력을 만들지 않았다. 같은 턴 조회 입력의 RegisterDamage와 실제 적용 경로의 턴 제한을 따로 검증했다.

### 경계와 응답

상자 내구도3, 목표(4,4), 인접 출발(3,4)의 실제 응답이다. 좌표는0기준이며 아래 메시지·Amount·Response는 전후 동일하다.

| 입력 | 외부 Evaluate | 내부 Query |
| --- | --- | --- |
| AdjacentMatch/Power/Hammer, 새 문맥 |Damage/1 · 본체 내구도 감소|동일 |
| MagnetAdjacent |None/0 · 자석 인접 예외는 색깔 자물쇠만 적용|None/0 · 인접 피해 대상 아님 |
| 이미 피해 기록된 본체, 허용 원인 |AlreadyDamaged/0 · 본체별 턴당 최대 1 피해|동일 |
| 다음 턴, 허용 원인 |Damage/1|동일 |
| 제거 본체, 허용 원인 |None/0 · 제거된 본체|동일 |
| 인접벽, AdjacentMatch |Wall/0 · 벽이 인접 매칭 피해를 차단|Damage/1 |
| 멀리 있는 출발(0,0), AdjacentMatch |None/0 · 인접하지 않음|Damage/1 |
| 비활성, 허용 원인 |None/0 · 보드 밖 또는 비활성 칸|Damage/1 |
| 보호 문맥, 허용 원인 |Protected/0 · 이번 턴에 생성된 파워 보호|Damage/1 |

Power/Hammer는 기존처럼 인접벽·거리에 영향을 받지 않는다. 비활성은 네 원인 모두 외부에서 거절되고, 보호 상태에서도 MagnetAdjacent는 앞선 자석 경계에서 거절된다. 내부 Query에 외부의 벽/거리/활성/보호를 중복 넣지 않았다. 모든 실제 응답과 메시지는 boundary-summary.json 및 baseline/after-values.jsonl에 기록했다.

Query 전후 전체 공개 런타임 스냅샷과 비공개 TurnEffectContext 필드 지문을 비교했다. HashSet·Dictionary·List와 타격 튜플의 공개 필드까지 포함하며 공개 속성만 비교하는 기존 Snapshot의 사각을 보완했다. 규칙 난수 Seed/DrawCount/Version, Unity 전역 Random.state는 무변경이다. SimulationRandom은 유일한 Next 경로에서 DrawCount·bounds를 함께 증가시키는 기존 소스를 확인했다.

예약 칸 수0/1/2/4의 상자 예약 피해는 살아 있는 내구도1~6에서0/1/1/1, 제거 본체에서 모두0이다. 같은 문맥의 MissionProgressRules.Query도 상태를 변경하지 않았다. 실제 Power/Hammer는 첫 호출에서만1 감소하고 같은 턴의 반복으로 추가 감소하지 않는다. 마지막 턴에는 점유가 제거되고 Crate 미션 Progress1이며 후속 반복은 빈칸이다. 실제 일반 매칭은 벽이 없으면1 감소하고 벽이 있으면 감소0이다.

발전기 연결 입력은 본체 ID2개와 Wire를 유지했다. 상자6개 피해 후 발전기 Charge0·모든 점유 제거·연결0·미션1이다. 타격별 EffectRecord와 비공개 문맥, GeneratorRecord도 전후 동일하다.

### 저장과 불변 정책

Power/Hammer 입력12개의 Encode 결과는 각각1895바이트, 발전기 연결 입력은1921바이트다. 원본 JSON·배치 ID와 재 Encode 바이트를 같은 실행 안에서 비교하고, base64 전체를 전후 기록으로 비교했다. FormatVersion1·LevelsPerPack50을 유지했다. 실제 SHA256/배치 ID/원본 JSON은 pack-summary.json 및 input-*.json에 있다. 배포 팩 파일은 생성하지 않았다.

16가지 네 bool 조합을 메모리 정의에 넣고 같은 ElementCatalog.Get→RequireDamageSourcePolicy→Allows 경로의64개 반환값을 확인했다. 실제 상자 정책의 네 반환값과 반복 조회 객체 동일성도 확인했다. 기존2/3/4인자 정의의 정책 누락3개는 각각 ID 포함 InvalidOperationException이다. 배치·충전 프로필과 기존 생성 계약 검사는 계속 통과했다.

## 별도 Editor 검사

| 검사 | PASS | FAIL | 종료 코드 |
| --- | ---: | ---: | ---: |
| CrateDamagePolicyVerification.Before, 생산 전 |633|0|0|
| CrateDamagePolicyVerification.Run, 최종 |696|0|0|
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
| 최종 필수11종 합계 |3417|0|각0 |

PowerEffectVerification.Data→DataChecks→ProtectionAndRollback/AdjacentAndQueries/Rejections/GeneratedProtection/SupplementalChecks와 준비 헬퍼를 확인했다. 메모리 ScriptableObject·실행기와 Logs 출력만 사용하며 UI의 Prepare/에셋 저장은 호출하지 않는 경로다. 사용자 Editor에서 Exit 진입점을 실행하지 않고 별도 batchmode/nographics Editor11개를 순차 실행했다.

첫 Before는 새 검사 준비에서 Scrap을 발전기 연결 대상으로 설정하여546 PASS 뒤 실패했다. 새 검사 입력만 고쳐 다시 Before633 PASS/0 FAIL·종료0을 확보했다. 초기 로그/입력/실행 기록은 before-initial에 보존했다. RED는 기존 입력559건의 비교가 통과한 뒤 새 정책 부재만 실패했다(전후 준비 검사 차이로622 PASS/1 FAIL·종료1). 생산 코드 적용 후 필수 실패는 없다.

기존 검사10종의 결과·값15개를 실행 전에 복사하고 새 실측을 Stage18에 보관한 뒤 원래 파일로 복원했다. historic-evidence-audit.json에서 전부 바이트 동일을 확인했다. 과거 단계 보고서를 덮어쓰지 않았다.

증거: Logs/ElementFramework/Stage18의 input-*.json, before-initial/, before-retry.log·before-execution.json·before-results.txt·baseline-values.jsonl, red.log·red-execution.json·red-results.txt, after.log·after-execution.json·after-results.txt·after-values.jsonl, boundary-summary.json, pack-summary.json, run-regression.ps1 및 각 검사 execution/results/values, verified-results.json, historic-evidence-audit.json, preservation-audit.json, source-final.json, completion-audit.json, progress.md. 로그는 Git 제외 대상이다.

## 보존·검토·미검증

보호한1824개 중 위 기존 소스4개만 변경되고 나머지1820개는 SHA256 동일하며 삭제0이다. 기존 meta/GUID 변경0, 신규 GUID2개 중복0이다. EF-05 원복 소스·EF-09 원본1254px3개/Editor·월드 바닥 차이·EF-11 Draw/Reset/해제 책임, 원본 레벨·팩·씬·프리팹·아틀라스/Addressables·Packages/ProjectSettings를 보존했다. enum 숫자는 변경하지 않았다.

직전 사용자 요청으로 EF-13~17을598ba08에 커밋·푸시한 뒤 EF-18을 재개했다. EF-18 변경은 미커밋이며 `work` 브랜치를 유지했다. 이번 구현에서 빌드·재패킹·이미지·팩 재생성·추가 커밋·사용자 Unity 종료·씬 저장은 하지 않았다. NCloud_Unit_CV 등 다른 프로젝트는 건드리지 않았다.

변경 diff와 호출부·계획은 직접 검토했다. 별도 에이전트 요청이 없으므로 독립 리뷰는 수행하지 않았으며 자체 검토는 검토 강도가 낮다. 새 정책→Simulation enum 참조는 현재 기존 기본 어셈블리 안의 연결이고 새 asmdef/패키지는 없다. 허용 정책은 양·색 조건·턴 제한·예약·실제 실행을 대신하지 않는다. 일반 매칭·직접 타격·미션/예약 데이터 경로까지 확인했으나 전체 플레이·렌더링·실기기·IL2CPP·성능은 검사하지 않았다. 필수 범위의 남은 실패는 없고 다른 종류 피해 정책·드론/저장 전환은 미착수다.

## 다음 한 단계

**EF-19 고철 뭉치 피해 원인 허용 정책 연결**만 준비했다. 상자에서 확인한 동일 정책을 고철1종의 읽기 전용 허용 조회에 연결하고, 고철의 고정·공급 경로와 미션·저장 의미를 보존한다. 캡슐/색 자물쇠/금속기둥/발전기는 이후 단계로 남긴다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-19-scrap-damage-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-19-scrap-damage-policy-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-19-command.md). 다음 구현은 시작하지 않았다.

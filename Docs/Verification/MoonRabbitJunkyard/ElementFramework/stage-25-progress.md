# EF-25 — 내구도형 피해 집계 조회 정책 연결 결과

상태: 완료. work/6b41ce4와 EF-23~24 미커밋 작업을 보존했다. 이번 단계도 미커밋이다. 새75264+기존59856=135120 PASS/0 FAIL, 필수18종 각각 별도 Editor 실제 종료0.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-25-damage-aggregation-query-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-25-damage-aggregation-query-goal.md) · [가이드](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경과 실제 호출부

ElementDamageAggregationPolicy는 PerHitCell(bool)만 가진 불변 조회 값이다. AlreadyApplied(context,bodyIndex,cell,hit)는 false일 때 HasDamaged(bodyIndex), true일 때 HasHit(hit,cell)를 읽는다. 문맥·상태를 보유/기록하지 않는다. null은false이고 true 정책의 hit0 제한 없음도 기존 HasHit 의미로 유지했다. 본체별 정책은 hit0도 같은 본체에 턴당1회다.

ElementDefinition에 읽기 전용 DamageAggregationPolicy/RequireDamageAggregationPolicy와7인자 경로를 추가하고 기존2~6인자·ID/표시명 검증·배치/충전/원인/색 계약을 보존했다. 누락은 ID 포함 InvalidOperationException이며 기본값 대체가 없다. LegacyElementDefinitions의 false/true 값을 각각 한 번 준비해 Crate/Scrap/Safe/ColorLock은false를 공유하고 Appliance는true를 사용한다. Generator에는 필수 집계를 강제하지 않았다.

ObstacleDamageRules.Query의 원인 허용→발전기 위임→색→내구도 검사 뒤 기존 집계 분기만 Get→Require→AlreadyApplied와 PerHitCell로 연결했다. 반응/양/메시지·순서를 유지한다. ElementCatalogVerification에는 새 불변 필드 타입만 추가했다. 신규 검사와 두 meta 외에 기존 변경은 이4파일뿐이다.

일반 매칭/자석/파워/망치는 DamageReaction.Evaluate→Query를 거친다. 외부 조회는 인접 거리/벽·비활성/보호·null 출발색 대체를 적용한다. PowerEffectResolution은 Damage를 받은 경우 기존 Apply로 RegisterDamage/RegisterHit→내구도 감소→Remove→미션/GeneratorRules.TargetRemoved를 수행한다. MissionProgressRules.Query와 DroneTargetManager 후보/예약 유효성도 Power 반응을 공유한다. 예약 투영은 별도 Project→ReservedDamage를 사용한다. 이 실행/기록/미션/예약/발전기 소스와 기존 허용 원인/색 정책은 수정하지 않았다.

## 실제 입력·전후 결과

저장 JSON 입력441개·시드12345·전체38340행을 재사용했다. baseline/before/after-values.jsonl 전체 바이트 SHA256 동일: ee391ae31535ec9a4c50a27b64a33b3b393726dced499d67fa8584184e509b9b. Query는 전체 상태 SHA·비공개 문맥 SHA와 실제 내부/외부 Response/Amount/Message·미션/예약 결과를 기록한다. 실제 실행 사례는 전체 상태·효과·문맥/미션을 기록한다. 조회 전후 전체 상태/비공개 문맥/정의 규칙/전역 난수 무변경을 확인했다.

핵심31356조합은 전체 내구도(Crate1~6/Scrap1~5/Safe1~5/ColorLock1~3/Appliance1~9), 자물쇠 지정색5개, 전달색5+null, 원인4개와-1/4, 금속기둥4칸, 문맥13종을 정확한 조합 집합으로 검사했다. 문맥은 fresh/null/same-body/other-body/same-hit/other-cell/other-hit/hit0/next-turn/removed/removed-same/source-null/source-other다. source-other는 출발색Type2인 사례이며 EF-24 기존 검사도 모든 지정색에서 상대적으로 다른 출발색을 재검증했다. removed는 내부 점유를 유지한 내구도0 전제이고 실제 삭제 후 외부 Empty330개와 구분했다.

벽/거리/비활성/보호4824행은 원인4/-1/4와 지정색·Type2·null을 포함한다. 지정색Type2와 전달색Type2가 겹치는72행을 제외한 고유 조합은4752개다. 다른 종류/발전기/미지원 조회84개를 합쳐 Query36264개다. 실제 내부 반응/양/메시지도 직접 확인했다. Power/Hammer 및 미정의 원인의 기존 거리/벽 우회, Safe 인접 거절과 색 자물쇠 색 우회를 유지했다.

본체형 실제 반복608행은 두 본체·동일턴 반복/다음턴·Power/Hammer·제거/미션2를 검사했다. 별도 실제 hit0/다른hit190행도 본체별 턴당1피해다. 금속기둥 실제 피해126행과 hit 적용108행은 같은hit 같은칸 억제/다른칸/다른hit/hit0 반복·내구도 하한/본체 미션1을 확인했다. 일반 매칭18건과 범위 겹침126건은 가로/세로 로켓2칸, 폭탄0/1/2칸, 조합1/2/3/4칸과 자석+자석4칸을 포함한다.

예약 투영36건은 중복 전달을 제거한0/1/2/4칸과 내구도 상한을 확인했다. 실제 관리자28건은 예상 피해1/충전0/내구도1이면 완료1과 취소 복원을 확인했다. 각 Query의 ReservedDamage0/1/2/4 결과도 전후 같다. 발전기 연결 금속기둥 직접 파괴9행은 충전0·연결0·미션1·무충전 철거를 확인했고 기존 발전기 전체 검사도 다시 통과했다.

MemoryPack188개(실제 피해18+범위126+본체38+연결1+구간5)의 전체 base64/SHA256/본체ID를 전후 비교했다. 1/50/51/100/101은 실제 Encode→ReadLevel→Encode로 전체 필드/바이트·버전1/50구간 주소를 확인했고, 같은시드 원본/왕복5종 피해·미션·효과/문맥225행이 같다. 이 복합 배치는 기존 미션 최대4개 제한을 지키면서5종 본체를 모두 포함한다. 제작 에셋·배포 팩은 생성하지 않았다.

## 별도 Editor 실제 검사

| 증거명 | executeMethod | PASS | FAIL | 종료 |
| --- | --- | ---: | ---: | ---: |
| before | Elements.Editor.DamageAggregationPolicyVerification.Before | 75289 | 0 | 0 |
| red | Elements.Editor.DamageAggregationPolicyVerification.Run | 0 | 1 | 1 |
| after | Elements.Editor.DamageAggregationPolicyVerification.Run | 75264 | 0 | 0 |
| color-match | Elements.Editor.ColorMatchPolicyVerification.Run | 20861 | 0 | 0 |
| generator-reaction | Elements.Editor.GeneratorReactionPolicyVerification.Run | 24489 | 0 | 0 |
| appliance-damage | Elements.Editor.ApplianceDamagePolicyVerification.Run | 5085 | 0 | 0 |
| color-lock-damage | Elements.Editor.ColorLockDamagePolicyVerification.Run | 4452 | 0 | 0 |
| capsule-damage | Elements.Editor.CapsuleDamagePolicyVerification.Run | 592 | 0 | 0 |
| scrap-damage | Elements.Editor.ScrapDamagePolicyVerification.Run | 960 | 0 | 0 |
| crate-damage | Elements.Editor.CrateDamagePolicyVerification.Run | 696 | 0 | 0 |
| charge-placement | Elements.Editor.GeneratorPlacementVerification.Run | 144 | 0 | 0 |
| durable | Elements.Editor.DurablePlacementVerification.Run | 210 | 0 | 0 |
| crate | Elements.Editor.CratePlacementVerification.Run | 75 | 0 | 0 |
| catalog | Elements.Editor.ElementCatalogVerification.Run | 1541 | 0 | 0 |
| id | Elements.Editor.ElementIdVerification.Run | 49 | 0 | 0 |
| fixed | Levels.Editor.FixedObstacleVerification.Data | 142 | 0 | 0 |
| generator | Levels.Editor.GeneratorVerification.Data | 246 | 0 | 0 |
| bot | Levels.Editor.BotObservationVerification.Run | 32 | 0 | 0 |
| scrap | Levels.Editor.ScrapVerification.Data | 186 | 0 | 0 |
| power | Levels.Editor.PowerEffectVerification.Data | 96 | 0 | 0 |

Before 실제75289 PASS/0 FAIL·종료0 후 RED는 계약 첫 검사 '불변 피해 집계 조회 정책 존재' 누락으로0 PASS/1 FAIL·종료1이다. GREEN은 불변성/공유 등록/기존 생성 계약·누락ID/같은 카탈로그 true·false와 전체 저장 입력/결과 일치를 확인했다. RED는 계약부터 실패해 행 비교에 도달하지 않았으며 전후 동등성은 Before/GREEN 전체 범위로 입증했다.

전환 전 검사 작성 중 첫 실행은2x2를 마지막 열에 배치해 보드 밖 오류, 두 번째는 복합 배치의 미션5개로 기존1~4개 제한 오류로 종료1이었다. 각 오류의 원본 입력/로그/결과/종료를 first-before/second-before에 보존했다. 각각 테스트 배치만 수정했다. Fixtures 진입점에서 수정한 Pack/Manager/BodyHit을 먼저 확인해 종료0을 얻은 뒤 전체 Before를 다시 실행해 통과했다. 목표 범위와 게임 규칙은 축소/변경하지 않았다.

각 execution.json에 정확한 메서드·UTC 시작/종료·실제 종료 코드가 있다. 기존17종 출력29파일을 백업→새 실측 복사→원래 파일 복원 후 전체 SHA256 동일을 확인했다. 사용자 ServeredMeridian Editor가 없는 상태에서 별도 batchmode/nographics만 사용했다. 다른 프로젝트/사용자 에디터·씬은 건드리지 않았다. 신규 검사는 batchmode 보호가 있고 기존 에셋/UI 저장/생성·빌드 API를 호출하지 않는다.

## 보존·검토·한계

보호1842파일 중 기존4소스만 승인 범위에서 변경, 나머지1838파일 전체 해시 동일·삭제0·기존 meta 변경0이다. 네 파일의 변경을 역변환해 전환 전 원문과 같음을 확인했다. 특히 Supports/ReservedDamage/Mission·Apply/Remove, 기존 생성자 검증·원인/색·발전기 정책은 그대로다. 새 GUID2개 중복0. EF-23~24 기존 검사/meta/보고서/목표/계획/명령문을 보존하고 현재 단계 상태·공통 인덱스만 갱신했다. EF-05 예외 원복, EF-09 원본1254px3개와 Editor/월드 바닥 차이, EF-11 Draw/Reset/해제 책임, enum 숫자·원본 레벨/팩/씬/프리팹/리소스·Packages/ProjectSettings를 유지했다.

work/6b41ce4 유지. 커밋/푸시/빌드/재패킹/이미지/팩 재생성/사용자 Unity 종료/씬 저장 없음. 별도 에이전트 요청이 없어 자체 검토로 호출 순서·다른 본체/칸·hit0/null·전후 실제 반응·변경 범위를 확인했다. 독립 리뷰, 전체 플레이/렌더링/실기기/IL2CPP/성능은 미검증이다. 필수 범위 남은 실패는 없다. 실제 피해 기록·예약 집계와 저장/드론 전환은 남아 있다.

증거: Logs/ElementFramework/Stage25의 protected-before/existing-work-before/head-before/git-before/progress, 기존4파일 .before, input-*.json441개, first/second-before와 잘못된 팩 입력, fixtures/before/red/after log/execution/results/values 및 baseline-values.jsonl, 기존17종 실행/결과/값, run-new/run-regression.ps1, values-summary/pack-summary/verified-results/historic-evidence-audit/preservation-audit/guid-audit/source-final/completion-audit.json. Logs는 Git 제외다.

## 다음 한 단계

EF-26 내구도형 예약 피해량 조회 연결만 준비했다. 기존 PerHitCell을 재사용해 ReservedDamage의 집계 단위만 연결한다. 피해 적용/기록·예약 관리자·Project/미션·발전기는 이관하지 않는다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-26-reserved-damage-query-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-26-reserved-damage-query-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-26-command.md). 다음 구현은 시작하지 않았다.

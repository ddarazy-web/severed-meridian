# EF-23 — 발전기 반응 원인 허용 조회 연결 결과

상태: 완료. work/6b41ce4를 보존하고 EF-23 변경은 미커밋. 최종 새24489+기존14506=38995 PASS/0 FAIL, 별도 Editor 필수16종 실제 종료0. Before24467 PASS/0 FAIL; RED24417 PASS/1 FAIL·종료1은 정책 누락 검증이며 최종 실패가 아니다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-23-generator-reaction-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-23-generator-reaction-policy-goal.md) · [가이드](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경과 호출 흐름

- LegacyElementDefinitions의 obstacle.generator에 기존 불변 ElementDamageSourcePolicy(true,true,true,true)를 한 번 등록했다. 발전기 Size2/충전3~5 프로필과 기존5종 정의는 그대로다.
- ObstacleDamageRules.Query의 Get→Require→Allows 그룹에 Generator를 포함하고 기존 GeneratorRules.Query 위임을 그 그룹 뒤, 색/내구도/칸별 피해 검사 전에 이동했다. 미정의 원인-1/4는 Require를 통과하되 Allows를 호출하지 않는다. Supports, 오류/반응/양/메시지와 Apply/Remove 이후 본문은 그대로다.
- GeneratorReactionPolicyVerification와 새 meta만 추가했다. 제작/에셋/UI 변경은 없다.

발전기는 일반 인접/Power/Hammer의 공통 PowerEffectResolution에서 Charge 반응을 받아 GeneratorRules.Apply로 적용된다. TurnEffectContext.RegisterCharge가 본체별 턴당1회를 보장한다. 완충은 Targets의 활성 연결을 따라 제거/미션/Disconnected를 기록하고 자기 점유를 없앤다. 직접 대상 파괴는 ObstacleDamageRules.Apply→TargetRemoved로 연결을 해제하고 마지막 연결이 없어지면 무충전 철거한다. MissionProgressRules.Query/Project와 DroneTargetManager의 예약/후보 제외는 이 반응을 재사용한다. 이 실행 코드들은 수정하지 않았다.

## 전환 전·후 실제 결과

저장 입력 99개, 시드12345, 실제 결과 8745행을 Before/RED/GREEN에서 같은 JSON으로 재사용했다. baseline/before/red/after-values.jsonl 전체 바이트 SHA256 동일이며 실행 내부에서도 SequenceEqual로 비교했다. 결과에는 Response/Amount/Message, 전체 상태·본체ID/연결ID·규칙 난수 DrawCount, 비공개 문맥 필드/튜플, 효과·충전/철거/미션 기록, 예약·투영과 전체 MemoryPack 바이트가 포함된다.

| 입력 경계 | 내부 Query | 외부 Evaluate |
| --- | --- | --- |
| 네 원인 및 -1/4, null/새/다음턴 | Charge/1 · 발전기 충전 | 자석 인접만 None/0; 그 외 Charge |
| 같은턴 해당 본체 충전 기록 | AlreadyDamaged/0 · 발전기 본체별 수당 최대 1 충전 | 자석 인접만 None/0; 그 외 AlreadyDamaged |
| 내구도0/기본1, 다른 색/null | 동일 Charge 또는 AlreadyDamaged | 동일, 발전기를 제거 본체로 해석하지 않음 |
| 인접 벽, 일반/자석 인접 | 기존 내부 반응 | Wall/0 |
| 먼 출발, 일반/자석 인접 | 기존 내부 반응 | None/0 |
| 비활성 | 기존 내부 반응 | None/0 |
| 보호 | 기존 내부 반응 | 자석 인접은 앞선 None; 그 외 Protected |
| 실제 삭제 후 Empty | 점유를 요구하므로 따로 조회 | None/0 |
| 삭제 후 테스트 메모리에 점유만 복원 | 기존 Charge/AlreadyDamaged | 내부 점유 전제의 별도 사례 |

원문 메시지의 '수당'은 기존 코드 그대로 보존했다. 의미는 턴당1충전이다. 생성 기본 내구도는 실제1이며0은 테스트 메모리로 주입한 경계다. 기본값이나 제작 소스를 바꾸지 않았다. 핵심 조합6912개는 필요 충전3~5/현재0~완충전 ×4칸 ×6원인 ×4문맥 ×3색 ×내구도0/1이다. 경계768개, 삭제 외부144개/점유 복원 내부144개, 기존5종+미지원2종의 색/null126개를 포함해 Query7950개다. 모든 Query에서 상태·비공개 문맥·카탈로그 규칙·전역 난수 무변경을 검사했다.

연결 대상 Crate/Safe/ColorLock/Appliance ×필요3~5 ×Power/Hammer를 실제 실행했다. 네 점유칸을 같은턴에 반복해 충전은1회이며 총384회 호출이다. 완충 시 대상/발전기 점유 전체 제거·본체 미션1·Activated1·Retired0를 확인했다. 직접 대상138회 타격은 마지막 대상 제거 시 Charge0/연결0/철거1이다. 실제 일반 매칭6개는 여러 접촉1충전과 전체 접촉벽0충전을 확인하고 뒤의 Power가 벽을 관통하며 같은턴 중복 충전하지 않음을 확인했다.

GeneratorVerification의 Make/Multiple/복수 발전기/벽 사례를 안전한 메모리 입력으로 재사용했다. 일부 연결 직접 제거 후 남은 연결 유지, 여러 연결 완충 제거, 두 발전기 독립 충전과 다른 발전기만 철거, 와이어 경로의 벽이 Power 충전/연결 작동을 막지 않음을 전후 비교했다. 별도 기존 GeneratorVerification.Data도 전체246 PASS다.

예약은 현재 충전12조합 ×점유0/1/2/4의 중복 DroneImpact 투영48건으로 직접 피해0/본체 충전1을 확인했다. 실제 가로 범위 예약12건은 발전기1충전과 연결된 금속기둥 두 칸의 직접피해2를 구분하며 완충 직전 예상 미션1/후보 제외/취소 복원을 확인했다. 조회는 상태/문맥/난수를 변경하지 않았다.

MemoryPack32개(실행24+연결3+구간5)의 전체 base64/SHA256과 본체·연결 ID를 전후 기록했다. 1/50/51/100/101의 실제 Encode→ReadLevel→Encode는 버전1/50구간 주소와 전체 필드/바이트를 보존한다. 원본과 왕복 데이터의 같은시드 충전/효과/문맥도 동일하다. 배포 파일이나 팩은 생성하지 않았다.

## 검사 과정과 실제 종료

| before | Elements.Editor.GeneratorReactionPolicyVerification.Before | 24467 | 0 | 0 |
| red | Elements.Editor.GeneratorReactionPolicyVerification.Run | 24417 | 1 | 1 |
| after | Elements.Editor.GeneratorReactionPolicyVerification.Run | 24489 | 0 | 0 |
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

초기 Before 준비 실패3건도 숨기지 않고 보존했다. 첫 실패는 기본 내구도를0으로 단정한 검사 전제, 둘째는4칸 매칭을 만들어3칸 부분집합 헬퍼가 매칭을 찾지 못한 입력, 셋째는 가로 예약이 연결 대상 두 칸을 직접 타격하는데 피해0을 기대한 전제였다. 모두 검사만 수정했고 게임 규칙은 바꾸지 않았다. fixture-assumption/match-fixture/reservation-fixture의 log/results/execution.json으로 남겼다. 최종 Before는 종료0. RED는 동일8745행 비교 후 obstacle.generator 필수 정책 누락 ID 오류로 종료1. GREEN은 정책 불변성·한 번 조합·네 허용·16개 다른 메모리 정책의 같은 Get/Require/Allows·누락 ID 오류를 추가 확인해 종료0이다.

각 정확한 executeMethod·UTC 시작/종료·실제 종료 코드는 execution.json에 있다. 기존15종의 결과/값25파일은 백업→현재 실측 복사→원래 파일 복원 후 SHA256 동일을 확인했다. 기존 에셋을 저장하거나 UI Prepare/팩 Generate를 호출하는 검사 경로는 사용하지 않았다. 사용자 ServeredMeridian Editor 없음 확인 후 배치 Editor만 실행했고 NCloud_Unit_CV는 건드리지 않았다.

## 보존·미검증·남은 문제

보호1836파일 중 생산2파일만 변경, 나머지1834파일 SHA256 동일·삭제0·기존 meta 변경0. 새 GUID 중복0. 원본 레벨/팩/씬/프리팹/텍스처/리소스/Packages/ProjectSettings와 기존 enum 숫자를 보존했다. EF-05 예외 원복, EF-09 원본1254px3개와 Editor/월드 바닥 차이, EF-11 Draw/Reset/해제 책임도 그대로다. work와 커밋6b41ce4를 유지했다. 현재 단계는 미커밋이며 빌드/재패킹/이미지/사용자 Editor 종료/씬 저장은 하지 않았다.

전체 플레이/렌더링/실기기/IL2CPP/성능은 미검증이다. 자체 소스·호출부·실측 검토를 했으며 별도 에이전트 요청이 없어 독립 리뷰는 하지 않았다. 필수 범위 남은 실패는 없다. 허용 원인 조회는 기존6종 모두 연결됐지만 색 조건/집계·실행/저장/드론 전환은 아직 남아 있다.

증거: Logs/ElementFramework/Stage23의 protected-before/existing-work-before/head-before/git-before/progress, input-*.json, before/red/after log/execution/results/values 및 baseline-values.jsonl, run-new.ps1/run-regression.ps1, 각 기존15종 실제 실행/결과/값, values-summary/pack-summary/verified-results/historic-evidence-audit/preservation-audit/guid-audit/source-final/completion-audit.json. Logs는 Git 제외다.

## 다음 한 단계

EF-24 색 자물쇠 색 일치 조회 정책만 준비했다. 원인 허용 다음의 색 비교 한 조건을 정의로 연결하고 null/미정의 원인·외부 색 대체·Power/Hammer 우회·기존5종/발전기 경계를 보존한다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-24-color-match-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-24-color-match-policy-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-24-command.md). 다음 구현은 시작하지 않았다.

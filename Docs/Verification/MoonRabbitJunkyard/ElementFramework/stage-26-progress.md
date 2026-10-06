# EF-26 — 내구도형 예약 피해량 조회 연결 결과

상태: 완료. 새7793+기존135120=142913 PASS/0 FAIL. 최종19종 각각 별도 Editor 실제 종료0. work/6b41ce4와 EF-23~25 미커밋 작업을 유지하며 이번 단계도 미커밋이다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-26-reserved-damage-query-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-26-reserved-damage-query-goal.md) · [가이드](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경과 실제 호출부

ObstacleDamageRules.ReservedDamage 한 메서드만 변경했다. Crate/Scrap/Safe/ColorLock/Appliance는 같은 Get→RequireDamageAggregationPolicy→PerHitCell을 읽는다. true는 cells, false는 Min(1,cells)이고 마지막 Min(body.Durability,amount)는 그대로다. 유효5종은 내구도0/칸0에도 누락을 ID 포함 오류로 거절한다. Generator와 미지원 종류는 단락 조건으로 정책 조회에 들어가지 않고 기존 본체별 계산을 유지한다. 음수-1을0으로 보정하지 않았다.

MissionProgressRules.Project는 먼저 좌표 중복을 제거한 뒤 본체별로 묶어 ReservedDamage를 호출한다. 발전기는 별도 충전 분기로 처리한다. PendingBodyRemovals는 투영의 완료 예정 본체를 읽는다. DroneTarget 생성, 관리자 ExpectedDamage/ExpectedComplete/ExpectedCharge, QueryArea의 예약·완료 본체 제외도 이 투영을 공유한다. LiveReservations는 소실된 예약을 제외하고 Land는 반환·재선정·착탄 해제를 수행한다.

실제 타격은 PowerEffectResolution→DamageReaction.Evaluate→ObstacleDamageRules.Query→Apply를 사용하며 발전기는 GeneratorRules의 별도 충전·연결·철거 경로를 사용한다. 이 호출부와 Query/Apply/Remove/턴 기록·미션·드론 관리자는 수정하지 않았다. 기존 정책/카탈로그/정의·생성 계약도 그대로며 새 생산 타입/메서드/필드는 없다. 신규 Editor 검사와 meta만 추가했다.

## 실제 입력·경계·전후 비교

Stage26 JSON 입력29개와 Stage25의 기존 실행 입력을 그대로 재사용하고 시드는12345다. baseline/before/after-values.jsonl 전체4540행의 SHA256은 모두 A3F95C0A9AA54C57022004A1191528F34AD85A4A7361BFC281C2EEBA3C886505다. 결과 정규화나 지문/ID 제외 없이 전체 바이트가 같다. 입력29개 자체 해시도 유지했다.

- 직접 예약량1960행: 전체 초기 내구도(Crate1~6/Scrap1~5/Safe1~5/ColorLock1~3/Appliance1~9)×문맥10종×칸수-1/0/1/2/4/9/81. 문맥은 fresh/null/same-body/other-body/same-hit/other-cell/other-hit/next-turn/zero/deleted다. 내구도0이지만 점유가 있는 경우와 실제 Remove 뒤의 삭제 상태를 구분한다. 상태·비공개 턴 문맥·정의·전역 난수 무변경을 검사했다. 9/81은 직접 조회 수치 경계이며 실제2×2 점유를81칸으로 만든 것이 아니다.
- 투영448행: 요청 칸수0/1/2/4, 다른 본체 포함 여부, 중복 전달 여부를 전체 내구도에서 검사했다. 실제 좌표 중복 제거, 본체/칸 합산, 내구도 상한, 미션별 기여, 완료 예정 본체를 확인했다. 단칸 종류는 실제 점유 한 칸만 전달한다.
- 관리자56행: 실제 Request→예약과 Release 취소, 새 Request 뒤 실제 Remove→LiveReservations 제외→Invalidate→Land의 다른 본체 재선정·착탄 반환을 기록했다. 피해/완료 예상과 예약 보유 수를 검사했다. 기존 관리자28행도 재실행했다.
- 발전기/미지원 직접84행: Generator/-1/6/99와 내구도0/1/3·7칸수 조합은 테스트 메모리의 직접 경계다. 정책 강제 없이 기존 결과를 유지했다. 실제 발전기 충전·연결·철거·예약은 별도 GeneratorReactionPolicyVerification/GeneratorVerification의 실제 실행 사례로 확인했다.

기존 실제 실행1992행도 재사용했다. 피해126, 일반 인접18, 칸/hit108, 본체 반복608, 본체hit190, 로켓/폭탄/조합 범위 겹침126, 삭제 뒤 외부 반응330, 예약 투영36, 관리자28, 연결 대상 타격9, 팩 및 왕복 상태를 포함한다. 동일턴 반복/다음턴·hit0/같은칸/다른칸/다른hit·Power/Hammer·본체 미션1/2와 단계 효과를 보존했다.

MemoryPack188개 전체 base64/바이트와 본체 ID를 전후 비교했다. 레벨1/50/51/100/101은 실제 Encode→ReadLevel→Encode로 전체 필드·버전1/50레벨 주소를 확인했다. 같은 시드 원본/왕복5종 피해·미션·효과·문맥225행도 같다. 제작 에셋·배포 팩은 생성하지 않았다. 공급 고철은 기존 ScrapVerification.Data를 통해 재검증했다.

## RED→GREEN과 별도 Editor 실행

같은 ID의 카탈로그 항목만 테스트 메모리에서 true/false 정의로 바꾸고 실제 LegacyElementDefinitions.Get→ReservedDamage를 호출했다. 원래 등록 참조를 finally에서 정확히 복원한다. 공개 수정 API·정책용 새 생산 계약·문자열 실패는 추가하지 않았다.

원래 소스의 RED는 Crate/perCell=true/cells2에서 actual1/want2로 실제 종류 조건이 정책을 무시해 실패했다. 5 PASS/1 FAIL·종료1이며 finally 복원 PASS도 있다. GREEN은5종 모두 true/false 실제 예약량, -1/0/1/2, 누락ID/0칸 오류와 복원을 확인했다.

| 증거명 | executeMethod | PASS | FAIL | 종료 |
| --- | --- | ---: | ---: | ---: |
| before | Elements.Editor.ReservedDamagePolicyVerification.Before | 7732 | 0 | 0 |
| red | Elements.Editor.ReservedDamagePolicyVerification.Run | 5 | 1 | 1 |
| after | Elements.Editor.ReservedDamagePolicyVerification.Run | 7793 | 0 | 0 |
| aggregation | Elements.Editor.DamageAggregationPolicyVerification.Run | 75264 | 0 | 0 |
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

각 execution.json에 메서드/UTC 시작·종료/실제 종료 코드가 있다. 기존18종은 과거 출력31파일을 백업한 뒤 삭제하여 실제 새 출력 생성을 확인하고, 새 결과를 Stage26에 복사한 뒤 finally에서 원본을 복원했다. 전체 SHA256이 같다. 과거 ElementFramework 증거2825파일도 전부 바이트 동일하다.

초기 제한권한 실행은 로그 초기화 전 대기하여 진입하지 못했다. 프로세스54604의 검사 명령줄·LpcReply 대기·CPU 불변을 확인하고 이번 검사 프로세스만 종료했다(실제 종료-1). 정상 권한으로 별도 배치 Editor를 실행했다. 다른 프로젝트의 사용자 Unity는 유지했다.

첫 Before는7732 PASS/0 FAIL이었으나, 첫 전후 비교는7792 PASS/1 FAIL·종료1이었다. 원인은 Fixture의 반복 생성이 같은 입력 파일을 덮어쓰며 본체 ID가 달라진 검사 문제였다. first-before/first-after 결과·값·종료를 보존했다. File.Exists인 입력은 그대로 읽도록 검사만 수정하고 생산 소스를 전환 전 원문으로 복원해 Before/실측 actual·want RED부터 다시 실행했다. 연결 소스 복원 뒤 전체 GREEN을 얻었다. 실패를 제외하거나 ID/지문을 정규화하지 않았다.

## 보존·검토·남은 문제

보호1948파일 중 승인한 생산1파일만 변경하고 나머지1947파일 전체 해시 동일·삭제0·기존 meta 변경0이다. 해당 메서드 부분을 원문으로 역치환한 전체 파일이 전환 전 원문과 같아 Query/Apply/Remove와 기존 EF-23~25 변경 보존도 확인했다. 신규 GUID1개는 중복0이다. 기존 미커밋35파일을 백업했고 기존 단계 문서/검사/meta도 유지했다.

EF-05 예외 원복, EF-09 원본1254px3개 및 Editor/월드 바닥 차이, EF-11 Draw/Reset/해제 책임, enum 숫자·원본 레벨/팩/씬/프리팹/리소스·Packages/ProjectSettings를 보존했다. 커밋/푸시/빌드/재패킹/이미지/팩 재생성/사용자 Unity 종료/씬 저장 없음. 배치 Editor 컴파일/데이터 검사만 수행했다.

단계 계획의 에이전트 별도 요청 시에만 사용을 우선하여 자체 검토했다. 독립 리뷰, 전체 플레이/렌더링/실기기/IL2CPP/성능은 미검증이다. 확인된 필수 실패는 없다. 실제 Apply의 종류별 기록 조건과 이후 실행·저장·드론 전환은 남아 있다. 기존 동작 보존을 위해 이번 단계에 Apply 연결을 섞지 않았으며 새 콘텐츠의 전체 실행 전환 완료로 해석하지 않는다.

증거: Logs/ElementFramework/Stage26의 protected-before/existing-work-before/git-before/head-before/past-evidence-before/inputs-before, 기존 작업 백업, 소스 before/connected, input-*.json29개, before/red/after log/execution/results/values와 baseline-values, 첫 실패 및 제한권한 실행 증거, 기존18종 결과/값/이전 출력, run-new/run-regression/audit.ps1, verified-results/values-summary/pack-summary/preservation-audit/past-evidence-audit/historic-evidence-audit/guid-audit/self-review/completion-evidence.json. Logs는 Git 제외다.

## 다음 한 단계

EF-27 실제 타격 기록 집계 연결만 준비했다. Apply의 RegisterHit/RegisterDamage 선택을 기존 PerHitCell로 연결하며 내구도 감소·제거·미션·충전 흐름은 유지한다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-27-damage-record-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-27-damage-record-policy-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-27-command.md). 다음 구현은 시작하지 않았다.

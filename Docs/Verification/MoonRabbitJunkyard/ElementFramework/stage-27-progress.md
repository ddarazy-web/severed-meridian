# EF-27 — 실제 타격 기록 집계 연결 결과

상태: 완료. 새42279+기존142913=185192 PASS/0 FAIL. 최종20종 각각 별도 Editor 실제 종료0. work/6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783와 EF-23~26 미커밋 작업 유지. 커밋하지 않았다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-27-damage-record-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-27-damage-record-policy-goal.md) · [가이드](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경과 실제 호출부

PowerEffectResolution.Apply의 실제 타격은 DamageReaction.Evaluate→ObstacleDamageRules.Query로 반응을 확인한 뒤 ObstacleDamageRules.Apply를 호출한다. Apply의 기록 선택만 수정했다. Crate/Scrap/Safe/ColorLock/Appliance는 Get→RequireDamageAggregationPolicy→PerHitCell을 읽고 true이면 기존 RegisterHit(hit,cell.Coordinate), false이면 기존 RegisterDamage(index)를 호출한다. 발전기/미지원 직접 Apply는 단락 조건으로 정책을 강제하지 않고 기존 본체 기록을 유지한다. 유효5종 누락은 ID 포함 오류이며 기록·내구도를 변경하지 않는다.

기록→Durability--→0이면 Remove→GeneratorRules.TargetRemoved→남은 내구도 반환 순서는 유지했다. Remove의 점유 해제/미션 완료와 발전기의 충전·연결·철거 호출부는 수정하지 않았다. Query/ReservedDamage/RegisterHit/RegisterDamage/턴 구조/미션/드론 관리자·공급/낙하/봇/UI/표현/풀/저장도 그대로다. 기존 생산 파일의 Apply 전체를 이전 원문으로 치환하면 파일 전체가 정확히 일치한다. 새 생산 계약 없이 신규 Editor 검사와 meta만 추가했다.

## 입력·실측·동등성

시드12345, 저장 입력28개를 최초 생성 후 재사용했다. baseline/before/after-values.jsonl 전체13009행은 정규화나 본체 ID 제외 없이 바이트 동일하다. 각 파일436390024바이트, SHA256은 AA470F30B6E8C0C0F61D8785529D7ECBA4284DBA72F2278AF678F212637BBB44다. 입력 파일 자체 해시도 동일하다.

- 실제 타격 순서8064행: 전체 내구도(Crate1~6/Scrap1~5/Safe1~5/ColorLock1~3/Appliance1~9), 최초hit -1/0/7/8, fresh/same-body/other-body/same-hit/other-cell/other-hit/next-turn/zero/deleted 문맥9종과8번 타격. 실제 Evaluate→허용 시 Apply→후속 조회, 남은 내구도, 비공개 damagedObstacles/hitCells 집합, 제거 점유·미션, 상태/정의/규칙·전역 난수 보존을 검사했다. 0내구도 점유와 실제 삭제를 구분한다.
- 직접 Apply405행: 종류 -1/0/1/2/3/4/5/6/99 ×내구도-1/0/1 ×hit-1/0/7 ×fresh/null-context/null-cell/null-state/deleted. 정상 Query 경로와 구분한 테스트 메모리 경계다. 음수/0을 보정하지 않으며 null은 기존 NullReferenceException, 삭제는 기존 InvalidOperationException을 유지한다. hit0도 원래 튜플 기록은 남지만 HasHit의 hit>0 조건 때문에 다음 타격을 막지 않는다.
- 기존4540행 재사용: 직접 예약1960, 투영448, 관리자 예약28/재선정28, 발전기·미지원84, 기존 실제 실행1992행. 미션 기여/완료 예상·예약/취소/무효화/착탄 재선정과 실제 피해/효과·로켓/폭탄/조합 겹침·발전기 충전/연결/철거를 포함한다.
- MemoryPack188개 전체 base64/바이트와 본체ID를 비교했다. 레벨1/50/51/100/101의 Encode→ReadLevel→Encode, 버전1/50레벨 주소와 같은 시드 원본/왕복 상태225행도 보존했다. 제작/배포 팩은 재생성하지 않았다. 공급 고철은 기존 ScrapVerification.Data로 확인했다.

## 실제 RED→GREEN

같은 ID 카탈로그 항목을 테스트 메모리에서만 true/false 정의로 교체했다. 실제 Query→Apply→재조회/다른칸·hit·본체 타격의 내구도와 비공개 기록을 확인했다. 원래 등록 참조를 finally에서 ReferenceEquals까지 확인하며 정확히 복원한다. 공개 수정 API나 문자열로 만든 실패는 없다.

원래 소스 RED는 Crate/perCell=True/step=1/actual=Damage/want=AlreadyDamaged다. 최초 실제 Apply 뒤 같은hit·칸 재조회가 잘못 허용된 결과다. 4 PASS/1 FAIL·실제 종료1과 finally 복원을 보존했다. 최소 연결 뒤5종 true/false 및 누락ID 검사 GREEN을 얻었다. 이번 EF-27에는 다른 실패나 결과 제외가 없었다.

## 실행 결과

아래 표는 Stage27/verified-results.json의 실제 각 Editor 실행이다. before/red는 TDD 증거이고 최종 합계는 after+기존19종이다.

| 증거명 | executeMethod | PASS | FAIL | 종료 |
| --- | --- | ---: | ---: | ---: |
| before | Elements.Editor.DamageRecordPolicyVerification.Before | 42157 | 0 | 0 |
| red | Elements.Editor.DamageRecordPolicyVerification.Run | 4 | 1 | 1 |
| after | Elements.Editor.DamageRecordPolicyVerification.Run | 42279 | 0 | 0 |
| reserved | Elements.Editor.ReservedDamagePolicyVerification.Run | 7793 | 0 | 0 |
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

증거는 Logs/ElementFramework/Stage27에 있다. 각 *-execution.json에는 정확한 메서드·UTC 시작/종료·실제 종료 코드, *-results.txt에는 개별 판단, *-values.jsonl에는 실제 입력/응답·상태, *.log에는 Editor 로그가 있다. 기존19종의 과거 출력33파일은 백업 후 삭제하여 새 결과 생성을 확인했고, 새 결과를 Stage27에 복사한 뒤 finally에서 원본 전체 바이트를 복원했다.

## 보존·검토·미검증·남은 문제

보호1950파일 중 기존 ObstacleDamageRules.cs의 Apply만 변경, 나머지1949파일은 해시 동일하다. 기존 GUID/enum 숫자·EF-05 예외 원복·EF-09 원본 이미지와 표현 차이·EF-11 풀 책임을 보존했다. 과거 증거3034파일과 출력33파일도 해시 동일하다. 신규 검사GUID는1개이며 중복이 없다. 기존 미커밋41파일은 백업했으며 승인된 Apply와 단계 상태/인덱스 문서 갱신 외에는 동일하다. git diff --check 통과.

계획의 에이전트 별도 요청 원칙에 따라 자체 검토했다. 실제 호출부·finally 복원·누락 오류·음수/null/hit0·전체 결과/팩 비교·금지 파일 범위를 확인했고 발견된 미해결 문제는 없다. 별도 사용자 기기 성능/화면 검사는 하지 않았으며 이 데이터·호출 계약 검증으로 주장하지 않는다. 사용자 Unity를 종료하거나 씬을 저장하지 않았다. 빌드·커밋·푸시·재패킹·이미지/배포팩 재생성도 하지 않았다.

이 단계는 실제 기록 선택 한 책임의 완료다. 제거 미션 종류 매핑, 전체 실행/저장/드론 전환은 아직 남아 있다. 다음은 [EF-28 제거 미션 정의 연결 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-28-removal-mission-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-28-removal-mission-goal.md) · [전체 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-28-command.md)이며 다음 구현은 시작하지 않았다.

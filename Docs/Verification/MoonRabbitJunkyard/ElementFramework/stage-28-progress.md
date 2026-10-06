# EF-28 — 내구도형 제거 미션 정의 연결 결과

상태: 완료. 새43431+기존185192=228623 PASS/0 FAIL. 최종21종 각각 별도 Editor 실제 종료0. 원본/과거 출력/기존작업 최종 감사 통과. work/6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783와 EF-23~27 미커밋 작업을 유지한다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-28-removal-mission-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-28-removal-mission-goal.md) · [가이드](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경과 실제 호출부

ObstacleDamageRules.Mission의 종류 switch를 유효5종 Get→RequireRemovalMissionProfile→Kind로 바꿨다. Generator/미지원은 기존 MissionKind.Crate를 반환한다. 이 공용 경계를 직접 장애물 기여, 발전기 연결 대상 기여, 실제 Remove의 완료가 함께 사용한다. MissionProgressRules/Remove/Apply/Query/ReservedDamage/GeneratorRules/드론 관리자의 알고리즘은 수정하지 않았다.

ElementRemovalMissionProfile은 sealed 불변 MissionKind Kind만 보유한다. Crate/Scrap/Safe/ColorLock/Appliance 외 값은 ArgumentOutOfRangeException이다. ElementDefinition에 읽기 전용 프로필·Require와8인자 생성자를 추가했다. 기존2~7인자는 기존 참조·검증 계약을 유지하고 새 프로필은 null이다. Require 누락은 ID 포함 InvalidOperationException이며 기본값 대체가 없다. 기본5종에는 기존과 같은 미션을 등록했고 발전기는 null이다. 기존 카탈로그 불변 검사의 허용 필드 타입에 새 프로필만 추가했다.

실행 파일의 Mission 전체를 이전 원문으로 치환하면 파일 전체가 정확히 같다. 보호1952파일 중 기존 변경은 정의/기본 등록/불변 검사/Mission의4파일, 나머지1948파일은 동일하다. 신규 프로필·검사와 각각의 meta를 추가했다. 기존 임시 정의 생성자는 실제 조회 경계를 조사했으며 현재 구현에서 필요한 fixture 변경은 없었다. 기존 집계 true/false 타격 사례는 미션 제거에 도달하지 않아 별도의 미션 프로필을 강제하지 않는다.

## 실제 RED→GREEN과 실패 이력

- Before: 43305 PASS/0 FAIL·종료0. 변경 전 실제 결과를 저장했다.
- 계약 RED: 프로필 타입이 없는 실제 계약 부재로0 PASS/1 FAIL·종료1. 반사 조회를 사용해 컴파일 오류가 아닌 검사 실패를 확인했다.
- 계약 GREEN: 기본 등록과 생성 계약만 추가하고 Mission switch를 유지한 상태에서85 PASS/0 FAIL·종료0.
- 동작 RED: 같은 ID obstacle.crate.wood에 Scrap 프로필을 등록했다. 실제 기여 조회 후6번의 Query→Apply→Remove를 수행했지만 actualQuery=0/actualProgress=1,0, 기대값은Query=1/Progress=0,1이었다. 92 PASS/1 FAIL·종료1. 원래 등록 참조 finally 복원 PASS를 포함한다.
- 연결 GREEN:43431 PASS/0 FAIL·종료0. 직접5종과 발전기 연결 대상4종×충전 작동/직접 철거의8사례, 총13개의 같은ID 실제 상태를 기록했다. 기존과 다른 미션의 기여·완료와 연결 해제가 실제로 반영되고 원래 등록 참조를 정확히 복원했다. 유효 종류 누락의 직접 Mission/실제 미션 조회 ID 오류와 조회 무변경도 검사했다.

첫 Before 실행은 새 meta에서 GUID가 두 줄로 나뉘어 Unity가 검사 파일을 무시했다. 클래스 미발견·실제 종료1, 테스트 미진입이므로 계약 RED로 인정하지 않는다. import-failure.log와 execution.json을 보존했다. 신규 meta의 생성 GUID를 유지해 형식만 수정한 뒤 정상 Before부터 실행했다. 기존 meta를 재생성하지 않았고 검사 실패를 제외하거나 정규화하지 않았다.

## 전체 입력·실측·동등성

시드12345, 신규 저장 입력40개를 최초 생성 후 재사용한다. 신규 비교466행과 기존13009행, 전체13475행의 baseline/before/after 파일452032618바이트가 동일하다. SHA256은 E3D9AEC1F2436F501FBE3F420BA113951CB63244099DB3A7B8ECF143FE717E9A다. 본체ID/지문/상태를 제외하거나 결과를 정규화하지 않았다.

- 미션 조회112행: 전체 내구도(Crate1~6/Scrap1~5/Safe1~5/ColorLock1~3/Appliance1~9)×fresh/completed/deleted/null-context. 서로 다른 두 본체와 양쪽 미션을 가진 유효 레벨에서 기여/피해량/완료 예상과 조회 무변경을 확인했다.
- 실제 미션 타격306행: 제거까지 다음턴 실제 Query→Apply, 해당 미션만 단일 완료·다른 미션 유지·이미 완료된 미션 상한·삭제 재완료 없음·전역 난수 보존.
- 발전기48행: 실제 연결 가능한4종×필요충전3/4/5의 모든 충전 과정. 연결 대상 기여/완료 예상·실제 효과/완충 제거/연결 해제·다른 본체 보존.
- 기존13009행: 전체 내구도·같은/다른 본체/칸/hit·hit0/음수hit·턴/null/0/삭제·직접Apply·예약/기여/완료 예상/취소/무효화/재선정·발전기 충전/연결/철거·실제 피해/미션/효과·규칙/전역 난수 상태를 재사용했다. 정상 조회 경로와 인위적 직접 경계를 구분한다.
- MemoryPack188개 전체 바이트/본체ID와 레벨1/50/51/100/101의 실제 Encode→ReadLevel→Encode, 버전1/50구간, 동일시드 원본/왕복 상태225행도 일치한다. 원본 에셋/배포 팩은 생성하거나 재패킹하지 않았다.

정의 메타데이터 자체는 의도적으로 달라졌다. definitions-before/after.json에 원문 스냅샷을 별도로 저장했고 새 RemovalMissionProfile.Kind5개 이외 값은 동일하다. 이 차이를 전체 실행 결과 비교에서 숨기지 않는다. definition-delta.json에 양쪽 SHA256과 차이 범위를 기록했다.

## 별도 Editor 회귀 검사

기존20종 모두 완료했다. 아래 표는 verified-results.json의 실제 실행이며 각 execution.json에 UTC 시작/종료 시간이 있다. 과거 출력35파일의 백업→삭제→새 결과 생성→증거 복사→finally 원본 전체 바이트 복원과 과거 증거3244파일 보존을 최종 해시 감사했다. before/계약/동작RED는 TDD 증거이며 최종 합계는 after+기존20종이다.

| 증거명 | executeMethod | PASS | FAIL | 종료 |
| --- | --- | ---: | ---: | ---: |
| before | Elements.Editor.RemovalMissionProfileVerification.Before | 43305 | 0 | 0 |
| contract-red | Elements.Editor.RemovalMissionProfileVerification.Contracts | 0 | 1 | 1 |
| contract-green | Elements.Editor.RemovalMissionProfileVerification.Contracts | 85 | 0 | 0 |
| red | Elements.Editor.RemovalMissionProfileVerification.Run | 92 | 1 | 1 |
| after | Elements.Editor.RemovalMissionProfileVerification.Run | 43431 | 0 | 0 |
| record | Elements.Editor.DamageRecordPolicyVerification.Run | 42279 | 0 | 0 |
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

증거: Logs/ElementFramework/Stage28. 각 execution.json은 메서드/실제 종료와 시간을, results.txt는 개별 판단을, values.jsonl은 실제 입력/응답·상태를, log는 Editor 실행을 보존한다. 사용자 ServeredMeridian Editor는 실행 중이지 않았고 다른 프로젝트의 사용자 Unity는 유지했다.

## 검토·미검증·남은 문제

계획의 별도 요청 시에만 에이전트 사용 원칙에 따라 자체 검토한다. 현재 원문 대조·생성자 호환·finally 참조·실제 기여/완료·원본/GUID 범위에는 미해결 문제가 없다. 최종 전체 회귀/과거 출력 감사와 문서 링크/목표 체크/기존 작업 보존 감사도 완료했다. 미해결 검사 실패는 없다. EF-05 예외 원복·EF-09 이미지/표현 차이·EF-11 풀 책임, enum 숫자와 기존 미커밋을 보존한다.

사용자 기기 화면·성능 검사는 수행하지 않았으며 이 데이터 검사로 주장하지 않는다. 커밋/푸시/빌드/재패킹/이미지·배포팩 재생성/사용자Unity종료/씬저장은 하지 않았다. 제작 미션 집계·저장·공급·드론 정책/비행과 다른 요소 계열 전환은 여전히 남아 있다. 다음은 [EF-29 최초 장애물 미션 수량 정의 연결 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-29-initial-mission-supply-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-29-initial-mission-supply-goal.md) · [전체 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-29-command.md)이다. 다음 구현은 시작하지 않았다.

초기 rg 목록에서 제외된 Addressables 상태/패키지 서명5파일은 EF-27 원본 해시와 현재 해시가 동일함을 확인해 보호 목록에 보완했다. 최초 목록과 보완 근거를 모두 보존했다. 보호 총1952개/승인 변경4개/동일1948개이며 기존 미커밋47파일을 백업하고 승인된 계약·등록·Mission·불변 검사와 단계 상태/인덱스 문서 갱신 외 변경이 없음을 확인했다. 신규GUID2개는 유효·유일하다. git diff --check 통과.

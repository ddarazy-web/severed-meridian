# EF-30 — 회수캡슐 일반 인접 반응 정책 우선 적용 기록

상태: 완료. 새48386+기존275824=324210 PASS/0 FAIL. 최종23종 각각 별도 Editor 실제 종료0. 원본·과거 출력·기존 작업 감사 통과. work/6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783와 EF-23~29 미커밋 변경을 유지한다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-30-capsule-adjacent-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-30-capsule-adjacent-policy-goal.md).

## 변경과 실제 호출

ObstacleDamageRules.Query의 비파워/비망치 분기에서 Safe 거부에 `cause != DamageCause.AdjacentMatch` 조건만 추가했다. 정책이 AdjacentMatch를 허용한 경우만 이 종류 제한을 통과한다. 기본 Safe 정책은 계속false여서 현재 게임의 회수캡슐 피해 규칙은 같다. 자석 인접과 미정의 원인의 기존 Safe 거부는 유지한다. 다른 생산 코드는 수정하지 않았다.

DamageReaction.Evaluate는 활성/인접/벽 조건, 기존 자석 제한, 덮개와 보호를 확인한 뒤 장애물 Query에 위임한다. Query는 기존 정의의 RequireDamageSourcePolicy→Allows를 먼저 사용하며, 이후 색 조건·내구도·기존 집계 정책을 조회한다. 실제 적용은 BoardActionExecutor.Swap→매칭 소비→PowerEffectResolution.ApplyCore→Evaluate→허용된 Damage만 ObstacleDamageRules.Apply 순서다. Apply/Remove와 미션 진행은 수정하지 않았다. 호출부는 Logs/ElementFramework/Stage30/call-sites.txt에 기록했다.

새 CapsuleAdjacentPolicyVerification은 테스트 메모리의 비공개 카탈로그에 같은 Safe ID를 일시 교체하며, 공개 등록 수정 API나 생산 계약을 추가하지 않았다. 다른 정의·프로필은 원래 참조를 유지하고 SourcePolicy의 Power/MagnetAdjacent/Hammer도 그대로 복사한다. 모든 임시 등록은 finally에서 정확한 원래 참조로 복원한다.

## 실제 RED→GREEN

- RED: 유효한 회수캡슐 본체2개·제거 미션2의 저장 입력에서 일반 인접만true로 바꾸고 실제 Evaluate를 호출했다. actual None/0 대비 want Damage/1로5 PASS/1 FAIL·종료1이었다. 조회 상태/비공개 문맥/전역 난수 무변경과 finally 정확한 등록 참조 복원도PASS다.
- Before: 생산 수정 전47656 PASS/0 FAIL·종료0.
- GREEN:48386 PASS/0 FAIL·종료0. 같은 ID의 실제 인접 반응은Damage/1이다. 기본 등록false→true→false의 응답과 메시지 및 모든 조회 무변경을 검사했다.
- 실제 교환/매칭10건: 내구도1~5×벽 유무. 하나의 매칭이 두 본체에 인접하고 벽이 첫 본체 경로만 막는다. 허용된 반응만 실제 공통 적용을 거쳐 각 본체의 내구도1 감소, 턴당 본체1회 기록, 제거 미션과 제거 효과 본체수, EffectRecord의 AdjacentMatch/Before/After를 확인했다. 기본false의 같은10건은 피해 기록과Damage 효과가0이다. 강제Apply는 사용하지 않는다.
- 누락 정책: 유효 상태를 준비한 뒤 같은 ID의 SourcePolicy를null로 교체했다. 실제 조회의 InvalidOperationException에ID가 포함되고 기본값 대체가 없으며 상태/문맥/난수와 원래 등록 참조를 보존했다.

컴파일·제품 검사 실패는 없었다. 예상한 실제 RED만 실패했고 이후 새 검사는 통과했다. 기존 fixture는 수정하지 않았다. 새 검사의 턴 기록/효과 명시 조건은 Before 종료 후 추가했으며 기록하는 기본 결과의 필드나 값은 바꾸지 않았다. GREEN에서 같은 저장 입력을 그대로 재사용해 전체 바이트 동일성을 확인했다.

## 전체 입력과 동등성

시드12345, 새 저장 입력20개(조회10·실제 매칭10)를 최초 생성 후 재사용한다. 전후15023행/486707085바이트 전체 파일이 동일하다. SHA256: A1B8127AB7EF28A9F26AC01E891DE85100DC8D0B0A21F74121C0404F3EBA23BB. 상태·ID·응답·메시지를 정규화하거나 제외하지 않았다.

- 기본 조회455행: 내구도1~5×13조건×7원인. fresh/damaged/next/complete/null/wall/far/inactive/deleted/web/mold/protected/second-body를 검사한다. 원인은AdjacentMatch/Power/Hammer/MagnetAdjacent/-1/4/99다. 상태·비공개 문맥·입력·전역 난수 무변경을 확인했다.
- 기본 실제 매칭10행: 동일 입력·시드의 전체 실행 상태/결과/턴 문맥이 같다.
- EF-29 기존14558행: 최초 미션 수량672·공급 요약384·미지원20·null목록7 및 기존13475행을 재실행했다. 전체 공급 요약/문자열·전체 검증/Build 응답·피해/미션/효과·예약/기여/완료 예상/취소/무효화/재선정·발전기 충전/연결/철거와 규칙/전역 난수 보존을 포함한다. 입력은 기존 저장 파일을 사용한다.
- MemoryPack188개 전체 바이트/본체ID, 버전1/레벨1·50·51·100·101의 실제 Encode→ReadLevel→Encode와50레벨 구간/원본·왕복 상태225행을 포함한다. 원본 에셋이나 배포 팩을 재생성하지 않았다.
- 임시 허용 연결77행: 실제 같은 ID 반응1·전체 경계65·실제 매칭10·누락 정책1. 다른6원인의 전체 응답/메시지는 기본과 같고 원래 정의/프로필 참조를 복원했다.

## 별도 Editor 검사와 보존 감사

새 검사와 기존22종을 각각 별도 Editor에서 실행했다. 각 결과는 PASS>0/필수FAIL0/실제 종료0이며 정확한 기존22종 메서드를 EF-29 최종 실행 표와 대조했다. Before/RED는 아래 합계에 포함하지 않는다.

| 검사 | 실제 메서드 | PASS | FAIL | 종료 |
| --- | --- | ---: | ---: | ---: |
| after | Elements.Editor.CapsuleAdjacentPolicyVerification.Run | 48386 | 0 | 0 |
| initial-supply | Elements.Editor.InitialMissionSupplyVerification.Run | 47201 | 0 | 0 |
| removal-mission | Elements.Editor.RemovalMissionProfileVerification.Run | 43431 | 0 | 0 |
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

과거 결과/값39개를 각각 백업→삭제→새 생성→Stage30 증거 복사→finally 전체 바이트 동일 복원했다. 과거 증거3905개 전체도 시작 해시와 같다. 실제 입력·응답·시작/종료·메서드는 Stage30의 execution.json/results.txt/values.jsonl과 verified-results.json에 기록했다. 최종 전체 audit.ps1은 종료0이다.

보호1958개 중 생산 조건 변경1개만 승인 범위에서 달라졌고1957개는 동일하다. 현재 파일에서 Safe 일반 인접 조건만 메모리 문자열로 원복한 결과가 이전 파일 전체와 정확히 같다. 무시된 Android addressables_content_state.bin/meta와 패키지 서명4개도 SHA256이 같다. 기존 enum/GUID·EF-05 예외 원복·EF-09 표현·EF-11 풀과 원본 에셋을 유지한다. 신규 Assets 파일은 검사와meta2개뿐이고 GUID는 유효/유일하다. 기존 미커밋62개는 이번 조건과 승인된 완료/다음 문서 외에 보존했다. git diff --check도 통과했다.

## 미검증·남은 문제

사용자 기기 화면·성능, Android/iOS IL2CPP, 플레이어/Addressables 콘텐츠 빌드는 수행하지 않았으며 데이터 검사로 이를 보장하지 않는다. 커밋/푸시/빌드/재패킹/이미지·팩 재생성/사용자 Unity 종료/씬 저장을 하지 않았다. work와 기존 EF-23~29 미커밋 작업을 유지한다. 에이전트는 별도 요청 시에만 사용하는 계획에 따라 자체 검토한다.

이번 단계의 미충족 항목은 없다. 자체 검토는 Scope/default gate/실제 RED·GREEN·공통 적용·비공개 문맥·전체 결과와 최종 감사를 연결해 수행했고 Stage30/self-review.txt 및 완료 요건 감사에 증거를 남겼다. 다른 종류와 자석 경로까지 넓히는 수정이나 실패 fixture 보정은 필요하지 않았다.

다음 한 단계는 [EF-31 회수캡슐 자석 인접 정책 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-31-capsule-magnet-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-31-capsule-magnet-policy-goal.md) · [전체 복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-31-command.md)이다. 이번에 보존한 자석 전단/내부 거부를 Safe 한 종류에 한해 다음 단계에서 연결한다. 다른 종류/공급 생성/저장·제작 도구/드론 목표·비행/표현·리소스 전환은 남아 있다. EF-31 구현은 시작하지 않았다.

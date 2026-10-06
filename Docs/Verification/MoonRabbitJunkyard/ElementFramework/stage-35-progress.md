# EF-35 — 내구도 적용의 집계 정책 전달 기록

상태: 완료. 등록 내구도 조회/적용은 같은 전달 정의의 필수 집계 정책을 소비한다. 기존 직접 Apply는 구형 종류 판정을 호환 경계에 유지하고 같은 상태 변경 본문을 재사용한다.

최종 결과: 새 Run50875+기존27종534031=584906 PASS/0 FAIL, 별도 Editor28종 실제 종료0. 주·부가63출력 원문 복원과 원본/기존작업/과거 증거/전체 실행 바이트 감사 통과. 생산 변경은 ObstacleDamageRules.cs 한 파일, 신규 검사는 ElementDurabilityApplyPolicyVerification.cs와 meta다. 기존 C# 검사는 수정하지 않았다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-35-durability-apply-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-35-durability-apply-policy-goal.md).

## 보호와 실제 호출 조사

시작 기준 work/6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783. 보호1972파일/기존작업98파일/과거5544증거/동일 입력37파일을 start.ps1과 각 before 명세에 기록했다. Assets/Packages/ProjectSettings는 hidden/no-ignore까지 포함하여 원본/GUID/enum·EF-05/09/11·무시 Addressables/패키지 서명을 보호했다.

PowerEffectResolution.ApplyCore의 Charge/Damage 경로가 ApplyReaction을 소비한다. 충전은 반환값을 쓰지 않고 충전값을 재조회하고, 피해는 반환 내구도를 after로 소비한다. 직접 Apply 검사 호출도 유지했다. 실제 호출부·오류 순서·확정 최소 내부 서명은 계획서와 callers-before.txt에 기록했다.

## 검사 먼저 확보한 RED와 GREEN

새 Red는 실제 별도 Editor 종료1, PASS25/FAIL2. 유효 런타임/실행기를 구성한 뒤 같은ID 발전기를 크기2의 내구도/PerHitCell 정의로 임시 교체했다. 원래 내구도1을 그대로 사용했다. 실제 public Swap→로켓 발동/소비→Evaluate→등록 조회/적용에서 내구도1→0/점유 제거/충전 없음은 통과했지만 실제 효과의 HitGroup·좌표에 해당하는 칸 기록 대신 본체 기록이 남아 실패했다. 누락 집계 정책 Query의 ID 오류/무변경은 통과했지만 등록 Apply는 오류 없이 상태를 변경해 두 번째 실패를 남겼다. red-results/red-connection-values/Red.log/Red-execution에 원문을 보존한다.

Before는 실제 종료0, PASS50838/FAIL0. QueryDurability의 정책 조회 대상만 전달 정의로 바꾸고, ApplyDurability는 배치 확인→충전 배치 충돌 거부→필수 집계 정책→공통 ApplyCore 순서로 연결했다. 직접 Apply는 기존 Crate~Appliance 범위와 정책 조회 판정을 수행한 뒤 같은 ApplyCore를 호출한다. 기록→내구도 감소→Remove/미션→TargetRemoved/연결 수명→반환 본문은 하나이며 원래 순서를 유지한다.

Quick은 실제 종료0, PASS36/FAIL0. 새 전체 Run은 실제 종료0, PASS50875/FAIL0. 두 정책의 실제 교환에서 로켓 소비/발동, 피해 효과1→0/제거 본체 기록, 발전기 전체 점유 제거/충전 없음, 연결 대상 금속기둥9→7/미션0 유지/활성 연결0/충전 사건0을 확인했다. PerHitCell은 실제 hit·좌표 기록만 남기고 본체 정책은 본체 기록만 남긴다. 누락 Query/Apply는 ID 오류로 거절하고 상태/비공개 문맥/원본/규칙·전역 난수 무변경을 확인했다. 임시 교체4건은 모두 finally 정확한 원래 정의 참조로 복원했고 등록 짝은 변경하지 않았다.

## 정상 전체 비교와 의도적 차이

baseline/before/after 전체18182행·616394258바이트는 SHA256 53B9D59B4B742EAF2B021749FF00FF2A155E5741CE98FB4634159393A353912F로 동일하다. EF-34 전체 원문과도 동일하다. 정의 전후2756바이트는 SHA256 408B3C7DC00698AD84DDDA484AABC99DF2B6E8DFBAE4845BD1768A662B10902F로 동일하다. 실행 값을 정규화하지 않았다.

전체188 MemoryPack,225팩 상태, 정상 공개 교환40건, 직접 적용405건, 최초 공급672/공급 요약384건, sequence8064행과 예약/완료 예상/취소/무효화/재선정·발전기 기록을 확인했다. 같은 시드의 전체 상태/비공개 턴 문맥/난수·내구도/2×2/hit/턴·색5개/null·네 원인과 -1/4/99·벽/덮개/보호/삭제/비활성·피해/미션/효과·충전 임계/연결 철거/마지막 연결 제거·기존 자석 예외·공급/검증·본체ID/버전1/50구간은 기존 검사와 전체 바이트로 보존했다.

대체 비기본 정의의 실제 정책 교환2건과 누락 Query/Apply2건은 after-connection-values에 별도로 기록했다. 임시 발전기→내구도 행동은 기존 기본 발전기 행동과 달리 파괴되며, 그 타격 기록은 전달 정책을 따른다. 정상6종의 실행 결과·정의 메타데이터에는 차이가 없다.

## 모든 출력과 감사 중 발견한 문제

출력 작성 경로를 output-writers-inventory/required-output-paths/suites에 사전 조사했다. 모든 주·부가63출력 및 조건부 실패 경로를 백업→삭제→새 생성 시간/해시 확인→증거 복사→finally 원문 복원했다. 각 output-lifecycle에 시작/새 생성/원래 해시/복원 해시를 보존한다.

부가 정의 snapshot7개를 metadata-audit -RequireAll로 확인했다. EF-34/33의2개는 원문 동일하며, 과거 EF-28~32의5개에는 이미 도입된 EF-33 ReactionBehavior 토큰만 추가된다. 과거 원문과 새 원문/해시를 각각 보존하고 과거 파일은 원문으로 복원했다. 이 제한적인 메타데이터 비교를 실행/팩 결과에는 적용하지 않았다.

중간 new-values-audit 첫 실행은 회귀가 Stage34 after-values를 사전 백업 후 삭제한 시점에 원래 경로를 읽어 종료1이었다. 게임/fixture 실패가 아니라 감사 읽기 경로 문제다. 동일 원문의 reaction-apply-previous-values 백업을 사용하도록 감사만 수정하여 실제 재실행 종료0을 확인했다. audit-interleaving.json과 대화의 실제 오류 출력에 근거를 보존했다. 생산/원본/입력/기대값은 바꾸지 않았다. final-audit 준비 중 출력 수 상수 치환을 점검해63개로 수정했고 실제 최종 감사는63개 전부를 확인했다. 새 fixture 실패로 기대값을 완화한 사례는 없다.

## 요구사항별 최종 근거

| 요구사항 | 실제 근거 | 판정 |
| --- | --- | --- |
| 같은 전달 정의의 필수 집계 정책 조회/적용 | QueryDurability와 ApplyDurability→ApplyCore; source-audit exactExpectedText | 통과 |
| 기존 직접 Apply 서명/구형·부분 정의/반환·오류 보존 | 기존 판정 원문+공유 본문, direct-apply405행, record42279/0·reaction-query53053/0 | 통과 |
| 본문 하나·두 행동/등록 짝·공개 API/생산 리플렉션 없음 | 감소 본문1개, 다른 기존 C#484개 전체 해시 동일 | 통과 |
| 실제 공개 교환 RED→GREEN·원래 내구도1·원래 참조 복원 | Red25/2, Quick36/0, Run50875/0, 실제 효과의 hit·좌표와 피해/제거/미션/소비/연결·finally | 통과 |
| 누락 정책 ID 오류·상태/문맥/원본/난수 무변경 | 누락 Query/Apply 각각 실제 기록, Quick/Run | 통과 |
| 정상6종 메타데이터/경계·전체 실행·팩 유지 | 전체18182행/616394258바이트 동일·정의 전후 동일,188팩/225상태, coverage-audit | 통과 |
| 새 Run+기존 정확한27종 각각 별도 Editor·종료0/FAIL0 | verified-results28종/584906/0, 기존 EF34 메서드/검사 수 모두 일치 | 통과 |
| 주/부가/조건부 출력 생성·증거·원문 복원 |63개 output-lifecycle 및 final-audit 실제 종료0 | 통과 |
| 원본/GUID/enum·WIP/work/HEAD·과거/무시 상태·패키지 | protection-audit1972/98/5544/37 예상 밖0·새 Assets검사/meta2개·GUID유일·diff check0 | 통과 |
| 변경 최소화·제거/미션/연결/무효화/효과 순서 유지 | 원래 생산 원문에서 정확한 정책 전달/공유 추출만 변경, source-audit | 통과 |
| 다음 한 단계 문서·전체 복사용 명령문·다음 구현 없음 | EF36 계획/목표/명령문만 추가, 다음 Sources 없음 | 통과 |

## 실제 최종 별도 Editor 결과

| 검사 | 실제 메서드 | PASS | FAIL | 종료 |
| --- | --- | ---: | ---: | ---: |
| durability-apply-policy | Elements.Editor.ElementDurabilityApplyPolicyVerification.Run | 50875 | 0 | 0 |
| reaction-apply | Elements.Editor.ElementReactionApplyVerification.Run | 53295 | 0 | 0 |
| reaction-query | Elements.Editor.ElementReactionBehaviorVerification.Run | 53053 | 0 | 0 |
| durable-magnet | Elements.Editor.DurableMagnetPolicyVerification.Run | 54307 | 0 | 0 |
| capsule-magnet | Elements.Editor.CapsuleMagnetPolicyVerification.Run | 49166 | 0 | 0 |
| capsule-adjacent | Elements.Editor.CapsuleAdjacentPolicyVerification.Run | 48386 | 0 | 0 |
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
## 제약·미검증·다음 단계

source/protection/coverage/new-values/final-audit 실제 종료0. 승인된 생산1파일과 현재 단계 문서 외 예상 밖 변경0이다. 사용자 제약을 우선해 skill의 기본 커밋/워크트리/에이전트 흐름을 사용하지 않았다. 기존 work/HEAD를 유지했고 커밋/푸시/플레이어 빌드/Addressables 콘텐츠 빌드/재패킹/팩·이미지 재생성/사용자 Unity 종료/씬 저장/새 패키지·asmdef·종류·행동을 수행하지 않았다.

미검증: 요청에서 제외한 플레이어/Addressables 빌드와 실제 기기 화면은 검사하지 않았다. UI/비행/표현/풀·원본 에셋은 이번 변경 대상이 아니다. EF-35 요구사항의 남은 문제는 없다. 전체 요소 전환은 아직 완료되지 않았다.

다음 [EF-36 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-36-scrap-supply-mission-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-36-scrap-supply-mission-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-36-command.md)을 저장했다. LevelMissionRules.Supply의 고철 고정/유지 수량 대상 선택을 기존 제거 미션 프로필로 연결하는 한 책임을 묶는다. 공급 생성/이동/낙하/제거는 바꾸지 않으며 다음 단계 구현이나 검사 추가는 시작하지 않았다.
문서 작성 후 최종 protection-audit도 실제 종료0이다. 현재 단계 계획5/목표8 완료와 다음 단계 계획5/목표8 미완료·구현 없음, 문서8개/상대 링크165개와 전체 실행문2541자를 doc-audit로 확인했다. 최종 검토는 저자가 수행했다. 전달 정의 조회/적용과 직접 호환 경계·단일 본문/오류/순서를 원래 원문과 대조했으며 사용자 지시대로 별도 에이전트는 사용하지 않았다. 현재 승인 범위의 미충족 항목은 없다.

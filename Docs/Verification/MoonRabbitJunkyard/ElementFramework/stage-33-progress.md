# EF-33 — 반응 행동 키·조회 등록표 연결 기록

상태: 완료. 실행 결과26종 480736 PASS/0 FAIL·각 별도 Editor 실제 종료0. 전체 실행/입력/보존 및 문서 최종 감사 근거는 Logs/ElementFramework/Stage33에 있다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-33-reaction-behavior-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-33-reaction-behavior-goal.md) · [가이드](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 결과와 변경 범위

ElementDefinition에 불변 nullable ReactionBehavior를 추가했다. 기존2~8인자 생성자는 유지하며 없는 키를 추론하지 않는다. 기존 내구도형5종은 Durability, 발전기1종은 GeneratorCharge를 명시한다. ID/이름/기본 정책 bool/기존 프로필 값·참조는 유지했다. ElementBehaviorRegistry는 명시적으로 두 조회 위임을 등록하고 RequireReactionBehavior→TryGetValue→해당 위임 하나만 실행한다. 전체 순회·콘텐츠별 클래스·생산 리플렉션/문자열 실행·공개 등록 수정 API는 없다.

ObstacleDamageRules.Query의 Supports와 원인 정책 거부 뒤 등록표를 연결했다. QueryCharge는 기존 GeneratorRules.Query를 호출하고 QueryDurability는 기존 내구도 조회 원문을 유지한다. 구형 종류→정의 ID 어댑터는 유지한다. Apply/Remove/Mission/ReservedDamage의 원문, GeneratorRules의 적용·충전·연결, 공통 효과/드론/공급/저장 코드는 변경하지 않았다. 새 파일은 키/등록표/검사와 meta6개다. 기존 생산3파일, 검사7파일만 변경했다. 검사6개의 임시 정의는 원래 키를 전달하고 의도적 null을 보존했으며 ElementCatalog의 엄격한 불변 필드 허용 목록에 nullable 행동 enum만 추가했다. 기대값/검사를 낮추거나 삭제하지 않았다.

source-audit.ps1은 생산3파일을 추가 키/조회 연결 전 전체 원문으로 정확히 재구성해 비교한다. 기존supports/원인 거부·Safe 예외/ColorLock 색·집계·Apply/Remove/미션/예약 전체 원문 보존과 검사7파일의 최소 키 전달/불변 필드 목록 보완을 확인했다. 기존LF인 CapsuleAdjacent/InitialMissionSupply/RemovalMission도 유지했다.

## 실제 실패에서 GREEN까지

검사부터 추가했다. RED는 유효 런타임을 먼저 구성한 뒤 같은ID/기존 프로필 원래 참조를 가진 키 없는 기존8인자 정의로 임시 교체해 실제 Evaluate를 호출했다. 내구도5종은 Damage/1, 발전기는 Charge/1을 반환해 ID 포함 행동 누락 오류 기대6건이 실제 실패했다. 24 PASS/6 FAIL·종료1이며 각 finally 원래 등록 참조 복원을 확인했다.

Before는 동일 입력/시드 전체 기준50638 PASS/0 FAIL·종료0이다. 최초 Quick에서는 새 대체 행동 fixture의 발전기 현재 내구도를0으로 잘못 가정해 실패했다. 실제 저장 입력/RED 상태는 내구도1이었다. 신규 조회 전용 fixture의 런타임만 명시적으로0으로 준비한 후 readonly 스냅샷/정의 교체를 수행했다. None/0 기대와 제품 규칙·기존 fixture는 그대로 유지했다. 실패 결과와 quick-fixture-adjustment.md를 보존했다. 계약 Quick100/0, 확장 Quick2445/0·종료0과 전체 Run53053/0·종료0을 확인했다.

## 실제 연결과 전후 동등성

연결1096건: 누락 키6, 같은ID 대체 키6, 미지원 키6, 경계1008, 프로필 오류24, 원인 거부6, public Swap40이다. 대체 키 사례는 런타임 구성 후 호환 배치 프로필과 다른 행동 키를 가진 같은ID로 바꿔 실제 응답을 확인했다. 종류가 아닌 키로 조회한다. 이를 강제 적용해 성공을 만들지 않았다.

public Swap40건은 로켓 교환→발동/소비→공통 ApplyCore→Evaluate→등록 조회→기존 적용에서 전체 내구도28조합과 발전기 요구충전3/4/5의 현재충전12조합을 검증했다. 내구도/2×2 점유칸 피해와 제거 미션, 발전기 본체 수당1충전/임계 활성화/연결 철거·미션/효과·원본/규칙·전역 난수를 확인했다. 동일 키 기본 등록의 나머지 전체 경계/색5개·null/네 원인·-1/4/99/예약·취소·재선정/공급/검증은 기존 전체 실행 기록과 회귀 검사로 보존했다. 새 경계1008건은 선행 거부의 전체 응답/Amount/메시지 또는 실제 ID 오류와 상태/비공개 문맥/원본/난수 무변경을 검사한다.

전체 실행18142행/614480385바이트가 baseline/Before/After/EF-32 보호 해시와 동일하다. SHA256 F0D30C5DFF747D5C6F6EF88C9D95F018A3355292B7615F6DCE1F45762F8C4F84. 실행 기록을 정규화하지 않았다. 비공개 턴 문맥은 모든 비공개 필드와 튜플 값을 포함한다. 피해/미션/효과/예약·완료 예상/취소·무효화·재선정/발전기·공급/전체 검증과 MemoryPack188개 전체 바이트/본체ID/버전1/50구간, 팩 왕복 상태225행을 확인했다. 저장 입력37개/초기6개 해시도 동일하다. 상세 카테고리 수는 values-summary.json, connection-summary.json, 실제 기준/결과/실행 영수증을 따른다.

## 의도적 차이와 오류 순서

정의 메타데이터 snapshot에 Durability5/GeneratorCharge1 필드만 추가됐다. 정의 메타데이터 비교에만 정확히 이 새 필드를 제외하면 기존 전체 snapshot과 동일하다. 실행/상태/팩 비교에서는 이 처리를 사용하지 않았다. 기존 메타데이터 부분 생성자의 누락 키는 실제 등록 조회에 도달할 때 정의ID 오류로 드러난다. 미지원 키99도 기본 행동으로 대체하지 않는다. 호환되지 않는 내구도/충전 배치 조합 및 필수 배치 프로필 누락도 실제 선택된 행동에서 ID 오류로 거절한다.

Supports/비활성·삭제/덮개·보호/벽·거리/자석 전단/원인 정책 거부는 키 조회보다 먼저다. SourcePolicy 누락과 미지원 키가 함께 있어도 기존 원인 정책 오류가 먼저다. 유효 키의 기존 색/집계 프로필 누락 오류를 유지한다. Safe 미정의 원인·ColorLock 색 순서·전체 응답/메시지/집계와 EF-30~32 자석 및 발전기 예외는 기본 등록에서 유지했다. 잘못된 정의의 오류 노출 차이는 정상 게임 상태 변화가 아니다.

## 별도 Editor 최종 검사

| 검사 | 실제 메서드 | PASS | FAIL | 종료 |
| --- | --- | ---: | ---: | ---: |
| after | Elements.Editor.ElementReactionBehaviorVerification.Run | 53053 | 0 | 0 |
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

## 보존 감사와 감사 도구 보완

원본1964개 중1954개 동일, 선언한 생산3/검사7만 변경됐다. 신규GUID3개는 각각5ef2ac2ddfd846348626aab5f3c0ffb2/dea30c89a9fc4acca387d357c6a78803/f5f1d7911171489fbe33842f358bf634이며 중복0이다. work/6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783 및 기존81개 작업을 유지한다. 원본/GUID/enum·EF-05/09/11·무시된 Android Addressables 상태·패키지 서명4개는 전체 보호 목록과 해시로 확인했다.

기존25종의 주 결과/값45개는 백업→삭제→실제 실행 시간 내 새 생성→증거 복사→finally 전체 바이트 복원했다. 정확한 메서드는 EF-32 최종25종과 같고 검사 수도 모두 동일하다. 전체4794 과거 증거 감사에서 부가 정의 snapshot5개(Stage28~32)가 새 키 때문에 갱신된 것을 발견해 처음에는 실패했다. 신규 snapshot은 Stage33의 *-definitions.json에 생성 시각과 함께 보관했다. 사전 manifest와 현재 SHA256가 모두 같은 변경되지 않은 Stage29/30/32 definitions-before.json 사본에서 원래 바이트를 복구했다. 추가 키5/5/1/1/3 외 기존 전체 메타데이터도 동일함을 확인했다. 이5개가 처음부터 사전 백업/finally 복원됐다고 보고하지 않는다. 주45개와 구분한 auxiliary-restoration-audit.json 및 최초 불일치 목록을 보존한다. 향후 runner에는 부가5개도 백업/삭제/복원 대상으로 포함했다. 재감사에서4794개 전체 변경0을 확인했다.

PowerShell5 배열 파이프 열거와 혼합 자료형 합계 집계의 감사 도구 오류도 기대값을 바꾸지 않고 수정했다. 실제 결과26행을 직접 집계한480736을 기준으로 재확인했다. 제품/기존 테스트 코드 변경이나 새 실행 결과 정규화는 없었다. result-audit/regression-audit/final-work-audit/final-doc-audit, source-review-notes 및 requirement-completion-audit.json에 실제 감사 근거를 기록한다. 최종 문서 링크·현재5/8 완료/다음5/8 미착수·전체 명령문과 다음 구현 부재도 확인한다.

## 미검증과 남은 문제

기기/Android·iOS/IL2CPP·실제 Addressables 콘텐츠 빌드는 미검증이며 Editor 데이터 검증과 구분한다. 커밋/푸시/빌드/재패킹/배포 팩·이미지 재생성/사용자 Unity 종료/씬 저장은 하지 않았다. EF-33 조회 연결의 남은 구현·회귀·보존 문제는 없다. 모든 요소/공급/저장/드론 전환이 완료됐다는 의미는 아니다. 실제 적용 선택은 기존 응답별 직접 호출이며 다음 단계에서 다룬다.

다음은 [EF-34 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-34-reaction-apply-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-34-reaction-apply-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-34-command.md). 조회/적용 짝과 공통 효과 실행의 실제 적용 위임을 한 단위로 연결한다. EF-34 구현은 시작하지 않았다.
최종 문서 감사 실제 종료0: 문서13개/링크598개, 현재 계획5·목표8 완료, 다음 계획5·목표8 미착수, 전체 명령문2964자. 문서 갱신 뒤 final-work-audit도 실제 종료0으로 기존81작업 승인 범위 외 동일·work/HEAD·원본/GUID/Assets추가6·특수보호5개·diff check를 재확인했다. requirement-completion-audit.json은 원래 요구사항15묶음별 실제 근거와 완료, 장치/빌드 미검증 및 다음 구현 미착수를 기록한다.

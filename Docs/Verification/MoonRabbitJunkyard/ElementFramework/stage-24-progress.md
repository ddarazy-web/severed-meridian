# EF-24 — 색 자물쇠 색 일치 조회 정책 연결 결과

상태: 완료. work/6b41ce4와 EF-23 미커밋 변경을 보존했다. 현재 단계도 미커밋이다. 최종 새20861+기존38995=59856 PASS/0 FAIL, 필수17종 별도 Editor 각각 종료0. Before20832 PASS/0 FAIL, RED20758 PASS/1 FAIL·종료1은 새 계약 누락 검증이다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-24-color-match-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-24-color-match-policy-goal.md) · [가이드](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경과 호출 흐름

- Elements/Data/ElementColorMatchPolicy를 추가했다. RequiresMatchingColor(bool)만 보유하는 불변 조회 값이며 Allows(sourceColor,targetColor)는 true일 때 기존 nullable 색 일치, false일 때 색 제한 없음을 반환한다. 상태·문맥·Unity 참조를 저장하지 않는다.
- ElementDefinition에 읽기 전용 ColorMatchPolicy/RequireColorMatchPolicy와6인자 생성 경로를 추가했다. 기존2/3/4/5인자 경로와 ID/표시명 검증·배치/충전/원인 정책은 보존했다. 누락은 ID 포함 오류이고 기본값 대체는 없다.
- LegacyElementDefinitions의 obstacle.color-lock에 true 정책을 한 번 등록했다. 다른5종에는 색 정책을 강제하지 않는다.
- ObstacleDamageRules.Query의 색 자물쇠 비교 한 조건만 Get→Require→Allows로 바꿨다. 원인 허용→발전기 위임→비Power/Hammer 색 검사→내구도/집계 순서와 메시지는 그대로다. ElementCatalogVerification의 불변 필드 허용 목록 한 조건에 새 불변 타입만 추가했다.
- ColorMatchPolicyVerification와 새 meta를 추가했다. 신규 패키지/asmdef/제작 에셋/저장 DTO 변경은 없다.

실제 일반 매칭은 PowerEffectResolution.PushAdjacent가 원래 색으로 AdjacentMatch를 전달한다. 자석이 소비한 일반 블록은 그 원래 색으로 MagnetAdjacent를 전달한다. Power는 색을 우회하며 Hammer 역시 색 비교 블록에 들어가지 않는다. 외부 DamageReaction.Evaluate는 null 전달색을 출발칸 색으로 대체한 후 내부 Query를 호출한다. 미션/예약은 Power 반응을 공유하므로 색 자물쇠는 지정색이 달라도 직접 파워 대상으로 남는다. 직접 제거는 기존 Apply→Remove→GeneratorRules.TargetRemoved로 미션과 연결 해제/철거를 적용한다. 이 실행/미션/예약 소스는 변경하지 않았다.

## 실제 입력·전후 결과

저장 입력223개·시드12345·실제8447행을 Before/RED/GREEN에서 재사용했다. baseline/before/red/after-values.jsonl 전체 바이트 SHA256 동일이며 검사에서도 SequenceEqual로 비교했다. 결과는 Response/Amount/Message·전체 상태/규칙 난수 DrawCount·비공개 문맥/튜플·실제 효과/미션·예약·본체/연결 ID와 MemoryPack 전체 바이트를 포함한다.

| 색 자물쇠 입력 | 내부 Query | 외부 Evaluate |
| --- | --- | --- |
| 일반/자석 인접/-1/4, 같은 색 | Damage/1 또는 같은턴 AlreadyDamaged/0 | 동일 |
| 다른 색 또는 내부 null | None/0 · 자물쇠 지정 색 불일치 | null이면 출발칸 색으로 대체한 실제 결과 |
| 전달색null, 출발칸 같은 색 | 내부 None/0 | Damage/AlreadyDamaged |
| 전달색null, 출발칸 다른 색/null | 내부 None/0 | None/0 |
| Power/Hammer, 모든 색/null | 색 조건 우회, 기존 내구도/턴 결과 | 동일 |
| 내구도0, 색 일치/Power/Hammer | None/0 · 제거된 본체 | 동일 |
| 내구도0, 일반/자석/미정의 색 불일치 | None/0 · 자물쇠 지정 색 불일치 | 기존 순서 유지 |
| 벽/거리, 일반·자석 인접 | 내부 조건의 기존 결과 | Wall/None으로 먼저 거절 |
| 비활성/보호 | 내부 조건의 기존 결과 | None/Protected로 먼저 거절 |
| 실제 삭제 뒤 Empty | 점유 전제와 구분 | None/0 |
| 테스트 메모리에 삭제 점유만 복원 | 내구도0의 기존 메시지/None | 별도 내부 전제 사례 |

Power/Hammer와 미정의 원인은 기존처럼 거리·벽 검사를 우회한다. 색 비교보다 먼저 적용되는 외부 보호/비활성 경계도 유지했다.

핵심4320조합은 내구도1~3 ×지정색5 ×전달색5+null ×원인4/-1/4 ×8문맥/상태다. 문맥/상태는 null/fresh/same-turn/next-turn/removed/removed-same-turn/source-null/source-other다. 경계1440개는 벽/거리/비활성/보호 ×색6 ×원인4 ×내구도/지정색이다. 삭제 외부1080개/점유 복원 내부1080개와 다른5종+미지원2종84개를 포함해 Query6924개다. 각 조회는 상태·비공개 턴 문맥·카탈로그 규칙·전역 난수 무변경을 확인하고 색 자물쇠 내부 반응/양/메시지를 직접 검증했다.

실제 Power/Hammer120회는 내구도1~3·지정색5·같은턴 반복/다음턴/제거 후 반복을 확인했다. 본체별 턴당1피해·내구도 하한0·본체 미션1을 보존했다. 실제 일반 교환 매칭60개와 자석 교환60개는 색 일치/불일치·벽 유무를 구분했다. matching && !wall일 때만1피해/제거 미션이 증가하며 실제 행동/효과/전체 상태/문맥이 전후 같다.

예약 투영60건은 같은 좌표 DroneImpact를0/1/2/4회 생성하고 다시 중복 전달했다. 직접 피해는 고유 본체1, 충전0이며 실제 관리자 예약15건의 예상 피해1/완료(내구도1이면1)/후보 제외/취소 복원을 확인했다. 모든 조회는 상태/문맥/전역 난수를 변경하지 않았다.

발전기와 내구도3 색 자물쇠 연결 입력은 대상 직접 타격3회 후 Charge0/활성 연결0·ColorLock 미션1·무충전 철거를 확인했다. 기존 GeneratorReactionPolicyVerification 전체8745행/24489 PASS도 다시 실행해 내부 자석 허용·외부 거절·Durability0·4칸/본체별1충전·복수 연결 등 EF-23 경계를 보존했다.

MemoryPack36개(실제피해30+연결1+구간5)의 전체 base64/SHA256과 ID를 비교했다. 1/50/51/100/101은 실제 Encode→ReadLevel→Encode로 버전1/50구간 주소·전체 필드/바이트를 유지하고, 원본과 왕복 데이터의 같은시드 피해/미션/효과/문맥도 동일했다. 제작 에셋이나 배포 팩은 생성하지 않았다.

## 별도 Editor 실제 검사

| 증거명 | 실제 executeMethod | PASS | FAIL | 종료 |
| --- | --- | ---: | ---: | ---: |
| before | Elements.Editor.ColorMatchPolicyVerification.Before | 20832 | 0 | 0 |
| red | Elements.Editor.ColorMatchPolicyVerification.Run | 20758 | 1 | 1 |
| after | Elements.Editor.ColorMatchPolicyVerification.Run | 20861 | 0 | 0 |
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

Before는 첫 실행부터 종료0/필수FAIL0였다. 동일8447행을 확인한 RED는 '불변 색 일치 정책 존재' 계약 누락으로 종료1이다. GREEN은 불변성/한 번 등록/기존 생성자/누락 ID 오류와 같은 카탈로그의 true/false 정책·모든 지정색/전달색/null을 추가 확인해20861 PASS다. 최종 필수 실패는 없다. 감사 스크립트의 추가 블록 역변환에서 빈 줄 한 개를 잘못 처리한 것은 감사 도구만 수정해 원문 대조를 통과시켰다. 게임 소스 수정이나 목표 축소로 실패를 덮지 않았다.

각 executeMethod·UTC 시작/종료·실제 종료 코드는 execution.json에 있다. 기존16종 결과/값27파일을 백업→새 실측 복사→원래 파일 복원 후 SHA256 동일을 확인했다. 사용자 ServeredMeridian Editor 없음 확인 후 별도 batchmode/nographics에서만 실행했다. NCloud_Unit_CV와 사용자 에디터·씬은 건드리지 않았다. 새/재사용 메모리 헬퍼는 에셋/UI Prepare/Generate/저장 경로를 호출하지 않았다.

## 보존·미검증·남은 문제

보호1838파일 중 승인 기존4파일만 변경했고 나머지1834파일 SHA256 동일·삭제0·기존 meta 변경0이다. 새 GUID2개 중복0. 기존 EF-23 소스/검사/meta/보고서·목표·계획·명령문을 보존하며, 이번 단계에 필요한 기존 연결2파일과 공통 인덱스/상태 설명만 추가 갱신했다. EF-05 예외 원복, EF-09 원본1254px3개와 Editor/월드 바닥 차이, EF-11 Draw/Reset/해제 책임, enum 숫자·원본 레벨/팩/씬/프리팹/리소스·Packages/ProjectSettings는 그대로다.

work/6b41ce4 및 현재 미커밋 상태 유지. 커밋/빌드/재패킹/이미지/팩 재생성/사용자 Editor 종료/씬 저장 없음. 자체 소스·호출부·실제 응답 검토를 했고 별도 에이전트 요청이 없어 독립 리뷰는 하지 않았다. 전체 플레이/렌더링/실기기/IL2CPP/성능은 미검증이다. 필수 범위 남은 실패는 없다. 피해 집계/실행·저장·드론 전환은 남아 있다.

증거: Logs/ElementFramework/Stage24의 protected-before/existing-work-before/head-before/git-before/progress, 기존4파일 .before, input-*.json, before/red/after log/execution/results/values 및 baseline-values.jsonl, run-new.ps1/run-regression.ps1, 기존16종 실제 실행/결과/값, values-summary/pack-summary/verified-results/historic-evidence-audit/preservation-audit/guid-audit/source-final/completion-audit.json. Logs는 Git 제외다.

## 다음 한 단계

EF-25 내구도형 피해 집계 조회 정책 연결만 준비했다. 동일 Query 끝의 본체별/칸별 두 경로를 한 정책으로 연결하며 적용·예약 집계는 별도로 유지한다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-25-damage-aggregation-query-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-25-damage-aggregation-query-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-25-command.md). 다음 구현은 시작하지 않았다.

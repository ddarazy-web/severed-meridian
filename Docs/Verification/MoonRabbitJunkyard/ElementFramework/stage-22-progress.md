# EF-22 — 금속기둥 상자 피해 원인 허용 조회 연결 검증

상태: 완료. 필수15종 **14506 PASS / 0 FAIL**, 각 별도 Editor 종료0. 다음 구현은 미착수.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-22-appliance-damage-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-22-appliance-damage-policy-goal.md) · [가이드](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경과 호출부

LegacyElementDefinitions의 obstacle.metal-rod-box에 AdjacentMatch/Power/Hammer true·MagnetAdjacent false인 기존 불변 ElementDamageSourcePolicy를 한 번 조합했다. ObstacleDamageRules의 기존4종 허용 조회에 Appliance를 포함했다. 생산 변경은 이 두 파일 각 한 줄이며 Get→RequireDamageSourcePolicy→Allows를 사용한다. 원인 -1/4는 기존대로 Allows에 전달하지 않는다. 정책 누락은 ID 포함 오류이고 기본값 대체는 없다.

같은 hit의 같은 칸만 거절하는 기존 HasHit·RegisterHit와 ReservedDamage·Apply/Remove/Mission은 수정하지 않았다. hit0은 HasHit의 hit>0 조건 때문에 같은 칸 반복도 거절하지 않는다. 다른 칸/다른 hit에는 같은 턴에도 추가 피해가 가능하다. 본체별 턴당1피해로 바꾸지 않았다. 외부 DamageReaction.Evaluate의 거리/벽/비활성/보호·자석 인접 색 자물쇠 전용 예외와 GeneratorRules 위임 순서를 보존했다. 기존 생성자·배치2/9·발전기 충전 프로필·상자/고철/캡슐/자물쇠 정책은 그대로다.

실제 일반 매칭은 OriginalColor/HitGroup으로 인접 피해를 전달하고 PowerEffectResolution은 범위별 hit를 만들어 각 점유 칸에 공통 Apply를 호출한다. MissionProgressRules.Query/Project는 같은 반응과 ReservedDamage를 사용하며 중복 좌표를 제거한다. 마지막 칸 피해는 Remove 후 TargetRemoved로 연결 해제·발전기 철거를 진행한다. 이 호출부의 실행 소스는 변경하지 않았다. 정책 타입 불변 bool4개·객체 동일성/네 허용값·메모리16개 조합의 동일 카탈로그64회 조회·기존2/3/4인자 생성자의 누락 ID 오류3개도 확인했다.

## 저장한 실제 전환 전후 기준

입력242개를 생산 변경 전에 메모리 JSON으로 저장했다. After는 동일 JSON/UUID/시드12345를 읽고 아래 실측3040개 전체가 baseline-values.jsonl과 after-values.jsonl에서 바이트 동일하다.

| 기록 | 건수 |
| --- | ---: |
|query|2388|
|apply|126|
|pack|18|
|adjacent|18|
|hit-apply|108|
|reservation|36|
|overlap|126|
|overlap-pack|126|
|other-color|84|
|generator-target|9|
|generator-pack|1|

Query는 내구도1~9 × 점유4칸 × 원인4개/-1/4 × 새/null 문맥/hit0/같은hit 같은칸/같은hit 다른칸/다른hit/다음턴/제거 본체를 검사했다. 각 점유 칸의 실제 바깥 인접 출발에서 벽/거리/비활성/보호도4원인으로 비교했다. 다른5종·미지원 -1/6의 Type1/2·원인6개 응답을 별도로 기록했다. 모든 Query에서 공개 상태·규칙 난수 Seed/DrawCount/Version·비공개 문맥 HashSet/Dictionary/List/타격 튜플·Unity Random.state는 무변경이다. null 문맥에서는 null 출발색도 사용했다. 금속기둥의 색은 허용 조건이 아니다.

| 실제 입력 | 내부 Query | 외부 Evaluate |
| --- | --- | --- |
| 새 문맥, 일반 인접/Power/Hammer/-1/4 |Damage/1 · 폐가전 칸 피해 · 별도 타격 반복 가능|동일 |
| 같은hit 같은칸 |AlreadyDamaged/0 · 같은 타격의 같은 폐가전 칸|동일 |
| 같은hit 다른칸, 다른hit 같은칸, 다음턴 |Damage/1|동일 |
| hit0 같은칸 기록 |Damage/1|동일, hit0 중복 제한 없음 |
| null 문맥/null 출발색 |Damage/1|동일 |
| MagnetAdjacent |None/0 · 인접 피해 대상 아님|None/0 · 자석 인접 예외는 색깔 자물쇠만 적용 |
| 제거 본체, 일반 인접/Power/Hammer/-1/4 |None/0 · 제거된 본체|동일 |
| 제거 본체, MagnetAdjacent |None/0 · 인접 피해 대상 아님|기존 외부 자석 예외 |
| 인접벽, AdjacentMatch/MagnetAdjacent |기존 내부 반응|Wall/0 · 벽이 인접 매칭 피해를 차단 |
| 먼 출발, AdjacentMatch/MagnetAdjacent |기존 내부 반응|None/0 · 인접하지 않음 |
| 비활성 |기존 내부 반응|None/0 · 보드 밖 또는 비활성 칸 |
| 보호, 일반 인접/Power/Hammer |기존 내부 반응|Protected/0 · 이번 턴에 생성된 파워 보호 |
| 보호, MagnetAdjacent |None/0 · 인접 피해 대상 아님|보호보다 앞선 기존 자석 예외 거절 |

Power/Hammer는 기존처럼 거리·벽에 제한되지 않는다. 쿼리용 제거 본체는 테스트 메모리에 점유를 남기고 Durability0만 주입했다. 실제 제거는 네 칸 점유가 모두 없어진다. 벽은 저장 메모리 Flow, 비활성/보호/미지원/타격 기록은 테스트 메모리 경계 주입이며 제작 에셋을 바꾸지 않았다. 전체 Response/Amount/Message와 상태/문맥은 boundary-summary.json에 있다.

실제 Power/Hammer126회는 같은 턴의 새 hit 반복·다음턴·제거 후 두 번 반복을 확인했다. 내구도가 매 새 hit마다1씩 감소하고 제거 미션은1이다. 공통 Query→Apply108회는 첫 hit0/7·같은 칸 두 번·다른 칸·새 hit 같은 칸을 실제 적용해 hit0에서는 최대6피해, hit7에서는 같은 칸 중복 한 번만 거절해 최대5피해를 확인했다. 둘 다 내구도 하한0/본체 미션1이다.

일반 교환 매칭18개는 내구도1~9/벽 유무다. 매칭이 점유 칸 두 개에 인접하면2피해, 한쪽 벽을 막으면 나머지 칸만1피해다. 예약36개는 점유0/1/2/4칸의 DroneImpact를 두 번 전달한 실제 MissionProgressRules.Project로 확인했다. 좌표 중복을 제거해 피해 min(내구도, 고유 칸 수), 상태/문맥/난수 무변경이며 미션 예상/충전 반환도 전후 동일하다.

### 범위 중첩·유효 배치

기존 FixedObstacleVerification.OverlapData는 종료와 기존 파일 쓰기를 수행하므로 직접 호출하지 않았다. 그 메모리 Make/Obstacle/Combination/Build·공개 Activate/Swap 경로와 실제 효과 검사 방식을 새 검사에 재사용했다. 14개 실제 범위 × 내구도1~9 =126사례다. 가로/세로 로켓2칸, 단일 폭탄0/1/2칸, 로켓+로켓2칸, 로켓+폭탄2/3/4칸, 폭탄+폭탄1/2/4칸, 자석+자석4칸과 내구도 하한을 확인했다. 각 효과의 (HitGroup, Target) 중복0·단계별 Before/After·4칸 점유 유지 또는 전체 제거·본체 미션1도 확인했다.

계획의 폭탄 중첩1~4는 실제 제작 가능한 배치로 검증했다. 단일3×3 폭탄이2×2 네 칸을 덮는 중심은 장애물 내부라 폭탄을 동시에 배치할 수 없다. 그 가짜 점유를 만들지 않고 단일 폭탄1/2칸과 폭탄 조합3/4칸으로 폭발 피해1~4칸을 모두 검사했다. 이 조정은 검사 입력 선택이며 생산 규칙 차이는 없다. Before 첫 실행부터 필수 실패0이다.

### 연결 대상 제거와 저장

필요 충전3의 발전기와 내구도9 금속기둥 연결 입력을 사용했다. 같은 점유 칸을 새 턴마다 직접 타격해9번째에 전체 점유 제거·발전기 Charge0/활성 연결0·Appliance 미션1이다. 실제 효과와 비공개 연결 해제/철거 기록도 동일하다.

메모리 팩145개 전체 바이트/ID/원본 JSON을 실행 안에서 재 Encode 비교하고 base64 전후 비교했다. 직접 타격18개는 각각1835바이트, 연결1개는1921바이트다. 범위126개는 입력과 전체바이트/SHA256을 pack-summary.json에 기록했다. FormatVersion1/LevelsPerPack50을 유지했으며 배포 팩은 생성하지 않았다.

## 별도 Editor 검사

| 실제 증거명 | PASS | FAIL | 종료 |
| --- | ---: | ---: | ---: |
|after|5085|0|0|
|before|5055|0|0|
|bot|32|0|0|
|capsule-damage|592|0|0|
|catalog|1541|0|0|
|charge-placement|144|0|0|
|color-lock-damage|4452|0|0|
|crate-damage|696|0|0|
|crate|75|0|0|
|durable|210|0|0|
|fixed|142|0|0|
|generator|246|0|0|
|id|49|0|0|
|power|96|0|0|
|scrap-damage|960|0|0|
|scrap|186|0|0|
| 필수15종 합계, Before 제외 |14506|0|각0 |

before/after는 ApplianceDamagePolicyVerification.Before/Run이다. 기존14종의 정확한 executeMethod·UTC 시작/종료·실제 종료 코드와 입력/응답은 run-regression.ps1 및 각 execution/log/results/values에 있다. RED는 동일3040개 결과 비교 후 obstacle.metal-rod-box 필수 정책 누락으로5013 PASS/1 FAIL·종료1이었다. RED를 보존했다. Before 입력 준비45개는 After에서 재실행하지 않으며 After의 정책/비교75개로 최종5085 PASS다. GREEN 이후 필수 실패는 없다.

새 검사·재사용 헬퍼·기존 Data 진입점은 메모리 경로이며 UI Prepare/에셋 저장/Regression 임시 원본 생성 경로를 호출하지 않았다. 새 검사 Exit는 Application.isBatchMode로 보호했다. 사용자 ServeredMeridian Editor 없음 확인 후 각 batchmode/nographics Editor에서 실행했다. NCloud_Unit_CV는 건드리지 않았다. 기존 출력23개를 백업→새 실측 복사→원래 파일 복원하고 SHA256 동일을 확인했다. 이전 OverlapData 증거도 그대로다.

## 보존·미검증·남은 문제

보호1834개 중 기존 생산 파일2개만 각각 한 줄 변경, 나머지1832개 SHA256 동일·삭제0·기존 meta 변경0이다. 새 검사 GUID1개 중복0. 기존 EF-18~21 미커밋 소스·보고서·이전 작업을 보존했다. Apply/Remove 이후 본문은 HEAD와 동일하다. EF-05 예외 원복, EF-09 원본1254px3개 및 Editor/월드 바닥 차이, EF-11 Draw/Reset/해제 책임·원본 레벨/팩/씬/프리팹/리소스/Packages/ProjectSettings/enum 숫자는 그대로다.

work/598ba08 유지·변경 미커밋. 빌드·재패킹·이미지·팩 재생성·커밋·사용자 Unity 종료·씬 저장 없음. 자체 소스/호출부/실제 응답 검토를 했고 별도 에이전트 요청이 없어 독립 리뷰는 하지 않았다. 전체 플레이/렌더링/실기기/IL2CPP/성능은 미검증이다. 필수 범위 남은 실패는 없다. 발전기 허용 조회·공통 피해 실행/저장/드론 전환은 미착수다.

증거: Logs/ElementFramework/Stage22의 protected-before.json/existing-work-before.json/git-before.txt/progress.md, input-*.json/baseline-values.jsonl, before/red/after의 log/execution/results/values, boundary-summary.json/pack-summary.json/values-summary.json, run-regression.ps1 및 각 기존 검사 실행/결과/값, verified-results.json/historic-evidence-audit.json/preservation-audit.json/guid-audit.json/source-final.json/completion-audit.json. Logs는 Git 제외 대상이다.

## 다음 한 단계

EF-23 발전기 반응 원인 허용 조회만 준비했다. 내부 Charge와 외부 자석 예외를 구별하고 허용 조회 뒤의 기존 충전 위임을 유지하는 작은 연결이다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-23-generator-reaction-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-23-generator-reaction-policy-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-23-command.md). 다음 구현은 시작하지 않았다.

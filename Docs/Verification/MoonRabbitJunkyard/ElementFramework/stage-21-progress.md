# EF-21 — 색 자물쇠 피해 원인 허용 조회 연결 검증

상태: 완료. 최종 필수14종 합계 **9421 PASS / 0 FAIL**, 각 별도 Editor 종료0. 다음 구현 미착수.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-21-color-lock-damage-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-21-color-lock-damage-policy-goal.md) · [가이드](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경과 호출부

LegacyElementDefinitions의 obstacle.color-lock 정의에 네 원인 모두 true인 기존 불변 ElementDamageSourcePolicy를 한 번 조합했다. ObstacleDamageRules의 기존 Crate/Scrap/Safe 허용 조회 그룹에 ColorLock만 포함했다. 생산 변경은 이 두 파일 각 한 줄이다. Get→RequireDamageSourcePolicy→Allows를 사용하며 미정의 원인 -1/4는 기존대로 정책 Allows에 전달하지 않는다. 누락은 ID 포함 InvalidOperationException이고 기본값 대체가 없다.

색 조건은 기존 Query에 남겼다. AdjacentMatch/MagnetAdjacent와 미정의 원인은 지정 색 일치가 필요하고 Power/Hammer는 색과 무관하다. 외부 DamageReaction.Evaluate의 거리/벽/비활성/보호·자석 인접 예외 순서, 본체별 턴당1피해·Apply/Remove·Mission/예약 피해량을 변경하지 않았다. PowerEffectResolution은 일반 매칭 OriginalColor를 인접 조회에 전달하고 자석이 실제 일반 블록을 제거한 뒤 OriginalColor로 MagnetAdjacent를 생성한다. MissionProgressRules.Query/Project는 동일 반응/ReservedDamage를 사용한다. Apply 마지막 피해→Remove→GeneratorRules.TargetRemoved의 직접 파괴·연결 해제·발전기 철거 경로도 그대로다.

기존2/3/4인자 생성자·카탈로그·배치/충전/상자/고철/캡슐 정책은 유지했다. 금속기둥/발전기 허용 정책은 이번에 등록하지 않았다. 기존 불변 bool4개만 있는 정책·객체 동일성, 실제 네 허용값, 메모리16개 조합의 같은 카탈로그64회 조회, 기존 생성자3종 정책 누락 ID 오류를 검사했다.

## 실제 저장 입력과 전후 비교

생산 변경 전 메모리 입력188개를 JSON으로 저장했다. After는 동일 JSON/본체 UUID와 시드12345로 재구성했다. 아래 실제 기록2782개 전체가 baseline-values.jsonl과 after-values.jsonl에서 바이트 동일하다.

| 기록 | 건수 |
| --- | ---: |
|query|2424|
|apply|120|
|pack|30|
|adjacent|60|
|magnet|60|
|other-color|84|
|generator-target|3|
|generator-pack|1|

Query는 내구도1~3 × 본체색Type1~5 × 출발색 일치/불일치/null × 네 원인/-1/4 × null 문맥/새 문맥/같은턴/다음턴/제거/출발 칸 색 null을 실행했다. 각4개 외부 경계도 색3종/원인4개로 검사했다. 다른5종과 미지원 -1/6의 Type1/2 ×6원인도 실제 외부/내부/색 응답을 기록했다. 모든 Query에서 상태·규칙 난수 Seed/DrawCount/Version·비공개 턴 HashSet/Dictionary/List/타격 튜플·Unity Random.state가 무변경이었다. 예약 칸0/1/2/4는 살아 있는 자물쇠에서0/1/1/1, 제거 본체에서 모두0이다.

| 실제 입력 | 내부 Query | 외부 Evaluate |
| --- | --- | --- |
| 인접, 새 문맥, 같은 색 |Damage/1 · 본체 내구도 감소|동일 |
| 인접, 다른 색 |None/0 · 자물쇠 지정 색 불일치|동일 |
| sourceColor=null, 출발 칸 지정 색 |None/0 · 자물쇠 지정 색 불일치|출발 칸 색 대체 후 Damage/1 |
| sourceColor=null, 출발 칸 Color=null |None/0 · 자물쇠 지정 색 불일치|동일 |
| Power/Hammer, 색 일치/불일치/null |Damage/1|동일 |
| -1/4, 색 일치/불일치/null |기존 색 조건 검사|기존 출발 칸 색 대체 후 검사 |
| 같은 턴 피해 기록, 허용 색/원인 |AlreadyDamaged/0 · 본체별 턴당 최대 1 피해|동일 |
| 다음 턴, 허용 색/원인 |Damage/1|동일 |
| 제거 본체, 허용 색/원인 |None/0 · 제거된 본체|동일 |
| 제거 본체, 인접 다른 색 |None/0 · 자물쇠 지정 색 불일치|동일, 색 검사 우선 |
| 인접벽, AdjacentMatch/MagnetAdjacent |기존 색 조건/피해|Wall/0 · 벽이 인접 매칭 피해를 차단 |
| 먼 출발, AdjacentMatch/MagnetAdjacent |기존 색 조건/피해|None/0 · 인접하지 않음 |
| 비활성 |기존 색 조건/피해|None/0 · 보드 밖 또는 비활성 칸 |
| 보호 |기존 색 조건/피해|Protected/0 · 이번 턴에 생성된 파워 보호 |

Power/Hammer는 기존처럼 거리·벽에 제한되지 않는다. 명시적 다른 출발색은 출발 칸 지정색으로 덮어쓰지 않는다. 외부 null 대체와 내부 null을 같은 의미로 축소하지 않았다. 위 표는 실제 boundary-summary.json의 응답/Amount/Message에서 확인했으며 전체 상태와 비공개 문맥도 저장했다.

실제 일반 교환 매칭60개와 자석+일반 교환60개는 내구도3종/본체색5종/일치·불일치/벽 유무다. 일치하고 벽이 없을 때만1 감소하고 최종 제거 미션1이다. Power/Hammer120회는 본체색5종/내구도1~3에서 같은 턴2회 반복과 다음 턴, 제거·미션·효과를 비교했다. 자석 직접 범위는 일반 블록만 선택하고 자물쇠에는 MagnetAdjacent로 피해가 들어온다.

발전기 필요 충전3/연결 자물쇠 내구도3 입력에서 직접 타격3번째에 대상 제거·발전기 Charge0·점유/활성 연결0·ColorLock 미션1이다. 효과와 비공개 연결 해제/철거 기록도 동일하다. 메모리 팩31개 전체 바이트·ID/원본 JSON·규칙/전역 난수를 비교했다. 직접 타격30개는 각각1895바이트, 연결 입력은1921바이트이며 FormatVersion1/LevelsPerPack50이다. 실제 ID/SHA256은 pack-summary.json에 있다. 배포 팩을 만들지 않았다.

## 별도 Editor 검사와 실패 이력

| 검사 증거명 | PASS | FAIL | 종료 |
| --- | ---: | ---: | ---: |
|after|4452|0|0|
|before|4452|0|0|
|bot|32|0|0|
|capsule-damage|592|0|0|
|catalog|1541|0|0|
|charge-placement|144|0|0|
|crate-damage|696|0|0|
|crate|75|0|0|
|durable|210|0|0|
|fixed|142|0|0|
|generator|246|0|0|
|id|49|0|0|
|power|96|0|0|
|scrap-damage|960|0|0|
|scrap|186|0|0|
| 최종 필수14종 합계, Before 제외 |9421|0|각0 |

before/after는 ColorLockDamagePolicyVerification.Before/Run이다. 나머지 이름/실제 executeMethod는 각 execution.json과 run-regression.ps1에 있다. 캡슐/고철/상자·발전기/내구도/상자 배치·카탈로그/ID·고정 장애물/발전기/봇/고철/파워의 목표 진입점13개를 각각 실행했다. RED는 동일2782개 결과 비교 후 obstacle.color-lock 필수 정책 미등록 오류로4380 PASS/1 FAIL·종료1이었다. Before 입력 준비75개와 After 정책/비교75개가 달라 두 최종 PASS 수는 우연히 같다.

전환 전 자석 사례 준비 중 실패를 숨기지 않았다. 첫 단색 배경은 기존 시작 조건 위반(StartNotSatisfied)이었다. 5색으로 바꾼 뒤에는 한 벽 반대편의 다른 같은 색 블록 제거도 인접 피해를 만들었다. 실패 결과/로그/종료를 별도 보존하고 기존 FixedObstacleVerification처럼 나머지 일반 블록을 테스트 메모리의 곰팡이로 숨겨 한 방향 인접 벽을 격리했다. 생산 코드 수정 없이 최종 Before를 통과했다. 의도한 RED와 입력 준비 실패는 최종 필수 실패와 구분한다.

새 검사와 기존 Data 진입점/재사용 헬퍼는 메모리 LevelDefinition을 사용한다. UI Prepare/AssetDatabase 저장/Regression의 임시 에셋 생성 경로를 호출하지 않았다. Application.isBatchMode로 새 Exit 진입점을 보호했다. 사용자 ServeredMeridian Editor 없음 확인 후 batchmode/nographics 별도 Editor를 사용했다. NCloud_Unit_CV는 건드리지 않았다. 기존 출력21개를 백업·새 실측 복사·원래 파일 복원하고 SHA256 동일을 확인했다.

## 보존·미검증·남은 문제

보호1832개 중 기존 생산 파일2개만 변경, 나머지1830개 SHA256 동일·삭제0·기존 meta 변경0이다. 새 검사 GUID1개 중복0. EF-18~20 미커밋 소스/보고서와 이전 작업을 보존했다. Apply/Remove 이후 본문은 HEAD와 동일하다. EF-05 예외 원복, EF-09 원본1254px3개/Editor·월드 바닥 차이, EF-11 Draw/Reset/해제 책임 및 원본 데이터/팩/프리팹/씬/리소스/Packages/ProjectSettings/enum 숫자도 그대로다.

work/598ba08 유지·변경 미커밋. 빌드·재패킹·이미지·팩 재생성·커밋·사용자 Unity 종료·씬 저장은 하지 않았다. 자체 소스/호출부/실제 응답 검토를 수행했고 별도 에이전트 요청이 없어 독립 리뷰는 하지 않았다. 전체 플레이/렌더링/실기기/IL2CPP/성능은 미검증이다. 필수 범위 남은 실패는 없다. 금속기둥/발전기 허용 정책·공통 실행/저장/드론 전환은 미착수다.

증거: Logs/ElementFramework/Stage21의 protected-before.json/existing-work-before.json/git-before.txt/progress.md, input-*.json/baseline-values.jsonl, before/red/after의 log/execution/results/values 및 입력 준비 실패 증거, boundary-summary.json/pack-summary.json/values-summary.json, run-regression.ps1와 각 검사 실행/결과/값, verified-results.json/historic-evidence-audit.json/preservation-audit.json/guid-audit.json/source-final.json/completion-audit.json. Logs는 Git 제외 대상이다.

## 다음 한 단계

EF-22 금속기둥 상자 허용 원인 조회만 준비했다. 2×2 칸/hit별 피해 집계와 확대 범위 중첩은 보존하면서 허용 조회만 같은 정책에 연결한다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-22-appliance-damage-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-22-appliance-damage-policy-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-22-command.md). 다음 구현은 시작하지 않았다.

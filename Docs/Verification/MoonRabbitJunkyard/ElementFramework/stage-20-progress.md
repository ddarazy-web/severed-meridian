# EF-20 — 고물 회수 캡슐 피해 원인 허용 정책 연결 결과

상태: 완료. 2026-10-04, Unity6000.3.10f1, `work`/598ba08. 기존 EF-18/19 변경을 유지하고 캡슐1종의 읽기 전용 허용 조회만 연결했다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-20-capsule-damage-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-20-capsule-damage-policy-goal.md) · [가이드](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경과 책임

기존 ElementDamageSourcePolicy를 재사용했다. `obstacle.recovery-capsule` 정의에 Power/Hammer 허용·AdjacentMatch/MagnetAdjacent 거절 값을 한 번 준비했다. 기존 상자/고철의 Get→RequireDamageSourcePolicy→Allows 조회 분기에 캡슐을 포함했다. 생산 변경은 LegacyElementDefinitions와 ObstacleDamageRules의 각 한 줄이다.

정책/정의/카탈로그 타입·기존2/3/4인자 생성자·배치/충전 계약을 변경하지 않았다. ObstacleKind.Safe/MissionKind.Safe의 이름·숫자와 저장 ID를 유지했다. 상자·고철 정책과 다른3종의 동작도 유지했다. Apply/Remove/Mission/턴 집계/예약 피해량·공급/낙하·발전기 실행·드론·봇 DTO·UI/표현/풀·저장 코드는 수정하지 않았다.

정의된 네 원인만 정책에 전달한다. 미정의 DamageCause -1/4는 캡슐의 기존 None/0·인접 피해 대상 아님 의미를 그대로 유지한다. 상자/고철의 미정의 원인 허용과 다르며 한 종류의 의미로 통일하지 않았다. 미지원 ObstacleKind -1/6도 기존 의미를 보존한다. 누락 정책은 ID 포함 오류로 거절하며 기본값 대체는 없다.

## 실제 전환 전후 입력·비교

생산 변경 전 메모리 입력38개를 저장했다. After는 동일 JSON/본체 ID와 시드12345로 재구성하고 입력 UUID를 다시 생성하지 않았다. 전체 실제 기록484개가 전후 동일하다.

| 기록 | 건수 | 실제 확인 범위 |
| --- | ---: | --- |
| 외부/내부 Query·미션·예약 피해 |314|내구도1~5/네 원인/-1/4, null/새 문맥/동일턴/다음턴/제거 본체, 벽/거리/비활성/보호, 다른 종류/미지원 |
| 다른 종류 내부 색 조건 |84|상자/고철/색 자물쇠/금속기둥/발전기/미지원2종 × 원인6개 × Type1/Type2 |
| 실제 Power/Hammer 적용 |60|내구도1~5, 각 턴2회 반복·다음턴·최종 제거/미션/효과 |
| 실제 일반 블록 교환 매칭 |10|내구도1~5 × 인접벽 유무, 캡슐 피해 거절 |
| 연결 대상 직접 파괴 |5|5내구도 캡슐 제거와 연결 발전기 무충전 철거 |
| 메모리 팩 전체 바이트 |11|타격 입력10개와 발전기 연결 입력1개 |

314개 Query에서 공개 런타임 스냅샷과 비공개 TurnEffectContext 필드 지문, 규칙 난수 Seed/DrawCount/Version, Unity 전역 Random.state를 전후 비교했다. HashSet/Dictionary/List·타격 튜플 필드를 포함한다. Query는 문맥 기록이나 난수 진행을 변경하지 않았다. 다른 종류의 색 조건은 별도 내부84개 비교에 Type1/Type2를 사용하며 외부/내부 묶음 Query는 명시적인 Type1이다.

벽은 실제 메모리 Flow 설정으로 저장했다. 비활성·보호·미지원 종류와 점유를 남긴 제거 본체는 테스트 메모리에만 명시적으로 주입했다. 제작 에셋/생산 소스를 변조하지 않았다. 아래는 내구도3, 목표(4,4), 인접 출발(3,4)의0기준 입력이다.

| 입력 | 외부 Evaluate | 내부 Query |
| --- | --- | --- |
| 새 문맥, Power/Hammer |Damage/1 · 본체 내구도 감소|동일 |
| AdjacentMatch |None/0 · 인접 피해 대상 아님|동일 |
| MagnetAdjacent |None/0 · 자석 인접 예외는 색깔 자물쇠만 적용|None/0 · 인접 피해 대상 아님 |
| 미정의 원인 -1/4 |None/0 · 인접 피해 대상 아님|동일 |
| 이미 피해 기록된 본체, Power/Hammer |AlreadyDamaged/0 · 본체별 턴당 최대 1 피해|동일 |
| 다음 턴, Power/Hammer |Damage/1|동일 |
| 제거 본체, Power/Hammer |None/0 · 제거된 본체|동일 |
| 인접벽, AdjacentMatch |Wall/0 · 벽이 인접 매칭 피해를 차단|None/0 · 인접 피해 대상 아님 |
| 먼 출발, AdjacentMatch |None/0 · 인접하지 않음|None/0 · 인접 피해 대상 아님 |
| 비활성, Power/Hammer |None/0 · 보드 밖 또는 비활성 칸|Damage/1 |
| 보호, AdjacentMatch |Protected/0 · 이번 턴에 생성된 파워 보호|None/0 · 인접 피해 대상 아님 |
| 보호, Power/Hammer |Protected/0 · 이번 턴에 생성된 파워 보호|Damage/1 |

외부 벽/거리/비활성/보호 우선순위를 내부 정책으로 덮어쓰지 않았다. Power/Hammer는 기존처럼 벽·거리에 제한되지 않는다. 보호된 캡슐의 일반 인접 피해도 외부 보호 응답이 먼저이고, 자석 인접은 보호보다 앞선 자석 경계에서 거절된다. 제거 본체의 인접/미정의 원인은 내구도 검사보다 앞선 원인 거절 메시지다. 메시지·Response·Amount와 실제 상태 모두 전후 동일하다.

예약 칸 수0/1/2/4의 캡슐 피해는 살아 있는 내구도1~5에서0/1/1/1, 제거 본체에서 전부0이다. 미션 조회도 무변경이다. 실제 Power/Hammer는 첫 타격만1 감소하고 같은 턴 반복은 추가 감소하지 않는다. 마지막 턴에 점유가 제거되고 Safe 미션 Progress1이다. 일반 매칭은 벽 유무와 관계없이 내구도를 유지하고 미션0이다.

### 연결 대상 철거·저장

필요 충전3의 발전기를5내구도 캡슐에 연결한 저장 메모리 입력을 사용했다. 캡슐을 새 턴마다 직접 타격해5번째에 제거하면 발전기 Charge0·전체 장애물 점유 제거·활성 연결0·Safe 미션1이다. 타격별 상태/효과·비공개 문맥의 연결 해제/철거 기록도 전후 동일하다. 연결·충전·철거 실행 소스는 수정하지 않았다.

원본 JSON·배치 ID·규칙/전역 난수와 재 Encode 바이트를 실행 안에서 비교하고 base64 전체를 전후 비교했다. 직접 타격10개는 각각1895바이트, 발전기 연결1개는1921바이트다. FormatVersion1/LevelsPerPack50을 유지했다. 실제 UUID와 SHA256은 input-*.json/pack-summary.json에 있다. 배포 팩 파일은 생성하지 않았다.

실제 캡슐 정책의 네 반환값·반복 조회 객체 동일성을 확인했다. 기존 불변 정책의16가지 bool 조합을 메모리 정의로 만들고 동일한 ElementCatalog.Get→Require→Allows 경로64개를 확인했다. 기존2/3/4인자 정의의 누락3개는 각각 ID 포함 InvalidOperationException이다. 카탈로그/배치/충전 검사도 계속 통과했다.

## 별도 Editor 검사

| 검사 | PASS | FAIL | 종료 코드 |
| --- | ---: | ---: | ---: |
| CapsuleDamagePolicyVerification.Before, 생산 전 |527|0|0|
| CapsuleDamagePolicyVerification.Run, 최종 |592|0|0|
| ScrapDamagePolicyVerification.Run |960|0|0|
| CrateDamagePolicyVerification.Run |696|0|0|
| GeneratorPlacementVerification.Run |144|0|0|
| DurablePlacementVerification.Run |210|0|0|
| CratePlacementVerification.Run |75|0|0|
| ElementCatalogVerification.Run |1541|0|0|
| ElementIdVerification.Run |49|0|0|
| FixedObstacleVerification.Data |142|0|0|
| GeneratorVerification.Data |246|0|0|
| BotObservationVerification.Run |32|0|0|
| ScrapVerification.Data |186|0|0|
| PowerEffectVerification.Data |96|0|0|
| 최종 필수13종 합계 |4969|0|각0 |

Before 첫 실행527 PASS/0 FAIL·종료0으로 기준을 확보했다. RED는 전후484개 결과가 동일한 뒤 `obstacle.recovery-capsule`의 정책 누락으로 실패했다(520 PASS/1 FAIL·종료1, ID 포함 InvalidOperationException). Before의 입력 준비10개 검사는 After에서 저장 입력을 읽으므로 반복하지 않는다. RED 증거는 보존했고 GREEN 이후 필수 실패는 없다.

새 검사 준비 헬퍼·스냅샷 헬퍼와 기존 ScrapVerification.Data/PowerEffectVerification.Data의 메모리 호출 경로를 확인했다. UI Prepare/에셋 저장을 호출하지 않고 Logs만 출력한다. 모든 검사를 각각 별도 batchmode/nographics Editor에서 실행했다. 사용자 Editor에서 Exit 진입점을 실행하지 않았다. 기존12종 결과/값19개를 백업→새 실측 복사→원래 파일 복원하고 바이트 동일을 확인했다.

증거: Logs/ElementFramework/Stage20의 protected-before.json/existing-work-before.json/git-before.txt/progress.md, input-*.json/baseline-values.jsonl, before/red/after.log 및 execution/results/values, boundary-summary.json/pack-summary.json, run-regression.ps1 및 각 기존 검사 log/execution/results/values, verified-results.json/historic-evidence-audit.json/preservation-audit.json/guid-audit.json/source-final.json/completion-audit.json. 로그는 Git 제외 대상이다.

## 보존·미검증·남은 문제

보호1830개 중 이번 기존 생산 파일2개만 변경되고 나머지1828개는 SHA256 동일이며 삭제0이다. 기존 meta/GUID 변경0, 새 검사 GUID1개 중복0이다. 기존 EF-18/19 소스·보고서와 미커밋 작업을 보존했다. EF-05 예외 원복·EF-09 원본1254px3개 및 Editor/월드 바닥 차이·EF-11 Draw/Reset/해제 책임, 원본 레벨/팩/씬/프리팹/리소스·Packages/ProjectSettings를 보존했다. enum 숫자를 변경하지 않았다.

`work`/598ba08을 유지하고 변경은 미커밋이다. 빌드·재패킹·이미지·팩 재생성·커밋·사용자 Unity 종료·씬 저장은 하지 않았다. NCloud_Unit_CV 등 다른 프로젝트는 건드리지 않았다.

소스/호출부/diff와 실제 입력을 직접 검토했다. 별도 에이전트 요청이 없으므로 독립 리뷰는 수행하지 않았고 자체 검토는 검토 강도가 낮다. 전체 플레이·렌더링·실기기·IL2CPP·성능은 검사하지 않았다. 필수 범위의 남은 실패는 없고 색 자물쇠/금속기둥/발전기의 피해 정책·공통 실행·저장·드론 전환은 미착수다. 정책은 원인 허용만 다루며 피해량/색 조건/턴 제한/실제 실행을 대신하지 않는다.

## 다음 한 단계

**EF-21 색 자물쇠 피해 원인 허용 정책 연결**만 준비했다. 네 원인 허용을 같은 불변 정책으로 연결하고 일반/자석 인접의 색 일치 조건과 미정의 원인·null 색의 기존 의미를 유지한다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-21-color-lock-damage-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-21-color-lock-damage-policy-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-21-command.md). 다음 구현은 시작하지 않았다.

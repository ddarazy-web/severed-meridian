# EF-17 — 발전기 배치 크기·충전 수치 정의 연결 검증

상태: 완료. 2026-10-04, ServeredMeridian/Unity6000.3.10f1. work/HEAD b353077 유지. EF-13~16 미커밋 소스·문서를 보호했으며 커밋/푸시하지 않았다.

## 변경과 책임

```text
Elements/Data/ElementChargePlacementProfile.cs           충전형 배치 크기/허용 충전량 불변 값
Elements/Data/ElementDefinition.cs                       기존 계약 유지·충전 프로필 조합
Elements/Runtime/LegacyElementDefinitions.cs             발전기 정의 한 번 등록
Obstacles/Rules/LevelPlacementRules.cs                   Generator Size/배치 충전 검사 연결
Elements/Editor/Tests/GeneratorPlacementVerification.cs  실제 전후/충전/연결/계약 검사
Elements/Editor/Tests/ElementCatalogVerification.cs      충전 프로필의 불변성 검사 추가
Elements/Editor/Tests/CratePlacementVerification.cs      미등록 Generator 오류 fixture 교체
```

런타임1개/Editor 검사1개와 meta2개를 추가하고 기존 소스5개만 변경했다. 새로운 폴더/asmdef/패키지/제작 에셋은 없다.

ElementChargePlacementProfile은 sealed·get-only·readonly 정수 Size/MinRequiredCharge/MaxRequiredCharge로 구성한다. size/min은 양수, max는 min 이상이어야 한다. 기존 ElementPlacementProfile의 양수 MaxDurability 계약은 변경하지 않았다. ElementDefinition의2인자 메타데이터/3인자 내구도 프로필 생성자를 유지하고4인자 조합을 추가했다. RequireChargePlacement의 누락은 정의 ID 포함 InvalidOperationException이다.

호환 카탈로그에 obstacle.generator/고장 난 발전기/null 내구도 프로필/충전 프로필2·3~5를 한 번 준비한다. 반복 조회가 동일 정의 객체인 것을 검사했다. 기존 사전 Get을 사용하며 조회 시 할당/전체 종류 검색/Unity 원본 검색·전역 변경 API가 없다. Size와 ObstacleValueError의 발전기 배치 충전 범위만 이 프로필을 읽는다. 발전기의 MaxDurability0·다른5종 수치·미지원1/0·원본 종류 enum 숫자는 유지했다.0 반환은 기존 비내구도형 의미이며 양수 프로필을 완화한 것이 아니다.

편집/레벨 검사/연결 점유/런타임 구성/보드의 기존 Size 호출부는 수정하지 않았다. 실제 발전기 Query/Apply/전선 작동/제거/미션·낙하/공급/드론 코드는 그대로다.

## 전환 전후 실제 기록

생산 변경 전에 메모리 입력24개와 결과100건을 저장했다. 전환 후 새 Editor에서 같은 JSON 입력을 재사용하며 전체100건이 문자열 단위로 동일하다. 프로필 값3건/오류7건을 추가하여 최종110건이다.

| 범주 | 건수 | 실제 결과 |
| --- | ---: | --- |
| 기존6종/미지원 수치 |11| Crate1/6·Scrap1/5·Safe1/5·ColorLock1/3·Appliance2/9·Generator2/0; -1/6/999/int.MinValue/int.MaxValue는1/0 |
| 충전량 실제 편집 |5|2~6 중3/4/5만 Changed1;2/6 Changed0 |
| 원본 값 직접 검사 |5| 실제 SerializedObject requiredCharge2~6 변경; InvalidPlacementValue는2/6만1개 |
|2×2 실제 경계 편집 |5|(0,0)/(7,7) 허용; (8,8)/(9,8)/(-1,0) 거절 |
| 내부 벽 |1|(4,4)-(4,5) 벽이 발전기2×2 내부에 있으면 실제 편집 거절 |
| 중복 본체 |1| 서로 다른 발전기 ID2개 중첩,4칸 Find=-2·편집/레벨 검사 거절 |
| 실제 턴별 충전 |48| 대상4종×필요 충전3/4/5의 모든 충전 턴 |
| 완충/바이트 |12| Crate/Safe/ColorLock/Appliance×필요 충전3/4/5 |
| 대상 직접 파괴 |12| 같은 연결 fixture에서 대상을 직접 파괴해 무충전 발전기 철거 |

실제 충전 거절 메시지는 전후 모두 `필요 충전량은 3~5입니다.`이며 내부 벽은 `2×2 본체 내부에 고철 벽이 있습니다.`이다. 중복 오류는 `중복 배치: 기존 Inspector 목록에서 수정하세요.`다. 위 좌표는 코드 기준0부터 표시한다.

## 충전·피해·미션·연결·난수·저장

12개 연결 fixture는 실제 기존 편집 경로로 생성한 발전기/대상 본체 ID2개와 전선을 저장했다. seed12345로 구성한 런타임 초기 활성 연결은1이다. 매 새 턴의 Power Hit은 charge를 정확히1 증가시키며 requiredCharge 전에는 미션0/본체 유지, 완충 때 대상·발전기 점유 전체 제거/미션1/활성 연결0이다. 각 단계의 전체 런타임/효과/TurnEffectContext·GeneratorRecord 스냅샷48건이 전후 동일하다.

같은 원본으로 새 런타임을 만들어 연결 대상을 직접 파괴하는12건도 실행했다. 대상의 기존 최대 내구도만큼 새 턴 Hit을 진행하면 발전기 Charge0으로 자동 철거되고 미션1/연결0이다. 실제 상태·철거 문맥 기록도 전후 일치한다. 발전기 실행 소스는 변경하지 않았다.

충전의 규칙 Random.DrawCount는 무소비, Unity 전역 난수는 각 프로세스 안 전후 동일하다. 원본 JSON·배치 ID·메모리 Encode 바이트는 충전 후에도 동일하고 전환 전후도 같다. 버전1/50레벨 구간은 유지됐다. 실제 본체 ID·base64 전체는 input-*.json/after-values.jsonl/runtime-values-summary.json에 있으며 배포 팩 파일을 생성하지 않았다.

| 대상/필요 충전량 | 바이트 수 | 전후 동일 SHA256 |
| --- | ---: | --- |
| Crate/3 | 1921 | `7c07fa99e3a22bf5dad1dfd38b9f275b93f04d0b93f62203ae63704c02e6e68a` |
| Crate/4 | 1921 | `b6c7947130d7875876282095e93a34b96d98e77eada3af84c76ca629af7dffa1` |
| Crate/5 | 1921 | `640ae2aaac1052f20f3c37e248838ad23d33998b0da9a1b7a06a9d1aeecbbcd9` |
| Safe/3 | 1921 | `926cb55138c27f85a24602a716ff1476df3b3c6d0629ddf1861745c0ce147d15` |
| Safe/4 | 1921 | `351336af6c0825e104879cfe4405aec16c92669a201725721f17d0a2382b02aa` |
| Safe/5 | 1921 | `717b6f3ba72a3f60f98834dfaf9563d332c2af1d15fa5312013a114308fea511` |
| ColorLock/3 | 1921 | `03f82e8c4c6ab157c96880ccebb563322e285177b560d5b0ab51b12e80f6e9fc` |
| ColorLock/4 | 1921 | `b8cdad49e805aca7dcb4a9a455dc257aa652bf6233ae18273d74bb1017d78781` |
| ColorLock/5 | 1921 | `59b3e7e2d240440547181d0636914894d4d8ec605966ca0207ea8aff5e558cc6` |
| Appliance/3 | 1921 | `9e93fcf2c5e1c05b5b3b9cb184def4022c9666af56737972794977fbd0745b6a` |
| Appliance/4 | 1921 | `537376094640b67ab27451f9d4cb3896a20cbefd85f1b0e3d9165aa62b3b8a0e` |
| Appliance/5 | 1921 | `bc07ad262783c855a7adc841ad041da8c6e1790c2834a573681171c8806be4ba` |

## 프로필·누락·무효 수치

같은 카탈로그 Get→RequireChargePlacement 경로에 별도 메모리 프로필을 넣어2/3~5·1/2~8·3/4~4를 각각 조회했다. 원래 불변 프로필 객체와 모든 수치가 일치했다. 실제 기본 발전기 정의를 임의 변경하지 않았다.

- 2인자 element.no-charge 정의의 필수 충전 프로필 조회는 해당 ID 포함 InvalidOperationException이다.
- 빈 카탈로그의 같은 ID는 ID 포함 KeyNotFoundException이다.
- Size0/-1, Min0/-1, Max4<Min5는 각각 ArgumentOutOfRangeException이며 parameter/실제 수치를 포함한다.
- 기존 내구도 프로필 Size0/-1·Max0/-1 오류와2인자 정의/500개 카탈로그 계약도 계속 통과했다.

기존 EF-15의 미등록 Generator 오류는 이제 실제 등록되므로 빈 카탈로그의 유효 미등록 ID element.unregistered.fixture로 바꿨다. 오류 종류/ID 포함 검사 수를 유지하고 누락 정책을 기본값으로 대체하지 않았다. 카탈로그 구조 검사는 새 프로필 타입을 허용하면서 별도의 sealed/readonly/get-only 정수 검사1개를 추가했다. 기존 검사를 삭제하거나 실패를 숨기지 않았다.

## 별도 Editor 결과

| 검사 | PASS | FAIL | 종료 |
| --- | ---: | ---: | ---: |
| GeneratorPlacementVerification.Before, 생산 전 |130|0|0|
| GeneratorPlacementVerification.Run, 최종 |144|0|0|
| DurablePlacementVerification.Run |210|0|0|
| CratePlacementVerification.Run |75|0|0|
| ElementCatalogVerification.Run |1540|0|0|
| ElementIdVerification.Run |49|0|0|
| FixedObstacleVerification.Data |142|0|0|
| GeneratorVerification.Data |246|0|0|
| BotObservationVerification.Run |32|0|0|
| ScrapVerification.Data |186|0|0|
| 최종 필수9종 합계 |2624|0|각0|

첫 Before 실행은 새 검사의 중복 본체 JSON 조립 오류로 종료1이었다. 새 검사에서 중첩 좌표 객체를 잘못 자르는 방식 대신 본체 자체의 JsonUtility.ToJson을 사용하도록 수정하고 초기 실패 기록을 보존했다. 생산 소스 변경 전 최종 Before130 PASS/0 FAIL·종료0을 확인했다. 이후 RED는 컴파일 오류가 아닌 새 충전 프로필 부재(130 PASS/1 FAIL·종료1)이며 기존100건 비교는 통과했다.

모두 별도 Unity6000.3.10f1 batchmode/nographics Editor다. 기존8종의 결과/값은 실행 전 백업→새 실측을 Stage17에 복사→원래 파일 복원했고 바이트 일치를 확인했다.

증거: [Stage17](../../../../Logs/ElementFramework/Stage17)의 before.log/before-initial-results.txt/before-initial-execution.json/before-retry.log/before-execution.json/before-results.txt, input-*.json/baseline-values.jsonl, red/after.log 및 execution/results/values, run-regression.ps1, durable/crate/catalog/id/fixed/generator/bot/scrap.log와 각각 execution/results/values, verified-results.json/runtime-values-summary.json, protected-before.json/existing-work-before.json/git-before.txt/preservation-audit.json/source-final.json/progress.md/completion-audit.json. 로그는 Git 제외 대상이다.

## 보존 감사·한계

시작 시 보호한1820개 중 예상 소스5개만 변경됐고 나머지1815개는 SHA256 동일하다. 기존 meta/GUID 변경0, 새2개 GUID 중복0이다. 기존 미커밋 작업은 이5개와 단계 상태/목차 외에 유지했다. 기존 ElementPlacementProfile/ElementId/LegacyElementMap/ElementCatalog·원본 레벨/팩·씬/프리팹/아틀라스/Addressables·패키지/프로젝트 설정은 보존됐다.

EF-05 예외 원복 소스, EF-09 원본1254px3개와 Editor/월드 바닥 차이, EF-11 Draw/Reset/해제 책임을 보존했다. 빌드·재패킹·이미지·원본 변환·팩 재생성·커밋·사용자 Unity 종료·씬 저장은 하지 않았다. NCloud_Unit_CV 등 다른 프로젝트는 건드리지 않았다.

최종 소스/호출부/명세는 직접 검토했다. 계획의 에이전트 별도 요청 규칙에 따라 독립 리뷰는 하지 않았으며 작성자 자체 검토는 검토 강도가 낮다. 실행 스킬의 커밋/새 worktree/자동 정리 대신 기존work/미커밋 작업·검증 증거를 유지했다. 필요한 계약 검사 확장의 위험(가변 참조 유입)은 두 프로필 각각의 불변성 검사로 확인했다. 최종 검토에서 추가 결함은 없고 필수 실패는 남지 않았다.

배치 정의 연결만 완료했으며 피해/행동·제작/배포·드론은 전환하지 않았다. 전체 플레이/렌더링/실기기/IL2CPP/성능은 검사하지 않았다. 프로필은 배치 허용 충전 범위만 정의하며 실제 충전 실행/이벤트 정책을 대신하지 않는다.

## 다음 단계

**EF-18 나무상자 피해 원인 허용 정책 연결**이다. 첫 실행 조회 경계로 상자1종의 허용 원인만 불변 데이터로 연결하고 실제 피해량/턴 집계/적용/미션/예약 계산은 보존한다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-18-crate-damage-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-18-crate-damage-policy-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-18-command.md). 다음 구현은 시작하지 않았다.

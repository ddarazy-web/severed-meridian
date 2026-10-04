# EF-15 — 나무상자 배치 수치 정의 연결 검증

상태: 완료. 2026-10-04, ServeredMeridian / Unity6000.3.10f1. work 브랜치, HEAD b353077 유지. 시작 시 EF-13/14 미커밋 소스·문서가 있었으며 그대로 보존했다. 커밋/푸시하지 않았다.

## 변경과 조회 흐름

```text
Elements/Data/ElementPlacementProfile.cs           양수 Size/MaxDurability 불변 값
Elements/Data/ElementDefinition.cs                 기존2인자 + 프로필 조합/필수 조회
Elements/Runtime/LegacyElementDefinitions.cs       상자 정의를 한 번 준비하는 호환 경계
Obstacles/Rules/LevelPlacementRules.cs             Crate의 Size/MaxDurability만 연결
Elements/Editor/Tests/CratePlacementVerification.cs 전환 전 입력/결과와 새 계약 검사
Elements/Editor/Tests/ElementCatalogVerification.cs 조합된 프로필 불변 계약 검사 확장
```

새 소스3개와 meta3개를 추가하고 기존 소스3개만 변경했다. 폴더/asmdef/패키지 추가 없음. Elements는 불변 메모리 정의를 소유하고 LevelPlacementRules가 구형 종류를 조회한다. 편집/레벨 검사/게임 보드 호출부는 같은 공통 조회를 사용하며 개별 수정하지 않았다.

ElementPlacementProfile은 sealed·get-only·readonly 정수 필드이며 Size/MaxDurability 양수만 허용한다. 임의 모양·층·피해/행동 키는 없다. ElementDefinition의 기존 `(ElementId,string)` 계약은 null 프로필을 허용하는 메타데이터 정의로 유지하고3인자 조합 생성자를 추가했다. RequirePlacement는 누락 ID를 포함한 InvalidOperationException을 발생시킨다.

LegacyElementDefinitions는 private static readonly 카탈로그 하나에 `obstacle.crate.wood`/나무상자/1/6을 준비한다. Get은 LegacyElementMap과 기존 Dictionary 조회를 사용한다. 매 조회 생성/전체 순회/Unity 검색·외부 로딩·변경 가능한 전역 등록 API는 없다. 동일 정의 객체를 반복 반환하는 검사와 소스 검토를 수행했다. 기존 종류 분기는 과도기 연결이며 신규 콘텐츠의 최종 제작 방식은 아직 미구현이다.

## 전환 전후 실측

생산 변경 전 Before를 실행해 입력15개와 결과31건을 저장했다. 별도 Run에서 동일 저장 입력을 사용해 기존 결과 일치 후 새 프로필 부재로 종료1(RED)인 것을 확인했다. 구현 후 전환 전31건과 전환 후 처음31건이 문자열 단위로 모두 일치했다. 이후 프로필/오류10건을 추가하여 최종 관찰41건이다.

| 종류/enum 숫자 | Size | MaxDurability | 전후 |
| --- | ---: | ---: | --- |
| Crate0 | 1 | 6 | 동일, 정의 조회 연결 |
| Scrap1 | 1 | 5 | 동일, 기존 조회 유지 |
| Safe2 | 1 | 5 | 동일, 기존 조회 유지 |
| ColorLock3 | 1 | 3 | 동일, 기존 조회 유지 |
| Appliance4 | 2 | 9 | 동일, 기존 조회 유지 |
| Generator5 | 2 | 0 | 동일, 기존 조회 유지 |
| 미지원 -1/6/999/int.MinValue/int.MaxValue | 1 | 0 | 동일 |

- 실제9×9 보드 마지막 칸 `(8,8)`에 상자 내구도1~6을 각각 편집하여 Changed1/Skipped0, value/edit 오류null, 전체 레벨 검사0을 확인했다.
- 내구도0/7은 모두 Changed0/Skipped1, 실제 `내구도는 1~6입니다.` 오류로 거절했다.
- `(8,8)` 기존 상자 점유와 `(0,0)` 일반 블록 점유는 거절됐다. `(9,8)` 및 `(-1,8)`은 보드 범위 밖 오류다. 자기 인덱스0의 `(8,8)` 재편집은 허용됐다.
- 같은 칸에 ID가 다른 실제 본체2개를 메모리 입력에 만들어 Find=-2/점유 오류/레벨 검사 오류를 확인했다.
- 별도 메모리 카탈로그에 같은 ID의 프로필1/6·2/11·3/2를 각각 넣고 Get→RequirePlacement로 조회했다. 모두 해당 수치를 그대로 반환하며 원래 불변 프로필 객체와 같다. 전역 상자 정의를 변경하거나 다른 크기를 실제 게임에 적용하지 않았다.

## 누락/무효 오류

| 실제 입력 | 실제 결과 |
| --- | --- |
|2인자 정의 element.no-placement의 RequirePlacement | InvalidOperationException, 해당 ID·배치 프로필이 없습니다 |
| 빈 카탈로그 element.no-placement 조회 | KeyNotFoundException, 해당 ID |
| 구형 정의 경계의 미전환 Scrap 조회 | KeyNotFoundException, obstacle.scrap |
| Size0/-1, Max6 | ArgumentOutOfRangeException, size 및 실제0/-1 |
| Size1, Max0/-1 | ArgumentOutOfRangeException, maxDurability 및 실제0/-1 |

프로필 유효성 오류는 프로필 생성 시 아직 ID가 없는 수치 오류이며 parameter/Actual value를 포함한다. 정의/프로필 누락 오류는 모두 정의 ID를 포함하고 기본값 대체가 없다. Scrap의 기존 Size/MaxDurability는 이 미전환 정의 경계를 호출하지 않아 정상 유지된다.

## 피해·미션·난수·저장·인스턴스

메모리 fixture6개에 상자 내구도1~6, 좌표 `(4,4)`, Crate 미션1을 넣고 실제 편집으로 생성한 본체 ID를 저장했다. 전환 후 새 Editor에서 같은 JSON 입력을 읽어 seed12345로 런타임을 만들었다. 실제 Power Hit1회 후 내구도 d-1, 내구도1만 제거 미션Progress1, 나머지Progress0이다. 효과 기록·전체 런타임 스냅샷·규칙 난수 DrawCount가 전후 일치하고 Hit은 난수를 소비하지 않았다. Unity 전역 난수는 같은 프로세스 내 전후 값이 유지됐다.

각 fixture의 원본 JSON·본체 ID·메모리 Encode 바이트가 보존됐다. 기존 포맷버전1/50레벨 구간은 그대로다. 생성된 메모리 바이트는 JSONL의 base64로 기록했으며 원본 팩 파일을 생성/변경하지 않았다.

| 초기 내구도 | 보존된 실제 본체 ID | 바이트 수 | 전후 동일 SHA256 |
| ---: | --- | ---: | --- |
| 1 | `5bd7e541556842ecb088a7a9eb999a94` | 1895 | `5b1e575e1af10b8fea758fc49d3df562622feffcbf3e21c48ccbff4717d03a36` |
| 2 | `96b1b3c647684304a704ecfd28453e84` | 1895 | `f43b405da6a15983c5f5fe8bcfa74875a091dc3e51f10b83f872e214697c7dbf` |
| 3 | `490e92e9c0f34939915fb1f5aa2810de` | 1895 | `66ed542f582cd3ffa968da9564af931d6942b28d4a48a62c1ac768db97407ec9` |
| 4 | `8444d676bf5b402faab3976a8f6f2ce8` | 1895 | `d64b384c61a1ca5e1fd670ce7f6d9d643b300339fda96385dbef021ee59d4e64` |
| 5 | `9a5b527adaf947cd88bd9361034534b3` | 1895 | `b6e9634a13760afb77c4c1217730c74d695c915768c649dee21daf8035f00704` |
| 6 | `cbf4022966a14767919394789e2f1cfa` | 1895 | `66fa60e576dfe9c81a51305799f845b3e0ef2dce012ae7556f673d1a66879e6a` |

ID 기존 검사는 Generator/Crate의 실제 연결과 표시명/배열순서/본체 ID가 다른 입력도 각각 정상 처리함을 확인했다. 정의 ID를 본체 ID로 치환하지 않았다.

## 실행 결과

| 별도 batchmode/nographics Editor 검사 | PASS | FAIL | 종료 |
| --- | ---: | ---: | ---: |
| CratePlacementVerification.Before, 생산 전 | 57 | 0 | 0 |
| CratePlacementVerification.Run, 최종 | 75 | 0 | 0 |
| ElementCatalogVerification.Run | 1539 | 0 | 0 |
| ElementIdVerification.Run | 49 | 0 | 0 |
| FixedObstacleVerification.Data | 142 | 0 | 0 |
| BotObservationVerification.Run | 32 | 0 | 0 |
| 최종 필수5종 합계 | 1837 | 0 | 각0 |

카탈로그 검사의 기존1538 항목을 유지하고 조합한 프로필 자체의 readonly/get-only 정수 계약1개를 추가했다. ID/표시명만 허용하던 구조 검사는 프로필 타입까지 확장하되 불변성 검사를 약화하지 않았다. 카탈로그 공개 Count/Get와 기존2인자/500개 조회/입력독립/ID 오류 검사는 유지됐다.

증거: [Stage15](../../../../Logs/ElementFramework/Stage15)의 before-retry.log/before-execution.json/before-results.txt, input-*.json/baseline-values.jsonl, red.log/red-execution.json/red-results.txt, after.log/after-execution.json/after-results.txt/after-values.jsonl, catalog/id/fixed/bot.log 및 각 execution/results, verified-results.json, protected-before.json/existing-work-before.json/git-before.txt/preservation-audit.json/progress.md/completion-audit.json. 로그는 Git 제외 대상이다. 과거 Stage13/14 및 기존 fixed/bot 결과는 백업→새 실측 복사→원래 내용 복원했고 바이트 일치를 확인했다.

검사 준비 중 첫 컴파일에 Simulation namespace 누락이 있어 새 검사 코드에서만 보완했다. 구현 전 RED는 컴파일 오류가 아닌 실제 프로필 부재 실패(58 PASS/1 FAIL, 종료1)다. 기존 검사 실행 래퍼는 fixed/bot의 hashtable Values 이름 충돌로 불필요한 values 파일 복사 경고를 냈다. 두 검사의 실제 종료0·필수 FAIL0·results 복사/과거 결과 복원은 정상이며 각각의 실측 파일과 복원 바이트를 별도로 검사했다. 경고를 테스트 실패로 숨기지 않았다.

## 보존 감사와 한계

시작 시 보호한 파일1812개 중 예상 변경3개(ElementDefinition/ElementCatalogVerification/LevelPlacementRules)만 다르며 나머지1809개는 SHA256 동일하다. 기존 meta/GUID 변경0, 새 meta3개의 GUID 중복0이다. 기존 미커밋 소스·문서의 변경은 이번3개 및 단계 상태/목차 갱신에만 제한했다. EF-13/14 과거 보고서/목표/명령문 등은 보호했다.

EF-05 종료 예외 원복 소스, 피해·미션·낙하·공급/드론/봇 DTO/월드·에디터 표현/풀/씬·프리팹/아틀라스/Addressables/원본 데이터/기존 팩/패키지·프로젝트 설정의 파일 내용은 그대로다. EF-09 원본1254px3개·Editor/월드 바닥 차이와 EF-11 Draw/Reset·해제 책임은 그대로 유지했다. 플레이어/콘텐츠 빌드·재패킹·이미지·원본 변환·팩 재생성·커밋·사용자 Unity 종료·씬 저장을 하지 않았다. NCloud_Unit_CV 등 다른 프로젝트 Unity는 건드리지 않았다.

계획과 소스를 단계 종료 시 직접 재검토했다. 사용자 계획의 에이전트 별도 요청 규칙에 따라 하위 에이전트는 사용하지 않았으며 작성자 자체 검토라 독립 리뷰보다 검토 강도가 낮다. 계획 실행 스킬의 커밋/새 worktree/자동 정리 대신 기존work/미커밋 상태·증거를 유지했다. 새 구조 검사는 프로필 조합에 맞춰 확장했고, 이 변경의 위험인 가변 참조 유입을 별도 프로필 불변 검사로 검증했다.

이번은 대표1종 배치 수치 소비 경계만 검증했다. 제작 ScriptableObject/배포 DTO/행동·피해 정책/표현/리소스 준비/드론 변경은 미구현이다. 전체 플레이/렌더링/실기기/IL2CPP/프레임 성능은 검사하지 않았다. 명목상 구조 보존을 실제 게임 화면 검증으로 확대 해석하지 않는다. 필수 검사의 남은 실패는 없다.

## 다음 단계

**EF-16 나머지 내구도형4종 배치 수치 연결**이다. 같은 Size/MaxDurability 계약의 데이터 추가와 배치 조회만 묶고 발전기의0 내구도/충전은 분리한다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-16-durable-placement-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-16-durable-placement-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-16-command.md). 다음 단계 구현은 시작하지 않았다.

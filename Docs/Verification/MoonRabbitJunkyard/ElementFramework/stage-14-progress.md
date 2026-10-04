# EF-14 — 읽기 전용 정의 메타데이터·카탈로그 조회 검증

상태: 완료. 2026-10-04, ServeredMeridian / Unity6000.3.10f1. 작업 브랜치 work, HEAD b353077 유지. 시작 시 EF-13의 미커밋 소스·문서가 있었으며 되돌리거나 커밋하지 않았다.

## 변경과 소유권

기존 Elements 아래 런타임2개와 Editor 검사1개, 각 meta3개만 추가했다. 기존 EF-13 코드/폴더/GUID는 보존했다.

```text
Assets/Scripts/Features/Elements/
  Data/ElementDefinition.cs               ID·표시명 불변 메모리 값
  Runtime/ElementCatalog.cs               생성 시 독립 ID 색인, Count/Get
  Editor/Tests/ElementCatalogVerification.cs  계약·오류·입력 독립성·500개 조회
```

ElementDefinition은 sealed 일반 C# 클래스이며 유효 ElementId와 원문 DisplayName의 get-only 속성만 보유한다. Unity Object/가변 배열/원본 에셋/실행 상태 참조가 없다. default ID와 null/빈/공백뿐인 표시명은 생성자에서 거절한다. 표시명 오류에는 유효 ID를 포함한다. 표시명은 키가 아니므로 같은 이름을 여러 ID에 사용할 수 있다.

ElementCatalog는 IEnumerable<ElementDefinition>을 한 번 순회해 새 Dictionary<ElementId,ElementDefinition>에 넣는다. 입력 컬렉션 자체를 보유하지 않는다. 공개 API는 Count와 Get뿐이다. Get은 무효 ID를 먼저 거절하고 Dictionary.TryGetValue 한 번으로 조회한다. Get 내부에 목록 순회/Unity 검색/원본 로딩이 없다. 평균 O(1) 사전 조회 구조를 정적으로 확인했으며 모든 상황의 최악 시간/FPS 측정을 주장하지 않는다.

정의 자체가 불변이므로 카탈로그가 같은 정의 객체를 보유·반환해도 입력 목록 변경이 전파되지 않는다. 생성자는 null 입력/null 정의/동등 ID 중복을 거절한다. 무효 ID 정의는 공개 생성자에서 만들 수 없으므로 반사·비정상 생성만을 위한 중복 방어는 추가하지 않았다. Get(default)는 별도로 거절한다. 미등록 값은 ID를 포함한 KeyNotFoundException이며 상자 대체는 없다. 빈 카탈로그는 Count0이고 모든 유효 ID 조회는 미등록 오류다.

## 실제 메모리 값과 독립성

기존 LegacyElementMap.Get으로 만든6종의 입력과 조회 결과는 다음과 같다. 각 관찰의 Count는6이었다.

| ID 입력/출력 | 표시명 |
| --- | --- |
| obstacle.crate.wood | 나무상자 |
| obstacle.scrap | 고철 뭉치 |
| obstacle.recovery-capsule | 고물 회수 캡슐 |
| obstacle.color-lock | 색깔 자물쇠 |
| obstacle.metal-rod-box | 금속기둥 상자 |
| obstacle.generator | 고장 난 발전기 |

- 생성에 사용한 배열0을 같은 상자 ID의 다른 표시명으로 교체하고 배열1을 null로 바꿨다. 카탈로그 Count6, 상자/고철 이름과 원래 상자 정의 객체가 그대로였다.
- 별도 List를 Clear한 뒤 element.late를 추가했다. 입력 목록 Count1/카탈로그 Count6, 원래 상자를 조회할 수 있고 element.late는 조회되지 않는다.
- 다른 카탈로그에 상자/고철을 모두 `새 표시명`으로 넣어 Count2와 각각의 올바른 ID 조회를 확인했다. 표시명 변경과 중복은 키에 영향을 주지 않는다.
- element.case, ELEMENT.CASE, 앞뒤 공백이 있는 ` element.case `를 별도 정의로 넣어 Count3 및 각각의 원문 값/이름 조회를 확인했다. ID 정규화 정책을 추가하지 않았다.
- 정의 필드의 readonly/get-only·참조 형식과 카탈로그 Count/Get 공개 API를 검사했다. 메타데이터/조회/오류 경로에서 Unity 전역 난수 상태도 유지됐다.

## 오류 실제 결과

실제 오류 기록12건은 다음과 같다. parameter/전체 문장은 JSONL에 있다.

| 입력 | 실제 거절 |
| --- | --- |
| null 목록 | ArgumentNullException, definitions |
| null 정의 | ArgumentException, definitions |
| 정의 default ID | ArgumentException, id |
| Get default ID | ArgumentException, id |
| 표시명 null/빈/공백/tab | ArgumentException, displayName, element.invalid-name 포함 (4건) |
| 동등한 element.duplicate 두 정의 | ArgumentException, definitions, 해당 ID 포함 |
| 사후 추가 element.late | KeyNotFoundException, 해당 ID 포함 |
| 빈 카탈로그 element.missing | KeyNotFoundException, 해당 ID 포함 |
| 일반 카탈로그 element.absent | KeyNotFoundException, 해당 ID 포함 |

## 500개 규모 조회

결정적 ID element.fixture.000~element.fixture.499, 표시명 정의000~정의499를 메모리에서 만들었다. 정순과 역순 각각 Count500, 중복0이다. 모든500개에 대해 두 카탈로그의 ID·표시명이 정확하고 같은 불변 정의 객체를 반환함을 실제 검사했다.

실제 규모 관찰은 정순500건+역순500건=1000건이다. 각 JSONL 기록에 입력/출력 ID, 표시명, 카탈로그 수가 있다. 소규모 값14건·오류12건을 합쳐 전체 **1,026건**을 저장했다. 이는 메모리 색인 조회 검사이며 실제500종 제작·UI 검색/저장/게임 실행·프레임 성능 검증을 의미하지 않는다.

## 실행 결과와 보존

생산 타입 추가 전에 reflection 기반 전체 검사를 작성·실행했다. RED는 실제 ElementDefinition 부재로 `불변 메모리 정의 계약 존재` 실패, 종료1이었다. 컴파일 오류가 아니다. 구현 후 최종 결과:

| 별도 batchmode/nographics Editor 검사 | PASS | FAIL | 종료 |
| --- | ---: | ---: | ---: |
| Elements.Editor.ElementCatalogVerification.Run | 1538 | 0 | 0 |
| Elements.Editor.ElementIdVerification.Run | 49 | 0 | 0 |
| Levels.Editor.BotObservationVerification.Run | 32 | 0 | 0 |
| 합계 | 1619 | 0 | 각0 |

[Stage14 증거 폴더](../../../../Logs/ElementFramework/Stage14): red.log/red-execution.json/red-results.txt, catalog.log/catalog-execution.json/catalog-results.txt/catalog-values.jsonl, id.log/id-execution.json/id-results.txt/id-values.jsonl, bot.log/bot-execution.json/bot-results.txt, protected-before.json/preservation-audit.json, existing-work-before.json/git-before.txt, source-final.json, progress.md, completion-audit.json. 로그는 Git 제외 대상이다. ID 검사의 기존 Stage13 기록은 백업 후 새 결과를 Stage14에 복사하고 원래 기록을 복원해 과거 실측을 보존했다.

보호 파일 **1,806개 변경0**. EF-13의 미커밋 코드/meta/완료 문서와 다음 명령문도 보존했다. 기존 문서의 현재 단계 상태·관련 목차만 갱신했다. 새 meta3개 GUID 중복0. 기존 소비자는 ElementDefinition/ElementCatalog를 참조하지 않는다. enum 숫자·배치 Id/발전기 연결·저장 포맷/원본/GUID/프리팹/씬/아틀라스/Addressables·EF-05 예외 원복은 유지됐다. 빌드·재패킹·이미지·팩 재생성·커밋·사용자 Unity 종료·씬 저장은 하지 않았다.

## 한계와 다음 단계

정의는 현재 ID/표시명만 담는다. 행동/피해/점유/미션/표현/제작 ScriptableObject/배포 DTO는 없다. 카탈로그는 아직 게임·편집·봇·저장에 연결되지 않았다. 전체 플레이/승률/렌더링/실기기/IL2CPP/성능 검증은 하지 않았다. EF-09 원본1254px3개와 Editor/월드 바닥 차이, EF-11 Draw/Reset 초기화 책임과 승인된 드론 수정은 이번에 다루지 않았다.

다음은 **EF-15 나무상자 배치 수치 정의 연결**이다. 대표1종의 크기/최대 내구도만 불변 정의 데이터로 옮겨 기존 Size/MaxDurability 조회에 연결하고, 다른 규칙·저장은 그대로 둔다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-15-crate-placement-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-15-crate-placement-goal.md) · [전체 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-15-command.md). 다음 구현은 시작하지 않았다.

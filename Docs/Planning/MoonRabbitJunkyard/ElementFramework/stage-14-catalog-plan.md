# EF-14 — 읽기 전용 정의 메타데이터·카탈로그 조회 계획

상태: 완료. 카탈로그1538+ID49+봇32 PASS/0 FAIL, 각각 종료0. [검증 기록](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-14-progress.md). 큰 구간 B의 두 번째 작은 구현 단계.

연결: [통합 가이드라인](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-13 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-13-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-14-catalog-goal.md).

실행 담당: executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 목적과 경계

EF-13의 안정적인 ID를 받아 런타임에서 읽기 전용 정의를 O(1) 색인으로 조회하는 최소 계약을 만든다. 생성 시 한 번 색인을 구성하고 Get 호출마다 전체 입력 목록이나 Unity Object를 찾지 않는다. 기존 enum→ID 매핑은 검사 입력 구성에서만 재사용한다. 게임/에디터/봇/저장 소비자는 아직 연결하지 않는다.

추가 후보는 Elements/Data/ElementDefinition.cs, Elements/Runtime/ElementCatalog.cs, Elements/Editor/Tests/ElementCatalogVerification.cs와 meta다. 여기서 ElementDefinition은 **ID와 표시명만 가진 불변 메모리 조회 값**이다. 제작 ScriptableObject나 배포 DTO를 의미하지 않는다. 후속 단계에서 제작/배포 경계를 별도로 연결하므로 현재 이름을 이유로 Unity 원본 타입이나 저장 계약을 변경하지 않는다. 범주·행동/피해/점유/미션/표현 정책은 현재 사용하지 않으므로 미리 추가하지 않는다.

## 최소 계약

- 정의는 유효 ElementId와 비어 있지 않은 표시명을 보유하며 setter/Unity Object/가변 원본 참조가 없다. 표시명은 조회 키가 아니고 같은 표시명 여러 개를 허용한다. 원문 ID는 정규화하지 않는다.
- 카탈로그 생성자는 IEnumerable<ElementDefinition>을 받아 독립된 ID 색인을 한 번 구성한다. 입력 배열/목록이 사후 변경돼도 카탈로그는 변하지 않는다. Count/Get(ElementId)만 우선 제공하고 등록 수정/전역 singleton/프레임 검색/역매핑/파일 로딩은 만들지 않는다.
- null 입력, null 정의, 무효/default ID, 중복 ID는 명시적으로 거절한다. 유효하지만 등록하지 않은 ID의 Get은 ID를 포함한 오류로 거절한다. 알 수 없는 요소를 상자로 바꾸지 않는다. 오류에 ID가 있는 경우 해당 원문을 포함한다.
- 동일 ID는 Ordinal 동등성을 따른다. 다른 대소문자/앞뒤 공백의 유효 ID는 EF-13 계약상 다른 값이다. 빈 카탈로그의 Count0/미등록 조회 거절도 검사하며 게임 준비 정책과 혼동하지 않는다.

## 작업 단위와 검증

1. EF-13 ID/default/매핑 계약과 기존 변경을 읽고 보호 해시를 확보한다. 반환값 소유권과 오류 계약을 먼저 검사한다.
2. 불변 ID/표시명 값과 읽기 전용 카탈로그 기본 조회만 구현한다. 외부 입력 컬렉션을 복사하고 생성 뒤 수정 API를 노출하지 않는다.
3. 메모리 정의6종을 LegacyElementMap으로 구성해 정확한 조회/Count/표시명 독립성을 검사한다. 생성 후 입력 목록 교체/삭제, 같은 표시명, 중복/무효/null/미등록 ID, 대소문자 구별을 검사한다.
4. 결정적 ID500개를 메모리에 생성하고 각 Get의 실제 값, 중복0/Count500, 입력 순서 역전의 결과 동일성을 확인한다. 이는 조회 규모 검사이며 FPS/제작 UI/전체 프레임 성능 주장을 하지 않는다. O(1) 경계는 색인 구현·호출부 정적으로 확인한다.
5. 별도 Editor에서 새 검사, ElementIdVerification, BotObservationVerification을 실행하고 실제 입력/값/오류·종료0/필수 FAIL0·원본/기존 파일/GUID 보존을 기록한다. 결과에 따라 다음 작은 단계의 문서만 작성한다.

## 제외와 완료 기록

새 제작/배포 에셋·저장 포맷·원본 변환·팩 재생성·행동 등록/규칙·드론·UI/MVVM·표현/풀·자동 플레이 전환은 제외한다. 기존 소비자와 enum 숫자·배치 Id/발전기 연결/EF-05/09/11 기준을 유지한다. 빌드·재패킹·이미지·임의 커밋·사용자 Unity 종료·씬 저장 금지.

검증 기록은 stage-14-progress.md에 저장한다. 필수 실패나 입력 독립성 미검증은 미완료로 기록한다. 전체 정의 시스템/500개 제작 콘텐츠/기존 게임 전환 완료라고 표시하지 않는다.

# EF-38 — 거미줄 ID·카탈로그 기본 등록 계획

> 2026-10-05: 이 ID 전용 준비안은 큰 구간 1단계 계획으로 대체되었다. 이 문서의 실행문은 사용하지 않고 [새 통합 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/phase-01-common-element-rules-plan.md)을 따른다. 과거 준비 내용은 참고용으로 보존한다.

> 실행자는 superpowers:executing-plans로 작은 작업을 순서대로 수행한다. 사용자의 별도 요청 없이 에이전트·커밋을 사용하지 않는다.

상태: 준비 완료, 구현 미착수.
목표: 기존 CoverKind.Web를 영구 ID cover.web과 기존 불변 카탈로그에 연결한다.
구조: 장애물6종과 같은 ElementId/ElementDefinition/ElementCatalog를 재사용한다. 거미줄 정의는 표시명 거미줄과 ElementPlacementProfile(1,3)만 가진다. 피해·집계·미션·행동은 아직 등록하거나 연결하지 않는다.
기술: Unity6000.3.10f1, 기존 C#/Editor 검사, 현재 패키지·asmdef.
설계: [통합 가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF37 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-37-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-38-web-catalog-goal.md).

## 선정 근거·파일 경계

현재 LevelPlacementRules.CoverValueError의 Web 최대값3은 상수이고 거미줄은 정의 카탈로그에 없다. 정의 등록을 먼저 독립 완료하고 실제 상한 소비는 다음 단계로 분리한다. 새로운 고물/덮개 종류나 새 행동을 만드는 작업이 아니다.

| 파일 | 책임 |
| --- | --- |
| Assets/Scripts/Features/Elements/Runtime/LegacyElementMap.cs | Get(CoverKind)의 Web→cover.web 매핑 |
| Assets/Scripts/Features/Elements/Runtime/LegacyElementDefinitions.cs | 같은 카탈로그의 Web 정의와 Get(CoverKind) 조회 |
| Assets/Scripts/Features/Elements/Editor/Tests/WebCatalogVerification.cs(+meta) | 실제 ID/조회 RED·GREEN과 정상 전체/오류/복원 |

소비: 기존 ElementId·ElementPlacementProfile·ElementDefinition·ElementCatalog.Get(ElementId).
생산: public static ElementId LegacyElementMap.Get(CoverKind kind), public static ElementDefinition LegacyElementDefinitions.Get(CoverKind kind). 기존 Get(ObstacleKind)의 서명·매핑·예외는 유지한다. 새 Get(CoverKind)는 Web만 지원하며 Mold/미정의 값은 입력을 포함한 ArgumentOutOfRangeException으로 거절한다. 새 조회는 생성 시 등록한 같은 불변 참조를 반환한다. 기존 카탈로그의 Count/Get 외 수정 API는 추가하지 않는다.

## 공통 제약과 검토 초점

work/HEAD·모든 미커밋·원본/GUID/enum·EF05/09/11·과거 증거·무시 Addressables/패키지 서명을 보존한다. 9×9/50레벨 팩/버전1을 유지한다. 소스 이동·새 종류·정책·행동·패키지·asmdef/커밋/푸시/빌드/재패킹/팩·이미지 재생성/사용자 Unity 종료/씬 저장/에이전트를 금지한다.

검토 초점은 Web 외 값의 거절, Get 오버로드의 정확한 타입 선택, 기존6종의 동일 참조·메타데이터/오류, 메타데이터 전용 정의의 null 정책·행동, 새 전역 카탈로그 항목과 기존6종을 복사한 검사 카탈로그의 Count6 구분이다. 이 다섯 경계는 아래 새 검사에 포함한다. 미래 덮개 실행을 장애물 Durability 행동에 임의 연결하지 않는다.

## 실행 단위와 검증

- [ ] 보호 기준·EF37 verified-results.json의 정확한30종/687011 PASS·모든 작성 경로·기존 검사의 타입 선택/카탈로그 소유권을 조사한다. 기존 테스트나 기대값을 변경하지 않는다.
- [ ] 검사를 먼저 작성한다. 아직 없는 오버로드는 Editor 검사에서 정확한 인자 타입의 MethodInfo로 조회해 컴파일 오류 대신 실제 매핑/조회 부재 RED·종료1을 확보한다. 공개 getter의 cover.web/표시명/1·3/같은 참조, Mold·미정의 거절, 기존6종의 ID/숫자/이름/값·참조/부분 생성자/오류·난수·원본 무변경을 검사한다. 정상 Before를 확보한다.
- [ ] Web 매핑·불변 정의·같은 카탈로그 조회만 최소 추가한다. 기존 Get(ObstacleKind)·ElementCatalog·모든 실행/검증 소비자는 보존한다. 새 정의의 나머지 프로필/ReactionBehavior는 null이며 새 행동 키를 추가하지 않는다. GREEN을 확인한다.
- [ ] WebCatalogVerification.Before/Red/Run과 기존 정확한30종을 각각 별도 Editor에서 실행한다. 정상 전체18182행/188팩·225팩 상태·원문 바이트/상태·문맥·난수·피해·미션·효과·예약·취소·재선정·발전기·공급·검증·본체ID/버전/50구간을 비교한다. 등록 메타데이터의 의도적 추가는 별도 기록하며 정상 결과를 정규화하지 않는다. 모든 주/부가/조건부 출력을 백업→삭제→새 생성 확인→증거 보존→finally 전체 바이트 복원하고 원본 파일 독립성을 확인한다.
- [ ] 과거 전체 해시/원본·기존 작업/GUID/diff·정확한 수/실제 종료를 감사하고 stage-38-progress.md에 요구사항별 보고를 작성한다. 전체 완료 후 다음 한 단계 계획/목표/전체 복사용 명령문만 작성하며 다음 구현은 시작하지 않는다.

LevelPlacementRules.CoverValueError·거미줄 피해/제거/내용물/미션/공급/낙하/드론/파워/바닥·번식/UI/MVVM/표현/풀/봇/저장·변환/원본 에셋은 변경하지 않는다. 등록만으로 소비자 전환 완료를 주장하지 않는다. 실제 발견한 예외나 의존성은 최소 근거와 함께 현재 계획에 반영하며 범위를 확대하지 않는다.

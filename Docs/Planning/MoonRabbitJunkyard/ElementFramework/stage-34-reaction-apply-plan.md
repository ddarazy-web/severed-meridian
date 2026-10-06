# EF-34 — 반응 행동 등록과 실제 적용 위임 연결 계획

상태: 완료. 534031 PASS/0 FAIL·27종 별도 Editor 실제 종료0, 전체 상태/출력 복원/보호 감사 통과.

> 실행 담당: superpowers:executing-plans. 에이전트는 별도 요청 시에만 사용한다.

목표: 기존6종의 조회와 적용 위임을 같은 명시적 행동 등록에 연결하고, 실제 공통 효과 실행에서 내구도/충전 적용 하나만 선택한다.

구조: EF-33의 행동 키와 등록표를 재사용한다. 등록 하나가 조회와 적용을 함께 제공하고 공통 효과 실행이 현재 정의의 키로 적용 위임 하나를 선택한다. 기존 ObstacleDamageRules.Apply와 GeneratorRules.Apply의 실제 규칙은 재사용하며 상태·기록·제거·연결 수명은 기존 소유자가 유지한다.

환경: ServeredMeridian, Unity6000.3.10f1, 현재 C#/asmdef·패키지·9×9 관례.

연결: [가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-33 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-33-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-34-reaction-apply-goal.md).

## 한 단계의 결과와 경계

EF-33은 Query 선택을 정의의 키로 연결했다. PowerEffectResolution.ApplyCore는 아직 Charge 응답에서 GeneratorRules.Apply, Damage 응답에서 ObstacleDamageRules.Apply를 직접 호출한다. EF-34는 **행동 등록의 조회/적용 짝 → 공통 효과 실행의 실제 적용 위임**을 한 단위로 연결한다. 적용 필드만 추가하거나 종류별로 반복 분할하지 않는다.

응답의 의미와 효과 기록 책임은 유지한다. 내구도/충전 응답을 처리할 때 선택한 적용 위임을 정확히 한 번 호출한다. 덮개/일반 블록/파워 발동을 장애물 적용으로 보내지 않는다. 새 등록표 전체 순회·콘텐츠별 클래스·리플렉션 실행·공개 수정 API는 만들지 않는다. 이번 단계에서도 신규 콘텐츠/새 데이터 형식/전체 요소 실행기는 만들지 않는다.

## 파일과 인터페이스

| 파일 | 책임 |
| --- | --- |
| Assets/Scripts/Features/Elements/Runtime/ElementBehaviorRegistry.cs | Durability/GeneratorCharge 등록에 조회와 적용 위임을 함께 보관하고 키로 한 항목 조회 |
| Assets/Scripts/Features/Obstacles/Runtime/ObstacleDamageRules.cs | 기존 등록의 조립과 구형 종류→정의 경계; 기존 내구도 적용 규칙 재사용 |
| Assets/Scripts/Features/Obstacles/Runtime/GeneratorRules.cs | 기존 충전·작동·연결 철거 규칙 보존; 새 콘텐츠별 적용 복제 금지 |
| Assets/Scripts/Features/PowerBlocks/Runtime/PowerEffectResolution.cs | 실제 ApplyCore에서 등록된 적용을 호출; 효과 기록/목표 무효화/소비 순서 유지 |
| Assets/Scripts/Features/Elements/Editor/Tests/ElementReactionApplyVerification.cs(+meta) | 신규 계약·실제 적용 및 전체 전후 기준 검사 |

소비 인터페이스는 현재 internal ReactionQuery와 ElementDefinition.ReactionBehavior, 실제 Evaluate 결과, LevelRuntimeState/RuntimeCell/TurnEffectContext/hit이다. 적용 위임은 기존 내구도 Apply의 반환값과 충전 Apply의 변경값을 공통 효과 기록이 그대로 관찰할 수 있게 연결한다. 생산 변경 전에 실제 반환값 소비·직접 Apply 호출부를 조사해 내부 서명을 확정하고 이 문서에 기록한다. 저장/표현용 공개 인터페이스는 추가하지 않는다.

### 조사로 확정한 내부 적용 계약

`ReactionApply(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context, int hit) -> int`를 사용한다. 등록 항목은 읽기 전용 Query/Apply 짝이며 기존 `queries` 사전과 두 행동 키를 유지한다. 반환값은 적용 이후 본체 내구도다. 충전 행동도 같은 반환 형태를 사용하되 공통 효과는 기존처럼 ChargeBefore/After를 실제 본체에서 읽는다.

직접 생산 호출은 ApplyCore의 GeneratorRules.Apply와 ObstacleDamageRules.Apply 두 곳뿐이다. ApplyCore는 두 응답에 대해 등록 적용을 호출하고, Charge 응답의 즉시 목표 무효화와 Damage 응답의 반환값/제거 판정 및 뒤따르는 무효화·효과 기록은 기존 위치에 유지한다. 구체 함수 선택은 응답이 아니라 정의의 키로 한다. 등록의 적용 래퍼는 기존 내구도/충전 프로필 검증을 거쳐 원래 Apply를 호출하며 원래 Apply/Remove 알고리즘은 변경하지 않는다.

실행은 현재 work 체크아웃과 Logs/ElementFramework/Stage34 기록을 사용한다. 별도 워크트리·커밋·하위 에이전트는 사용하지 않는다. 검사 전용 리플렉션 관찰은 실제 원래 위임을 실행한 후 상태/문맥/반환값을 기록하고 finally 원래 등록 항목 참조를 복원한다. 생산 코드에는 리플렉션이나 수정 API를 추가하지 않는다.

## 검토 초점

등록된 Query와 Apply가 서로 다른 행동을 선택하거나 같은 타격이 두 번 적용되지 않아야 한다. 부분 정의/누락 키를 기본 행동으로 추론하지 않는다. 기존 프로필 누락 오류와 조회 거부가 적용보다 앞선다. 발전기의 임계 충전/연결 철거·마지막 연결 제거, 2×2의 칸/hit 기록, 제거 미션과 효과 기록의 순서, 드론 예약 무효화가 유지돼야 한다. 실제 정의 스냅샷도 검사 출력이므로 주 결과/값 외 부가 출력까지 보호한다.

## 작업과 검증

- [x] work/HEAD·미커밋·원본/GUID/enum·EF-05/09/11·과거 증거/부가 출력·무시된 Addressables/패키지 서명을 보호하고 Query/Apply/ApplyCore 및 직접 적용 호출부와 동일 입력/시드 전환 전 기준을 확보한다.
- [x] 검사부터 작성한다. 등록의 조회/적용 짝과 실제 public Swap의 단일 적용, 키 누락/미지원·프로필 오류·거부 뒤 무변경, 메타데이터 부분 생성자 호환을 검사한다. 행동 선택을 종류/응답으로 우회하는 구현에서 실패하는 실제 RED를 확보한다.
- [x] 두 행동의 적용 위임과 실제 ApplyCore 호출을 함께 최소 연결한다. 기존 내구도/충전 Apply 본문과 제거·미션·연결·소비·목표 무효화·효과 기록 책임을 보존하고 실제 내구도 전체/충전 임계/미션/효과 및 GREEN을 확인한다.
- [x] 동일 입력/시드의 전체 실행/비공개 문맥/규칙·전역 난수/예약·취소·재선정/발전기·공급/검증/MemoryPack 전체 바이트·본체ID·버전1/50구간을 비교한다. 새 Run과 EF-33 최종26종 각각 별도 Editor 정확한 메서드·종료0/필수FAIL0, 기존 결과/값 및 부가 정의 스냅샷의 백업/삭제/새 생성/증거 복사/finally 전체 복원을 확인한다.
- [x] 원본·기존 작업·과거 증거/GUID·diff check와 요구사항별 감사를 끝내고 변경/실측/의도적 차이/미검증/남은 문제를 보고한다. 다음 한 단계 계획/목표/전체 복사용 명령문만 제공하고 다음 구현은 시작하지 않는다.

신규 검사 진입점은 Elements.Editor.ElementReactionApplyVerification.Before/Red/Run으로 별도 batch Editor에서만 실행한다. 기존26종 메서드는 EF-33 verified-results.json과 정확히 대조한다. 합계는 실행된 개별 결과에서 계산하며 검사 수 감소나 기대값 완화로 통과시키지 않는다. 같은ID 임시 교체는 런타임 구성 후 수행하고 finally 정확한 원래 등록 참조를 복원한다. 연결/미션/제거 결과를 강제 Apply만으로 검증하지 않고 public Swap→공통 효과 실행의 실제 결과를 확인한다.

## 제외와 종료

기본 정책/밸런스·새 행동/종류·Remove/미션 진행·예약 피해량·생성/충전 알고리즘·드론 정책/비행·공급/낙하·파워 색/소비/범위/조합·덮개/바닥/번식·UI/MVVM/표현/풀/봇·저장/변환/원본 에셋 전환은 제외한다. 커밋/푸시/빌드/재패킹/팩·이미지 재생성/사용자 Unity 종료/씬 저장 금지. 기존 직접 Apply의 테스트 의미를 바꾸지 않으며 실제 필요한 키 전달만 최소 보완한다.

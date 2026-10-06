# EF-35 — 내구도 적용의 집계 정책 전달 계획

상태: 완료.584906 PASS/0 FAIL·28종 별도 Editor 종료0·63출력 원문 복원 및 최종 감사 통과.

> 실행 담당: superpowers:executing-plans. 현재 work 체크아웃을 사용하며 에이전트는 별도 요청 시에만 사용한다.

목표: 등록된 내구도 행동의 조회와 실제 타격 기록이 같은 정의의 집계 정책을 사용하도록 연결한다.

구조: EF-34의 Query/Apply 짝과 두 행동 키를 유지한다. 등록 내구도 적용에서 정의의 필수 DamageAggregationPolicy를 읽어 공통 적용 본문으로 전달한다. 기존 직접 Apply 호출의 구형 종류 범위 판정은 호환 진입점에 보존하고, 상태 변경 본문은 복제하지 않는다.

환경: ServeredMeridian, Unity6000.3.10f1, 현재 C#/asmdef·패키지·9×9 관례.

연결: [가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-34 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-34-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-35-durability-apply-policy-goal.md).

## 한 단계의 결과와 경계

EF-34는 등록된 적용을 실제 공통 효과에 연결했다. 그러나 기존 Apply 본문은 아직 Crate~Appliance 종류 범위로 집계 정책 사용 여부를 결정한다. 따라서 조회가 정의의 PerHitCell 정책을 선택해도 기존 종류 범위 밖의 본체에는 본체 기록을 남길 수 있다. 이번 단계는 이 조회/적용 불일치를 해소하는 한 책임이며 여섯 종류별로 나누지 않는다.

기존6종의 정상 정의에서는 상태·효과·미션·예약·난수 차이가 없어야 한다. 테스트에서 런타임 구성 후 같은ID의 발전기를 호환 내구도/칸 집계 정의로 임시 바꾼 사례만, 선택한 정책에 맞는 hit/칸 기록으로 의도적으로 달라진다. 새 종류·새 키·데이터 형식은 추가하지 않는다.

## 파일과 내부 계약

| 파일 | 책임 |
| --- | --- |
| Assets/Scripts/Features/Obstacles/Runtime/ObstacleDamageRules.cs | 등록 내구도 Apply에서 필수 집계 정책 전달; Query는 전달받은 정의의 집계 정책 조회; 기존 직접 Apply 호환 경계와 공통 상태 변경 본문 |
| Assets/Scripts/Features/Elements/Editor/Tests/ElementDurabilityApplyPolicyVerification.cs(+meta) | 실제 교환의 정책 전달 RED/GREEN 및 전체 전후 기준 |

ReactionApply의 `int (ElementDefinition, LevelRuntimeState, RuntimeCell, TurnEffectContext, int hit)` 계약과 기존 직접 Apply 서명은 유지한다. 공통 적용 본문은 `bool perHitCell` 같은 검증된 집계 선택값을 소비하는 최소 내부 함수로 분리할 수 있다. 생산 변경 전 실제 직접 호출부·반환값 소비·오류 순서를 확인해 최종 내부 서명을 기록한다. 조회/적용에 서로 다른 정책을 재조회하거나 본문을 두 벌로 복제하지 않는다.

## 검토 초점

- 등록 내구도 행동의 집계 정책 누락은 정의 ID 오류로 거절하고 기본 본체 정책으로 추론하지 않는다.
- 같은ID 임시 교체는 런타임/실행기 구성 뒤 수행하고 finally 정확한 원래 등록 참조를 복원한다.
- 실제 public Swap에서 hit/좌표 기록과 본체 기록을 함께 확인한다. 호출 횟수나 강제 Apply만으로 통과시키지 않는다.
- 기존 직접 Apply의 발전기·미지원 종류·부분 정의 의미와 예외 순서를 호환 진입점에서 유지한다.
- 내구도 감소→제거→미션/연결 수명→무효화/효과 기록 순서, 발전기 실제 충전 알고리즘과 기존 자석 예외를 바꾸지 않는다.

## 작업과 검증

- [x] work/HEAD·미커밋·원본/GUID/enum·EF-05/09/11·과거 출력/부가 snapshot·무시 Addressables/패키지 서명을 보호한다. 현재 호출부/집계 정책 소비를 조사하고 동일 입력/시드 전환 전 전체 기준을 확보한다.
- [x] 검사부터 작성한다. 호환 크기의 내구도/PerHitCell 정책을 가진 같은ID 발전기를 실제 public Swap으로 타격하여 hit/칸 기록과 본체 기록 불일치 RED를 확보한다. 원본 내구도1로도 기록 방식을 관찰할 수 있으므로 불필요한 내구도 강제 설정을 하지 않는다. 필수 집계 정책 누락의 ID 오류와 무변경도 확인한다.
- [x] 등록 조회/적용이 같은 정의의 집계 정책을 사용하도록 최소 연결하고 기존 직접 Apply를 호환 경계로 유지한다. 공통 적용 본문은 하나만 두고 나머지 알고리즘/오류/기록 순서를 보존해 GREEN을 확인한다.
- [x] 기존 정상 정의의 전체 실행 상태/비공개 문맥/난수/피해·미션·효과/예약·취소·재선정/발전기·공급/검증/MemoryPack 전체 바이트·본체ID·버전1/50구간을 비교한다. 새 Before/Red/Run과 EF-34 최종27종의 정확한 메서드/검사 수를 각각 별도 Editor에서 확인한다. 주/부가/조건부 출력의 백업→삭제→새 생성→증거 복사→finally 전체 복원을 검증한다.
- [x] 요구사항별 최종 감사와 보고를 마치고 바로 다음 한 단계 계획/목표/전체 복사용 명령문만 작성한다. 다음 구현은 시작하지 않는다.

신규 진입점은 Elements.Editor.ElementDurabilityApplyPolicyVerification.Before/Red/Run이다. EF-34 verified-results.json의27종과 정확히 대조하며 검사 수나 기대값을 낮추지 않는다. 임시 비기본 정의의 의도적 기록 차이는 별도 연결 증거로 남기고 정상 전후 실행 값을 정규화해 숨기지 않는다.

## 제외와 제약

미션 매핑/Remove·예약 피해량·허용 원인/색·충전/연결/생성 알고리즘·드론/공급/낙하·파워 소비/범위/조합·덮개/바닥/번식·UI/MVVM/표현/풀/봇·저장/변환·원본 에셋·새 행동/종류/패키지/asmdef는 제외한다. 커밋/푸시/빌드/재패킹/팩·이미지 재생성/사용자 Unity 종료/씬 저장 금지. 정책 전달과 함께 다른 잔여 종류 분기를 정리하지 않는다.

## 실제 호출 조사와 확정 내부 계약

생산 소비자는 PowerEffectResolution.ApplyCore의 Charge/Damage 두 경로다. Charge 경로는 반환값을 쓰지 않고 충전값을 재조회하며, Damage 경로는 반환값을 after에 저장한다. 이후 제거/비활성화/기록 순서를 그대로 유지한다. 직접 Apply는 기존 검사에서 리플렉션으로 호출되므로 서명과 구형 의미를 유지한다.

공통 본문은 `private static int ApplyCore(LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context, int hit, bool perHitCell)`로 확정한다. 등록 ApplyDurability는 배치 필수 확인 → 충전 배치 충돌 거부 → 전달 정의의 필수 집계 정책 확인 → ApplyCore 호출 순서다. 직접 Apply는 기존 cell/index/body/kind와 Crate~Appliance 범위/필수 집계 정책 판정을 먼저 수행한 뒤 같은 ApplyCore를 호출한다. ApplyCore의 기록→감소→Remove→TargetRemoved→반환 본문은 기존 그대로다. QueryDurability는 원래 선행 거부 순서를 유지하고 집계 정책 조회 대상만 전달 정의로 바꾼다.

보호 기준: work/6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783, 기존1972파일/미커밋98파일/과거5544증거/입력37파일. RED는 실제 별도 Editor 종료1, PASS25/FAIL2이며 실제 public Swap의 기록 불일치와 누락 집계 정책 적용의 비거부를 재현했다.

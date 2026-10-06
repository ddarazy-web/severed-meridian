# EF-30 — 회수캡슐 일반 인접 반응 정책 우선 적용 계획

상태: 완료. 새48386+기존275824=324210 PASS/0 FAIL·최종23종 별도 Editor 종료0. 최종 보존 감사와 완료 기록·EF-31 다음 문서를 작성했다. EF-31 구현은 시작하지 않았다.

실행 담당: superpowers:executing-plans. 에이전트는 별도 요청 시에만 사용한다.

연결: [가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-29 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-29-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-30-capsule-adjacent-policy-goal.md).

## 근거와 범위

ObstacleDamageRules.Query는 기존 DamageSourcePolicy를 먼저 조회하지만, 이후 비파워/비망치 분기에서 Safe 종류를 다시 거부한다. 따라서 같은 ID 회수캡슐의 AdjacentMatch를 허용하도록 정의만 바꿔도 일반 인접 반응이 막힌다. 이 잔여 제한 한 책임만 전환한다. 기본 등록의 Safe.AdjacentMatch=false는 유지하므로 현재 게임의 캡슐 밸런스는 바꾸지 않는다.

자석 인접은 DamageReaction.Evaluate의 별도 ColorLock 제한과 내부 제한이 함께 존재한다. 해당 경로는 별도 단계로 남기며 이번 단계에서 섞지 않는다. 색 일치, 발전기, 집계, 실제 적용, 미션, 공급, 저장 전환도 분리한다.

변경 후보:

- Obstacles/Runtime/ObstacleDamageRules.cs: Query의 Safe 일반 인접 추가 거부 조건만 최소 수정한다. 기존 정책 조회/오류, 다른 원인, 메시지와 실제 적용은 유지한다.
- Elements/Editor/Tests/CapsuleAdjacentPolicyVerification.cs(+meta): Before/Red/Run, 같은 ID 정의 교체의 실제 Evaluate/Query 및 허용된 실제 적용, 전체 보존 검사.
- 실제 호출 실패가 입증된 fixture만 필요시 최소 조정한다. 생성자·누락 오류 기대값을 낮추지 않는다.

## 검사와 최소 계약

유효한 회수캡슐 본체와 제거 미션을 가진 저장 입력에서 같은 ID Safe의 DamageSourcePolicy.AdjacentMatch만 true로 테스트 메모리에서 교체한다. 나머지 정의와 프로필은 정확한 원래 참조를 유지한다. 인접한 출발 칸, 벽/덮개/보호가 없는 대상에서 실제 DamageReaction.Evaluate를 먼저 호출한다. 기존 actual None/0 대비 want Damage/1의 동작 RED를 확보하며 문자열 비교로 실패를 만들지 않는다. 조회 무변경과 finally의 원래 등록 참조 복원을 확인한다.

GREEN은 허용 반응을 실제 공통 적용 경로로 처리해 내구도 감소·턴 기록·제거 미션·효과가 연결되는지 확인한다. Query에만 반응 문자열을 만들거나 허용되지 않은 반응을 강제로 Apply해서 성공을 만들지 않는다.

전체 내구도1~5, 같은/다른 본체, 미션 완료/잔량, 새 턴/이미 피해/null 문맥, 인접/비인접/벽/비활성/삭제/덮개/보호 경계를 검사한다. 기본 false 등록, true 교체, false 재교체를 구분하고 조회 상태/비공개 문맥/전역 및 규칙 난수 보존을 확인한다. Power/Hammer/MagnetAdjacent와 미정의 원인(-1/4/99)의 기존 결과·메시지를 그대로 유지한다. 유효 대상의 누락 정책은 ID 포함 오류이며 기본값 대체는 없다.

## 작업과 검증

- [x] work/EF-23~29 미커밋·원본/GUID/enum·과거 출력과 무시된 Addressables 상태/패키지 서명을 보호한다. Evaluate→Query→정책 및 실제 적용 호출부를 조사하고 동일 저장 입력/시드의 전환 전 전체 결과를 확보한다.
- [x] 검사부터 작성하고 같은 ID Safe 일반 인접 true의 실제 None/0→Damage/1 RED와 조회 무변경/finally 참조 복원을 확인한다.
- [x] 일반 인접 잔여 거부만 수정해 GREEN과 실제 공통 적용을 확인한다. 다른 원인·미정의 원인·색 일치·발전기·누락 계약을 보존한다.
- [x] 기본 등록의 전체 반응/상태/비공개 문맥/난수·피해/미션/효과·예약/재선정·발전기·공급 요약/전체 검증·MemoryPack 전체 바이트/본체ID/버전1/50구간을 동일 입력으로 비교한다. 새 검사와 EF-29 최종 기존22종을 각각 별도 Editor 실제 종료0/필수FAIL0으로 확인한다.
- [x] 과거 결과/값 출력을 백업→삭제→새 생성 확인→증거 복사→finally 전체 바이트 복원한다. 최종 원본/GUID/기존 작업/과거 증거 감사·diff check·보고와 다음 한 단계 문서를 마무리한다. 다음 구현은 시작하지 않는다.

필수 기존22종: InitialMissionSupplyVerification.Run과 [EF-29의 기존21종](stage-29-initial-mission-supply-plan.md). 정확한 메서드는 EF-29 최종 실행 표 및 Logs/ElementFramework/Stage29/verified-results.json의 최종22종과 대조한다. Before/Red/FixtureProbe는 최종 회귀 메서드의 대체가 아니다.

## 제외와 검토 초점

정의/기본 등록/생성 계약, DamageReaction.Evaluate/자석 인접 제한, 색 일치/집계/Apply/Remove/미션 진행/드론/발전기/공급/낙하/비행/UI/MVVM/표현/풀/봇/저장/변환/원본 에셋은 변경하지 않는다. 새 생산 정책·프로필·패키지·asmdef를 만들지 않는다. EF-05/09/11·원본/GUID/enum·기존 작업 보존. 커밋/푸시/빌드/재패킹/이미지·팩 재생성/사용자 Unity 종료/씬 저장 금지.

검토 초점: 기본 false 밸런스 변경, 자석 제한까지 우회하는 과도한 수정, 미정의 원인의 기존 Safe 거부 손실, 조회의 상태 변경, 기존 프로필을 빠뜨린 임시 정의, 반응을 거치지 않은 강제 적용으로 만든 가짜 GREEN.

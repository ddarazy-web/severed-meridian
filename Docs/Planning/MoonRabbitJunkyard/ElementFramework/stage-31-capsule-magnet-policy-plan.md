# EF-31 — 회수캡슐 자석 인접 반응 정책 연결 계획

상태: 완료. 실제 RED→GREEN, 새 검사와 기존23종 별도 Editor·전체 보존 감사 통과. 상세 결과는 EF-31 검증 기록을 따른다.

실행 담당: superpowers:executing-plans. 에이전트는 별도 요청 시에만 사용한다.

연결: [가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-30 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-30-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-31-capsule-magnet-policy-goal.md).

## 근거와 범위

EF-30은 Safe의 일반 인접 추가 거부만 전환했다. 자석 인접은 여전히 DamageReaction.Evaluate의 ColorLock 전용 제한과 ObstacleDamageRules.Query의 두 종류 조건에 의해 거부된다. 같은 ID Safe의 기존 DamageSourcePolicy.MagnetAdjacent만true로 바꿔도 실제 반응이None/0이다.

다음 단계에서는 **회수캡슐 한 종류의 자석 인접 정책 연결**만 수행한다. 같은 반응이 전단과 내부에서 거절되므로 두 경계를 함께 연결하고 실제 Evaluate→공통 적용으로 확인해야 의미가 있다. 한쪽만 우회한 Query 직접 검사로 완료하지 않는다. 다른 종류의 자석 정책 전환과 발전기 충전은 별도다.

기본 Safe.MagnetAdjacent=false와 AdjacentMatch=false는 유지한다. 색 자물쇠의 기존 자석 예외·색 비교와 다른5종의 제한, 비장애물의 제한도 그대로 둔다. 전체 자석 범위·소비·색 선택·파워 조합·미션 선택을 바꾸지 않는다.

변경 후보:

- Obstacles/Runtime/DamageReaction.cs: 기존 자석 전단 제한에 Safe의 기존 정책 허용을 좁게 연결한다. 기본false에서는 현재 거부 순서/응답/메시지를 유지한다. 거리/벽/덮개/보호 및 다른 원인의 순서를 바꾸지 않는다.
- Obstacles/Runtime/ObstacleDamageRules.cs: 정책을 이미 통과한 Safe 자석 인접만 내부 잔여 거부에서 제외한다. EF-30 일반 인접 연결과 미정의 원인 거부를 유지한다.
- Elements/Editor/Tests/CapsuleMagnetPolicyVerification.cs(+meta): Before/Red/Run, 같은 ID 정의 교체의 실제 Evaluate/공통 적용 및 전체 보존 검사.
- 기존 fixture는 실제 호출 실패가 확인된 경우에만 최소 조정한다. 생성자·누락 오류 기대값을 낮추지 않는다.

## 검사와 최소 계약

유효한 Safe 본체와 제거 미션을 가진 저장 입력을 준비하고 런타임 상태를 먼저 구성한다. 같은 ID의 SourcePolicy에서 MagnetAdjacent만true로 테스트 메모리에서 바꾼다. AdjacentMatch/Power/Hammer는 원래 값이고 나머지 정의·프로필은 정확한 원래 참조다. 인접하고 벽/덮개/보호가 없는 대상의 실제 Evaluate로 actual None/0 대비 want Damage/1 RED를 확보한다. 조회 무변경과 finally의 정확한 등록 참조 복원을 확인하며 공개 등록 수정 API나 문자열 실패를 만들지 않는다.

GREEN은 실제 자석 발동이 일반 블록을 소비하고 PushAdjacent로 생성한 MagnetAdjacent를 공통 Evaluate→Apply 경로로 처리하는 사례를 사용한다. 강제Apply나 Query만의 반응으로 성공을 만들지 않는다. 내구도1~5, 같은/다른 본체, 턴 기록·미션 완료/잔량, 실제 효과의 원인/내구도 전후/제거 본체를 확인한다. 기본false에서는 같은 저장 입력의 현재 결과가 전체 바이트로 동일해야 한다.

거리/벽/비활성/삭제/덮개/보호/null 문맥·이미 피해/다음 턴과 false→true→false를 검사한다. 인접 허용이 덮개와 보호를 우회하지 않으며 색 조건을 Safe에 추가하지 않는다. AdjacentMatch/Power/Hammer/-1/4/99의 기존 전체 결과·메시지와 EF-30 일반 인접 허용 연결을 유지한다. 다른5종과 비장애물의 자석 조회도 전후 비교한다.

새로 정책을 읽는 유효 Safe 자석 대상의 SourcePolicy 누락은 ID 포함 오류로 처리하며false 기본값으로 대체하지 않는다. 기존에는 전단의 종류 제한이 이를 숨겼으므로 이 오류 노출은 명시적인 연결 계약의 차이로 기록한다. 거리/벽 등 선행 거부와 기본 등록의 밸런스를 몰래 바꾸지 않는다.

## 작업과 검증

- [x] work/EF-23~30 미커밋·원본/GUID/enum·과거 출력·무시된 Addressables 상태/패키지 서명을 보호한다. Evaluate→Query→정책과 실제 자석 소비/인접 생성/공통 적용 호출부 및 동일 저장 입력/시드의 전체 기준을 확보한다.
- [x] 검사부터 작성하고 같은 ID Safe 자석 인접만true의 실제 None/0→Damage/1 RED, 조회 무변경, 나머지 정책 값/프로필 참조와 finally 복원을 확인한다.
- [x] Safe 자석 경로의 전단/내부 잔여 거부만 최소 연결한다. 실제 자석 발동·허용 반응의 공통 적용, 전체 경계와 기본false/다른 원인·다른 종류·누락ID 계약을 확인한다.
- [x] 기본 등록의 전체 상태/비공개 문맥/규칙·전역 난수·피해/미션/효과·예약/완료 예상/취소/무효화/재선정·발전기/공급·전체 검증·팩 전체 바이트/본체ID/버전1/50구간을 같은 입력으로 비교한다. 새 검사와 EF-30 최종 기존23종을 각각 별도 Editor 정확한 메서드·실제 종료0/필수FAIL0으로 확인한다.
- [x] 과거 결과/값을 백업→삭제→새 생성→증거 복사→finally 전체 바이트 복원한다. 최종 원본/기존 작업/과거 증거 감사·diff check·보고와 다음 한 단계 계획/목표/전체 복사용 명령문을 마무리한다. 다음 구현은 시작하지 않는다.

필수 기존23종: CapsuleAdjacentPolicyVerification.Run과 [EF-30의 기존22종](stage-30-capsule-adjacent-policy-plan.md). 정확한 메서드는 EF-30 최종 실행 표와 Logs/ElementFramework/Stage30/verified-results.json의 최종23종을 대조한다. Before/Red는 최종 Run의 대체가 아니다.

## 제외와 검토 초점

정의/기본 등록/생성 계약, 다른 종류의 자석 정책 연결, 색 비교/집계/Apply/Remove/미션 진행/드론/발전기/공급/낙하/비행/UI/MVVM/표현/풀/봇/저장/변환/원본 에셋은 변경하지 않는다. 새 생산 정책·프로필·패키지·asmdef를 만들지 않는다. EF-05/09/11·GUID/enum·기존 작업을 보존한다. 커밋/푸시/빌드/재패킹/이미지·팩 재생성/사용자 Unity 종료/씬 저장 금지.

검토 초점: 전단만 바꾼 가짜 연결, 기본false의 거부 메시지/순서 변경, 다른 종류까지 허용, 덮개/보호 우회, 미정의 원인 허용, 필수 정책 누락의false 대체, 허용 반응 없이 강제Apply, 자석 색 선택·소비/범위·규칙 난수 변경.

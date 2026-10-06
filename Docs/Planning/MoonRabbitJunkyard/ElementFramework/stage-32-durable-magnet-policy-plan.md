# EF-32 — 상자·고철·금속기둥 자석 인접 정책 연결 계획

상태: 완료. 실제 RED18 PASS/3 FAIL·종료1, GREEN54307 PASS/0 FAIL·종료0과 기존24종 회귀·최종 보존 감사를 완료했다. 최종 합계427683 PASS/0 FAIL. 다음EF-33 문서만 준비했고 구현은 시작하지 않았다.

실행 담당: superpowers:executing-plans. 에이전트는 별도 요청 시에만 사용한다.

연결: [가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-31 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-31-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-32-durable-magnet-policy-goal.md).

## 근거와 묶는 책임

EF-31은 Safe 자석 인접만 전단/내부 제한에서 기존 정책으로 연결했다. Crate/Scrap/Appliance는 같은 내구도 피해 경로와 같은 두 자석 종류 제한을 사용하지만, 같은 ID의 기존 MagnetAdjacent만true로 바꿔도 실제 Evaluate가None/0이다. 세 종류를 한 번의 **내구도형 자석 정책 연결**로 묶는다. 종류마다 같은 조건 한 줄을 별도 단계로 반복하지 않는다. 단, 본체별/칸별 집계 차이는 각각 실제 실행으로 검증한다.

발전기는 충전 행동이며 기본MagnetAdjacent=true와 외부 전단 제한이 충돌하는 기존 예외가 있으므로 함께 허용하지 않는다. 색 자물쇠의 기존 색 비교·전단 예외와 EF-30/31 Safe 연결도 유지한다. 기본 Crate/Scrap/Appliance.MagnetAdjacent=false는 변경하지 않는다.

## 최소 변경과 검사

생산 후보는 DamageReaction.Evaluate의 자석 전단과 ObstacleDamageRules.Query의 내부 자석 잔여 종류 제한 두 곳이다. 세 내구도형의 기존 RequireDamageSourcePolicy().MagnetAdjacent를 연결한다. 새 정책/프로필·공개 등록 수정 API·새 보조 추상화는 만들지 않는다. 기본false에서 현재 거부 순서·전체 응답·메시지가 같아야 하며, true라도 벽·덮개·보호와 기존 집계를 우회하면 안 된다.

새 DurableMagnetPolicyVerification(+meta)은 저장 입력·Before/Red/Run과 기존 검사 재사용으로 구성한다. 유효 본체/제거 미션에서 런타임을 먼저 구성하고 한 번에 한 종류의 같은 ID만 교체한다. SourcePolicy의 MagnetAdjacent만true이며 다른 세 bool과 정의/프로필 참조는 그대로다. 세 종류 모두 실제 Evaluate의None/0 대비Damage/1 RED를 각각 확인하고 조회 무변경·finally 정확한 원래 등록 복원을 보장한다. 첫 실패로 나머지 두 RED를 생략하지 않는다.

GREEN은 실제 BoardActionExecutor.Swap의 자석 발동→일반 블록 소비→PushAdjacent(MagnetAdjacent)→Evaluate→공통 Apply다. Crate/Scrap의 본체별 턴당1회와 Appliance의 같은hit·같은칸 중복 거부/다른칸·다른hit 집계, 2×2 중첩 범위, 제거 직후 후속 인접 처리·미션/효과를 확인한다. 반응만 확인하거나 강제Apply로 성공을 만들지 않는다. 내구도는 기존 정의의 전체 범위(Crate1~6/Scrap1~5/Appliance1~9)를 사용한다.

각 종류에서 false→true→false, 같은/다른 본체·hit/턴·미션 잔량/완료·null 문맥·거리/벽/덮개/보호/비활성/삭제·색5개/null을 검사한다. 다른6원인(AdjacentMatch/Power/Hammer/-1/4/99)과 나머지 종류/비장애물, ColorLock 및 Safe의 실제 연결을 보존한다. 새로 정책을 읽는 유효 대상의 누락은ID 포함 오류이며false 대체는 없다. 전단 제한이 숨기던 오류의 노출 차이는 기록하고 선행 거부는 보존한다.

## 작업과 검증

- [x] work/EF-23~31 미커밋·원본/GUID/enum·과거 출력·무시된 Addressables 상태/패키지 서명을 보호하고 두 제한 및 실제 자석 소비/공통 적용 호출부, 동일 입력/시드 전체 기준을 확보한다.
- [x] 검사부터 작성해 세 종류 각각 같은 ID의MagnetAdjacent만true 실제None/0→Damage/1 RED를 확보한다. 다른 정책/참조·조회 무변경·finally 정확한 복원을 확인한다.
- [x] 세 내구도형 자석 경로의 두 제한만 최소 연결한다. 전체 내구도/경계·본체/칸 집계·실제 공통 적용·미션/효과·누락ID와 기본false/다른 종류/원인·Safe 연결을 검증한다.
- [x] 기본 등록의 전체 상태/비공개 문맥/규칙·전역 난수·미션/예약/취소/재선정·발전기/공급/전체 검증·팩 전체 바이트/ID/버전1/50구간을 같은 입력으로 비교한다. 새 검사와 EF-31 최종 기존24종을 각각 별도 Editor 정확한 메서드·종료0/필수FAIL0으로 확인한다.
- [x] 과거 결과/값을 백업→삭제→새 생성→증거 복사→finally 전체 바이트 복원한다. 원본/기존 작업/과거 증거 감사·diff check·완료 보고 및 다음 한 단계 계획/목표/전체 복사용 명령문을 마무리하고 다음 구현은 시작하지 않는다.

기존24종은 CapsuleMagnetPolicyVerification.Run과 [EF-31 기존23종](stage-31-capsule-magnet-policy-plan.md)이다. 정확한 메서드는 EF-31 최종 실행 표와 Logs/ElementFramework/Stage31/verified-results.json의 최종24종을 대조한다. Before/RED는 Run 대체가 아니다. 기존 fixture는 실제 실패가 확인된 경우만 최소 조정하며 생성자/누락 기대값을 낮추지 않는다.

## 제외와 검토 초점

정의/기본 정책/생성 계약·발전기/색 자물쇠의 자석 전환·색 비교/집계/Apply/Remove/미션 진행·자석 색 선택/소비/범위/파워 조합·드론/공급/낙하/비행/UI/MVVM/표현/풀/봇/저장/원본 에셋은 수정하지 않는다. 새 생산 정책/프로필/패키지/asmdef를 만들지 않는다. EF-05/09/11·GUID/enum·기존 작업을 보존한다. 커밋/푸시/빌드/재패킹/이미지·팩 재생성/사용자 Unity 종료/씬 저장 금지.

검토 초점: 세 종류 중 일부 RED/실제 실행 생략, 기본false의 메시지/순서 변경, 발전기까지 허용, 2×2를 본체1회로 잘못 축소, 같은칸 중복 피해, 정책 누락false 대체, 강제Apply 성공, 기존Safe/ColorLock 연결 또는 규칙 난수 손상.

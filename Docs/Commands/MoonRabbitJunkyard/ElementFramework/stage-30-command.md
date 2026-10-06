# EF-30 — 회수캡슐 일반 인접 반응 정책 우선 적용 실행 명령문

아래 전체를 복사하여 사용한다.

```text
ServeredMeridian의 EF-30 회수캡슐 일반 인접 반응 정책 우선 적용을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-29-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-30-capsule-adjacent-policy-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-30-capsule-adjacent-policy-goal.md

이번 단계만 수행하고 work와 EF-23~29 미커밋 변경을 유지해. 실제 Evaluate→ObstacleDamageRules.Query→정책 및 공통 적용 호출부를 조사하고 원본/GUID/enum/기존 작업/과거 출력과 동일 저장 입력/시드의 전환 전 전체 결과를 확보해. 무시된 Addressables 상태와 패키지 서명도 보호해.

검사부터 작성해. 유효한 회수캡슐 본체와 제거 미션이 있는 저장 입력에서 같은 ID Safe의 DamageSourcePolicy.AdjacentMatch만 true로 테스트 메모리에서 교체하고 나머지 정의/프로필은 원래 참조를 유지해. 실제 DamageReaction.Evaluate로 인접·벽/덮개/보호 없는 조건의 actual None/0 대비 want Damage/1 RED를 확보해. 조회 무변경과 finally 정확한 등록 참조 복원을 확인하고 공개 수정 API·문자열 실패를 만들지 마.

Query의 Safe 일반 인접 잔여 거부만 최소 수정해. 기본 등록 AdjacentMatch=false와 현재 밸런스는 유지해. 허용된 실제 반응을 공통 적용 경로로 처리해 내구도/턴 기록/제거 미션/효과까지 GREEN을 확인해. 반응을 거치지 않고 강제 Apply해서 성공을 만들지 마. Power/Hammer/MagnetAdjacent·미정의 원인(-1/4/99)의 기존 결과/메시지와 누락 정책의 ID 포함 오류를 유지해.

전체 내구도1~5·같은/다른 본체·미션 잔량/완료·턴/이미 피해/null 문맥·인접/비인접/벽/비활성/삭제/덮개/보호와 false→true→false를 검사해. 기본 등록의 전체 상태/비공개 문맥/규칙·전역 난수·피해/미션/효과·예약/완료 예상/취소/무효화/재선정·발전기 충전/연결/철거·공급 요약/전체 검증·MemoryPack 전체 바이트/본체ID/버전1/50구간 동등성을 확인해.

새 검사와 EF-29 최종 기존22종을 각각 별도 Editor에서 정확한 메서드/실제 입력·응답/종료0/필수FAIL0으로 확인해. 과거 결과/값 출력은 백업·삭제·새 생성 확인·증거 복사 후 finally에서 전체 바이트 동일하게 복원해. 실제 호출 실패가 확인된 fixture만 최소 조정하고 생성자/누락 오류 기대값을 낮추지 마.

정의/기본 등록/생성 계약·DamageReaction.Evaluate와 자석 제한·색 일치/집계/Apply/Remove/미션 진행/드론/발전기/공급/낙하/비행/UI/MVVM/표현/풀/봇/저장/변환/원본 에셋은 변경하지 마. 새 생산 정책/프로필/패키지/asmdef를 만들지 마. EF-05/09/11·원본/GUID/enum·기존 작업을 보존해. 커밋/푸시/빌드/재패킹/이미지·팩 재생성/사용자 Unity 종료/씬 저장 금지. 실패를 숨기거나 목표를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-30-progress.md에 변경·실측·차이·미검증·남은 문제를 기록해. 전체 완료 후 보고하고 다음 한 단계 계획/목표/명령문만 작성해. 실행 명령문을 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에 전체 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

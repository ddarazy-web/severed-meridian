# EF-31 — 회수캡슐 자석 인접 반응 정책 연결 실행 명령문

아래 전체를 복사하여 사용한다.

```text
ServeredMeridian의 EF-31 회수캡슐 자석 인접 반응 정책 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-30-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-31-capsule-magnet-policy-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-31-capsule-magnet-policy-goal.md

이번 단계만 수행하고 work와 EF-23~30 미커밋 변경을 유지해. 실제 Evaluate의 자석 전단 제한→Query의 내부 제한/정책 및 자석 소비→인접 생성→공통 적용 호출부를 조사해. 원본/GUID/enum/기존 작업/과거 출력·무시된 Addressables 상태/패키지 서명과 동일 저장 입력/시드의 전환 전 전체 기준을 확보해.

검사부터 작성해. 유효한 Safe 본체와 제거 미션의 저장 입력에서 런타임 상태를 먼저 구성하고 같은 ID의 DamageSourcePolicy.MagnetAdjacent만true로 테스트 메모리에서 교체해. 다른 정책 값과 나머지 정의/프로필은 원래 참조를 유지해. 실제 Evaluate의 인접·벽/덮개/보호 없는 조건에서 actual None/0 대비 want Damage/1 RED를 확보해. 조회 무변경과 finally 정확한 등록 참조 복원을 확인하고 공개 등록 수정 API·문자열 실패를 만들지 마.

Safe 자석 경로의 전단/내부 잔여 거부만 최소 연결해. 기본 MagnetAdjacent=false/AdjacentMatch=false 밸런스와 거부 순서/전체 응답·메시지, EF-30 일반 인접 연결을 유지해. 실제 자석 발동의 일반 블록 소비에서 생성된 허용 MagnetAdjacent를 공통 Evaluate→Apply로 처리해 내구도/턴 기록/제거 미션/효과까지 GREEN을 확인해. 강제Apply나 Query만으로 성공을 만들지 마.

내구도1~5·같은/다른 본체·미션 잔량/완료·새 턴/이미 피해/null 문맥·거리/벽/덮개/보호/비활성/삭제와 false→true→false를 검사해. AdjacentMatch/Power/Hammer/-1/4/99, 다른5종/비장애물·색 자물쇠 자석 경로는 보존해. 새로 정책을 읽는 유효 Safe의 누락은 ID 포함 오류이고false 대체는 없어야 해. 기존 전단 거부에 숨겨졌던 오류 노출 차이는 명시해.

기본 등록의 전체 상태/비공개 문맥/규칙·전역 난수·피해/미션/효과·예약/완료 예상/취소/무효화/재선정·발전기 충전/연결/철거·공급 요약/전체 검증·MemoryPack 전체 바이트/본체ID/버전1/50구간을 동일 입력으로 비교해. 새 검사와 EF-30 최종 기존23종을 각각 별도 Editor 정확한 메서드·실제 입력/응답·종료0/필수FAIL0으로 확인해. 과거 결과/값은 백업·삭제·새 생성 확인·증거 복사 후 finally 전체 바이트 복원해. 실제 실패가 확인된 fixture만 최소 조정하고 생성자/누락 기대값을 낮추지 마.

정의/기본 등록/생성 계약, 다른 종류의 자석 정책 전환, 색 비교/집계/Apply/Remove/미션 진행/드론/발전기/공급/낙하/비행/UI/MVVM/표현/풀/봇/저장/변환/원본 에셋을 변경하지 마. 자석 색 선택·소비·범위/파워 조합도 그대로 유지해. 새 생산 정책/프로필/패키지/asmdef를 만들지 마. EF-05/09/11·원본/GUID/enum·기존 작업을 보존하고 커밋/푸시/빌드/재패킹/이미지·팩 재생성/사용자 Unity 종료/씬 저장을 하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-31-progress.md에 변경·실측·의도적인 차이·미검증·남은 문제를 기록해. 전체 완료 후 보고하고 다음 한 단계 계획/목표/명령문만 작성해. 전체 명령문을 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

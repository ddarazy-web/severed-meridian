# EF-32 실행 명령문

아래 블록을 복사해 실행한다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-32-durable-magnet-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-32-durable-magnet-policy-goal.md).

```text
ServeredMeridian의 EF-32 상자·고철·금속기둥 자석 인접 정책 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-31-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-32-durable-magnet-policy-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-32-durable-magnet-policy-goal.md

이번 단계만 수행하고 work와 EF-23~31 미커밋 변경을 유지해. Crate/Scrap/Appliance가 공유하는 자석 전단/내부 제한을 한 책임으로 묶어 연결해. 실제 Evaluate→Query→기존 정책 및 자석 발동→일반 블록 소비→PushAdjacent→공통 적용 호출부를 조사해. 원본/GUID/enum/기존 작업/과거 출력·무시된 Addressables 상태/패키지 서명과 같은 저장 입력/시드의 전환 전 전체 기준을 확보해.

검사부터 작성해. 각 종류의 유효 본체/제거 미션에서 런타임을 먼저 구성한 뒤 한 번에 한 종류의 같은 ID SourcePolicy.MagnetAdjacent만true로 테스트 메모리에서 교체해. 다른 세 bool과 나머지 정의/프로필은 원래 참조를 유지해. 세 종류 각각 실제 Evaluate의 인접·벽/덮개/보호 없는 actual None/0 대비 want Damage/1 RED를 확인해. 첫 실패로 나머지 두 RED를 생략하지 마. 조회 무변경과finally 정확한 등록 참조 복원을 보장하고 공개 등록 수정 API·문자열 실패를 만들지 마.

세 내구도형 자석 경로의 전단/내부 잔여 제한만 최소 연결해. 기본MagnetAdjacent=false와 거부 순서/전체 응답·메시지, EF-30/31 Safe 연결·색 자물쇠와 발전기의 기존 자석 예외는 유지해. 실제 BoardActionExecutor.Swap의 자석 발동에서 소비된 일반 블록의 인접 반응이Evaluate→Apply로 처리되어 내구도/기록/제거 미션/효과까지GREEN인지 확인해. 강제Apply나Query만으로 성공을 만들지 마.

Crate1~6/Scrap1~5/Appliance1~9의 전체 내구도를 검사해. 본체별 턴당1회와2×2 칸별 같은hit·같은칸 중복 거부/다른칸·다른hit 처리, 중첩 범위·제거 직후 후속 인접 처리도 실제 실행으로 확인해. false→true→false·같은/다른 본체·새 턴/이미 피해/null 문맥·미션 잔량/완료·거리/벽/덮개/보호/비활성/삭제·색5개/null을 검증해. AdjacentMatch/Power/Hammer/-1/4/99, 나머지 종류/비장애물·Safe/ColorLock/Generator 경로는 보존해. 새로 정책을 읽는 유효 대상의 누락은ID 포함 오류이고false 대체는 없어야 해. 전단 제한이 숨기던 오류 노출 차이는 명시하고 선행 거부는 유지해.

기본 등록의 전체 상태/비공개 문맥/규칙·전역 난수·피해/미션/효과·예약/완료 예상/취소/무효화/재선정·발전기 충전/연결/철거·공급 요약/전체 검증·MemoryPack 전체 바이트/본체ID/버전1/50구간을 같은 입력으로 비교해. 새 검사와 EF-31 최종 기존24종을 각각 별도 Editor 정확한 메서드·실제 입력/응답·종료0/필수FAIL0으로 확인해. 과거 결과/값은 백업·삭제·새 생성 확인·증거 복사 후finally 전체 바이트 복원해. 실제 실패가 확인된fixture만 최소 조정하고 생성자/누락 기대값을 낮추지 마.

정의/기본 등록/생성 계약, 발전기·색 자물쇠의 자석 정책 전환, 색 비교/집계/Apply/Remove/미션 진행/드론/발전기 실행/공급/낙하/비행/UI/MVVM/표현/풀/봇/저장/변환/원본 에셋을 변경하지 마. 자석 색 선택·소비·범위/파워 조합도 그대로 유지해. 새 생산 정책/프로필/패키지/asmdef를 만들지 마. EF-05/09/11·원본/GUID/enum·기존 작업을 보존하고 커밋/푸시/빌드/재패킹/이미지·팩 재생성/사용자 Unity 종료/씬 저장을 하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-32-progress.md에 변경·실측·의도적 차이·미검증·남은 문제를 기록해. 전체 완료 후 보고하고 다음 한 단계 계획/목표/명령문만 작성해. 전체 명령문을Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

# EF-24 — 복사용 목표 실행 명령문

```text
ServeredMeridian의 EF-24 색 자물쇠 색 일치 조회 정책 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-23-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-24-color-match-policy-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-24-color-match-policy-goal.md

이번 단계만 수행하고 work와 미커밋 변경을 유지해. 실제 색 조회/매칭/자석 인접/Power/Hammer·미션/예약/철거 호출부를 조사하고, 색/null·원인4개/-1/4·내구도1~3/0·null/새/같은턴/다음턴·외부 출발색 대체·벽/거리/보호/비활성/삭제의 전환 전 실제 입력/결과와 원본/GUID/과거 증거를 저장해.

계획의 불변 ElementColorMatchPolicy(RequiresMatchingColor, Allows)를 도입하고 ElementDefinition의 기존2/3/4/5인자 생성 계약을 유지하며6인자 경로와 필수 조회를 추가해. obstacle.color-lock에 true 정책을 한 번 등록하고 기존 색 비교 위치를 같은 Get→Require→Allows로 최소 연결해. 누락은 ID 포함 오류이고 기본값을 대체하지 마. true/false 메모리 정책도 같은 카탈로그 조회에 반영됨을 확인해. 내부 null 불일치/외부 null 출발색 대체, -1/4 색 비교, Power/Hammer 색 우회, 조회 순서·반응/양/메시지와 기존5종·발전기 내부 자석 허용/외부 거절·Durability0을 보존해.

동일 저장 입력/시드로 Query 상태/비공개 문맥/규칙·전역 난수 무변경과 실제 피해/턴당1회/제거·미션/효과/예약/연결 철거·MemoryPack 전체바이트/ID/버전1/50구간을 전후 비교해. 검사부터 작성해 누락 RED와 연결 후 GREEN을 확인해.

별도 Editor에서 새 검사와 GeneratorReactionPolicyVerification.Run, ApplianceDamagePolicyVerification.Run, ColorLockDamagePolicyVerification.Run, CapsuleDamagePolicyVerification.Run, ScrapDamagePolicyVerification.Run, CrateDamagePolicyVerification.Run, GeneratorPlacementVerification.Run, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data, PowerEffectVerification.Data를 실행해. 정확한 메서드/실제 입력·응답·결과/각 종료0/필수FAIL0과 과거 출력 백업·복원 바이트 동일을 기록해.

Apply/Remove/충전/미션/예약/턴·칸 집계, 발전기 Query/Apply/연결/철거, 다른 종류 정책, 공급/낙하/드론/봇/UI/MVVM/표현/풀, 제작/배포/저장 포맷/변환은 변경하지 마. EF-05/09/11·원본/GUID/enum 숫자/기존 작업을 보존해. 빌드/재패킹/이미지/팩 재생성/임의 커밋/사용자 Unity 종료/씬 저장 금지. 실패를 숨기거나 목표를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-24-progress.md에 변경·실측·검증·차이·미검증·남은 문제를 저장해. 완료 조건 충족 후 보고하고 결과에 맞춘 다음 한 단계 계획/목표/명령문만 작성해. 명령문을 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에 전체 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

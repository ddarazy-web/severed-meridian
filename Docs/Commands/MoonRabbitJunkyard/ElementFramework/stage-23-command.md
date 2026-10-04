# EF-23 복사용 목표 명령문

```text
ServeredMeridian의 EF-23 발전기 반응 원인 허용 조회 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-22-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-23-generator-reaction-policy-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-23-generator-reaction-policy-goal.md

이번 단계만 수행하고 work와 미커밋 변경을 유지해. 발전기 조회/충전/연결/철거·일반 인접/Power/Hammer·미션/예약 호출부를 조사해. 필요 충전3~5/현재 충전0~완충 전/점유4칸, 원인4개/-1/4·null/새/같은턴/다음턴 문맥·색/null·벽/거리/비활성/보호·삭제의 전환 전 실제 입력/결과와 원본/GUID/과거 증거를 저장해. 삭제 뒤 외부 Empty와 점유를 남긴 내부 Query를 구별해.

기존 ElementDamageSourcePolicy를 재사용해 obstacle.generator에 네 원인 true를 한 번 등록하고 같은 Get→Require→Allows의 읽기 전용 허용 조회에 Generator를 연결해. GeneratorRules.Query 위임은 허용 조회 뒤, 색/내구도/칸별 피해 검사 전으로 최소 이동해. 발전기 Durability0을 제거 상태로 재해석하지 마. 내부 네 원인/미정의 원인의 Charge/AlreadyDamaged·내부 MagnetAdjacent 허용/외부 색 자물쇠 전용 거절·일반 인접 충전·본체별 턴당1충전·반응/양/메시지를 유지해. 누락은 ID 포함 오류이고 기본값을 대체하지 마. 다른 메모리 정책도 같은 카탈로그 조회에 반영됨을 확인해. 기존5종 정책·생성/배치/충전·색 조건/칸별 집계를 보존해.

Query의 상태/비공개 문맥/규칙·전역 난수 무변경과 동일 저장 입력/시드의 실제 충전/완충 작동·연결 대상 제거·직접 대상 제거 후 연결 해제/무충전 철거·미션/효과/예약·MemoryPack 바이트/본체 ID/버전1/50구간을 전후 비교해. 연결 대상4종·복수 대상/복수 발전기·벽/와이어 표시 비의존은 기존 GeneratorVerification의 안전한 메모리 사례를 확인해 재사용해.

별도 Editor에서 새 검사와 ApplianceDamagePolicyVerification.Run, ColorLockDamagePolicyVerification.Run, CapsuleDamagePolicyVerification.Run, ScrapDamagePolicyVerification.Run, CrateDamagePolicyVerification.Run, GeneratorPlacementVerification.Run, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data, PowerEffectVerification.Data를 실행해. 각 실제 입력/응답/결과·종료0·필수 FAIL0과 과거 출력 백업/복원 바이트 동일을 기록해.

GeneratorRules.Query/Apply/충전 수치/작동/철거·Apply/Remove/Mission/예약/턴 집계·다른 종류 정책/색 조건·공급/낙하·드론·봇 DTO·UI/MVVM·표현/풀·제작/배포·저장 포맷/변환/팩 재생성은 변경하지 마. EF-05/09/11·원본/GUID/enum 숫자/기존 작업을 보존해. 빌드·재패킹·이미지·임의 커밋·사용자 Unity 종료·씬 저장 금지. 실패를 숨기거나 목표를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-23-progress.md에 변경·실측·검증·미검증·남은 문제를 저장해. 완료 조건 충족 후 보고하고 결과에 맞춘 다음 한 단계 계획/목표/명령문만 작성해. 명령문을 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에 전체 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

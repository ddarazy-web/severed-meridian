# EF-22 복사용 목표 명령문

```text
ServeredMeridian의 EF-22 금속기둥 상자 피해 원인 허용 정책 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-21-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-22-appliance-damage-policy-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-22-appliance-damage-policy-goal.md

이번 단계만 수행하고 work와 미커밋 변경을 유지해. 금속기둥 Query/Apply/Remove·일반 인접/범위/미션/예약·연결 대상 철거 호출부를 조사하고 내구도1~9/점유4칸/원인4개와 -1/4, hit0/같은hit 같은칸/같은hit 다른칸/다른hit/다음턴/null 문맥·벽/거리/비활성/보호/제거의 전환 전 실제 입력·결과와 원본/GUID/과거 증거를 저장해.

기존 ElementDamageSourcePolicy를 재사용해 obstacle.metal-rod-box에 AdjacentMatch/Power/Hammer 허용·MagnetAdjacent 거절 정책을 한 번 등록하고 같은 Get→Require→Allows의 읽기 전용 허용 조회만 연결해. 누락은 ID 포함 오류이고 기본값 대체하지 마. 다른 메모리 정책도 같은 카탈로그 조회에 반영됨을 확인해. 같은hit의 같은칸만 중복 거절하는 현재 집계·다른칸/다른hit의 반복 피해·미정의 원인·반응/양/메시지·외부 경계와 기존4종 정책/발전기·생성/배치/충전을 유지해.

Query의 상태/비공개 문맥/규칙·전역 난수 무변경과 동일 저장 입력/시드의 실제 일반 매칭·Power/Hammer·제거/미션/효과·예약0/1/2/4칸·연결 대상 파괴/발전기 철거·MemoryPack 바이트/본체 ID/버전1/50구간을 전후 비교해. 로켓2칸·폭탄 중첩1~4칸·조합 확대 범위·자석+자석4칸 피해/내구도 하한을 실제 경로로 검증해. 기존 OverlapData의 안전한 메모리 경로와 증거 소유권을 확인해 재사용하거나 별도 실행해.

별도 Editor에서 새 검사와 ColorLockDamagePolicyVerification.Run, CapsuleDamagePolicyVerification.Run, ScrapDamagePolicyVerification.Run, CrateDamagePolicyVerification.Run, GeneratorPlacementVerification.Run, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data, PowerEffectVerification.Data를 실행해. 각 실제 입력/응답/결과·종료0·필수 FAIL0과 과거 출력 백업/복원 바이트 동일을 기록해.

칸별 집계·Apply/Remove/Mission/예약 피해량·발전기 실행/피해/충전 정책·다른 종류/색 조건 정책·공급/낙하·드론·봇 DTO·UI/MVVM·표현/풀·제작/배포·저장 포맷/원본 변환/팩 재생성은 변경하지 마. EF-05/09/11·원본/GUID/enum 숫자/기존 작업을 보존해. 빌드·재패킹·이미지·임의 커밋·사용자 Unity 종료·씬 저장 금지. 실패를 숨기거나 목표를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-22-progress.md에 변경·실측·검증·미검증·남은 문제를 저장해. 완료 조건 충족 후 보고하고 결과에 맞춘 다음 한 단계 계획/목표/명령문만 작성해. 명령문을 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에 전체 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

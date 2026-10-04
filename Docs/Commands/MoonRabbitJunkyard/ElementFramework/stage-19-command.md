# EF-19 복사용 목표 명령문

```text
ServeredMeridian의 EF-19 고철 뭉치 피해 원인 허용 정책 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-18-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-19-scrap-damage-policy-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-19-scrap-damage-policy-goal.md

이번 단계만 수행하고 work 브랜치를 유지해. 고철 Query/Apply/Remove·고정/유지 공급·미션/예약 호출부와 기존 작업을 확인해. 내구도1~5/네 원인/미정의 원인·동일턴 반복/다음턴·벽/거리/비활성/보호/제거 본체·고정/공급의 실제 전환 전 입력·결과를 저장하고 외부 경계와 내부 조회를 구별해.

기존 ElementDamageSourcePolicy를 재사용해 obstacle.scrap에 일반 인접 매칭·Power·Hammer 허용/MagnetAdjacent 거절 정책을 한 번 준비하고 같은 Get→Require→Allows의 읽기 전용 허용 조회만 연결해. 누락은 ID 포함 오류로 거절하고 기본값을 대체하지 마. 다른 메모리 정책이 같은 카탈로그 조회에 반영됨을 확인해. 상자 정책과 다른4종/미지원·외부 경계·반응/양/메시지·본체별 턴당1피해를 유지해.

Query의 상태·비공개 턴 문맥·규칙/전역 난수 무변경과 동일 저장 입력/시드의 실제 피해/제거·미션·효과·예약 피해·고정/공급 결과·MemoryPack 바이트/본체 ID·버전1/50구간을 전후 비교해. 별도 Editor에서 새 검사와 CrateDamagePolicyVerification.Run, GeneratorPlacementVerification.Run, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data, PowerEffectVerification.Data를 실행해. 안전한 메모리 경로를 확인하고 각 실제 입력/응답/결과·종료0·필수 FAIL0과 과거 증거 보존을 기록해.

다른 종류 피해 정책·Apply/Remove/Mission/턴 집계/예약 피해량·고철 공급·낙하·발전기 충전/연결·드론·봇 DTO·UI/MVVM·표현/풀·제작/배포·저장 포맷·원본 변환·팩 재생성은 변경하지 마. EF-05/09/11·원본/GUID/enum 숫자/기존 작업을 보존해. 빌드·재패킹·이미지·임의 커밋·사용자 Unity 종료·씬 저장 금지. 실패를 숨기거나 목표를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-19-progress.md에 변경·실측·검증·미검증·남은 문제를 저장해. 완료 조건 충족 후 보고하고 결과에 맞춘 다음 한 단계 계획/목표/명령문만 작성해. 명령문은 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에 전체 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

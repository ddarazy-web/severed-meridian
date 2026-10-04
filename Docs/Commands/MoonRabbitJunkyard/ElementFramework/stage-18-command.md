# EF-18 복사용 목표 명령문

```text
ServeredMeridian의 EF-18 나무상자 피해 원인 허용 정책 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-17-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-18-crate-damage-policy-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-18-crate-damage-policy-goal.md

이번 단계만 수행해. work 브랜치를 유지하고 DamageReaction/ObstacleDamageRules.Query·Apply·미션/ReservedDamage 호출부와 기존 작업을 확인해. 상자 내구도1~6/네 피해 원인/동일턴 반복·다음턴·벽/비활성/거리/보호/제거 본체의 실제 전환 전 입력과 결과를 확보해. 외부 경계와 내부 조회 의미를 구분해. 상자의 일반 인접 매칭·Power·Hammer 허용/MagnetAdjacent 거절을 작은 불변 원인 정책으로 정의에 조합하고 읽기 전용 허용 조회만 연결해. 기존2/3/4인자 정의·배치/충전 계약을 유지하고 crate 정책을 한 번 준비해. 누락 정책은 ID 포함 오류로 거절하고 기본값 대체를 하지 마. 다른 메모리 정책도 같은 카탈로그 조회에 반영됨을 확인해.

다른5종/미지원·외부 피해 경계·반응 enum/양/메시지·본체별 턴당1피해·실제 적용/제거/미션/예약 피해 계산은 유지해. Query의 상태/턴 문맥/규칙·전역 난수 무변경과 같은 입력/시드의 실제 피해/제거/미션/발전기 철거·효과/예약 피해량·MemoryPack 바이트/배치 ID·버전1/50구간을 전후 비교해. 별도 Editor에서 새 검사와 GeneratorPlacementVerification.Run, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data를 실행해. PowerEffectVerification.Data는 안전한 메모리 경로인지 확인 후 실행해. 각 실제 입력/반응/결과/종료0·필수 FAIL0과 과거 증거 보존을 기록해.

다른 종류 피해 정책·피해 적용/제거/미션/턴 집계/예약 피해량/낙하/공급·발전기 충전·드론·봇 DTO·UI/MVVM·표현/풀·제작/배포 에셋·저장 포맷·원본 변환·팩 재생성은 전환하지 마. EF-05/09/11·원본/GUID/enum 숫자/기존 변경을 보존해. 빌드·재패킹·이미지 작업·임의 커밋·사용자 Unity 종료·씬 저장 금지. 필수 실패를 숨기거나 목표를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-18-progress.md에 변경·실측·검증·미검증·남은 문제를 저장해. 완료 조건 충족 후 보고하고 결과에 맞춘 다음 한 단계 계획서·목표문서·명령문만 작성해. 명령문은 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에도 전체를 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

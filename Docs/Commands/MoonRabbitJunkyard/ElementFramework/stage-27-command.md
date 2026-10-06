# EF-27 — 실제 타격 기록 집계 연결 실행 명령문

아래 전체를 복사하여 목표 명령문으로 사용한다.

```text
ServeredMeridian의 EF-27 실제 타격 기록 집계 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-26-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-27-damage-record-policy-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-27-damage-record-policy-goal.md

이번 단계만 수행하고 work와 EF-23~26 미커밋 변경을 유지해. 커밋하지 마. 실제 Evaluate/Query→Apply와 기록·내구도 감소·제거/발전기 통보 호출부를 조사하고 원본/GUID/기존 작업/과거 출력과 전환 전 실제 입력/결과를 저장해. 전체 내구도·같은/다른 본체·칸·hit/hit0·같은턴/다음턴·null/0/삭제·발전기/미지원의 직접 경계를 구분해.

검사부터 작성해 기준을 확보해. 같은 ID의 테스트 메모리 true/false 집계 정의로 실제 Query→Apply→재조회/다음 타격의 내구도와 비공개 기록을 검사해 기존 종류 조건의 실제 RED를 확인해. 등록 참조는 finally에서 정확히 복원하고 공개 수정 API나 문자열 실패를 만들지 마.

기존 DamageAggregationPolicy.PerHitCell을 재사용해 유효5종 Apply의 RegisterHit/RegisterDamage 선택만 Get→Require→PerHitCell로 최소 연결해. true는 기존 RegisterHit(hit,cell), false는 기존 RegisterDamage(index)야. 기록 이후 내구도 감소→0이면 Remove→GeneratorRules.TargetRemoved→반환의 순서를 유지해. 유효5종 누락은 ID 포함 오류이고 기본값 대체는 없어야 해. Generator/미지원 직접 경계는 기존 본체 기록을 유지하며 정책을 강제하지 마. 새 정책 타입/메서드/필드/생성 계약을 만들거나 음수/0/null/hit0 경계를 임의 보정하지 마.

GREEN과 동일 저장 입력/시드의 전체 결과·상태/비공개 문맥/규칙·전역 난수·실제 피해/집계·미션/효과·예약/기여/완료 예상/취소/무효화/재선정·발전기 충전/연결/철거·MemoryPack 전체 바이트/본체ID/버전1/50구간 전후 동등성을 확인해. 계획서에 지정한 새 검사와 기존19종을 각각 별도 Editor에서 실행하고 정확한 메서드/실제 입력·응답·결과/각 종료0/필수FAIL0·과거 출력 백업/복원 바이트 동일을 기록해.

Query/ReservedDamage/Remove/RegisterDamage/RegisterHit 자체·턴 기록 구조·MissionProgressRules/DroneTargetManager/예약 저장·GeneratorRules/충전/연결/철거, 기존 정책/카탈로그/생성 계약·공급/낙하/드론/봇/UI/MVVM/표현/풀·제작/배포/저장/변환은 변경하지 마. EF-05/09/11·원본/GUID/enum 숫자/기존 작업을 보존해. 커밋/빌드/재패킹/이미지/팩 재생성/사용자 Unity 종료/씬 저장 금지. 실패를 숨기거나 목표를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-27-progress.md에 변경·실측·검증·차이·미검증·남은 문제를 저장해. 완료 조건 충족 후 보고하고 결과에 맞춘 다음 한 단계 계획/목표/명령문만 작성해. 명령문을 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에 전체 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

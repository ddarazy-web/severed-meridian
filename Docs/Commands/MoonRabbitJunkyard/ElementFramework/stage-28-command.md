# EF-28 — 내구도형 제거 미션 정의 연결 실행 명령문

아래 전체를 복사하여 사용한다. 다음 단계 구현은 이 명령문 실행 지시 후 시작한다.

```text
ServeredMeridian의 EF-28 내구도형 제거 미션 정의 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-27-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-28-removal-mission-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-28-removal-mission-goal.md

이번 단계만 수행하고 work와 EF-23~27 미커밋 변경을 유지해. 실제 Mission 호출부·기여 조회·Apply→Remove→미션 완료·발전기 연결 대상을 조사하고 원본/GUID/기존 작업/과거 출력과 동일 저장 입력/시드의 전환 전 전체 결과를 확보해.

검사부터 작성해. 불변 ElementRemovalMissionProfile.Kind, 유효5종, 누락ID 오류와 기존2~7인자 생성자 호환의 계약 RED→GREEN을 확인해. 기본 프로필을 등록하되 Mission switch는 유지한 상태에서 같은 ID의 다른 미션 프로필을 테스트 메모리에 등록해 실제 기여 조회와 실제 타격→제거 완료가 기존 종류 조건 때문에 실패하는 동작 RED를 확보해. 서로 다른 본체와 양쪽 미션을 가진 유효 입력을 사용하고 등록 참조를 finally에서 정확히 복원해. 공개 수정 API·문자열 실패·계약 부재만으로 연결 RED를 대신하지 마.

ElementDefinition에 읽기 전용 RemovalMissionProfile과 RequireRemovalMissionProfile,8인자 생성자를 추가하고 기존 생성 계약을 보존해. 기본 내구도형5종은 기존 미션 프로필을 등록해. ObstacleDamageRules.Mission의 유효5종만 Get→Require→Kind로 연결해. Generator/미지원은 기존 Crate 반환을 유지하고 정책을 강제하지 마. 기존 불변 검사에는 새 프로필만 추가해.

동일 저장 입력/시드의 전체 결과·상태/비공개 문맥·규칙/전역 난수·실제 피해/미션/효과·예약/기여/완료 예상/취소/무효화/재선정·발전기 충전/연결/철거·MemoryPack 전체 바이트/본체ID/버전1/50구간 동등성을 검증해. 새 메타데이터의 의도적 차이는 숨기지 말고 기록해. 계획의 새 검사와 기존20종을 각각 별도 Editor에서 실행해 정확한 메서드/실제 입력·응답/종료0/필수FAIL0을 확인해. 기존 임시 정의 생성자의 영향은 실제 호출로 판단하고 필요한 fixture만 최소 수정해. 과거 출력은 백업·삭제·새 출력 확인·증거 복사 후 finally에서 전체 바이트 동일하게 복원해.

MissionProgressRules 알고리즘/완료·Remove/Apply/Query/ReservedDamage/턴 기록·DroneTargetManager/GeneratorRules/제작 미션 집계·낙하/공급/비행/UI/MVVM/표현/풀/봇/저장/변환은 변경하지 마. 원본/GUID/enum 숫자·EF-05/09/11·기존 작업을 보존해. 커밋/푸시/빌드/재패킹/이미지/팩 재생성/사용자 Unity 종료/씬 저장 금지. 실패를 숨기거나 목표를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-28-progress.md에 변경·실측·차이·미검증·남은 문제를 기록해. 전체 완료 조건 충족 후 보고하고 결과에 맞춘 다음 한 단계 계획/목표/명령문만 작성해. 명령문을 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에 전체 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

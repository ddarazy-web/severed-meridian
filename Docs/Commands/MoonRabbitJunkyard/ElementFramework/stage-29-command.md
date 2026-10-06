# EF-29 — 최초 장애물 미션 수량 정의 연결 실행 명령문

아래 전체를 복사하여 사용한다.

```text
ServeredMeridian의 EF-29 최초 장애물 미션 수량 정의 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-28-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-29-initial-mission-supply-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-29-initial-mission-supply-goal.md

이번 단계만 수행하고 work와 EF-23~28 미커밋 변경을 유지해. 실제 Supply→Validate→LevelStateBuilder 호출부를 조사하고 원본/GUID/기존 작업/과거 출력 및 같은 저장 입력/시드의 전환 전 전체 결과를 확보해. 무시된 Addressables 상태와 패키지 서명도 보호 목록에 포함해.

검사부터 작성해. 서로 다른 Crate/Scrap 본체와 양쪽 미션이 있는 유효 입력에서 같은 ID Crate의 제거 미션 프로필을 Scrap으로 테스트 메모리에서 교체해. 실제 Initial 수량 actual1/1 대비 want0/2와 실제 Validate의 부족 공급 판단으로 기존 종류 조건의 RED를 확인해. 원래 등록 참조는 finally에서 정확히 복원하고 공개 수정 API·문자열 실패를 만들지 마.

LevelMissionRules.Supply의 최초 장애물 매칭만 기존 Get→RequireRemovalMissionProfile→Kind로 연결해. 제거 미션5종과 내구도형5종에만 필수 조회를 적용해. 본체 항목 수를 세고 내구도/2×2 칸수로 증폭하지 마. 발전기/미지원 장애물과 비제거/미지원 미션에는 필수 프로필을 강제하지 말고 null 장애물 목록의 기존0 반환을 유지해. 유효 대상 누락은 ID 포함 오류이고 기본값 대체는 없어야 해. 새 생산 정책/프로필/생성 계약은 만들지 마.

전체 내구도·본체0/1/2·2×2/다른종 혼합·목표0/1/상한·누락ID/발전기/미지원/null 경계를 검사해. 기존 임시 집계 정의에는 필요시 원래 제거 미션 프로필을 보존하고, 조회용 상태 준비는 임시 정의 교체 전에 수행해. 실제 호출 영향이 확인된 fixture만 최소 수정하고 기존 생성자/누락 오류 기대값을 낮추지 마.

기본 등록의 전체 MissionSupplySummary 필드/문자열·Validate 전체 오류 목록·고정/유지/동적 공급·비제거 미션·전체 실행 상태/비공개 문맥/규칙·전역 난수·미션/효과·예약/기여/완료예상/취소/무효화/재선정·발전기 충전/연결/철거·MemoryPack 전체 바이트/본체ID/버전1/50구간 동등성을 검증해. 새 검사와 계획의 기존21종을 각각 별도 Editor에서 실행하고 정확한 메서드/실제 입력·응답/종료0/필수FAIL0을 확인해. 과거 출력은 백업·삭제·새 결과 확인·증거 복사 후 finally에서 전체 바이트 동일하게 복원해.

수량 산식/공급 생성/Validate 알고리즘·Name·정의/기본 등록/생성 계약·ObstacleDamageRules/미션 진행/드론 관리자/발전기/낙하/비행/UI/MVVM/표현/풀/봇/저장/변환/원본 에셋은 변경하지 마. 원본/GUID/enum 숫자·EF-05/09/11·기존 작업을 보존해. 커밋/푸시/빌드/재패킹/이미지/팩 재생성/사용자 Unity 종료/씬 저장 금지. 실패를 숨기거나 목표를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-29-progress.md에 변경·실측·차이·미검증·남은 문제를 기록해. 전체 완료 조건 충족 후 보고하고 결과에 맞춘 다음 한 단계 계획/목표/명령문만 작성해. 명령문을 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에 전체 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

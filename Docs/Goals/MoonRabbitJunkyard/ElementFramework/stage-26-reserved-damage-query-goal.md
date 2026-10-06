# EF-26 — 내구도형 예약 피해량 조회 연결 목표

상태: 완료. 새7793+기존135120=142913 PASS/0 FAIL, 최종19종 각각 종료0. [검증 기록](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-26-progress.md).

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-26-reserved-damage-query-plan.md) · [EF-25 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-25-progress.md).

- [x] 실제 호출부·전체 내구도/0·칸 수/중복/다른칸·본체/문맥/삭제·발전기/미지원의 전환 전 입력/결과·원본/GUID/기존 작업/과거 증거를 확보했다.
- [x] 기존 불변 집계 정책을 재사용해 유효5종의 ReservedDamage 조건만 연결했다. 같은 ID의 메모리 true/false 정의 조회가 실제 피해량에 반영되는 RED→GREEN과 원래 등록 복원을 확인했다. 새 타입/필드/생성 계약은 없고 발전기/미지원에는 정책을 강제하지 않았다.
- [x] 본체별 최대1/칸별 합산/내구도 상한·0/음수 경계·조회 무변경·반응/기여/완료 예상을 보존했다.
- [x] 실제 예약/취소/무효화·타격/미션/효과·발전기/연결·전체 MemoryPack 바이트/ID/버전1/50구간 전후 동일을 확인했다.
- [x] 새 검사와 기존18종 각각 실제 종료0/필수FAIL0·정확한 메서드/실제 입력·응답·과거 출력 복원을 확인했다.
- [x] EF-05/09/11·원본/GUID/enum/기존 미커밋·변경/차이/미검증/남은 문제를 stage-26-progress.md에 기록했다.
- [x] 완료 보고와 다음 한 단계 계획/목표/전체 복사용 명령문을 제공하고 다음 구현은 시작하지 않았다.

ReservedDamage의 집계 단위 조회 한 책임만 완료 대상이다. 예약 관리자/합산/기여 알고리즘·실제 피해 기록 전환은 제외한다.

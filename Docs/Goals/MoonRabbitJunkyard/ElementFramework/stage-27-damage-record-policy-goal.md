# EF-27 — 실제 타격 기록 집계 연결 목표

상태: 완료. [검증 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-27-progress.md).

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-27-damage-record-policy-plan.md) · [EF-26 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-26-progress.md).

- [x] 실제 호출부·전체 내구도/본체·칸/hit0·같은/다른hit·턴/null/0/삭제·발전기/미지원의 전환 전 입력/결과와 원본/GUID/기존 작업/과거 증거를 확보했다.
- [x] 같은 ID의 메모리 true/false 정의로 Query→Apply→후속 조회/타격의 실제 내구도와 비공개 기록을 검증해 RED→GREEN을 확인하고 원래 등록 참조를 finally에서 복원했다.
- [x] 기존 PerHitCell만 재사용해 유효5종 Apply의 기록 선택만 연결했다. 새 생산 계약/기본값 대체는 없고 누락은 ID 오류다. 발전기/미지원에는 정책을 강제하지 않았다.
- [x] 기록→내구도 감소→제거/발전기 통보/미션·효과 순서와 실제 본체/칸/hit/턴 집계, hit0/null/0 직접 경계를 유지했다.
- [x] 동일 저장 입력/시드의 전체 결과·상태/비공개 문맥/규칙·난수·예약/취소/재선정·발전기/연결·전체 MemoryPack 바이트/본체ID/버전1/50구간 동등성을 확인했다.
- [x] 새 검사와 기존19종 각각 실제 종료0/필수FAIL0·정확한 메서드/실제 입력·응답·과거 출력 복원을 확인했다.
- [x] EF-05/09/11·원본/GUID/enum/기존 미커밋을 보존하고 stage-27-progress.md에 변경·차이·미검증·남은 문제를 기록했다.
- [x] 완료 보고와 다음 한 단계 계획/목표/전체 복사용 명령문을 제공하고 다음 구현은 시작하지 않았다.

Apply의 실제 기록 선택 한 책임만 완료 대상이다. 제거/미션/예약/발전기·전체 실행/저장/드론 전환 완료를 뜻하지 않는다.

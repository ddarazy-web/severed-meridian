# EF-28 — 내구도형 제거 미션 정의 연결 목표

상태: 완료. [검증 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-28-progress.md).

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-28-removal-mission-plan.md) · [EF-27 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-27-progress.md).

- [x] 호출부/전환 전 저장 입력·결과/원본GUID/기존 미커밋/과거 출력 기준을 확보했다.
- [x] 불변 제거 미션 프로필·유효5종·누락ID 오류·기존 생성자 호환의 계약 RED→GREEN을 확보했다.
- [x] 같은 ID의 다른 미션 등록으로 실제 기여 조회와 실제 타격→제거 완료의 동작 RED→GREEN을 확인하고 finally 원래 참조를 정확히 복원했다.
- [x] 유효5종 Mission만 Get→Require→Kind로 연결했고 Generator/미지원의 기존 반환과 다른 실행 알고리즘을 유지했다.
- [x] 기본 등록의 전체 결과/상태/문맥/난수/미션·효과/예약·재선정/발전기·연결/전체팩 바이트·본체ID·버전1/50구간을 보존했다. 새 메타데이터의 의도적 차이를 기록했다.
- [x] 새 검사와 기존20종 각각 별도 Editor 종료0/필수FAIL0, 정확한 실행 메서드/실제 입력·응답·과거 출력 원본 복원을 확인했다.
- [x] 원본/GUID/enum/EF-05/09/11·기존작업 보존 감사와 금지 작업 준수, stage-28-progress.md 결과/미검증/남은 문제 기록을 완료했다.
- [x] 완료 보고·다음 한 단계 계획/목표/전체 복사용 실행문을 제공하고 다음 구현은 시작하지 않았다.

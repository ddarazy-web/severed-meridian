# EF-11 — 월드 보드·효과 오브젝트 풀 기준 목표

상태: 완료. 보드34 PASS+Play48 PASS, 각 종료0. [검증 기록](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-11-progress.md).

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-11-pool-baseline-plan.md) · [가이드라인](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

- [x] 보드/장식/공급/효과와 세션 재시작/전환/파괴의 생성·재사용·반환 호출부를 확인했다.
- [x] 실제 반복·작은→큰→작은 표시의 생성/활성 수·인스턴스 동일성을 확인했다.
- [x] 재사용 전후 sprite·색·transform·order·프레임 상태와 숨김/재활성화를 검사했다.
- [x] 효과/공급 종료·취소·소유자 파괴의 실제 객체 수명과 소스 확인을 구분하여 기록했다. 필수 안전한 실제 검사 누락은 없다.
- [x] EF-10 리소스 핸들과 렌더러 풀 소유권을 구분하고 실제 값·한계·안전한 종료0/필수 FAIL0을 저장했다.
- [x] 생산/원본/GUID/기존 작업을 보존하고 금지 작업을 하지 않았다.
- [x] 완료 보고 및 다음 한 단계 계획·목표·전체 복사용 실행문을 제공했다. 다음 구현은 하지 않았다.

새 풀 구현이 아닌 기존 동작 기준 확보다. 미실행·필수 실패는 완료로 표시하지 않는다.

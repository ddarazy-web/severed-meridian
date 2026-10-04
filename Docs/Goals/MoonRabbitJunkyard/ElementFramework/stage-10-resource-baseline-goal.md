# EF-10 — 레벨 리소스 준비·아틀라스 소유권 기준 목표

상태: 완료. 별도 Editor 200 PASS/0 FAIL, 종료0. [검증 기록](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-10-progress.md).

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-10-resource-baseline-plan.md) · [가이드라인](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

- [x] 세션 시작/재시작/전환의 준비·로딩·소유권/해제 호출부를 확인했다.
- [x] 배치·매칭 파워·공급·미션·효과·층/장치가 필요한 실제 준비 주소 집합과 제외 대상을 확인했다.
- [x] 공유 아틀라스 내부 이미지와 별도 주소 로드를 구분하고 중복 제거/캐시 재사용을 확인했다.
- [x] 준비 전 조회/Dispose 후 호출·실제 취소/실패/보류 Dispose·핸들 해제 순서를 검증했다.
- [x] 실제 입력/준비 주소/수량/수명 순서·미측정 한계를 저장하고 안전한 검사 종료 코드·필수 FAIL/예외0을 확인했다.
- [x] 생산/리소스/아틀라스/Addressables/저장/GUID/기존 작업을 보존했다. 빌드/재패킹·임의 커밋·씬 저장 없음.
- [x] 완료 보고와 EF-11 계획·목표·복사용 실행문을 제공했다. 다음 구현은 하지 않았다.

필수 실패/미실행은 미완료다. 정적 메타데이터로 실제 핸들 수명을 증명하지 않는다. 오브젝트 풀·봇 전환·플랫폼 메모리 성능·새 로더는 이번 범위에 포함하지 않는다.

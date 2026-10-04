# EF-11 — 월드 보드·효과 오브젝트 풀 기준 확보 계획

상태: 완료. 보드34 PASS+Play48 PASS, 각 종료0. [검증 기록](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-11-progress.md).

연결: [가이드라인](integration-guideline.md) · [EF-10 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-10-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-11-pool-baseline-goal.md).

목표: 렌더러/표현 객체의 현재 생성·재사용·초기화·반환 기준을 실제 인스턴스로 확보한다. EF-10의 아틀라스 소유권과 별개로 다루며 공용 풀 전환은 하지 않는다. 실행 담당은 executing-plans, 병렬 에이전트는 별도 요청 시에만 사용한다.

## 작은 작업 단위

1. 기존 변경과 원본 해시를 보호하고 PuzzleWorldBoard의 bodies/decorations/supplyImages/Take, 셀·선택 표현과 효과 표시·종료/취소·세션 재시작/전환·파괴 호출부 및 관련 기존 검사를 읽는다. 현재 목록 재사용과 실제 풀 미사용 구간을 구분한다.
2. 기존 안전한 메모리 사례로 동일 판 반복 Draw와 작은→큰→작은 표시를 검사한다. 렌더러 생성/활성 수와 실제 instanceID, 숨김/재활성화, sprite·색·transform·order 초기화 상태를 기록한다. 원본 프리팹/씬 저장 없이 검사 객체만 소유·파괴한다.
3. 공급/효과의 생성→표시→종료/취소→재사용과 세션 재시작/전환·소유자 파괴를 조사한다. 안전한 독립 실행이 가능한 동작만 실제 검사하고 호출부 검토와 구분한다. 효과 렌더러 반환과 EF-10 아틀라스 핸들 해제를 혼동하지 않는다. 필요한 Editor 검사/meta만 보완한다.
4. 정상 반복뿐 아니라 변경된 상태의 재사용을 비교하여 이전 색/order/프레임/위치의 잔류 여부를 확인한다. 실제 객체/활성 수·순서·미측정 값을 Logs/ElementFramework/Stage11에 저장한다. 누락/오류는 숨기지 않고 생산 수정으로 확대하지 않는다.
5. 안전한 실행 종료0·필수 FAIL0·보호 파일/GUID 보존과 검증 기록을 확인한다. 완료 보고 후 결과에 맞는 다음 한 단계 계획·목표·복사용 명령문을 작성하고 구현은 하지 않는다. 봇 공개 관찰 및 실제 요소 정의 전환은 별도 작은 단계로 판단한다.

## 제약

생산·아트·프리팹·씬·아틀라스/Addressables·저장·GUID·기존 변경 보존. 빌드/재패킹·이미지 생성/수정·임의 커밋·사용자 Unity 종료·씬 저장 금지. 공용 풀 프레임워크, 사전 생성 정책, 새 패키지, 드론/UI/봇 구현 제외. 실기기 메모리/GC 성능 측정은 이번 기준 확보와 별도다. 필수 실제 검사가 불가능하면 미완료로 기록한다.

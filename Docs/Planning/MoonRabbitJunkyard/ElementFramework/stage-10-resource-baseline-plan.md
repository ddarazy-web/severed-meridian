# EF-10 — 레벨 리소스 준비·아틀라스 소유권 기준 확보 계획

상태: 완료. 별도 Editor 200 PASS/0 FAIL, 종료0. [검증 기록](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-10-progress.md).

목표: 초기 배치뿐 아니라 매칭/공급/미션/효과가 사용할 아틀라스 준비와 소유권의 현재 기준을 확보한다. 준비 집합/로딩 수명만 다루고 오브젝트 풀은 별도로 분리한다.

연결: [가이드라인](integration-guideline.md) · [EF-09 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-09-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-10-resource-baseline-goal.md).

실행 담당: superpowers:executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 작은 작업 단위

1. 기존 변경·Editor·보호 파일을 기록하고 PuzzleArtwork.PrepareAsync/PrepareEffectsAsync/LoadAsync/Dispose, BoardSpriteAtlas, 세션 시작/재시작/전환의 실제 소유권과 관련 기존 검사를 읽는다. 전체 PlayMode/UI 검사의 씬/원본 저장 경로를 먼저 구분한다.
2. 초기/매칭 가능 기본 파워, 미션 그림, 고정/유지 공급의 고철/회수, 초기 층/장치/전선과 실제 효과 Paths가 준비하는 주소를 대응표로 확보한다. 등장하지 않고 생성/효과/미션으로도 필요 없는 종류의 별도 주소를 준비하지 않는지 확인한다. 공유 아틀라스 내부 동반 이미지는 불필요한 별도 주소 로드와 구분한다.
3. 기존 안전한 검사/메모리 fixture를 재사용해 실제 준비 주소 집합·중복 제거/캐시 재사용·준비 전 Get/Dispose 후 조회/로드·취소/실패/보류 중 Dispose와 해제 소유권을 검증한다. 실제 Editor 로드가 필요하면 기존 리소스와 현재 로드 방식으로만 별도 안전한 실행을 한다. Addressables 콘텐츠/플레이어 빌드·패커블 변경·대체 로더/생산 인터페이스 추가는 금지한다.
4. 필요한 Editor 전용 검사/meta만 추가하고 입력 레벨/효과·실제 준비 주소/atlas 수·요청/조회/해제/취소 순서·미측정 값을 Logs/ElementFramework/Stage10에 기록한다. 소스 정적 확인만으로 실제 핸들 해제 성공을 주장하지 않는다. 기존 번들/로드 환경이 막히면 실패 원인/필수 미검증을 보고하고 완료를 축소하지 않는다.
5. 안전한 검사 종료 코드·필수 FAIL/예외0·원본 해시/GUID/기존 작업 보존을 확인하고 stage-10-progress.md·계획/목표를 갱신한다. 실제 결과로 EF-11의 가장 작은 다음 작업을 정한다. 후보는 오브젝트 풀 기준이며 봇 공개 관찰은 분리한다. 다음 계획/목표/명령문만 만들고 EF-11 구현은 하지 않는다.

## 제약·위험

생산 코드·리소스/아틀라스·Addressables·저장·원본/GUID·기존 작업을 보존한다. 빌드·임의 커밋·사용자 Unity 종료·씬 저장·이미지 생성/수정·아틀라스 재패킹 금지. 새 리소스 시스템·표현 정의·풀 전환·드론·UI·봇은 제외한다. EF-09의 기존1254px 원본3개와 바닥 표현 차이는 이 단계에서 임의 수정하지 않는다.

위험: 공급에서 나올 종류 누락, 같은 주소 중복 요청, 미사용 장애물 전체 준비, 취소/전환 중 핸들 조기 해제 또는 잔류, Dispose 후 보류 로드 완료의 재사용. 필수 실패는 숨기지 않고 원인/수정 경계를 보고하며 생산 수정까지 확대하지 않는다.

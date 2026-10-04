# EF-17 — 발전기 배치 크기·충전 수치 정의 연결 계획

상태: 완료. 2026-10-04. 큰 구간 B의 발전기 배치 소비 경계 연결.

연결: [가이드라인](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-16 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-16-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-17-generator-placement-goal.md).

## 목적과 범위

EF-16으로 내구도형5종의 배치 수치가 정의를 사용한다. 발전기는 크기2·최대 내구도0·배치 필요 충전량3~5로 별도 의미를 갖는다. 기존 양수 내구도 프로필의 검사를0 허용으로 완화하지 않고 발전기의 크기/배치 충전 허용 범위를 불변 정의에 조합한다. 타입명은 실제 코드 책임에 따라 정한다.

`obstacle.generator`를 기존 호환 카탈로그에 한 번 준비하고 LevelPlacementRules.Size 및 ObstacleValueError의 발전기 배치 충전 검사만 연결한다. MaxDurability의 발전기0 의미, 기존5종 수치·양수 프로필/2인자 정의 계약·미지원 종류 결과를 유지한다. 실제 충전/전선 작동/연쇄·목표/드론·피해/미션 실행은 전환하지 않는다.

## 작업 단위와 검증

1. EF-16 결과와 실제 발전기 Size/배치 충전 검사·편집·레벨 검사·연결/런타임 구성 호출부를 읽는다. 기존 변경/원본/GUID를 보호하고 발전기 크기/0 내구도/충전2~6·2×2 경계/내부벽/중복 점유 및 기존5종/미지원 값의 실제 전환 전 기준을 저장한다.
2. 양수 크기와 유효 배치 충전 범위를 갖는 작은 불변 프로필을 정의에 조합하고 발전기 ID에 한 번 등록한다. 임의 행동 키/충전 실행 정책/새 UI·전역 수정 API·전체 종류 탐색은 만들지 않는다. 같은 메모리 프로필에 다른 수치를 넣어 카탈로그를 통한 실제 조회를 검사한다. 누락 정의/필수 프로필은 ID 포함 오류로 거절한다.
3. 발전기의 배치 크기/충전 허용 범위만 정의를 읽게 한다. 충전3~5 허용·2/6 거절, 내구도0의 기존 의미, 실제2×2 경계/내부벽/중복·본체 ID/전선 연결 결과를 전후 비교한다. 상자 등5종·미지원 반환 의미는 유지한다.
4. 같은 저장 메모리 fixture를 사용해 실제 발전기 충전/활성 연결/대상 제거·피해/미션/규칙·전역 난수·MemoryPack 바이트/버전1/50구간이 유지됨을 확인한다. 기존 EF-15 missing Generator fixture는 이제 카탈로그의 미등록 유효 ID 오류 검사로 조정하며 기존 누락 오류 검증은 유지한다.
5. 별도 Editor에서 새 검사, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data를 실행한다. 과거 증거를 백업/복원하고 실제 입력/수치/오류/각 종료0·필수 FAIL0을 기록한다.
6. stage-17-progress.md와 완료 보고를 작성한다. 실제 결과에 맞춘 다음 한 단계 계획·목표·명령문만 작성하고 다음 구현은 시작하지 않는다.

실행 담당: executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 제외

발전기 실행 정책·피해/미션/낙하/공급·드론·봇 DTO·UI/MVVM·표현/풀·제작/배포 에셋·저장 포맷·원본 변환·팩 재생성은 변경하지 않는다. EF-05/09/11·원본/GUID/enum 숫자/기존 변경을 보존한다. 빌드·재패킹·이미지 작업·임의 커밋·사용자 Unity 종료·씬 저장 금지. 필수 실패를 숨기거나 목표를 축소하지 않는다.

검증: [EF-17 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-17-progress.md). 필수9종2624 PASS/0 FAIL·각 종료0.

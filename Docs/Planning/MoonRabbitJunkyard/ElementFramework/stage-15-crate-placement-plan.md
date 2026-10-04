# EF-15 — 나무상자 배치 수치 정의 연결 계획

상태: 완료. 2026-10-04. 큰 구간 B의 대표1종 연결 단계.

연결: [가이드라인](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-14 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-14-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-15-crate-placement-goal.md).

실행 담당: executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 목적과 범위

ID/메모리 카탈로그는 있지만 기존 실행이 사용하지 않는다. 대표 나무상자1종의 배치 크기와 최대 내구도를 정의에서 읽도록 연결해 첫 소비 경계를 검증한다. 피해/미션·배치 저장/인스턴스 ID·다른5종 전환과 섞지 않는다.

실제 LevelPlacementRules의 Crate는 Size1/MaxDurability6이다. 배치 가능·검사·편집은 이 공통 조회를 사용한다. 동일 반환값을 보존하며 기존 구형 enum을 읽는 호환 경계에서만 LegacyElementMap을 사용한다. 단계 사이에도 구형 원본과 에디터는 동작해야 한다.

## 최소 계약과 변경 후보

- Elements에 불변 배치 수치 프로필을 추가한다. 현재 소비자가 쓰는 Size/MaxDurability만 보유하고 양수 검사를 한다. 임의 모양·새 층·피해/행동 키는 미리 추가하지 않는다.
- ElementDefinition에 이 프로필을 조합한다. 기존 ID/표시명2인자 계약은 유지하고 메타데이터만 있는 정의도 허용한다. 프로필이 없는 정의를 배치 수치로 조회할 때는 ID를 포함해 거절하며 기본 크기/내구도로 대체하지 않는다.
- 나무상자 `obstacle.crate.wood`/나무상자/Size1/MaxDurability6 정의와 읽기 전용 카탈로그를 호환 경계에서 한 번 준비한다. 프레임·조회마다 생성/검색하지 않는다. 전역 수정 가능한 등록표나 DI 전면 전환은 만들지 않는다.
- LevelPlacementRules.Size/MaxDurability의 **Crate 조회만** 이 정의로 연결한다. 다른 종류/미지원 종류의 기존 반환 의미는 그대로 둔다. 이 과도기 분기는 새 콘텐츠마다 추가하는 확장 방식이 아니다. 나머지 종류 이관은 후속 단계다.

파일 후보: Elements/Data/ElementPlacementProfile.cs, ElementDefinition의 조합 확장, Elements/Runtime의 구형 상자 정의 준비·조회 경계, 기존 Obstacles/Rules/LevelPlacementRules.cs, Elements/Editor/Tests의 메모리 검사. 실제 책임에 맞춰 타입명은 확정하고 새 asmdef/패키지/설정 UI는 추가하지 않는다.

## 작업 단위와 검증

1. EF-14 계약과 Size/MaxDurability/배치 가능 검사·편집·레벨 검사 호출부를 읽고 기존 변경/원본/GUID를 보호한다. 전환 전 상자와 다른5종의 실제 반환값·배치 결과를 확보한다.
2. 불변 프로필과 상자 정의를 만들고 기존2인자 정의 계약을 보존한다. 같은 구조에 다른 메모리 수치를 넣어 조회가 데이터에 따름을 검사한다. 런타임 기본 상자 수치를 임의 변경하지 않는다.
3. 상자 배치 수치 조회만 연결한다. 실제 크기1/최대6·내구도1~6 허용/0·7 거절·경계/중복 점유 검사와 다른 종류 반환값을 전환 전후 비교한다. 정의/프로필 누락은 ID 포함 오류로 검사한다.
4. 별도 Editor에서 새 검사, ElementCatalogVerification, ElementIdVerification, FixedObstacleVerification.Data, BotObservationVerification을 실행한다. 원본을 저장하지 않는 메모리 편집/검사 경로만 쓴다. 피해·미션·난수·기존 저장 바이트/enum숫자/배치 Id/원본/GUID 보존도 해당 안전 fixture에서 확인한다.
5. 실제 입력/정의 수치/반환·배치·오류·종료0/필수 FAIL0/한계를 stage-15-progress.md에 기록한다. 완료 보고와 다음 한 단계 계획·목표·전체 복사용 명령문만 작성하고 다음 구현을 시작하지 않는다.

## 제외

다른 장애물/층/파워 전환, 피해·미션·낙하·공급/드론 정책·비행 변경, 봇 DTO에 정의 참조/ID 노출, UI/MVVM·아트·표현/풀, 제작/배포 에셋·저장 포맷·원본 변환·팩 재생성은 제외한다. 빌드·재패킹·이미지·임의 커밋·사용자 Unity 종료·씬 저장 금지. EF-05 예외 원복과 EF-09/11 차이는 보존한다. 필수 실패를 숨기는 규칙 변경이나 완료 조건 축소는 하지 않는다.

검증: [EF-15 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-15-progress.md). 최종 필수5종1837 PASS/0 FAIL·각 종료0.

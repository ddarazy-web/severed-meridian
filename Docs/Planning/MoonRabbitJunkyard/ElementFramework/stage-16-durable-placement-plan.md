# EF-16 — 나머지 내구도형 장애물 배치 수치 연결 계획

상태: 완료. 2026-10-04. 큰 구간 B의 배치 수치 연결 후속 단계.

연결: [가이드라인](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-15 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-15-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-16-durable-placement-goal.md).

## 목적과 경계

EF-15에서 상자의 배치 수치 조회만 연결했으며 다른 규칙은 그대로다. 같은 양수 Size/MaxDurability 계약을 쓰는 나머지4종을 한 번 준비하는 정의 목록에 추가하고 배치 수치만 연결한다. 고철 뭉치·회수 캡슐·색깔 자물쇠는1/5·1/5·1/3, 금속기둥 상자는2/9를 유지한다. 동일 계약에 데이터만 추가하는 작은 작업으로 묶는다.

발전기는 크기2/최대 내구도0·충전3~5의 다른 의미가 있으므로 양수 내구도 프로필에 억지로 넣지 않는다. 발전기/미지원 종류 반환 및 검사 의미를 보존한다. 상자1/6과2인자 메타데이터 정의 계약도 유지한다. 피해 정책·2×2 타격량·숨은 내용물·충전·미션·공급·표현·저장 전환은 이 단계에 포함하지 않는다.

## 작업 단위와 검증

1. EF-15 결과와 정의/호환 경계/Size·MaxDurability/편집·검사 및 고철 공급 호출부를 읽고 기존 변경과 원본/GUID를 보호한다. 기존 안전 fixture의 실제 입력을 저장해4종·발전기·미지원 종류의 전환 전 수치/오류/점유·배치 결과를 확보한다.
2. LegacyElementDefinitions의 읽기 전용 카탈로그에 기존 ID4개와 불변 프로필을 추가한다. 임의 종류별 생성/전체 순회/수정 가능한 전역 등록표를 만들지 않는다. 기존 배치 조회를 이 정의로 연결하고 같은 계약의 신규 종류마다 수치 분기를 복제하는 방향은 피한다. 과도기 enum 경계는 유지한다.
3. 4종 전체 허용 내구도와0/최대+1 거절,1×1/2×2 경계·내부벽·중복 점유·색 조건을 실제 편집/레벨 검사로 비교한다. 상자·발전기·미지원 값도 유지한다. 누락 정의/프로필·무효 수치는 오류를 숨기거나 기본값으로 대체하지 않는다.
4. 전환 전 저장한 메모리 입력을 재사용해 피해/제거/미션·규칙/전역 난수·배치 ID·발전기 연결·MemoryPack 바이트/버전/50구간 보존을 비교한다. 고철 공급 수치 조회 영향도 포함한다. 원본이나 배포 팩을 저장하지 않는다.
5. 별도 Editor에서 새 검사, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run과 고철 공급 관련 안전 검사를 실행한다. EF-15 검사가 일부러 미등록 Scrap을 사용한 오류 fixture는 이제 등록되지 않은 유효 ID로 변경하고 ID 오류 검증을 유지한다. 과거 증거는 백업/복원한다.
6. 실제 입력/수치/오류/편집·피해/미션/난수/바이트/종료0·필수 FAIL0과 보호/한계를 stage-16-progress.md에 저장한다. 결과에 따라 다음 한 단계 계획·목표·전체 복사용 명령문만 작성하며 다음 구현을 시작하지 않는다.

실행 담당: executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다. 이번 문서 작성은 구현 착수가 아니다.

## 제외

피해/미션/낙하/공급 실행 규칙·드론·봇 DTO·UI/MVVM·표현/풀·제작/배포 에셋·저장 형식·원본 변환·팩 재생성은 변경하지 않는다. EF-05/09/11 기준과 기존 작업을 보존한다. 빌드·재패킹·이미지 작업·임의 커밋·사용자 Unity 종료·씬 저장 금지. 필수 실패를 숨기거나 완료 조건을 축소하지 않는다.

검증: [EF-16 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-16-progress.md). 필수8종2479 PASS/0 FAIL·각 종료0.

# EF-15 복사용 목표 실행 명령문

아래를 Codex에 붙여 넣는다. 셸 명령이 아니다.

```text
ServeredMeridian의 EF-15 나무상자 배치 수치 정의 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-14-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-15-crate-placement-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-15-crate-placement-goal.md

이번 단계만 수행해. EF-14 정의/카탈로그와 LevelPlacementRules.Size/MaxDurability 및 배치 검사·편집 호출부를 읽고 전환 전 결과와 기존 작업을 보호해. 불변 Size/MaxDurability 프로필을 정의에 조합하되 기존 ID/표시명2인자 계약은 유지해. 나무상자 obstacle.crate.wood의 Size1/MaxDurability6 정의를 한 번 준비하고 기존 Crate 배치 수치 조회만 연결해. 다른5종과 미지원 종류의 기존 반환 의미는 유지해. 프로필/정의 누락은 ID 포함 오류로 거절하고 기본값 대체를 하지 마.

안전한 메모리 검사로 다른 수치의 프로필도 같은 조회 구조에 반영됨을 확인해. 실제 상자 내구도1~6 허용/0·7 거절·경계/중복 점유와 다른 종류 반환 결과를 전환 전후 비교해. 별도 Editor에서 새 검사, ElementCatalogVerification, ElementIdVerification, FixedObstacleVerification.Data, BotObservationVerification을 실행해. 실제 입력/수치/배치/오류·종료0/필수 FAIL0과 피해/미션/난수/저장 바이트·enum숫자/배치 Id/원본/GUID/기존 변경 보존을 기록해.

다른 종류·피해/미션/낙하/공급·드론·봇 DTO·UI/MVVM·표현/풀·제작/배포 에셋·저장 포맷·원본 변환·팩 재생성은 전환하지 마. EF-05/09/11 기준과 기존 작업을 보존해. 빌드·재패킹·이미지 작업·임의 커밋·사용자 Unity 종료·씬 저장 금지. 필수 실패를 숨기거나 목표를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-15-progress.md에 변경·실측·검증·미검증·남은 문제를 저장해. 완료 조건 충족 후 보고하고 결과에 맞춘 다음 한 단계 계획서·목표문서·명령문만 작성해. 명령문은 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에도 전체를 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

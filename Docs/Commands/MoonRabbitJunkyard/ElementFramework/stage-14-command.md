# EF-14 복사용 목표 실행 명령문

아래를 Codex에 붙여 넣는다. 셸 명령이 아니다.

```text
ServeredMeridian의 EF-14 읽기 전용 정의 메타데이터·카탈로그 조회를 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-13-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-14-catalog-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-14-catalog-goal.md

이번 단계만 수행해. EF-13의 ElementId/default/LegacyElementMap 계약과 기존 변경을 확인·보호하고, Elements 안에 ID·표시명만 가진 불변 메모리 정의와 Count/Get(ElementId) 읽기 전용 카탈로그를 구현해. 생성 시 입력을 독립된 ID 색인으로 복사하고 조회마다 전체 목록이나 Unity Object를 검색하지 마. 기존 게임·편집·봇·저장 소비자는 연결하지 마.

메모리 검사로 6종의 정확한 조회와 표시명 독립, 입력 목록 사후 변경의 영향 없음, Ordinal ID 구별, null/무효/default/중복/미등록 ID의 명시적 거절과 ID 포함 오류를 확인해. 기본 상자 대체를 하지 마. 결정적 ID500개의 정확한 조회·Count500·중복0·입력 역순 결과 동일성도 기록하되 실제 제작 콘텐츠나 전체 성능 검증으로 확대해 주장하지 마.

별도 Editor에서 새 검사, ElementIdVerification, BotObservationVerification을 실행해. 실제 입력/값/오류·종료0/필수 FAIL0·보호 파일/원본/GUID 보존을 확인해. 제작 ScriptableObject·배포 DTO·행동/피해/점유/미션/표현 정책·드론·UI/MVVM·봇 전환·공용 풀·저장 포맷 변경·원본 변환·팩 재생성은 제외해. 기존 enum 숫자·배치 Id/발전기 연결·EF-05/09/11 차이는 보존해. 빌드·재패킹·이미지 작업·임의 커밋·사용자 Unity 종료·씬 저장 금지. 필수 실패는 숨기거나 목표를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-14-progress.md에 변경·실측·검증·미검증·남은 문제를 저장해. 완료 조건 충족 후 보고하고 결과에 맞춘 다음 한 단계 계획서·목표문서·명령문만 작성해. 명령문은 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에도 전체를 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

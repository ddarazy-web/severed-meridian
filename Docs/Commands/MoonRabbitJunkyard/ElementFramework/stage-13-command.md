# EF-13 복사용 목표 실행 명령문

아래를 Codex에 붙여 넣는다. 셸 명령이 아니다.

```text
ServeredMeridian의 EF-13 영구 정의 ID·기존 장애물 매핑을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-12-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-13-element-id-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-13-element-id-goal.md

이번 단계만 수행해. 기존 enum/배치 Id/발전기 연결/저장 경계와 작업 시작 상태를 확인·보호한 뒤 Elements 기능 안에 불변 ElementId와 기존 장애물6종의 명시적 LegacyElementMap만 구현해. 값 보존·Ordinal 동등성/해시·잘못된 값/default 처리, 계획의 정확한 매핑 문자열·중복0·미지원 enum 거절을 안전한 메모리 검사로 확인해. 기존 게임·편집·관찰·저장 소비자는 전환하지 마.

별도 Editor에서 새 검사와 BotObservationVerification 등 안전한 기존 회귀 검사를 실행해. 실제 ID/출력/오류·종료0/필수 FAIL0·보호 파일/enum 숫자/원본/GUID 보존을 기록해. 기존 작업·예외 원복·EF-09/11 차이는 보존해. 카탈로그·정의 에셋·행동/피해/미션·드론·UI/MVVM·봇 전환·표현/풀·저장 포맷 변경·원본 변환·팩 재생성은 제외해. 빌드·재패킹·이미지 작업·임의 커밋·사용자 Unity 종료·씬 저장 금지. 필수 실패는 숨기거나 목표를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-13-progress.md에 변경·실측·검증·미검증·남은 문제를 저장해. 완료 조건 충족 후 보고하고 결과에 맞춘 다음 한 단계 계획서·목표문서·명령문만 작성해. 명령문은 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에도 전체를 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

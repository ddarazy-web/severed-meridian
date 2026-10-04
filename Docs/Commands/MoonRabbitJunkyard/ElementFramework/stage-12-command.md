# EF-12 복사용 목표 실행 명령문

아래를 Codex에 붙여 넣는다. 셸 명령이 아니다.

```text
ServeredMeridian의 EF-12 봇 공개 관찰·숨은 정보 차단 기준 확보를 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-11-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-12-bot-observation-baseline-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-12-bot-observation-baseline-goal.md

이번 단계만 수행해. BotObservationBuilder.Capture와 관찰 DTO, 공개 행동 후보, 기존 BotObservationVerification 및 관련 안전한 검사를 읽어. 기존 메모리 사례를 재사용해 공개 상태의 실제 값·행동 후보, 곰팡이 아래 내용물 등 숨은 정보만 다른 두 입력의 관찰 동일성, 관찰/후보 조회 전후 원본·난수 보존, 관찰 스냅샷의 독립성과 내부 정의/ID/공급/예약/난수 참조 노출 여부를 검증해. 공개 데이터 계약과 실제 봇 전략에서 사용하는 경계를 구분해 기록해. 전략 점수·선택 정책·전체 플레이 승률을 이번 범위로 확대하지 마.

누락된 Editor 검사와 실제 관찰 기록만 보완하고 안전한 별도 Editor 검사를 실행해. 생산 코드·원본/프리팹/씬·아틀라스·Addressables·저장 포맷·GUID·기존 작업을 보존해. 빌드·재패킹·이미지 생성/수정·임의 커밋·사용자 Unity 종료·씬 저장 금지. 새 관찰 인터페이스·봇 전환·정의 카탈로그·공용 풀·드론 비행·UI는 구현하지 마. EF-11에서 확인한 Draw 초기화/Reset 호출 경계와 기존 아트 차이는 임의 수정하지 마. 필수 검사 실패는 원인을 기록하고 완료를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-12-progress.md에 실제 입력/관찰/후보/난수 값과 실행 결과·한계를 저장해. 완료 조건을 충족하면 변경·검증·미검증·남은 문제를 보고하고 결과에 맞춰 다음 한 단계 계획서·목표문서·명령문만 작성해. 명령문은 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에도 전체를 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

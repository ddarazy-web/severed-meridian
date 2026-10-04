# EF-11 복사용 목표 실행 명령문

아래를 Codex에 붙여 넣는다. 셸 명령이 아니다.

```text
ServeredMeridian의 EF-11 월드 보드·효과 오브젝트 풀 기준 확보를 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-10-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-11-pool-baseline-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-11-pool-baseline-goal.md

이번 단계만 수행해. 기존 월드 보드·효과·공급 렌더러의 생성, 재사용, 비활성화, 초기화, 해제 호출부와 안전한 기존 검사를 먼저 확인해. 메모리 사례와 원본을 저장하지 않는 별도 Editor 검사로 생성 수·활성 수·인스턴스 동일성·색/위치/크기/order/프레임 등 실제 재사용 상태를 기록하고, 판 재그리기·작은 판/큰 판·효과 종료/취소·재시작/전환·소유자 파괴의 현재 기준을 확보해. 실제로 검사한 객체 수명과 정적 호출부 확인을 구분해. 리소스 핸들 소유권은 EF-10 기준을 유지하고 렌더러 풀과 혼동하지 마.

누락된 Editor 검사와 실제 결과 기록만 보완해. 생산 코드·원본 에셋/프리팹/씬·아틀라스·Addressables·저장 포맷·GUID·기존 작업을 보존해. 풀 교체·공용 풀 프레임워크·사전 생성 정책·새 리소스 시스템·드론 비행·UI·봇 전환은 구현하지 마. 빌드·재패킹·이미지 수정/생성·임의 커밋·사용자 Unity 종료·씬 저장은 하지 마. 안전한 실제 검사가 막히거나 생산 수정이 필요하면 원인과 미검증을 기록하고 완료를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-11-progress.md에 실제 결과를 저장해. 완료 조건을 충족하면 변경·검증·미검증·남은 문제를 보고하고 결과에 맞춰 다음 한 단계 계획서·목표문서·명령문만 작성해. 명령문은 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에도 전체를 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

# 팝업 프레임워크 2단계 — 복사용 목표 명령문

작성일: 2026-10-02. 상태 갱신: 2026-10-03 · 2단계 구현·검증 완료. 아래는 실행에 사용한 명령이며 재요청 시 기존 완료 증거를 먼저 확인한다. 코드 블록의 복사 버튼으로 클립보드에 복사할 수 있다.

[계획](../../Planning/project-wide/popup-framework-stage-02-plan.md) · [목표·완료 조건](../../Goals/project-wide/popup-framework-stage-02-goal.md)

```text
ServeredMeridian 프로젝트만 대상으로 팝업 프레임워크 2단계를 목표로 설정하고 진행해.

Docs/Systems/project-wide/2026-10-02-popup-framework-design.md,
Docs/Planning/project-wide/popup-framework-stage-02-plan.md,
Docs/Goals/project-wide/popup-framework-stage-02-goal.md,
Docs/Commands/project-wide/popup-framework-stage-02-command.md를 읽어.
전체 계약은 Docs/Planning/project-wide/2026-10-02-popup-framework-plan.md를 참고해.
이 명령은 2단계 계획 승인과 직접 순차 구현(Native) 선택이야.

1단계 완료 기록을 현재 코드와 대조하고 완료된 동작을 재구현하지 마.
같은 목표가 진행 중이면 이어서 하고 다른 미완료 목표와 중복 생성하지 마.

2단계의 세 작업을 직접 순서대로 구현해.
호출자가 보관을 선택한 이동만 복원 가능 팝업의 값 상태/순서/포커스를 깊게 복사해.
Begin은 표시를 유지하고 Commit에서만 보관과 표시 해제를 확정하며 실패하면 Rollback해.
원래 씬과 동일 feature/session 문맥의 준비된 새 Host에서만 명시적으로 Restore해.
다른 씬에서는 표시하지 말고, 같은 씬의 문맥이 바뀌면 이전 보관을 폐기해.
새 비활성 후보의 데이터와 현재 기능 연결을 모두 준비한 뒤 한 번에 활성화해.
성공은 보관 소비, 실패는 후보/구독 정리 후 명시 재시도/Discard로 처리해.
GameObject/delegate/구독/진행 요청/게임 진행 데이터는 저장하지 마.
복원 중 구매/재시작/Next/보상/결과음을 실행하지 마.

동작 실패를 먼저 재현하고 최소 구현 후 실제 두 메모리 씬 왕복을 검사해.
보관 미선택/복원 제외/깊은 복사/문맥 변경/중복 ticket/이동 실패/후보 예외/늦은 콜백을 확인해.
부분 입력0·새 핸들/포커스·외부 차단/정지·소유 자원 정리와 1단계 회귀를 검사해.
네 화면 크기와 안전 영역의 새 캡처를 확인하고 물리 Android 미검증은 명시해.
독립 최종 리뷰를 한 번 수행하고 관련 결함만 수정한 뒤 마지막 변경으로 재검증해.
완료 조건 10개별 증거를 Logs/PopupFramework와 Docs/Verification/project-wide/popup-framework-stage-02.md에 기록해.
Docs/Guides/project-wide/popup-framework-usage.md에는 실제 API의 이동/복원/실패 처리와 소유권 예제를 갱신해.

실제 게임 팝업 전환·관리 창/템플릿 생성 도구·디스크/앱 재실행/게임 진행 저장은 하지 마.
빌드·Addressables 콘텐츠 빌드·커밋·푸시·사용자 Unity 종료·자동 씬 저장·새 패키지/asmdef/DI·풀링은 하지 마.
기존 사용자 변경·1단계·Stage13·레벨/규칙/이미지/씬/기존 프리팹/GUID를 보존해.
모든 조건을 감사한 뒤 2단계 목표만 완료하고 3단계나 다른 목표를 자동 시작하지 마.
```

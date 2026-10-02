# 팝업 프레임워크 1단계 — 복사용 목표 명령문

작성일: 2026-10-02. 상태: 문서 준비 완료 · 1단계 완료. 완료한 작업은 재구현하지 않는다. 아래 코드 블록 복사 버튼으로 클립보드에 복사할 수 있다.

[계획](../../Planning/project-wide/popup-framework-stage-01-plan.md) · [목표·완료 조건](../../Goals/project-wide/popup-framework-stage-01-goal.md)

```text
ServeredMeridian 프로젝트만 대상으로 공통 Popup UI 프레임워크 1단계를 목표로 설정하고 진행해.

Docs/Systems/project-wide/2026-10-02-popup-framework-design.md,
Docs/Planning/project-wide/popup-framework-stage-01-plan.md,
Docs/Goals/project-wide/popup-framework-stage-01-goal.md를 읽어.
전체 계약은 Docs/Planning/project-wide/2026-10-02-popup-framework-plan.md를 참고해.
이 명령은 1단계 계획 승인과 직접 순차 구현(Native) 선택이야.

같은 목표가 진행 중이면 이어서 하고 다른 미완료 목표와 중복 생성하지 마.
이미 완료된 구현은 현재 증거로 보존 여부만 확인하고 반복하지 마.

1단계의 3개 작업을 직접 순서대로 구현해.
고유 핸들 Open/Close·중간 제거·중복 정책·최상위 입력/포커스·입력 차단/정지 요청을 공통 관리해.
기존 uGUI와 직접 프리팹 참조를 사용하고, 아래 팝업은 보이되 최상위만 입력을 받게 해.
입력 한 번으로 여러 팝업이 닫히거나 게임으로 전달되지 않게 해.
외부 입력 차단과 외부 정지를 팝업 종료로 해제하지 마.

동작 실패를 먼저 재현하고 최소 구현 후 Data/실제 EventSystem 입력 검사를 실행해.
4개 화면 크기·안전 영역·포커스·클릭/Submit/Cancel·콜백 중 닫기/재열기·정지 요청 중첩·host 소멸/정리를 확인해.
기존 독립 최종 리뷰가 있으면 재사용하고, 없을 때만 한 번 수행해. 관련 결함만 수정한 뒤 영향 검사를 수행해.
방향키와 같은 프레임의 Submit/Cancel, 비활성/동적 선택, 차단/정지 알림 중 CloseAll/Detach/추가 열기를 회귀 검사해.
중첩 알림 이후 오래된 요청 값이 다른 구독자에게 전달되지 않게 해.
완료 조건 9개별 증거를 Logs/PopupFramework와 Docs/Verification/project-wide/popup-framework-stage-01.md에 기록해.
Docs/Guides/project-wide/popup-framework-usage.md에는 실제 구현된 1단계 API 사용법을 작성해.
실제 물리 Android 입력을 검사하지 않았다면 미검증으로 명시해.

기존 게임의 결과/일시정지/설명 전환, 씬 복원, 관리 창/템플릿 생성 도구는 구현하지 마.
빌드·Addressables 콘텐츠 빌드·커밋·푸시·사용자 Unity 강제 종료·자동 씬 저장·새 패키지/asmdef/DI·풀링은 하지 마.
원본 레벨/씬/규칙/이미지/GUID와 사용자 변경·완료 Stage13을 보존해.
모든 조건을 감사한 뒤 1단계 목표만 완료하고 2단계나 다른 목표를 자동 시작하지 마.
```


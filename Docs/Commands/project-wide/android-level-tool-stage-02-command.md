# Android 레벨툴 2단계 — 복사용 목표 실행문

2026-10-11 · 다음 단계 실행문 사전 작성. 문서 작성은 1단계 검증 완료나 2단계 구현 시작을 의미하지 않는다.

```text
ServeredMeridian의 Android 레벨툴 2단계를 구현·검증해.
다음 문서를 먼저 읽어:
Docs/Planning/project-wide/2026-10-11-android-level-tool-plan.md
Docs/Planning/project-wide/android-level-tool-stage-02-plan.md
Docs/Goals/project-wide/android-level-tool-stage-02-goal.md
Docs/Verification/project-wide/android-level-tool-stage-01-progress.md

1단계의 제품·내부 저장 기반과 검증 상태부터 확인하고,
미완료 전제를 완료로 가정하지 마.
같은 LevelTool 씬과 문서·편집 세션을 사용해.
Android 화면은 기기 이름 대신 Safe Area와 UI 배율을 반영한 사용 가능한 폭으로 배치해.
넓으면 양쪽 패널과 보드, 좁은 세로는 하단 패널, 좁은 가로는 옆쪽 패널을 제공해.
레벨/소재/속성/더보기로 패널을 열고 좁은 화면은 기본적으로 한 개씩 표시해.
저장·Undo/Redo·게임 시험과 현재 층/도구 상태에 접근할 수 있게 해.
대표 화면 배치와 전환 폭은 기존 UI 조사 후 정하고 필요한 질문은 일반 채팅으로 해.

회전·재배치·패널 전환으로 문서·dirty·세션·Undo/Redo·선택을 초기화하지 마.
열린 팝업과 미확정 필드 입력·포커스·커서/텍스트 선택을 보존해.
Safe Area·모바일 배율·약 48dp에 준하는 누름 영역을 함께 검토해.
작은 화면에서도 팝업과 확인/취소가 화면 안에 있고 스크롤 가능하게 해.
화면 키보드에 가려진 필드에 접근하고 한국어 입력을 잃지 않도록 해.
짧은 상태 안내와 도움말은 현재 지원하는 기능만 설명해.
Windows 기존 데스크톱 화면과 16:10 창 정책을 보존해.

고급 화면은 기본 진입·스크롤·닫기 접근을 확인하되 모든 화면을 재작성하지 마.
롱터치·핀치·영역 선택·작업 메뉴는 3단계,
외부 파일 선택기·ZIP 전송·중단 복구는 4단계로 남겨.
필요하면 하위 에이전트를 사용하되 Unity 검사·에셋 변경·프로필 전환은 순차 실행해.
질문은 제한시간 없는 일반 채팅 문답으로 해.
커밋·푸시·Player/APK/Addressables 빌드·HTML 매뉴얼 갱신은 금지해.
Android 모듈이 없으면 임의 설치하지 말고 컴파일 검사를 미완료로 보고해.

배치 경계값뿐 아니라 실제 UI의 대표 폭·방향·팝업·입력 상태를 검사해.
편집→회전→Undo/Redo→저장→재열기와 Windows 회귀를 확인해.
실제 Android 키보드·IME·Safe Area 미검증을 Editor 모의 검사로 완료 처리하지 마.
검사 후 임시 테스트 연결과 프로필·콘텐츠 선택을 복원해.
변경·검증·미검증·제약을 보고하고
Docs/Verification/project-wide/android-level-tool-stage-02-progress.md에 기록해.
완료 후 3단계 계획서·목표 문서·복사용 실행문을 작성하되 자동 구현하지 마.
커밋 메시지는 실제 구현·검증 결과에 맞게 마지막 코드 블록으로 제시해.
```

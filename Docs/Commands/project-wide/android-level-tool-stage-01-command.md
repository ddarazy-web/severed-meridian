# Android 레벨툴 1단계 — 복사용 목표 실행문

2026-10-11 · 문서 작성은 구현 시작이 아니다. 아래 내용을 복사해 실행 요청으로 사용한다.

```text
ServeredMeridian의 Android 레벨툴 1단계를 구현·검증해.
다음 문서를 먼저 읽어:
Docs/Planning/project-wide/2026-10-11-android-level-tool-plan.md
Docs/Planning/project-wide/android-level-tool-stage-01-plan.md
Docs/Goals/project-wide/android-level-tool-stage-01-goal.md
Docs/Verification/project-wide/android-level-tool-stage-01-progress.md

먼저 현재 소스와 진행 기록을 대조해 이미 구현된 항목을 확인해.
기존 구현을 재작성하거나 다른 단계의 작업을 되돌리지 말고,
1단계에서 부족한 구현·검증만 보완해. 재검증하지 않은 결과는 새 통과로 보고하지 마.

기존 Android 게임과 별도의 Android Level Editor 프로필을 만들고,
같은 LevelTool 씬과 공통 편집 로직을 사용해.
PRODUCT_LEVEL_EDITOR를 Android/Windows 공통 도구 심볼로 사용하되
제품 선택과 Addressables 경로는 플랫폼까지 구분해 충돌하지 않게 해.
기존 네 제품과 전역 PlayerSettings, Windows Alt+Enter/F11 창 전환을 보존해.

Android 앱 내부에서 JSON 프로젝트 생성·열기·저장·사본 저장을 제공해.
크기 개선이나 터치 UX를 선행하지 말고, 기존 모달을 이용한 최소 진입 UI로 연결해.
복구·기존 편집본·명시적 인계를 기본 프로젝트보다 우선하고,
최초 기본 프로젝트 생성 이후에는 저장본을 다시 열어.
중복 이름·경로 탈출·손상 데이터·쓰기 실패에서 원본과 현재 작업을 보존해.
JSON 저장·재열기와 공통 게임 시험 요청 생성을 검사해.
Windows 전용 창/폴더 기능이 Android에서 실행되지 않게 해.

휴대폰/태블릿 화면 재배치, 롱터치·다중 터치, 외부 파일 선택기·ZIP 전송은 후속 단계로 남겨.
필요하면 하위 에이전트를 사용하되 Unity 검사·에셋 변경·프로필 전환은 순차 실행해.
질문은 제한시간 없는 일반 채팅 문답으로 해.
커밋·푸시·Player/APK/Addressables 빌드·HTML 매뉴얼 갱신은 금지해.
Android 모듈이 없으면 임의 설치하지 말고 해당 컴파일 검사를 미완료로 보고해.
검사 후 임시 테스트 연결과 프로필·콘텐츠 선택을 복원해.

완료 후 변경·검증·미검증·제약을 보고하고 진행 기록을 작성해.
2단계 계획서·목표 문서·복사 가능한 목표 실행문을 작성하되 자동 구현하지 마.
커밋 메시지는 실제 구현·검증 완료 후 마지막에 복사 가능한 코드 블록으로 제시해.
```

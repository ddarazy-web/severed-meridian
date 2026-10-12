# Android 레벨툴 3단계 — 복사용 목표 실행문

2026-10-11 · 3단계 실행·자동 검증 완료. 아래는 실행 당시 목표문이다. 결과는 [완료 기록](../../Verification/project-wide/android-level-tool-stage-03-progress.md), 다음 작업은 [4단계 실행문](android-level-tool-stage-04-command.md)을 확인한다.

```text
ServeredMeridian의 Android 레벨툴 3단계를 구현·검증해.
다음 문서와 실제 이전 단계 검증 상태부터 확인해:
Docs/Planning/project-wide/2026-10-11-android-level-tool-plan.md
Docs/Planning/project-wide/android-level-tool-stage-03-plan.md
Docs/Goals/project-wide/android-level-tool-stage-03-goal.md
Docs/Verification/project-wide/android-level-tool-stage-02-progress.md

같은 LevelTool 씬·편집 세션·JSON·Undo를 사용하고
터치 판정과 공통 편집 명령을 분리해.
일반/파워 블록·장애물·바닥·덮개를 선택·배치·교체·수정할 수 있게 해.
터치 시작에서 배치하지 말고 짧은 탭은 손을 뗄 때 한 번만 확정해.
롱터치 0.6초와 배율을 반영한 이동 기준을 초기값으로 검증해.
롱터치 메뉴 뒤나 손을 뗄 때 배치가 실행되지 않게 해.
이동·두 번째 손가락·팝업·포인터 취소·회전·앱 중단은 미확정 조작을 취소해.
다중 터치가 끝난 뒤 남은 손가락을 새 탭으로 처리하지 마.

핀치·두 손가락 이동, 이동 도구의 한 손가락 드래그, +/− 버튼을 연결해.
이동·확대는 문서와 Undo를 변경하지 않고 확대 후 대상 칸을 정확히 선택해야 해.
영역/추가 선택과 명시적 교체를 제공해 Shift·더블탭을 필수로 삼지 마.
2×2 장애물은 점유 칸 전체가 같은 개체로 선택·편집되게 해.
롱터치와 선택 후 작업 버튼은 같은 메뉴·공통 명령을 사용해.
필요한 복사·잘라내기·붙여넣기·삭제 명령은 Windows와 공유해.
같은 세션의 클립보드를 우선하고 프로젝트 간 정의 병합·OS 클립보드는 추가하지 마.
경계·충돌·참조를 검증하고 실패 시 원본을 보존해.
확정 편집은 하나의 Undo로 처리하고 미리보기·취소는 문서를 바꾸지 마.
관련 Windows 씬/EditorWindow 메뉴도 같은 편집 의미로 연결해.

기존 JSON 스키마·Windows 마우스/키보드·16:10 화면과
2단계 회전·패널·입력·세션 상태 보존을 유지해.
고급 연결점·튜토리얼 대상·시험 화면 전체를 재작성하지 마.
외부 파일·ZIP·최근 프로젝트·중단 복구는 4단계로 남겨.
질문은 제한시간 없는 일반 채팅으로 해.
필요하면 하위 에이전트를 사용하되 Unity 검사·에셋 변경·프로필 전환은 순차 실행해.
커밋·푸시·Player/APK/Addressables 빌드·HTML 매뉴얼 갱신은 금지해.
Android 모듈이 없으면 임의 설치하지 말고 컴파일 미검증을 보고해.

입력 이벤트 순서·취소에서 실제 편집 횟수와 Undo를 검사하고
UI Toolkit 보드·메뉴의 실제 연결도 확인해.
같은 초기 JSON/명령의 터치·Windows 결과와
대표 요소 편집→Undo/Redo→내부 저장→재열기를 비교해.
실제 Android 터치·OS 뒤로가기 미검증을 Editor 검사로 완료 처리하지 마.
임시 테스트 연결과 프로필·콘텐츠 선택을 복원해.
변경·검증·미검증·제약을 보고하고
Docs/Verification/project-wide/android-level-tool-stage-03-progress.md에 기록해.
완료 후 4단계 상세 계획·목표·복사용 실행문을 작성하되 자동 구현하지 마.
실제 변경·검증 결과에 맞는 짧은 커밋 메시지만 마지막 코드 블록으로 제공해.
```

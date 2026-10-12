# Android 레벨툴 4단계 — 복사용 목표 실행문

2026-10-11 · **실기기·빌드를 제외한 4단계 구현·자동 검증 완료.** 아래는 실행 계약의 기록이다. 다음 작업은 [5단계 실행문](android-level-tool-stage-05-command.md)으로 진행한다.

```text
ServeredMeridian의 Android 레벨툴 4단계 파일 전송·복구를 구현·검증해.
먼저 다음 문서와 실제 이전 단계 검증 상태를 확인해:
Docs/Planning/project-wide/2026-10-11-android-level-tool-plan.md
Docs/Planning/project-wide/android-level-tool-stage-04-plan.md
Docs/Goals/project-wide/android-level-tool-stage-04-goal.md
Docs/Verification/project-wide/android-level-tool-stage-03-progress.md

같은 LevelTool 씬·AuthoringToolWorkspace·AuthoringEditSession·JSON을 유지하고
공통 프로젝트 ZIP과 플랫폼별 시스템 선택기/스트림 경계를 분리해.
기본 편집 장소는 앱 내부 프로젝트 사본으로 해.
Windows 시스템 파일 선택기와 Android 시스템 문서 선택기로 ZIP을 주고받고
Android content:// URI를 일반 경로로 바꾸지 말고 스트림으로 읽고 써.
기존 의존성과 네이티브 경계부터 조사해 임의 플러그인/SDK를 설치하지 마.

ZIP은 정상 저장본의 project.json과 공개 목록의 참조 JSON만 포함해.
복구 기록·Undo·임시 파일·미공개 저장 세대·PC 절대 경로를 제외하고
JSON 스키마·문서/개체 ID·정의/튜토리얼 참조와 리소스 매핑을 유지해.
임시 내부 폴더에서 ZIP 경로·형식·전체 참조를 검증한 뒤 새 프로젝트로 등록해.
기존 프로젝트 병합·덮어쓰기·단일 레벨 전송은 추가하지 마.
경로 탈출·절대/드라이브/UNC 경로·역슬래시·중복/대소문자 충돌·링크 엔트리,
손상 ZIP/JSON·누락/추가 파일·잘못된 참조·취소·쓰기 실패를 거절해.
초기 제한 128 MiB 압축 입력·512 MiB 전체 해제·10,000 엔트리·16 MiB 개별 JSON을
선언값뿐 아니라 실제 읽은 양에도 적용해. 정상 fixture와 Unicode를 확인하고
값을 조정해야 하면 근거를 기록해. seek 불가능한 스트림과 짧은 읽기도 검사해.
실패 시 현재 JSON·세션·dirty·선택·Undo/Redo와 기존 프로젝트를 보존해.

내부 저장 성공과 외부 ZIP 내보내기 성공을 구분해서 안내해.
외부 스트림 쓰기·flush·close 성공 전에 완료라고 표시하지 마.
선택기 취소·권한 거부·오래된/중복 결과·중단 후 복귀를 검사하고 자원을 해제해.
외부 제공자가 실패 시 부분 파일을 남길 수 있는 제약을 정확히 보고해.

최근 내부 프로젝트를 중복 없이 최신순 최대 10개 보관해.
누락/삭제 프로젝트와 손상 최근 기록이 현재 편집본을 덮지 않게 해.
기존 ToolDraftStore와 Workspace.ExportState/RestoreState로 복구를 확장해.
미완성 current 문서·saved 기준·선택·층/도구·뷰·Undo/Redo를 유지해.
pause/focus loss에서 진행 중 터치만 취소하고 확정 작업과 입력 초안을 기록해.
중복/역순 체크포인트가 새 기록을 덮지 않게 하고
resume에서 살아 있는 세션을 파일로 덮어쓰지 마.
뒤로가기는 조작/메뉴/패널/파일 요청 취소를 먼저 처리하고
종료에는 저장/버리기/취소를 제공해. 저장 실패는 종료를 막아.
프로젝트 전환 실패·복구본 손상·이전 복구본 사용과
마지막 체크포인트 이후 OS 강제 종료 시 손실 가능 구간을 검증·기록해.

계획의 네 작업 단위로 진행하고 Windows 기존 화면·입력·저장과
2·3단계 모바일 패널/회전/IME/팝업/터치 취소의 관련 회귀를 유지해.
대표 일반/파워/2×2 장애물·바닥·덮개·내부 연결·튜토리얼 프로젝트의
편집→Undo/Redo→내부 저장→ZIP→새 프로젝트 가져오기→재열기를 비교해.
Portable/Editor 스트림 검사와 실제 UI 메뉴 연결을 모두 확인해.

질문은 제한시간 없는 일반 채팅으로 해.
필요하면 하위 에이전트를 쓰되 Unity 검사·에셋 변경·프로필 전환은 순차 실행해.
커밋·푸시·Player/APK/번들/Addressables 빌드·HTML 매뉴얼 갱신은 금지해.
Android 모듈·플러그인·SDK를 임의 설치하지 마.
가능한 Android 타깃 스크립트 컴파일만 검사하고 임시 연결·프로필·콘텐츠 선택을 복원해.
실제 시스템 선택기·SAF 제공자·OS 뒤로가기·프로세스 종료·PC→Android→PC 왕복을
Editor 모의 검사로 완료 처리하지 말고 미검증으로 남겨.
변경·통과 검사·미검증·제약을
Docs/Verification/project-wide/android-level-tool-stage-04-progress.md에 기록해.
완료 후 5단계 상세 계획·목표·복사용 실행문을 작성하되 자동 구현하지 마.
실제 변경·검증 결과에 맞는 짧은 커밋 메시지만 마지막 코드 블록으로 제공해.
```

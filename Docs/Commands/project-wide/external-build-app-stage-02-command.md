# Windows 외부 빌드 앱 2단계 — 복사용 실행문

```text
ServeredMeridian의 Windows 외부 빌드 앱 2단계를 구현·검증해.
다음 문서를 먼저 읽어:
Docs/Planning/project-wide/external-build-app-integration-plan.md
Docs/Verification/project-wide/external-build-app-stage-01-progress.md
Docs/Planning/project-wide/external-build-app-stage-02-plan.md
Docs/Goals/project-wide/external-build-app-stage-02-goal.md

Tools/BuildApp에 독립 WPF 앱을 만들고 기존 JSON 호스트와 Unity 실행기를 재사용해.
프로젝트·프로필 선택, 버전·옵션·Android 서명, 저장·검사·실행·취소·진행 상태,
로그 미리보기·보관 기간·안전한 정리 UI를 구현해.
앱 버전은 기존 제품 정보 안에 통합하고 등록된 옵션과 플랫폼 기능만 표시해.
마지막 작업 위치와 창 상태를 기억하되 빌드 설정 원본을 이중 저장하지 마.
서명은 기존 DPAPI와 호환되게 하고 평문 비밀번호를 JSON·명령행·로그에 남기지 마.

외부 앱 컴파일과 모의 실행, 실제 read-only 조회·사전 검사까지만 수행해.
실제 Player/Addressables 빌드와 실제 비밀번호 사용, 실제 로그 삭제는 하지 마.
복제 설정·임시 자격 증명·임시 로그로 검증해.
기존 미커밋 변경을 보존하고 커밋·푸시·배포·HTML 매뉴얼 갱신은 하지 마.
질문은 제한시간 없는 일반 채팅으로 해.

완료 후 변경·검증·미검증을 보고하고 3단계 계획·목표·복사용 실행문을 작성해.
3단계 구현은 자동 시작하지 마.
마지막에 완료한 작업의 간략한 커밋 메시지를 복사 가능한 코드 블록으로 제시해.
```

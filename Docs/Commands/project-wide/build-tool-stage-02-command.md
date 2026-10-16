# 제품 빌드 도구 2단계 — 복사용 목표 실행문

2026-10-16 · 후속 실행문. 이 문서 작성만으로 2단계를 시작하지 않는다.

```text
ServeredMeridian의 제품 빌드 도구 2단계를 구현·검증해.
다음 문서를 먼저 읽어:
Docs/Planning/project-wide/2026-10-15-build-tool-plan.md
Docs/Planning/project-wide/build-tool-stage-02-plan.md
Docs/Goals/project-wide/build-tool-stage-02-goal.md
Docs/Verification/project-wide/build-tool-stage-01-progress.md

Match → Build → 빌드 도구의 기존 조회·사전 검사 계약을 유지하면서
정규 제품 프로필의 빌드 요청·실행·결과 표시를 연결해.
공통 실행기, 등록된 플랫폼 처리기, 제품 정의의 책임을 분리하고
플랫폼별 switch나 제품 정보 중복 상수를 늘리지 마.

프로필 GUID와 실행 대상을 확정한 뒤 실제 프로필 전환·재컴파일 대기를 처리해.
재로드에도 요청을 잃거나 중복 빌드하지 말고, 에디터 재시작 뒤에는 자동 실행하지 마.
빌드 API 호출이 이미 시작된 요청은 완료 증거 없이 재호출하지 마.
현재 ProductProfiles와 기존 콘텐츠·빌드 콜백을 재사용하고 중복 처리하지 마.
복제 프로필은 원본 프로필 사용을 안내하고 이번 단계의 실제 실행 대상에서 제외해.
고정 출력 계약을 유지하고 GPG·플러그인 옵션은 아직 구현하지 마.

성공·실패·취소·부분 실패·정리 실패의 상태와 로그를 남겨.
빌드한 프로필 유지와 소유 임시 설정 복원을 구분해.
실제 빌드 백엔드와 모의 백엔드를 분리해 상태·재로드·중복 방지를 검증해.
1단계 조회·선택·사전 검사의 무변경 검사도 다시 확인해.

실제 Player/APK/AAB/iOS 출력·Addressables 번들 빌드는 별도 승인 전까지 실행하지 마.
커밋·푸시·배포·SDK 설치·HTML 매뉴얼 갱신도 하지 마.
질문은 제한시간 없는 일반 채팅으로 하고 기존 변경을 보존해.

완료 후 실제 변경·검증·미검증·제약을 보고하고
Docs/Verification/project-wide/build-tool-stage-02-progress.md에 기록해.
3단계 계획서·목표·복사 가능한 실행문을 작성하되 자동 구현하지 마.
실제 완료한 구현에 대한 커밋 메시지만 마지막에 복사 가능한 코드 블록으로 제시해.
```

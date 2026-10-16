# 제품 빌드 도구 3단계 — 복사용 목표 실행문

계획 작성만으로 실행하지 않는다. 아래를 복사해 별도로 요청한다.

```text
ServeredMeridian의 제품 빌드 도구 3단계를 구현·검증해.
다음 문서를 먼저 읽어:
Docs/Planning/project-wide/2026-10-15-build-tool-plan.md
Docs/Planning/project-wide/build-tool-stage-03-plan.md
Docs/Goals/project-wide/build-tool-stage-03-goal.md
Docs/Verification/project-wide/build-tool-stage-02-progress.md

기존 조회·실행·재로드·정리 계약을 유지하면서 제품/플랫폼별 옵션 등록과 선택 저장을 추가해.
하나의 옵션에 컴파일 준비·빌드 전처리·후처리·정리를 조합할 수 있게 해.
옵션 ID·버전·기본값·적용 대상·의존/충돌을 등록하고 해당 항목만 표시해.
실행값은 요청에 확정하고 ON→OFF·제품 전환에도 이전 설정이 남지 않도록 해.
자신의 설정 변경과 외부 변경을 구별하며 재로드 중복 적용·빌드 재호출을 막아.
부분 적용·후처리·기록 실패에도 소유 임시 상태를 역순 정리하고 충돌을 보고해.
실제 SDK 처리기가 없으면 동작하는 GPG/플러그인 옵션처럼 표시하지 마.

검사 전용 옵션과 모의 백엔드로 검증하고 기존 1/2단계 회귀 검사도 수행해.
실제 Player/APK/AAB/iOS·Addressables 빌드는 별도 승인 전까지 실행하지 마.
SDK/패키지 설치·제거, 커밋·푸시·배포·HTML 매뉴얼 갱신은 하지 마.
기존 미커밋 변경을 보존하고 질문은 제한시간 없는 일반 채팅으로 해.

완료 후 실제 변경·검증·미검증·SDK별 후속 작업을 보고하고
Docs/Verification/project-wide/build-tool-stage-03-progress.md에 기록해.
후속 작업은 자동 구현하지 마.
실제 완료한 내용의 커밋 메시지만 마지막에 복사 가능한 코드 블록으로 제시해.
```

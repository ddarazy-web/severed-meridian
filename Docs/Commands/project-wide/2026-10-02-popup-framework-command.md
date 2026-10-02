# 팝업 프레임워크 — 단계별 복사용 목표 명령문

작성일: 2026-10-02. 상태 갱신: 2026-10-03 · 1~2단계 완료, 3단계 문서 준비·구현 미착수. 아래 명령은 직접 실행(Native)을 선택할 때 사용한다. 코드 블록 복사 버튼으로 복사할 수 있다. 완료 증거는 각 단계의 별도 검증 문서를 따른다. 3단계 실행에는 사용자의 별도 요청이 필요하다.

[계획](../../Planning/project-wide/2026-10-02-popup-framework-plan.md) · [완료 조건](../../Goals/project-wide/2026-10-02-popup-framework-goal.md)

## 1단계 — 관리·입력

[최신 1단계 독립 목표 명령문](popup-framework-stage-01-command.md)을 사용한다. 아래는 전체 계획 작성 당시 명령이며 독립 계획의 상세 조건을 우선한다.

```text
ServeredMeridian만 대상으로 공통 팝업 프레임워크 1단계를 목표로 설정하고 진행해.
Docs/Systems/project-wide/2026-10-02-popup-framework-design.md,
Docs/Planning/project-wide/2026-10-02-popup-framework-plan.md,
Docs/Goals/project-wide/2026-10-02-popup-framework-goal.md를 읽고 계획의 1단계만 직접 순차 구현해. 이 명령은 계획 승인 및 Native 실행 선택이야.
같은 목표가 진행 중이면 이어서 하고 다른 미완료 목표와 중복 생성하지 마. 이미 완료된 단계면 현재 보존 여부만 확인하고 구현을 반복하지 마.
Open/Close 핸들·중간 제거·중복 정책·최상위 입력/포커스·일시정지 요청을 구현해. 실제 EventSystem에서 아래 팝업/보드로 입력이 누출되지 않고 입력당 한 동작인지 검사해. 외부 입력 차단/정지는 보존해.
실패 재현 후 최소 구현과 검증을 수행하고 완료 조건 1~4의 근거를 Logs/PopupFramework와 Docs/Verification/project-wide/popup-framework-stage-01.md에 남겨. 선택한 실행 스킬의 최종 리뷰를 수행하고 관련 결함만 수정해.
기존 게임 팝업 전환과 2단계는 시작하지 마. 기존 사용자 변경·레벨·규칙·GUID를 보존해. 빌드·커밋·푸시·사용자 Unity 종료·자동 씬 저장·새 패키지 추가는 하지 마. 증거 확인 후 이 단계 목표만 완료해.
```

## 2단계 — 선택적 보관·복원

[최신 2단계 독립 목표 명령문](popup-framework-stage-02-command.md)을 사용한다. 아래는 전체 계획 작성 당시 명령이며 독립 계획의 상세 조건을 우선한다.

```text
ServeredMeridian만 대상으로 공통 팝업 프레임워크 2단계를 목표로 설정하고 진행해.
Docs/Systems/project-wide/2026-10-02-popup-framework-design.md,
Docs/Planning/project-wide/2026-10-02-popup-framework-plan.md,
Docs/Goals/project-wide/2026-10-02-popup-framework-goal.md를 읽어. 1단계 완료 증거를 현재 코드와 대조하고 실제 미완료면 먼저 보고해. 완료 구현은 반복하지 마.
이 명령은 계획 승인 및 Native 실행 선택이야. 계획의 2단계만 직접 순차 구현해. 같은 목표는 이어서 하고 다른 미완료 목표와 중복 생성하지 마.
이동 호출자의 보관 선택, 복원 가능 팝업의 값 캡처, 이동 commit/rollback, 원래 씬과 동일 논리 게임 문맥 검증, 후보 일괄 복원을 구현해. GameObject/콜백/진행 요청과 게임 진행 데이터는 저장하지 마.
실제 두 씬 왕복·보관 미선택·문맥 변경·깊은 복사·이동 실패·복원 예외·소멸 정리를 검사해. 명령/결과음 재실행과 부분 복원 입력은 없어야 해.
조건 5~8과 1단계 영향 검사의 증거를 Logs/PopupFramework와 Docs/Verification/project-wide/popup-framework-stage-02.md에 남겨. 선택한 실행 스킬의 최종 리뷰와 관련 수정 검증 후 이 단계만 완료해.
3단계는 시작하지 마. 빌드·커밋·푸시·사용자 Unity 종료·자동 씬 저장·새 패키지 추가는 하지 마. 기존 사용자 변경·레벨·규칙·GUID를 보존해.
```

## 3단계 — 도구·추가 패턴·기존 전환

```text
ServeredMeridian만 대상으로 공통 팝업 프레임워크 3단계를 목표로 설정하고 진행해.
Docs/Systems/project-wide/2026-10-02-popup-framework-design.md,
Docs/Planning/project-wide/2026-10-02-popup-framework-plan.md,
Docs/Goals/project-wide/2026-10-02-popup-framework-goal.md를 읽어. 1~2단계 완료 근거를 현재 코드와 대조하고 실제 미완료면 먼저 보고해. 완료 구현을 반복하지 마.
이 명령은 계획 승인 및 Native 실행 선택이야. 계획의 3단계만 직접 순차 구현해. 같은 목표는 이어서 하고 다른 미완료 목표와 중복 생성하지 마.
관리 창·정책 등록/검증·실시간 순서/입력/정지/복원 대기 조회·시험 열기/닫기·템플릿 생성을 구현해. 기존 파일을 덮어쓰지 마.
기존 일시정지·결과·설명을 공통 시스템으로 전환하고 지정 프리팹/생성 도구만 연결해. 중첩 정지와 외부 정지를 분리하며 기존 Retry/Next/Asset 안내/오디오를 보존해. 기존 씬은 재생성하지 마.
실제 UI·화면 비율·안전 영역·템플릿 생성/컴파일·생성 도구 재실행·재시작/Next·백그라운드·씬 종료와 앞 단계 영향 검사를 확인해. 독립 최종 리뷰를 한 번 수행하고 관련 결함만 수정해.
조건 9~12와 전체 조건별 증거를 Logs/PopupFramework와 Docs/Verification/project-wide/popup-framework-stage-03.md에 남기고 Docs/Guides/project-wide/popup-framework-usage.md를 작성해. 미검증 실기기 입력은 명시해.
빌드·커밋·푸시·사용자 Unity 종료·자동 씬 저장·새 패키지/asmdef/DI·게임 진행 저장은 하지 마. 사용자 변경·레벨·규칙·이미지·GUID를 보존해. 모두 입증한 뒤 목표를 완료하고 다음 목표를 자동 시작하지 마.
```


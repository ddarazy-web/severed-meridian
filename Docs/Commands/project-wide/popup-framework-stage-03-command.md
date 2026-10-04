# 팝업 프레임워크 3단계 마무리 — 복사용 목표 명령문

작성일: 2026-10-03. 상태: 3단계 완료. 아래는 마무리 실행 이력 및 재확인용 명령문이다. 코드 블록의 복사 버튼으로 클립보드에 복사한다. 기존 활성 3단계 목표를 이어가며 새 목표를 중복 생성하지 않는다.

[계획](../../Planning/project-wide/popup-framework-stage-03-plan.md) · [목표·완료 조건](../../Goals/project-wide/popup-framework-stage-03-goal.md) · [기존 검증 기록](../../Verification/project-wide/popup-framework-stage-03.md)

```text
ServeredMeridian 팝업 프레임워크 3단계의 최종 검수·완료 판정을 확인해.
이미 완료되어12개 조건의 최종 증거가 있으면 재구현/새 목표 생성 없이 확인 결과만 보고해.
미완료 조건이나 회귀가 확인되는 경우에만 아래 마무리 절차를 이어서 진행해.
Docs/Planning/project-wide/popup-framework-stage-03-plan.md의
'다음 실행 계획 — 3단계 최종 검수·완료 판정'과
Docs/Goals/project-wide/popup-framework-stage-03-goal.md,
Docs/Verification/project-wide/popup-framework-stage-03.md,
Docs/Systems/project-wide/2026-10-02-popup-framework-design.md를 읽어.

이미 활성 상태인 3단계 목표를 이어서 진행하고 중복 목표를 만들지 마.
완료된 1~2단계와 구현된 도구·게임 연결·재시작·복원을 보존하고 재구현하지 마.
현재 HEAD/status, 원본 기준과 진행 원장, 실제 결과 파일을 먼저 대조해.
최신 증거는 도구/선행 회귀 11회·213 PASS, 게임 묶음9회·2453 PASS,
게임 묶음 내 기존 회귀2132 PASS, UI 조작97 PASS, 생성 도구18 PASS야.
과거 실패 이력과 최신 실제 결과를 구분하고 요약만으로 완료를 판정하지 마.
final-gate-manifest.json의22회/2781 PASS와 원본 결과/exit 및 계약 수정 근거를 확인해.

직접 순차 수행(Native)으로 마무리 작업 A→B→C를 진행해.
A: 기존 최종 리뷰 유무를 확인하고, 없으면 최종 독립 리뷰 한 번을 수행해.
추적된 diff와 미추적 파일 모두를 포함하고 리뷰 집중 항목 다섯 개를 확인해.
관련 결함은 재현 실패→최소 수정→동일 검사 통과로 처리하고 판단 근거를 기록해.
B: 마지막 변경에 영향받는 검사와 화면 확인을 마무리해.
기존 성공 증거가 현재 변경에 적용 가능한지 확인하고 필요한 검사만 재실행해.
결과의 FAIL0와 소유 Unity 프로세스의 실제 exit0를 함께 확인해.
네 화면 크기와 비영점 안전 영역 캡처를 실제 열어 확인하고,
최상위 입력·포커스·raycast와 기존 Retry/Next/Asset/오디오 동작을 보존해.
C: HEAD/diff/원본 해시/기존 GUID와 소유 시험 자원 정리를 감사해.
12개 완료 조건 각각의 증거를 검증 문서에 연결하고 사용 안내/색인을 갱신해.

빌드·Addressables 콘텐츠 빌드·커밋·푸시는 하지 마.
사용자 Unity 종료·씬 자동 저장·새 패키지/asmdef/DI/풀링/디스크 저장은 하지 마.
UI 디자인·게임 규칙·레벨·이미지와 기존 사용자 변경을 보존해.
소유한 시험 자원만 정리하고 물리 Android 미검증은 분명히 기록해.
12개 조건을 모두 입증한 뒤에만 기존 3단계 목표를 완료해.
증거가 부족하면 남은 항목을 보고하고, 다른 단계는 자동 시작하지 마.
```

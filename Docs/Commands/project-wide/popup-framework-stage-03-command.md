# 팝업 프레임워크 3단계 — 복사용 목표 명령문

작성일: 2026-10-03. 상태: 문서 준비 완료 · 구현 미착수. 코드 블록의 복사 버튼으로 클립보드에 복사할 수 있다. 이 명령을 보내면 계획 승인과 직접 순차 구현(Native)을 선택한 것으로 본다.

[계획](../../Planning/project-wide/popup-framework-stage-03-plan.md) · [목표·완료 조건](../../Goals/project-wide/popup-framework-stage-03-goal.md)

```text
ServeredMeridian 프로젝트만 대상으로 팝업 프레임워크 3단계 계획을 승인하고 직접 순차 구현(Native)으로 진행해.

Docs/Systems/project-wide/2026-10-02-popup-framework-design.md,
Docs/Planning/project-wide/popup-framework-stage-03-plan.md,
Docs/Goals/project-wide/popup-framework-stage-03-goal.md,
Docs/Commands/project-wide/popup-framework-stage-03-command.md를 읽어.

먼저 2단계 완료 여부를 현재 소스와 증거에서 확인해.
미완료라면 기존 2단계 목표에서 캡처 중 Host 종료, 이동 예제 입력 차단 해제,
복원 후 실제 현재/이전 명령 수신 검증과 최종 리뷰 처리·재검증·보존 감사를 마무리해.
2단계 완료 전에는 실제 게임 팝업 전환을 시작하거나 3단계 목표를 중복 생성하지 마.
2단계가 완료된 뒤 3단계를 목표로 설정해. 이미 같은 목표가 활성 상태면 이어서 해.

3단계의 세 작업을 직접 순서대로 구현해.
등록 검사·관리 창·읽기 전용 실행 조회와 덮어쓰지 않는 State/View/프리팹 템플릿을 만들어.
기존 일시정지·결과·설명을 공통 PopupService/Host와 현재 게임 Binding으로 전환해.
중첩·중간 제거·최상위 입력·독립 정지/차단 소유권과 기존 디자인/Retry/Next/Asset 안내를 보존해.
Refresh가 팝업을 재생성하거나 순서를 바꾸지 않게 해.
재시작/Next 성공에만 새 논리 문맥을 확정하고 실패/취소는 이전 문맥을 유지해.
필요한 같은 문맥에만 명시 복원하며 복원 자체가 명령/보상/결과음을 실행하지 않게 해.
현재 게임에 없는 왕복 씬 기능이나 게임 진행 저장은 추가하지 마.

각 작업의 동작 실패를 먼저 확인하고 최소 구현 후 실제 도구/게임 검사를 실행해.
1~2단계와 관련 Stage13/UI/입력/오디오 회귀, 네 화면 크기와 안전 영역을 검증해.
독립 최종 리뷰 한 번과 관련 수정 후 재검증을 수행해.
Docs/Goals/project-wide/popup-framework-stage-03-goal.md의 12개 조건별 증거를
Docs/Verification/project-wide/popup-framework-stage-03.md와 Logs/PopupFramework/Stage03에 기록해.
Docs/Guides/project-wide/popup-framework-usage.md와 문서 색인을 갱신해.

빌드·Addressables 콘텐츠 빌드·커밋·푸시·사용자 Unity 종료·자동 씬 저장은 하지 마.
새 패키지/asmdef/DI/풀링/디스크 저장·UI 디자인/게임 규칙/레벨/이미지 변경은 하지 마.
기존 사용자 변경과 프리팹/씬/GUID를 보존하고 소유한 시험 자원만 정리해.
미검증 물리 Android는 완료로 표현하지 마.
모든 조건을 입증한 뒤 3단계 목표만 완료하고 다른 작업을 자동 시작하지 마.
```

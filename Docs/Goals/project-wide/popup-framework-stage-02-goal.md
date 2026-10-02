# 팝업 프레임워크 2단계 목표·완료 조건

작성일: 2026-10-02. 상태 갱신: 2026-10-03 · 구현·리뷰 처리·8회/157 PASS와 완료 조건 감사 완료. 최종 증거는 연결한 검증 기록을 따른다.

[계획](../../Planning/project-wide/popup-framework-stage-02-plan.md) · [복사용 명령문](../../Commands/project-wide/popup-framework-stage-02-command.md) · [설계](../../Systems/project-wide/2026-10-02-popup-framework-design.md) · [1단계 완료](../../Verification/project-wide/popup-framework-stage-01.md)

## 목표

필요한 씬 이동에서만 복원 가능 팝업의 상태를 메모리에 보관하고, 원래 씬·같은 논리 기능/게임 세션의 준비된 새 Host에 명시적으로 복원한다. 이동 확정/취소와 복원 성공/실패를 분리하며 부분 복원 입력과 행동 재실행을 막는다.

## 완료 조건

- [x] 1. preserve=true에서 Restorable 항목만 아래→위 순서로 깊게 복사하며 복수 같은 종류도 구분한다. 캡처 후 원본 중첩 값 수정이 보관 데이터에 영향을 주지 않는다. Unity 참조/콜백/구독은 보관하지 않는다.
- [x] 2. Begin은 표시/핸들/정지 소유권을 유지한다. 단일 pending·목록 변경 금지, 다른 서비스/중복/소비/소멸 ticket 거부가 상태를 훼손하지 않는다.
- [x] 3. Commit에서만 표시 해제·보관 확정/교체한다. preserve=false/빈 캡처는 오래된 보관을 폐기한다. 이동 실패 Rollback은 기존 표시/입력/정지/보관 상태를 유지하고 pending을 정리한다.
- [x] 4. 다른 씬에서 Restore는 None이며 원래 보관을 유지한다. 원래 씬의 feature/session 불일치는 ContextMismatch와 폐기다. 같은 레벨의 새 게임도 이전 상태를 복원하지 않는다.
- [x] 5. 준비된 빈 Host와 같은 문맥에서 내용/입력값/선택/탭/스크롤/포커스와 상대 순서를 새 뷰로 복원한다. 핸들은 새로 발급하고 옛 핸들은 새 뷰를 제거하지 않는다. 성공 보관 소비 후 재호출은 None이다.
- [x] 6. 데이터/현재 기능 연결을 모두 준비한 뒤 입력을 활성화한다. 복원 중 부분 입력과 구매/재시작/Next/보상/결과음 호출은 0이다. 현재 연결만 사용하고 포커스 누락/비활성은 유효 기본 선택으로 복귀한다.
- [x] 7. ApplyState/PrepareRestore의 두 번째 후보 예외·잘못된 Host·이미 열린 Host에서 Failed/Error/빈 Handles를 반환한다. 기존 목록은 보존하고 후보/구독을 정리하며 상태는 명시 재시도 또는 Discard 전까지 유지한다.
- [x] 8. 실제 SceneManager 두 씬 왕복에서 이전 뷰/Host/구독 정리와 새 참조·문맥을 확인한다. 소멸 후 늦은 콜백은 다른 문맥을 조작하지 않고 외부 입력 차단/정지와 Time.timeScale을 보존한다.
- [x] 9. 마지막 변경 후 두 복원 검사와 관련 1단계 검사 모두 PASS/FAIL0/실제 exit0이다. 네 화면 크기/비영점 안전 영역의 복원 후 포커스/raycast/새 캡처를 확인한다. 가상 입력과 미검증 물리 Android를 구분한다.
- [x] 10. 독립 리뷰 관련 수정, 조건별 증거·사용 안내, 원본/GUID/HEAD/diff 감사, 소유 프로세스/메모리 씬/임시 자원 정리를 완료한다. 빌드·커밋·푸시 없이 이 단계만 완료하며 3단계를 시작하지 않는다.

## 증거와 제외

[검증 기록](../../Verification/project-wide/popup-framework-stage-02.md)에 조건별 검사명/UTC/실제 exit/캡처/보존 감사를 연결했다. 마지막 복원59+씬26+1단계72=157 PASS이며 8회 모두 FAIL0/exit0이다. 독립 리뷰의 I1/I2와 수신자 증거 M1을 처리했다. `Logs/PopupFramework/Stage02/final-gate-results.txt`와 review-resolution.md가 최종 감사 근거다.

기존 게임 팝업 전환/관리 도구는 3단계다. 디스크/앱 재실행/게임 진행 저장·게임 세션 영속화·새 Addressables 정책/패키지/asmdef/DI/풀링·사용자 Unity 종료/씬 자동 저장은 제외한다. 1단계와 기존 사용자 변경/레벨/규칙/이미지/에셋/GUID를 보존한다.

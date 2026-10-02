# 팝업 프레임워크 2단계 검증 기록

상태: 2단계 구현·검증 완료 · 최종 검사 8회/157 PASS · 리뷰 처리와 조건별 보존 감사 완료.

[계획](../../Planning/project-wide/popup-framework-stage-02-plan.md) · [목표](../../Goals/project-wide/popup-framework-stage-02-goal.md) · [사용 안내](../../Guides/project-wide/popup-framework-usage.md)

## 적용 내용과 경계

서비스에 씬별 값 보관과 Begin/Commit/Rollback/Restore/Discard를 추가했다. Begin은 복원 가능한 항목만 깊게 복사하고 기존 표시를 유지한다. Commit에서만 보관/해제를 확정한다. 다른 씬에서는 원래 데이터를 유지하며 표시하지 않고, 같은 씬의 feature/session 불일치에서는 해당 보관을 폐기한다.

Restore는 비활성 후보의 값·새 핸들·현재 연결을 준비한 후 순서/입력을 적용한다. 성공은 보관 소비, 실패는 후보/연결 정리와 명시 재시도다. 값 포커스 키와 유효 기본 선택 복귀를 지원한다. 준비 실패한 비활성 후보도 명시 ReleaseRestore로 해제한다.

캡처 중 Host 종료는 캡처를 중단하고 그 Host의 뷰/연결/요청을 정리한다. 일반 Capture/Copy 예외는 기존 표시/보관을 유지한다. ticket.IsPending은 소비/Host 종료 후 false이며, 사용 예제는 Begin부터 finally까지 이번 이동 입력 차단을 보호한다. Commit 뒤 언로드 실패는 Rollback하지 않는다.

서비스 보유와 이동 성공 판단/게임 문맥/전역 입력 차단은 호출 기능 책임이다. 실제 게임의 세 팝업 전환, 관리 창/템플릿 도구, 게임 진행 저장은 이번 단계에 구현하지 않았다. 3단계 문서가 있어도 이번 목표에서 시작하지 않는다.

## 마지막 실행

Unity 6000.3.10f1의 소유한 숨김 Editor를 `Logs/PopupFramework/Stage02/run-suite.ps1`로 순차 실행했다. 플레이어/Addressables 빌드는 호출하지 않았다. 각 결과/summary/log 접두사는 `Logs/PopupFramework/` 기준이다. 아래 결과를 모두 읽고 소스 변경 이후의 신선도와 실제 exit를 게이트로 확인한다.

| 검사 | 접두사 | PASS / FAIL / 실제 exit | UTC 종료 시각 (2026-10-02) |
| --- | --- | --- | --- |
| RunRestorationData | stage02-data-current | 59 / 0 / 0 | 15:48:53.3706550Z |
| RunRestorationScene | stage02-scene-current | 26 / 0 / 0 | 15:49:14.4904889Z |
| 1단계 Data | stage02-stage01-data | 16 / 0 / 0 | 15:49:29.8749175Z |
| 1단계 RunInputScene | stage02-stage01-input | 49 / 0 / 0 | 15:49:51.2507873Z |
| RunReviewNavigation | stage02-stage01-navigation | 3 / 0 / 0 | 15:50:10.9562177Z |
| RunReviewFocus | stage02-stage01-focus | 2 / 0 / 0 | 15:50:31.1847406Z |
| RunReviewDynamic | stage02-stage01-dynamic | 1 / 0 / 0 | 15:50:51.6888239Z |
| RunReviewRequests | stage02-stage01-requests | 1 / 0 / 0 | 15:51:11.2980251Z |

합계는 복원85+1단계72=157 PASS다. 날짜는 실제 결과의 UTC 기록을 사용한다. missing additive destination의 native 오류와 EXPECTED ArgumentNullException은 실패 경로 재현이며 성공한 씬 로드로 해석하지 않는다. 이 의도된 오류 이외 컴파일/예외/실패 로그를 최종 게이트에서 검사했다.

## 완료 조건별 감사

| 조건 | 소스·검사와 관찰 결과 |
| --- | --- |
| 1 값/필터/순서 | SnapshotStore.Item은 ID/원본 핸들 값/State/포커스 키만 보관. Begin과 Restore에서 Copy. A/B/C의 B 제외 후 A/C, multi 순서, 원본 int[] 변경 뒤 값1 유지. 값 전용 깊은 Copy 계약은 확장 구현 책임 |
| 2 pending/표시 | Begin 표시·핸들·순서 유지, 중복 Begin/목록 변경/foreign/소비 token 거부. Host 종료 pending 무효. 캡처 중 Detach와 Play SetActive(false)에서 count/구독/요청0, 재연결 새 문맥 Begin 가능 |
| 3 확정/취소 | Commit 표시/Host 정리. preserve=false Commit/빈 캡처는 오래된 보관 폐기, false Rollback은 이전 보관 유지. 실제 missing destination 오류 뒤 원래 Top/포커스 유지. 캡처/Copy/이동/Commit/언로드 실패 예제의 finally 차단 해제 |
| 4 문맥 | 다른 실제 씬 None·원래 보관 유지, 원래 씬 명시 복원. session2/다른 feature ContextMismatch 후 None. 정확한 Discard만 해당 보관 폐기 |
| 5 새 뷰/UI | 실제 SceneManager CreateScene/SetActiveScene/UnloadSceneAsync 왕복·새 Host/EventSystem. 실제 InputField/Dropdown 두 개/ScrollRect와 내용/순서/포커스 복원. 옛 핸들 Close=false, 성공 후 빈 Host 재호출 None |
| 6 현재 연결/입력 | 비활성 staging+CanvasGroup 입력false. Prepare 실제 Submit0/부분 목록0/명령0. 현재 수신자 Submit1·이전0, 현재 이벤트 두 수신·이전0. Play 실제 가상 Enter 현재1·이전0. 아래 Submit0, 누락/비활성 포커스 기본 복귀 |
| 7 실패/재시도 | 두 번째 Apply/Prepare 각각 Failed/Error/빈 Handles/count0/views0/구독0. busy는 기존 목록 보존, null/잘못된 문맥 실패. 원인 제거 후 명시 재시도 성공, 정확한 Discard 뒤 None |
| 8 종료/소유권 | 첫 Host 언로드 후 null. 수명 토큰 연결된 지연 UniTask 취소·늦은 행동0. 외부 요청 OR·Time.timeScale 유지. 종료 후 Pulse 수신0·구독0·Count0·시험 prefab/catalog null·소유 A/B 씬 unload |
| 9 최종 검사/화면 | 위 8회 모두 FAIL0/실제 exit0, 소스 이후 결과. 네 PNG 직접 열기·실제 픽셀 크기/모서리 bounds/포커스/raycast 검사. 물리 Android 미검증 |
| 10 리뷰/감사 | 독립 리뷰 한 번과 I1/I2/M1 수정·처리 기록. 사용 안내·계획/목표/색인 갱신. 원본1813 중 허용수정3·나머지1810, 신규6 GUID·HEAD/diff/소유 프로세스 감사는 최종 게이트/manifest로 확정 |

명령/구매/Retry/Next/보상/결과음의 복원 중 재실행 없음은 **공통 뷰의 시험 명령 수신자**로 검증했다. 실제 게임 명령/구매/보상/오디오를 이번 단계에 연결했다고 주장하지 않는다.

## 화면 증거

최종 PNG 네 장을 직접 열었다. 시험 씬의 소유한 단색 Camera가 이전 edge 잔상을 제거했다. 안전 영역 `(24,36,width-48,height-72)`에서 실제 RectTransform 모서리와 최상위 버튼 raycast·포커스를 검사하고 PNG 크기를 디코드했다.

- [1280×720](../../../Logs/PopupFramework/stage02-1280x720.png)
- [450×800](../../../Logs/PopupFramework/stage02-450x800.png)
- [450×975](../../../Logs/PopupFramework/stage02-450x975.png)
- [600×800](../../../Logs/PopupFramework/stage02-600x800.png)

시험 UI는 InputField/버튼/선택/스크롤 fixture이며 게임 팝업 디자인 목업이 아니다. 해상도 검증용 캡처는 게임 이미지의 2의 승수 규격과 구분한다.

## 리뷰와 실패 이력

[원본 리뷰](../../../Logs/PopupFramework/Stage02/final-review.md) · [처리 기록](../../../Logs/PopupFramework/Stage02/review-resolution.md)

- I1 캡처 중 Host 종료: RED count1/구독1/요청true·exit1 → 캡처 소유 정리 GREEN count0/구독0/요청false. Play도 예외 로그0.
- I2 이동 예제: Begin을 try 안으로, 차단 해제를 finally로 이동. pending만 취소하고 복귀 Restore는 별도 시점이다. 실제 서비스 API를 사용하는 호출 예제 fixture로 Capture/Copy/준비/Commit/확정 후 실패를 검증했다.
- M1 연결 문자열만으로 수신자 증거가 부족해 필수 증거로 재평가했다. 이전/현재 실제 수신 RED exit1→GREEN exit0, 최종 Enter/Submit/Pulse로 재확인했다.
- 초기 ticket/Restore NotImplemented RED, inactive 후보 구독7 RED→ReleaseRestore, 준비 중 Host 비활성 예외1 RED→복원 소유 정리, 소비 ticket Host 참조 RED→Consume 해제도 보존했다.
- fixture 정리 Check가 프레임 끝 Destroy 전에 실행된 실패도 보존했다. 전용 PID104628/명령행 확인 후 종료, exit-1/missing-results는 통과로 계산하지 않았다. `cleanup-frame-red.log`가 근거다. NextFrame 확인과 정리 예외 결과 기록 뒤 최종 Scene은 뷰/에셋/씬0·exit0이다.

독립 리뷰를 반복하지 않았고 관련 결함을 한 연속 수정 작업으로 처리했다. 이월 minor는 없다. 임의 Copy/ReleaseRestore 계약 위반·subscriber 예외를 자동 수리하는 기능은 추가하지 않았다.

## 원본과 종료 감사

착수 HEAD `c3c4ef083ab29dc57f890f3dd3d8e7efb4768ef7`. 기준 `Logs/PopupFramework/Stage02/baseline-files.json`, `baseline-status.txt`. 기존 사용자 문서 변경을 보존했다.

원본 코드 수정은 PopupService.cs·PopupView.cs·Editor Tests/PopupFrameworkVerification.Input.cs 세 개다. 신규 Runtime 네 파일/Tests 두 파일과 메타 외에 게임/UI/씬/프리팹/레벨/이미지/패키지/Build Settings를 변경하지 않았다. `Logs/PopupFramework/Stage02/final-gate-results.txt`, `final-files.json`, `final-processes.json`으로 최종 증거를 연결한다.

빌드·콘텐츠 빌드·커밋·푸시·사용자 Unity 종료/씬 저장은 실행하지 않았다. checkout과 ledger/logs는 커밋 금지 상태에서 증거를 유지하기 위해 보존한다. 가상 Keyboard와 실제 Editor EventSystem만 검사했다. 물리 Android/복수 물리 장치/플레이어·플랫폼 빌드와 게임 진행 영속화는 미검증 또는 범위 밖이다.

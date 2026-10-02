# 팝업 프레임워크 1단계 완료·검증

작성일: 2026-10-02. 상태: 1단계 구현·검증 완료. 2단계 복원과 3단계 도구/기존 게임 연결은 미실행.

[계획](../../Planning/project-wide/popup-framework-stage-01-plan.md) · [목표](../../Goals/project-wide/popup-framework-stage-01-goal.md) · [사용 안내](../../Guides/project-wide/popup-framework-usage.md)

## 최종 실행

마지막 소스 변경 후 `Logs/PopupFramework/run-final-suite.ps1`에서 아래 8개 Unity 진입점을 순차 실행했다. 소유한 숨김 프로세스만 사용했으며 각 실제 종료 코드 0, 전체 runner 종료 코드 0이다. 결과/summary/Editor log는 같은 이름의 `Logs/PopupFramework/<이름>-results.txt`, `<이름>-summary.txt`, `<이름>.log`에 있다. 총 97 PASS / 0 FAIL.

| 이름 | 진입점 | PASS | 실제 exit |
| --- | --- | --- | --- |
| review-navigation-current | RunReviewNavigation | 3 | 0 |
| review-focus-current | RunReviewFocus | 2 | 0 |
| review-dynamic-current | RunReviewDynamic | 1 | 0 |
| review-requests-current | RunReviewRequests | 1 | 0 |
| data-current | Data | 16 | 0 |
| input-current | RunInputScene | 49 | 0 |
| existing-layout | GameScreen.Editor.PuzzleUILayoutVerification.Run | 18 | 0 |
| existing-contract | GameScreen.Editor.PuzzleUIStateVerification.Run | 7 | 0 |

Popup 진입점의 공통 타입은 `PopupUI.Editor.PopupFrameworkVerification`이다. Data UTC 2026-10-02T13:22:08.6110514Z, Input UTC 2026-10-02T13:22:31.6448643Z. 개별 리뷰의 UTC는 각 결과 파일 마지막 줄에 기록했다. 현재 로그에서 컴파일 오류/FAIL/Exception/실행 중단은 발견되지 않았다. 이는 플레이어 빌드 통과 주장이 아니다.

## 완료 조건별 근거

| 조건 | 실제 검사와 감사 | 판정 |
| --- | --- | --- |
| 1 핸들·중간 제거 | Data의 A/B/C·B만 제거·닫힌 B 재닫기 불변. long 핸들은 서비스 전체 static 단조 증가로 재발급하지 않음. Close는 일치한 핸들만 제거 | 충족 |
| 2 단일/복수 | Data의 동일 A 핸들/맨 위/내용 갱신, multi의 서로 다른 핸들 | 충족 |
| 3 등록·실패 복구 | Data의 미지/중복 ID·누락 프리팹 거부, 변경 후 throw하는 ApplyState의 기존 값/순서 복구, 신규 후보 정리 | 충족 |
| 4 최상위 입력 | Input의 아래 Submit0/최상위1, 네 크기 top raycast/배경 차단, 실제 가상 Mouse/Touch/Enter. Navigation의 같은 업데이트 Move+Submit/Cancel 외부0, 내부 Automatic 탐색/Enter1 | 충족 |
| 5 한 입력/콜백 | 같은 프레임 Cancel 재전달 차단, Escape 길게 눌러 B만 닫기, 닫기 금지 유지, 실제 터치 콜백에서 자신/다른 팝업 제거와 신규 열기 | 충족 |
| 6 포커스·화면 | 기본/이전 선택 복귀, 비활성 remembered/enabled=false 기본 복귀, Refresh 없이 동적 버튼 Escape 닫기. 아래 네 화면/비영점 safe area/capture | 충족 |
| 7 차단·정지 | Input 시험 연결의 외부 OR 보존·기존 제스처/선택 취소·정지 중간 제거·마지막 요청 해제·Time.timeScale 불변. Requests의 CloseAll/Detach 뒤 block/pause false/stale0, 같은 요청 상태에서 신규 C 열기는 pause true 유지 | 충족 |
| 8 정리 | 비활성/파괴/반복 Detach/재Attach 뷰0·Count0, Detach Changed 콜백 재열기 거부. 외부 delegate 명시 해제 뒤 각 이벤트 backing field null 및 service.host null. finally에서 소유 가상 장치/Canvas/시험 catalog/프리팹 정리 | 충족 |
| 9 리뷰·회귀·보존 | 독립 리뷰 1회와 4개 결함의 RED/최종 GREEN, 영향 검사 25 PASS. final-gate의 원본 해시·GUID·8개 fresh 결과·링크·fence·diff·HEAD·조건9 검사 | 충족 |

외부 구독은 연결 소유자가 해제한다. 프레임워크가 외부 구독을 임의 삭제했다는 의미가 아니다. 임시 자원 정리는 검사 finally와 각 정리 assertion, 소유 Unity 프로세스의 실제 종료로 확인했다.

## 화면 확인

`Logs/PopupFramework/input-1280x720.png`, `input-450x800.png`, `input-450x975.png`, `input-600x800.png`를 최종 Input 실행에서 새로 생성했다. PNG 디코딩/실제 크기와 비영점 안전 영역(좌24/아래36, 양쪽 합48/상하 합72), 최상위 버튼 raycast/배경 차단을 검사하고 네 이미지를 직접 열어 확인했다. 중앙 최상위 버튼과 어두운 하위 화면을 확인했다. 시험용 직사각형 UI이며 게임 팝업 디자인/실기기 품질을 인증하는 이미지는 아니다. 일반 배경 버튼은 화면 비율에 따라 일부 밖에 있어도 전체 blocker가 입력을 차단함을 raycast로 확인했다.

## 리뷰 수정과 실패 기록

독립 원본은 `Logs/PopupFramework/final-review.md`, 작성자의 처리 기록은 `review-resolution.md`이다. 추가 리뷰는 수행하지 않았다.

- navigation-red: 같은 업데이트 외부 Cancel1 → 최상위 내부 탐색/Move 소비 → current 3 PASS.
- focus-red: 비활성 기억 선택/현재 선택 복귀 실패 → 공통 유효성 검사 → current 2 PASS.
- dynamic-binding-red: 바인딩 준비를 보정한 검사에서 기존 분기를 복구해 Gate 없음/Count2 재현 → 실제 선택 Gate 준비 → current 1 PASS.
- requests-red: 콜백 후 오래된 pause true. nested-red: 동일 상태 중첩 열기에서 pause 알림 누락. multicast-red: 후속 구독자 staleBlock1 → 변경 때만 revision 증가/상태 선반영/각 구독자마다 revision 검사 → current 1 PASS, 세 모드 stale0.

리뷰의 Minor 증거 부족은 완료 조건의 검증 누락으로 재평가해 실제 터치 복합 콜백/정지 중간 제거/구독·참조 검사를 보강했다. 남긴 Minor는 없다.

초기 Data RED 1 PASS/1 FAIL, Input RED 0 PASS/1 FAIL, Detach RED 37 PASS/1 FAIL. 오래된 PNG를 읽던 lifecycle-green과 혼합 합성 Mouse/Touch의 lifecycle-green-2는 실패 기록이며 통과 증거가 아니다. 소유 PNG 삭제/새 캡처 대기와 장치별 독립 검사로 보정했다. 동적 버튼이 정상 파괴된 뒤 TRACE 조회로 실패하던 검사는 closed/alive 기록으로 수정했다. 각 실패 원본은 Logs에 보존한다.

## 보존·제한

기준 HEAD `680d4bc026f98127117b5c22573db8c449d13931`, work 브랜치. 원본 Assets/Packages/ProjectSettings 1780파일의 SHA256이 모두 착수 기준과 일치한다. 신규 Runtime 8개/Editor 검사 4개와 .meta, 공통 신규 프리팹 2개만 추가했다. 기존 게임 코드/규칙/씬/이미지/레벨/기존 프리팹/패키지/GUID와 Stage13은 보존했다. 문서 분류 인덱스에 새 문서 링크를 추가했다. 최종 신규 파일 해시는 `Logs/PopupFramework/final-files.json`, 최종 감사는 `final-gate-results.txt`다.

씬 복원/영속성은 구현하지 않았고 기존 결과/일시정지/설명 팝업은 아직 공통 서비스로 전환하지 않았다. 게임용 catalog 등록·관리 창·템플릿 생성 도구는 3단계다. 시험 전용 Assets 진입점은 지정 신규 프리팹만 생성했으며 사용자 씬을 저장하지 않았다.

가상 Keyboard/Mouse/Touchscreen은 실제 Editor EventSystem 경로만 입증한다. 물리 Android Back/터치와 동시에 쓰는 물리 장치는 미검증이다. 빌드·Addressables 콘텐츠 빌드·커밋·푸시·사용자 Editor 강제 종료·자동 씬 저장·새 패키지/asmdef/DI·풀링은 수행하지 않았다. 최종 소유 검사 프로세스는 모두 종료했으며 다른 프로젝트의 사용자 Unity와 Asset Import Worker는 건드리지 않았다.

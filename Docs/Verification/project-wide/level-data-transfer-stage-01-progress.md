# 제작 데이터 전달 1단계 진행·검증 기록

2026-10-17 · 1단계 완료

## 최종 Windows 확인

- 사용자가 네이티브 저장 창에서 ZIP을 저장했다. `native-20261017-000334-123/destination.txt`와 실제 파일 존재를 확인했다. 이 실행은 이후 열기 취소로 실패 종료했으므로 전체 성공으로 계산하지 않는다.
- 가져오기만 재개할 수 있도록 검사 진입점 `LevelAuthoring.Editor.ProjectTransferWorkflowVerification.RunWindowsImport`를 추가했다. 제품 코드나 ZIP 형식을 변경하지 않는다.
- 사용자가 같은 ZIP을 직접 선택한 후 `Logs/LevelDataTransferStage01/native-20261017-010440-004/result.txt`에 **PASS Windows native open, ZIP matches current source, original preserved / FAIL 0** 기록을 확인했다.
- 실행 로그 `Logs/TestHarness/20261017-010417-045-Run.log`, 프로세스 exit 0, 테스트 연결 해제 확인. 저장과 가져오기는 두 번의 실행에 나누어 확인했으며 최종 콘텐츠 해시가 현재 원본과 일치한다.
- 아래 자동화 실패 기록은 이력으로 보존한다. Windows 왕복 미검증은 해소됐고 Android 실기기·클라우드 동기화·제품 재빌드 확인은 범위 밖 미검증으로 유지한다.
- 제품 빌드·커밋·푸시·HTML 매뉴얼 갱신 없음. 사용자 제작 원본과 로그 보존. 다음 단계는 Unity ZIP 후보 검사·변경 비교·정식 JSON 원본 채택이다.

## 구현 범위

- ZIP 내보내기 전 프로젝트 ID·레벨 수·콘텐츠 버전과 포함 범위 안내.
- 날짜·시간이 포함된 ZIP 기본 파일명, Windows 실제 저장 경로 및 클라우드 동기화 구분 안내.
- 저장 후 선택기를 기다리는 동안 및 ZIP 작성 중 원본 변경 감지.
- 저장·파일 선택·쓰기·flush·close·취소 오류에서 성공 안내를 분리.
- 기존 ZIP 스키마 및 Windows/Android 파일 선택 계약 유지.

## 결정 기록

- Ruling: 사용자의 금지에 따라 제품 빌드·커밋·푸시는 실행하지 않는다. 소스 UI는 Unity Play Mode, 네이티브 파일 선택은 Unity 실행 검사로 확인한다. 기존 실행 파일에는 새 UI가 반영되지 않는 한계가 있다.
- Ruling: 기존 제작 데이터 변경은 이 단계에 포함하지 않는다. 현재 1~15레벨을 읽어 격리 폴더에 복제하고 사용자 로그는 삭제하지 않는다.
- Ruling: Android 제공자가 실제 경로를 주지 않으므로 가상 로컬 경로를 만들지 않고 선택한 문서로 안내한다. 실제 동기화 완료는 확인하지 않는다.
- Ruling: 검사 전용 과거 개인 백업 의존을 제거하고 기존 ZIP 구조 fixture를 독립 생성한다. 과거 배포 ZIP 실물 전체와의 호환을 검사한 것으로 확대하지 않는다.

## 검사 근거

- RED: `Logs/TestHarness/20261016-233035-699-Run.log` — 내보내기 메타데이터 미리보기 부재 재현.
- ZIP 왕복 최종: `Logs/LevelDataTransferStage01/20261016-235330-602/result.txt` — 11 PASS/FAIL 0. 독립 생성한 기존 구조 fixture, 한글 경로, 15레벨 및 원본 보존 확인. 실행 로그: `Logs/TestHarness/20261016-235256-256-Run.log` (exit 0).
- 1차 UI 회귀: `Logs/TestHarness/20261016-233502-202-Run.log` — 통과.
- RED: `Logs/TestHarness/20261016-233741-924-Run.log` — 파일 선택 중 외부 변경이 성공 처리되는 문제 재현.
- 보완 UI 회귀: `Logs/TestHarness/20261016-233924-241-Run.log` — 전체 통과. 추가된 출력 열기 실패·원본 변경 감지 포함.
- 독립 코드 검토: 출력 열기 실패 안내, 선택기 대기 중 원본 변경 감지, 개인 백업 fixture 의존을 지적받아 보완했다.

## Windows 검사 과정 이력

기존 Windows 실행 파일에서 검사용 사본을 열고 ZIP 내보내기를 누르면 선택기 호출에서 StackOverflowException이 발생했다. OpenFileName의 StringBuilder 마샬링을 소유권이 명확한 네이티브 LPWSTR 버퍼로 변경한 뒤 저장 대화상자가 표시됐다. 스레드 스택 크기 확대는 효과가 없어 반영하지 않았다.

`20261016-234711-981-Run.log` 검사는 저장 조작을 완료하지 못하고 시간 초과 취소됐다. 검사 코드가 취소 스레드에서 EditorApplication.Exit을 호출하지 않도록 메인 스레드 전환을 추가했다. 이 실패 실행은 통과로 계산하지 않는다. 실제 저장·열기 왕복 완료 전까지 1단계 전체 완료로 표시하지 않는다.

## 당시 미검증 및 후속 확인 이력

- 추가 시도 `20261016-235709-871-Run.log`: 현재 창을 새로 조회하고 스크린샷 좌표 입력 및 명시적 활성화를 시도했지만 `failed to activate captured window`가 재시도 후에도 발생했다. 접근성 인덱스에만 국한된 문제가 아니므로 추가 자동 재실행으로 완료를 주장하지 않는다. 검사 전용 Unity는 종료하고 사용자 확인을 기다린다.

- Windows 실제 저장·열기: `20261016-235428-132-Run.log`에서 저장 창 표시까지 확인. Computer Use의 `element 83 is not available in cached app state for Unity.exe` 오류가 재조회 후에도 반복되어 검사 전용 Unity만 종료했다. 통과로 계산하지 않는다. 기존 제품 실행 파일은 새 소스가 반영되지 않았으며 초기 오류로 응답 없음 상태가 된 창을 임의 종료하지 않았다.
- 후속 확인: 수정 소스가 로드된 Windows 환경에서 ZIP 저장 → 같은 ZIP 열기 → 해시 일치 확인. 기존 실행 파일로 수정 완료를 검증할 수 없다. 제품 재빌드는 별도 승인 범위다.

- Android 실기기 SAF와 외부 클라우드 제공자 동기화.
- 제품 빌드 후 실행 파일의 새 UI 반영 (제품 빌드 금지에 따라 수행하지 않음).

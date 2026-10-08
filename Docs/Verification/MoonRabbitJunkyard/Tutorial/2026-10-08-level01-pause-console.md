# 레벨1 튜토리얼 후 일시정지 콘솔 오류 점검

날짜: 2026-10-08
요청: 레벨1 튜토리얼 진행 후 일시정지 팝업을 누르면 콘솔 오류 발생.

## 확인과 수정

- 사용자 에디터 로그: ElementVisualFrameDto → effectAnimations → frames의 재귀 타입을 Unity JSON/씬 직렬화가 순회하며 깊이10 초과 오류가 발생했다.
- 에디터 실행 요청의 이미지 설정 전달을 기존 DTO의 MemoryPack + Base64로 변경했다. 레벨 파일/출시 팩 포맷은 변경하지 않았다.
- 씬/프리팹/호출부에서 사용하지 않는 PuzzleGameSession.visualConfiguration DTO 직렬화 필드를 제거했다. ConfigureVisuals 주입 또는 레벨의 시각 카탈로그 로딩 경로를 유지한다.
- 이미지 미준비 예외는 사용자 로그에서 Play 종료·씬 복원 근처 HUD LateUpdate에 나타났다. 일시정지/계속하기만으로 같은 이미지 예외가 자연 재현되지는 않았다.
- 설치된 Addressables 소스가 ExitingPlayMode에 리소스를 정리함을 확인했다. 이때 세션의 ready가 남아 있는 경계 검사는 실패했다. 세션이 해당 종료 이벤트에서 준비 상태를 해제하고, 파기 시 구독을 해제하도록 수정했다.
- 기존 조합형 실제 게임 검사는 Exception뿐 아니라 Error/Assert도 수집한다. Unity 네이티브 직렬화 진단은 콜백으로 잡히지 않아 신규 검사에서 원본 로그도 읽는다.

## 재현 검사

명령: Tools/Testing/ProjectTests.ps1 -Action Run -Method PuzzlePauseConsoleVerification.Run

- 실제 Level_01을 읽어 Asset/MemoryPack 두 방식으로 실행 요청. 원본 변경 없음.
- 다음 → 지정 교환/3매칭 → 마지막 다음 → 일시정지 버튼 → HUD 이미지 확인 → 계속하기 → 일시정지 상태에서 Play 종료.
- 종료 경계의 마지막 HUD 프레임과 세션 준비 해제를 별도 검사한다.
- RED: 20261008-172529-838-Run.log. 종료 시 ready 유지 및 원본 로그의 직렬화 오류 확인.
- GREEN: 20261008-172737-540-Run.log. 두 입력 경로 통과, managed Error/Exception/Assert 및 원본 로그의 깊이 초과 오류 없음.
- 새 검사 작성 중 문자열/enum 컴파일 오류와 로그 파일 공유 모드 오류는 검사 코드에서 수정했다. 중단된 검사용 프로세스만 종료했고 사용자 에디터에는 영향을 주지 않았다.

## 범위

- 사용자와 동일한 일시정지 경로 및 추가 종료 경계를 Windows Unity에서 검사했다. 미준비 이미지 예외의 자연 발생 타이밍 전체를 동일하게 재현했다고 주장하지 않는다.
- HTML·레벨 데이터·씬·프리팹은 수정하지 않는다. 빌드·커밋·푸시하지 않는다.

## 최종 검증

- 오류 재현/회귀: 25 PASS, 0 FAIL. managed 오류 기록은 빈 파일이며 원본 로그에 두 대상 오류 문자열 없음.
- 기존 조합형 제작/실행: 20261008-172909-660-Run.log, 45+32+24 = 101 PASS.
- 기존 게임 세션 통합: 20261008-173019-134-Run.log, 215 PASS.
- 테스트 분리 후 game-only-compile.log, exit0. 컴파일 오류/직렬화 깊이/미준비 이미지 오류 없음.
- baseline.json의 레벨·씬·프리팹·HTML 파일97개 모두 동일. 테스트 폴더와 연결 기록 제거 완료. git diff --check 통과.
- 기존 작업을 보존했고 플레이어 빌드·커밋·푸시는 실행하지 않았다.

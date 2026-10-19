# 레벨툴 리팩토링 1단계 검증 기록

## 범위

`LevelToolScreen`에서 보드 포인터·휠·롱터치·핀치·영역 시작 상태와 줌 보정 예약을 `LevelToolBoardInput`으로 분리했다. 선택 도구 설정은 화면에, 문서·선택·Undo는 기존 세션에 유지한다. 2단계 JSON 편집 경계 정리는 구현하지 않았다.

## 변경 계약

- `Attach`: root와 보드 스크롤 영역에 이벤트를 한 번만 등록한다.
- `Cancel`: 문서를 변경하지 않고 후보 셀·영역 시작·미리보기를 취소한다. 아직 눌린 포인터는 Up이 편집을 확정하지 않도록 추적한다.
- `Reset`: 취소에 더해 포인터 캡처와 줌 보정 예약을 해제한다. 앱 중단·포커스 손실에 사용한다.
- `Dispose`: 모든 입력·geometry 콜백을 해제하고 Reset한다. 화면 비활성화에서 호출하며 재호출은 무시한다.
- `LevelToolScreen`은 Context 조회와 편집 명령·재그리기 콜백만 제공한다. 입력 객체에는 화면 전체, JSON 문서, 저장소를 전달하지 않는다.
- 마우스 셀 선택도 입력 객체로 이동했다. 연결 꼭짓점의 자체 조작 경로는 유지했다.

## 변경 전 근거

| 검사 | 결과 | Unity 로그 |
|---|---|---|
| Touch Run | 통과 | `Logs/TestHarness/20261019-162303-526-Run.log` |
| Viewport Run | 통과 | `Logs/TestHarness/20261019-162411-960-Run.log` |
| 신규 InputLifecycle | 의도한 실패: Dispose 후 geometry 콜백이 minWidth를 변경 | `Logs/TestHarness/20261019-162556-743-Run.log` |

## 수정 후 검사

실행 형식: `& Tools/Testing/ProjectTests.ps1 -Action Run -Method LevelAuthoring.Editor.<아래 메서드>`.

| 메서드 | 확인 내용 | 로그 |
|---|---|---|
| LevelToolTouchVerification.Run | 탭·드래그 취소·롱터치·두 손가락·이동·영역·마우스·교체·Undo·저장 재로드 | `Logs/TestHarness/20261019-163309-405-Run.log` |
| LevelToolTouchVerification.RunInterruptedTouch | 앱 중단·포커스·캡처 손실 후 새 입력·잔여 포인터 | `Logs/TestHarness/20261019-163407-729-Run.log` |
| LevelToolTouchVerification.RunFinePinch | 작은 핀치 변화 누적과 피벗 좌표 보존 | `Logs/TestHarness/20261019-163444-972-Run.log` |
| LevelToolAdvancedTouchVerification.RunVertex | 모바일 연결 꼭짓점 선택 | `Logs/TestHarness/20261019-163520-266-Run.log` |
| LevelToolInputVerification.Run | 지연 입력 확정·키보드 명령·복구 수명주기 | `Logs/TestHarness/20261019-163606-681-Run.log` |
| LevelToolShortcutVerification.Run | 명령 메뉴·단축키·실행 가능 상태 | `Logs/TestHarness/20261019-163634-617-Run.log` |
| LevelToolViewportVerification.Run | 창 크기별 보드 공간·Ctrl+휠·화면 맞춤·집중 모드·스크롤 | `Logs/TestHarness/20261019-163659-180-Run.log` |
| LevelToolTouchVerification.RunInputLifecycle | 포인터 캡처·예약 해제, Dispose 반복, geometry 해제, 재활성화·Attach 반복, 모달 취소, 실제 레벨 전환 | `Logs/TestHarness/20261019-163842-596-Run.log` |

**최종 결과: 위 8종 모두 exit 0, 테스트 연결 해제 완료.** 수명주기 검사는 기존 해제 누락을 실패 재현한 뒤 수정 후 통과했다. 4개 화면 크기(1366×768, 1920×1080, 1280×800, 1024×768)의 뷰포트 검사를 통과했다.

1280×800 렌더 캡처를 확인했다. 보드와 패널 영역은 분리되어 있으나, 배치 검사 캡처에는 일부 스킨/입력 영역이 흰색으로 출력되는 기존 제한이 있어 실제 제품의 시각 품질 전체가 검증됐다고 판단하지 않는다. 이 단계에서 스타일·에셋은 변경하지 않았다.

## 검사 중 발견 및 조치

- 재활성화 검사에서 모바일 viewport 재설정 전 터치를 보내 첫 검사가 실패했다. 화면 입력은 정상이며 실제 렌더 좌표 기록으로 확인했다. 재설정과 레이아웃 프레임을 기다리는 검사로 수정했다.
- Undo 뒤 Refresh 직후 같은 프레임에 다음 터치를 보내면 새 셀의 크기가 아직 NaN이었다. 다음 레이아웃 이후 조작하도록 검사 단계를 분리했다. 런타임에 임의 지연을 추가하지 않았다.
- 기존 내부 상태를 읽는 Touch/AdvancedTouch 검사는 이동된 상태를 입력 객체에서 조회하도록 조정했다. 행동 검증 조건은 유지했다.

## 검토·제약

- 자체 검토: 콜백 등록/해제 짝, 예약 해제, 상태 중복, 입력 차단, 기존 편집 명령·Undo 연결을 확인했다. 독립 리뷰어 검토는 수행하지 않았다.
- 기존 work 브랜치를 유지했다. 새 worktree·커밋은 만들지 않았다.
- 테스트 데이터는 Logs 하위 격리 프로젝트를 사용한다. 사용자 후보 원본·선택 설정·레벨 리소스는 수정 대상이 아니다.
- 자동 검사는 Unity Editor Play Mode의 실제 UI Toolkit 이벤트와 렌더 레이아웃을 사용한다. 물리 Android 터치·OS 뒤로가기·실제 Windows 실행 파일의 수동 조작 검사를 대신하지 않는다.
- 제품 빌드, HTML 매뉴얼 갱신, 커밋·푸시, 실제 사용자 이력 삭제를 수행하지 않았다.

## 완료와 다음 단계

1단계 구현·자동 검증 완료. [2단계 계획](../../Planning/MoonRabbitJunkyard/LevelToolRefactoring/2026-10-19-stage02-plan.md)과 [2단계 목표·실행문](../../Goals/MoonRabbitJunkyard/LevelToolRefactoring/2026-10-19-stage02-goal.md)을 작성했으며 2단계 구현은 시작하지 않았다.

커밋 메시지 제안:

```text
refactor: 레벨툴 보드 입력과 이벤트 수명주기 분리

- 터치·마우스·줌 입력을 전용 객체로 분리
- 화면 해제 시 콜백·포인터 캡처·줌 예약 정리
- 입력 회귀 검사 보강 및 2단계 계획 작성
```

# Android 레벨툴 2단계 — 구현·검증 기록

2026-10-11 · 구현·자동 검사 완료. 실제 Android 검증 대기이며 전체 목표는 미완료다. 아래 중간 실패·진행 기록은 최종 검사 결과와 함께 읽는다.

## 구현 방향

- 기존 Windows PanelSettings는 보존하고 Android 실행에서만 DPI 기준 160의 런타임 사본을 사용한다. 실제 dp와 IME는 기기 검수 대상이다.
- Safe Area와 배율을 적용한 사용 가능한 폭 940 UI 단위를 넓은 배치 기준으로 잡았다. 기존 205/290 패널과 약 400의 보드 공간을 확보하는 기준이다.
- 넓은 화면은 양쪽 패널, 좁은 세로는 단일 하단 패널, 좁은 가로는 단일 옆 패널을 사용한다. 레벨/소재/속성/더보기와 저장·Undo/Redo·시험을 같은 명령에 연결한다.
- 화면 변경은 기존 요소의 크기·표시만 바꾼다. 세션과 필드를 회전마다 재생성하지 않는다.
- 기본 모달을 모바일에서 스크롤 가능한 내용으로 감싸고 Safe Area/키보드 바깥 영역 안에 제한한다.
- Unity `TouchScreenKeyboard.area`는 Android에서 빈 Rect이므로 Android UI 스레드에서 API 30+ WindowInsets IME 여백을 읽는다. 이전 버전은 표시 프레임 차이를 사용한다. OS가 이미 Unity 화면을 줄인 부분은 다시 빼지 않는다.

참고: [Unity 키보드 영역 API](https://docs.unity3d.com/ja/6000.0/ScriptReference/TouchScreenKeyboard-area.html). Android 실제 여백·한국어 IME·떠 있는 키보드 동작은 아직 검증하지 않았다.

## 상태 소유와 변경 경계

| 상태 | 소유·보존 방식 |
|---|---|
| 문서·dirty·선택·Undo/Redo | 기존 AuthoringToolWorkspace/AuthoringEditSession을 유지한다. ExportState는 현재 문서·저장 기준·양쪽 이력을 함께 기록하므로 전환 전후 전체 상태를 비교한다. |
| 층·배치 도구·작업 페이지·열린 패널 | LevelToolScreen의 기존 필드와 mobilePanel을 유지한다. 회전은 기하 정보와 표시만 바꾸며 프로젝트를 다시 열지 않는다. |
| 필드 raw·확정 값·커서/선택·포커스 | 같은 VisualElement를 재사용한다. 패널 숨김의 지연 입력은 일시 캐시로 보존하고 복귀·명령 실행 시 기존 검증 경로로 돌려준다. 숫자 Blur 정규화 이후 raw 복원을 예약한다. |
| Safe Area·키보드·팝업 경계 | MobileViewport가 표시 좌표를 계산하고 모바일 배치·스크롤을 갱신한다. Android에서만 PanelSettings 런타임 사본을 사용하며 Windows 원본 설정을 변경하지 않는다. |

## 검증 진행

- 첫 실행 `Logs/TestHarness/20261011-070936-818-Run.log`: 새 모바일 배치의 `StyleLength(0)` 오버로드 충돌로 컴파일 실패. `0f`로 명시하고 소유 기록을 확인해 임시 연결을 정상 해제했다.
- 두 번째 실행 `20261011-071039-038-Run.log`: PC 스킨의 인라인 최소 너비가 모바일 전환 후 남아 360 폭에서 시험 버튼이 넘쳤다. 모바일 진입에서 최소 너비를 해제하고 짧은 버튼 문구를 적용했다.
- 검토에서 모바일 필드 크기가 스크롤바까지 확장되는 CSS 충돌을 발견해 스크롤바 전용 규칙을 추가했다. 패널 버튼은 지연 입력 포커스를 빼앗지 않도록 처리하고 실제 PointerDown/Up 전환 검사를 추가했다.
- 추가 검사 첫 컴파일에서 StyleFloat 비교 오류를 확인해 `.value` 접근으로 수정했다. 현재 재실행 로그: `20261011-071523-462-Run.log`.
- 테스트는 대표 폭/방향, 비대칭 Safe Area, 939/940 경계, 모의 키보드와 모달·입력·세션·선택·Undo/Redo·JSON 저장 왕복을 확인한다. 실제 기기 검사를 대체하지 않는다.

## 미검증과 제한

Android 실제 DPI·Safe Area·OS 키보드·한국어 IME, 실기기 가로/세로 회전은 미검증이다. APK/Player/Addressables 번들 빌드·커밋·푸시·HTML 매뉴얼 갱신은 하지 않았다. 터치 제스처와 외부 파일 전송은 3/4단계로 남긴다. Windows·제품/저장 회귀, Android 컴파일과 추가 UI 배율 검사는 통과했다. 3단계 계획·목표·복사용 실행문은 작성 완료했으며, 3단계 구현은 시작하지 않았다. 빌드 금지 조건 아래에서는 최신 구현의 실기기 실행 근거를 확보하지 못하므로 전체 목표를 완료 처리하지 않는다.

## 후속 검사 및 수정

- 20261011-071631-884: SelectRange 직후의 커서 스냅샷이 다음 프레임 선택 범위와 달랐다. 검사에서 선택 적용 프레임 이후 기준값을 기록하도록 수정했다.
- 20261011-071727-075: 대표 7개 화면·패널·회전 팝업·모의 키보드 접근까지 통과했으나 실제 PointerDown/Up 패널 이동에서 지연 필드가 재생성되는 것을 발견했다. 버튼 focusable=false 외에 PointerDown 기본 포커스 이동을 차단하고 재검사 중이다.
- 모바일 팩 확인 영역의 좁은 줄바꿈을 화면 캡처에서 확인해 더보기 패널에 시험 입력/팩 상태 확인을 함께 배치했다. 모바일 정지 화면에서는 사용 가능한 영역이 달라질 때만 배치를 갱신하도록 조정했다.
- 현재 실행: 20261011-072036-652-Run.log (모바일 UI 전체 재검사). 최종 성공, Windows 회귀 및 Android 타깃 컴파일은 아직 확인 전이다.

## 입력 검사 원인 분리

- 미확정 입력 준비에 사용한 TextElement.text 대입이 필드의 문서 변경 콜백으로 전파되는 것을 073013-920 진단 로그에서 확인했다. 실패 일부는 실제 패널 이동 전에 이미 문서를 변경한 검사 준비 오류였다. SetValueWithoutNotify로 드래프트를 준비하고 이동 전 필드 동일성과 세션 불변 검사를 추가했다. 임시 런타임 진단 로그는 제거했다.
- 073113-506: 필드·raw·확정 값은 유지됐고 숨긴 필드의 UI 선택이 Blur에 의해 접혔다. 숨김 중에는 raw/값을 확인하고, 복귀 후 포커스·커서·선택 범위가 복원되는지를 확인하도록 검사를 정리했다.
- 073233-958 RunPendingInput: exit 0. 실제 PointerDown/Up 패널 이동·복귀, 드래프트와 확정 값 분리, 복귀 후 포커스/커서/선택, 회전/키보드, Undo/Redo, JSON 저장·재열기 통과. 전체 폭/모달 검사는 이 빠른 검사에 포함하지 않았다.
- 고급 화면 기본 접근 검사를 추가 준비 중이다. Windows 회귀·Android 타깃 컴파일 및 최종 전체 모바일 검사는 아직 미완료다.

## 숨긴 입력의 명령 실행 검사

- `073750-802 RunPendingCommands`: 숨긴 이름 초안의 실제 Save 버튼 입력, 문서·JSON 저장, 입력 캐시 해제까지 통과했다. 뒤이어 잘못된 숫자 문자열을 숨기는 검사에서 실패했다.
- `074147-434`: 실패 조건을 분리한 결과 패널 숨김·문서·dirty·Undo 상태는 보존됐다. 숫자 필드의 포커스 해제 이후 raw가 `-`에서 기존 값 `20`으로 정규화되는 것을 실제 UI 로그로 확인했다. 포인터 직후에는 `-`가 유지됐으나 레이아웃 안정화 후에 바뀌었다.
- 숨긴 필드의 Blur 처리 이후 초안 복원을 예약하고, 다시 표시할 때 해당 콜백을 해제하도록 보완했다. 현재 검증 로그는 `074239-627`이며 아직 최종 통과로 판정하지 않는다.
- `074239-627 RunPendingCommands`: exit 0. 실제 Save로 숨긴 이름 초안이 문서·JSON에 저장됐고, 잘못된 숫자는 원본 raw와 값·문서 전체 상태·저장 파일 바이트를 보존한 채 거절됐다. 숨긴 이름의 Undo/Redo와 후속 정상 입력·저장도 통과했다. 임시 테스트 연결은 해제됐다. 전체 모바일 화면·Windows·Android 컴파일 검사는 별도 진행한다.
- `074343-976 RunAdvancedScreens`: 흐름·튜토리얼·시험 화면의 실제 포인터 진입, 세로/가로 속성 스크롤, 패널 닫기와 보드 복귀, 세션/선택/이력 보존까지 통과했다. 기록 관리 Foldout이 기본으로 접혀 있는데 내부 버튼을 바로 검사하여 0 크기로 실패했다. 펼치기 포인터 조작을 추가하고 재검사한다. 숨긴 입력 저장 완료 화면과 좁은 가로 흐름 화면 캡처를 직접 확인했다. 실제 기기 검증은 아니다.
- `074629-583 RunAdvancedScreens`: exit 0, 임시 연결 해제. 흐름·튜토리얼·시험·기록 화면의 기본 진입·속성 스크롤·세로/가로 전환·패널 닫기와 보드 복귀가 통과했다. 기록 관리의 실제 펼치기, 회전 후 펼침 상태 유지와 세션·선택·이력 보존도 확인했다. 기록 삭제나 봇 시험 실행은 이 검사 범위에 포함하지 않았다.
- `074834-058 LevelToolMobileVerification.Run`: exit 0, 임시 연결 해제. 최신 구현의 7개 화면 크기, 939/940 배치 경계, 비대칭 Safe Area, 네 패널, 회전 모달·모의 키보드에서 확인/취소 접근, 미확정 입력 포커스/커서/선택 복원, 세션·선택·편집 층·Undo/Redo 보존 및 JSON 저장·재열기가 통과했다. 실제 Android 검증을 대체하지 않는다.

## 최종 회귀 결과

아래 검사는 같은 작업 트리에서 순차 실행했고 모두 exit 0으로 종료했다. 로그는 `Logs/TestHarness/20261011-<시각>-Run.log`에 있다.

| 검사 진입점 | 로그 시각 | 검증 범위 |
|---|---|---|
| LevelAuthoring.Editor.LevelToolViewportVerification.Run | 075135-956 | 기존 Windows 화면 4개 크기·팝업·도움말·메뉴·보드 집중 |
| LevelAuthoring.Editor.LevelToolInputVerification.Run | 075241-674 | 실제 Play Mode 지연 입력·저장·숫자 검증·포커스·명령·게임 왕복 |
| LevelAuthoring.Editor.LevelToolShortcutVerification.Run | 075310-541 | 공통 메뉴·단축키·모달·활성 상태 |
| LevelAuthoring.Editor.LevelToolWindowVerification.Run | 075336-567 | 16:10 크기 정책·최소 크기·복원 계산·Editor 창 격리 |
| Products.Editor.LevelToolWindowProfileVerification.Run | 075402-136 | Windows 도구 창 설정·다른 제품/전역 설정 보존 |
| Products.Editor.ProductBoundaryVerification.Run | 075427-713 | 다섯 제품·도구/게임 콘텐츠 경계 |
| Products.Editor.ProductSelectionVerification.Run | 075512-641 | 대상 플랫폼별 제품 선택·콘텐츠 경로·선택 복원 |
| LevelAuthoring.Editor.LevelToolProjectAccessVerification.Run | 075538-516 | 내부 프로젝트·이름/경로 검증·JSON 저장/재열기 |
| LevelAuthoring.Editor.AndroidLevelToolScreenVerification.Run | 075604-316 | 기본 프로젝트 진입·복구/인계·편집·저장·시험 요청 |
| Products.Editor.AndroidLevelToolCompilationVerification.Run | 075630-166 | PRODUCT_LEVEL_EDITOR Android 스크립트 38개 어셈블리, Editor 검사 제외·설정 보존 |

마지막 검사 종료 후 `Assets/__ProjectTests`와 `Logs/TestHarness/connection.json`이 없는 것을 확인했다. 실제 OS의 Windows 창 전환·Android 키보드와 dp는 위 검사로 입증하지 않는다.

대표 모바일 캡처의 360×780 속성, 975×800 더보기, 낮은 가로 키보드 모달, 1280×800 양쪽 패널, 숨긴 입력 저장 완료, 흐름·기록 화면의 좁은 가로 스크롤을 직접 확인했다. 원본은 `Logs/AndroidLevelToolStage02/`에 있다. 추가 읽기 전용 검토에서는 Blur 이후 복원의 재진입·콜백 해제와 캐시 수명을 확인했고 중대한 잔여 문제는 발견하지 못했다.

## 다음 단계 문서

- [3단계 계획](../../Planning/project-wide/android-level-tool-stage-03-plan.md)
- [3단계 목표](../../Goals/project-wide/android-level-tool-stage-03-goal.md)
- [3단계 복사용 실행문](../../Commands/project-wide/android-level-tool-stage-03-command.md)

실제 Android 확인은 최신 실행 파일과 기기가 있어야 한다. 별도 빌드 승인 없이 APK를 만들거나 Editor 모의 검사를 실기기 통과로 바꾸지 않는다.

## 완료 감사의 추가 검사

전체 모바일 행렬의 ConstantPixelSize 배율 1 검사에 더해 배율 1.5/2에서 실제 UI 좌표, Safe Area·모의 키보드, 논리 폭의 배치 경계와 입력/세션 보존을 별도 검사했다. 실제 Android DPI나 물리 dp 검증을 대신하는 검사는 아니다. 런타임 소스가 바뀌지 않았으므로 통과한 관련 회귀를 반복하지 않고 새 검사 결과를 추가했다.

- `080111-158 RunScaledViewports`: exit 0. 배율 2의 1920×1200·1900×1200·824×1830과 배율 1.5의 1440×900에서 실제 패널 논리 크기·Safe Area·배치 경계를 확인했다. 버튼은 48 UI 단위와 렌더링 픽셀 96/72 기준을 검사했으며 실제 dp로 판정하지 않았다. 배율 변경 중 모달의 같은 필드·입력·포커스·커서·선택과 세션이 보존됐다.
- `080350-591 RunScaledViewports`: exit 0. 위 네 사례의 배율 변경·키보드 모달에서 확인·취소를 각각 스크롤해 Safe Area와 실제 스크롤 뷰포트 안에 표시되는지 추가 검사했다. 취소는 실제 PointerDown/Up으로 실행했고 문서·이력·선택을 보존했다. 임시 테스트 연결은 해제됐다.
- 배율 변경 모달과 휴대폰의 취소 버튼 표시 캡처를 직접 확인했다.

## 실제 Android 검증 대기

2026-10-11의 읽기 전용 `adb devices -l` 결과는 연결 기기가 없는 빈 목록이었다. 확인 과정에서 시작된 ADB 서버는 종료해 원래 상태로 돌렸다. 제품에 설정된 `Builds/Android/LevelTool.apk`도 존재하지 않는다. APK 생성은 사용자 지시로 금지되어 있다.

실제 기기에서 다음을 확인해야 두 미완료 목표 조건을 판정할 수 있다.

1. 휴대폰·태블릿의 가로/세로에서 실제 노치·시스템 영역과 주요 버튼의 터치 크기를 확인한다.
2. 레벨 이름·숫자를 입력하면서 패널 전환·회전·키보드 표시/숨김을 반복하고 raw·포커스·커서/선택을 확인한다.
3. 한국어 조합 중 입력이 사라지지 않는지, 조합 완료 후 저장되는지 확인한다.
4. 작은 키보드 화면에서 새 프로젝트·사본 저장·도움말의 입력과 확인/취소에 스크롤로 접근한다.
5. 편집→회전→Undo/Redo→내부 저장→재열기 결과와 세션을 확인한다.

이 검수에는 테스트 실행 파일과 연결된 기기가 필요하다. 현재 허용 범위의 구현·자동 검사·문서는 마무리했고 전체 목표는 실기기 검증을 기다린다. 실제 전체 작업 완료 전에는 최종 커밋 메시지를 제공하지 않는다.

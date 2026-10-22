# 네이티브 레벨툴 UX 1단계 최종 보고서

상태: 2026-10-22 구현 및 Windows Editor 범위 검증 완료. Android 화면·키보드·뒤로가기는 시뮬레이션 검증이며 실기기 검증이 아니다.

## 변경 결과

- 시작 화면에 새 프로젝트, 작업 폴더 열기, ZIP 가져오기, 최근 프로젝트를 배치했다. 편집 중에는 프로젝트 메뉴에서 접근한다.
- 프로젝트 이름을 먼저 입력하고 Windows는 저장 폴더를 선택한다. Android는 기존 내부 프로젝트 저장 경로를 사용한다. 프로젝트 생성 시 레벨을 자동 생성하지 않는다.
- 빈 프로젝트에 ‘첫 레벨 만들기’를 안내하고 보드·시험 명령은 차단한다. 모바일의 불필요한 편집 하단 메뉴도 숨긴다.
- 레벨 번호·이름과 기본 9×9/현재 레벨 복제를 확인한 뒤 보드 편집으로 진입한다. 복제는 보드·미션·이동 수·튜토리얼·공급을 보존한다.
- 기존 메타데이터 목록·편집 버튼·검색·프로젝트별 최근 편집·세 버튼 전환을 재사용한다. Windows 최근 프로젝트 경로는 재실행 후 유지하며 Android 기존 내부 이름 기록과 분리한다.
- 새 레벨과 변경은 메모리에 유지하고 명시적 저장 때 파일에 반영한다. 목록 표시를 위해 전체 JSON을 읽지 않는다.

## 구현 판단과 검토 수정

- Ruling: 빈 프로젝트에서도 선택 레벨 ID가 null인 편집 세션을 유지한다. 가짜 레벨 없이 첫 추가·undo/redo·복구를 재사용하기 위한 결정이다. 이후 새 보드 명령도 선택 레벨 없는 상태를 차단해야 하며, 누락하면 null 접근 오류가 생길 수 있다.
- 최종 독립 코드 검토의 두 P2를 수정했다. 첫 레벨 undo→redo 시 소재 목록 캐시가 비워지지 않던 문제, 이전 시험/기록 탭 때문에 새 보드가 숨겨지던 문제다. 두 현상을 검사에서 재현한 뒤 같은 검사에서 해소 확인했다. 미수정 리뷰 항목은 없다.
- 기존 테스트의 ‘프로젝트 생성 즉시 레벨 1개’ 가정은 명시적 `LevelToolTestProject` fixture로 옮겼다. 제품 동작을 과거 가정에 맞추지 않았다.

## 검증 결과

[최종 검사 발췌](NativeLevelToolStage01/final-checks.txt). 제품 빌드 없이 Unity 검사 runner가 종료 코드 0으로 끝났고 각 실행 후 테스트 연결을 해제했다.

| 검사 | 결과와 원본 로그 |
|---|---|
| 빈 프로젝트·첫 추가·저장·재열기·복구·복제·실패/취소 보존·플랫폼별 최근 기록 | portable `native-start` 24개 통과. `Logs/NativeStartChecks/portable-final.txt` |
| ZIP 데이터 왕복·실패·취소·사용자 스트림 소유권·기존 초안 보존 | portable `stage4-package` 54개 통과. `Logs/NativeStartChecks/package-final.txt` |
| 신규 UI와 컴파일 | `LevelToolPlayVerification.RunNativeStartUx`, `20261022-171334-917-Run.log` exit 0. 이름/번호 오류, 첫 생성·명시적 저장·undo/redo·복제, 최근 빈 목록/누락 경로, Tab·Esc·키보드 submit, 모바일 뒤로가기 포함 |
| 1만 목록 지연 로딩 | `LevelBrowserLazyDisplayVerification.Run`, `20261022-170032-081-Run.log` exit 0. 본문 파일 1만 개가 없는 목록도 표시하고 실제 선택한 1개만 읽음. 선택 변경·검색·화면 캐시 검증 |
| 편집 버튼·최근 편집·미저장 전환 | `LevelBrowserUiVerification.Run`, `20261022-170137-470-Run.log` exit 0. 행 선택만으로 열리지 않음, 현재 레벨만 버리기, 계속 편집과 미확정 입력 보존, 저장 후 전환 |
| ZIP 등록 후 프로젝트 전환 취소 | `LevelToolTransferRecoveryVerification.RunImportTransitionCancel`, `20261022-170610-146-Run.log` exit 0 |
| 다른 레벨·공유 변경을 보존하는 현재 레벨 버리기 | `LevelScopedDiscardVerification.Run`, `20261022-171123-974-Run.log` exit 0 |
| 기존 공통 메뉴·단축키 활성화 | `LevelToolShortcutVerification.Run`, `20261022-171149-384-Run.log` exit 0 |

최종 수정 소스·관련 검사 범위의 `git diff --check` 통과. 줄 끝 자동 변환 안내는 있었으며 전체 기존 작업 트리까지 깨끗하다고 주장하지 않는다.

## 화면 증거

UI Toolkit 실제 씬을 RenderTexture로 캡처했다. 최종 캡처는 ConstantPixelSize, scale=1로 논리 크기를 명확히 했으며 Android 화면은 기존 모바일 레이아웃·키보드 가용 영역 처리를 호출해 모의했다.

- [시작 화면](NativeLevelToolStage01/start.png), [빈 프로젝트](NativeLevelToolStage01/empty.png), [첫 9×9 보드](NativeLevelToolStage01/first-level.png)
- 새 레벨 팝업: [1280×800](NativeLevelToolStage01/new-level-1280x800.png), [1440×900](NativeLevelToolStage01/new-level-1440x900.png), [390×844](NativeLevelToolStage01/new-level-390x844.png), [844×390](NativeLevelToolStage01/new-level-844x390.png), [820×1180](NativeLevelToolStage01/new-level-820x1180.png)
- 모바일 빈 프로젝트: [세로](NativeLevelToolStage01/empty-390x844.png), [가로](NativeLevelToolStage01/empty-844x390.png), [태블릿](NativeLevelToolStage01/empty-820x1180.png)
- [작은 가로 화면·키보드 표시 후 확인/취소 접근](NativeLevelToolStage01/keyboard-844x390.png)

초기 캡처의 흰 필드는 검사 RenderTexture에 스텐실 버퍼가 없어 발생했다. 일반 Editor에서도 같은 캡처 설정으로 재현했으며 depth 24로 바꾼 동일 검사에서 정상화됐다. 제품 UI를 임시 색상으로 덮어 문제를 숨기지 않았다.

## 데이터·임시 파일 보존 경계

검사는 GUID 기반 격리 프로젝트와 recoveryDirectory를 사용한다. 정식 제작 원본 경로를 검사 대상으로 덮어쓰지 않았고 원본 scene/prefab 저장도 수행하지 않았다. portable은 finally에서 임시 사본을 제거하며 native-start runner도 자기 실행의 격리 저장소를 종료 때 제거한다. 이전 native-start/일반 Editor 재현 검사에서 남은 정확한 8개 저장소도 경로·링크·증거 파일 유무를 확인한 뒤 제거했다.

최종 보고서·캡처·결과 발췌는 Docs에, 상세 검사 로그는 Logs에 보존한다. 실제 제작 원본 전체의 실행 전후 해시 감사를 새로 수행한 것은 아니므로 그 수준의 불변성 인증으로 해석하지 않는다. 기존 검사들은 원본 대신 사본을 쓰며 ZIP/실패 회귀에서 저장본·초안 보존을 확인했다.

## 미검증과 다음 범위

- Android 실기기 키보드/SAF/실제 터치, 제품 실행 파일, 기기 밀도와 운영체제 창 조작은 검증하지 않았다. 제품 빌드·설치가 금지된 이번 범위와 구분한다.
- 키보드 검사는 UI Toolkit의 키 이벤트/submit 경로이며 실제 운영체제 키보드 자동 조작이 아니다.
- 기존 보드 편집 상단·소재·패널 전체 개편은 아직 하지 않았다. 휴대폰의 기존 편집 화면 배치는 2단계 개선 대상이다.
- 이번 관련 회귀를 통과한 것이 프로젝트 전체 검사나 기존 모든 레벨툴 검사 통과를 뜻하지 않는다.
- 커밋·푸시, 제품 빌드·설치, HTML 매뉴얼 갱신은 하지 않았다. 2단계 구현도 시작하지 않았다.

[1단계 목표](../../Goals/MoonRabbitJunkyard/LevelToolNativeUx/2026-10-22-stage01-goal.md) · [2단계 계획](../../Planning/MoonRabbitJunkyard/LevelToolNativeUx/2026-10-22-stage02-plan.md) · [2단계 목표](../../Goals/MoonRabbitJunkyard/LevelToolNativeUx/2026-10-22-stage02-goal.md) · [2단계 실행문](../../Commands/MoonRabbitJunkyard/LevelToolNativeUx/2026-10-22-stage02-command.md)

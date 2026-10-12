# Android 레벨툴 4단계 — 진행 기록

2026-10-11 · **실기기·빌드를 제외한 요청 범위의 구현·자동 검증 완료.** 실제 플랫폼 검수는 아래의 미검증 목록으로 남긴다.

## 범위와 제약

- 기존 `work` 작업본과 1~3단계 변경을 유지한다.
- 실기기 검사·Player/APK/Addressables 빌드·커밋·푸시·HTML 매뉴얼 갱신을 제외한다. 가능한 스크립트 컴파일은 실행 파일 빌드와 구분한다.
- 공통 ZIP/최근 기록과 플랫폼 어댑터를 하위 에이전트에 분담한다. 화면 연결·복구 수명주기·Unity 순차 실행은 주 에이전트가 담당한다.
- 계획의 실기기 완료 게이트는 이번 사용자 요청의 범위 밖이다. 구현·자동 검사 결과와 실제 플랫폼 미검증을 분리한다.

## 작업·인터페이스 확인

| 작업 | 생산/소비 경계 | 판단 |
|---|---|---|
| 1 ZIP | 저장본의 공개 JSON → ProjectPackageStore → 검증된 새 내부 폴더 | 기존 ContentSnapshotStore 전체 검증 재사용; Workspace 직접 변경 없음 |
| 2 선택기 | ILevelToolFileTransfer → 호출자 소유 Stream → 작업 1 | 단일 요청·취소/실패 분리; URI는 플랫폼 안에 유지 |
| 3 복구 | Workspace 상태 + 기존 ToolDraftStore + RecentProjectStore → 화면 | current/saved/Undo 유지; 최근 목록은 내부 이름만 저장 |
| 4 검증 | 위 세 경계 + 실제 UI 메뉴·수명주기 | Unity/프로필 변경 순차; 실기기·네이티브 실제 실행 미검증 |
| 1/2 | Import 등록 완료 후 dirty 전환 | 전환 실패 시 기존 세션과 등록된 새 프로젝트 모두 보존 |
| 2/3 | 선택기 pause/focus·Back | 요청 취소 우선; 선택기 중단을 앱 종료로 처리하지 않음 |
| 3/4 | 입력 초안·프로젝트 전환·역순 체크포인트 | 자동 검사는 메모리와 파일의 의미를 비교해야 함 |

## 결정 기록

- Ruling: 새 worktree를 만들지 않고 기존 `work`를 사용한다 — 진행 중 1~3단계 작업본을 그대로 통합하기 위해 — 관련 없는 변경을 되돌리지 않는다.
- Ruling: Windows는 기존 폴더 열기/저장을 유지하고 ZIP 가져오기만 내부 사본으로 등록한다 — 기존 사용 흐름을 보존하기 위해 — Windows 최근 메뉴는 내부 사본 기록과 일반 경로를 혼동하지 않는다.
- Ruling: 신규 외부 의존성 없이 Windows native picker와 Android SAF 결과 수신용 작은 로컬 Java bridge를 사용한다 — 현재 재사용 가능한 선택기가 없음 — 실제 native 실행은 별도 검증으로 남긴다.
- Ruling: 최근 목록 손상은 이전 정상본과 경고를 제공하고 둘 다 손상되면 명확히 실패한다 — 프로젝트 JSON/초안은 자동 덮어쓰지 않는다.

## 초기 진행 기록

1. ZIP·최근 기록의 실패 검사를 먼저 작성 중이며, 구현 전 RED를 확인한다.
2. 플랫폼 결과 수신 경계와 단일 요청 수명주기를 준비 중이다.
3. 화면 연결과 중단 복구는 기존 초안·입력 확정·종료 계약을 조사 중이다.

## 재현·구현 기록 (중간 시점)

- 최근 기록 RED: `Logs/GameAuthoringStage02/stage04-recent-red.txt`, 기존 317개 경로 후 `recent internal project storage exists` 실패. 이후 최근 기록·역순 체크포인트·전환 실패·삭제된 폴더의 current/saved/Undo/Redo 검사는 통과했고 초안 경고/이전본 API 누락에서 실패했다 (`stage04-draft-red.txt`). 최근 목록과 초안 이전본 보호를 구현했다. 최종 GREEN은 아직 확인 전이다.
- ZIP RED: `stage04-package-red.txt`의 저장소 누락 실패. 첫 구현 후 손상 ZIP에서 `InvalidDataException`이 사용자용 형식 오류로 정규화되지 않는 것을 확인했고 수정했다 (`stage04-package-green.txt` 최초 실행은 실패 기록). 해당 실패를 숨기지 않고 재검사한다.
- 선택 요청 RED: `20261011-115006-853-Run.log`의 `LevelToolFileRequest.Start` 미구현 실패. 단일 요청·취소·중복/오래된 결과·스트림 소유권을 구현하고 mock GREEN을 검사 중이다.
- 중단 UI RED: `20261011-115349-520-Run.log`에서 `pause records uncommitted input separately` 실패. 입력 초안을 문서와 분리해 기록하고 pause/focus loss 즉시 체크포인트와 복원 경로를 추가했다. 실제 UI 재검사는 진행 중이다.
- Windows 시스템 선택기는 STA/Unicode comdlg32와 static AOT hook, Android는 현재 Activity에 붙는 작은 SAF Fragment로 구현했다. Java source importer는 Android 전용이며 activity/manifest/패키지를 변경하지 않았다. 실제 native 실행은 아직 검증하지 않았다.
- Android 요청 취소는 managed 결과를 취소하되 native 선택기가 돌아올 때까지 receiver를 유지한다. 해당 기간 새 native 요청은 거절하며 오래된 결과를 적용하지 않는다.

## 통합 결과와 검증 근거

- 최종 Portable: `Logs/GameAuthoringStage02/stage4-portable-final.txt`, **401개 통과, exit 0**. 기존 JSON·저장·편집·참조·경로/링크·Undo/Redo 검사와 ZIP·최근 목록·복구 검사를 포함한다.
- 실제 UI 통합: `Logs/TestHarness/20261011-121524-055-Run.log`, `LevelToolTransferRecoveryVerification.RunAll`, **13케이스 통과, exit 0**. 내보내기, 가져오기 성공/취소/dirty 전환 취소/읽기·close 실패, 내보내기 flush·close 실패/취소, Back, 종료 저장 실패, pause, 새 화면 복원을 확인했다.
- ZIP 게임 입력 비교: `20261011-121641-493-Run.log`, `ProjectPackageTrialVerification.Run`, **exit 0**. 대표 fixture 전체 레벨의 ID와 JSON 게임 정의·MemoryPack 입력 바이트를 왕복 전후 비교했다. Player/콘텐츠 빌드를 수행하지 않았다.
- 13케이스 통합 첫 실행은 완료 안내 검사에 실패했다 (`121205-672`). 상태를 추가 기록한 재실행과 직접 검증에서 null 문자열로 표현된 공유 튜토리얼 부재 값이 검증에 걸리는 원인을 확인했다 (`121338-251`, `121435-838`). null 값은 복원하지 않도록 수정한 후 전체 13케이스를 재검사했다. 실패 로그를 성공 근거로 사용하지 않는다.
- 손상된 view가 정상 이전 복구본을 덮는 문제는 `stage4-view-red.txt`로 재현했다. 저장소와 화면의 `ToolDraftStore.ValidateView`를 공통화한 뒤 `stage4-view-green.txt`의 38개 focused 검사를 통과했고 최종 401개에도 포함했다.
- 독립 검토에서 확인한 전송 worker 완료 전 잠금 해제, Android KeyDown Back 경로 누락, 이전 복구본 보호 기준 차이, 다른 레벨에 지연 입력 복원되는 문제를 수정했다. 최신 검토에서는 추가 조치할 실제 결함이 없었다.
- ZIP 제한 검증의 범위를 구분한다. 실제 128 MiB 입력 한도는 생성 스트림으로 실행했다. 개별 16 MiB·합계 512 MiB·엔트리 개수는 선언 크기 검사로 확인했고 실제 읽기·쓰기 카운터도 코드 검토했다. 위조된 개별 길이 ZIP은 해당 .NET 런타임에서 JSON 형식 검증으로 거절되므로 실제 초과 해제량을 실행 검증했다고 주장하지 않는다.
- 첫 회귀 묶음 실행의 PowerShell 후속 조건은 성공한 검사 후 남은 `$LASTEXITCODE`를 읽어 중단했다. 실제 ZIP 게임 입력 검사는 exit 0이었으며 남은 회귀는 검사 도구의 예외 계약에 따라 새 묶음으로 순차 실행한다. 후속 결과는 아래에 기록한다.

## 동작·제약

- 파일 메뉴에서 프로젝트 ZIP 가져오기/내보내기와 최근 내부 프로젝트를 사용한다. 내보내기는 내부 저장 후 정상 공개 JSON만 전송한다.
- 가져오기는 이름 확인→선택기→임시 검증→새 내부 폴더 등록→기존 dirty 선택으로 이어진다. 전송 실패·취소·close 오류에서 현재 세션을 바꾸지 않는다. 등록 이후 close 오류 또는 전환 취소 시 검증된 내부 사본이 남을 수 있으며 기존 프로젝트는 보존된다.
- 최근 목록은 내부 이름만 최대 10개 저장한다. Windows 일반 작업 폴더는 기존 폴더 열기를 유지한다.
- 복구는 current/saved·선택·Undo/Redo와 화면/미확정 입력 초안을 보관한다. 저장본 복귀는 실제 디스크 저장본을 먼저 검증한다. 손상·누락 시 복원/이전본/다른 프로젝트를 명시적으로 선택하며 원본을 자동 덮어쓰지 않는다.
- 전송 전 체크포인트를 남기고, 중단된 worker가 끝날 때까지 작업 잠금을 유지한다. Back은 파일 요청, 메뉴, 진행 중 조작, 패널을 먼저 닫은 후 저장/버리기/취소로 종료를 처리한다.
- 외부 제공자는 원자적 쓰기를 보장하지 않을 수 있어 실패 시 부분 ZIP이 남을 수 있다. 해당 실패 안내와 내부 저장 성공을 구분한다.
- OS가 pause/focus 콜백 없이 프로세스를 종료하면 마지막 체크포인트 이후 변경은 잃을 수 있다. 모든 강제 종료 변경 보존을 보장하지 않는다.

## 남은 실제 플랫폼 확인

Windows 실행 파일의 실제 대화상자, Java 소스의 Android 패키징, 실제 SAF 제공자·권한·URI IO, 터치 키보드/IME·OS Back, 실제 OS 프로세스 종료와 PC↔Android 왕복은 수행하지 않았다. Android 스크립트 컴파일은 Java/네이티브/실기기 검증을 대신하지 않는다. 사용자 요청으로 이번 작업에서 실기기와 빌드를 제외하며 출시 전 별도 확인 항목으로 보존한다.

## 최종 회귀와 정리

다음 검사들은 모두 Unity 도구의 **exit 0**과 테스트 연결 해제를 확인했다. 로그는 `Logs/TestHarness/` 아래다.

| 검사 | 로그 |
|---|---|
| 단일 선택 요청·중복/오래된 결과·취소·스트림 소유권 | `20261011-121737-881-Run.log` |
| 모바일 숨김/미확정 입력과 저장 등 공통 명령 | `20261011-121804-358-Run.log` |
| 모바일 배율·Safe Area·키보드·회전·팝업 상태 보존 | `20261011-121851-741-Run.log` |
| 터치 중단·capture 상실·다중 손가락 취소 | `20261011-122003-170-Run.log` |
| Windows 단축키·입력 문맥·명령 경계 | `20261011-122041-452-Run.log` |
| Windows 1280×800 등 여러 크기·보드/모달·편집 상태 | `20261011-122107-726-Run.log` |
| Android C# 컴파일, 제품/콘텐츠 선택 복원 | `20261011-122213-292-Run.log` |
| Java importer의 Android/레벨툴 심볼 경계 + Android C# 재컴파일(38 assemblies) | `20261011-122339-114-Run.log` |

Java importer를 Android 전용과 `PRODUCT_LEVEL_EDITOR` 제약으로 설정했다. 실제 Java 컴파일·패키징은 실행하지 않았으며 importer 설정과 C# 컴파일만 검증했다.

기존 `work`와 1~3단계 작업본을 유지했다. Unity 테스트 임시 연결과 프로필/콘텐츠 선택을 복원했고 씬·ProjectSettings·HTML을 새로 변경하지 않았다. 커밋·푸시·Player/APK/Addressables 빌드·SDK/플러그인 설치는 하지 않았다. 소스와 문서는 미커밋 작업본이다.

다음 단계의 [계획](../../Planning/project-wide/android-level-tool-stage-05-plan.md), [목표](../../Goals/project-wide/android-level-tool-stage-05-goal.md), [복사용 실행문](../../Commands/project-wide/android-level-tool-stage-05-command.md)을 준비했다. 5단계 구현은 시작하지 않았다.

# 제품 빌드 도구 2단계 — 구현·검증 기록

2026-10-16 · Unity 6000.3.10f1 / Windows / 브랜치 `work`

[통합 계획](../../Planning/project-wide/2026-10-15-build-tool-plan.md) · [2단계 계획](../../Planning/project-wide/build-tool-stage-02-plan.md) · [목표](../../Goals/project-wide/build-tool-stage-02-goal.md) · [3단계 계획](../../Planning/project-wide/build-tool-stage-03-plan.md)

## 구현 결과와 책임

`Match → Build → 빌드 도구`에서 선택한 정규 프로필의 빌드 요청·진행·결과를 관리한다. 실제 실행은 제품·타깃·고정 출력을 보여주는 확인 대화상자 이후 시작한다. 조회·선택·사전 검사는 기존 읽기 전용 계약을 유지한다.

| 구성 | 구현 책임 |
|---|---|
| `BuildToolRequest`, `BuildToolRunStore` | 실행 대상·지문·세션·상태·결과, `Library/MatchBuildTool`의 요청/이력 JSON·로그 |
| `BuildToolRunner` | 준비→선택→전환/컴파일 대기→직전 검사→호출→정리·결과. 중복·충돌·완료 증거 없는 요청 차단 |
| `BuildToolController` | Editor update 진행, SessionState 세션 식별, 도메인 재로드 이후 재구성 |
| `UnityProfileBuildBackend` | 최신 정규 프로필 검사, 기존 제품 선택 경로, 프로필 기반 BuildPlayer, 실제 BuildReport 변환, 소유 임시 상태 비교 후 복원 |
| `MatchBuildToolWindow.Execution` | 확인·준비 취소·미완료 정리, 제품/경로/시간/Player 결과/오류/로그·부분 출력 표시 |
| 기존 플랫폼 등록표 | Android/Windows 파일 출력과 iOS 디렉터리 출력, Android 고정 APK 제약. 공통 실행기에 플랫폼 switch 추가 없음 |
| `BuildToolContentPreparation` | 동일 소유 Player 요청에서 이미 생성한 MemoryPack의 중복 생성 방지 |

플랫폼 전환 전 요청을 기록하고 완료 후 실제 활성 프로필·타깃·콘텐츠·출력을 대조한다. 파일과 메모리 프로필/PlayerSettings를 함께 지문화하여 저장하지 않은 변경도 감지한다. 복제 프로필은 실행하지 않는다.

빌드가 이미 시작됐으나 결과가 없으면 재호출하지 않고 `NeedsAttention`으로 남긴다. 에디터 재시작도 자동 실행하지 않는다. 명시적 ‘미완료 요청 정리’는 빌드 재호출 없이 소유 임시 상태만 정리한다. Player 완료 증거와 작업 전체 상태를 따로 보여준다.

## 콘텐츠 연결 근거

- 기존 `LevelToolAtlasBuild`(-1100), `LevelToolManualBuild`(-1000), `LevelPackBuild`(-950), 설치된 Addressables 4.1.0 `AddressablesPlayerBuildProcessor`(+1)는 모두 BuildPlayerProcessor다. 패키지 소스를 직접 확인했다.
- 실제 백엔드는 기존 콘텐츠 빌더를 검사하고 요청 동안 Addressables 자동 빌드를 켠다. 별도로 `BuildPlayerContent`를 호출하지 않는다.
- Unity 6000.3의 [공식 BuildPipeline 소스](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/BuildPipeline.bindings.cs)에서 프로필 기반 API와 네이티브 PreparePlayerBuild 연결을 추가 확인했다. 실제 콜백 실행을 확인했다는 뜻은 아니다.
- LevelPackBuild가 생성 완료를 소유 요청에 표시하면 기존 LevelPackAddressablesBuilder의 두 번째 Generate만 생략한다. 독립 콘텐츠 빌드는 기존 생성 경로를 유지한다.
- 기존 ProductBuildGuard·전체 감사·매뉴얼/아틀라스 처리는 그대로 둔다. 원본 제작 데이터 제외 계약을 우회하지 않는다.
- 원본 자동 빌드 설정은 성공·실패·취소·예외 이후 비교 후 복원한다. 정상 선택한 제품은 활성 상태로 유지한다. 외부 변경은 덮어쓰지 않고 충돌로 남긴다.

위 내용은 코드·설치된 패키지 연결 및 모의 검증의 근거다. 실제 Unity Player 콜백 실행과 산출물 검증은 수행하지 않았다.

## 검증 이력

모든 명령은 `Tools/Testing/ProjectTests.ps1 -Action Run -Method <아래 메서드>`다. 실제 백엔드의 Build 메서드는 호출하지 않았다.

| 메서드·로그 | 결과와 범위 |
|---|---|
| `BuildToolExecutionVerification.Run` / `20261016-013704-407-Run.log` | 저장 전 메모리 프로필 변경을 지문이 감지하지 못하는 실패 확인 |
| 동일 / `20261016-013854-393-Run.log` | 파일+메모리 지문 보완 후 기존 모의 검사 exit 0 |
| 동일 / `20261016-014424-562-Run.log` | 제한 실행 환경에서 라이선스 초기화 대기. 이 작업 소유 검사 프로세스를 종료하고 연결 해제. 기능 검사 결과로 사용하지 않음 |
| 동일 / `20261016-014605-194-Run.log` | 정상 경로 중 Windows 기록 파일 교체 실패로 exit 1. 정상 결과를 실패로 덮는 기존 처리 확인 |
| 동일 / `20261016-014746-631-Run.log` | 기록 오류 시 정리·잠금, null 결과·호출 예외의 결과 미확인 처리 등 통과, exit 0 |
| `BuildToolExecutionReloadVerification.Run` / `20261016-014832-606-Run.log` | 실제 스크립트 재로드 2회, 모의 요청 이어가기·재호출 차단·세션/라우팅/파일 보존 통과, exit 0 |
| `BuildToolExecutionVerification.Run` / `20261016-014940-311-Run.log` | Windows 일시적 파일 교체 오류로 정리 후 결과 기록이 확인 필요 상태가 됨. exit 1, 완료 근거에서 제외 |
| 동일 / `20261016-015048-461-Run.log` | 일시 공유 잠금 직접 재현·해제 복구 포함 최종 모의 실행 검사 통과, exit 0 |
| `BuildToolPreflightVerification.Run` / `20261016-015126-284-Run.log` | 기존 사전 검사·플랫폼 등록·원본 설정 보존 통과, exit 0 |
| `BuildToolWindowVerification.Run` / `20261016-015215-842-Run.log` | 메뉴·확인 안내·선택·창 재생성·오류/빈 목록·스크롤·실제 재로드·원본 설정 보존 통과, exit 0 |
| `BuildToolExecutionReloadVerification.Run` / `20261016-015337-648-Run.log` | 최종 저장 처리로 실제 스크립트 재로드 2회·모의 요청 최대 1회 호출·세션/라우팅/파일 보존 재확인, exit 0 |

로그 경로는 `Logs/TestHarness/` 아래다. 최종 네 메서드 모두 exit 0이며 각각 종료 후 테스트 연결을 해제했다.

### 모의 실행·소유권 검사 범위

- 요청 저장 선행, 중복 Start 거절, busy/다른 플랫폼 준비 대기, 정상 결과별 API 1회, 성공·명시적 실패·취소.
- 빌드 예외/null/잘못된 결과는 정리 후 결과 확인 필요. 실제 결과를 추측하거나 새 요청으로 자동 재시도하지 않는다.
- 선택 중 부분 실패, 컴파일 실패, 삭제 GUID, 동일 GUID 설정 변경, 안전한 대기 취소, 정리 실패와 명시적 정리.
- 이력/로그 부분 저장 실패 후에도 잠금 유지·재구성 자동 실행 차단. 빌드·선택·취소 중 로그 실패에도 정리 수행.
- 손상 기록, 디렉터리가 아닌 저장 위치, Windows의 짧은 공유 잠금 복구.
- 실제 backend의 복원 판정 함수를 분리된 JSON·Action으로 검사: 원상태면 무변경, 예상 소유 값이면 복원, 외부 값은 보존+오류, 쓰기 실패는 별도 기록. 실제 Unity 원본 설정을 변경한 복원 검사는 아니다.
- 같은 세션 재구성·실제 스크립트 재로드, 다른 세션 재시작 식별, 호출 중단 기록, 완료 결과가 있는 정리만 재개, 모의→실제 방식 전환 차단.
- 실제 backend의 복제 프로필 거절·저장 전 프로필 변경 지문 검사. 실제 Build는 호출하지 않음.

### 검토와 수정

읽기 전용 독립 검토에서 Important 3건을 확인했다. 요청 시작의 부분 저장 실패 후 숨은 요청 재개, 로그 실패가 정리 자체를 막는 문제, 완료 증거가 없는 반환을 확정 실패로 처리하는 문제다. 요청을 메모리에도 먼저 소유하고 오류 시 확인 필요 상태를 저장하며, 기록 실패와 별개로 정리를 시도하도록 수정했다. null/비정상 결과·호출 예외는 소유 상태 정리 후에도 확인 필요 상태로 유지한다. 각각의 모의 실패 주입 검사가 최종 실행 검사에 포함된다.

추가로 실제 검사에서 발견한 Windows 일시적 공유 잠금은 오류 코드 32/33/1175만 최대 3회, 총 150ms 범위로 재시도한다. 영구 오류·경로 오류를 무제한 재시도하거나 성공으로 숨기지 않는다.

## 요구사항 감사와 한계

| 요구 | 완료 근거 / 한계 |
|---|---|
| 명시 요청·최신 프로필·고정 출력·공통/플랫폼 분리 | UI 확인 경계, backend Validate/Fingerprint, 플랫폼 등록표, 모의 실행 검사 |
| 재로드·재시작·중복 호출 차단 | 재구성 및 실제 스크립트 재로드 검사. 에디터 재시작은 서로 다른 세션 값으로 모의 검사 |
| 실패·취소·부분 적용·복원 충돌 | 실패 주입 및 실제 복원 판정 함수 검사. 실제 프로필 전환/원본 쓰기 경로는 미실행 |
| 결과·시간·로그·출력 UI | 모의 결과 기록, UI 소스와 창 검사. 출력 버튼은 존재 경로만 열며 부분 출력도 명시 |
| 기존 콘텐츠·Guard 연결 | 기존 콜백과 설치 패키지 소스 확인·Unity 컴파일. 실제 콘텐츠/Player 실행은 금지 범위로 미검증 |
| 조회 무변경 | 기존 사전 검사·창 검사와 새 실행/재로드 검사의 상태/파일 스냅샷 |
| 다음 단계 인계 | 3단계 계획·목표·복사용 실행문. 실제 옵션/SDK는 자동 구현하지 않음 |

원본 보존 검사는 기존 스냅샷과 동일하다: 프로필·Addressables 파일 전체, 전역 ProjectSettings/EditorBuildSettings 파일, 활성 프로필·플랫폼·출력·콘텐츠 라우팅과 프로필 메모리 상태. 전역 PlayerSettings의 모든 메모리 필드를 비교했다고 주장하지 않는다.

실제 Player/APK/AAB/iOS·Addressables 빌드, 실제 플랫폼 전환 뒤 컴파일, 기기 설치, SDK/서명/Steam·스토어 동작은 미검증이다. 빌드 승인 대화상자의 실제 시작 경로도 실행하지 않았다. 테스트는 전체 게임 검사 실행이 아닌 위 메서드의 관련 검사다.

이번 작업에서 커밋·푸시·배포·SDK 설치·HTML 매뉴얼 갱신·씬/프리팹 재작성은 하지 않았다. 기존 미커밋 변경을 보존한다.

## 완료 판정

2단계는 실제 백엔드 연결 구현과 허용된 모의/컴파일/재로드/조회 회귀 검증 범위에서 완료했다. Player·Addressables 실제 산출물 검증은 별도 승인이 필요한 미수행 항목으로 유지한다. 기본 실제 요청 기록 `Library/MatchBuildTool/current.json`은 생성하지 않았다. 테스트 요청은 Git 제외 `Logs/BuildToolStage02` 아래에만 있다.

3단계 [계획](../../Planning/project-wide/build-tool-stage-03-plan.md)·[목표](../../Goals/project-wide/build-tool-stage-03-goal.md)·[복사용 실행문](../../Commands/project-wide/build-tool-stage-03-command.md)을 작성했다. 옵션 소스나 SDK 연동은 구현하지 않았다.

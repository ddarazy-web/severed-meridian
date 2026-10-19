# 레벨툴 리팩토링 4단계 검증 기록

상태: 4단계 완료. 아래 실행 기록은 시간순이며 최종 판정은 마지막 완료 감사에 정리한다. 대량 복구 성능은 해결되지 않았다.

기준: [4단계 계획](../../Planning/MoonRabbitJunkyard/LevelToolRefactoring/2026-10-19-stage04-plan.md), [목표](../../Goals/MoonRabbitJunkyard/LevelToolRefactoring/2026-10-19-stage04-goal.md).

## 실행 원칙과 선행 조사

- 기존 work 브랜치에서 진행한다. 제품 빌드·커밋·푸시·HTML·프리팹 에셋 편집을 하지 않는다. 하위 에이전트를 사용하지 않는다.
- 판정: 실행 스킬의 자동 커밋/별도 에이전트 리뷰보다 사용자 금지 및 통합 계획을 우선한다. 자체 검토와 이 문서를 실행 기록으로 사용한다.
- A의 저장·전달 검증과 B의 복구 측정은 같은 기존 Workspace/Session API를 사용한다. B에서는 Undo를 비우지 않으며 A의 UI 검사 종료용 정리와 분리한다. C는 실제 A/B 결과를 문서화한다.
- 사용자 데이터 보호 해시: `Logs/LevelToolRefactorStage04/20261019-audit/protected-before.json`. 대상은 ContentData, Assets/Data, Assets/AddressableAssetsData, ProjectSettings이며 종료 시 파일 목록과 해시를 비교한다.
- `LevelToolTransferRecoveryVerification.RunAll`은 고유 Logs 시험 폴더, 가짜 파일 선택기와 스트림으로 전송 실패를 주입한다. 실제 사용자 복구 경로를 대체한 시험 인스턴스만 사용한다. 기존 씬의 메모리상 시험 설정을 저장하거나 프리팹 원본에 적용하지 않는다.
- `ContentImportHistoryVerification.Run`은 사용자 후보 스냅샷을 읽기만 하고, 삭제/잠금/설정 변경은 고유 Logs 아래 복제 트리에서만 수행한다.
- `JsonPackConversionVerification`은 ContentData 하위에 시험 출력을 만들고 과거 latest 포인터에 의존한다. 실행 전에 고유 Logs 출력으로 보완한다.
- `PackedTrialVerification`은 정식 Addressables 리소스를 읽는다. 이번에는 동일한 격리 스냅샷으로 만든 바이트를 기존 로더 주입 경계로 전달하여 JSON/팩을 비교한다.

## 실행 기록

- ZIP 전송·취소·실패·복구 UI 전체 검사: `Logs/TestHarness/20261019-194403-497-Run.log`. 마지막 ReloadRecovery에서 미확정 문자열 복원 실패, exit 1. 나머지 전송/실패/취소/중단 사례 assertion 통과. 전체 통과로 계산하지 않는다.
- 좁은 재현 `194559-644`, `194712-537`, `194806-318`에서 동일 실패를 확인했다. 저장 시 필드 경로와 복원 후 경로는 동일하지만 실제 복원 입력 목록은 0개였다.
- 원인: 3단계에서 OnDisable에 추가한 패널 Dispose가 필드를 비운 뒤 기존 FlushRecovery가 실행되어 이전 정상 체크포인트의 미확정 입력을 빈 목록으로 덮었다. 종료 시 FlushRecovery를 패널 해제 앞으로 이동했다. 임시 진단 로그는 제거했다. 기존 재현 검사에 Disable 직후 기록과 기존 pendingInputs 동일성 assertion을 추가했다.
- 수정 검증: `Logs/TestHarness/20261019-194854-980-Run.log`에서 Disable 직후 입력 기록 보존, 재실행 후 미확정 한글·선택·Undo/Redo 복원 및 원본 JSON 보존 통과, exit 0.
- 전체 전송/복구 재검사 `Logs/TestHarness/20261019-194945-785-Run.log`도 exit 0, 하네스 해제 완료. 이후 히스토리 보호→팩 게시 실패 복원→ZIP 왕복을 순차 실행 중이다.
- 구조 문서 초안을 `Docs/architecture/MoonRabbitJunkyard/level-tool-structure.md`에 작성했다. 최종 검사 결과 반영 전이다.
- 히스토리 보호: `20261019-195100-574-Run.log`, 17개 검사 통과, exit 0.
- 팩 게시/등록 실패·재시작 복원: `20261019-195157-822-Run.log`, 통과, exit 0.
- ZIP 왕복 후 모든 fixture 레벨 ID/게임 정의/MemoryPack 입력 동일성: `20261019-195242-350-Run.log`, 통과, exit 0.
- 기존 변환 검사의 출력 위치를 ContentData에서 고유 Logs 폴더로 변경했다. Workspace 검사의 결과 로그도 고유 폴더를 사용하여 이전 로그를 덮어쓰지 않는다. assertion은 유지한다.
- `LevelToolRecoveryCostVerification.Run` 추가: 사용자 main 스냅샷은 읽기 전용, 각 10/30/60회 표본은 고유 복사본. Export/Save/Read/Restore 시간, 파일 크기, Undo/Redo 수, 메모리 표본을 기록하며 동일 상태와 실제 Undo/Redo를 비교한다. 보호 한도 초과 시 해당 검사 프로세스만 exit 2로 종료하고 큰 표본은 실행하지 않는다.
- 최초 비용 측정 `20261019-195419-069-Run.log`: 문서 38개, 10회 표본에서 30.09초 보호 한도 도달. Mono의 Process.WorkingSet64가 0을 반환했고 Environment.Exit도 즉시 종료하지 못했다. 해당 로그를 명령행에 가진 PID 75504만 재식별해 6,388,899,840바이트 메모리를 확인하고 중단했다. exit -1, 하네스 해제 완료. 30/60회 미실행, 0바이트는 유효 측정값으로 사용하지 않는다.
- 측정 보완: Windows GetProcessMemoryInfo로 실제 작업 메모리를 얻고 한도 도달 시 현재 검사 프로세스만 TerminateProcess한다. Export/Save 중간 결과와 중단 단계를 기록하도록 추가했다. 제품 코드나 사용자 이력은 변경하지 않는다.
- 보완 측정 `20261019-195656-546-Run.log`: 문서 38개/10회 편집, ExportState 6,249.51ms, 상태 UTF-8 26,414,289바이트, ToolDraftStore.Save 18,605.21ms. Read 단계에서 총 30.022초/작업 메모리 7,328,096,256바이트로 보호 종료(exit 2), 하네스 정상 해제. 30/60회는 미실행. 저장 성공과 최종 복원 완료는 구분하며 이번 표본의 Read/Restore/UndoRedo 검증은 완료되지 않았다.
- 근거: AuthoringEditSession은 Undo마다 모든 문서 사본을 보관하고 ExportState가 전체 이력을 직렬화한다. ToolDraftStore.Save는 저장 전·임시 파일 검증 때 Workspace와 전체 이력을 재구성하며 Read에서도 재구성한다. 이번 패널 분리와 독립된 기존 비용이며 최적화 완료로 보고하지 않는다. 후속 개선은 변경 문서 공유/복구 검증 중복 축소의 동작 보존을 먼저 설계해야 한다.

## 중간 인계 기록 (이후 실행으로 해소)

- 현재 실행 중인 검사 없음. 마지막 비용 검사 세션 1324는 exit 2, 연결 해제 확인.
- A의 남은 항목: 수정된 JsonWorkspaceVerification/JsonPackConversionVerification 실행; 기본 속성·공급·독립/공유 레벨별 설정의 저장 왕복 통합 증거 보강; 동일한 스냅샷의 팩 바이트를 기존 PackedPuzzlePlayAdapter 로더 주입 경계에 제공해 JSON/팩 게임 시험 결과 비교. 정식 Addressables를 교체하지 않는다.
- B: 정상 범위의 작은 표본에서 전체 복원/UndoRedo 동일성 보완 가능. 10회 실데이터 표본이 한도를 넘었으므로 30/60회 실데이터 표본은 재실행하지 않는다. 최적화는 별도 후속 제안이며 자동으로 범위를 넓히지 않는다.
- C: 구조 문서 초안 검토, 보호 해시 26,682개 파일 재비교, 수정한 OnDisable의 추가 입력/수명주기 회귀, 최종 diff/컴파일 및 목표별 완료 감사가 남아 있다.

## 중간 잔여 항목 (최종 판정은 아래 참고)

- A: 편집/저장/ZIP/팩/시험/히스토리 보호의 범위별 증거 확보.
- B: 10/30/60회 편집 이력의 복구 비용 및 동일성 측정. 표본 30초 또는 Unity 작업 메모리 8GiB 보호 한도.
- C: 실제 구조 문서, 변경 범위 및 보호 파일 최종 비교, 결과 보고.

## 후속 실행 기록

- JsonWorkspaceVerification: `20261019-200009-681-Run.log`, 임시 SO 표시/편집 수집/UndoRedo/실패 원복/공유 문서 등록/해제 통과, exit 0.
- JsonPackConversionVerification: `20261019-200047-740-Run.log`에서 구간·세대·중복 검사는 통과했으나 과거 latest-fixtures에 공유 흐름이 없어 마지막 assertion 실패. 제품 변환 오류가 아니라 시험 입력 전제 누락이다. assertion을 삭제하지 않고 독립 생성 fixture로 보완했다.
- LevelToolDataFlowVerification: `20261019-200321-070-Run.log`, 기본 속성·고정 공급·독립 튜토리얼·공통 구성·레벨별 조건 횟수를 실제 편집 API로 생성하고 UndoRedo, Workspace 상태, 저장·재로드, ZIP 왕복의 내용 fingerprint/ID/참조/선택 레벨 보존 통과, exit 0.
- 보호 대상 첫 비교: `Logs/LevelToolRefactorStage04/20261019-audit/protected-after.json`, 26,682개 파일, 추가/삭제/변경 0. 최종 검사 후 확인을 마친다.
- JsonPlayVerification.RunIsolatedPacked 추가: 기존 실제 게임 세션의 튜토리얼 동작·정착·공급·미션·재시작·정식 완료 기록 보호 assertion을 유지하고 같은 JSON에서 생성한 메모리 바이트를 팩 로더에 주입한다. 기존 제품 Addressables는 읽거나 교체하지 않는다.
- 공유 팩 fixture 보완 중 `200409-664`는 빈 프로젝트의 팩용 요소 정의 누락, `200532-224`는 복사한 visual의 리소스 목록 누락으로 실패했다. 기존 시험 카탈로그·요소·visual과 그 프로젝트 리소스 목록을 함께 연결해 fixture를 완성했다. 제품 변환 로직은 수정하지 않았다. 데이터 흐름 검사에는 이동 횟수 31·공급 수 3/Type2·공유 조건 횟수 2·독립 레벨 연결 없음의 직접 assertion을 보강했다.
- `200808-861`은 첫 교환 고정 블록 누락, `200949-264`는 빈 프로젝트의 미션 누락을 팩 실행 검증기가 거절했다. fixture에 실제 교환 3매칭 배치와 색상 미션을 추가했다. `201101-870` 데이터 흐름/선택 레벨/ZIP 통과, `201140-124` 팩 변환 전체 및 공유 튜토리얼 레벨 1/2 roundtrip 통과, 각각 exit 0.
- 정상 소형 복구 표본 `201218-836`: 문서 2개/10회 편집, Undo 9/Redo 1, 복구 파일 54,769바이트. Export 14.31ms, Save 68.37ms, Read 34.10ms, Restore 21.13ms, 전체 약 0.242초. 상태/선택/실제 UndoRedo 동일성 통과. 프로세스 작업 메모리 표본 최대 2,201,833,472바이트는 Unity 전체 메모리이며 이 표본만의 할당량이 아니다. 실데이터 38문서 표본의 보호 종료를 이 결과로 대체하지 않는다.
- 마지막 팩 fixture 보완: `200808-861`/`200949-264`의 실패는 각각 고정 시작 블록/미션을 요구하는 게임 실행 검증에 따른 입력 오류다. 최종 자료는 고정 교환 배치와 미션을 포함한다.
- 게임 비교의 과거 latest-export 포인터가 없어 현재 TutorialSampleBoards의 교환/로켓 생성/조합/내구도 샘플을 직접 인코딩한다. 같은 스냅샷으로 JSON 요청과 팩 요청을 만든다. 사용자 main은 카탈로그·리소스의 읽기 전용 공급원으로만 사용한다.
- `201333-726`에서 교환 게임 결과와 정식 완료 기록 보존까지 통과했으나 임시 객체를 영구 SO처럼 항상 clean이라고 가정한 assertion에서 실패했다. 내용 동일성과 초기 dirty 상태 동일성을 각각 검사하도록 수정하고, 팩 생성 그래프의 예약 해제 후 누수 기준을 잡는다. 영구 SO를 쓰는 기존 검사 경로의 clean 조건은 유지한다.
- 최종 실제 게임 비교 `20261019-201455-755-Run.log`: 교환/로켓 생성/로켓+폭탄/장애물 피해 4개 샘플의 초기 보드·공급·RNG·미션, 동일 행동 수락, 정착·파워·튜토리얼 상태가 JSON/팩에서 일치했다. 각 튜토리얼 완료·재시작·정식 완료 키 불변·소스 내용/dirty 불변·객체 해제, 취소/손상 요청 거절 통과, exit 0.

## 최종 완료 감사

모든 Unity 검사는 순차 실행했고 종료 후 하네스 연결을 해제했다. 실행 형식은 아래와 같으며 표의 메서드를 지정했다.

```powershell
& Tools/Testing/ProjectTests.ps1 -Action Run -Method LevelAuthoring.Editor.<표의 메서드>
& Tools/Testing/ProjectTests.ps1 -Action Compile
```

로그는 모두 `Logs/TestHarness/20261019-<표의 로그>`다.

| 범위 | 메서드 | 최종 로그 | 결과 |
|---|---|---|---|
| 전송/복구 17사례 | LevelToolTransferRecoveryVerification.RunAll | 194945-785-Run.log | 통과, exit 0 |
| 이력 보호 17검사 | ContentImportHistoryVerification.Run | 195100-574-Run.log | 통과, exit 0 |
| 게시 실패 복원 | JsonPackPublicationVerification.Run | 195157-822-Run.log | 통과, exit 0 |
| ZIP 자료 왕복 | ProjectPackageTrialVerification.Run | 195242-350-Run.log | 통과, exit 0 |
| Workspace | JsonWorkspaceVerification.Run | 200009-681-Run.log | 통과, exit 0 |
| 편집→저장→ZIP | LevelToolDataFlowVerification.Run | 201101-870-Run.log | 통과, exit 0 |
| 팩 구간/참조/공유 흐름 | JsonPackConversionVerification.Run | 201140-124-Run.log | 통과, exit 0 |
| 소형 복구 동일성 | LevelToolRecoveryCostVerification.RunSmall | 201218-836-Run.log | 통과, exit 0 |
| JSON/팩 실제 게임 4샘플 | JsonPlayVerification.RunIsolatedPacked | 201455-755-Run.log | 통과, exit 0 |
| 입력/수명주기 | LevelToolInputVerification.Run | 201611-413-Run.log | 통과, exit 0 |
| 숨긴 모바일 입력의 저장/Undo/Redo | LevelToolMobileVerification.RunPendingCommands | 201652-456-Run.log | 통과, exit 0 |
| 최종 컴파일 | -Action Compile | 201742-621-Compile.log | 성공, exit 0 |
| 실데이터 복구 비용 | LevelToolRecoveryCostVerification.Run | 195656-546-Run.log | 10회 표본 Read 중 보호 종료 exit 2; 성능 통과 아님 |

- A: 편집·저장·ZIP·팩 변환·게임 비교·히스토리 보호 증거 확보. 사용자 데이터/ID/스키마/정식 리소스 형식 변경 없음.
- B: 성능 측정 및 위험 보고 완료. 38문서/10회 편집에서 Export 6.25초, Save 18.61초, Read 중 누적 30.02초 보호 종료. 당시 전체 Unity 작업 메모리 7.33GB. 30/60회 표본은 보호 정책에 따라 미실행, 큰 표본의 최종 복원도 미검증. 소형 표본의 정상 복원 결과로 이 실패를 대체하지 않는다.
- C: 의존 관계·상태 소유권·해제 순서·기능 추가 위치를 구조 문서에 작성했다. 남은 Screen 래퍼의 실제 호출부를 확인했으며 이번 분리로 새로 미사용이 된 경로를 발견하지 않아 무관한 정리는 하지 않았다.
- 최종 보호 감사 `Logs/LevelToolRefactorStage04/20261019-audit/protected-final.json`: 26,682개 파일, 추가/삭제/내용 변경 0. 사용자 원본·후보·팩·Addressables·설정 보존.
- `git diff --check` 통과. 기존 meta 변경 없음, 신규 검사 meta GUID 2개는 각각 1곳에만 존재. Assets/__ProjectTests 없음, 하네스 해제 확인. prefab/scene/HTML/Packages/ProjectSettings/ContentData 변경 없음.
- 제품 빌드·커밋·푸시·사용자 이력 삭제는 하지 않았다. Android 실기기와 제품 실행 파일 수동 검사는 미실시다.

## 후속 우선순위

대량 Undo 문서 사본과 복구 검증 중복 재구성의 시간·메모리 비용을 별도 계획으로 개선해야 한다. 기존 저장 형식, Undo 의미, 손상 복구와 원자적 저장 보호를 유지하는 기준을 먼저 정한다. 이번 단계에서 자동으로 추가 구현하거나 이력을 축소하지 않았다.

```text
fix: 레벨툴 종료 입력 복구 보존 및 통합 회귀 검증

- 편집·ZIP·MemoryPack·게임 시험과 사용자 데이터 보호 검증 보강
- 구조·확장 경계 문서화 및 대량 복구 성능 위험 기록
```

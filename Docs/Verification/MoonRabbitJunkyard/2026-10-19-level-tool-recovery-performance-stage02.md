# 복구 성능 개선 2단계 실행 기록

상태: 2단계 완료. 전체 성능 목표 달성과는 구분하며 30회 보호 종료·60회 미실행을 아래에 기록한다.

- 기준: LevelToolRecoveryPerformance/2026-10-19-stage02-plan.md 및 대응 목표. 제품 빌드/커밋/푸시/HTML/프리팹 편집 금지, 하위 에이전트 미사용.
- 시작 git 상태 clean. 1단계 작업은 현재 저장소 기준에 포함되어 있다. 새 2단계 변경만 수행한다.
- 보호 파일 기준: Logs/LevelToolRecoveryPerformance/stage02/protected-before.json.
- 소유권 조사: ContentSnapshot은 생성/조회 모두 사본을 반환한다. Session.Get/Documents/SelectedCells는 사본, Apply 콜백은 문서 전체 사본을 받으며 최종 게시도 사본이다. current.Documents/saved는 외부로 직접 반환되지 않는다. current의 LevelId/Cells는 선택 동작에 따라 바뀌므로 State 자체는 공유하지 않는다.
- 방향: 내부에 게시된 문서/사전은 수정하지 않는 규칙을 유지하고 Capture는 상태 껍질/선택만 복사한다. Apply의 변경되지 않은 문서는 기존 내부 사본을 재사용하고 변경 문서는 복사한다. 출력은 v1 JsonWriter 직렬화, 복원은 Workspace에서 이미 파싱한 JToken 직접 전달 및 복원 한 건 내부의 동일 문서 공유를 사용한다.
- 복원 공유 기준은 ID/kind/JSON 내용 전체 일치다. 전역 캐시·ID만 비교·공개 ContentDocument API 변경은 하지 않는다.

## 실행 증거

- 211825-349 기준 검사는 fixture가 실제 필드 displayName 대신 name을 써서 Save 검증에서 실패했다. 시험 필드만 바로잡았다.
- 211946-980 RunContracts: 변경 전 외부 참조/선택/콜백 보관/예외/무변경/Fork/UndoRedo/추가삭제/저장/Discard/v1 왕복 계약 통과, exit 0.
- 212031-125 RunSharing: 변경 전 `unchanged document is shared across internal history`에서 예상한 실패(exit 1). 원본 테스트와 연결 해제 상태를 확인한 뒤 구현했다.
- 구현: 게시된 Session 내부 문서 사전은 이후 수정하지 않는다. Capture/Fork/saved는 내부 사본을 공유하며 State의 선택 배열은 복사한다. Apply callback은 여전히 전체 편집 사본을 받고, 변경된 문서만 새 사본으로 게시한다.
- 구현: Session.WriteState가 JsonWriter로 v1을 직접 쓰며 Workspace는 같은 writer에 Session을 기록한다. RestoreObject는 상위 JObject를 읽어 문자열 재직렬화/파싱을 없앤다. 복원 하나 안에서 ID/kind/JSON 전체가 같은 문서만 공유한다. SharedTutorialDraft의 Session 복원도 같은 경계를 사용한다.

- 212207-384 RunSharing: 내부 공유, 외부 수정 격리, 예외/무변경/Fork/UndoRedo/저장/Discard, v1 문자열 왕복 및 복원 후 공유 통과, exit 0.
- 212244-970 Prepared 회귀: 손상 기록/previous/저장 순서/가변 View/일회 적용/실패/공유 초안 보존 통과, exit 0.
- 212317-351 고정 10회 입력: 입력 SHA256 0911A4AAAB24D1E39C0C65C843E7FD1E0499455242012EBD163E44B8209688B0 유지. Save 6,115.80ms, ReadPrepared 5,045.89ms, Apply 0.5098ms. 전체 상태 및 실제 Redo/Undo 동일성까지 통과, 전체 14.738초, 프로세스 최대 3,468,873,728바이트, draft 30,180,635바이트. 1단계 동일 경로는 Save 10,648.54ms/Read 9,513.75ms였고 추가 이력 검증 중 30초 보호 종료했다. 단일 관측 비교이며 반복 중앙값과 UI 정지 시간은 3단계다.
- RunPreparedSeries를 추가하여 실제 개선된 ReadPrepared→Apply 경로로 10/30/60 편집 표본을 점진 측정한다. 기존 Run/RunSmall의 legacy Read→Restore 회귀 경로는 유지한다. 큰 표본은 이전 표본이 보호 한도 내에서 완료해야 실행한다.

- 여러 문서 동시 변경의 단일 Undo/Redo를 추가한 212502-137 RunSharing: 통과 exit 0.
- 212542-919 RunPreparedSeries: 38문서 실제 편집 10회(Undo9/Redo1) 통과. Export 619.88ms, Save 6,297.37ms, Read 4,887.29ms, Apply 0.2773ms, 편집/검증 포함 전체 20.707초, 최대 프로세스 표본 4,497,600,512바이트. 고정 파일 검사와 달리 원본 편집 Session도 유지하므로 두 메모리 수치를 같은 실험으로 비교하지 않는다.
- 같은 실행의 30회: Export 1,542.81ms, workspace 70,437,589바이트, Save 14,606.77ms. Read 중 총 30.101초/6,957,383,680바이트에서 보호 종료(exit 2), 하네스 해제 확인. 30회 Read/전체 복원은 미완료, 60회는 미실행. 추가 큰 표본 반복 없이 UI/소형/편집 어댑터 회귀로 이동한다.
- 10회 실측 Export+Save+Read+Apply는 약 11.80초로 통합 목표 5초에 미달한다. 전체 프로세스 약 4.50GB도 4GiB 목표를 넘는다. 2단계 개선과 전체 성능 목표 달성을 구분한다.

- 212747-994 전체 전송/복구 UI 17사례 통과, exit 0. 미확정 입력·선택·UndoRedo·취소·실패와 fresh-screen 복구 유지.
- 212909-291 JsonWorkspaceVerification 통과, exit 0. 임시 Unity 표시 모델, 편집 수집·실행 취소/재실행 표시·실패 원복·해제 유지.
- 212948-104 LevelToolDataFlowVerification 통과, exit 0. 기본/공급/독립·공유 튜토리얼, 레벨별 바인딩, 저장/재로드/ZIP/선택 레벨 연결 유지.
- 213020-250 소형 legacy 복구 회귀 통과, exit 0. Export 2.50ms, Save 43.91ms, Read 21.47ms, Restore 22.70ms, 전체 0.128초. 전체 상태와 실제 Redo/Undo 동일성 포함.
- 213052-610 Compile 성공 exit 0, 하네스 해제 완료. 이후 테스트만 추가하여 Session/Workspace 인코딩·재구성 및 제거한 문자열 왕복을 고정 입력에서 분리 측정한다.
- 보호 파일 중간 비교 protected-after.json: 26,682개 추가/삭제/변경 0.

## 최종 코덱 검증과 완료 감사

- 213216-299 코덱 분리 측정 통과 후, 입력 격리 assertion을 실제 RestoreObject에 전달한 원본 노드의 문서 변경으로 보강했다. 최종 213339-772-Run.log도 컴파일·실행 통과(exit 0), 연결 해제 확인.
- 최종 고정 입력 구간: Workspace JSON 파싱 2,930.75ms, 제거한 Session 문자열 생성 891.25ms/재파싱 2,975.47ms, Session Decode 1,524.66ms/Encode 587.07ms, Workspace 전체 Decode 4,814.87ms/Encode 567.85ms. 구간 분리 시험 합계 17.449초, 최대 프로세스 표본 4,059,795,456바이트. 제거된 경로를 시험에서 재현한 값이며 제품 경로 전체에 이 값을 중복 합산하지 않는다.
- 이전 고정 v1 입력과 새 Session/Workspace 출력은 문자열까지 동일했다. 외부 원본 JObject 문서를 수정해도 복원된 전체 상태가 바뀌지 않았다. 호환성의 기본 계약은 JSON 내용/이력 의미이며 객체 속성의 임의 배치 순서를 새로운 저장 계약으로 추가하지 않는다.
- 소유권: current/saved/undo/redo/Fork의 쓰기 경계를 조사했다. 공개 반환값·입력 Snapshot·보관 callback·선택 배열의 수정, 여러 문서 변경, 예외·무변경·추가/삭제·저장·Discard·분기 편집 및 공유 초안 회귀 통과.
- 복구: 과거 파일, 손상/실패, 미확정 입력, 선택, 현재/저장 기준, previous 보호와 화면 fresh recovery 회귀 통과.
- 측정: 동일 10회 입력 해시 유지. 10회 실제 편집과 전체 검증 정상 완료. 30회는 Read 중 30초 제한, 60회는 미실행. 성능 목표 미달은 그대로 남겼다.
- 보호: Logs/LevelToolRecoveryPerformance/stage02/protected-final.json에서 26,682개 파일 추가/삭제/내용 변경 0. 원본/후보/정식 팩/Addressables/ProjectSettings 보존.
- 자체 검토: 공개 가변 ContentDocument 계약 변경 없음, 전역 복원 캐시 없음, 새 패키지/asmdef 없음, 기존 meta 변경 없음. 변경은 Session/Workspace/공유 초안 복원과 관련 검사/Markdown에 한정한다.
- 3단계 계획·목표·실행문 작성 완료. 구현 미착수. 실제 UI 메인 스레드 정지·반복 중앙값·제품 실행 파일/실기기 성능은 미검증이다.
- 제품 빌드·커밋·푸시·HTML·프리팹/씬 수정·사용자 이력 삭제는 하지 않았다.

## 실행한 검사 진입점

기본 명령: `Tools/Testing/ProjectTests.ps1 -Action Run -Method LevelAuthoring.Editor.<진입점>`. 제품 빌드 명령이 아니다.

| 진입점 | 최종 로그 번호 | 결과 |
|---|---|---|
| LevelToolHistoryOwnershipVerification.RunSharing | 212502-137 | 통과 |
| LevelToolRecoveryPreparedVerification.Run | 212244-970 | 통과 |
| LevelToolRecoveryCostVerification.RunFixedTen | 212317-351 | 통과 |
| LevelToolRecoveryCostVerification.RunPreparedSeries | 212542-919 | 10회 통과, 30회 보호 종료, 60회 미실행 |
| LevelToolTransferRecoveryVerification.RunAll | 212747-994 | 17사례 통과 |
| JsonWorkspaceVerification.Run | 212909-291 | 통과 |
| LevelToolDataFlowVerification.Run | 212948-104 | 통과 |
| LevelToolRecoveryCostVerification.RunSmall | 213020-250 | 통과 |
| -Action Compile | 213052-610 | 성공 |
| LevelToolRecoveryCostVerification.RunFixedTenCodec | 213339-772 | 통과 |

로그 위치는 `Logs/TestHarness/20261019-<번호>-Run.log`이며 Compile만 접미사가 `-Compile.log`다.

```text
perf: 레벨툴 이력 문서 공유 및 복구 직렬화 최적화

- 내부 이력 사본 공유와 외부 수정 격리 유지
- v1 직접 직렬화 및 중복 문자열 파싱 제거
- 복구 회귀·성능 검증과 3단계 계획 정리
```

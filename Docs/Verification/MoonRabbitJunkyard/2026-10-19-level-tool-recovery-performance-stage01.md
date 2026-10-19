# 복구 성능 개선 1단계 실행 기록

상태: 1단계 완료. 기록은 시간순이며 최종 완료 감사와 마지막 보완 결과를 함께 확인한다. 전체 성능 문제의 해결을 의미하지 않는다.

- 기준: Planning/MoonRabbitJunkyard/LevelToolRecoveryPerformance/2026-10-19-plan.md 및 대응 목표.
- 실행 전 변경: 이번 계획·목표 문서 두 폴더만 미추적. 기존 소스 변경 없음.
- 실행 방식: executing-plans와 프로젝트 Unity 규칙 적용. 계획의 별도 승인 없는 하위 에이전트 금지에 따라 자체 검토한다. 제품 빌드/커밋/푸시/HTML/프리팹 편집 금지.
- 보호 기준: Logs/LevelToolRecoveryPerformance/stage01/protected-before.json. 사용자 자료는 읽기만 하고 고유 시험 출력 사용.
- 확인: Save는 입력/임시 파일/기존 파일을 Validate하면서 각각 Workspace 전체 복원. Read 후 화면에서도 추가 복원하고 최종 적용 때 Export→Restore를 반복한다.
- 결정: 1단계는 검증된 결과의 제한된 소유권 전달과 동일 바이트의 임시 파일 검증을 우선한다. 전역 가변 캐시, 검증 제거, 복구 형식 변경, Undo 축소는 하지 않는다.
- 남은 일: 기준 검사·고정 fixture/비용 기록, 안전성 회귀 보강, 중복 경로 개선, 동일 시험 및 측정, 보호 파일 최종 감사, 2단계 문서.

## 진행 증거

- 기준 소형 표본: 20261019-203642-238-Run.log, exit 0. Export 18.03ms, Save 61.90ms, Read 24.00ms, Restore 20.82ms. 전체 0.236초, 프로세스 표본 최대 2,181,939,200바이트.
- 새 경계 재현: 203748-485-Run.log에서 prepared recovery read boundary exists 실패(exit 1), 예상한 신규 경계 부재 확인. 이후 203913-846 및 204012-393 검사 통과(exit 0).
- 변경: ToolDraftStore가 검증된 Workspace와 화면 값을 소유하는 일회용 PreparedDraft를 반환한다. 화면은 검증 결과를 적용하며 기존 Workspace 객체 정체성을 유지한다. 외부로 반환하는 JSON은 복사한다. 실제 임시 파일은 검증된 직렬화 바이트와 직접 대조하며 현재 파일/previous 검증은 유지한다.
- 신규 검사는 결과 재사용 거절, View 수정 격리, 전체 이력/선택 및 Undo/Redo, 손상된 current의 previous 복귀, current 손상이 정상 previous를 덮지 않음, 오래된 sequence 무시, 잘못된 workspace/view/shared 초안 거절, 버리기 시 저장본 읽기 실패의 기존 세션 보존을 확인했다.
- 고정 10회 입력: 기존 4단계 recovery-cost-7cd97e920e6b4bd2baa78ef5aaa5167e/10/draft.json을 Logs/LevelToolRecoveryPerformance/stage01/fixed-ten-record.json으로 복사. SHA256 두 파일 동일: 0911A4AAAB24D1E39C0C65C843E7FD1E0499455242012EBD163E44B8209688B0. 사용자 원본은 수정하지 않는다.
- 비교 한계: 이전 10회 기준은 저장 후 Read 중 보호 종료된 단일 측정이다. 고정 입력의 함수별 비용은 비교할 수 있지만 이전 미완료 전체 시간의 정확한 개선율이나 Android 성능으로 확대 해석하지 않는다.

## 비용 및 추가 검사 진행

- 전송/복구 17사례: 204042-653-Run.log, exit 0, 하네스 해제 완료.
- 개선 후 소형 표본: 204240-684-Run.log, exit 0. Export 24.46ms, Save 58.14ms, legacy Read 50.50ms, 별도 Restore 28.28ms, 전체 0.293초. 이 검사는 호환 API를 사용하므로 새 화면 경로 속도를 대표하지 않으며 단일 소형 측정의 변동을 개선으로 주장하지 않는다.
- 고정 10회 표본: 204317-769-Run.log. 동일 입력 SHA256 확인. 초회 Save 10,648.54ms, ReadPrepared 9,513.75ms, Apply 0.3376ms. 전체 상태 ExportState 동일성 확인 후 Redo/Undo 검증 중 총 30.018초/Unity 작업 메모리 4,912,058,368바이트로 보호 종료(exit 2), 하네스 해제 확인. 전체 통과로 표시하지 않는다. 30/60회는 실행하지 않는다.
- 이전 동일 입력의 초회 Save 18,605.21ms 대비 약 42.8% 감소를 관측했다. 현재 파일이 이미 있는 반복 저장은 추가 검증이 있으므로 이 수치로 대표하지 않는다. 이번 고정 표본은 기존 편집 세션을 미리 유지하지 않으므로 전체 프로세스 메모리의 엄밀한 전후 개선율도 계산하지 않는다.
- 고정 이력의 실제 Redo/Undo 확인은 초회 Save 비용을 제외한 별도 시험으로 분리한다. 이를 저장 포함 전체 30초 검사의 성공으로 바꾸어 보고하지 않는다.
- 204449-300 검사는 추가한 공유 초안 검사에서 Runtime namespace 누락으로 컴파일 실패했다. 이때 Unity가 만든 시험 폴더 meta에 folderAsset 표시가 없어 안전 해제가 중단됐다. 원본/복사본 해시 일치를 확인한 뒤 기록에 속한 생성 meta만 Logs에 백업하고 제거하여 정상 Detach했다. 원본 에셋 meta는 수정하지 않았다. namespace 보완 후 재검사 진행.

## 중복 경로 비교

아래 횟수는 실제 호출부와 최종 코드 검토 기준이며 프로파일러 계측값이 아니다.

| 경로 | 이전 Workspace 재구성 | 현재 | 비고 |
|---|---:|---:|---|
| 초회 Save | 2 | 1 | 입력 의미 검증 유지, 디스크 임시 파일은 입력 바이트와 일치 검사 |
| 기존 파일이 있는 Save | 3 | 2 | 기존 정상/손상 기록 판단은 여전히 재검증 |
| 초기 화면 Read→복원 적용 | 4 | 1 | 검증 결과가 세션을 소유하다 한 번만 이동 |
| previous 복원 적용 | 3 | 1 | previous도 동일 검증 결과 경계 사용 |

파일 I/O·임시 바이트 비교·기존 기록 검증 각각의 실시간은 아직 독립 계측하지 않았다. Save 전체와 Read/Apply 경계 시간만 직접 측정했다. 불필요한 제품 계측 API를 추가하는 대신 단계 2의 구간 분석에 이 한계를 명시한다.

- 204655-170에서 손상 current를 교체하는 File.Replace가 IOException으로 실패했다. 같은 코드·사례는 앞선 2회 검사에서 통과했으며, 당시 current는 `broken`, previous는 기존 정상 기록이고 임시 파일은 남지 않은 것을 확인했다. OS 파일 교체 실패의 외부 원인은 확정하지 않았다. 제품에 임의 재시도나 검증 완화를 넣지 않고 코드 변경 없이 새 격리 폴더에서 재현 확인한다.

- 코드 변경 없는 재검사 204821-188: 전체 Prepared 검증 통과, exit 0. 실제 파일 잠금 시 current/previous 보존 및 임시 파일 정리, 유효 공유 사본과 부모 이력의 독립 복원/명시 적용도 통과했다. 단발 File.Replace IOException은 재현되지 않았고 근본 원인을 확정하지 못했으며 제품 재시도 정책은 변경하지 않았다.
- 고정 10회 이력 단독 복원 204852-224: 통과, exit 0. 입력 해시 동일, ReadPrepared 10,186.56ms, Apply 0.3445ms. 전체 상태 동일성과 실제 Redo→Undo 후 전체 상태 동일성 확인. 파일 복사/읽기/적용/검증 전체 24.640초, 프로세스 최대 5,475,987,456바이트, draft 30,180,635바이트. 저장 포함 표본의 보호 종료와 별개로 보고한다.

- 입력/수명주기 204947-338-Run.log: 통과 exit 0. 최종 제품 소스 컴파일 205024-380-Compile.log: 성공 exit 0, 하네스 정상 해제.
- 고정 입력 구간 분리 측정 205118-286-Run.log: input Validate 9,209.29ms, 임시 파일 쓰기/Flush 29.25ms, 디스크 바이트 동일성 검사 626.98ms, 기존 파일 읽기/파싱/Validate 9,645.78ms. 전체 22.348초, 프로세스 최대 4,855,869,440바이트. 이 값은 각 작업을 격리하여 순차 실행한 비용이며 단일 Save 내부 계측이나 전체 UI 시간으로 해석하지 않는다. 앞의 독립 비용 미계측 항목은 이 추가 검사로 보완했다.

## 1단계 완료 감사

- 기준 보호 파일 26,682개: protected-after.json 비교에서 추가/삭제/내용 변경 0. 사용자 원본·후보·팩·Addressables·ProjectSettings 보존.
- 기능 검증: Prepared 결과 일회 적용/가변 View 격리, 전체 상태·선택·Redo/Undo, 손상 current/previous, 오래된 저장 순서, 잘못된 저장/화면/공유 초안, 버리기 읽기 실패, 실제 교체 잠금, 공유 초안과 부모의 독립 복원·적용을 검사했다.
- UI 회귀: 전송/복구 17사례와 입력 수명주기 검사 통과. 종료 전 미확정 입력 보존 수정 유지.
- 비용: 동일 입력 해시를 고정하고 Save/Read/Apply 및 검증/I/O 구간을 측정했다. 대형 저장 포함 표본의 추가 이력 검사 보호 종료를 기록했으며 큰 이력 자체의 상태와 Redo/Undo는 별도 정상 표본으로 검증했다. 30/60회는 미실행이다.
- 구조: 기존 Workspace 객체 정체성 유지, 한 번만 세션 소유권 이동. 입력 의미 검증·디스크 실제 기록 검사·previous 보호 유지. v1 및 작성 JSON/MemoryPack 형식 변경 없음.
- 2단계 상세 계획/목표/실행문 작성 완료, 구현 미착수. 병목은 입력/기존 기록의 전체 이력 검증·재구성이며 파일 쓰기 자체가 아니다.
- Android 실기기·제품 실행 파일·메인 스레드 100ms 목표는 미검증이다. 전체 5초/15초 성능 목표를 달성했다고 보고하지 않는다. 단발 File.Replace IOException의 외부 원인은 확정하지 못했으나 실패 시 기록 보존과 재검사 정상 결과를 모두 남겼다.
- 제품 빌드·커밋·푸시·HTML·프리팹 편집·사용자 이력 삭제 없음.

검사 명령은 `Tools/Testing/ProjectTests.ps1 -Action Run -Method LevelAuthoring.Editor.<클래스.메서드>`를 사용한다. 실행한 진입점: LevelToolRecoveryPreparedVerification.Run, LevelToolTransferRecoveryVerification.RunAll, LevelToolRecoveryCostVerification.RunSmall/RunFixedTen/RunFixedTenHistory/RunFixedTenParts, LevelToolInputVerification.Run. 컴파일은 `-Action Compile`이다.

```text
perf: 레벨툴 복구 검증과 상태 적용의 중복 재구성 제거

- 검증된 복구 결과의 일회 적용 및 임시 파일 바이트 검증
- 이력·공유 초안·실패 보호 회귀와 성능 측정 보강
- 2단계 이력 사본 최적화 계획 작성
```


최종 보완: 구간 측정 205118-286-Run.log exit 0 및 하네스 해제 확인. 최종 보호 비교 protected-final.json에서 26,682개 파일 변화 없음. 신규 검사 GUID는 원본 한 곳에만 존재하고 기존 meta 변경 없음. diff 공백 검사 통과. 제품 에셋·설정·HTML 변경 없음.

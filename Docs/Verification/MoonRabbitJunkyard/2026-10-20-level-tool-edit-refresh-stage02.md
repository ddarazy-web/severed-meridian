# 변경 문서 복사 최적화 2단계 — 작업·검증 기록

상태: 2단계 구현·회귀·성능 측정 완료(2026-10-20). 이름 편집 중앙값 100ms 목표 충족. 3단계 대표 조작·반복 편집 검증은 미착수다.

계획: [2단계 계획](../../Planning/MoonRabbitJunkyard/LevelToolRecoveryPerformance/2026-10-20-edit-refresh-stage02-plan.md).

## 범위와 구현 판단

- 기존 작업 트리를 보존한다. 제품 빌드·커밋·푸시·HTML 갱신·하위 에이전트를 사용하지 않는다.
- 게시된 문서는 불변으로 공유하고, 편집 대상으로 지정한 문서만 초안과 게시본으로 깊은 복사한다.
- 내부 문서 참조를 외부 콜백에 넘기지 않는다. 검증·복사가 끝난 뒤 한 번에 게시하여 예외 시 기존 문서와 이력을 유지한다.
- dirty는 불변 참조가 같으면 비교를 생략하고, 다른 참조는 내용을 비교한다. project의 세대별 documents 경로는 기존처럼 제외한다.
- 대상이 알려진 레벨·속성·모양·튜토리얼·공유 사본·기존 Editor 창 편집 호출부를 대상 지정 API로 전환한다.
- 호환 경로: `ElementSelectionEditing.Paste`는 콜백에서 카탈로그 및 요소 문서 전체를 조회한다. 기존 `Apply`를 유지한다. 읽기 계약까지 바꾸는 추가 작업은 이번 단일 대상 편집 최적화와 구분한다.
- 튜토리얼의 임시 그래프 구성 및 기존 Editor 표시 객체 직렬화, 얕은 문서 사전 구성·순회는 남는다. 전체 처리 비용이 O(1)이 되었다고 보고하지 않는다.

## 검사 기록

1. RED: `20261020-143642-916-Run.log`. 대상 지정 API 부재로 신규 검사 실패.
2. GREEN: `20261020-144112-407-Run.log`, exit 0, 검사 연결 해제. `AuthoringTargetedEditVerification.Run`:
   - 단일 편집은 초안·게시본 2회 복사.
   - 콜백이 보관한 중첩 JSON을 수정해도 게시본은 유지.
   - 다중 대상 예외 원복, 한 번의 Undo/Redo, 저장 값 복원 dirty 해제.
   - 무변경 편집 이력 없음, 새 분기 Redo 제거, Fork 격리.
   - 대상 밖 접근/추가·선택 레벨 삭제·ID 불일치 거부.
   - 추가/삭제·저장 후 Undo/Redo·세대 경로 제외·이력 복구.
   - 문서 수를 102개로 늘려도 단일 편집 복사 2회 및 무관한 버전 유지.
3. `20261020-144227-367-Run.log`: `LevelToolEditingBoundaryVerification.Run`, exit 0, 연결 해제. 속성·배치·복사/붙여넣기·공급 및 잘못된 편집 경계 통과.
4. `20261020-144320-634-Run.log`: `JsonWorkspaceVerification.Run`, exit 0, 연결 해제. 기존 Editor 표시 사본 캡처, 여러 문서 등록, Undo/Redo, 실패 원복 통과. 이후 일괄 실행 래퍼가 빈 `$LASTEXITCODE`를 실패로 오인하여 중단됐으며, 검사는 성공했다. PowerShell 성공 상태 확인으로 래퍼를 고쳐 다음 검사부터 이어갔다.
5. `20261020-144443-919-Run.log`: `LevelToolManualSaveVerification.RunSharedPending`, exit 0, 연결 해제. 공유 사본의 미확정 입력 적용/취소 및 명시적 저장 전 메모리 유지 통과.
6. `20261020-144520-596-Run.log`: `LevelToolManualSaveVerification.RunExistingRecovery`, exit 0, 연결 해제. 기존 복구본 읽기 및 수동 저장 정책 회귀 통과.
7. `20261020-144606-331-Run.log`: `LevelToolSaveUxVerification.Run`, exit 0, 연결 해제. 저장 실패·재시도·dirty 유지, 전환 실패, 네 화면 크기의 저장/공유/복구 안내 검사 통과.
8. `20261020-144716-039-Run.log`: `LevelToolRefreshPerformanceVerification.Run`, exit 0, 연결 해제. 29개 검사 통과. 결과: `Logs/LevelToolRefreshPerformance/check-ff346ddec7cc4e2687ffa5b6834d8895/results.txt`. 무변경 표시 문서 조회 0회, 단일 레벨 변경 조회 1회, 무관한 정의·보드·소재 보존, Undo/재로드/공급/오류 후 복구 확인.
9. `20261020-144757-404-Run.log`: `LevelToolDataFlowVerification.Run`, exit 0, 연결 해제. 공급·튜토리얼 수치·이력·저장·JSON/ZIP 데이터 전달 통과.
10. `20261020-144833-080-Run.log`: `JsonPackConversionVerification.Run`, exit 0, 연결 해제. MemoryPack 구간 분할·기존 codec 왕복·문서 비변경 확인.
11. `20261020-144933-305-Run.log`: `JsonPlayVerification.RunIsolatedPacked`, exit 0, 연결 해제. 격리 입력의 초기 보드/공급/RNG/미션·튜토리얼 동작·파워/정산 및 취소/잘못된 입력 검사 통과. 의도적인 실패 입력 로그는 결과 실패로 계산하지 않는다. 제품 빌드 및 최종 렌더 품질 검사가 아니다.
12. `20261020-145030-959-Run.log`: `LevelToolRecoveryPreparedVerification.Run`, exit 0, 연결 해제. 복구 상태의 이력·선택·공유 사본 격리와 손상/이전 기록 처리 통과.
13. `20261020-145106-879-Run.log`: `LevelToolManualSavePerformanceVerification.Run`, exit 0, 연결 해제. 같은 38문서의 정상 표본 3회 통과.
14. `20261020-145307-574-Run.log`: `LevelToolTransferRecoveryVerification.RunReloadRecovery`, exit 0, 연결 해제. 새 화면에서 복구한 저장/현재 상태·선택·Undo/Redo와 미확정 입력을 확인했다. 명시적 사본 저장은 정상 JSON을 만들고 Undo를 유지하며 원본 JSON과 기존 복구 기록을 변경하지 않는다.

## 성능 결과

입력 SHA256: `0911A4AAAB24D1E39C0C65C843E7FD1E0499455242012EBD163E44B8209688B0`.
내용 fingerprint: `c9e92deacc6d99383c2ecb886c010d086b07193176a581e1f3e95b5650bf7e4f`.

원시 기록: `Logs/LevelToolManualSave/performance-7b6d5c01cfdf425ba76d82f4b347acaf/measurements.jsonl`.
요약: `Logs/LevelToolRefreshPerformance/stage02/final-measurement-summary.json`.

| 항목(ms, 별도 표기 제외) | 1단계 최종 | 2단계 1회 | 2회 | 3회 |
|---|---:|---:|---:|---:|
| 전체 편집 중앙값 | 221.13~226.99 | 62.35 | 64.85 | 63.50 |
| 데이터 편집 중앙값 | 164.77~166.61 | 14.53 | 14.59 | 14.49 |
| 명령·화면 구간 중앙값 | 56.59~57.84 | 47.73 | 50.32 | 48.99 |
| 전체 편집 최대값 | 303.28~413.42 | 246.80 | 105.21 | 117.41 |
| Undo | 58.06~111.39 | 50.89 | 51.16 | 47.79 |
| Redo | 56.40~69.12 | 48.36 | 49.69 | 47.68 |
| clean leave | 4.22~5.71 | 2.76 | 1.17 | 1.20 |
| 명시적 저장 완료 | 10754~11121 | 9768.88 | 10125.50 | 9958.73 |
| peak working set(GiB) | 2.98~3.12 | 3.02 | 3.13 | 3.15 |
| 자동 프로젝트/복구 쓰기 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 |

전체 편집 중앙값은 1단계 대비 약 72%, 데이터 편집은 약 91% 감소했다. 세 표본 모두 이름 편집 중앙값 100ms 목표를 충족했다. 최대 지연은 목표와 구분하며 순간 지연이 사라졌다고 주장하지 않는다.

복사 범위는 신규 모델 검사의 2문서/102문서 입력에서 모두 단일 편집 2회로 확인했다. 이전 `Apply` 구현은 전체 문서 초안을 복사했으므로 38문서에서 38개 초안과 변경 문서 게시본이 필요했다. 표시 조회는 별도 회귀에서 무변경 0회·단일 레벨 변경 1회로 확인했다.

측정은 동기 명령 경로이며 지연 큐로 비용을 옮기지 않았다. 화면 구간은 명령 처리가 포함되므로 순수 GPU 렌더 시간이 아니다. 누적 할당 카운터가 지원되지 않아 null로 기록했다. 표본 30초·8GiB 보호는 유지했고 발동하지 않았다.

저장은 여전히 약 10초이며 이번 작업으로 해결됐다고 판단하지 않는다. 반복 100회 편집, 대표 조작의 개별 시간, 실기기·물리 IME·최종 렌더 품질은 3단계 또는 별도 기기 검증으로 남는다. 1단계에서 기록한 780×360 보드 높이와 오프스크린 흰색 스킨 문제는 이번 범위에서 수정하지 않았다.

## 최종 검토와 인계

- 자체 코드 검토에서 콜백에 전달되는 값이 대상의 사본인지, 예외 전에 현재 사전을 변경하지 않는지, 게시 시 보관된 참조를 다시 격리하는지 확인했다.
- `Assets/Scripts`의 세션 `Apply` 호출은 앞서 기록한 붙여넣기 호환 경로만 남는다. 기존 Editor 표시 사본의 Capture는 로드된 표시 객체를 직렬화하며, 이 비용까지 제거한 것으로 주장하지 않는다.
- 검사 실행은 Unity 컴파일을 포함했고 모두 연결 해제했다. 고정 fixture 해시는 유지됐으며 ProjectSettings·Packages·Scenes·Prefabs 변경이 없다. 신규 meta GUID는 32자리이며 Tests 내 중복이 없다.
- 문서 상대 링크와 `git diff --check`를 확인했다. 공백 오류는 없고 Git의 LF→CRLF 정규화 안내만 있다. 커밋·푸시·제품 빌드·HTML 갱신은 하지 않았다.
- 후속: [3단계 계획](../../Planning/MoonRabbitJunkyard/LevelToolRecoveryPerformance/2026-10-20-edit-refresh-stage03-plan.md), [3단계 목표·복사용 실행문](../../Goals/MoonRabbitJunkyard/LevelToolRecoveryPerformance/2026-10-20-edit-refresh-stage03-goal.md). 다음 단계는 자동 실행하지 않는다.

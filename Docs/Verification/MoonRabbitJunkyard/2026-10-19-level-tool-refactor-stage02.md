# 레벨툴 리팩토링 2단계 검증 기록

## 변경 범위

- `AuthoringEditSession.EditLevel`: 현재 선택 레벨의 형식 확인과 기존 Apply 트랜잭션을 공통 진입점으로 제공.
- `LevelDocumentEditing`: 레벨/요소 속성 수정, 선택 배치·교체·삭제, 다른 층 보존을 화면에서 이동.
- `SupplyDocumentEditing`: 공급 항목 추가·수정·삭제/순서 변경과 튜토리얼 공급 사본 처리를 공통화.
- `LevelToolDocuments`: 현재 선택 레벨의 명시적 형식 전환과 카탈로그가 지정된 새 레벨 템플릿 생성.
- 화면은 위 기능 호출과 UI 갱신을 담당한다. 기존 Flow/Connection/Shape/Tutorial 편집 기능은 재사용한다.

새 스키마·저장 형식·데이터 자동 보정은 추가하지 않았다. 미완성 초안은 기존처럼 편집 가능하고 저장·시험 시 검증된다. 원자성은 기존 Apply의 문서 사본/실패 미반영 계약을 사용한다.

## 변경 전 기준

| 메서드 | 결과 | 로그 |
|---|---|---|
| JsonEditingVerification.Run | 통과 | `Logs/TestHarness/20261019-165601-228-Run.log` |
| JsonWorkspaceVerification.Run | 통과 | `Logs/TestHarness/20261019-165623-778-Run.log` |
| TutorialDraftEditingVerification.Run | 통과 | `Logs/TestHarness/20261019-165701-503-Run.log` |
| LevelToolTouchVerification.Run | 통과 | `Logs/TestHarness/20261019-165741-081-Run.log` |
| 신규 LevelToolEditingBoundaryVerification.Run | 누락 EditLevel API로 의도한 실패 | `Logs/TestHarness/20261019-165858-526-Run.log` |

## 신규 검증

`LevelToolEditingBoundaryVerification.Run`: 최종 보강 후 51개 항목 통과.

- 기본 속성 변경·한 번의 Undo/Redo·무변경 조작 이력 미생성.
- 잘못된 이동 수 초안 보존, 저장 거절, Undo 복구.
- 2×2 본체를 1×1로 교체할 때 원본 기준 한 번만 처리하고 생성 ID 보존.
- 마지막 칸 충돌 시 전체 영역 편집 실패, 선택·Undo/Redo까지 기존 상태 유지.
- 다른 층을 지우는 교체 거절과 원본 보존.
- 내구도·색·방향 초안 값 보존, 삭제와 Undo.
- 현재 선택 레벨만 변경, 구형 레벨의 암묵적 편집 거절.
- 공급 항목 수정·순서·삭제·실패 미반영과 일반/튜토리얼 공급 분리.
- 저장·재로드의 JSON 값 보존.
- 구형 전환 시 지정된 세 필드 외 원본 보존, Undo/Redo 및 반복 전환의 무변경 처리.

결과: `Logs/LevelToolRefactorStage02/b7cc78b58ef8497fa9f0ae12f9f83ee6/result.txt`.
실행 로그: `Logs/TestHarness/20261019-170717-208-Run.log`.

처음 추가한 구형 전환 검사는 기존 fixture에 형식 4 레벨이 없어 실패했다(`20261019-170333-347-Run.log`). 테스트 안에서 형식 4 레벨과 고정 블록을 생성하도록 수정했고 이후 통과했다. 제품 코드의 오류를 숨기거나 검사를 제거하지 않았다.

## 기존 UI 및 통합 회귀

실행 형식: `& Tools/Testing/ProjectTests.ps1 -Action Run -Method LevelAuthoring.Editor.<메서드>`.
| 메서드 | 결과 | 로그 (`Logs/TestHarness/`) |
|---|---|---|
| LevelToolEditingBoundaryVerification.Run | 통과, 51항목 | `20261019-170717-208-Run.log` |
| JsonEditingVerification.Run | 통과 | `20261019-170743-054-Run.log` |
| JsonWorkspaceVerification.Run | 통과 | `20261019-170806-486-Run.log` |
| TutorialDraftEditingVerification.Run | 통과 | `20261019-170838-430-Run.log` |
| LevelToolAdvancedVerification.Run | 통과 | `20261019-170912-802-Run.log` |
| LevelToolAdvancedVerification.RunSharedTutorial | 통과 | `20261019-172213-846-Run.log` |
| LevelToolTouchVerification.Run | 재시도 통과 | `20261019-173754-951-Run.log` |
| LevelToolTouchVerification.RunBodyReplace | 통과 | `20261019-173900-716-Run.log` |
| LevelToolTouchVerification.RunBodyProperties | 통과 | `20261019-173935-701-Run.log` |
| LevelToolInputVerification.Run | 통과 | `20261019-174016-904-Run.log` |
| LevelToolAdvancedVerification.RunTutorial | 개별 항목 통과, 정상 종료 미확인 | `20261019-174048-916-Run.log` |

첫 RunTutorial 실행은 개별 항목이 통과한 뒤 Unity 종료가 지연되어 정상 종료를 확인하지 못했다(`20261019-171122-711-Run.log`). 해당 로그 경로와 일치하는 검사 프로세스만 종료했고 하네스가 테스트 연결을 해제했다(exit -1). 이 실행을 성공으로 계산하지 않고 재실행했다. 다른 프로젝트 에디터는 변경하지 않았다.

재실행(`20261019-171722-275-Run.log`)도 마지막 `tutorial UI undo restores original document`까지 PASS 후 종료 지연이 재현됐다. 해당 검사만 종료했으며, RunTutorial은 **개별 항목 통과 / 정상 종료 미확인**으로 남긴다. 종료 지연의 원인은 확정하지 않았고 기존 검사 코드를 변경하지 않았다.

`RunSharedTutorial`은 종료가 늦었으나 exit 0을 확인했다(`20261019-172213-846-Run.log`). 이어진 Touch.Run은 테스트 진입 전 Unity 초기화 도중 exit -1073740940으로 종료됐다(`20261019-173306-166-Run.log`). 하네스 연결 해제를 확인하고 다시 실행했다. 실제 검사 assertion 실패와 초기화/종료 문제를 구분한다.

최종 RunTutorial 재시도에서도 개별 항목 PASS 후 종료 지연이 반복됐다. 해당 실행 프로세스만 종료했고 exit -1 및 하네스 연결 해제를 확인했다. 최종 결과는 **10종 정상 통과(신규 51항목 포함), 1종 개별 항목 통과·정상 종료 미확인**이다. 종료 문제의 원인 분석·정상 종료 확인은 잔여 항목이며 3단계 시작 기준 검사에 포함한다. 전체 검증 완료로 표현하지 않는다.

최종 `git diff --check`에서 공백 오류 없음. 사용자 데이터·설정·씬·프리팹·HTML 변경 없음. 테스트 임시 연결은 해제했다.

## 경계 검토

- 레벨 편집 UI의 직접 JSON 키 할당/배열 수정은 공통 기능으로 이동했다.
- 남은 JSON 생성은 Recovery/RecoveryInputs의 UI 복구 상태, TutorialPicking의 임시 선택 정보 등 콘텐츠 편집이 아닌 상태다. 기존 위치를 유지했다.
- UI의 형식 확인은 구형 문서에 전환 안내를 보여주는 읽기 판단으로 남는다. 쓰기 허용 판정은 EditLevel에 모았다. 기존 저수준 배치 함수의 스키마 방어는 다른 호출부를 위해 유지했다.
- 흐름 경로 선택·연결 선택·일반/튜토리얼 편집 대상 선택은 UI 조정이며 기존 기능별 서비스에 위임한다. 불필요한 새 래퍼는 추가하지 않았다.
- Undo 단위를 나누거나 공통 API 내부에서 중첩 Apply를 호출하지 않는다. 일괄 배치 함수는 세션 트랜잭션 안에서 호출한다.
- 자체 검토를 수행했으며 독립 리뷰어 검토는 별도 수행하지 않았다.

## 데이터와 작업 제약

- 새 검사는 `Logs/LevelToolRefactorStage02/<id>/`에 별도 프로젝트를 생성한다. 기존 검사는 각자의 격리 fixture/복구 경로를 사용한다.
- 시작 시 1단계 변경이 스테이징되어 있었으며 작업 도중 HEAD에 1단계 커밋이 확인됐다. 이번 실행에서는 Git 인덱스 변경·커밋·푸시를 수행하지 않았다.
- 사용자 원본·ProjectSettings·레벨 리소스·씬·프리팹·UI 에셋은 수정하지 않는다.
- 제품 빌드·HTML 매뉴얼 갱신·사용자 이력 삭제를 하지 않는다.
- Android 실기기 및 Windows 실행 파일 수동 검증은 이번 작업에 포함하지 않는다.

## 다음 단계

[3단계 계획](../../Planning/MoonRabbitJunkyard/LevelToolRefactoring/2026-10-19-stage03-plan.md)과 [목표·실행문](../../Goals/MoonRabbitJunkyard/LevelToolRefactoring/2026-10-19-stage03-goal.md)을 작성했다. 3단계 구현은 시작하지 않았다.

커밋 메시지 제안:

```text
refactor: 레벨툴 JSON 편집 규칙을 공통 기능으로 분리

- 레벨·요소 속성과 선택 배치·공급 편집 공통화
- 단일 Undo와 실패 원복·저장 호환성 유지
- 편집 경계 회귀 검사 및 3단계 계획 추가
```

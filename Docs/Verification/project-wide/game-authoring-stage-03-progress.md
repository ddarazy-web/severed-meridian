# 게임·제작 도구 분리 3단계 — 검증 기록

> 2026-10-09 · 3단계 구현·검증 완료. 아래 지원 범위와 제한을 포함한 완료다.

## 구현과 소유권

JSON 원본·선택·Undo/Redo·미저장·저장 revision은 `LevelAuthoring/Editing/AuthoringEditSession`이 소유한다. 공통 API에는 UnityEditor/ScriptableObject가 없다. 기존 Unity 창은 `JsonAuthoringWorkspace`가 소유하는 임시 SO로 표시한다. 동작 콜백에서 변경을 수집하고 실패하면 표시를 복원한다. Unity Undo는 해당 임시 객체만 제거한다.

`CopySerialized`의 null 배열 정규화가 불필요한 이력을 만드는 문제를 재현·수정했다. 표시 기준값과 비교한 실제 변경만 원문에 병합한다. 리소스 조회는 작업 폴더의 프로젝트 매핑을 재사용한다.

## 기능 대조표

경로는 Assets/Scripts/Features 기준이다.

| 기능 | 연결과 원본 규칙 |
|---|---|
| 문서·세션 | LevelAuthoring/Editing: 새 레벨·복제·속성·선택·Save/SaveAs·Undo/Redo·Discard·상태 복원 |
| 배치·2×2·교체·삭제·이동 | LevelDocumentEditing + Window.JsonBoard. 점유·본체 ID·연결 참조 보존 |
| 보드 활성·영역·일반 속성 | 기존 UI 동작/속성 확정 → 공통 세션. 영역 선택은 셀 목록으로 기록 |
| 단일/다중 속성·붙여넣기 | Element/Placement/Common/ContextMenu 패널의 명시적 EditLevel 트랜잭션 |
| 흐름·연결 | FlowDocumentEditing/ConnectionDocumentEditing 공통 API. 기존 창 overlay/graph/merge는 동작 경계에서 수집. 자동 전선 탐색은 기존 알고리즘 재사용 |
| 미션·공급 | MissionDocumentEditing/SupplyDocumentEditing 공통 API. 기존 창의 SupplyAction 콜백으로 한 번의 이력 |
| 공통 튜토리얼·레벨별 값 | JSON flowId/bindings. 공유 원본은 별도 사본 편집 후 명시적 적용. 사용 레벨 목록은 현재 작업 폴더 기준 |
| 샘플 | JSON tutorialSample 등록·선택·적용. 적용 시 독립 단계 ID, 원본 수정이 적용본에 전파되지 않음 |
| 모양 | ShapeDocumentEditing/Window.JsonShapes. 등록·이름·현재 초안 기준 사용 현황·미사용 삭제·기본 생성구를 갖춘 새 레벨 |
| 저장·복구 | ContentSnapshotStore의 해시 충돌 검사. 이전 정상본은 원본 덮어쓰기 없이 다른 폴더에 사본 저장 |
| 시험 | 현재 편집 snapshot → JsonPuzzlePlayAdapter → PuzzleEditorLaunchRequest.Json → CreateTest. 플레이/진단도 현재 JSON 입력 |
| 복귀 | 도메인 재로드 후 문서·이력·선택층·선택 셀·스크롤 복원. 진행 중 드래그·연결 입력은 취소 |

문서 ID는 이름/번호 변경에서 유지한다. 레벨 복제는 새 문서 ID를 부여하며 레벨 내부 배치/단계 ID와 명시 학습 완료 ID는 기존 SO 복제 의미를 유지한다. 완료 ID가 같으면 같은 학습임을 기존 도움말에서 안내한다.

## 실행한 검사

로그는 저장소의 무시된 Logs/GameAuthoringStage03 아래에 있다.

| 검사 | 결과/근거 |
|---|---|
| 독립 .NET JSON/편집 검사 | 194 PASS, common-final.log. 저장 충돌·불완전 초안·독립 세션·사본·배치·층·2×2·모양·흐름·연결·공급·미션·실패 원자성·Undo |
| 임시 표시 트랜잭션 | workspace-final.log exit 0. 등록/참조 Undo·Redo·실패 객체 정리 |
| 기존 제작 필드 codec 회귀 | codec-regression.log 29 PASS. draft codec 별도 4 PASS |
| 전체 fixture 창 검사 | window-review-fixes.log exit 0. JSON 열기·저장·재열기·새 레벨·복제·단일 속성/삭제·배치·공급·공유원본·흐름·모양·Undo·요청 생성 |
| 샘플 실제 버튼 | sample-ui.log exit 0, 7항목. 생성/적용/ID/원본 격리/Undo·Redo/디스크 불변 |
| 복구·수명주기 | lifecycle-green.log exit 0, 10항목. 복구원본 보호·취소·실패·저장·버리기·신규레벨 버리기·선택 복원 |
| 실제 게임 씬 왕복 | window-play-domain.log exit 0. 도메인 재로드 활성, 미저장 이동횟수 게임 적용, CreateTest, 선택층·선택칸·Undo/Redo 복원, JSON/SO/정식 튜토리얼 기록 불변 |
| 공통 튜토리얼 사본 게임 왕복 | flow-draft-play-2.log exit 0, 12항목. stable ID 원본/카탈로그 재연결, 단계/파라미터 보존, 복귀 후 명시적 적용, 부모 폴더 변경/종료 시 잘못된 시험 차단 |
| 통합 재실행 | final-all.log exit 0. 당시 연결된 Unity 검사 전체 및 독립 194개 통과. 이후 추가한 자식 사본 왕복은 위 별도 검사로 확인 |

실제 UI 검사는 Unity 창/버튼/필드 콜백을 생성·호출하는 자동 검사다. 별도 사용자의 시각 검수나 Windows 배포 앱 검증을 했다는 뜻은 아니다. Player/Addressables 빌드는 수행하지 않았다.

## 리뷰와 의도적 제한

독립 리뷰에서 단일 요소 속성/삭제의 이력 누락, SO 모양 목록의 모드 전환 잔류, 삭제된 모양 콜백, JSON 공통 흐름 자식 창의 SO 기능 노출을 확인하고 수정했다. 후속 수명주기 검토에서 자식 창의 재로드 원본 연결과 카탈로그 참조 복원도 추가했다. 공통 연산 5개 파일은 기존 규칙과 별도 대조 검토했다.

검증 중 두 실패도 구분했다. 기존 SO의 임시 공통 흐름까지 차단하던 제한은 JSON 출신 표시로 좁혀 SO 회귀를 복원했다. 새 게임 왕복 검사 자료의 설명 단계에 행동 대상이 지정되어 실행이 거절된 경우는 잘못된 시험 자료를 유효한 강조 영역 설정으로 수정했으며, 제품 검증 규칙을 완화하지 않았다.

- JSON 여러 레벨 일괄 시험은 지원하지 않는다. 이유를 표시하고 차단하며 선택한 JSON 레벨의 플레이/진단/게임 시험을 제공한다.
- JSON 표현 리소스는 현재 Unity 프로젝트에 존재하는 리소스 매핑을 이용한다. AssetDatabase 없는 실행용 화면/로더는 4단계다.
- JSON 공통 원본 사본 창은 독립 편집·현재 사본 시험·명시적 원본 적용에 한정한다. 샘플/모양/폴더/카탈로그 관리는 부모 JSON 창에서 한다. 외부 Inspector의 별도 이력으로 편집하는 경로도 차단한다.
- 이전 정상본 복구는 별도 사본 저장 방식이다. 손상되거나 외부 수정된 원본을 자동 덮어쓰지 않는다.
- 공통 레벨툴 씬·Windows UI·전체 SO 이관은 이번 범위가 아니다. 현재 배포 기본 입력, 50레벨 팩, Addressables, Build Profile을 바꾸지 않았다.
- 커밋·푸시·Player/Addressables 빌드·HTML 매뉴얼 변경을 하지 않았다. 테스트 연결 해제, git diff --check, 신규 스크립트 메타, 데이터/씬/패키지/프로필 변경 없음 확인.

## 최종 회귀와 인계

- 기존 SO 교체/Undo: Logs/TestHarness/20261009-001246-635-Run.log exit 0.
- JSON 사본 격리: flow-draft-final.log exit 0.
- 기존 SO 공통 구성/샘플: so-tutorial-final.log exit 0.
- 마지막 실제 Play 재연결: flow-draft-play-2.log 12 PASS.
- [4단계 계획](../../Planning/project-wide/game-authoring-stage-04-plan.md) · [목표](../../Goals/project-wide/game-authoring-stage-04-goal.md) · [복사용 실행문](../../Commands/project-wide/game-authoring-stage-04-command.md). 문서만 작성했으며 구현은 착수하지 않았다.


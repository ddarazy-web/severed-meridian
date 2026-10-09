# 게임·제작 도구 분리 5단계 기능 대응표

2026-10-09 · 현재 구현과 검증의 대응표. 최종 독립 리뷰와 완료 조건 대조를 마쳤다.

[계획](../../Planning/project-wide/game-authoring-stage-05-plan.md) · [목표](../../Goals/project-wide/game-authoring-stage-05-goal.md) · [검증 이력](game-authoring-stage-05-progress.md) · [1단계 조사](game-authoring-stage-01-dependency-audit.md)

코드 경로는 `Assets/Scripts/Features/` 기준이다. 화면 열의 partial은 `LevelTool/Runtime/LevelToolScreen`이다. 로그는 `Logs/GameAuthoringStage05` 및 `Logs/TestHarness`에 있다. 검사는 실제 UI 콜백/씬을 사용하는 Unity 검사이며 사람의 시각 검수나 배포 앱 검증을 뜻하지 않는다.

| 기능 | 기존 기능 소유자 → 공통 구현 | 새 화면 | 확인한 근거 |
|---|---|---|---|
| 기본 레벨·배치·층·미션·복제 | LevelEditor 패널 → DocumentEditing, LevelDocumentEditing, MissionDocumentEditing | Board, 기본 레벨 목록 | LevelToolUIExercise: 새 레벨·층 교체·영역·내구도·미션·복제·단일 Undo. 통합 RunPlay 통과 |
| 팔레트·거절 입력 | LevelTool 기존 기본 화면 | Board, 팔레트 | ux-green: 즉시 선택 표시 및 거절된 수치 복원 |
| 고정·유지 공급 | SupplyPanel → SupplyDocumentEditing | Supply | advanced-isolated-green 및 통합 Advanced: 순서·수량·색·소진·유지·다중 생성구·Undo |
| 낙하·직접 경로·포털·벽·도착점 | FlowPanel → FlowDocumentEditing | Flow, FlowOverlay | Advanced 및 Reload: 경로 확정/취소·포털·중력·벽 거절·재로드 복원 |
| 합류·연결·전선 | Merge/Flow 패널 → Flow/ConnectionDocumentEditing | Flow, Connections, ConnectionOverlay | Advanced: 합류 순서·부분 전선·내부 전선 거절·연결 ID·충전값·Undo |
| 2×2 본체·이미지 | PlacementPanel → LevelDocumentEditing | Board, 공통 보드 렌더러 | UIExercise: 이동 instanceId 보존·역교체. Visual: 하나의 본체+77 일반 블록, 78개 실제 sprite, 크기/피벗/각도 보존. 연결 대상 이동·삭제·Undo072520-313 통과 |
| 모양 | JsonShapes → ShapeDocumentEditing | Shapes | Advanced: 등록·사용처·사용 중 삭제 거절·활성 마스크만 적용·Undo |
| 여러 레벨 제작 | DuplicatePanel → DocumentEditing, AuthoringEditSession | 목록·새 레벨·복제 | UIExercise 및 Advanced: 새 ID, 번호, 문서 선택·이력 보존. 여러 레벨 시험은 아래 별도 행 |
| 튜토리얼 조립 | Composer/Conditions → TutorialAuthoringRules, TutorialDraftEditing, 기존 HandlerRegistry | Tutorial, TutorialConditions, TutorialTargets | RunTutorial: 10종 조건·행동·셀/영역 선택·명시/자동 강조·취소·Undo. ComposerConditions 기존 조건 회귀 065825-373 통과 |
| 공유 구성·레벨별 값 | Shared 패널/FlowAuthoring → SharedTutorialDraft, TutorialFlowResolver | TutorialSharing, Parameters, Bindings | RunSharedTutorial: 12종 값·동기화·독립 복사·충돌·Undo. RunShared: 도메인 재로드 보존. SharedDraftGame: 고정 공급·두 행동·내구도 실제 게임 |
| 샘플·생성 블록·논리 재생 | SampleCatalog/Boards/UserSampleStore → 공통 Tutorial 규칙·기존 ReplayValidator | TutorialSamples, Supply, Validation | RunTutorialSamples: 단계/전체 등록·ID/원본 격리·시험 보드·세 샘플 재생. 공유 사본 실제 게임과 함께 다단계 공급 검사 |
| 구조·참조·오류 이동 | 기존 문서/레벨/튜토리얼 검사기 재사용 | Validation | SamplesExercise: 잘못된 단계 오류→해당 단계 선택, 수정 후 검사 갱신. Portable 280: 문서·참조·불완전 초안/공개 저장 경계 |
| 봇·반복·시드·중단 | AutoPlay 기존 실행기 → Runtime BotPlay/BatchSession | Bot, Batch, TrialBoard | RunBot 065407-671: 전략·시드·한 수/판·정지·동일 사본 재시험·초안/정식 기록 보존. 시험 보드 표시 |
| 결과·비교·보관·재생 | AutoPlay 기존 통계/Replay/Archive | History, HistoryDisplay | history-ux-green 및 RunBot: 필터·원본 덮어쓰기 거절·버전 불일치 제한·별도 보관·원본 행동 재생. BotAnalysis Core062932-917: 실제 200판과 전 행동 재생 |
| 여러 레벨·추천 구간 | 기존 MultiLevel/Balance 실행·저장 공통화 | MultiTrial | RunMulti, BalanceJson, MultiLevel Start, RangesOnly: 오류 레벨 격리·중지·목록 복원·현재 200판 구간·사본과 이력 보존 |
| 시험 기록 정리 | TestRecordCleanup을 GUID 유지하여 Runtime으로 이동 | Records | Core064921-915: 소유 파일·정션/모르는 파일 보호·JSON source.context. UI065312-445: 조회/확인/취소·중첩 정리·별도 보관 보존·실제 일시정지 시험 중 차단 |
| 실제 게임 왕복 | JsonPuzzlePlayAdapter → 기존 PuzzlePlayRequest/CreateTest | Play | 통합 RunPlay, SharedDraftGame, JsonPlay065934-199: 같은 SO/JSON 입력·시드·각 행동 결과, 공급/RNG/미션/파워/튜토리얼, 실패/취소 객체 정리·정식 기록 불변 |
| 저장·종료·복구 | AuthoringToolWorkspace, AuthoringEditSession | Storage, Recovery | Reload065509-118, Shared065604-224, TrialLifecycle065718-213 및 scene-unload: 실제 재로드/Play 종료/씬 해제, 초안·선택·Undo/Redo·중단 기록 보존 |
| 기본 진입·구형 자료 | LevelEditorWindow/LevelInitialStateWindow/LevelAssetOperations → LevelToolLauncher/LegacyImport | 공통 씬, 기존 창은 안내/인계 | Default070214-790: 실제 메뉴·미저장 씬 복원. Gateway072111-517: 구형 SO 원본 불변·JSON 선택/이력·공유 사본 분리. Handoff072220-106: 현재 작업 취소 보호·불완전 사본·명시 적용. LaunchIncoming072321-851: 실제 씬 전달·이전 씬 복원 |

## 불변 조건과 검증 경계

- 원본 JSON과 SO를 자동 동기화하지 않는다. 전체 정식 JSON 이관·50레벨 MemoryPack 입력 전환은 6단계다. 현재 팩/Addressables 구현을 변경하지 않았다.
- 문서 ID, 배치 instanceId, 단계/조건 authoringId, flowId, bindings, 학습 완료 의미를 보존한다. 공통 변경은 한 동작 한 Undo이며 공유 사본은 명시 적용 전 부모와 분리한다.
- 미완성 초안은 복구·인계할 수 있지만 공개 저장/시험의 유효성 검사를 우회하지 않는다.
- 런타임 제작/시험은 기존 퍼즐·튜토리얼·봇 규칙을 재사용한다. Runtime의 Editor/Tests 의존 검색 결과 없음과 최종 변경 범위를 독립 리뷰에서 확인했다.
- 통합 실행의 기록 정리 실패는 수정 후 별도 회귀로 확인했고, 미도달 검사들은 aggregate-tail로 모두 확인했다. 한 번의 통합 실행이 처음부터 끝까지 무실패였다고 주장하지 않는다.
- 폐기된 창의 UI 테스트는 현재 공통 씬 검사로 통합 명령에서 교체했다. 과거 검증 결과를 현재 구형 UI가 계속 제공된다는 근거로 사용하지 않는다.
- 독립 리뷰의 공유 사본 인계 배치 도구 잔류 문제를 수정하고072803-339에서 회귀 통과했다. 빌드·기기·배포 앱·HTML 매뉴얼은 이번 검증 범위가 아니다.


# 레벨 튜토리얼 1단계 — 데이터·저장·에디터 검증

날짜: 2026-10-07. 상태: 완료.

[계획](../../../Planning/MoonRabbitJunkyard/Tutorial/stage-01-data-editor-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/Tutorial/stage-01-data-editor-goal.md) · [저장 결정](../../../Decisions/MoonRabbitJunkyard/2026-10-07-tutorial-pack-envelope.md).

## 구현 결과

- LevelDefinition에 레벨별 튜토리얼을 추가했다. 설명·교환·파워 교환 발동·아이템, 문구·강조·행동 좌표·아이템·파워 정의 ID·생성/발동/제거 조건·고정 시드/공급을 작성한다.
- 레벨 설정의 ‘레벨 튜토리얼’ 패널에서 단계 추가·삭제·위/아래 정렬과 조건/공급 편집을 지원한다. 첫 교환 칸은 임시 선택하고 둘째 칸에서 한 번의 Undo로 확정한다. 보드에 대상/방향을 표시한다.
- 대상 선택은 배치/교체/지우기·흐름·연결점 입력보다 먼저 처리하고 우클릭 편집도 차단한다. 취소·Esc·포커스 상실·단계/레벨 전환·UI 재생성·Dispose에서 선택/구독을 정리한다.
- 정적 검사를 레벨 검사/실행 구성/내보내기에 연결했다. 초기 대상·범위·비활성·인접·벽·아이템·ID·수량·최소 이동·무작위 공급·공급 값 오류를 단계 경로로 표시한다. 후속 생성 파워의 초기 배치는 요구하지 않는다.
- 기존 팩1/2 DTO를 보존하고 TFPK 팩3에 기존 팩2와 전체 레벨 번호별 메타데이터를 담았다. 튜토리얼 없는 입력의 형식·바이트·지문과 기존50레벨 주소·ECPK·제작 SO 배포 제외는 유지한다.
- Copy/Snapshot/JSON/실행 요청에 메타데이터를 전달하고 입력 지문에 튜토리얼 편집을 반영한다. 구형 쓰기의 정보 손실을 거절하며 초기 미배치 공급/결과와 생성 본체 정의도 팩에 포함한다.

## 실제 검사

Unity6000.3.10f1의 별도 배치 Editor와 Tools/Testing/ProjectTests.ps1를 사용했다. 검사 후 Tests/Editor의 소스 연결을 해제했다.

| 검사 | 통과 | Logs/TestHarness 실행 로그 |
|---|---:|---|
| Tutorial.Editor.LevelTutorialDataVerification.Run | 63 | 20261007-131746-888-Run.log |
| Tutorial.Editor.LevelTutorialEditorVerification.Run | 27 | 20261007-131623-696-Run.log |
| Levels.Editor.LevelStorageBaselineVerification.Run | 82 | 20261007-131030-492-Run.log |
| Elements.Editor.ElementPackVerification.Run | 53 | 20261007-131256-129-Run.log |
| Levels.Editor.PlacementReplacementVerification.Run | 22 | 20261007-131327-852-Run.log |
| Elements.Editor.ElementLegacyVisualVerification.Run | 79 | 20261007-131404-962-Run.log |
| Elements.Editor.ElementContentAuthoringVerification.Run | 74 | 20261007-131433-118-Run.log |
| 합계 | 400 | 각 검사 exit0, FAIL0 |

신규 원문은 Logs/Tutorial/Stage01/data-results.txt와 editor-results.txt, 회귀 원문은 regressions 하위에 보관했다. 이전 결과 파일은 검사 뒤 원문으로 복원했다.

데이터 검사에는 전체 값 왕복·팩1/2 읽기·기존 출시 팩2 바이트 동일·v4/v5 기존 지문·팩3 지문·1/50/51/100/101·혼합/중복/누락·헤더/잘린 데이터·참조 폐쇄·실제 Asset/MemoryPack 실행 요청이 포함된다. Asset/팩3의 전체 논리 상태·다음 난수와 기존 게임 결과가 동일했다.

에디터 검사에는 실제 창/문구 바인딩 입력·버튼·포인터/더블클릭·Undo/Redo·표시·강조 토글·아이템 선택·저장/재로드·GUID·단계/레벨 전환·Esc·포커스 상실·UI 재생성·오류 표시·반복 Dispose가 포함된다. 고유 이름의 소유한 시험 에셋을 만들고 정리했다.

테스트 소스를 연결 해제한 상태의 게임 C# 컴파일도 exit0으로 통과했다. Logs/Tutorial/Stage01/game-only-compile.log 및 game-only-compile-result.json에 기록했다. -batchmode -quit -projectPath -logFile로 에디터 컴파일만 수행했고 플레이어/Addressables 번들은 생성하지 않았다.

## 수정 과정과 자기 검토

최초 계약 누락 실패를 먼저 확보했다(red.txt). 보드 Color 속성과 UnityEngine.Color 이름 충돌을 수정했고, 실제 UI의 삭제 Undo 실패를 확인해 버튼 편집을 독립 Undo 그룹으로 확정했다. 이후 동일 검사와 추가 수명 검사가 통과했다.

실패한 컴파일이 만든 시험 연결 폴더의 불완전한 메타데이터는 소유 기록/내용을 확인하고 mount-meta-recovery에 원문을 보관한 뒤 연결을 해제했다. 원본 메타데이터는 변경하지 않았다. 회귀 출력 수집에서 과거 로그까지 읽다가 메모리 부족이 발생해, 실제 갱신 결과 파일만 백업/복원하도록 좁혀 남은 검사도 통과했다.

최종 자기 검토에서는 구형 DTO/필드 순서, 모든 Snapshot/Copy/JSON/지문 호출부, 제작/배포 경계, 참조 정의 폐쇄, 보드 선택 우선순위와 수명 해제, 원본/일반 동작 보존을 점검했다. 하위 에이전트는 사용하지 않았다. Compile은 자동 승인 검사에서 처음 빌드로 오인해 거절됐으나, 실제 실행 인수와 빌드 호출 부재를 확인한 뒤 재검토가 승인됐다.

## 보존과 범위

기준 HEAD: 197cc7633b3c07137bfc8d45f5f22db85925e7ec, 브랜치 work. final-audit.json에서 원본 레벨·레벨/콘텐츠 팩·요소 제작 데이터와 메타데이터 47파일의 해시 유지, 테스트 연결 해제, index.lock 없음, HEAD 유지를 확인했다. 기존 문서 WIP를 보존했다. 빌드·커밋·푸시·사용자 Unity 종료·임의 씬 저장은 수행하지 않았다.

게임의 자동 진행·입력 제한·튜토리얼 시드/공급 적용·무료 권한·승리 보류는 2단계다. 안내 UI·영구 완료 기록·에디터 시험3모드·출시 레벨 적용은 3단계다. 미래 보드 재생은 아직 검사하지 않았으며 패널에도 정적 검사 범위를 명시했다. 모바일 화면/터치 검수는 후속 단계다.

[2단계 계획](../../../Planning/MoonRabbitJunkyard/Tutorial/stage-02-runtime-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/Tutorial/stage-02-runtime-goal.md) · [복사 가능한 실행문](../../../Commands/MoonRabbitJunkyard/Tutorial/stage-02-command.md). 2단계 구현은 시작하지 않았다.

# 2단계 실행 기록

계획: [stage-02-gameplay-plan.md](stage-02-gameplay-plan.md)

상태: 2026-09-30 2단계 구현·검증 완료. 3단계 이후는 시작하지 않았다.

## 실행 판단과 계약 확인

- Ruling: 사용자가 지정한 현재 작업 경로와 무커밋 정책을 우선하여 기존 `work` 브랜치에서 진행한다. 별도 worktree로 미추적 1단계 에셋을 누락시키지 않는다.
- Ruling: 셸 기반 skill ledger 대신 이 문서를 실행 원장으로 사용한다. 기존 미커밋 작업을 포함해 검증하고 자동 커밋하지 않는다.
- 작업 2→3: 세션의 State/CanAcceptInput/TrySwap/TryActivate를 입력이 사용한다. 실행기만 상태를 변경한다.
- 작업 2→4: Changed는 상태 표시 갱신 신호다. 선택 칸은 입력이 소유하므로 최소 표시가 입력의 선택 값도 조회한다.
- 작업 4→5: 기존 Preview를 비활성화하면 1단계 검증의 자동 Start 전제가 달라진다. 회귀 검증에서는 전용 검사 씬 또는 검사 소유 Preview를 사용하고 플레이 씬을 이전 동작으로 되돌리지 않는다.
- 기존 API 확인: StartingBoardSearch.Advance(128), BoardActionExecutor.HasPendingCascade/AdvanceCascade/Swap/Activate. Won 시 Outcome은 라스트팡 종료 전에 생기므로 Outcome만 보고 처리를 중단하면 안 된다.
- 기본 레벨 1, 시드 12345. 입력은 Input System 1.18.0. 계획의 Addressables 4.1.0은 manifest 기준으로 유지한다.

## 검증 기록

- 세션 존재 검사 RED 확인: `Logs/PuzzleGameplayVerification/red-results.txt`.
- 세션 1차 Play Mode 검사 PASS: MemoryPack 초기화, 무효 교환 상태 보존, 유효 교환 1회 소비, 중복 차단, 생성 로켓 이미지, 직접 실행기 상태 비교, 회수 미션/라스트팡 완료, 이동 소진, 로드 중 파괴, 누락 레벨. `Logs/stage02-session.log`, `Logs/PuzzleGameplayVerification/results.txt`.
- 이 검사는 테스트 전용 상태 주입으로 종료 분기를 재사용하며, 초기화 경로는 별도로 실제 팩 1을 로드했다. 당시 남았던 실제 입력 조작과 사용자 씬 검증은 아래 최종 검사와 직접 조작에서 완료했다.
- 입력 1차 검사: 좌표/탭/중복/포커스 취소 10개 통과 후 합성 마우스 검사 실패. 설치 패키지의 InputManager는 Game View 미포커스 시 Editor 업데이트로 이벤트를 처리한다. 배치 검사의 InputSettings 사본에만 IgnoreFocus/AllDeviceInputAlwaysGoesToGameView를 설정하고 finally에서 원본을 복원한다. 제품 입력 설정은 변경하지 않는다.

## 최종 검사 기록

| 범위 | 현재 결과 / 증거 |
| --- | --- |
| 게임 세션 | 29 PASS, `Logs/stage02-final.log`, `Logs/PuzzleGameplayVerification/results.txt` |
| 입력 | 22 PASS, `Logs/PuzzleBoardInputVerification/results.txt` |
| 실제 PuzzleGame 씬 | 16 PASS, `Logs/stage02-scene-final.log`, `Logs/PuzzleGameplayVerification/scene-results.txt` |
| 기존 월드 표시 | 174 PASS, `Logs/stage02-world-final.log`, `Logs/WorldBoardVerification/results.txt` |
| 기존 편집/플레이 보드 이미지 | 22 PASS, `Logs/stage02-editor-regression.log`, `Logs/RabbitArtworkVerification/results.txt` |

- 실제 씬에서 Input System 마우스로 두 칸 탭 → 매칭 생성 로켓 이미지 → 파워 탭 → 연쇄 → 씬 재진입을 확인했다. 프리팹 생성 2회 후 세션 하나와 레벨 에셋 직접 참조 없음도 확인했다.
- 가로·세로 캡처: `Logs/PuzzleGameplayVerification/gameplay-landscape.png`, `gameplay-portrait.png`, `gameplay-created-rocket.png`, `gameplay-after-cascade.png`. 카메라 렌더 결과를 직접 확인했다. 실제 에디터의 최소 표시도 아래 직접 조작으로 확인했다.
- 입력 검사에 가로/세로 카메라 비율 변경 후 좌표/상태 보존, UI Raycast, 25% 미만 드래그, 파워 탭/결합을 포함한다.
- 초기 조건 실패는 실제 StartingBoardSearch가 실패하는 고정판을 PrepareAsync에 전달하여 실행기/입력 생성이 차단됨을 확인했다. 종료 분기의 Blocked/Aborted 검사와 구분한다.
- 종료 검사는 UniTaskStatus.Pending 상태에서 소유자를 파괴했음을 먼저 확인한다. 누락 레벨, 장애물 피해, 회수 미션·라스트팡은 별도 검사한다.
- 공급 정의에만 고철이 있는 경우 이미지가 준비되지 않는 RED를 확인했다. 공급 종류/유지 모드에서 필요한 아틀라스만 추가 준비하도록 수정한 뒤 GREEN을 확인했다. 회수 부품도 같은 공급 경로를 처리한다.
- UI 차단 검사의 최초 실패는 새 Canvas의 레이아웃 준비 전 검사였다. 실제 Raycast hit를 확인한 뒤 입력을 검사하도록 수정했다.

## 독립 검토와 수정

- `stage02_final_review`의 읽기 전용 검토: Critical 없음, Important 2건.
- 터치가 Ended 상태로 남아 이후 마우스를 계속 차단하는 문제, 같은 업데이트 안의 Began→Moved/Ended를 놓치는 문제를 수용했다.
- 세 회귀 모두 수정 전 FAIL 확인: `Logs/PuzzleBoardInputVerification/touch-red-results.txt`. Input System EnhancedTouch의 프레임 보존 경로와 Enable/Disable 수명 관리로 수정했다. 수정 후 세 회귀 및 전체 입력 검사 GREEN: `Logs/stage02-touch-green.log`, 최종 `Logs/stage02-final.log`.
- 검토의 추가 검증 지적 중 초기화 실패와 화면 비율별 입력은 원래 목표의 요구사항이므로 감사 검사를 보완했다. 재검토를 반복하지 않고 전체 검사를 다시 수행했다.
- 검토자가 실행하지 않은 Unity/기기/핸들 실측은 부모 작업의 실행 증거와 미검증 목록으로 구분한다. Android 기기·플레이어 빌드는 이번에 검증하지 않았다. 핸들 소유권은 기존 월드 수명주기 검사 및 실제 pending 파괴/재진입으로 확인하며 수치 기반 메모리 프로파일링을 수행했다고 주장하지 않는다.

## 범위와 운영 판단

- Ruling: 1단계 표시 검증은 게임 씬의 Preview가 비활성화된 뒤에도 검사 소유 Play Mode에서만 세션을 제거하고 Preview를 켠다. 저장된 플레이 씬은 변경하지 않는다. 잘못 적용되면 중복 표시 위험이 있으므로 최종 174개 회귀와 씬 재진입 검사를 다시 수행했다.
- 일부 소유 배치가 검사를 끝낸 뒤 Mono 종료 단계에 남았다. 정상 종료를 기다린 뒤 명령행과 PASS 결과가 일치하는 해당 프로세스만 정리했다. 사용자 에디터와 다른 프로젝트는 종료하지 않았다.
- 사용자 무커밋 지시를 유지한다. 새 패키지, 정식 UI 프리팹, 3단계 실행 버튼, 4단계 기능, 상세 애니메이션은 추가하지 않았다.

## 실제 에디터 직접 조작

- ServeredMeridian의 `PuzzleGame` Game 화면에서 레벨 1 / 시드 12345를 실행했다. 최소 표시의 한글 메시지, 이동 수, 미션 수치, 선택 좌표를 확인했다.
- 1부터 세는 좌표로 (5행, 8열) → (4행, 8열)을 클릭했다. 이동 수 20→19, 미션 4/30·6/30, 세로·가로 로켓 이미지 두 개를 확인했다.
- 생성된 (4행, 8열) 로켓을 클릭했다. 이동 수 19→18, 미션 14/30·13/30, 연쇄 완료와 추가 자석·폭탄 이미지를 확인했다.
- (1행, 1열) → (1행, 2열)의 무효 교환은 거절 메시지를 표시하고 이동 수 18과 미션 수치를 보존했다. 실제 상태·난수 전체 보존은 별도의 세션 검사로 확인했다.
- 원본 화면 캡처: `Logs/PuzzleGameplayVerification/gameplay-native-rocket.jpg`, `gameplay-native-cascade.jpg`, `gameplay-native-invalid.jpg`. 캡처는 증거용이며 게임 이미지 에셋이 아니다.
- 검증 후 Play Mode를 종료했다. `Logs/stage02-interactive.log`에서 컴파일 오류와 실행 예외가 발생하지 않았다.

## 완료 조건별 증거

| 요구사항 | 확인한 근거 |
| --- | --- |
| 컴파일 및 기본 로드 | 최종 세션/씬 검사 실행 로그, 실제 Editor Play Mode 진입 |
| 콜드 이미지·장애물·파워 표시 | 세션 콜드 로드, 기존 월드 174개 검사, 실제 생성 로켓 캡처 |
| 유효/무효 교환과 난수 보존 | `ValidSwapConsumesOneMove`, `InvalidSwapPreservesState`, 직접 조작 |
| 마우스/터치·중복·취소·좌표 | 입력 22개 PASS, EnhancedTouch 세 회귀의 RED→GREEN |
| 파워 생성·탭·교환·결합 | 세션 로켓 검사, 입력 파워 탭/드래그 결합, 실제 씬 입력 |
| 연쇄·장애물 피해·미션 | 직접 실행기와 Snapshot 비교, 회수·내구도 검사 |
| 승패·라스트팡·종료 입력 | Won 이후 pending 관찰 및 Stopped, MovesExhausted/Blocked/Aborted 검사 |
| 실패·취소·재진입 | 누락 레벨, 시작 탐색 실패, 실제 Pending에서 파괴, 씬 재진입 |
| 기존 기능·원본 보존 | 월드 174개 / 편집 이미지 22개 PASS, 원본 사본 비교, 추적 파일 변경 없음 |
| 씬/프리팹과 저장 구조 | Unity API 생성 2회, 단일 세션, Preview 비활성, 씬 의존성에 LevelDefinition 없음 |
| 화면 비율과 실제 조작 | 가로/세로 카메라 캡처·좌표 검사, 위 실제 Editor 조작 캡처 |
| 문서 및 범위 | 목표·계획·사용법·전체 단계 상태 갱신, 자동 커밋 및 3단계 구현 없음 |

최종 PASS 기록은 총 263줄(반복 라스트팡 검사를 포함한 assertion 수)이며 실패는 0줄이다. 세션/입력/씬 결과는 21:09~21:10, 기존 월드/편집 회귀는 21:03~21:04 실행 결과다. 이후 런타임 코드 변경 없이 직접 화면 확인과 문서 정리만 수행했다. 누락 레벨 49에 대한 오류 로그는 의도한 실패 경로의 결과다.

계획의 테스트 작성 순서는 연결 타입 부재 RED를 먼저 확보하고 세부 동작 검사를 추가한 방식이었다. 개별 유효/무효 교환 검사 각각의 독립 RED를 관찰했다고 주장하지 않는다. 공급 이미지와 터치 결함은 별도의 실제 RED→GREEN을 남겼다.

## 변경 파일과 인계

- `Assets/Scripts/Features/GameScreen/Runtime/Session/PuzzleGameSession.cs`: 로드·시작 탐색·행동 실행·연쇄·수명 소유.
- `Runtime/Input/PuzzleBoardInput.cs`: 월드 좌표, 마우스/터치, 탭/드래그, 중복/취소 처리.
- `Runtime/Presentation/PuzzlePlayDebugView.cs`: 읽기 전용 최소 상태 표시.
- `Runtime/World/PuzzleArtwork.cs`: 초기 배치에 없는 공급 고철/회수 부품의 필요한 아틀라스 준비.
- `Editor/PuzzleGameAssets.cs`, `Assets/Prefabs/Game/Puzzle/PuzzleGameSession.prefab`, `Assets/Scenes/PuzzleGame.unity`: 게임 세션과 카메라/보드 연결. 위 상대 경로는 GameScreen 폴더 기준이다.
- `Editor/Tests/PuzzleGameplayVerification.cs`, `PuzzleBoardInputVerification.cs`, `PuzzleGameSceneVerification.cs`: 플레이 및 입력·실제 씬 검사. `PuzzleWorldBoardVerification.cs`는 검사 소유 Preview 경로를 유지하도록 조정했다.
- 신규 Unity 파일에는 `.meta`가 있으며 기존 GUID는 보존했다. 임시 검사 실행 스크립트는 남기지 않았다. `git diff --check` 통과, 기존 추적 파일 변경 없음. 1단계부터 있던 미추적 폰트·프리팹·씬은 유지했다.

실행은 [사용법](stage-02-gameplay-usage.md)을 따른다. Android 실제 터치 기기, 플레이어 빌드 및 수치 기반 Memory Profiler 검증은 수행하지 않았다. 정식 미션 아이콘/색상별 UI와 화면 배치는 4단계 범위이며 현재 표시는 미션 정의 순서의 종류·진행 수치다.

## 후속 편집 보드 이미지 회귀 수정

사용자가 레벨을 다시 불러와도 색 블록만 보인다고 보고한 뒤 실제 Editor Play Mode 왕복으로 재현했다. 종료 후 `LevelBoardArtwork`에는 Blocks/Floor 로더가 남지만 두 Addressables 핸들의 `IsValid()`는 false였으며 편집 보드의 이미지 수는 0이었다. 레벨 재지정도 이 캐시를 재사용하므로 새 요청을 하지 않았다. 기존 22개 이미지 검사는 사전 Warmup 이후의 매핑 검사여서 이 모드 전환 결함을 검출하지 못했다.

`LevelBoardArtwork`에서 ExitingEditMode/ExitingPlayMode에 편집용 캐시를 해제하고 EnteredEditMode/EnteredPlayMode에 열린 보드에 다시 그리기 신호를 전달하도록 수정했다. 런타임 아틀라스 로더와 레벨 데이터는 변경하지 않는다.

재현 가능한 `BoardArtworkLifecycleVerification`을 추가했다. `Tools > Match > 편집 보드 이미지 수명 검증`은 검사 소유 창을 연 채 현재 씬을 Play Mode로 전환했다가 돌아오므로 편집 모드에서 실행한다. 수정 전에는 종료 후 자동 복구가 시간 초과로 실패했으며 증거는 `Logs/BoardArtworkLifecycleVerification/red-results.txt`에 보존했다. 최종 결과는 같은 폴더의 `results.txt`를 따른다. 검사는 원본 레벨을 수정하지 않고 검사 창만 닫는다.

최종 5개 검사 PASS: 최초 표시, Play 진입 후 표시, 종료 후 자동 복구, 같은 레벨 재지정, 원본 보존. 사용자 편집 창에서도 고정 블록 25칸의 이미지와 바닥을 직접 확인했으며 `Logs/BoardArtworkLifecycleVerification/editor-fixed.jpg`에 저장했다. 임시 상태 수집 스크립트는 제거했다.

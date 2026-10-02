# 10단계 소리 Implementation Plan

> 실행 스킬: `superpowers:executing-plans`로 아래 4개 작업을 순서대로 직접 수행한다. 자동 커밋하지 않는다.

**목표:** 실제 화면 연출에 짧은 효과음을 연결한다.

**구조:** 게임 세션의 기존 표시 전이와 재생 시간을 소비하는 소리 표시기를 둔다. 규칙 결과를 재실행하지 않으며 시간 예약·중복 제한은 순수 모델에서 검사하고 Unity 재생 객체·클립은 씬이 소유한다.

**기술:** Unity 6000.3.10f1, C#, 기존 UniTask·uGUI·SpriteRenderer, AudioClip/AudioSource. 새 패키지는 추가하지 않는다.

**명세:** [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-10-audio-goal.md).

상태: 작업 1~4 구현·통합 검증 및 최종 리뷰 수정 완료 (2026-10-01). 사용자의 10단계 목표 실행 요청에 따라 착수했다. [실행 기록](../../../Verification/MoonRabbitJunkyard/WorldGameScreen/stage-10-progress.md).

## 공통 제약

목표 문서의 보존·제외·수치 기준을 모든 작업에 적용한다. 빌드·콘텐츠 빌드·자동 커밋·푸시는 실행하지 않는다. 계획 작성 이후 9단계 Editor 검증·최종 리뷰 수정을 완료했다. 실행 직전 실제 코드와 최신 완료 기록을 다시 확인하며 새 미완료가 있으면 먼저 해결한다. 기존 9단계 변경을 임의로 복원하지 않는다.

진동 기능·설정·플랫폼 호출은 추가하지 않는다. 교환·섞기 아이콘의 256×256 투명 이미지와 기존 GUID 연결을 보존한다. 10단계 음원과 UI 아이콘은 별개이며 음원 연결을 위해 아이템 바·전체 게임 씬을 재생성하지 않는다.

## 최종 리뷰에서 확인할 범위

- 한 프레임에 여러 타격·착지 시각을 통과해도 실제 요청은 한 번 소비하고 중첩 제한을 지킨다.
- 드론의 최신 상승·대기·선회·돌파에서 고정 예상 시간으로 타격음을 앞당기지 않는다.
- 준비 취소·파괴 후 늦은 완료가 이전 세션의 소리를 재생하거나 클립을 다시 소유하지 않는다.
- pause와 앱 백그라운드 처리를 구분하고 복귀 시 지난 미션·결과음을 재발행하지 않는다.
- 실제 청취·플랫폼 차이는 수행 증거가 있는 범위만 판단하며 무음 검사로 음질을 확정하지 않는다.

## 변경 위치와 책임

아래 경로는 `Assets/Scripts/Features/GameScreen/` 기준이며 신규 파일은 실행 전 다시 중복 책임을 확인한다.

| 파일 | 책임 |
| --- | --- |
| `Runtime/Feedback/PuzzleFeedbackCue.cs` (신규) | 의미별 cue enum, 시간·종류·우선순위 |
| `Runtime/Feedback/PuzzleFeedbackSchedule.cs` (신규) | 표시 시간 예약, 일회성 소비, 0.06초 제한, Clear |
| `Runtime/Feedback/PuzzleAudioPlayback.cs` (신규) | 합성 클립 한 번 생성, 8개 음성 재사용, pause·Stop·반환 |
| `Runtime/Session/PuzzleGameSession.Feedback.cs` (필요 시 신규) | 기존 교환·제거·파워·정착·진행·결과의 표시 전이를 cue로 연결 |
| `Runtime/Session/PuzzleGameSession.cs`, `.Presentation.cs`, `.Controls.cs`, `.Progress.cs` | 기존 재생 시간·pause·초기화·다시하기·실패·취소 경계 연결 |
| `Runtime/World/PuzzlePowerPlayback.cs`, `PuzzleBoardSettlementPlayback.cs` | 기존 실제 돌파·타격·착지 시간 제공. 표시 경로·속도는 유지 |
| `Editor/PuzzleAudioFeedbackAssets.cs` (필요 시 신규) | 게임용 재생 프리팹 생성 및 기존 세션 연결만 수행, 전체 씬 재생성 금지 |
| `Editor/Tests/PuzzleAudioFeedbackVerification.cs` 및 책임별 partial | 순수 cue·실제 씬·수명·상태 동등성 검사 |

게임 프리팹은 `Assets/Prefabs/Game/Puzzle/Feedback/PuzzleAudioFeedback.prefab`로 분리한다. Inspector 활성값을 기존 세션에서 연결하며 UI 설정 프리팹은 만들지 않는다.

## 우선 검토할 실패 조건

| 조건 | 기대 동작 | 담당 |
| --- | --- | --- |
| 저프레임으로 여러 표시 경계를 한 번에 지남 | 각 실제 cue를 한 번 소비하되 밀집 제한 적용 | 1·2 |
| 드론 개수·조합에 따라 비행 시간표가 변경됨 | 고정 대기시간 대신 실제 돌파·타격 시간을 사용 | 2 |
| 매 프레임 HUD Refresh 및 초기 목표 완료 | 소리 중복 없음 | 2 |
| 8음성 포화 중 최종 결과 도착 | 결과음 우선, 화면 대기·풀 확장 없음 | 1 |
| pause·백그라운드·취소 후 재시작 | 오래된 예약·클립·콜백 재생 없음 | 3·4 |

## Task 1 — 검증용 음원과 재사용 재생기

**인터페이스:** `PuzzleFeedbackCueKind`는 Swap, InvalidSwap, Match, Rocket, Bomb, DroneDive, DroneHit, Magnet, Landing, MissionArrival, MissionComplete, Start, Win, Lose를 제공한다. `PuzzleFeedbackSchedule.Schedule(PuzzleFeedbackCueKind kind, float at)`, `Tick(float deltaTime)`, `Clear()`를 만들고 Tick에서 이번 프레임에 허용된 cue만 반환한다. 재생기 `Play(PuzzleFeedbackCueKind kind)`, `SetPaused(bool paused)`, `StopAndClear()`를 제공한다. 순수 모델에는 Unity 오브젝트가 없다.

- [x] 같은 종류 at=0/0.03/0.07을 예약해 첫째·셋째만 허용, 큰 deltaTime에서도 중복 소비 없음, Clear 이후 남은 요청 없음을 검사한다. 신규 검사 `GameScreen.Editor.PuzzleAudioFeedbackVerification.Data`를 작성해 구현 전 실패를 기록한다.
- [x] 목표의 합성·음량·8개 재사용·우선순위 기준으로 최소 구현한다. cue별 음색은 주파수/길이/감쇠로 구분하고 선택값을 검증 기록에 적는다. 음원을 프레임마다 만들거나 전역 영구 소유하지 않는다.
- [x] 9개 이상 동시 요청, 동일 종류 반복, 결과음 도착, pause/resume, StopAndClear 후 재사용을 검사한다. 활성 음성≤8, 클립 생성 수 고정, 반환 후 소유 참조 0을 확인한다.
- [x] Data를 실행해 PASS/FAIL 수·종료 코드·로그를 기록한다. Data 진입점은 빌드 없이 종료하도록 구현한다.

## Task 2 — 실제 표시 전이와 소리 연결

**소비:** 작업 1의 Schedule/Play, 기존 파워 표시 시간표·실제 드론 재생 시간·정착 착지 시간, `ProgressFeedback`의 실제 도착·완료 전이와 `ResultReady`.

- [x] 차단 입력·무효 교환·빈 변화에서 예상 cue 수를 명시한 검사부터 작성한다. 실제 유효 교환은 Swap 1회, 실제 무효 복귀는 InvalidSwap 1회, 차단 입력은 0회다.
- [x] 기존 표시 단계에 소리 예약을 연결한다. 드론은 기존 `PuzzleEffectTimeline`의 기초 예상 시간만 믿지 않고 최신 `PuzzlePowerPlayback`이 조정한 돌파·타격 시간으로 연결한다. 실제 타격 전 DroneHit이 발생하면 실패하도록 검사한다.
- [x] 착지 기록은 같은 표시 시각에 묶는다. 미션 도착·완료는 양의 표시 증가 및 첫 목표 달성에만 연결한다. 같은 도착에서 완료했으면 MissionComplete 1회, MissionArrival 0회다.
- [x] Start/Win/Lose는 전이 진입 시 한 번만 예약한다. Refresh 10회에도 요청 수가 늘지 않고, 라스트팡·수집 정리 전 결과 cue는 0회여야 한다.
- [x] 실제 씬 검사 `GameScreen.Editor.PuzzleAudioFeedbackVerification.RunScene`을 작성한다. 표시 경계 직전/직후·낮은 프레임·단독 4파워·10조합·착지·회수·미션·승패를 확인한다. 화면 녹화/청취가 가능하면 별도 실적을 남긴다.

## Task 3 — 중단·수명 경계

**소비:** 작업 1의 cue·예약·재생기와 기존 세션 중단 경계.

- [x] 세션 pause·다시하기·실패·취소·씬 종료에 재생기와 예약을 연결한다. pause에서는 신규 cue를 소비하지 않고 재생 음성을 유지하며, reset에서는 즉시 중단·기록 초기화한다.
- [x] `OnApplicationPause(true)`에서 음성을 중단하고 기존 예약을 버린다. 복귀 시 미션/결과 상태를 읽었다는 이유만으로 지난 cue를 재발행하지 않는다. 게임의 기존 앱 중단 정책은 변경하지 않는다.
- [x] 준비 중 취소·로딩 오류·다시하기 5회·오브젝트 파괴 후 늦은 콜백·씬 재진입을 검사한다. 음성/클립/예약 소유 수와 세션 입력 상태를 확인한다.

## Task 4 — 실제 플레이 검수와 인계

- [x] Asset/MemoryPack 각각 실제 씬으로 시작한다. 대표 행동의 최종 보드·미션·이동 수·공급·난수·승패를 직접 실행기와 비교한다. 소리 설정 on/off로 결과가 달라지면 실패다.
- [x] 현재 영향받는 `PuzzleSwapAnimationVerification`, `PuzzleSettlementAnimationVerification`, `PuzzlePowerAnimationVerification`, `PuzzleProgressFeedbackVerification` 및 Editor 실행 검사를 선정해 빌드 없는 진입점만 실행한다. 이전 검사 수를 새 실적으로 합산하지 않는다.
- [x] 가로/세로·pause/resume·반복 플레이에서 청취 가능한 환경이면 음량·밀집 타격·결과음 명료도를 확인한다. 실제로 듣지 못했으면 청감 항목은 미검증으로 남긴다.
- [x] `Docs/Verification/MoonRabbitJunkyard/WorldGameScreen/stage-10-progress.md`에 완료 조건 12개별 증거, 실행 진입점·결과·한계·보존 감사·클립 소유권을 기록한다. `Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-10-audio-usage.md`에 켜기/끄기·검증용 음원·pause·실기기 한계를 안내하고 목차를 갱신한다.

실행 명령 예시는 새 검사 진입점 작성 후 Unity Editor에서 `-batchmode -projectPath C:/Projects/Git/ServeredMeridian -executeMethod GameScreen.Editor.PuzzleAudioFeedbackVerification.Data -logFile C:/Projects/Git/ServeredMeridian/Logs/Stage10/data.log`다. 실행 전에 내부 빌드 호출이 없음을 확인하고 사용자 에디터가 프로젝트를 점유하면 강제 종료하지 않는다. 실제 검사 수와 청감·실기기 실적은 수행 후에만 작성한다.



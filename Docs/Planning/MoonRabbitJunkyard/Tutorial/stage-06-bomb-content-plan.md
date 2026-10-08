# 튜토리얼 후속 6단계 — 3레벨 달 폭탄 콘텐츠

> 실행 방식: `superpowers:executing-plans`로 이 세션에서 직접 작업 묶음별 진행한다. 하위 에이전트·자동 커밋을 사용하지 않는다. 현재는 계획만 작성했다.

상태: 완료. 2026-10-07. 최종 근거와 검증 범위·제약은 stage-06-progress.md를 따른다.

목표: 실제 3레벨의 전체 9×9 보드에서 ㄱ 모양 5매칭으로 달 폭탄을 생성하고 인접 교환으로 주변 3×3 효과를 체험한 뒤 같은 보드에서 자유 플레이를 이어간다.

구조: 완료된 공통 데이터·등록 처리기·진행 엔진·실제 퍼즐 실행·안내 UI·레벨별 기록을 재사용한다. 콘텐츠와 외부 검사를 추가하며 폭탄 전용 진행 엔진이나 중앙 분기를 추가하지 않는다.

기술: Unity6000.3.10f1, 기존 C#/uGUI/UniTask/MemoryPack/Addressables, schema5 제작 데이터, 50레벨 구간 팩.

기획: [게임 규칙](../../../Contents/MoonRabbitJunkyard/04_게임규칙.md) · [튜토리얼 기획](../../../Contents/MoonRabbitJunkyard/13_레벨튜토리얼.md) · [통합 가이드](integration-guideline.md) · [5단계 결과](../../../Verification/MoonRabbitJunkyard/Tutorial/stage-05-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/Tutorial/stage-06-bomb-content-goal.md) · [실행문](../../../Commands/MoonRabbitJunkyard/Tutorial/stage-06-command.md).

## 전제와 확정 범위

- 3레벨은 ㄱ 매칭을 대표로 직접 안내한다. ㄴ/T도 유효하지만 별도 강제 안내는 추가하지 않는다. 단독 달 폭탄은 주변 3×3이며 폭탄+폭탄 5×5 조합을 이번 안내에 혼합하지 않는다.
- 설명 → 지정 일반 교환으로 ㄱ 5매칭 생성 → 생성 결과 설명 → 새 달 폭탄 인접 교환 발동 → 종료 설명의 5단계다. 두 교환은 각각 이동 1회를 소비한다. 결과 조건은 `Generated power.bomb`와 `Activated power.bomb`다.
- 기본 전체 9×9 활성 보드, 장애물 없는 색상 수집 미션. 현재 Level_02의 일반 색상/미션/이동 설정을 초기 기준으로 삼고 튜토리얼 배치·시드·공급은 새 후보로 작성한다. 전체 밸런스 조정은 하지 않는다.
- 초기 후보 시드는 12345다. 정확한 교환/생성/발동 좌표와 고정 공급은 실제 조회·실행·재생으로 먼저 확정한다. 현재 2레벨의 고정 공급/좌표를 그대로 복사해 성공을 추정하지 않는다.
- 중앙의 ㄱ 패턴 하나로 폭탄 하나를 생성하고 발동 칸의 3×3이 모두 보드 안에 오게 한다. 낙하·연쇄 후 폭탄과 교환 상대가 유지되어야 한다. 안내 중 모든 미션이 끝나지 않아야 한다.
- 기존 1/2레벨, 모든 기존 구간 레벨, GUID·현재 HEAD/WIP를 보존한다. 기존 3레벨이 발견되면 먼저 현재 데이터를 평가하며 소유하지 않은 데이터/메타를 덮어쓰지 않는다.
- 이번 단계 실행 시 신규 Level_03/메타와 해당 1~50 팩만 콘텐츠 변경을 허용한다. 문서 작성만으로는 저장하지 않는다.

## 책임과 파일

| 경로 | 책임 |
|---|---|
| Assets/Data/Levels/Level_03.asset 및 .meta | 신규 전체 9×9 출시 데이터와 고정 안내/공급 |
| Assets/Data/LevelPacks/levels-000001.bytes | 모든 기존 구간 레벨을 포함한 3레벨 추가 |
| Tests/Editor/Features/Tutorial/TutorialBombLevelVerification.cs 및 .meta | 메모리 후보/데이터/팩, Preview·Apply·Run |
| Tests/Editor/Features/Tutorial/TutorialBombLevelVerification.Play.cs 및 .meta | 실제 에디터 선택·게임 UI/입력·모드·기록·전환 |
| Docs/Verification/MoonRabbitJunkyard/Tutorial/stage-06-progress.md | 변경·실제 검사 근거·제약 |

소비 계약: 기존 `StartingBoardBuilder.Build`, `ActionQuery.Swap`, `BoardActionExecutor`, `LevelTutorialReplayValidator.Validate`, `LevelAssetOperations.CreateElementAtPath`, `LevelPackBuild.CreatePackBytes(IEnumerable<LevelDefinition>)`, `LevelPackCodec.Snapshot(LevelDefinition)` 및 현재 게임/에디터 진입 경로. 신규 공개 검사 엔트리는 `public static void Preview()`, `Apply()`, `Run()`이며 각각 exit 0/1과 Logs/Tutorial/Stage06 결과를 남긴다. 구현 전 현재 시그니처/호출부를 확인한다.

## A. 후보와 실제 재생 증명

- [x] 기존 HEAD/WIP·레벨1/2·모든 구간 레벨·팩·GUID·관련 과거 검사 출력 해시를 보관한다. 기존 3레벨 존재를 확인한다.
- [x] Run에 실제 3레벨/5단계/팩 검사부터 작성하고 부재 시 실패를 확인한다. 부재 검사를 위해 기존 사용자 에셋을 삭제하지 않는다.
- [x] Preview는 저장하지 않는 전체 9×9 후보를 구성한다. 실제 조회에서 의도한 ㄱ 5칸/폭탄 생성 위치를 확인하고 실제 생성 결정 및 보드 개수가 각각 1임을 검사한다.
- [x] 고정 공급으로 생성→낙하/연쇄→후속 발동을 실제 재생한다. 후속 교환 상대와 폭탄 유지, 두 교환 이동 차감, 실제 미션 기록 집계를 확인하고 소비량+필요한 여유만 저장한다.
- [x] 임의 교환/제자리 발동 거절, 공급 누락/부족/무작위 대체 거절, 논리 완료 뒤 표시 미완료 단계 유지, 마지막 설명 뒤 남은 미션·일반 공급 복귀를 검사한다.

명령: `& Tools/Testing/ProjectTests.ps1 -Action Run -Method TutorialBombLevelVerification.Preview`.

통과 기준: 실제 재생 오류 0, 정확히 폭탄 하나, 지정 단독 발동의 3×3 실제 범위, 후속 대상 유지. 실패한 후보는 데이터에서 수정하고 특별 엔진 처리를 추가하지 않는다.

## B. 출시 저장·팩·게임 화면

- [x] Apply는 검증된 후보만 현재 schema5/요소 카탈로그 제작 경로로 저장한다. 신규 GUID를 기록하고 기존 에셋/메타는 덮어쓰지 않는다.
- [x] 모든 기존 1~50 구간 레벨을 수집하여 CreatePackBytes로 해당 팩만 갱신한다. 다른 팩/ECPK/주소/제작 SO 배포 제외를 유지한다.
- [x] 저장 재로드 및 Asset/팩 스냅샷 동등, 기존 모든 구간 레벨 논리/GUID 보존과 새 SO의 런타임 직접 참조 부재를 확인한다.
- [x] 실제 LevelEditorWindow에서 3레벨 선택→Asset/MemoryPack 게임 플레이로 진입한다. 실제 다음 버튼과 게임 포인터로 5단계 진행, 생성 이미지·발동 3×3·미션·표시 완료 및 자유 플레이 일반 교환을 확인한다.
- [x] 1280×720/720×1280에서 강조·손가락·말풍선이 지정 두 칸을 가리지 않고 장식이 입력을 막지 않는지 좌표 검사 및 캡처로 확인한다. 마지막 설명 전후 같은 상태 객체·보드·이동·미션·난수를 유지한다.

명령: `& Tools/Testing/ProjectTests.ps1 -Action Run -Method TutorialBombLevelVerification.Apply`, 이후 같은 도구의 `-Method TutorialBombLevelVerification.Run`.

통과 기준: 새 출시3/팩 및 실제 Asset/팩 플레이 동등. 폭발 범위에 뒤따르는 연쇄 효과는 단독 폭탄 직접 3×3과 구분해 검사한다.

## C. 기록·전환·회귀·인계

- [x] 자동/항상/실행 안 함, 중단 새 세션 첫 단계 재현, 완료 1회·자동 생략, 레벨1/2/3 기록 분리를 확인한다. 시험 기록만 사용하고 실제 플레이어 키/이전 시험 값을 보존한다.
- [x] 실제 출시 팩 2→3 전환에서 이전 결과/표시/늦은 다음 신호가 신규 보드·단계·기록을 바꾸지 않음을 확인한다. 전환을 시험 인스턴스 미션 완료로 유도했다면 승리 밸런스 검사와 구분한다.
- [x] 새 Run과 TutorialRocketLevelVerification.Run, TutorialGuidanceVerification.Run, TutorialGameIntegrationVerification.Run, Tutorial.Editor.LevelTutorialDataVerification.Run, GameScreen.Editor.PuzzleGameplayVerification.Run, PuzzlePopupVerification.RunScene/RunRestore, Levels.Editor.BoardActionVerification.Data/PowerEffectVerification.Data 관련 회귀를 실행한다. 명시된 Popup 메서드에는 GameScreen.Editor 접두사를 사용한다.
- [x] 폭탄 직접 효과/표시 회귀는 현재 검사 메서드를 읽고 실제 범위를 확인한 뒤 실행한다. 5단계에서 확인한 기존 드론 시간 종합 실패를 폭탄 통과로 숨기지 않고 따로 기록한다. 관련 실패만 최소 수정하며 관계없는 드론 리팩토링은 하지 않는다.
- [x] 결과를 Stage06에 보관하고 과거 출력 원문을 복원한다. ProjectTests Detach/Status 후 테스트 연결이 없는 native Unity6000.3.10f1 batchmode -quit로 게임 소스만 컴파일한다. ProjectTests Compile은 테스트를 연결하므로 단독 컴파일 근거로 쓰지 않는다.
- [x] 소유 임시 파일/기록·화면 크기 복원, 기존 해시/HEAD/WIP 및 승인 diff를 감사한다. 완료 보고·진행 문서와 다음 4레벨 수거 드론의 계획·목표·복사용 실행문을 작성하되 다음 구현은 시작하지 않는다.

## 검토 초점과 제한

1. 시작부터 이미 완성된 매칭 — A의 실제 시작 검사에서 거절.
2. 다른 패턴이 우선해 폭탄 대신 다른 파워 생성 — A의 실제 매칭/생성 결정과 개수 검사.
3. 낙하/연쇄가 생성 폭탄이나 상대를 없앰 — A의 연속 재생에서 거절.
4. 폭탄 단독/조합/추가 연쇄 범위 혼동 — B의 직접 효과 3×3과 후속 결과 구분.
5. 완료 기록/이전 세션 신호 전이 — C의 실제 모드/재진입/2→3 검사.

빌드·번들·커밋·푸시·하위 에이전트·사용자 Unity 종료·임의 씬 저장 금지. 새 이미지, 다른 출시 레벨, 전체 밸런스/해금/인벤토리/계정/매뉴얼/고물탑은 제외한다. 실제 기기 미검증은 보고한다. 직접 재현된 관련 결함만 최소 수정한다.

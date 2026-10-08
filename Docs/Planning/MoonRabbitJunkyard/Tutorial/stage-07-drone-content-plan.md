# 튜토리얼 후속 7단계 — 4레벨 수거 드론 콘텐츠

> 실행 방식: `superpowers:executing-plans`로 이 세션에서 직접 작업 묶음별 진행한다. 하위 에이전트·자동 커밋은 사용하지 않는다. 실제 구현과 검증을 완료했다.

상태: 완료. 2026-10-07. 실제 결과와 기존 실패/제약은 stage-07-progress.md를 따른다.

목표: 실제4레벨 전체9×9에서 2×2 매칭으로 수거 드론 하나를 생성하고 인접 교환으로 발동한 뒤 같은 보드로 자유 플레이를 이어간다.

구조: 기존 제작/카탈로그·등록 처리기·진행 엔진·실제 실행·드론 표적 선택/비행·안내·기록을 재사용한다. 드론별 튜토리얼 분기나 새 표적 선택기를 만들지 않는다.

기술: Unity6000.3.10f1, 기존 C#/uGUI/UniTask/MemoryPack/Addressables, schema5, 50레벨 구간 저장.

기획: [게임 규칙](../../../Contents/MoonRabbitJunkyard/04_게임규칙.md) · [튜토리얼 기획](../../../Contents/MoonRabbitJunkyard/13_레벨튜토리얼.md) · [통합 가이드](integration-guideline.md) · [6단계 결과](../../../Verification/MoonRabbitJunkyard/Tutorial/stage-06-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/Tutorial/stage-07-drone-content-goal.md) · [실행문](../../../Commands/MoonRabbitJunkyard/Tutorial/stage-07-command.md).

## 전제와 결정

- 설명→지정2×2생성→생성 설명→인접 교환 발동→종료 설명의5단계다. 두 교환은 각각 이동1을 소비한다. 결과 조건은 `Generated power.drone`와 `Activated power.drone`다.
- 소개는 드론 하나의 생성/단독 발동이다. 파워 조합·추가 드론 소개·장애물/새 밸런스는 포함하지 않는다.
- 기본 전체9×9 활성 보드/장애물 없는 색상 수집 미션. 현재 Level_03의 일반 색상·미션·이동·schema5 ElementSupply를 초기 기준으로 가져오고, 고정 배치/시드/튜토리얼 공급은 새 후보로 작성한다. legacy Supply가 비어 있다고 생성구를 누락하지 않는다.
- 최초 후보 시드는12345. 생성 좌표와 낙하 후 좌표/교환 상대를 실제 조회·재생으로 확정한다. 시드/공급/좌표를 이전 콘텐츠에서 복사해 성공을 추정하지 않는다.
- 드론 발동 위치의 직접 `+` 5칸과 이후 추가 표적1개를 구분한다. 중앙 드론 소모·주변 피해·비행 후 추가 제거를 실제 결과/표적 기록으로 확인한다. 이후 연쇄는 별도로 집계한다.
- 추가 표적은 현재 미션 우선 선택과 실제 드론 비행을 그대로 사용한다. 떠오름→잠시 호버→돌진을 유지하고, 도착/재탐색이 끝나기 전에 안내/기록을 앞당기지 않는다. 별도의 회전이나 시간 조정은 추가하지 않는다.
- 추가 표적을 미리 표시/강조하지 않는다. 설명 예시는 “2×2로 맞추면 수거 드론이 생겨요.” / “옆 블록과 바꾸면 가까운 칸을 정리하고 다른 목표로 날아가요.” 실제 규칙과 일치시키며 타겟을 고정하는 문구는 피한다.
- 기존1/2/3·모든 구간 레벨·GUID·현재 HEAD/WIP를 보존한다. 기존4레벨 발견 시 현재 데이터를 먼저 평가하고 소유하지 않은 데이터/메타를 덮어쓰지 않는다.
- 실행 지시 후 신규4레벨/메타와 동일1~50 팩만 콘텐츠 변경을 허용한다. 지금은 저장하지 않는다.

## 책임과 파일

| 경로 | 책임 |
|---|---|
| Assets/Data/Levels/Level_04.asset 및 .meta | 신규 전체9×9 출시 콘텐츠/5단계/고정 공급 |
| Assets/Data/LevelPacks/levels-000001.bytes | 기존 모든 구간 레벨을 유지하는4레벨 추가 |
| Tests/Editor/Features/Tutorial/TutorialDroneLevelVerification.cs 및 .meta | 후보·데이터·팩·실제 표적/효과 검사 |
| Tests/Editor/Features/Tutorial/TutorialDroneLevelVerification.Play.cs 및 .meta | 실제 에디터/UI/포인터·모드·기록·3→4 경계 |
| Docs/Verification/MoonRabbitJunkyard/Tutorial/stage-07-progress.md | 변경·실제 근거·제약 보고 |

검사 계약: `public static void Preview()`, `Apply()`, `Run()`. 성공exit0/실패exit1, 결과Logs/Tutorial/Stage07. 기존 StartingBoardBuilder/ActionQuery/BoardActionExecutor/LevelTutorialReplayValidator, LevelAssetOperations.CreateElementAtPath, LevelPackBuild.CreatePackBytes(IEnumerable<LevelDefinition>), LevelPackCodec.Snapshot(LevelDefinition), 현재 표적/표시 기록 및 실제 게임 진입을 소비한다. 현재 타입/시그니처/호출부는 구현 전에 읽는다.

## A. 메모리 후보와 실제 재생

- [x] HEAD/WIP·기존1/2/3·모든 구간 레벨·팩·GUID·관련 과거 출력 해시를 확보하고4레벨 존재 여부를 확인한다.
- [x] 실제4레벨/5단계 검사부터 작성해 부재 시 실패를 확인한다. 기존 사용자 에셋을 삭제하여 부재 조건을 만들지 않는다.
- [x] Preview에서 저장하지 않는 전체9×9 후보를 구성한다. 실제 지정 교환의 정확한2×2/4칸 매칭과 선택 결정·Generated power.drone 및 생성 드론1개를 확인한다.
- [x] 낙하/연쇄 후 드론과 교환 상대가 유지되는 후보로 고정 공급 끝까지 재생한다. 실제 소비량과 필요한 여유만 남긴다. 부족/누락/무작위 대체·임의 교환·제자리 발동은 거절한다.
- [x] 단독 발동의 직접+5칸과 추가 표적 선택/착탄/실제 제거를 구분해 확인한다. 미션 집계·이동20→19→18·관련 논리/표시 완료 대기·일반 공급 복귀와 남은 미션을 검사한다. 새 선택기를 만들지 않는다.

명령: `& Tools/Testing/ProjectTests.ps1 -Action Run -Method TutorialDroneLevelVerification.Preview`.

통과 기준: 실제 재생 오류0, 생성 드론1개, 지정 후속 발동과 실제 드론 비행/추가 제거, 잘못된 후보 거절. 후보 실패는 배치/시드/공급에서 수정한다.

## B. 저장·팩·실제 플레이

- [x] Apply는 검증한 신규4만 현재 schema5/카탈로그 제작 경로로 저장한다. 신규 GUID를 기록하고 재실행/기존 데이터의 소유권을 확인한다.
- [x] 기존 모든1~50 구간 레벨을 포함하는 CreatePackBytes로 해당 팩만 갱신한다. 다른 팩/ECPK/주소/제작 SO 배포 제외를 유지한다.
- [x] Asset 재로드/팩 스냅샷 동등, 기존1/2/3 논리/해시/GUID 보존과 새 SO의 런타임 직접 참조 부재를 확인한다.
- [x] 실제 LevelEditorWindow4 선택→Asset/MemoryPack 게임 플레이, 실제 다음/보드 포인터로5단계를 진행한다. 두 화면 비율에서 투명 포커스/어둠 막·손가락·말풍선·입력 통과를 캡처·좌표 검사로 확인한다.
- [x] 실제 드론의 비행/호버/착탄과 수집 표시가 끝난 뒤 단계와 완료 기록이 진행되는지 확인한다. 표적의 사전 강조를 추가하지 않는다.
- [x] 마지막 설명 전후 같은 보드/상태 객체/미션/이동/난수와 일반 공급 복귀·정상 일반 교환을 확인하고 Asset/팩 최종 상태를 비교한다.

명령: 같은 도구의 `-Method TutorialDroneLevelVerification.Apply`, 이후 `-Method TutorialDroneLevelVerification.Run`.

## C. 기록·전환·회귀·인계

- [x] 자동/항상/실행 안 함, 중단 새 세션 첫 단계/고정 보드 재현, 완료1회/중복0/자동 생략 및1/2/3/4 기록 분리를 확인한다. 미완료 다른 레벨이 완료로 바뀌지 않아야 한다. 시험 기록/실제 플레이어 키/이전 값을 보존한다.
- [x] 실제 출시 팩3→4 전환에서 이전 결과/표시/늦은 다음이 신규 보드·단계·기록을 바꾸지 않음을 확인한다. 시험 인스턴스 미션을 강제 완료했다면 전환 경계와 실제 승리 밸런스를 구분한다.
- [x] 신규 Run, TutorialBombLevelVerification.Run/BombPresentation, TutorialRocketLevelVerification.Run, TutorialGuidanceVerification.Run, TutorialGameIntegrationVerification.Run, Tutorial.Editor.LevelTutorialDataVerification.Run, GameScreen.Editor.PuzzleGameplayVerification.Run 및 PuzzlePopupVerification.RunScene/RunRestore, Levels.Editor.BoardActionVerification.Data/PowerEffectVerification.Data 관련 회귀를 수행한다. Popup에는 GameScreen.Editor 접두사를 사용한다.
- [x] 현재 드론/표적/비행 검사를 읽고 관련 엔트리와 범위를 추가로 확인한다. 기존 종합 드론 시간 기대값 실패를 전체 통과로 숨기지 않는다. 이번 실행에서 직접 재현된 관련 결함만 최소 수정하고 근거를 남긴다. 임의의 비행 시간/궤도/미션 우선순위 변경은 하지 않는다.
- [x] 결과를 Stage07에 보관하고 과거 출력/시험 상태를 복원한다. Detach/Status 뒤 테스트 없는 native Unity6000.3.10f1 batchmode -quit 게임 소스 컴파일을 확인한다. ProjectTests Compile은 단독 컴파일을 대신하지 않는다.
- [x] 원본/해시/GUID/HEAD/WIP·소유 임시 파일·승인 diff를 감사하고 완료 보고를 남긴다. 완료 기준으로 다음5레벨 무지개 자석의 계획·목표·복사용 실행문만 작성한다.

## 검토 초점과 제한

1. 시작에 이미2×2/3매칭이 완성됨 — A의 실제 시작 검사에서 거절.
2. 생성 위치/낙하로 후속 교환이 사라짐 — A의 연속 실제 재생에서 거절.
3. 직접+5칸/표적/연쇄가 중복 집계됨 — A의 실제 기록과 미션 합계로 구분.
4. 논리는 끝났지만 드론이 아직 비행 중임 — B의 실제 표시 완료 대기 검사.
5. 이전 세션 신호/기록이 새 레벨에 섞임 — C의 모드·미완료 다른 레벨·3→4 검사.

빌드·번들·커밋·푸시·하위 에이전트·사용자 Unity 종료·임의 씬 저장 금지. 다른 출시 레벨·새 이미지·전체 밸런스/해금/인벤토리/계정/매뉴얼/고물탑은 제외한다. 실기기 미검증과 기존 네이티브 종료 경고는 실제 확인 범위와 함께 보고한다.

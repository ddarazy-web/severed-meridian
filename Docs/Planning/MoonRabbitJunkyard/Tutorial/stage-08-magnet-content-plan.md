# 튜토리얼 후속8단계 — 5레벨 무지개 자석 콘텐츠

상태: 재검토 대기. [조합형 튜토리얼 전환 기획](../../../Contents/MoonRabbitJunkyard/15_조합형튜토리얼.md)에 맞춰 진행 순서와 실행 내용을 조정한 뒤 사용한다. 아래는 이전 구조 기준의 계획이며5레벨 데이터는 아직 만들지 않았다.

[통합 가이드](integration-guideline.md) · [기획](../../../Contents/MoonRabbitJunkyard/13_레벨튜토리얼.md) · [규칙](../../../Contents/MoonRabbitJunkyard/04_게임규칙.md) · [7단계 결과](../../../Verification/MoonRabbitJunkyard/Tutorial/stage-07-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/Tutorial/stage-08-magnet-content-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/Tutorial/stage-08-command.md).

## 목표와 범위

기존 공통 튜토리얼과 요소 카탈로그·실제 실행/표시를 사용하여 전체9×9 실제5레벨을 만든다. 설명→지정 직선5매칭으로 무지개 자석 하나 생성→생성 설명→일반 색 블록과 인접 교환 발동→종료 설명의5단계다. 마지막 설명 뒤 같은 보드에서 자유 플레이한다. 두 교환은 각각 이동1을 차감한다.

결과 조건은 `Generated power.magnet`, `Activated power.magnet`이다. 실제 교환 상대 색을 대상으로 하는 기존 자석 규칙을 사용한다. 단독 발동 소개만 포함하며 자석 조합·추가 출시 레벨·새 엔진·표적 선택기·이미지·전체 밸런스는 제외한다.

기본 기준은 Level_04의9×9·색 수집 미션·이동20·schema5 ElementSupply다. 배치/시드/튜토리얼 공급/좌표는 실제 재생으로 새로 확정한다. 색상 전체 제거로 추가 연쇄가 생길 수 있으므로 직접 자석 효과와 후속 연쇄를 분리하고, 완료 미션 때문에 자유 플레이가 즉시 끝나지 않는 후보를 선택한다. 필요하면 신규5레벨의 미션 목표만 실제 결과에 근거해 조정하고 이유를 보고한다. 기존1~4레벨은 변경하지 않는다.

## A. 저장 없는 후보 재생

- [ ] 현재 HEAD/WIP·기존1~4/GUID·모든 구간 레벨·팩·과거 검사 출력 기준을 확보한다. 기존5 발견 시 삭제/덮어쓰기 없이 평가한다.
- [ ] 외부 Tests/Editor/Features/Tutorial에 TutorialMagnetLevelVerification.cs/.Play.cs와 메타를 작성한다. Preview/Apply/Run 계약과 실제5레벨 부재 실패를 확보한다.
- [ ] 초기 완성 매칭 없는81칸 후보에서 지정 교환으로 정확한 직선5매칭·선택 결정·자석1개를 실제 조회/실행으로 확인한다. 생성/낙하 후 위치와 상대를 확정한다.
- [ ] 고정 공급을 끝까지 재생하여 실제 소비량+필요한 여유만 남긴다. 공급 누락/부족/무작위 대체, 비지정 교환/제자리 발동을 거절한다.
- [ ] 교환 상대 색 선택, 직접 색 제거 범위/실제 제거/미션 기록을 후속 연쇄와 구분한다. 이동20→19→18·남은 미션·표시 완료 대기·일반 공급 복귀를 확인한다.

명령: `& Tools/Testing/ProjectTests.ps1 -Action Run -Method TutorialMagnetLevelVerification.Preview`.

## B. 저장과 실제 게임 화면

- [ ] 검증된 신규 Assets/Data/Levels/Level_05.asset/.meta만 제작 도구로 저장한다. 모든 기존1~50 구간 레벨을 포함하는 CreatePackBytes로 levels-000001.bytes만 갱신한다.
- [ ] Asset 재로드/팩 동등·기존1~4/다른 팩/ECPK/주소/제작 SO 배포 제외를 확인한다.
- [ ] 실제 LevelEditorWindow에서5 선택→Asset/MemoryPack 게임 플레이, 실제 Next/보드 포인터로5단계를 실행한다. 가로1280×720·세로720×1280에서 투명 포커스·주변 어둠·손가락·말풍선·입력 통과를 캡처/좌표로 확인한다.
- [ ] 자석 발동·제거·낙하·미션 수집 표시 중 단계/완료 기록을 앞당기지 않는다. 마지막 설명 전후 같은 상태/보드/미션/이동/난수·일반 조작 및 두 데이터 경로의 최종 상태 동등을 확인한다.

명령: 같은 도구의 `-Method TutorialMagnetLevelVerification.Apply`, 다음 `-Method TutorialMagnetLevelVerification.Run`.

## C. 기록과 회귀, 인계

- [ ] 자동/항상/실행 안 함·중단 새 세션 첫 단계 재현·완료1회/중복0/자동 생략·미완료 다른 레벨 포함1~5 기록 분리를 실제 실행한다. 플레이어 기록/시험 값/화면 크기를 복원한다.
- [ ] 실제 출시 팩4→5 전환에서 이전 결과/표시/늦은 다음을 격리한다. 시험 미션 강제 완료로 전환을 유도했다면 실제 승리 밸런스와 구분한다.
- [ ] TutorialDroneLevelVerification.Run, TutorialBombLevelVerification.Run/BombPresentation, TutorialRocketLevelVerification.Run, TutorialGuidanceVerification.Run, TutorialGameIntegrationVerification.Run, TutorialFocusVerification.Run, Tutorial.Editor.LevelTutorialDataVerification.Run, GameScreen.Editor.PuzzleGameplayVerification.Run 및 PuzzlePopupVerification.RunScene/RunRestore, Levels.Editor.BoardActionVerification.Data/PowerEffectVerification.Data를 실행한다. 현재 자석/색 선택 관련 검사도 읽고 적절한 엔트리를 추가한다.
- [ ] 기존 종합 드론 시간 기대값 실패는 별도 보고한다. 실제 재현한 직접 관련 결함만 최소 수정한다. 원래 출력은 백업·복원하고 테스트 해제 후 native Unity6000.3.10f1 게임 소스 단독 컴파일을 확인한다. ProjectTests Compile로 대체하지 않는다.
- [ ] 원본/GUID/HEAD/WIP·승인 diff·임시 상태를 감사한다. stage-08-progress.md에 실제 논리/UI/기록 근거와 제약을 보고한다. 다음 콘텐츠는 기획의6레벨 장애물·7레벨 아이템·9레벨 조합 등 현재 미구현 소개를 확인하여 우선순위에 맞는 한 단계의 계획·목표·복사용 실행문만 작성한다. 다음 출시 레벨을 임의 생성하지 않는다.

빌드·번들·커밋·푸시·하위 에이전트·사용자 Unity 종료·임의 씬 저장 금지. 계정·인벤토리·매뉴얼·고물탑은 제외한다. 실행 전까지5레벨 구현을 시작하지 않는다.

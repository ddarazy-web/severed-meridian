# EF-02 — 드론 선택·예약·효과 타임라인 기준 확보 계획

> 실행 담당: `superpowers:executing-plans`로 현재 단계만 수행한다. 병렬 에이전트는 별도 요청 시에만 사용한다.

**목표:** 현재 드론 선택·난수·예약·재선정과 효과 기록→표시 시간의 계약을 재현 가능한 기준으로 확보한다.

**구조:** 기존 목표/미션/파워/타임라인 검사를 재사용하고 누락만 보완한다. EF-01 피해 기준을 사용하며 새 정책이나 비행을 구현하지 않는다.

**기술:** 현재 Unity/C#, 기존 Editor 검사·JsonUtility. 새 패키지/asmdef 없음.

**설계:** [통합 가이드라인](integration-guideline.md) · [전체 설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-01 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-01-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-02-drone-baseline-goal.md).

상태: 완료. 2026-10-04, 352 PASS/0 FAIL, 관찰 16건. [검증 기록과 실제 구현 차이](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-02-progress.md).

## 파일·범위·검토 위험

읽기: `PowerBlocks/Runtime/DroneTargetManager.cs`, `PowerEffectResolution.cs`, `PowerPresentationTrace.cs`; `Missions/Runtime/MissionProgressRules.cs`; `GameScreen/Runtime/World/PuzzleEffectTimeline.cs`, `PuzzlePowerPlayback.cs`, `PuzzlePowerPlayback.Clips.cs`와 표시 상태 적용 호출부. 경로의 기준은 `Assets/Scripts/Features/`다.

검사: `PowerBlocks/Editor/Tests/TargetPowerVerification.cs`, `.Supplemental.cs`; `Obstacles/Editor/Tests/FixedObstacleVerification.Edges.cs`의 예약 사례; `GameScreen/Editor/Tests/PuzzlePowerAnimationVerification.cs`의 Data/타임라인 사례. 필요할 때만 같은 PowerBlocks 검사 폴더에 `TargetPowerVerification.Baseline.cs`/`.meta` 또는 기존 partial 관찰 기록을 추가한다. 타임라인 누락 검사는 같은 GameScreen 검사 계열에 둔다.

결과: `Logs/ElementFramework/Stage02/` 및 `Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-02-progress.md`.

생산 코드·표현·원본 에셋·난수·저장 포맷은 변경하지 않는다. 빌드·임의 커밋·사용자 Editor 종료·씬 저장 금지. 실행 전 경로/프로세스/종료 API를 확인하고 데이터 검사로 새 연출 완료를 주장하지 않는다.

검토 위험은 조회의 상태/난수 변경, 노출 색/덮개/일반 대체 우선순위 혼동, 2×2/충전 과다 예약, 캐시 갱신/자기 예약 해제 누락, 최종 계산 상태를 표시/실시간 재선정으로 오인하는 경우다. 각 위험은 아래 작업의 실제 사례로 확인한다.

**인계 계약:** 시드/레벨/행동·조회 순서와 후보 좌표/미션/본체/범위 기여, 예약·해제·재선정, 규칙 난수 전후, 타격 순서·표시 시작/착탄 시간을 기록한다. 예약을 봇 공개 관찰에 추가하지 않는다. 정책 전환은 현재 기준을 보존하고 새 비행은 별도 의도적 변경으로 다룬다.

## 작업 1 — 계약과 검사 대응표

- [x] EF-01 결과와 현재 소스를 확인하고 작업 전 보존 목록을 만든다.
- [x] QueryArea/Reserve/Land·미션 기여·캐시/예약의 책임과 검색→선택→예약→재검증→피해 순서를 표로 남긴다.
- [x] 읽기 전용 후보 조회와 최종 선택의 난수 사용을 구분하고 현재 미션 필터/동률 선택 의미를 확인한다.
- [x] 효과 계산→PowerPresentationTrace→PuzzleEffectTimeline→Playback→보드/미션 표시의 소유권을 추적한다. `Retargeted`, `WaitForAttacks`와 요청된 돌진 중 중단·호버의 차이를 기록한다.
- [x] TargetPowerVerification.Data/Supplemental과 PuzzlePowerAnimationVerification.Data의 종료·저장·빌드/그래픽 의존성을 읽고 최소 안전 데이터 범위를 선정한다. Assets/UI/FinalChecks/Regression을 자동 포함하지 않는다.

## 작업 2 — 선택·예약의 누락만 보완

- [x] 남은 미션 우선/완료 미션 제외, 노출 색 직접 기여/덮개 대체, 유효 미션 부재 시 일반 대체를 확인한다.
- [x] 반복 조회의 상태/예약/난수 무변경, 단일 후보 무난수와 복수 후보 시드 재현성을 확인한다.
- [x] 여러 드론과 2×2/충전 목표의 예약 기여·과다 예약 방지를 확인한다. EF-01 피해 검사를 복제하지 않는다.
- [x] 목표 소실·다른 예약/보드/미션 변경 뒤 캐시 갱신·착탄 재선정·자기 예약 해제를 확인한다.
- [x] 범위형 드론의 조준 중심과 실제 기여를 기록한다. 미션 대상 칸만 조준해야 한다는 새 제약은 넣지 않는다.
- [x] 기존 사례가 없을 때만 메모리 fixture/검사 전용 기록을 추가한다. 런타임 API를 늘리지 않는다.

## 작업 3 — 효과 순서·실행·인계

- [x] 실제 효과 기록으로 타임라인을 생성하고 선행 대기/착탄·피해/미션 표시 순서를 확인한다. 생성으로 상태/목표/난수가 바뀌지 않아야 한다.
- [x] 3기 이하 상승·대기와 조합/다수 선회는 현재 동작으로 기록한다. 미구현인 모든 드론 통일/돌진 중 중단을 기준 PASS로 만들지 않는다.
- [x] 선정한 Editor 데이터 검사를 실행하고 종료 코드·FAIL/예외·새 결과 생성 시간을 확인한다.
- [x] 보존 비교/최종 diff 검토 후 진행 기록과 목표에 실제 결과를 반영한다.
- [x] 완료 보고에 현재 계약/타임라인 한계·검사/미검증을 포함한다. 완료 후 EF-03의 가장 작은 작업을 선정해 계획/목표/복사용 실행문을 작성한다. 기본 후보는 남은 층·발전기 피해 기준이며 결과에 따라 선행 작업을 조정하고 이유를 기록한다. EF-03은 구현하지 않는다.

완료 여부는 이 단계 목표의 실제 증거로 판단한다. 새 정책·비행 구현은 이후 관련 구간에서 수행한다.

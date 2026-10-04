# EF-04 — 발전기 충전·연결 기준 확보 결과

상태: **완료**, 2026-10-04(KST). EF-05 문서 준비 완료, 구현 미착수.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-04-generator-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-04-generator-baseline-goal.md) · [통합 가이드라인](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

## 변경과 실제 실행

Editor 전용 `Assets/Scripts/Features/Obstacles/Editor/Tests/GeneratorVerification.Baseline.cs`와 meta를 추가했다. 생산 코드·기존 검사 파일·원본 에셋·저장 포맷은 변경하지 않았다. 기존 Data가 EdgeChecks→AdditionalChecks까지 호출하므로 독립 Supplemental 진입점이나 기존 사례 복제는 추가하지 않았다. 새 Baseline은 실제 값 기록, 전선 표시 칸 타격, 직접 제거 후 실제 세 턴 충전 사례에 한정한다.

Unity 6000.3.10f1 별도 배치 Editor: **262 PASS / 0 FAIL**, 두 진입점 모두 종료 코드 0.

| 진입점 | PASS | 실행 KST | 증거: Logs/ElementFramework/Stage04 아래 |
| --- | ---: | --- | --- |
| Levels.Editor.GeneratorVerification.Data | 246 | 09:41:58~09:42:41 | data-results.txt, data.log, data-execution.json |
| Levels.Editor.GeneratorVerification.Baseline | 16 | 09:43:49~09:44:18 | baseline-results.txt, baseline.log, baseline-execution.json |

실제 관찰 **125건**은 generator-observations.jsonl에 저장했다. 각 행에 seed=12345, 메모리 레벨 JSON, 행동/타격 순서·턴, 실제 충전 배열·점유 본체·활성 연결 수·미션 진행을 기록했다. stateProperties/contextProperties/effectProperties는 기존 Snapshot의 읽기용 속성 문자열이며 그 내부가 JSON이라는 뜻은 아니다. contextProperties에 GeneratorRecord의 Charged/Activated/Disconnected/Retired 순서와 전후 충전 값이 포함된다. 관찰 108행의 충전/연결/미션 값을 파일에서 다시 확인했고, 연결·철거·전선 관찰 17행도 확인했다.

## 요구사항 대응표

| 요구사항 | 실행 근거 | 확인 결과 |
| --- | --- | --- |
| 충전 3~5, 연결 대상 4종 | Data + Baseline | 상자/고물회수캡슐(Safe)/색 잠금/금속 기둥(Appliance) 각각 3·4·5 충전 기준, 미완충 미션 0·본체 유지, 완충 미션 1·연결 대상과 발전기 전체 제거 |
| 동일 턴 본체 제한/다음 턴 | Data + 관찰 108행 | 본체 네 칸/반복 타격은 같은 턴 추가 충전 없음. NextTurn 후 1씩 다시 충전 |
| 벽·고정·실제 매칭 | AdditionalChecks | 벽은 인접 충전 차단, 직접 파워는 통과. 교환/낙하 금지. 외곽 여러 칸 매칭과 후속 파워도 한 턴 1충전 |
| 복수 발전기 | AdditionalChecks | 다른 본체는 각각 독립 1충전. 한 발전기 자동 철거가 다른 발전기의 연결·점유에 영향 없음 |
| 일부 연결 직접 제거 | EdgeChecks + Baseline | 연결 3→2, 발전기 충전 0 유지, 상자 미션 진행 1 |
| 남은 연결 완충 간접 제거 | EdgeChecks + Baseline | 실제 충전 0→1→2→3 뒤 연결 2→0. Appliance/Crate 미션 진행 [0,1]→[1,2], 점유 본체 없음 |
| 마지막 대상 직접 제거 | Data + Baseline | Appliance 9번 별도 타격 후 연결 1→0, 충전 0, 자동 철거 Retired 1회, 미션 1 |
| 간접 제거의 바닥/주변/후속 미션 | EdgeChecks + Baseline | 먼지 2와 주변 블록 보존. 제거 후 대기 타격의 미션 중복 증가 없음 |
| 전선 표시 칸 타격 | Baseline | (4,6) 일반 블록 타격은 충전 0·연결 1 유지, GeneratorRecord 0. 전선은 별도 피격 본체가 아님 |
| 조회·예약·실패 원자성 | Data/EdgeChecks/AdditionalChecks | 조회 무변경, 충전/직접·간접 예상 완료 중복 방지, 예약 소실 재선정, 미지원 반응 실패 시 충전·문맥 전체 보존 |

## 현재 책임·호출 순서

DamageReaction의 벽/인접 조건을 거쳐 ObstacleDamageRules.Query가 발전기를 GeneratorRules.Query에 위임한다. 이미 충전한 본체는 AlreadyDamaged로 응답한다. PowerEffectResolution의 Charge 처리가 GeneratorRules.Apply를 호출하고 효과에 ChargeBefore/After를 남긴다.

GeneratorRules.Apply는 턴 충전 등록→Charge 증가→Charged 기록→완충이면 Activated→활성 연결 대상 Remove→Disconnected 기록→발전기 Remove 순서다. ObstacleDamageRules.Remove는 점유가 살아 있을 때만 일반 장애물의 내구도·미션·점유를 제거한다. 발전기 자체는 해당 장애물 미션을 증가시키지 않는다. 바닥 층은 제거하지 않는다.

연결 대상의 직접 피해 제거는 ObstacleDamageRules.Apply→Remove→GeneratorRules.TargetRemoved 순서다. 남은 활성 연결이 없을 때 발전기 Remove와 Retired를 기록한다. 연결 정의는 지우지 않고 현재 점유에서 ActiveConnections를 파생한다. 따라서 사본과 원본의 연결 수명이 독립적이다. MissionProgressRules.Query는 현재 충전+1이 임계치에 도달하는지와 연결 대상의 미션 기여를 조회하며 실제 진행을 바꾸지 않는다.

## 보존·검토·한계

작업 시작 시 보호 파일 **1097개**(Scripts/Data/Prefabs/Scenes/Packages/ProjectSettings)의 SHA256을 기록하고 기존 파일 변경 0건을 확인했다. 신규 검사 2개 파일만 추가했다. GUID는 32자리이며 저장소 내 중복 없음. 기존 변경·원본 에셋·저장/enum/GUID·패키지·씬을 보존했다. 실제 로그에 FAIL/컴파일 오류/필수 예외 없음. diff 공백 검사 및 문서 링크 검사 통과.

executing-plans와 프로젝트 Unity 규칙에 따라 순서대로 수행했다. 이번 목표는 기존 계약의 특성 검사이므로 실패하는 생산 구현을 만들거나 RED를 꾸미지 않았다. 사용자 제약에 따라 기존 작업 경로에서 검사하며 커밋·워크트리 이관·원본 저장·사용자 Editor 종료·빌드를 하지 않았다. 병렬 에이전트 대신 추가 검사와 기록의 범위·예상 값·정리 경로를 별도 자체 검토했다.

Play Mode/실기기/시각적 전선·충전 표시 검사는 미실행이다. 데이터 규칙 기준 확보에 한정하며 새 프레임워크·정책·드론 비행은 미구현이다. 검증 범위에서 미해결 실패 없음.

## 다음 단계

곰팡이는 턴 종료와 제거 이력·난수·미션 목표 증가가 연결되어 있으므로 발전기와 분리한다. [EF-05 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-05-mold-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-05-mold-baseline-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-05-command.md)을 준비했다. EF-05 구현은 시작하지 않았다.

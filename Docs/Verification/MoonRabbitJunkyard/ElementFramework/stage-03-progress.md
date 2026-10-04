# EF-03 — 거미줄·먼지 피해 기준 확보 결과

상태: **완료**, 2026-10-04(KST). EF-04는 문서만 준비했다.

## 변경·실제 검증

생산 코드는 보존했다. Editor 전용 `LayerVerification.Baseline.cs`와 meta만 추가했다. 기존 Data/Supplemental을 재사용하고 실제 NextTurn(2), 낙하 출발·도착 먼지 잔류 및 실제 값 기록을 보완했다.

Unity 6000.3.10f1 별도 배치 Editor: **198 PASS / 0 FAIL**, 세 진입점 모두 종료 코드 0.

| 진입점 | PASS | 증거: Logs/ElementFramework/Stage03 아래 |
| --- | ---: | --- |
| Levels.Editor.LayerVerification.Data | 139 | data-results.txt, data.log, data-execution.json |
| Levels.Editor.LayerVerification.Supplemental | 40 | supplemental-results.txt, supplemental.log, supplemental-execution.json |
| Levels.Editor.LayerVerification.Baseline | 19 | baseline-results.txt, baseline-final.log, baseline-final-execution.json |

최종 Baseline은 06:56:50~06:57:10(KST) 실행했다. 초기 Baseline 실행도 통과했으며 변환 관찰을 추가하고 최종 검사했다. 관찰 13건은 observations.txt에 저장했다. 시드 12345, 입력·타격 순서, 실제 내용물/내구도/미션/효과/턴 문맥을 기록했다. Snapshot 출력은 읽기용 속성 문자열이며 JSON 형식이 아니다. 메모리 fixture만 사용하고 레벨을 저장하지 않았다.

## 대응표와 현재 계약

| 범위 | 근거 | 확인한 결과 |
| --- | --- | --- |
| 거미줄 1~3/일반·로켓·폭탄·드론·자석 | Data | 벗기는 타격은 내용물·먼지를 보존. 완전 제거 때만 거미줄 미션 1. 동일 턴 추가 덮개 피해 없음 |
| 실제 다음 턴 | Baseline | NextTurn(2) 후 거미줄·먼지 재피해 허용. 이미 제거된 먼지는 0 유지 |
| 먼지 1~3 소비 | Data/Baseline | 일반 소비 때 1 감소. 일반 재유입·재소비는 색 미션을 증가시키지만 같은 턴 먼지는 추가 감소하지 않음 |
| 파워/장애물 접촉 | Data/Supplemental | 파워 발동·상자 제거만으로 먼지 감소 없음 |
| 매칭/파워 생성·조합 | Data | 덮개 칸 참여와 실제 소비 분리. 10개 조합의 덮개 피해 제한과 내용물 보존 |
| 자석 색 변환·후속 로켓 | Supplemental/Baseline | 변환 칸 먼지 2→1, 후속 효과가 추가 감소시키지 않음 |
| 실제 재유입 매칭/잔존 패턴 | Supplemental | 새 블록 소비는 허용, 같은 턴 덮개 중복 피해 없음. 다음 턴에는 피해 이력만 초기화 |
| 낙하·공급 | Supplemental/Baseline | 거미줄 내용물 고정. 일반 블록은 (0,0)→(1,0) 낙하, 먼지 2/3은 각 좌표에 그대로 남고 입력 상태 보존 |

호출 순서: MatchResolution.ApplyLayers는 덮개를 먼저 적용하고 내용물 소비를 건너뛴다. 노출 일반 소비에는 DustRules.ConsumeNormal을 호출한다. PowerCombinationResolution도 일반 변환 때 이를 호출한다. PowerEffectResolution은 반응별 덮개/내용물 처리를 분리하고 실제 Remove에서 색 미션과 먼지 피해를 처리한다. Hammer는 먼지 소비 예외를 유지한다. WebRules/DustRules가 턴별 기록과 완전 제거 미션을 소유한다. Settlement는 내용물 이동과 좌표에 남는 층을 분리한다.

## 보존·한계·다음 단계

작업 전 보호 파일 1095개의 SHA256을 비교해 기존 파일 변경 0건을 확인했다. 패키지·에셋·프리팹·씬·저장 포맷과 기존 진행 중 작업을 보존했다. 빌드·커밋·사용자 Editor 종료·씬 저장 없음. 로그 및 결과 파일에 필수 FAIL/컴파일 오류/예외 없음.

Play Mode/실기기/시각 연출은 검사하지 않았다. 현재 피해 기준 확보에 한정하므로 새 정의 프레임워크·드론 비행은 미구현이다. 검증 범위의 미해결 실패는 없다.

다음은 [EF-04 발전기 충전·연결 기준 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-04-generator-baseline-plan.md), [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-04-generator-baseline-goal.md), [복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-04-command.md)이다. 층과 독립된 충전/연결 상태를 확보하며 공통 규칙 이관은 아직 하지 않는다.

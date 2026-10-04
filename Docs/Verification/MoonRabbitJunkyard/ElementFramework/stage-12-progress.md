# EF-12 — 봇 공개 관찰·숨은 정보 차단 기준 검증

상태: 완료. 2026-10-04, ServeredMeridian / Unity 6000.3.10f1. 기준 커밋 `43ac77c`.

## 변경과 실제 결과

Editor 전용 `Assets/Scripts/Features/AutoPlay/Editor/Tests/BotObservationBaselineVerification.cs`와 meta만 추가했다. 기존 안전한 메모리 fixture와 결정적 Snapshot/공개 형식 그래프 검사를 재사용했다. 기존 검사의 Run 전체나 에셋 저장/UI 검사는 새 검사 내부에서 호출하지 않았다. 모든 레벨은 메모리에 만들고 검사에서 소유·파괴한다.

최종 별도 batchmode/nographics Editor에서 추가 **102 PASS/0 FAIL**, 기존 BotObservationVerification **32 PASS/0 FAIL**, 각각 종료0. 총 **134 PASS**, 실제 입력/관찰 **26건**. 보호 파일 **1,793개 변경0**, 원본/GUID/생산 코드/Addressables/프리팹/씬/저장 포맷과 EF-05 예외 원복 수정 보존. 시작 시 작업 트리는 깨끗했다. 빌드·재패킹·이미지 작업·커밋·사용자 Editor 종료·씬 저장은 하지 않았다.

증거는 [Stage12 로그 폴더](../../../../Logs/ElementFramework/Stage12)에 있다. `baseline.log`, `baseline-execution.json`, `baseline-results.txt`, `observation-values.jsonl`, `existing.log`, `existing-execution.json`, `existing-results.txt`, `protected-before.json`, `preservation-audit.json`, `source-evidence.json`, `source-boundary-final.json`을 함께 확인한다. JSONL은 각 입력 JSON, seed, runtime 변경 절차, 조회 전후 전체 공개 속성 Snapshot, DTO/후보 전체 값, TurnEffects, 난수 draw 전후와 개수를 저장한다. 로그는 Git 제외 대상이며 보고서는 저장소에 남긴다.

## 공개 계약과 소비 경계

| 경계 | 실제 코드에서 확인한 계약 | 검증 근거 |
| --- | --- | --- |
| Capture 시점 | 미종료·Ready인 실행기만 허용 | 기존 검사에서 파워 실행 뒤 처리 중 Capture 거절 |
| 셀 | 좌표/활성/내용/일반 색/로켓 방향/덮개·먼지 내구도/공개 본체 키 | 정상·파워·층 입력 실측; Mold 내부 Unknown 및 색/방향/키 null |
| 본체 | 공개 점유 좌표의 첫 칸으로 `row*columns+column` 키 생성, 종류/내구도; ColorLock 색, Generator 충전/목표와 활성 연결의 공개 키 | 6종·2×2·제거·ID/배열 순서 변경 비교 |
| 미션·장치 | 미션 종류/색 목표의 색/잔여량, 남은 이동, 벽·출구·통로 | 실제 값과 미션 진행 후 과거 관찰 독립성 |
| 후보 | ActionQuery.Find → 새 BotAction/BotMatch. 허용 교환/발동 좌표·패턴 종류·색·칸만 복사 | MatchSwap/PowerSwap/CombinationSwap/Activate 실측 |
| DTO 소유권 | get-only 속성, ToArray 복사 후 ReadOnlyCollection. FlowPortal 등은 참조 없는 값 형식 | 기존 중첩 형식 그래프 검사·쓰기 거절, 추가 컬렉션7개 읽기 전용 및 상태 변경 후 스냅샷 불변 |
| 숨은 데이터 | 정의 객체/인스턴스 ID/내부 인덱스/공급/예약/난수 참조 없음 | 그래프 정적 검사와 아래 실제 변경 쌍을 별도로 검증 |
| 전략 입력 | BasicBotStrategy.Choose/Evaluate와 BotMissionEvaluation은 DTO만 소비. PlanningSearch 생성자도 DTO만 받음 | source-evidence 및 source-boundary-final의 소스/해시. 기존 안전 검사에서 숨은 쌍의 선택/평가 동일성 재사용 |
| 실행 경계 | BotPlaySession은 실행 상태·기록을 보유하지만 이를 전략 인자로 넘기지 않음. Observe는 안정 시점 토큰을 캐시; Submit은 동일 관찰·후보 소속·현재 시점을 확인 후 공통 실행기로 재검증 | 호출부 정적 검토. 이번 단계에서 세션 전체 플레이 실행은 하지 않음 |

ActionQuery는 런타임 상태를 읽는 신뢰 경계다. Movable은 덮인 칸을 제외하고 MatchQuery의 색 조회 및 HasMagnetTarget은 곰팡이 아래를 제외한다. 후보는 실행 결과나 이후 연쇄를 보장하지 않는다. BotTurnRecord의 RandomBefore/After와 세션의 State는 실행/진단 영역이며 BotObservation에 포함하지 않는다. 미래 정의용 ElementId는 아직 없고 기존 배치 Id는 연결용 인스턴스 ID다. 이번에 재해석하지 않았다.

## 실제 값·후보·난수

좌표는 0부터 센다. 모든 관찰은 9×9/81칸이다.

| 입력/시점 | 본체 | 후보 | 규칙 난수 draw 전→후 | 실제 값 |
| --- | --- | --- | --- | --- |
| 정상 고정 색 / 공개 색 변경 / 미션 진행 | 0 | 각0 | 각0→0 | 이동20, Color/Type1 잔여12; (0,1) Type2→Type5; Progress3 뒤 잔여9, 과거 DTO12 유지 |
| 일반 매칭 입력 | 0 | 3 | 0→0 | (2,4)↔(3,4): Three/Type1, (3,3)(3,4)(3,5) 포함 |
| Rocket/Bomb/Drone/Magnet 각 (4,4) | 0 | 각5 | 각0→0 | Activate1+PowerSwap4. 파워 Color=null, 로켓 Vertical |
| 로켓(4,4)+폭탄(4,5) | 0 | 9 | 0→0 | 조합 교환1 포함, 제자리 발동2·일반 파워 교환6 |
| Crate/Scrap/Safe/ColorLock/Appliance 각 (3,3) | 각1 | 각0 | 각0→0 | 키30·내구도1, 앞4종 점유1/Appliance 점유4. ColorLock만 Type3 |
| 위 5종 제거 후 | 각0 | 각0 | 각0→0 | 본체 키 참조 모두 null, 과거 본체 DTO 유지 |
| 발전기+상자/벽·출구·통로 | 2 | 1 | 3→3 | 발전기 키40, 점유(4,4)(4,5)(5,4)(5,5), 충전2/목표3/내구도1, 연결 키43. 상자(4,7) 키43/내구도6. 벽(0,0)↔(1,0), 통로(1,1)→(2,1), 출구(8,0) |
| ID 이름/본체 순서 변경 쌍 | 2 | 1 | 3→3 | 위 DTO와 후보 전체 동일 |
| 곰팡이 A / 숨은 로켓 B / 드론 예약 | 각0 | 각3 | 0→0 / 1→1 / 2→2 | Mold(0,0) 내구도1/Unknown, Web(2,2)2, Dust(2,3)2. 세 DTO/후보 동일 |
| 숨은 공급 A/B | 각0 | 각3 | 각0→0 | seed123/987, 고정 배치 동일. 서로 다른 공급 목록/커서에도 DTO/후보 동일 |

기본 정상 fixture는 검사 목적으로 고정 색 패턴을 써서 행동 후보0이다. 실제 플레이 시작 가능성을 뜻하지 않는다. 일반 매칭 입력을 별도로 보완해 후보3과 실제 패턴을 기록했다. 곰팡이·예약·공급 비교에는 기존 ItemBoosterVerification.PlayFixture의 보이는 (8,8) 가로 로켓을 사용한다. 후보3은 (7,8)↔(8,8), (8,7)↔(8,8)의 PowerSwap와 (8,8) Activate다. 빈 후보끼리만 비교하지 않았다.

## 숨은 입력 쌍과 원본 보존

- 곰팡이 A는 (0,0) 일반/Type1을 가린다. B는 내부를 Rocket/Type5/Vertical로 바꾸고 규칙 난수를 한 번 추출한다. DTO 전체와 후보 전체가 동일하다. 가려진 칸은 조작/매칭 후보에 없고 Unknown을 Empty로 바꾸지 않는다.
- 발전기 연결 대상과 위치는 유지하며 모든 본체 Id를 history-id로 바꾸고 배열0→1 이동을 한다. 실제 내부 Id/첫 본체 Kind가 다름을 먼저 검사한 뒤 공개 본체 키/활성 연결/후보 전체가 동일함을 확인한다.
- 예약 비교는 같은 runtime/TurnEffectContext를 사용하는 실제 DroneTargetManager.Request(4,4)로 예약1을 만든다. 선택 시 draw1→2가 되지만 그 뒤 Capture는2→2이고 DTO/후보는 이전과 같다. 이는 메모리에서 Ready 실행기에 문맥을 설치한 격리 입력이다. 게임에서 비행 중 Observe가 허용된다는 뜻은 아니다. 처리 중 관찰 거절은 기존 검사로 따로 확인한다.
- 공급 A는 (0,0) 고정 목록 Normal/Type1×2. B는 Bomb×3, Normal/Type5×7이며 커서 ItemIndex1/ItemConsumed1이다. seed123/987과 공급 상태의 실제 차이를 검사한 뒤 공개 DTO/후보 동일성을 확인한다. 기존 검사도 별도의 난수 추출을 포함하는 공급 쌍을 검증한다.
- 모든 추가 관찰은 Capture 전후 runtime·정의 JSON·턴 문맥·규칙 draw count·UnityEngine.Random.state가 같고 반복 Capture도 같음을 검사한다. 기존 검사에는 후보 평가·선택 후 상태 보존도 있다. 공개 색/미션/본체 점유를 바꿔도 과거 DTO가 바뀌지 않는다.

## 실행 중 오류와 한계

초기 추가 검사에서 BoardCoordinate 비교 연산자 지원을 잘못 가정한 컴파일 오류와 DroneTargetManager 생성에 필요한 TurnEffectContext 누락이 있었다. 검사 자체를 Equals 및 실제 문맥 생성으로 수정했고 생산 코드는 변경하지 않았다. attempt1/attempt2 로그와 pre-final 결과를 보존했다. 최종 필수 검사에는 실패가 없다.

이 결과는 기존 공개 계약과 지정된 메모리 쌍에 대한 기준이다. 모든 가능한 레벨/미래 콘텐츠의 비간섭 증명, 전략 개선, 점수 정책 적합성, 전체 승률·성능·게임 세션/실기기/IL2CPP 검증은 하지 않았다. 사용자 Editor는 건드리지 않았다. EF-09 원본1254px3개·Editor/월드 바닥 표현 차이와 EF-11 Draw가 색/order/회전을 초기화하지 않는 책임 분리는 그대로 남긴다. 정의 기반 관찰 전환과 공용 풀 정리는 별도 단계다.

## 완료 대응과 다음 단계

EF-12 목표의 7항목을 코드 경계 표, 실제26건, 변경 쌍, 원본/난수·스냅샷 검사, 중첩 계약 검사, 두 최종 실행/해시 감사와 문서로 충족했다. 전체 리팩토링 완료를 뜻하지 않는다.

다음은 큰 구간 B의 첫 작은 작업 **EF-13 영구 정의 ID·기존 장애물 매핑**이다. ID 값과 기존6종→ID 연결만 추가하고 실행기/저장/관찰 소비자는 전환하지 않는다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-13-element-id-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-13-element-id-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-13-command.md). 다음 단계 구현은 시작하지 않았다.

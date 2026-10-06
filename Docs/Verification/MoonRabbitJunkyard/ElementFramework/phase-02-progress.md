# 큰 구간 2단계 진행 기록

계획: [드론 목표 선택과 비행](../../../Planning/MoonRabbitJunkyard/ElementFramework/phase-02-drone-target-flight-plan.md).

상태: 완료. 최종41종 실제 종료0/688965 PASS/0 FAIL, 전체 비교·보존 감사·3단계 인계 완료. 2026-10-06. 아래 실행 중/실패 기록은 당시 이력으로 보존한다.

- work/HEAD6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783와 기존 변경 목록, 소스/meta/패키지/설정1166개 해시를 Phase02에 기록했다. 1단계 증거는 덮어쓰지 않는다.
- A→B: 정책 선택 결과와 요청별 예약을 비행 기록이 소비한다. 실제 미션/피해/난수 소유권은 기존 실행기에 유지한다.
- B→C: 최종 목표만 보간하지 않고 최초 예약/소실/재선택과 표시 효과 시간을 비행 구간으로 연결한다. 이미 해결된 final 보드를 유효성 판정으로 사용하지 않는다.
- Ruling: 사용자 지정 work/WIP·직접 실행·빌드/커밋/푸시 금지와 기존 증거 보존을 따른다. 스킬의 새 워크트리/커밋/하위 리뷰어/증거 삭제 절차는 적용하지 않는다. 검증 원문과 진행 기록은 유지한다.
- 현재 SourceBefore에 목표 관리자와 월드 표시 소스를 보존했다. 독립 검사 DroneFrameworkVerification.Baseline은 기존 드론 판정/예약/발전기/재탐색 검사를 재사용하며 원래 출력 경로에 쓰지 않는다. RiseHover는 실제 조합의 첫 출발 지연이 새 짧은 상승/호버 범위인지 검사한다. 아직 실행 결과를 통과로 표기하지 않는다.

## 시작 기준과 첫 실패 재현

- DroneFrameworkVerification.Baseline 실제 종료0/250 PASS/0 FAIL, 고정 입력 관찰16행을 Phase02/baseline-observations.jsonl에 확보했다. 후보/예약/2×2/발전기/재탐색/미션/규칙 난수와 타임라인 입력이 포함된다. 원래 Stage02/TargetPower 검사 출력은 호출하지 않았다.
- DroneFrameworkVerification.RiseHover 실제 종료1/1 PASS/3 FAIL. 로켓+드론과 폭탄+드론의 첫 출발은1.8초, 자석+드론은2.15초로 새 짧은 상승/호버 범위를 벗어났다. 드론+드론의1초 출발은 통과했다. 컴파일/fixture 오류가 아닌 현재 연출 시간 차이의 실패다.
- 실패 원문은 rise-hover-red-results.txt와 runs/rise-hover-red-20261005-223611의 실제 execution.json/editor.log에 보존했다. 생산 소스는 아직 변경하지 않았다.
- 다음: A의 활성 정책별 실제 Query/비활성 호출0/기존 기여 동일 검사를 먼저 확보하고 등록 키 선택을 연결한다. 이후 B의 최초 예약/소실/재탐색 구간과 C의 모든 드론 상승/호버를 연결한다. 출발 시간을 줄이는 수정만으로 이번 목표를 완료하지 않는다.

## A. 활성 특성 정책의 실제 연결

- policy-red 실제 종료1: 등록표 부재를 확인한 뒤 DroneTargetPolicyRegistry를 구현했다. 미션 종류→기존 미션 인덱스를 한 번 구성하고 실제 반응과 선택 정의의 미션 특성으로 Color/Durability/Layer/Recovery/GeneratorTargets 키만 직접 조회한다.
- QueryArea 후보와 LiveReservations가 같은 정책 인스턴스/좌표별 기여 캐시를 공유한다. 기존 Invalidate에서 후보·정책 캐시를 함께 비운다. 후보 정렬·직접 우선·예약·Project·선택 난수의 소유권은 바꾸지 않았다.
- 등록 정책의 실제 기여 원문/순서·조회 상태/난수·비활성 호출0·완료한 미션 추가 호출0·다른 ID 내구도 정책 재사용·선택 정의 미션 키 변경250 PASS/0 FAIL, 실제 종료0.
- 전환 후 기존 실행250 PASS/0 FAIL. 기준16행 전체 원문 SHA256 D45F4FFE117D5990C6CABE9F9F45F104AE73B78A18ABBAE658942928E50330B8 동일. 상태/후보/예약/미션/난수/현재 타임라인까지 바이트 그대로 비교했으며 정규화는 없다.
- Ruling: 최초 검사 도구의 장애물 배치가 GUID를 새로 만들어 단순 재실행 입력이 달랐다. 입력/상태의 실제 차이 위치를 확인했고 결과를 정규화하지 않았다. 처음 기록된 원래 레벨 JSON과 ID를 기존 Editor 검사 Build 직전의 검사 전용 콜백으로 재사용해 대조했다. 콜백은 해당 관찰 구간에만 설정하며 finally에서 해제하고 기존 검사는 null 그대로다.
- 생성 ID 불일치 영수증/원문은 policy-baseline-fixture-id-mismatch.json 및 policy-current-generated-id-observations.jsonl에 보존했다. 수정 후 비교는 policy-baseline-comparison.json이다.
- 구형 공개 ObstacleMissionRule.Query의 종류/예외 호환 코드는 유지하고 새 등록 내구도 정책만 선택 정의를 소비하는 QueryDefinition을 호출한다. Layer는 기존 실제 정의 조회와 같은 기여 계산을 공유한다.
- B/C의 비행 기록/표시 변경과 D의 전체 회귀/Play Mode/보존 감사/다음 인계는 아직 미완료다. 새 정책 추가마다 별도 단계 문서는 만들지 않는다.

## B. 최초 예약과 실제 소실 효과 기록

- RetargetFlight 실제 실패: 고정된 부모 로켓 사례 자체는 통과했지만 최초 목표·소실을 잇는 비행 기록이 없었다. 실패 원문/실제 종료1을 보존한 후 기록을 추가했다.
- PowerPresentationTrace.Flights에 요청·출발·최초 목표·최종 목표(없음 포함)·예약 당시 효과/공격 개수·실제 소실 효과 인덱스·최종 착탄 hit를 보존한다. 저장 DTO에는 연결하지 않는다.
- 큐가 원래 예약 스냅샷을 소유하고 착탄 시 기존 Land가 확정한 결과만 기록한다. 타겟 선택/예약 해제/기여/난수/NextHit 호출 순서는 유지한다. 재선택 실패도 최초 예약이 있었으면 최종 목표null/착탄 hit0으로 기록한다.
- retarget-flight-record-green 실제 종료0/3 PASS/0 FAIL. 최초 소실 목표와 최종 목표가 구분되고 실제 소실 효과 인덱스가 기록됐다. 이는 메타데이터 검증이며 화면에서 돌진 중 정지·호버가 구현됐다는 증거는 아니다.
- 다음: 타임라인과 표시기의 구간별 비행을 연결한다. 원래 부모 로켓 fixture는 실제 효과 시간상 목표가 느린 상승 중 먼저 사라질 수 있으므로 돌진 중 소실 검증으로 대체 사용하지 않는다. 별도 실제 연쇄 범위/추가 폭발 fixture에서 표시 소실 시간이 돌진 구간 안에 들어오는 것을 확인해야 한다.
- 최초 목표가 돌진 전에 사라졌으면 그 목표로 허위 돌진하지 않고 호버 후 재선택한다. 소실이 돌진 중이면 현재 위치에서 정지/호버한 뒤 새 목표로 돌진한다. 최종 보드의 이미 삭제된 셀을 소실 판단에 사용하지 않는다.
- 새 Flights 기록은 의도적 표시 메타데이터 추가다. 다음 전후 대조에서는 기존 모든 논리 필드와 실제 입력/시드/효과/미션/난수를 그대로 대조하고 추가 기록만 별도 설명한다. 기존 원문은 덮어쓰거나 정규화하지 않는다.

## C. 비행 모델과 실제 표시 클립 연결 (진행 중)

- motion-model-green 실제 종료0. 모든 조합2/4/5/8의 예약 비행이 상승0.5초·0.65셀, 위치 고정 호버, 돌진 구간으로 연결되고 경계 시간/위치가 연속임을 확인했다.
- PuzzlePowerPlayback.Clips가 timeline.Flights.Phases를 그대로 참조하고 Paint가 해당 Phase.PositionAt을 사용한다. 기존 LiftOnly/OrbitRadius/원 선회 함수와 무작위 선회 슬롯 생성을 제거했다. 로켓·폭발·레이어 효과와 기존 리소스 준비/반환 수명은 유지한다.
- motion-renderer-connected 실제 종료0, 84 PASS/0 FAIL. 모든 모델 구간과 표시 클립의 개수 및 참조 일치를 확인했다. 이는 화면 재생 코드 연결 검사이며 그래픽 Play Mode 관찰을 대체하지 않는다.
- all-drone-rise-green 실제 종료0, 4 PASS/0 FAIL. 이전 조합 시작 시간 실패3건을 해소했다.
- 기존 그래픽 검사에서 선회 반지름/소수만 상승 기대를 모든 조합 상승·정지 호버·빠른 곡선 돌진 기대로 변경했다. 기존 변환/타격/레이어/종료/자원 검사는 유지한다. 변경한 그래픽 검사는 아직 실행하지 않았다.
- 남음: 실제 돌진 중 목표 소실·후보 없음·반복 재선택, 그래픽 Play Mode, 최종30종/전체 비교/보존 감사, 구간3 인계. 2단계 완료로 표기하지 않는다.

## B/C. 실제 연쇄 소실과 후보 없음 검사

- 실제 고정 레벨·시드와 검사 전용 추가 파워로 돌진 중 목표 소실을 탐색했다. 규칙 난수와 별개 System.Random(seed)로 일반 칸 중 1/12 폭탄·1/12 가로 로켓을 배치하고 실제 Swap/효과 기록/표시 시간만 사용했다.
- 최초 pair8/seed10은 목표 소실 후 후보가 없었다. 정지·호버는 통과했으나 재돌진 기대가 실패했다. 이 결과는 loss-motion-no-candidate-observation.txt/trace.txt에 보존했다. 후보 없음과 새 목표 재돌진은 별개 요구이므로 재돌진 fixture에는 LandingTarget이 존재해야 함을 명시했다. 다음 실행의 검사 using 누락 컴파일 오류도 해당 실행 로그에 보존했다.
- pair8/seed25 실제 연쇄로 최초 목표를 돌진 중 제거하고 그 위치에서 정지·호버 후 다른 실제 예약 목표로 돌진하는 4 PASS/0 FAIL, 실제 종료0. loss-motion-source/fixture/trace 원문과 real-dash-loss-with-landing-fixed 실행 기록에 보존했다. 부모 로켓의 상승 중 소실 사례를 돌진 중 소실 증거로 재사용하지 않았다.
- 별도 기존 부모 로켓 레벨에서 일반 칸은(4,8)만 남긴 실제 소실·후보 없음 사례3 PASS/0 FAIL, 실제 종료0. 최초 예약은 있으나 최종 목표null/hit0, 착탄 공격 없음, 유한 호버/종료, NoTarget 기록을 확인했다.
- 그래픽 Play Mode Run 실행 중. 새 run-graphics.ps1은 nographics 없이 별도 Editor를 실행하고 Stage08 기존134개 출력의 복사본/검사 후 원문/복원 SHA 비교를 각각 남긴다. 아직 실제 종료 및 그래픽 결과를 확인하지 않았다. 반복 재선택과 전체 회귀/전체 비교/최종 감사/3단계 인계는 미완료다.

### 그래픽 실행 1차와 시작 연출 검사 보완

- graphics-play-motion 실제 종료1, 490 PASS 후 게임 세션 파워 발동에서 FAIL1. 앞선 드론/조합/레이어/본체 그래픽 항목은 통과했으나 이 실행 전체는 성공으로 표기하지 않는다. 기존134 출력 SHA 복원 불일치0.
- 원인: 실제 세션 InitializeAsync는 ready=true 이후 기존 BeginStartFeedback의0.65초 입력 잠금을 시작한다. 검사에서 즉시 TryActivate를 호출했다. 기존 입력 잠금을 우회하는 생산 수정 없이, 검사에 시작 연출 중 입력 불가를 명시적으로 확인하고 실제 프레임에서 종료까지 최대5초 기다린 뒤 발동하도록 보완했다. 자원 실패/취소 검사도 동일하게 시작 연출 종료 후 입력 가능을 확인한다.
- graphics-play-motion-start-wait를 별도 실제 그래픽 Editor에서 재실행했다. 진행 중인 동안 결과를 성공으로 간주하지 않는다. 첫 실행의 drone-frame-0.png를 열어 실제 보드와 드론 이미지 표시를 확인했으며 정지 이미지 하나로 전체 모션을 입증하지 않는다.

- graphics-play-motion-start-wait 실제 실행: 종료0, 507 PASS/0 FAIL. 시작 연출 입력 잠금/종료 후 발동, 모든 기존 표시 fixture, 효과 준비/실패/종료 시 자원 반환 검사를 확인했다. Stage08 출력134개 SHA 복원 불일치0. 정지 이미지 직접 확인과 실제 시간별 Tick/Play Mode 검사를 함께 보존했다.
- graphics-game-scene RunScene을 실행 중이다. 원래 게임 씬을 별도 검사 Editor에서만 열며 SaveScene/빌드를 호출하지 않는다. 실제 종료/scene-results와 복원 감사를 확인하기 전 완료로 표기하지 않는다.

- graphics-game-scene 실제 종료0, 842 PASS/0 FAIL. 실제 게임 씬에서 4파워/10조합의 공격 중 결과 숨김, 입력/아이템 잠금, 일시정지·화면 회전 중 상대 위치/상태 유지, 최종 보드·난수·이동·공급·미션·회수/Phase/승패 동등, 효과 잔상 없음, 원래 MemoryPack 다시하기, 준비/재생 취소 후 상태·잠금·객체 수 복원을 확인했다. 기존 출력 원문 복원 SHA 불일치0.
- 그래픽 Run507 + RunScene842 =1349 PASS/0 FAIL이지만 실제 돌진 소실 fixture의 그래픽 프레임과 반복 재선택 자체는 아직 추가 확인이 필요하다. 모델의4 PASS를 전체 반복 비행 증거로 확대하지 않는다. 최종 논리30종/전체18182행 비교와 최종 보존 감사도 남아 있다.

### 실제 재탐색 비행 렌더러 검사

- 그래픽 Run에 pair8/seed25의 실제 연쇄 목표 소실 사례를 추가했다. 실제 Swap 결과/효과 기록과 타임라인의 소실 효과 시간만 사용하고 최종 보드로 표적 유효성을 역추정하지 않는다.
- 소실 직전1.480초, 소실 직후1.500초, 호버 종료 직전3.045초, 새 돌진 중3.192초에 실제 활성 EffectSprite의 Transform과 해당 비행 구간의 현재 위치를 비교했다. 정지/호버 동안 끊긴 돌진의 마지막 위치를 유지한다. 네 장의 렌더 캡처도 원문 보존했다.
- graphics-real-retarget 실제 종료0, 516 PASS/0 FAIL. 1.500초/3.192초 캡처를 직접 열어 드론 이미지/보드 표시를 확인했다. 실행 전 기존 출력 복원 SHA 불일치0. 기존 RunScene842 PASS는 직전 동일 생산 코드에 대한 증거이며 이 검사 추가 뒤 재실행한 것으로 표기하지 않는다.
- 반복 재탐색 설계 확인 요청: 현재 규칙은 예약 후 모든 pending 효과를 동기 해결하고 한 번 Land에서 재검증/재선택한 뒤 즉시 착탄한다. 한 요청의 중간 실제 재선택을 추가하면 규칙 난수 소비/예약 우선순위/착탄 결과가 달라질 수 있다. 여러 번 실제 재선택을 우선할지, 기존 논리 결과 보존을 우선하고 해당 부분을 후속 단계로 분리할지 사용자에게 질문했다. 사용자의 결정 전 논리 결과 변경이나 이 요구의 임의 제외를 하지 않는다. 이번 요구는 아직 충족되지 않았다.

## 사용자 확정 이후 — 여러 차례 실제 재선택

- 사용자가 첫 번째 선택지를 확정했다. 실제 효과 중 반복 재선택을 우선하며 선택 난수·예약 순서·최종 명중/후속 결과 차이를 허용하고 별도 대조한다. 계획과 목표에 이 결정을 추가했다. 기존 결과 보존을 이유로 반복 재선택 요구를 제외하지 않는다.
- RepeatSelection RED 실제 종료1, 여러 차례 이력 없음. 원문을 repeat-selection-red-results.txt에 보존했다.
- DroneTargetManager.Refresh는 동일 QueryArea/예약/정책/규칙 난수로 현재 예약을 재검증하며 착탄하지 않는다. Land는 Refresh 뒤 착탄 때만 예약을 반환한다. PowerEffectResolution은 실제 EffectRecord 추가 후 대기 중 요청을 순서대로 Refresh하고 각 소실 목표/다음 목표(nullable)/원인 효과 인덱스를 DroneRetargetRecord로 기록한다. 한 요청이 후보 없음으로 종료되면 다시 임의로 예약을 만들지 않는다.
- DroneFlightRecord.Retargets는 이 반복 이력을 소유하며 저장 DTO에는 연결하지 않는다. 모델은 각 실제 효과 시간의 소실까지 비행을 끊고 같은 위치에서 재탐색 호버 후 다음 구간을 만든다. 상승/호버 중 먼저 사라진 목표에는 허위 돌진을 만들지 않는다.
- repeat-selection-history-green 실제 종료0. 이어 repeat-selection-motion20 PASS/0 FAIL, 실제 종료0: 한 요청의 반복 실제 재선택과 모든 해당 표시 구간 시간/위치 연속을 확인했다.
- all-motion-after-repeat78 PASS/0 FAIL, 실제 종료0. 기존84와 요청 수가 다른 것은 승인된 실제 선택/연쇄 결과 변경의 영향이다. 결과 수치를 기존과 동일로 꾸미지 않는다.
- 기존 LossMotion은 최초 소실 효과 시간을 뒤의 중단 구간과 비교해2 FAIL이었다. loss-motion-repeat-first-event-mismatch.txt와 실행 로그를 보존했다. 실제 각 중단 구간의 목표 좌표/시간과 해당 목표의 Remove/Activate 효과를 대조하도록 수정했고 loss-motion-repeat-event-linked4 PASS/0 FAIL, 실제 종료0(pair8/seed10).
- graphics-real-repeat를 실행 중이다. 검사 fixture는 새 실제 소실 seed10을 사용하고 반복 중단과 각 이력의 시간을 연결한다. 기본 호버는 목표 변경으로 구간이 나뉘므로 호버 총 시간>=0.35초를 검사하고 각 구간 위치 고정 검사는 유지한다. 이전 그래픽516/RunScene842는 새 생산 코드 이후 통과한 증거로 재사용하지 않는다.
- 남음: 새 반복 이력/피해/예약 해제의 상세 감사와 실제 여러 번 비행 중단 사례, 그래픽 최종, 신규/기존30종/전체18182행·188팩·225상태의 승인된 차이 대조, 원문/GUID/WIP/과거 보존 감사 및3단계 문서 인계. 2단계 미완료.

- graphics-real-repeat 실제 종료0, 477 PASS/0 FAIL. 승인된 새 예약 갱신 결과로 표시 비행/구간 수가 바뀌었으며 기존516과 동일한 수치로 표기하지 않는다. 실제 렌더러의 소실 전1.36/직후1.38/호버 끝1.76/새 돌진1.86초 위치 검사를 포함한다. 기존 출력138개 원문 SHA 복원 불일치0.
- graphics-scene-after-repeat RunScene 실행 중. 현재 생산 코드의 실제 씬 논리/표시/일시정지/취소 검증은 종료 증거를 확인한 뒤 보고한다.

- graphics-scene-after-repeat 실제 종료0, 826 PASS/0 FAIL, 기존 출력 복원 불일치0. 새 실제 반복 선택 코드로 게임 씬의4파워/10조합·정지·화면 회전·최종 논리/표시·취소/재시작/객체 보존을 검사했다. 최종 전체 회귀/데이터 차이 감사는 아직 남아 있다.

## D. 최종 전체 회귀 실행 중

- repeat-selection-guards 실제 종료0, 53 PASS/0 FAIL. 요청당 비행 결과 하나, 착탄 hit/공격 일대일, 미션 진행 범위, 예약 이후의 실제 원인 효과 인덱스, 이전 선택→다음 소실 이력 연결, 마지막 재선택→실제 착탄/후보 없음과 구간 연속을 확인했다.
- Phase02/Regression에 기존30종의 실제 진입점/기대 검사 수/원문 복원 runner를 별도로 복사했다. Phase01/과거 증거는 수정하지 않는다. 실행 직전 현재 Assets/Scripts의.cs/.meta 전체 SHA를 source-hashes.json으로 기록했다.
- 기존30종은 live session41534에서 순서대로 실행 중이다. 첫 ScrapMaintainDurability 사례는 이전 최종 실행도 약4분이 걸리는 큰 검사다. 현재 결과/종료/전체 성공으로 간주하지 않는다. 각 종료 코드와 FAIL/기대 검사 수 확인 후 다음 검사를 실행하고 finally에서 기존 출력 원문을 복원한다.
- 전체18,182행·188팩·225상태 비교 도구는 원래 입력/순서/원문을 보존한다. 새 Flights 표시 이력만 별도 보조 비교로 분리해 metadata-only와 기존 논리 필드 차이를 구분한다. 논리 차이 행의 양쪽 전체 원문 및 최초 차이 문맥을 따로 남기며 자동으로 승인된 차이로 간주하지 않는다. 메타데이터 파서4개 경계 사례 통과. 실제 전체 비교는 첫 회귀 출력이 완성된 후 실행한다.

### 전체 실행 원문 비교와 표시 기록 분리

- 첫 기존30종 runner는 첫 검사에서 과거 전체 원문 SequenceEqual에 실패했다. 결과/값 원문은 Phase02/Regression에 보존했고 기존 출력은 finally에서 SHA까지 복원했다. 실패 전에 definitions-after 출력이 작성되지 않아 runner가 파일 부재에서 종료했으므로 이 실행의 Unity 실제 종료 코드는 별도 receipt가 없으며 runner 종료1만 관찰했다. 해당 실행을 실제 Unity 종료0/통과로 표기하지 않는다.
- 첫 실행의 전체18182행·188팩·225상태를 Phase01 최종 원문과 스트리밍 대조했다. 동일 입력/행 순서, 원문 동일16919행, 새 Flights 표시 이력만 추가1263행, 기존 논리 필드 차이0행. SHA 원문은 달라지며 그대로 보존한다. 양쪽 차이 행 전체 원문/문맥과 비교 내역은 full-values-comparison.json/changed-original-rows.jsonl/full-values-differences.json에 남겼다. 이 비교가 반복 연쇄의 모든 새 행동을 포괄한다는 주장은 하지 않는다.
- RecordedLogicComparison은 기존 JSONL행의 모든 원문/순서/개수를 비교하고 이번에 추가된 Flights 배열만 보조 비교에서 분리한다. 난수/피해/미션/팩/기존 필드 값은 바꾸거나 무시하지 않는다. baseline에 Flights가 이미 있으면 그 기록도 정확히 비교한다.
- Comparison 실제 종료0, 6 PASS/0 FAIL. 표시 기록 분리 성공, 피해/난수/미션 차이 거부, 행 개수 차이 거부, 이미 기준에 있는 표시 기록 변경 거부를 확인했다. 정상 JSONL 전체 비교23개 호출부만 이 비교기로 바꾸었고, 개별 피해/예약/미션/ID/팩/상태 검사는 그대로 유지했다. PASS 문구도 표시 이력 분리를 명시했다.
- 새 최종 증거는 RegressionFinal에 생성하고 소스 SHA를 다시 고정했다. 종료 직후 terminal.json을 먼저 기록하도록 runner를 보완했다. 기존 실패/과거 회귀 증거는 덮어쓰지 않았다. 현재 session43633에서30종 실행 중이며 첫 검사 종료/전체 성공은 아직 확인 전이다.

### 최종 회귀 첫 완료와 현재 보존 점검

- RegressionFinal 첫 ScrapMaintainDurability 실제 종료0, 50950 PASS/0 FAIL, 기존 기대 검사 수50950 일치. definitions/연결 결과까지 생성됐고 이전 출력4개 원문 SHA 복원 불일치0.
- 이 최종 실행의 전체 values SHA E799F5800A6FD8029C3BEF833D38E1D4A89B5B54A3D1E0479E5820BBC5F4C898가 앞서 실제18182행을 비교한 원문과 동일했다. 따라서 현재 최종 실행도 전체18182행·188팩·225상태 입력/순서 보존, 논리 필드 차이0, 추가 표시 기록1263행임을 원문 해시로 재검증했다. first-full-scope-revalidation.json에 기록했다.
- 검증 중 소스1130개 SHA 불일치0, EF37 원래 에셋·메타·패키지·설정1489개 차이0. 새 소스/메타10개는 명시된5종만 존재하며 새GUID5개 모두 고유/유효. 이들은 현재 중간 보존 점검이고 전체 실행 종료 후 다시 감사한다.
- session43633는 이제 두 번째 ScrapSupplyMissionPolicy 실행 중이다. 30종 전체 완료/최종 신규 검사/반복 비행 상세 감사/문서 인계는 아직 남아 있다. 첫 완료를30종 완료로 확대하지 않는다.

- RegressionFinal 두 번째 ScrapSupplyMission 실제 종료0, 51155 PASS/0 FAIL. 첫 두 검사 합102105 PASS/0 FAIL,각 terminal 영수증과 기존 출력 원문 복원 SHA를 확인했다. 현재 세 번째 ElementDurabilityApplyPolicy 실행 중이며 전체 완료로 표기하지 않는다.

- 승인된 실제 반복 선택의 별도 연쇄 사례(조합8/시드10, 동일 고정 레벨+표시 검사 전용 추가 폭탄/로켓)에서 비행 기록14→10, 첫 요청의 최종 목표null/hit0→(1,6)/hit44 차이를 확인했다. 이는 approved-repeat-landing-differences.json에 양쪽 실제 기록·SHA와 함께 보존했다. 전체 정상18182행의 논리 차이0을 이 별도 연쇄까지 모든 플레이 결과 불변으로 확대하지 않는다. 이 자료는 최종 착탄 기록 대조이며 전체 상태/난수 대조 증거는 아니다.
- RegressionFinal 세 번째 ElementDurabilityApplyPolicy 실제 종료0, 50875 PASS/0 FAIL. 첫3종 합152980 PASS/0 FAIL, 기존 출력 원문 SHA 복원 불일치0. 현재4번째 ElementReactionApply 실행 중이다.

- RegressionFinal 네 번째 ElementReactionApply 실제 종료0, 53295 PASS/0 FAIL. 첫4종 합206275 PASS/0 FAIL, 개별 기대 수와 일치하며 원문 SHA 복원 불일치0. 현재5번째 ElementReactionBehavior 실행 중이다.

- RegressionFinal 5~7번째 ElementReactionBehavior 53053, DurableMagnetPolicy 54307, CapsuleMagnetPolicy 49166 PASS/0 FAIL, 모두 실제 종료0이며 기존 기대 수와 일치했다. 첫7종 합362801 PASS/0 FAIL, 완료7종의 기존 출력 원문 SHA 복원 불일치0을 seventh-restoration-audit.json에 기록했다. session43633에서 남은23종을 계속 실행하며 소스 고정 상태를 유지한다.
- 최종 신규9종 runner인 run-final-new.ps1을 준비했다. 기존30종의 실제 종료 후에만 실행하며 Baseline 진입점은 포함하지 않는다. 기존 Phase02 결과와 fixture/상태/이력 출력은 별도 새 폴더에 복사한 후 원문 SHA로 복원한다. 아직 실행/통과로 표기하지 않는다. 최종 원본 에셋/과거 증거 감사 절차도 준비했으며 실행 종료 후 확인한다.

- RegressionFinal 8~9번째 CapsuleAdjacentPolicy 48386, InitialMissionSupply 47201 PASS/0 FAIL, 모두 실제 종료0. 완료9종 합458388 PASS/0 FAIL이며 기대 검사 수/기존 출력 원문 SHA 복원을 ninth-restoration-audit.json에 확인했다. 현재10번째 RemovalMissionProfileVerification을 같은 session43633에서 실행 중이다.
- 최종 신규 runner는 기존30종 final-regression-audit의 완료와 고정 소스1130개 SHA 일치를 실행 전 요구하고 종료 후에도 소스 SHA를 검사한다. 신규9종의 개별 기대 검사 수도 고정했으며 PowerShell 구문 검사는 통과했다. 이는 절차 준비 증거이며 신규 최종 실행 통과를 의미하지 않는다.

- RegressionFinal 10~12번째 RemovalMissionProfile 43431, DamageRecordPolicy 42279, ReservedDamagePolicy 7793 PASS/0 FAIL, 모두 실제 종료0. 완료12종 합551891 PASS/0 FAIL이며 기대 검사 수/기존 출력 원문 SHA 복원을 twelfth-restoration-audit.json에 확인했다. 현재13번째 DamageAggregationPolicyVerification을 session43633에서 실행 중이다. 실행 프로세스 PID73988의 CPU 증가를 확인했으며 아직 종료/통과로 표기하지 않는다.
- MissionProgressRules 시작 시점 원본을 Phase01/b-green-source/MissionProgressRules.cs에서 확인했다. Phase02 initial-protection의 원본 SHA와 정확히 일치하며, 참조 보관본은 수정하지 않았다. mission-original-reference-audit.json과 원본 대비 diff에 선택 정의용 두 조회 함수와 기존 Layer 조회의 동일 계산 위임만 추가됐음을 보존했다.
- 최종 보존 감사에는 원래1166파일 중 의도된7런타임/25검사 파일만 변경 허용, 나머지 동일/삭제 없음, 기존 에셋1489개/과거6941증거 동일, 새 소스·메타10개 및 GUID5개 고유 확인을 포함했다. 기존 비교 호출23개와 신규 파일 목록10개 일치 점검/PowerShell 구문 검사는 통과했고 전체 최종 감사는 아직 실행 전이다.
- 후속 session78021은 실제 확인한 회귀 runner PID28896/시작 시각2026-10-05T23:41:11.0533083Z의 종료를 기다린다. 프로세스 정상 종료를 확인한 후 audit-final-regression→run-final-new→audit-final-protection을 순서대로 실행하며, 실패하면 다음 Unity 실행을 하지 않는다. PostRegression-20261006-003309에 실행 상태를 기록했고 아직 후속 검사가 시작/완료된 것으로 간주하지 않는다. 별도 신규 검사 runner를 중복 실행하지 않는다.

## 최종 완료 결과와 요구사항 감사

| 범위 | 최종 증거와 결과 |
| --- | --- |
| 기존 논리 회귀 | `RegressionFinal/verified-regression.json`의 서로 다른30메서드와 각 실제 Unity terminal: 687011 PASS/0 FAIL, 종료0. 전체 runner session43633도 실제 종료0. |
| 신규 정책/비행 | `FinalNew-20261006-004653/verified.json`와9개 terminal: Current250, Policies250, RiseHover4, RetargetFlight3, Motion78, LossMotion4, NoTargetMotion3, RepeatSelection53, Comparison6 = **651 PASS/0 FAIL**, 종료0. 준비 중 수기 합계751은 오산이었으며 개별 기대 수와 실제651은 일치한다. |
| 실제 그래픽/게임 씬 | `runs/graphics-real-repeat-20261005-232950`: Run477, `runs/graphics-scene-after-repeat-20261005-233119`: RunScene826 PASS/0 FAIL, 둘 다 실제 종료0/graphics=true. 합1303. after 폴더의 다른 예전 결과는 합계에 재사용하지 않는다. |
| 전체 원문 비교 | 정상18182행·188팩·225상태: 입력/순서 보존, 원문 동일16919행·새 Flights 이력만1263행·기존 논리 차이0. 최종 SHA와 이미 대조한 원문 SHA 일치. `RegressionFinal/final-regression-audit.json`. |
| 소스/출력 보존 | 고정 소스·메타1130개 SHA 차이0, 완료30종의 출력 복원76영수증 확인. 신규9종도 이전 Phase02 출력/fixture/상태/이력을 복원하고 신규 결과는 별도 폴더에 보관했다. |
| 에셋/과거/작업 상태 | `final-protection-audit.json`: 원래1166파일 중 의도된7런타임/25Editor 검사만 변경, 나머지1134 동일·삭제 없음. 원래 에셋/설정1489·과거6941증거 차이0, 새 소스/메타10개·GUID5개 유효/고유, work/HEAD 보존. |

최종 합계는 **41종 / 688965 PASS / 0 FAIL / 실제 종료0**이다. 1단계의 과거687830이나 앞선 실패/이전 생산 코드의 그래픽 결과를 합산하지 않는다.

### 요구사항별 근거와 변경 의미

- 활성 정책: 정책250검사가 실제 기여/순서·조회 상태/난수 보존·비활성 호출0·완료한 미션 추가 호출0·같은 특성의 다른 ID 재사용을 확인했다. 등록 키 직접 조회와 공통 후보/좌표 기여 캐시를 소스에서 대조했다. 전체 Supports 순회나 드론별 별도 보드 수집기는 추가하지 않았다.
- 실제 목표 변경: effects 적용 후 활성 예약을 재검증하고 실제 선택 결과를 Retargets에 기록한다. RepeatSelection53은 한 요청의 여러 실제 재선택, 원인 효과·이력 연결·착탄/공격 일대일·미션 범위·표시 구간 연속을 확인한다. 후보 없음은 별도3검사에서 착탄/hit 없음·NoTarget·유한 호버 종료를 확인했다.
- 모든 드론 연출: 수량 분기 없이 Flights.Phases를 표시 클립이 그대로 참조한다. 단독·3기 조합·다수/변환과 모든 드론 조합의 상승0.5초/0.65셀·시간차 호버·빠른 돌진, 프로펠러4프레임·선회/표적 예고 부재를 모델/실제 그래픽 검사로 확인했다. 돌진 중 소실은 실제 효과 시간과 진행 중 위치에서 정지→호버→새 목표 돌진을 렌더러에서 확인했다. 같은 비행에서 두 번의 돌진 중단을 별도 촬영했다고 주장하지 않는다. 반복 선택 이력/각 구간 검증과 실제 돌진 소실 화면 검증의 범위를 구분한다.
- 표시/수명:4파워·10조합의 변환·공격 도착·본체/레이어·최종 화면/논리·입력 잠금·일시정지/재개·회전·재시작·준비/재생 취소·객체/아틀라스 반환 검사를 유지했다. 표시기가 피해/미션을 다시 적용하거나 final 보드로 목표 소실을 역추정하지 않는다.
- 원래 고정 관찰16행도 최종 Current로 재실행했다. 입력/시드/행 순서 보존, 원문 동일11행. 나머지2행은 빈 Flights 메타데이터만,3행은 비행 이력·조회 진단 문구(캐시 구성2→6)·표시 Attack.FlightDuration/시간 변화다. 실제 상태/효과·미션·난수와 공격3/반응12개의 원래 Record/순서는 동일하다. `fixed-observation-value-comparison.json`, `fixed-context-metadata-audit.json`, `fixed-timeline-record-audit.json`과 양쪽 전체 원문에 근거를 보관했다. 최초 PowerShell 배열 비교는 actualMissionProgress의 동일 배열을 차이로 오인했으며 초기 보고를 남기고 값 단위 비교로 정정했다.
- 사용자 승인된 실제 반복 선택의 별도 밀집 연쇄는 명중 결과가 달라진다. 조합8/시드10 사례의14→10비행 기록과 요청1의 null/hit0→(1,6)/hit44를 원문으로 기록했다. 이 증거는 착탄 기록 대조이며 해당 사례 전체 상태/난수를 원문 대조했다고 주장하지 않는다. 정상18182행의 논리 차이0을 모든 가능한 연쇄 결과 불변으로 확대하지 않는다.

### 최종 실행 운영과 제약

- 후속 연결 session78021은 종료된 부모 프로세스의 ExitCode가 null이라 중단됐다. 기존30종 실패가 아니며 session43633의 실제 종료0과 각30 Unity 종료0을 직접 확인했다. 실패/null 영수증을 보존하고 `observed-regression-terminal-recovery.json`에 관찰을 기록했다.
- 독립 감사 session60652 실제 종료0 후 최종 신규9종+보존 감사 session57144를 순차 실행했고 실제 종료0을 확인했다. 중복 Unity 실행이나 기존30종 재시작은 하지 않았다.
- Unity Editor 그래픽/Play Mode를 검증했다. 빌드·실제 모바일 기기 실행·Addressables 콘텐츠 빌드·디스크 팩 재생성은 하지 않았다. 저장/카탈로그 UI·표현/리소스/풀 전환은 구현하지 않았다.

## 다음 인계 — 큰 구간3

[계획서](../../../Planning/MoonRabbitJunkyard/ElementFramework/phase-03-storage-authoring-plan.md) · [목표/완료 조건](../../../Goals/MoonRabbitJunkyard/ElementFramework/phase-03-storage-authoring-goal.md) · [전체 복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/phase-03-command.md).

정의 ID 저장·구형 읽기/선택 변환·50레벨 팩2·카탈로그 검색/선택/검사의 MVVM을 A~D 기능 묶음으로 진행하도록 준비했다. 실행 상태에 실제 선택 정의를 연결하는 조건과 원본 보존·빌드/팩 생성 금지를 명시했다. 3단계 구현은 시작하지 않았다.

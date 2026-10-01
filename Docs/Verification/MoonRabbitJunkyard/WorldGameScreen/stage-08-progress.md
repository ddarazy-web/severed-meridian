# 8단계 실행 기록

계획: Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-plan.md
상태: 구현·Editor 검증 완료 (2026-10-01). 플레이어·Addressables 콘텐츠 빌드 미실행. 아래 과거 진행 상태보다 최종 감사가 우선한다.

## 최종 완료 조건 감사

| 요구사항 | 현재 근거 |
| --- | --- |
| 실제 효과 구성·전체 Editor 컴파일 | `creation-final.log` 정상 컴파일/종료 0. BuildClips 구현과 모든 호출부 존재 |
| 네 파워·10조합·생성·연쇄·마지막 파워 | Run 203 PASS, RunScene 920 PASS. 네 파워/10조합 전체 재생 및 연쇄, 다섯 색상 매칭·네 생성 파워 피격 보호. 기존 Settlement RunScene의 승/패·라스트팡·속도 변경 사례도 최신 재실행 통과 |
| 공격 전/도착/손상/완료 | Run의 로켓 대상 유지 및 실제 Transform 접촉, 드론 4컷, 자석 변환 직전/직후, 장애물 각 유효 타격 직전/직후, 종료 잔상 검증. fixture/scene mid·contact 캡처 |
| 1×1/2×2·반복·레이어·충전 | 5종×생존/파괴, 본체 단일 이미지·모든 유효 피해, 발전기 완충/철거·간접 제거, web/dust/mold, Protected/AlreadyDamaged/Wall 중간 상태 검증 |
| 규칙 결과 동등성 | RunScene 네 파워/10조합에서 직접 실행기의 전체 공개 상태 스냅샷·Phase·Outcome 대조. Settlement RunScene 승/패·라스트팡 및 시간 변경 동등성 |
| 입력·결과·수명 | 준비/재생 잠금, pause·회전, 다시하기 5회·객체 누적 없음, 효과 프레임 누락 실패, 준비 중 세션 객체 제거, Swap/Settlement RunScene 실제 씬 재진입 통과 |
| 동시 낙하·대각선·교환 | 최신 Settlement Run 282/RunScene 106, Swap Run 237/RunScene 26, FreshSupply 170. 기존 동시 낙하·신규 공급 조건 유지 |
| 리소스·Asset/MemoryPack | 필요한 종류별 아틀라스 준비, pending 종료 후 0개 반환, 누락 실패. EditorLaunch RunData 9 PASS. Asset/Scene/ProjectSettings/Packages의 신규 변경 없음 |
| 문서·인계 | 계획/목표 체크, 사용 안내·조합 표·시간 조정, 목차·로드맵 갱신. 상대 링크·diff·신규 소스 meta 확인 |

### 최종 실행 결과

| 진입점 | PASS | 결과 파일/로그 |
| --- | ---: | --- |
| PuzzlePowerAnimationVerification.Data | 99 | `Logs/Stage08/data-results.txt`, `body-arrival-green.log` |
| PuzzlePowerAnimationVerification.Run | 203 | `Logs/Stage08/render-results.txt`, `creation-final.log` |
| PuzzlePowerAnimationVerification.RunScene | 920 | `Logs/Stage08/scene-results.txt`, `contact-scene.log` |
| PuzzleSettlementAnimationVerification.Run | 282 | `Logs/PuzzleSettlementAnimationVerification/results.txt` |
| PuzzleSettlementAnimationVerification.RunScene | 106 | `Logs/PuzzleSettlementAnimationVerification/scene-results.txt` |
| PuzzleSwapAnimationVerification.Run | 237 | `Logs/PuzzleSwipeSwapVerification/results.txt` |
| PuzzleSwapAnimationVerification.RunScene | 26 | `Logs/PuzzleSwipeSwapVerification/scene-results.txt` |
| PuzzleEditorLaunchVerification.RunData | 9 | `Logs/PuzzleEditorLaunchVerification/data-results.txt` |
| SettlementVerification.FreshSupply | 170 | `Logs/FreshDiagonalVerification/manual-results.txt` |

전체 2052개 PASS, FAIL 0. 반복 프레임/조건 검사도 포함하므로 독립 시나리오 수로 해석하지 않는다. 최신 회귀 5개 로그는 `Logs/Stage08/regression-*.log`다. 검사 과정의 예상 이미지 누락 오류 로그는 실패 경로 검사의 입력이며 해당 검사의 기대 결과를 별도로 확인했다. 모든 소유 검사 프로세스는 정상 종료했다.

### 기록 생성과 소비 경계

| 결정 지점 | 보존 기록 | 표시 소비 |
| --- | --- | --- |
| PowerEffectResolution.PushRange | 소모 전 Origin/Center, 실제 Targets, HitGroup/ParentHitGroup, Power/Area/Direction | 로켓/폭탄/자석/드론 선행 범위와 연쇄 원인 |
| DroneTargetManager.Land 직후 | 실제 착탄점, 원래 출발점, WaitForAttacks | 로터 대기·비행·착탄. 표적 재선정 없음 |
| PowerCombinationResolution.Prepare | 실제 조합/변환/선택 색 | 10개 조합과 0.35초 변환 |
| 실제 피해·GeneratorRules 처리 뒤 | 내구도/덮개/먼지/충전 전후, RemovedObstacleIndices | 단일 본체 단계와 간접 제거 |
| MatchResolution | MatchedBlockChange/생성 종류 | 다섯 색상 제거와 신규 파워 보호 |
| 세션 행동·연쇄·아이템·라스트팡 | 실행 전 사본 + 실제 결과 | 같은 BeginEffects 경계, 모든 효과 완료 후 동시 정착 |

초기 `asset-inventory.txt`와 Priority2 목록(76프레임)을 대조했고, 최종 Run에서 모든 사용 경로를 실제 아틀라스로 로드했다. Match/PowerCreation/Rocket/Drone/Magnet/WoodBreak/MetalBreak/WebBreak/DustClear/MoldClear/BombExplosion과 GeneratorCharge는 분리된 Effects 아틀라스, 로켓/드론 본체는 PowerBlocks 아틀라스를 사용한다. 드론 시트만 Single import로 준비했으며 원본 픽셀 재생성은 하지 않았다. 기존 decoration prefab을 효과 객체 풀에 재사용해 별도 프리팹이 필요하지 않았다.

Final: Ruling: 브랜치 통합 메뉴 대신 현재 미커밋 작업 유지 — 사용자 명령의 커밋/푸시 금지와 기존 dirty 작업 보존을 우선한다. 잘못 판단한 경우 통합 시점만 늦어지며 사용자 파일을 제거하지 않는다.

보류한 minor 없음. 배포 번들·모바일 실기기 검증은 수행하지 않았다. 빌드 금지 범위에서 Editor 검증을 완료했으며 이를 배포 검증으로 확대하지 않는다. 9단계 소리/진동/미션 비행/승패 상세 연출은 구현하지 않았다.

## 최신 검증 — 도착/본체 반응 분리와 수명 (2026-10-01)

이 절이 아래 이전 재개 목록보다 우선한다. 현재 기존 낙하·교환·에디터 진입 최종 회귀 실행 중이다.

- Final: fixed 자석 변환 조기 제거/재등장 — Data 조합 6 RED→GREEN, 세 변환 조합의 0.2/0.38초 Play Mode 검사 통과. 원본은 변환 완료까지 유지하며 이미 발동한 파워를 다시 만들지 않는다.
- Final: fixed 반복 본체 반응이 로켓 도착 경로를 변경함 — `body-arrival-red.log`에서 상향 로켓 (8,4)→폐가전 (0,4)의 실제 도착 시간 실패를 확인했다. `body-arrival-green.log` Data 통과, `body-arrival-render.log` Run 157 PASS. 실제 로켓 Transform이 (1,4)에 도착하는 프레임에 내구도 9→8, 뒤따른 본체 pulse에서 8→7을 확인했다.
- 표시 결정: `Attack.ImpactAt`은 기하 도착으로 고정한다. 같은 HitGroup/본체의 유효 Damage 기록에는 정렬된 물리 접촉 시각을 배정하되 원본 Target·피해 전후 값·기록 순서를 보존한다. 본체 반응은 접촉 이후 최소 0.06초 간격으로 표현한다. 이 지연을 로켓 이동/착탄 시간에 섞지 않는다. 간접 제거는 제거될 본체의 앞선 반응 이후에 적용한다. 부모/자식 공격 전체를 뒤로 미루어 순환하는 시간 제약을 만들지 않는다.
- `layers-lifetime-final.log`: Run **183 PASS**, 종료 코드 0. 기존 전체 fixture에 곰팡이·Protected·AlreadyDamaged·Wall의 실제 규칙 결과를 추가하여 중간 내용물/내구도 유지와 잘못된 손상 효과 없음을 확인했다.
- 효과 프레임 누락은 정상 로드된 다른 아틀라스를 검사 메모리에 넣어 재현했다(원본 에셋 수정 없음). 준비 실패 메시지, 재생 잠금 해제, 아틀라스 0개, 잔상 없음을 확인했다. 다른 사례는 실제 `preparingEffects=true` 상태에서 세션 객체를 제거하고 pending 로드 완료 후 아틀라스 0개·잔상 없음을 확인했다. 초기 검사 오류는 세션이 소유·폐기한 레벨을 재사용한 것이며, 검사 입력을 각각 복제해 수정했다.
- `contact-scene.log`: 실제 씬 RunScene **920 PASS**, 종료 코드 0. 네 단독 파워와 10개 조합 전체 연쇄의 보드·난수·이동·공급·미션·회수·Phase·Outcome이 직접 실행기와 같다. frame별 패널 검사가 포함되므로 920개 독립 시나리오라는 뜻은 아니다. 타격 시점 추가 Tick으로 이전 1161건보다 반복 횟수가 줄었다.
- `scene-power-0-contact.png`에서 실제 게임 UI와 로켓/나무 장애물 접촉, `scene-power-2-contact.png`에서 드론 착탄을 직접 열어 확인했다. 각 fixture의 mid/contact 캡처가 있고 mid는 1280×720/450×800, contact는 pause/회전 검사 뒤 반대 방향이다. `rocket-body-contact.png`는 별도 정밀 위치 검사 캡처다.
- 독립 리뷰 중요 2건은 위 RED→GREEN으로 수정했다. 새 리뷰는 요청하지 않는다. 리뷰에서 확인이 부족했던 무효 반응·곰팡이·준비 실패/종료·착탄 캡처 증거도 보완했다.
- 아직 빌드·새 이미지 생성·새 패키지·자동 커밋을 하지 않았다. 최종 회귀 결과와 문서 완료 조건 감사가 남았다.

## 최신 구현 상태 — 독립 리뷰와 변환 순서 수정 (2026-10-01)

- 독립 읽기 전용 리뷰 1회를 수행했다. 중요 2건(변환 대상 조기 제거/재등장, 반복 본체 타격 시 로켓 위치와 반응 지연 불일치)을 확인했다. 추가 리뷰는 요청하지 않는다. 아직 전체 완료가 아니다.
- 자석 변환의 공격 없는 시작 기록이 0초에 처리되는 오류를 `transform-red.log` / Data 조합 6 실패로 재현했다. 변환 완료 시점 0.35초에 처리하도록 수정했다. `transform-green.log` 종료 코드 0, Data **95 PASS**. 실제 Play Mode에서 세 변환 조합의 원본 유지·발동 후 재등장 없음 검사 및 0.38초 캡처를 추가했다. `transform-render.log` 종료 코드 0, **154 PASS**. `transform-active-10.png`를 직접 열어 로켓 변환 표시를 확인했다. 전체 회귀와 두 번째 중요 수정은 남았다.
- 기본 로켓 이동은 방향별 전체 거리에 최소/최대 비행 시간을 적용하고 각 칸을 같은 거리 비율로 통과하도록 수정했다. 한 칸/네 칸의 1:4 비율 검사를 통과했다. 다만 같은 2×2 본체를 역방향으로 연속 타격하면 기록 순서 보존용 지연이 실제 경로와 어긋나는 문제는 이 수정으로 해결되지 않는다.
- 장애물 5종의 생존/파괴 10개 fixture에서 유효 타격 직전·직후 내구도, 2×2 단일 본체 및 반복 타격, 최종 잔상을 검사했다. `obstacle-frames.log` 당시 148 PASS, 변환 검사 추가 후 최신 Run 154 PASS에 포함된다. 예정된 반응 시간에 대한 검사이며 공간상의 도착 정합성 증거로 확대 해석하지 않는다.
- 기존 스와이프 검사에 비동기 효과 준비 대기를 반영했다. 콘텐츠 교환 검사에서 빠진 await 때문에 최종 상태 비교가 먼저 실행된 실패를 확인하고 해당 호출에도 await를 추가했다. `swap-await-green.log`의 Run 종료 코드 0, `Logs/PuzzleSwipeSwapVerification/results.txt`에서 최신 결과를 확인했다. 별도 RunScene은 아직 재실행하지 않았다.
- FreshSupply 검사는 `-quit`가 필요한 메서드였다. 누락 실행 후 유휴 상태인 소유 검사 PID 79920만 확인하여 종료했고, `-quit`를 추가한 재실행은 종료 코드 0이었다. 사용자 Editor를 종료하지 않았다. `Logs/FreshDiagonalVerification/manual-results.txt`의 신규 공급/최장 경로 검사가 통과했다.
- 효과 사용 안내 초안을 `Docs/Guides/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-usage.md`에 작성했다. 완료/목차 갱신은 최종 검증 후 수행한다.
- 남은 중요 수정: 위쪽으로 발사되는 로켓 (8,4), 폐가전 anchor (0,4)에서 실제 각 칸 도착과 유효 반복 타격을 함께 재현하고 시간표·이동을 일치시킨다. 무효/보호·곰팡이 중간 상태, 효과 로드 실패·준비 중 씬 종료, 최신 실제 씬·Asset/MemoryPack 진입·교환 회귀도 남아 있다.
- 이미지 재생성은 필요할 때 허용된 상태이며 이번 수정은 기존 이미지를 유지했다. 플레이어/Addressables 콘텐츠 빌드 및 커밋은 하지 않았다.

## 최신 구현 상태 — 실제 씬·매칭 레이어 검증 (2026-10-01)

이번 목표 턴은 실제 씬 검사 추가와 표시 결함 수정으로 진전했다. 아래 과거 기록보다 이 절이 우선한다.

- 기존 `PuzzleSettlementAnimationVerification.RunScene`을 비동기 효과 준비와 실제 종료 경계에 맞췄다. 축소·알파·HUD·동시 공급·회전·승패·다시하기 5회·씬 재진입 조건은 유지했다. `Logs/Stage08/settlement-scene.log`: 종료 코드 0, `Logs/PuzzleSettlementAnimationVerification/scene-results.txt` **106 PASS**.
- 새 `PuzzlePowerAnimationVerification.RunScene`은 실제 게임 씬을 MemoryPack으로 시작한 뒤 네 파워와 10개 조합 fixture를 같은 세션·보드·UI에서 실행한다. 각 fixture의 전체 연쇄 종료 후 직접 실행기와 공개 상태 스냅샷(보드·난수·이동·공급·미션·회수) 및 Phase/Outcome을 비교한다. pause와 가로/세로 회전, 결과 패널 조기 표시 차단, 효과 잔상 없음, 원래 MemoryPack 재시작, 로드/재생 중 재시작 5회를 검사한다. `power-scene.log` 종료 코드 0, `scene-results.txt` **1161 PASS**. 프레임별 패널 검사도 개수에 포함되므로 독립 시나리오 1161개라는 뜻은 아니다.
- 실제 UI를 포함한 `scene-power-0-mid.png`(1280×720), `scene-power-1-mid.png`(450×800)를 열어 확인했다. 각 fixture 캡처 14장은 Logs/Stage08에 있다. 아래 후속 로켓 보정 이후 실제 씬 캡처 최종 갱신은 남았다.
- 캡처에서 가로 로켓 발사 3·4컷의 원본 본체가 대기/2컷보다 위에 배치된 차이를 확인했다. 프레임별 y 보정으로 비행 높이를 맞췄다. 또한 궤적 오버레이가 본체를 덮지 않도록 본체 SortingGroup을 45, 효과를 40으로 분리했다. 수정 후 fixture-0 캡처에서 양방향 본체와 궤적 구분을 직접 확인했다.
- 직접 매칭의 거미줄·먼지 변화는 EffectRecord에 없고 MatchedBlockChange/매칭 전후 상태에만 있어 효과가 누락됐다. `match-layer-red.log`에서 재현 후 클립 구성에 추가했다. 먼지는 같은 좌표의 타격 기록과 중복시키지 않는다. `match-layer-green.log` 및 최종 `body-order.log` 모두 종료 코드 0, `render-results.txt` **63 PASS**.
- 사용자 후속 지시: 보정으로 해결하기 어려운 경우 이미지 재생성을 허용한다. 기존 '새 이미지 금지'보다 이 지시가 우선한다. 재생성 시 본체 크기·중심·방향을 고정하고 움직이는 부분만 달리하며 2의 거듭제곱 크기를 유지한다. 이번 턴에는 이미지를 재생성하지 않았다.
- 모든 검사 프로세스 정상 종료. diff 공백 검사 통과. 플레이어·Addressables 콘텐츠 빌드 미실행.
- 남은 감사 항목: 로켓의 실제 이동 위치와 칸별 ImpactAt 시간 일치(현재 선형 이동과 칸별 최소 시간 차이를 검토해야 함), 1×1/2×2 반복 유효 타격과 무효/보호 반응·곰팡이 중간 상태, 에디터 Asset/MemoryPack 진입·신규 대각선·스와이프 회귀, 취소/누락 리소스 수명과 실제 씬 최종 캡처, 사용 안내·목차·완료 조건별 증거 및 최종 리뷰. 아직 목표 완료 아님.

## 최신 구현 상태 — 세션·발전기 연결 (2026-10-01)

이 절이 아래 이전 재개 기록보다 우선한다. 직전 목표 턴은 클립 구현과 검증으로 진전했고, 이번 턴은 세션 연결과 간접 제거 표시를 구현했다. 막힘이나 대기 승인은 없다.

- 세션의 파워 탭·성공 교환·아이템·자동 연쇄/종료 단계에 실제 PowerTrace/Effects를 전달했다. 비동기 준비도 IsPresenting에 포함한다. 준비 중 입력·아이템·다음 연쇄를 막으며 이전 로드의 취소·완료가 재시작 보드를 덮어쓰지 않도록 요청별 재생기와 CancellationTokenSource를 사용한다. 수명 경계의 전체 회귀는 아직 남았다.
- 교환 종료 직후 표시 사본에서는 점유자 필드만 바꾸고 고정 레이어는 유지한다. 일반 매칭의 0.12초 축소·투명도를 새 효과 재생기에 유지하며, 생성 파워로 교체된 후 이전 축척을 다시 적용하지 않도록 정리했다.
- EffectRecord.RemovedObstacleIndices를 추가했다. 실제 타격 중 생성된 발전기 Activated/Disconnected/Retired 기록과 직접 파괴를 일회성 목록으로 묶는다. 피해·표적·난수·저장 DTO 변경 없이 타격 시점의 본체·연결 제거와 파괴 효과에 사용한다.
- 드론이 선행 공격 완료를 기다리는 구간에도 같은 출발점에서 프로펠러 4컷을 반복한다. 실제 비행이 시작되면 대기 효과를 숨긴다.
- RED: `session-red.log`에서 도착 전에 대상 제거, `generator-red.log`에서 간접 제거 기록 누락, `drone-wait-red.log`에서 대기 중 본체 누락을 각각 확인했다.
- GREEN: `data-results.txt` **91 PASS** (generator-green.log). `render-results.txt` **60 PASS** (GameScreen-Editor-PuzzlePowerAnimationVerification-Run-integration.log). 네 파워·10개 조합의 기본 재생, 드론 대기 표시, 발전기 완충/자동 철거 전후 두 본체 유지·제거, 실제 세션 로켓 도착과 입력 잠금을 포함한다.
- 기존 낙하 검사의 고정 0.12초 종료 가정을 새 효과 종료와 비동기 준비 경계로 갱신했다. 중간 축소·알파·낙하·직접 실행기 동등성 조건은 유지한다. `settlement-integration-green.log`: **282 PASS**, 종료 코드 0. 이후 생성 파워 축척 보완에 대한 최종 재검사는 아래 실행 결과로 기록한다.
- 빌드는 하지 않았다. 이번 결과는 Editor/Play Mode 검증이며 번들/실기기 검증을 뜻하지 않는다.
- 최종 재검사: `Levels-Editor-GeneratorVerification-Data-final.log`와 `GameScreen-Editor-PuzzleSettlementAnimationVerification-Run-final.log` 모두 종료 코드 0. 생성 파워 축척 보완 후에도 제거·낙하 **282 PASS**. 관련 검사 프로세스는 정상 종료했고 diff 공백 검사는 통과했다.
- 남은 필수 작업: stage-08 RunScene 구현·가로/세로 실제 씬 검증, Asset/MemoryPack 진입과 네 파워/10개 조합의 전체 세션 동등성, 반복 타격·덮개/먼지·보호 반응 검증, pause/회전/재시작 5회/로드 취소/씬 재진입, 로켓 이동 위치와 대상별 도착 시간 검수, 기존 RunScene/신규 대각선/스와이프 회귀, 사용 안내와 최종 리뷰. 모든 완료 조건은 아직 미체크다.

## 최신 재개 상태 (2026-10-01, 계획 갱신 시 확인)

### 후속 구현·검증: 효과 클립 구성

아래 계획 갱신 당시의 미정의 호출 문제는 이번 구현으로 해결했다. `PuzzlePowerPlayback.Clips.cs`에 실제 기록을 소비하는 로켓 양방향·십자 범위, 드론 시트 비행·표적·착탄, 폭발, 자석 흡인/변환, 매칭·생성, 장애물·덮개·먼지 효과 구성을 추가했다. 로켓의 0.06초 발사 준비, 가로 크기·중앙 보정도 반영했다. 아직 세션 연결 전이며 완성 판정은 하지 않는다.

- `GameScreen.Editor.PuzzlePowerAnimationVerification.Data`: **86 PASS**, Editor 종료 코드 0. `Logs/Stage08/clips-data.log`.
- `GameScreen.Editor.PuzzlePowerAnimationVerification.Run`: **47 PASS**, 종료 코드 0. 네 단독 파워·10개 조합을 실제 아틀라스로 준비하고 재생 종료·활성 효과 잔상 없음을 확인했다. 로켓은 출발 전/도착 전 대상 유지와 도착 후 제거를 별도 확인했다. `Logs/Stage08/power-captures.log`, `render-results.txt`. 이는 모든 타격 시각·상태 동등성·게임 세션을 검증한 결과가 아니다.
- 가로 1280×720/세로 450×800 fixture 캡처 14장과 드론 4프레임 캡처를 남겼다. 로켓 양방향과 폭발, 드론 네 컷을 직접 열어 확인했다. 드론은 이웃 시트 칸 누출 없이 한 컷만 표시된다. 실제 게임 씬 UI를 포함한 검증은 남았다.
- 같은 프레임 안에서 수동 Tick을 반복하는 검사에서는 새 SortingGroup 갱신 전 캡처가 효과를 가렸다. 캡처 직전 `SortingGroup.UpdateAllSortingGroups()`를 호출해 실제 렌더 순서를 반영했다. 이 수정은 검사 코드에만 적용했다.
- 런타임은 기존 Addressables 아틀라스 경로를 사용한다. 이번 검사는 Editor Play Mode 검사이며 플레이어 번들/실기기 검증을 대체하지 않는다. 플레이어·Addressables 콘텐츠 빌드 미실행.
- 최신 전체 컴파일 성공 및 diff 공백 검사 통과. Unity가 신규 .meta를 생성했다. 검사 프로세스 정상 종료.
- 다음: 발전기 연결 장애물의 간접 제거 기록·재생, 반복 타격/레이어 검사, 표시 중 비동기 로드 취소와 세션 연결, 기존 제거 축소·동시 낙하 보존, 실제 씬/수명/상태 동등성 검사. 드론 선행 공격 대기 구간의 본체 표시와 로켓 거리별 도착 경로도 세션 연결 전에 강화한다.

이 절이 아래 과거 진행 기록보다 우선한다. 사용자의 계획·목표·명령문 요청에 맞춰 문서를 갱신했으며 이번 문서 작업에서는 소스를 변경하거나 빌드를 실행하지 않았다.

- 표시 시간표 `PuzzleEffectTimeline`과 기존 데이터 검사 확장이 존재한다. 현재 `Logs/Stage08/data-results.txt`에 기록된 통과 결과는 최신 재생기 초안 작성 전의 검사이며 현재 전체 코드 검증이 아니다.
- `PuzzlePowerAssetPreparation.Prepare`로 드론 시트 Single import와 PowerBlocks 아틀라스 작성이 수행됐다. 콘텐츠 빌드는 하지 않았다. 기존 기록의 ‘대안 미구현’ 상태를 대체한다. 런타임 4컷 마스크 검증은 남아 있다.
- `PuzzleArtwork.PrepareEffectsAsync`, `PuzzleEffectSprite`, `PuzzlePowerPlayback` 초안 및 보드 효과 재사용 경계가 존재한다. 현재 `BuildClips(before)`는 호출만 있고 정의가 없다. 따라서 컴파일 정상 상태가 아니며, 재개 시 가장 먼저 실제 효과 구성 구현을 완성해야 한다. 임의 삭제·복원은 하지 않았다.
- `PuzzleGameSession.Presentation.cs`에는 파워 재생기가 아직 연결되지 않았다. 기존 제거·정착 재생만 사용한다.
- `render-results.txt`의 실패는 재생기 타입을 추가하기 전 확보한 RED다. 최신 초안의 Play Mode 결과로 해석하지 않는다. 모든 완료 체크는 유지해서 미완료로 남긴다.
- 다음 실행 순서: 재생기/컴파일 → 로켓·드론 → 폭탄·자석·조합 → 장애물·레이어 → 세션·수명·회귀 검증. 자세한 시작점은 갱신된 계획서에 기록했다.

## 기준과 결정

- 작업 1 진행 중; 작업 2~5 미시작. 시작 dirty 목록: Logs/Stage08/baseline-status.txt. 기존 work 브랜치 작업을 유지한다.
- Ruling: 사용자 지정 ServeredMeridian의 현재 작업 공간에서 실행 — 검증된 미커밋 7단계 및 동시 낙하에 의존하며 새 worktree는 해당 상태를 누락한다. 커밋/푸시 없음.
- Ruling: 실행 ledger는 이 문서와 Logs/Stage08에 둔다 — 프로젝트 Docs 분류와 Windows 환경에 맞추며 별도 shell 스크립트/패키지를 설치하지 않는다.
- Pre-flight: 작업 1의 표시 기록·아틀라스 소유권을 작업 2~4가 사용하고, 작업 5가 같은 Begin/Tick/Reset 경계에 연결한다. 피해 계산/난수/저장 DTO와 표시 시간표를 분리한다.
- 조사: EffectRecord는 유효 반응이 발생한 좌표만 남긴다. 드론 광역 착탄에서는 Source가 착탄점으로 바뀌며, 조합 재료는 기록 전 소모된다. 따라서 효과 목록만으로 발사 주체/착탄점을 추정하지 않는다. 실제 결정 지점의 일회성 발사 기록을 추가한다.
- 기존 아틀라스 로더는 Addressables만 사용한다. 빌드를 실행하거나 개별 텍스처 로드로 우회하지 않는다.

## 검증

아직 8단계 검증 완료 항목 없음. 플레이어·Addressables 빌드 미실행.

## 작업 1 진행 근거 (2026-10-01)

- PowerPresentationTrace/PowerAttackRecord를 추가했다. 발사 HitGroup·ParentHitGroup·Origin·Center·Power·Direction·Area·Targets와 드론 선행 공격 대기 수를 기록한다. 조합 정보를 보존한다.
- PowerEffectResolution은 실제 Range 선택/드론 Land 직후 표시 기록만 추가한다. 한 라스트팡 결과의 여러 Apply 호출은 같은 trace에 누적한다.
- TurnEffectContext.Copy는 trace 참조를 보존하며 새 효과 실행은 새 trace를 만든다. BoardActionResult/CascadeStepResult/ItemUseResult에 직접 전달하여 이전 연쇄 trace를 잘못 재생하지 않게 했다. 저장 DTO 변경 없음.
- RED: Logs/Stage08/trace-red.log에서 '실제 발사 결정의 일회성 표시 기록 제공' 실패. 최초 테스트 컴파일 오류는 좌표 Equals 비교로 수정한 후 의도한 실패를 확인했다.
- GREEN: GameScreen.Editor.PuzzlePowerAnimationVerification.Data 53 PASS, PowerEffectVerification.Data 96 PASS, CombinationVerification.Data 381 PASS, CascadeVerification.Data 65 PASS, RoundEndVerification.Data 22 PASS, ItemBoosterVerification.Data 21 PASS. 합계 **638 PASS**, 0 FAIL. 중간 안내의 738은 합산 오기이며 638로 정정한다.
- 세부 결과: Logs/Stage08/data-results.txt 및 각 기존 검사 Logs 폴더. 각 batch Editor는 정상 종료했다. 현재 실행 중인 검사 프로세스 없음.
- 자원 읽기 전용 검사: GameScreen.Editor.PuzzlePowerAnimationVerification.Assets, Logs/Stage08/asset-inventory.txt. 드론 512 시트는 mode=None이며, 로켓 개별 2~4프레임은 Single이다. 발사 전 본체를 1프레임으로 재사용 가능하다. 효과/충전 프레임은 Single로 존재한다.
- SpriteDataProviderFactories/ISpriteFrameEditCapability가 현재 Editor에 없다. Sprite Editor 스킬의 분할 API 작업은 수행하지 않았고 .meta를 직접 수정하지 않았다. 새 패키지 금지이므로 설치하지 않는다.
- 다음 조사/구현: 드론 전체 시트를 Single Sprite로 기존 PowerBlocks 아틀라스에 포함하고 표시용 마스크/프레임 위치로 4컷을 보여주는 대안. 이는 기존 픽셀을 변경하거나 새 이미지를 생성하지 않는다. 아직 이 대안을 구현하거나 확정하지 않았으며, 대기/비행/마스크 정확성 및 아틀라스 import를 검증해야 한다. manage-sprite-atlas 스킬을 읽었으므로 다음 작성 전 해당 resources/authoringvsruntime.cs와 references/common-errors.md 및 저장 예제를 읽는다. 기존 Addressables 배포 선택은 이미 승인되어 있다. Generate/Prepare는 전역 변경·콘텐츠 빌드 연결이 있으므로 호출하지 않는다.
- 다음 남은 작업: 실제 표시 시간표·PowerPlayback·필요 아틀라스 준비/해제·타격별 시각 상태·세션 연결·통합검증. 작업 1도 아직 완료 아님. 어떤 단계도 완료 체크하지 않는다.

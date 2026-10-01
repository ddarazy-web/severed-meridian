# 7단계 제거·낙하·채움 진행 기록

계획: [stage-07-settlement-plan.md](../../../Planning/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-plan.md)

상태: 구현·Editor 검증 완료 (2026-10-01). 필수 완료 조건 9개를 아래 증거와 대조했다.

## 실행 기록

- 실행 계획 및 Unity 프로젝트 규칙을 적용한다. 현재 work 디렉터리의 미커밋 작업을 전제로 직접 실행한다. 별도 checkout/커밋은 만들지 않는다.
- 시작 상태 및 데이터·프리팹·씬·Addressables·ProjectSettings SHA256을 `Logs/PuzzleSettlementAnimationVerification/baseline-*`에 기록했다.
- 열린 Unity는 다른 프로젝트이므로 조작하지 않는다. 이 프로젝트 검사 프로세스만 별도로 사용한다. 빌드 금지.
- Pre-flight: 작업 1의 표시 스냅샷은 작업 2의 좌표별 이동 소유권과 작업 3의 규칙 호출 전 캡처에 공통으로 필요하다. 제거와 정착 모두 재생 소유 표시를 유지하고 Reset에서 반환한다.
- Ruling: 별도 스킬 스크립트의 커밋 기반 작업 추적 대신 이 기록과 기존 작업 디렉터리의 파일·해시를 사용한다. 사용자 미커밋 변경 보존 및 커밋 금지 조건을 우선한다.
- 작업 1 진행: 교환 종료 뒤 제거 표시가 유지되어야 하는 회귀 검사를 먼저 추가한다.
- `task1-red-results.txt`에서 교환 직후 제거 재생 부재를 확인했고 `task1-green-results.txt`에서 제거 중간 알파·연쇄 잠금·생성 파워 보존 6개 검사를 통과했다.
- `task2-red-results.txt`에서 정착 즉시 표시를 확인하고 `task2-green-results.txt`에서 기본 Batch 재생·자동 매칭 잠금까지 10개 검사를 통과했다. 특수 경로는 아직 별도 검증 중이다.
- 파워 탭 검사 초안은 비활성 (0,0)을 사용해 잘못 거절을 검사했다. 활성 칸 확인을 추가하여 수정했다. `task3-green-active.log` 실행에서 파워 제거와 전체 연쇄의 직접 실행기 상태 동일성을 통과했다. 이전 실패는 파워 재생 부재의 독립 증거로 사용하지 않는다.
- 현재 네 방향 중력·대각선 동시 Batch·지정 경로·포털·내부 공급/소진·회수 도착 검사를 추가해 실행한다. 아직 모든 작업의 완료 조건을 충족한 것은 아니다.
- 경로 검사에서 회수 부품과 기존 일반 블록의 배치 충돌을 수정했다. 현재 fixture는 기존 블록을 지운 뒤 실제 회수 부품을 배치하고 검증 오류를 확인한다.
- 공급 클리핑 부재를 `supply-clip-red-results.txt`로 재현하고 런타임 단색 SpriteMask로 한 칸 안에서만 등장하도록 수정했다. 이미지 에셋·아틀라스 변경 없이 재사용하며 보드 종료 시 생성 Sprite를 반환한다.
- 지정 경로 공급 방향을 `routes-extended-red-results.txt`로 재현하고 경로 방향 우선으로 수정했다. 값 타입 FlowPathCell을 null과 비교한 컴파일 오류는 반복 조회 방식으로 고쳤다.
- 실제 씬 가로 1280×720·세로 450×800의 제거·낙하·공급·착지·연쇄 후 PNG를 캡처하고 열어 확인했다. 공급 마스크 적용 전후를 비교해 상단 넘침이 잘리는 것도 확인했다.
- 기존 교환 검사 첫 실패는 0.15초에 전체 표시가 끝난다는 이전 단계 전제였다. 새 제거 단계 완료를 기다리도록 고치고 기존 규칙 상태 비교를 유지했다. 가로 로켓 중간 이동도 보정된 본체 중심에서 칸 간 변위로 검사한다.
- `swap-regression-green-results.txt`: 기존 교환·실제 입력·파워/장애물·다시하기 검사 통과. `ui-playback-green-results.txt`: Asset 사본/MemoryPack, 망치, pause, 반복 재시작·취소·소유자 종료 검사 통과.
- `scene-lifecycle.log`, `scene-results.txt`: 낙하 중 다시하기 5회와 실제 씬 종료·재진입, 상태/잠금/객체/알파 복원 통과. 최종 UI 시점 검사를 추가해 재확인할 예정이다.
- 첫 경로 실행에는 실수로 같은 경로 묶음을 두 번 호출한 중복이 있었다. 호출을 제거했으며 이후 결과 개수만 최종 집계에 사용한다.

## 최종 검토 반영 (2026-10-01)

- 독립 읽기 전용 검토에서 공급 마스크 공유 범위 문제 1개와 중간 프레임·승패 증거 공백 2개를 받았다.
- `mask-isolation-red-results.txt`에서 인접한 두 공급원 중 아래쪽 이미지가 위쪽 마스크 안에 노출되는 실제 픽셀 실패를 재현했다. 공급 이미지와 마스크를 개별 SortingGroup에 넣고 전역 custom range 대신 그룹 내부 범위를 사용했다. 즉시 렌더 검사에서는 SortingGroup 갱신 후 픽셀을 읽는다. `mask-isolation-green-results.txt`에서 대상 칸 내부 초록 픽셀 유지, 이웃 칸 초록 픽셀 부재를 확인했다.
- 근거 API: [Unity Sprite Mask](https://docs.unity3d.com/6000.0/Documentation/Manual/sprite/mask/hide-reveal-parts-sprite-mask.html). 런타임 단색 마스크와 공급 그룹은 재사용하며 새 이미지 에셋이나 프로젝트 설정을 추가하지 않는다.
- `Frames` 검사는 모든 이동의 중간 위치·동시 개수·Batch 도착 좌표와 동일 renderer 연속성을 확인한다. 포털 출구의 중간 알파와 회수 Batch 직후 숨김은 최종 Draw **전**에 검사한다. 빠른/느린 낙하, 마지막 Batch 뒤 착지 축척, 종료 후 색·축척·렌더 순서도 확인했다.
- `Outcomes` 검사는 실제 게임 씬 UI에 승리/이동 소진 패배 fixture를 적용한다. 재생 시간 0.5배·2배와 pause를 거쳐 보드 전체·난수·Phase·Outcome을 직접 실행기와 비교한다. 모든 재생 프레임에서 결과 패널을 숨기고, 라스트팡까지 완료한 뒤 패널이 나타나는지 확인했다.
- 실제 씬에 고정 공급 3개(같은 색)와 그 뒤 다른 색 1개를 넣은 별도 fixture로 자동 추가 매칭 → 제거 → 재낙하 → 입력 복원을 양쪽 해상도에서 확인했다. `scene-*-automatic-remove-mid/automatic-fall-mid/automatic-end.png`도 직접 열어 확인했다. 첫 fixture는 공급 3개 제거 뒤 더 이동할 블록이 없어서 추가 낙하 assertion에 실패했다. 뒤에 다른 색 블록을 공급하도록 fixture만 보강했으며 규칙이나 assertion을 약화하지 않았다.
- 기존 월드 회귀 배치 프로세스는 검사 파일 기록 및 Editor 종료 절차 후 `Cleanup mono`에 멈췄다. 명령줄·PID와 완료 로그를 확인해 **이 작업에서 시작한 검사 프로세스만** 정리했다. 사용자 Editor는 종료하지 않았다.

## 완료 조건 대조

증거 경로의 기준은 저장소 루트 `Logs/PuzzleSettlementAnimationVerification/`이다. 파일명만 보존한 과거 실패 로그와 최종 결과를 구분한다.

| 완료 조건 | 직접 확인한 증거 |
| --- | --- |
| 실제 교환 → 제거 → 공급 정착 → 연쇄 | `final-scene-results.txt`, 두 방향 `scene-*-start/swap-mid/remove-mid/fall-mid/supply-mid/landing-mid/cascade-end.png`. HUD 이동 수는 제거 중 20, 완료 후 19 |
| 4방향·대각선·경로·포털·동시/다중 Batch | `final-route-results.txt`의 down/up/right/left/path/diagonal-batch/portal. 모든 renderer의 중간 좌표와 도착 소유권 확인 |
| 내부 공급원·방향·주변 클리핑 | supply-stop/supply-path/adjacent-supply 검사. `adjacent-supply-mask.png` 실제 렌더 픽셀 확인 |
| 파워 생성/발동·고철·회수·고정 레이어·소진 | 파워 직접 발동 전체 연쇄, moving-rocket/moving-scrap/recovery/fixed-layers/supply-stop. 회수 직후 최종 Draw 전 비표시, 2×2·벽·덮개·먼지 위치 유지 |
| 동일 규칙 결과·속도/pause 불변 | 전체 상태 스냅샷 및 Phase·Outcome 비교. 실제 씬 승패 각 시간 0.5배/2배, pause; 경로 검사 빠른/느린 중력 |
| 입력·아이템·다음 연쇄·HUD·결과 장벽 | 제거/정착 중 Advance 호출 차단, 기존 입력/UI 회귀, 실제 씬 HUD 시점 및 결과 패널 프레임별 검사. no-records/invalid-flow/missing-renderer 잠금·자원 정리 |
| pause·회전·재시작 5회·종료/재진입 | `final-scene-results.txt`의 낙하 중 회전, 반복 다시하기, 단일 세션 재진입. routes의 색/축척/order 복원; UI 회귀의 취소·소유자 파괴 |
| 가로/세로 연속 화면 증거 | 1280×720, 450×800의 단계별 PNG를 직접 열어 제거 알파·중간 이동·한 칸 공급·착지·연쇄 후 화면 확인. 회전 중 프레임 별도 보존 |
| 기존 회귀와 Asset/MemoryPack | 아래 최종 검사 표. 원본 레벨 저장 없이 두 입력 소스의 게임 진입과 아이템/pause/restart 확인 |
| 문서·변경 범위 | 계획/목표/명령문/사용 안내/인덱스 갱신, 기존 데이터·프리팹·씬·Addressables·설정 104개 파일의 시작 SHA256 대조 |

## 최종 실행 결과

Unity 6000.3.10f1, Windows Editor Play Mode, 기존 Addressables 번들. 아래 다섯 실행의 최종 결과는 **883 PASS / 0 FAIL**이며 동일 검사의 과거 반복 실행은 합산하지 않았다.

| 실행 진입점 (`GameScreen.Editor.` 생략) | 최종 로그 | 결과 사본 | PASS |
| --- | --- | --- | ---: |
| PuzzleSettlementAnimationVerification.Run | final-transforms.log | final-route-results.txt | 355 |
| PuzzleSettlementAnimationVerification.RunScene | final-cascade-layout.log | final-scene-results.txt | 87 |
| PuzzleSwapAnimationVerification.Run | swap-final.log | swap-regression-green-results.txt | 216 |
| PuzzleUIPlaybackVerification.Run | ui-final.log | ui-playback-green-results.txt | 48 |
| PuzzleWorldBoardVerification.Run | world-final.log | world-regression-green-results.txt | 177 |

`Run`의 missing-renderer 오류 로그는 의도적으로 출발 렌더러를 숨긴 실패 정리 fixture다. 최종 결과는 잠금·임시 표시·아틀라스 정리를 확인한 PASS다. 기존 Editor API obsolete 경고는 별도 범위이며 이 작업의 컴파일 오류는 없다.

## 최종 파일 감사

- `final-audit.txt`: 기존 데이터·프리팹·씬·Addressables·ProjectSettings 104개 SHA256 변경 0, 문서 9개의 로컬 링크 오류 0, 새 스크립트 10개 meta 누락 0. 범위 내 `git diff --check` 통과.
- 목표·계획 체크리스트와 로드맵·분류 인덱스를 실제 검증 결과로 갱신했다. 계획 수립 당시의 코드는 시작 시점 근거로 남겼다.
- Ruling: 사용자의 미커밋 변경 유지·커밋/푸시 금지 지시에 따라 기존 `work` 브랜치와 작업 폴더를 그대로 유지한다. 병합·추가 checkout·커밋·푸시는 하지 않는다.

## 완료 여부

- 작업 1: 구현 및 제거 중간 알파·축소·원래 축척 복원 검증 완료
- 작업 2: 구현 및 경로·픽셀·소유권·회수 중간 프레임 검증 완료
- 작업 3: 연결 및 직접 실행기·승패 UI·속도/pause 검증 완료
- 작업 4: 실제 씬·수명·관련 회귀·문서 및 파일 감사 완료

실행한 검사와 증거를 확보한 순서대로 추가한다. 기존 단계의 통과 개수를 이번 단계 완료 근거로 사용하지 않는다.

## 수행하지 않은 항목

플레이어 빌드, Addressables 콘텐츠 빌드, 새 번들 생성, 실제 Android/iOS 기기 성능·터치·프레임률 검증은 수행하지 않았다. 기존 번들을 사용한 Editor 컴파일 및 Play Mode 검증이다. 자동 저장·커밋·푸시는 수행하지 않았다. 새 이미지·패키지·저장 형식·규칙 변경과 8/9단계 상세 효과·소리·진동은 범위 밖이다.

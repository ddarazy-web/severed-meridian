# 프로젝트 문서 안내

Markdown 문서는 **문서 역할 → 프로젝트 → 개발 흐름** 순서로 분류한다. 파일명과 기존 문서 번호는 유지한다. 완료 여부는 각 문서의 상태와 검증 기록을 확인하며, 폴더 위치만으로 완료를 판단하지 않는다.

자동 검증 소스는 Assets 밖으로 분리했다. [테스트 연결·실행 안내](../Tests/README.md)를 확인한다. 기존 계획서와 실행 기록의 테스트 경로는 당시 위치이며 현재 경로 대응표는 안내에 있다.

## 분류별 목차

| 폴더 | 넣는 문서 | 기존 문서 수 |
| --- | --- | ---: |
| [Contents](Contents/README.md) | 게임 기획, 규칙, 스토리, 콘텐츠·아트 명세 | 32 |
| [Planning](Planning/README.md) | 작업 계획서, 개발 로드맵, 단계별 실행 계획 | 38 |
| [Goals](Goals/README.md) | 개발 목표, 범위, 완료 조건 | 36 |
| [Commands](Commands/README.md) | 복사해서 실행하는 목표 명령문 | 3 |
| [Verification](Verification/README.md) | 구현 진행, 테스트 결과, 완료 검증 기록 | 11 |
| [Guides](Guides/README.md) | 실행 방법, 조작, 운영·사용 안내 | 3 |
| [Systems](Systems/README.md) | MemoryPack, 아틀라스, 시스템 계약·구현 경계 | 4 |
| [architecture](architecture/README.md) | 기능별 코드 구조와 아키텍처 | 1 |
| [Decisions](Decisions/README.md) | 설계 선택과 근거, ADR | 4 |
| [Handover](Handover/README.md) | 완료 인수인계와 후속 작업 전달 | 1 |

표의 수는 2026-09-30 정리한 기존 문서 133개 기준이며 새로 만든 분류 목차는 제외한다. 루트 안내까지 포함한 기존 Markdown 134개를 보존했다.

## 달 토끼 고물상 시작점

- 현재 보드 기준: [9×9(81칸) 전환 결정](Decisions/MoonRabbitJunkyard/2026-10-01-nine-by-nine-board.md). 과거 10×10 검증 기록은 당시 조건으로 보존한다.

- [게임 기획서](Contents/MoonRabbitJunkyard/기획서.md)
- [고물탑 쌓기 기획서](Contents/MoonRabbitJunkyard/12_고물탑쌓기.md) — 퍼즐 이외의 별 소비·고물 수집·탑 성장 콘텐츠. 핵심 규칙 정리, 구현 미착수.
- [레벨 튜토리얼 기획서](Contents/MoonRabbitJunkyard/13_레벨튜토리얼.md) — 15개 결정. 데이터·제작과 등록식 진행 엔진은 완료, 실제 게임 연결·안내는 후속 단계. [진행 가이드라인](Planning/MoonRabbitJunkyard/Tutorial/integration-guideline.md).
- [퍼즐 요소 확장 구조 설계안](Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [전체 목표·현재 증거20개](Goals/MoonRabbitJunkyard/ElementFramework/2026-10-04-refactor-goal.md) — 큰 구간1~5 완료. 초기7구간/EF 이력은 과거 참고.
- [요소 확장·드론 수정 통합 개발 가이드라인](Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md) · [5단계 최종 기록](Verification/MoonRabbitJunkyard/ElementFramework/phase-05-progress.md) · [콘텐츠·행동·정책 추가 안내](Guides/MoonRabbitJunkyard/ElementFramework/element-extension-usage.md) — 실제 제작500개·필요 자원 준비·현재85종692682 PASS/FAIL0·전량 논리/원본 보존 감사 완료. 후속 구현 자동 시작 없음.
- [월드 게임 화면 전체 단계 계획](Planning/MoonRabbitJunkyard/WorldGameScreen/2026-09-30-world-game-screen.md)
- 완료된 3단계: [계획](Planning/MoonRabbitJunkyard/WorldGameScreen/stage-03-editor-launch-plan.md) · [목표·완료 조건](Goals/MoonRabbitJunkyard/WorldGameScreen/stage-03-editor-launch-goal.md) · [목표 명령어](Commands/MoonRabbitJunkyard/WorldGameScreen/stage-03-goal-command.md)
- [3단계 진행·검증 기록](Verification/MoonRabbitJunkyard/WorldGameScreen/stage-03-progress.md) · [에디터 게임 실행 안내](Guides/MoonRabbitJunkyard/WorldGameScreen/stage-03-editor-launch-usage.md)
- [2단계 진행·검증 기록](Verification/MoonRabbitJunkyard/WorldGameScreen/stage-02-progress.md) · [게임 실행 안내](Guides/MoonRabbitJunkyard/WorldGameScreen/stage-02-gameplay-usage.md)
- [HTML 사용 매뉴얼](MoonRabbitJunkyard/Manual/index.html) · [게임 화면 목업](MoonRabbitJunkyard/Mockups/puzzle-screen.html)
- [고물탑 화면 목업](MoonRabbitJunkyard/Mockups/junk-tower.html) · [목업 사용 안내](Guides/MoonRabbitJunkyard/junk-tower-mockup.md)

- 완료된 4단계 (2026-10-01): [계획](Planning/MoonRabbitJunkyard/WorldGameScreen/stage-04-mockup-ui-plan.md) · [목표·완료 조건](Goals/MoonRabbitJunkyard/WorldGameScreen/stage-04-mockup-ui-goal.md) · [목표 명령어](Commands/MoonRabbitJunkyard/WorldGameScreen/stage-04-goal-command.md)

- 5단계 (통합 검증 완료): [계획](Planning/MoonRabbitJunkyard/WorldGameScreen/stage-05-integration-plan.md) · [목표·완료 조건](Goals/MoonRabbitJunkyard/WorldGameScreen/stage-05-integration-goal.md) · [목표 명령어](Commands/MoonRabbitJunkyard/WorldGameScreen/stage-05-goal-command.md) · [검증 기록](Verification/MoonRabbitJunkyard/WorldGameScreen/stage-05-progress.md) · [실행 안내](Guides/MoonRabbitJunkyard/WorldGameScreen/stage-05-integration-usage.md)

- 6단계 (구현·Editor 검증 완료): [계획](Planning/MoonRabbitJunkyard/WorldGameScreen/stage-06-swipe-swap-plan.md) · [목표·완료 조건](Goals/MoonRabbitJunkyard/WorldGameScreen/stage-06-swipe-swap-goal.md) · [목표 명령문](Commands/MoonRabbitJunkyard/WorldGameScreen/stage-06-goal-command.md) · [검증 기록](Verification/MoonRabbitJunkyard/WorldGameScreen/stage-06-progress.md) · [사용 안내](Guides/MoonRabbitJunkyard/WorldGameScreen/stage-06-swipe-swap-usage.md). 플레이어·Addressables 콘텐츠 빌드 미실행.

- 7단계 (구현·Editor 검증 완료): [계획](Planning/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-plan.md) · [목표·완료 조건](Goals/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-goal.md) · [목표 명령문](Commands/MoonRabbitJunkyard/WorldGameScreen/stage-07-goal-command.md) · [검증 기록](Verification/MoonRabbitJunkyard/WorldGameScreen/stage-07-progress.md) · [사용 안내](Guides/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-usage.md). 빌드 미실행. 다음은 8단계 파워 상세 효과다.

- 8단계 (구현·Editor 검증 완료): [계획](Planning/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-plan.md) · [목표·완료 조건](Goals/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-goal.md) · [목표 명령문](Commands/MoonRabbitJunkyard/WorldGameScreen/stage-08-goal-command.md) · [검증 기록](Verification/MoonRabbitJunkyard/WorldGameScreen/stage-08-progress.md) · [사용 안내](Guides/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-usage.md). 빌드 미실행. 다음은 9단계 소리·진동·목표/승패 피드백이다.

## 작성·분류 기준

- `Core`는 기존 규칙·레벨 에디터·자동 플레이 개발 1~33단계다. `WorldGameScreen`은 월드 게임 화면 개발 1~10단계이며 8단계까지 세부 계획이 있다. 두 흐름의 같은 단계 번호를 혼동하지 않는다.
- 새 계획, 목표, 명령어, 진행 기록, 사용 안내는 각각 해당 역할 폴더 아래 `MoonRabbitJunkyard/WorldGameScreen/`에 작성한다. 여러 역할이 섞인 기존 문서는 주된 목적에 따라 한 곳에 두고 서로 링크한다.
- 이전 목표 문서에 포함된 실행문은 역사적 내용으로 보존한다. 새로 만드는 독립 목표 명령문은 `Commands`로 분리한다.
- 실제 결과와 과거 상태 설명을 문서 정리 작업에서 임의로 변경하지 않는다. 오래된 날짜·상태는 당시 기록일 수 있다.
- 문서 간 링크는 상대 경로로 작성한다. 복사용 명령어와 코드에서 참조하는 문서는 `Docs/...` 저장소 기준 경로를 사용한다.
- PNG, HTML, 폰트 등 비 Markdown 자료는 `Docs/MoonRabbitJunkyard/Art`, `Manual`, `Mockups`의 기존 위치를 유지한다. 아트 설명 Markdown은 `Contents/MoonRabbitJunkyard/Art`에 있다.
- 프로젝트 공통 설계·계획은 기존 규칙대로 `Decisions/project-wide`, `Planning/project-wide`를 사용한다. 아직 문서가 없는 분류에 빈 폴더를 미리 만들지 않는다.

문서 위치가 변경되었으므로 이전 대화의 경로 대신 이 목차 또는 새 목표 명령문을 사용한다.

- [요소 프레임워크 EF-10 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-10-progress.md) · [EF-11 다음 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-11-pool-baseline-plan.md)

- [요소 프레임워크 EF-11 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-11-progress.md) · [EF-12 다음 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-12-bot-observation-baseline-plan.md)

- [요소 프레임워크 EF-12 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-12-progress.md), 134 PASS/0 FAIL·관찰26건. 다음 [EF-13 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-13-element-id-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-13-element-id-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-13-command.md). 큰 구간 B의 첫 ID 매핑 구현 준비, 아직 구현 미착수.

- [요소 프레임워크 EF-13 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-13-progress.md), 469 PASS/0 FAIL·ID/오류 기록24건. 다음 [EF-14 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-14-catalog-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-14-catalog-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-14-command.md). 정의 ID/6종 매핑 구현, 기존 소비자 전환 미착수.

- [요소 프레임워크 EF-14 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-14-progress.md), 1619 PASS/0 FAIL·기록1026건. 다음 [EF-15 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-15-crate-placement-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-15-crate-placement-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-15-command.md). 메모리 카탈로그 구현, 대표1종 소비자 연결 준비.

- [EF-15 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-15-progress.md), 1837 PASS/0 FAIL·전후 동일31건. 다음 [EF-16 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-16-durable-placement-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-16-durable-placement-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-16-command.md). 상자 배치 수치 연결 완료, 나머지 내구도형4종 연결 준비.

- [EF-16 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-16-progress.md), 2479 PASS/0 FAIL·전후 동일97건. 다음 [EF-17 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-17-generator-placement-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-17-generator-placement-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-17-command.md). 내구도형5종 배치 수치 연결 완료, 발전기 배치 수치 연결 준비.

- [EF-17 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-17-progress.md), 2624 PASS/0 FAIL·전후 동일100건. 다음 [EF-18 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-18-crate-damage-policy-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-18-crate-damage-policy-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-18-command.md). 기존6종 배치 정의 연결 완료, 상자 피해 원인 조회 연결 준비.

- [EF-18 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-18-progress.md), 3417 PASS/0 FAIL·전후 동일559건. 다음 [EF-19 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-19-scrap-damage-policy-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-19-scrap-damage-policy-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-19-command.md). 상자 피해 원인 조회만 연결 완료, 고철1종의 동일 조회 연결 준비.

- [EF-19 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-19-progress.md), 4377 PASS/0 FAIL·전후 동일688건. 다음 [EF-20 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-20-capsule-damage-policy-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-20-capsule-damage-policy-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-20-command.md). 고철 허용 원인 조회 완료, 캡슐1종 연결 준비.

- [EF-20 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-20-progress.md), 4969 PASS/0 FAIL·전후 동일484건. 다음 [EF-21 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-21-color-lock-damage-policy-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-21-color-lock-damage-policy-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-21-command.md). 캡슐 허용 원인 조회 완료, 색 자물쇠1종 연결 준비.

- [EF-21 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-21-progress.md), 9421 PASS/0 FAIL·전후 동일2782건. 다음 [EF-22 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-22-appliance-damage-policy-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-22-appliance-damage-policy-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-22-command.md). 색 자물쇠 허용 원인 조회 완료, 금속기둥1종 연결 준비.

- [EF-22 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-22-progress.md), 14506 PASS/0 FAIL·전후 동일3040건. 다음 [EF-23 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-23-generator-reaction-policy-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-23-generator-reaction-policy-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-23-command.md). 금속기둥 허용 조회 완료, 발전기1종 연결 준비.

- [EF-23 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-23-progress.md), 38995 PASS/0 FAIL·전후 동일8745건. 다음 [EF-24 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-24-color-match-policy-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-24-color-match-policy-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-24-command.md). 기존6종 원인 허용 연결 완료, 색 조건 한 비교 연결 준비.

- [EF-24 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-24-progress.md), 59856 PASS/0 FAIL·전후 동일8447건. 다음 [EF-25 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-25-damage-aggregation-query-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-25-damage-aggregation-query-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-25-command.md). 색 일치 조회 연결 완료, 본체별/칸별 집계 조회 연결 준비.

- [EF-25 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-25-progress.md) · [EF-26 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-26-reserved-damage-query-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-26-reserved-damage-query-goal.md) · [명령문](Commands/MoonRabbitJunkyard/ElementFramework/stage-26-command.md)

- [EF-26 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-26-progress.md) · [EF-27 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-27-damage-record-policy-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-27-damage-record-policy-goal.md) · [명령문](Commands/MoonRabbitJunkyard/ElementFramework/stage-27-command.md)

- [EF-27 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-27-progress.md),185192 PASS/0 FAIL · [EF-28 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-28-removal-mission-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-28-removal-mission-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-28-command.md)

- [EF-28 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-28-progress.md),228623 PASS/0 FAIL · [EF-29 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-29-initial-mission-supply-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-29-initial-mission-supply-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-29-command.md)

- [EF-29 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-29-progress.md),275824 PASS/0 FAIL · [EF-30 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-30-capsule-adjacent-policy-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-30-capsule-adjacent-policy-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-30-command.md)

- [EF-30 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-30-progress.md),324210 PASS/0 FAIL · [EF-31 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-31-capsule-magnet-policy-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-31-capsule-magnet-policy-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-31-command.md)

- [EF-31 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-31-progress.md),373376 PASS/0 FAIL · [EF-32 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-32-durable-magnet-policy-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-32-durable-magnet-policy-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-32-command.md)


- [EF-32 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-32-progress.md),427683 PASS/0 FAIL · [EF-33 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-33-reaction-behavior-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-33-reaction-behavior-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-33-command.md)

- [EF-33 검증 완료](Verification/MoonRabbitJunkyard/ElementFramework/stage-33-progress.md),480736 PASS/0 FAIL · [EF-34 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-34-reaction-apply-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-34-reaction-apply-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-34-command.md)

## 요소 확장 리팩토링 최신 구간

- [큰 구간2 완료 기록](Verification/MoonRabbitJunkyard/ElementFramework/phase-02-progress.md): 드론 정책·상승/호버/돌진·실제 반복 재선택, 최종41종688965 PASS/0 FAIL.
- [큰 구간3 완료 기록](Verification/MoonRabbitJunkyard/ElementFramework/phase-03-progress.md): 정의 ID 저장·제작/편집 도구·스키마5/팩2, 최종54종690245 PASS/0 FAIL.
- [큰 구간4 완료 기록](Verification/MoonRabbitJunkyard/ElementFramework/phase-04-progress.md): 공통 표현·리소스·풀·HUD/봇 경계, 최종79종691470 PASS/0 FAIL·전량 논리/원본 보존 감사. 다음5단계 [계획](Planning/MoonRabbitJunkyard/ElementFramework/phase-05-extension-validation-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/phase-05-extension-validation-goal.md) · [전체 복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/phase-05-command.md). 5단계 구현은 미착수.
- [5개 큰 구간 통합 가이드](Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

# 프로젝트 문서 안내

Markdown 문서는 **문서 역할 → 프로젝트 → 개발 흐름** 순서로 분류한다. 파일명과 기존 문서 번호는 유지한다. 완료 여부는 각 문서의 상태와 검증 기록을 확인하며, 폴더 위치만으로 완료를 판단하지 않는다.

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
- [레벨 튜토리얼 기획서](Contents/MoonRabbitJunkyard/13_레벨튜토리얼.md) — 레벨 데이터·단계 편집·자동 진행과 에디터 시험의 15개 결정 정리, 구현 미착수.
- [퍼즐 요소 확장 구조 설계안](Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [7단계 전환 계획](Planning/MoonRabbitJunkyard/ElementFramework/2026-10-04-refactor-plan.md) · [목표·완료 조건](Goals/MoonRabbitJunkyard/ElementFramework/2026-10-04-refactor-goal.md) — 수백 종류 확장을 위한 리팩토링 제안, 구현 미착수.
- [요소 확장·드론 수정 통합 개발 가이드라인](Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md) — EF-01~09 기준 확보 완료. [EF-09 검증](Verification/MoonRabbitJunkyard/ElementFramework/stage-09-progress.md) 추가1615 PASS/0 FAIL·기존 파워 검사 통과. 다음 [EF-10 계획](Planning/MoonRabbitJunkyard/ElementFramework/stage-10-resource-baseline-plan.md) · [목표](Goals/MoonRabbitJunkyard/ElementFramework/stage-10-resource-baseline-goal.md) · [복사용 실행문](Commands/MoonRabbitJunkyard/ElementFramework/stage-10-command.md) 준비. 구조 전환·새 드론 비행 미구현.
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

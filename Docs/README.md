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

- [게임 기획서](Contents/MoonRabbitJunkyard/기획서.md)
- [월드 게임 화면 전체 단계 계획](Planning/MoonRabbitJunkyard/WorldGameScreen/2026-09-30-world-game-screen.md)
- 다음 3단계: [계획](Planning/MoonRabbitJunkyard/WorldGameScreen/stage-03-editor-launch-plan.md) · [목표·완료 조건](Goals/MoonRabbitJunkyard/WorldGameScreen/stage-03-editor-launch-goal.md) · [목표 명령어](Commands/MoonRabbitJunkyard/WorldGameScreen/stage-03-goal-command.md)
- [2단계 진행·검증 기록](Verification/MoonRabbitJunkyard/WorldGameScreen/stage-02-progress.md) · [게임 실행 안내](Guides/MoonRabbitJunkyard/WorldGameScreen/stage-02-gameplay-usage.md)
- [HTML 사용 매뉴얼](MoonRabbitJunkyard/Manual/index.html) · [게임 화면 목업](MoonRabbitJunkyard/Mockups/puzzle-screen.html)

## 작성·분류 기준

- `Core`는 기존 규칙·레벨 에디터·자동 플레이 개발 1~33단계다. `WorldGameScreen`은 월드 게임 화면 개발 1~5단계다. 두 흐름의 같은 단계 번호를 혼동하지 않는다.
- 새 계획, 목표, 명령어, 진행 기록, 사용 안내는 각각 해당 역할 폴더 아래 `MoonRabbitJunkyard/WorldGameScreen/`에 작성한다. 여러 역할이 섞인 기존 문서는 주된 목적에 따라 한 곳에 두고 서로 링크한다.
- 이전 목표 문서에 포함된 실행문은 역사적 내용으로 보존한다. 새로 만드는 독립 목표 명령문은 `Commands`로 분리한다.
- 실제 결과와 과거 상태 설명을 문서 정리 작업에서 임의로 변경하지 않는다. 오래된 날짜·상태는 당시 기록일 수 있다.
- 문서 간 링크는 상대 경로로 작성한다. 복사용 명령어와 코드에서 참조하는 문서는 `Docs/...` 저장소 기준 경로를 사용한다.
- PNG, HTML, 폰트 등 비 Markdown 자료는 `Docs/MoonRabbitJunkyard/Art`, `Manual`, `Mockups`의 기존 위치를 유지한다. 아트 설명 Markdown은 `Contents/MoonRabbitJunkyard/Art`에 있다.
- 프로젝트 공통 설계·계획은 기존 규칙대로 `Decisions/project-wide`, `Planning/project-wide`를 사용한다. 아직 문서가 없는 분류에 빈 폴더를 미리 만들지 않는다.

문서 위치가 변경되었으므로 이전 대화의 경로 대신 이 목차 또는 새 목표 명령문을 사용한다.

# 레벨 튜토리얼 2단계 — 복사용 목표 실행문

상태: 완료. 2026-10-07 구현·검증. 아래는 수행한 목표 실행문 기록이다. 아래 실행문은 새 2단계만 수행한다. 이전 실행문의 실제 게임 연결은 새 3단계로 이동했다.

~~~text
ServeredMeridian 레벨 튜토리얼 2단계 ‘확장 가능한 진행 엔진’을 진행해.

Docs/Contents/MoonRabbitJunkyard/13_레벨튜토리얼.md, Docs/Planning/MoonRabbitJunkyard/Tutorial/integration-guideline.md와 stage-02-runtime-plan.md, Docs/Goals/MoonRabbitJunkyard/Tutorial/stage-02-runtime-goal.md, Docs/Decisions/MoonRabbitJunkyard/2026-10-07-tutorial-extension.md, Docs/Verification/MoonRabbitJunkyard/Tutorial/stage-01-progress.md를 읽고 목표를 설정하여 이 세션에서 직접 수행해. 현재 HEAD·완료된 1단계·WIP·원본/GUID를 보존해.

공통 진행과 행동 처리기·완료 조건 판정기를 분리해. 설명/교환/파워 교환/아이템과 생성/발동/제거를 명시적으로 등록하고 현재 단계에 필요한 처리기만 직접 조회해. 망치/교환/섞기 규칙도 중앙 진행부에 분기하지 마. 중복·미등록은 오류로 진단하고 전체 검색·전역 가변 목록·매 프레임 리플렉션은 만들지 마. 오브젝트마다 처리기를 만들지 말고 같은 특성은 요소 ID·좌표·수량으로 공통 처리해.

엔진은 순서·승인·관련 결과/연출 대기·완료/오류/취소를 소유하고 실제 퍼즐·UI를 직접 참조하지 마. 승인과 성공을 구별하고 관련 성공·결과·연출 완료 후 한 번만 진행해. 과거/취소된 행동·중복 신호, 2×2 중복·재보충·세션 격리를 검사해. 체험 권한은 해당 단계 성공 시 1회 소진하고 거절/실패/취소는 소진하지 마. 읽기 전용 상태·대기/오류 이유를 제공해.

기존 validator의 종류별 검사·참조 수집을 등록 규칙에 연결하되 공개 API·배치/공급·초기 대상 검사·에디터 오류·팩 참조 폐쇄를 보존해. enum 값·MemoryPack 필드 순서·팩1/2/3·50레벨 주소를 바꾸지 마. 새 저장 항목이 필요한 확장은 별도 호환성 작업임을 명시해.

Tests/Editor/Features/Tutorial의 소유한 입력으로 모의 결과·연출 신호를 검사하고 TutorialRuntimeVerification.Run 엔트리를 제공해. 시험 전용 처리기/판정기를 별도 목록에 등록하여 중앙 엔진 수정 없이 확장되고 미사용 처리기는 호출되지 않는지 확인해. 등록 실패·각 단계/아이템·결과 ID/좌표/수량·신호 지연/순서·일시정지·취소·체험 권한·동시 세션을 검사해. Tools/Testing/ProjectTests.ps1로 관련 1단계 회귀와 테스트 해제 후 게임 소스 컴파일을 확인하고 과거 검사 출력은 보관 후 복원해.

실제 입력/세션 연결·고정 공급·EndStableTurn 수정·실제 행동 재생은 3단계로 남겨. 안내 UI·완료 저장·시험 3모드·대표 레벨 적용은 4단계로 남겨. 모의 검사를 실제 게임 검증으로 보고하지 마. 빌드·번들·출시 레벨/팩 덮어쓰기·커밋·푸시·하위 에이전트·사용자 Unity 종료·임의 씬 저장은 하지 마.

완료 시 변경·실제 검증 근거·미완료/제약·확장 방법을 보고하고 Docs/Verification/MoonRabbitJunkyard/Tutorial/stage-02-progress.md에 기록해. 다음 3단계 ‘실제 게임 연결·재생 검사’ 계획·목표·복사 가능한 전체 실행문을 실제 계약에 맞춰 작성하되 구현은 자동 시작하지 마.
~~~

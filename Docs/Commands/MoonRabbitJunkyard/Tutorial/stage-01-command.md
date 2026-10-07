# 레벨 튜토리얼 1단계 — 복사용 목표 실행문

상태: 1단계 완료. 아래는 수행한 목표 실행문이다. 다음 작업은 stage-02-command.md를 사용한다.

```text
ServeredMeridian 레벨 튜토리얼 1단계 ‘데이터·저장·레벨 에디터’를 실행해.

Docs/Contents/MoonRabbitJunkyard/13_레벨튜토리얼.md, Docs/Planning/MoonRabbitJunkyard/Tutorial/integration-guideline.md와 stage-01-data-editor-plan.md, Docs/Goals/MoonRabbitJunkyard/Tutorial/stage-01-data-editor-goal.md를 읽고 목표를 설정하여 직접 진행해. work의 현재 HEAD·완료분·WIP를 보존해.

설명·두 칸 교환·파워 교환 발동·지정 아이템 단계와 강조/행동 대상·생성/발동/제거 조건·고정 시드/공급을 레벨에 추가해. 에디터 단계 추가·삭제·정렬·보드 대상 선택·조건 편집·정적 오류 표시를 지원하고 SerializedObject/Undo, 저장/재로드와 선택/구독 수명을 유지해. 대상 선택이 블록 배치/삭제로 처리되지 않게 해. 후속 생성 파워를 초기 보드에 없다고 거절하지 마.

기존 팩1/2 DTO를 보존하고 기존 팩2 바이트와 레벨별 튜토리얼 메타데이터를 담는 외부 팩3을 사용해. 튜토리얼 없는 결과·바이트·지문을 유지하고 Copy/Snapshot/JSON/실행 요청·입력 식별의 누락을 검사해. 구형 쓰기로 정보를 버리지 마. 기존50레벨 주소·요소 콘텐츠 팩·제작 SO 배포 제외를 유지해.

Tests/Editor/Features/Tutorial과 ProjectTests.ps1로 전체 값 왕복·구형 읽기·오류·1/50/51/100/101·Asset/MemoryPack 동등·실제 작성/저장/Undo를 검사해. 관련 회귀와 테스트 연결 해제 후 게임 소스 컴파일을 확인하고 원본 레벨·디스크 팩·GUID·과거 검사 출력은 보존해.

게임 내 자동 진행·입력 제한·안내 UI·완료 기록·인벤토리·고물탑은 이번 단계에 구현하지 마. 빌드·Addressables 번들 빌드·출시 레벨/팩 자동 덮어쓰기·커밋·푸시·하위 에이전트·사용자 Unity 종료·임의 씬 저장도 하지 마.

완료 시 변경·검증 근거·제약을 보고하고 Docs/Verification/MoonRabbitJunkyard/Tutorial/stage-01-progress.md에 기록해. 다음2단계 계획서·목표문서·복사 가능한 전체 실행문을 작성하되 다음 구현은 자동 시작하지 마.
```

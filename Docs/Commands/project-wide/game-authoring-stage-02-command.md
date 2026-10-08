# 게임·제작 도구 분리 2단계 — 복사용 목표 실행문

> 2026-10-08 · 후속 실행문 / 자동 착수하지 않음
> 1단계 완료 보고를 확인한 뒤 아래 코드 블록을 복사하여 실행한다.

[계획서](../../Planning/project-wide/game-authoring-stage-02-plan.md) · [목표·완료 조건](../../Goals/project-wide/game-authoring-stage-02-goal.md)

```text
ServeredMeridian의 게임·제작 도구 분리 2단계를 구현해.

다음 문서와 현재 소스를 읽고 계획과 완료 조건에 따라 진행해.
- Docs/Planning/project-wide/game-authoring-stage-02-plan.md
- Docs/Goals/project-wide/game-authoring-stage-02-goal.md
- Docs/Planning/project-wide/2026-10-08-game-and-authoring-products-plan.md

기존 제작 SO를 변경하지 않고 레벨·요소·카탈로그·표현·공통 튜토리얼·샘플·모양의 JSON 계약, 안전한 내보내기/읽기/저장, 공통 플레이 진입 어댑터를 구현해.
공유 flowId와 레벨별 bindings, 안정 ID, 완료 기록 의미, 기획 출처를 보존해. 실행 팩을 역변환하여 제작 JSON을 만들지 마.
JSON은 Assets 밖 명시적 시험 작업 폴더에 만들고 기존 50레벨 팩·요소 팩·Addressables·기본 저장 방식을 변경하지 마.
Unity 없는 문서 파싱·참조/저장 검사와 실제 Unity 시험 입력 동등성, 실패/충돌/취소 복구, 원본/정식 저장 불변을 검증해.

기존 편집 UI 연결은 3단계 이후이며 전체 JSON 원본 정식화는 6단계야.
공통 레벨툴 씬은 4단계 시범, 5단계 기능 동등성 확인 후 Unity 기본 편집 화면으로도 사용하고 기존 EditorWindow 중복 편집 UI는 영구 유지하지 않는 방향을 지켜.

필요하면 하위 에이전트를 사용해.
커밋·푸시 및 Player/Addressables 빌드는 하지 마.
HTML 매뉴얼은 별도 요청 전까지 갱신하지 마.
질문은 충분히 설명하고 제한시간 없는 일반 채팅 문답으로 진행해.

완료 후 실제 변경·검증 결과·남은 제약을 보고하고, 3단계 계획서·목표 문서·복사 가능한 실행문을 작성해. 3단계 구현은 자동 시작하지 마.
실제 구현과 검증을 모두 마친 마지막 보고에서만 완료한 변경에 맞는 커밋 메시지를 복사 가능한 코드 블록으로 제공해.
```

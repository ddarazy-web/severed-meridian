# 조합형 튜토리얼 1단계 목표

상태: 2026-10-08 구현 및 Unity 편집·플레이 검증 완료.

[계획](../../../Planning/MoonRabbitJunkyard/Tutorial/composer-01-plan.md) · [기획](../../../Contents/MoonRabbitJunkyard/15_조합형튜토리얼.md)

## 목표

사용자가 레벨 에디터에서 셀과 샘플을 선택하고 교환·매칭 조건을 조립하여 저장 및 실제 게임 시험까지 할 수 있게 한다. 개발자가 실제 레벨 튜토리얼 내용을 대신 만들지 않는다.

## 완료 조건

- [x] 두 칸 선택 → 샘플 미리보기/적용 → 교환·매칭 조건 수정 가능.
- [x] 단계 추가/조건 추가 구분, 단계 복제/정렬, 취소/Undo/Redo가 기존 배치를 훼손하지 않음.
- [x] 자동 강조와 수동 수정, 오류 필드·셀 이동 가능.
- [x] 교환 횟수, 매칭 크기 정확히/이상, 직접/연쇄, 매칭 묶음 횟수, All/Any 실제 판정 통과.
- [x] 미충족 시 같은 단계에서 다음 조작 가능. 연출 완료까지 기다리고 다음 단계로 진행.
- [x] 신규 데이터 저장·재로드·Asset/MemoryPack 동등성, 구버전 팩 읽기 보존.
- [x] 샘플 수정이 원본/다른 단계에 전파되지 않음. 실제 레벨 및 기존 WIP 보존.
- [x] 자동 검사와 실제 편집/플레이 확인 결과를 구분하여 기록. 미검증을 완료로 표시하지 않음.
- [x] 종료 보고와 2단계 계획·목표·복사 가능한 실행문 제공.

빌드·커밋·푸시·하위 에이전트 금지. 나머지 조건 목록·공유 템플릿·기존 전체 전환은 후속 단계 범위다.

[완료 보고](../../../Verification/MoonRabbitJunkyard/Tutorial/composer-01-progress.md) · [다음 단계 계획](../../../Planning/MoonRabbitJunkyard/Tutorial/composer-02-plan.md) · [다음 목표](../../../Goals/MoonRabbitJunkyard/Tutorial/composer-02-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/Tutorial/composer-02-command.md).

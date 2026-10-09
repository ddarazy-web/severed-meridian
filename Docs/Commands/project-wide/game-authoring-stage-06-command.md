# 게임·제작 도구 분리 6단계 — 복사용 실행문

> 5단계 완료 후 복사용 실행문. 별도 실행 요청 전에는 착수하지 않는다.

[계획](../../Planning/project-wide/game-authoring-stage-06-plan.md) · [목표](../../Goals/project-wide/game-authoring-stage-06-goal.md)

```text
ServeredMeridian의 게임·제작 도구 분리 6단계를 진행해.
Docs/Planning/project-wide/game-authoring-stage-06-plan.md와
Docs/Goals/project-wide/game-authoring-stage-06-goal.md를 읽고 구현·검증해.
5단계 필수 미완료 사항이 있으면 먼저 해결해.

전체 제작 데이터를 별도 후보 JSON으로 이관·검증한 뒤 기본 원본으로 채택해.
원본과 안정 ID·공유 튜토리얼·학습 완료 의미를 보존하고 복구 경로를 남겨.
한 스냅샷에서 레벨·요소 MemoryPack을 만들고 기존 50레벨 구간과
Addressables 주소·로더 호환을 유지해. 생성 실패 시 이전 정상 팩을 보존해.
Android/iOS/Steam/Windows 레벨툴 프로필과 심볼·씬·리소스 경계를 확인해.

필요하면 하위 에이전트를 사용해.
커밋·푸시·Player/Addressables 빌드·HTML 매뉴얼 수정은 하지 마.
질문은 충분히 설명하는 제한시간 없는 일반 문답으로 해.
실제 배포 빌드·실기 검증은 별도 승인 전까지 실행하지 말고 미검증으로 보고해.
완료 후 변경·검증·제약·복구 방법을 보고하고,
마지막에 실제 완료한 작업의 커밋 메시지를 복사 가능한 코드 블록으로 제공해.
```


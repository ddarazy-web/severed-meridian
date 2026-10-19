# 제작 데이터 전달 3단계 목표·완료 조건

2026-10-17 · 완료 · 2단계의 후보 채택 결과를 기존 팩 변환·등록 경로에 연결

## 목표

채택한 JSON을 50레벨 단위 MemoryPack과 요소 팩으로 변환하여 기존 게임 로딩 경로에 반영한다.

[작업 계획](../../Planning/project-wide/level-data-transfer-stage-03-plan.md) · [통합 계획](../../Planning/project-wide/level-data-transfer-integration-plan.md) · [실행문](../../Commands/project-wide/level-data-transfer-stage-03-command.md)

## 완료 조건

- [x] 1~15레벨은 1~50 구간 팩으로, 경계 fixture는 각 번호 구간에 정확히 포함.
- [x] 요소 팩·세대 정보·Addressables 주소의 일관성 검사 통과.
- [x] 변환 후 해시 일치/원본 수정 후 갱신 필요 상태를 정확히 표시.
- [x] 등록 실패 시 이전 팩/등록 상태 복구 검증 통과.
- [x] 대표 레벨의 JSON/MemoryPack 플레이 비교 완료, 미검증 플랫폼은 별도 표시.
- [x] 변경 범위와 기존 데이터 보존 확인, 검사 로그 및 실제 UI 확인 결과 기록.
- [x] 미검증 항목을 완료로 표현하지 않고 후속 작업에 구분.

## 범위 밖

APK/AAB/Windows/iOS 제품 빌드, 원격 콘텐츠 배포, 실제 클라우드 업로드.

## 마무리

근거: [최종 검사·판단 기록](../../Verification/project-wide/level-data-transfer-stage-03-progress.md). 관련 검사 6개 모두 Unity exit 0. 실제 변경 바이트 복원, 원본이 손상된 상태의 복구와 UI 표시까지 포함한다.

제품 빌드·실기기 및 배포 번들 로드는 미검증이다. 게임 비교는 production LevelPackLoader에 격리 팩 바이트를 공급해 레벨 1·3·7의 시작 보드, 튜토리얼, 일반 교환을 실행했다. 실제 Addressables 등록은 검사 후 기존 팩/설정으로 복원했다. UI 생성 시간은 파일 수정 시각이며 UI 취소는 변환 시작 전에만 제공한다.

통합 3단계 개발 계획을 종료한다. 후속 작업은 실제 프로젝트의 선택 원본 변환 또는 별도 승인된 제품 배포 검증이며 임의로 4단계를 추가하지 않는다. [후속 운영 실행문](../../Commands/project-wide/level-data-transfer-stage-03-command.md)을 제공한다. 커밋·푸시는 실행하지 않았다.

# 제작 데이터 전달 2단계 목표·완료 조건

2026-10-17 · 완료

## 목표

외부 ZIP을 별도 후보로 검사하고 변경 내용을 확인한 뒤 정식 JSON 제작 원본으로 채택한다.

[작업 계획](../../Planning/project-wide/level-data-transfer-stage-02-plan.md) · [통합 계획](../../Planning/project-wide/level-data-transfer-integration-plan.md) · [실행문](../../Commands/project-wide/level-data-transfer-stage-02-command.md)

## 완료 조건

- [x] Unity 메뉴에서 ZIP 선택→검사→변경 비교→반영 흐름 사용 가능.
- [x] 검사 오류·취소 시 기존 선택 설정/JSON/MemoryPack 바이트 동일.
- [x] 프로젝트 교체 및 레벨 제외 예정 정보를 확인한 뒤에만 반영.
- [x] 수입 후보에 기존 SO와의 동일성을 요구하지 않으며 과거 SO 이관 검사 유지.
- [x] 채택된 데이터의 Unity JSON 시험 정상, 생성 팩과의 차이 표시.
- [x] 변경 범위와 기존 데이터 보존 확인, 검사 로그 및 실제 UI 확인 결과 기록.
- [x] 미검증 항목을 완료로 표현하지 않고 후속 작업에 구분.

## 범위 밖

MemoryPack 파일 교체, Addressables 등록 변경, 부분 레벨 병합, 제품 빌드.

## 마무리

완료 결과를 보고하고 다음 단계 문서를 실제 결과에 맞춰 정리한다. 커밋 메시지는 구현 완료 후에만 복사 가능한 코드 블록으로 제시한다. 커밋·푸시는 실행하지 않는다.

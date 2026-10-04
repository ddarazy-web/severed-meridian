# EF-13 — 영구 정의 ID·기존 장애물 매핑 목표

상태: 완료. 새49+봇32+장애물142+발전기246 PASS/0 FAIL, 각각 종료0. [검증 기록](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-13-progress.md).

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-13-element-id-plan.md) · [가이드라인](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md).

- [x] 기존 콘텐츠 enum 숫자·배치 Id·연결·저장 경계와 작업 시작 상태를 확인하고 보호했다.
- [x] 불변 ElementId의 값 보존·Ordinal 동등성/해시·잘못된 값/default 처리 계약을 구현·검증했다.
- [x] 장애물6종의 명시적 매핑이 계획의 문자열과 정확히 일치하고 중복0이다.
- [x] 미지원 enum은 입력 값을 포함해 거절하며 숨은 기본 대체가 없다.
- [x] 표시명/배치 Id/배열 순서 변경은 같은 종류의 매핑에 영향을 주지 않는다.
- [x] 별도 Editor의 새 검사 및 안전한 기존 회귀 검사 종료0/필수 FAIL0과 실제 값·오류를 기록했다.
- [x] 기존 소비자·동작·생산/저장/원본/GUID/Addressables/기존 변경 보존을 확인했다. 빌드·변환·다음 단계 구현은 하지 않았다.
- [x] stage-13-progress.md와 완료 보고·다음 한 단계 계획/목표·전체 복사용 명령문을 제공했다.

실제 미실행/필수 실패는 완료로 표시하지 않는다. 전체 카탈로그·저장 전환·수백 종류 지원 완성을 뜻하지 않는다.

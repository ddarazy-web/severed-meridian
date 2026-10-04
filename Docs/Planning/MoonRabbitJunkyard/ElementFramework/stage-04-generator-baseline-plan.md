# EF-04 — 발전기 충전·연결 기준 확보 계획

상태: 완료. 262 PASS/0 FAIL, 관찰 125건.

목표: 공통 규칙 이관 전 발전기 충전·연결 해제·간접 제거의 기존 계약과 실제 결과를 확보한다.

연결: [통합 가이드라인](integration-guideline.md) · [EF-03 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-03-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-04-generator-baseline-goal.md).

실행 담당: superpowers:executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 범위와 작은 작업 단위

1. 원본·기존 변경 해시를 기록한다. GeneratorRules, 피해/미션/연결 호출부와 GeneratorVerification.Data 및 Data에서 호출하는 EdgeChecks를 확인한다. Supplemental은 독립 공개 진입점이 아니므로 호출 계약을 먼저 확인한다. UI/Start/Restart/Regression과 에셋 저장·삭제 경로는 포함하지 않는다.
2. 기존 메모리 검사를 재사용한다. 요구 충전 3~5, 같은 턴 본체 여러 칸/중복 접촉은 1충전, 다음 턴 재충전, 벽 차단, 여러 발전기 독립, 직접 대상 제거·마지막 연결 자동 철거, 완충 연결 전체 제거·단일 미션·먼지 보존을 대응표로 정리한다. 기존 검사에 없는 핵심 사례와 실제 기록만 보완한다.
3. 필요하면 동일 Tests 폴더의 GeneratorVerification.Baseline.cs/meta에 최소 Editor 진입점을 추가한다. 시드·초기 연결/충전·행동 순서·실제 충전/활성 연결/제거 본체/미션/GeneratorRecord를 기록한다. 메모리 fixture를 사용하고 생산 API는 추가하지 않는다.
4. 별도 안전한 Unity Editor 데이터 검사로 실제 종료 코드/새 결과를 확인한다. 보호 파일 해시·diff·GUID를 검사한다. 실패는 숨기지 않고 원인을 확인하며 생산 규칙 변경이 필요하면 범위를 논의한다.
5. stage-04-progress.md에 변경/실측/미검증/한계를 기록하고 목표를 갱신한다. 결과에 따라 가장 작은 EF-05(기본 후보: 곰팡이 번식 기준) 계획·목표·복사용 실행문을 작성한다. EF-05 구현은 시작하지 않는다.

## 제약·위험

ServeredMeridian만 대상으로 한다. 생산 코드·원본 에셋·enum/GUID·저장 포맷·진행 중 작업을 보존한다. 빌드·커밋·사용자 Unity 종료·씬 저장 금지. 새 정책·드론 비행·프레임워크·UI 전환 제외.

충전을 일반 내구도 피해와 혼동하거나 연결 대상마다 본체 충전을 중복 적용하는 위험, 간접 제거 미션 중복과 마지막 연결 철거 타이밍을 확인한다. 다음 단계의 파일 계약은 이번 실측 후 확정한다.

## 실행 결과와 조정

기존 Data→EdgeChecks→AdditionalChecks 경로를 재사용했다. Supplemental은 독립 실행하지 않았다. 관찰·전선 단독 타격·실제 세 턴 완충 사례만 Baseline에 추가했다. 생산 구현 변경 없음. [완료 보고](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-04-progress.md)의 대응표와 실제 실행 증거로 작업 1~5를 확인했다.

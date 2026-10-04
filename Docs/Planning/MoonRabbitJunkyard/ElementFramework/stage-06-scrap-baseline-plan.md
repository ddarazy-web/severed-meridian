# EF-06 — 이동형 고철 피해·낙하·공급 기준 확보 계획

상태: 완료. 341 PASS/0 FAIL, 관찰33건.

목표: 공통 행동 이관 전 이동형 고철의 피해·점유 이동·공급 커서와 보충 카운터의 현재 계약을 확보한다.

연결: [가이드라인](integration-guideline.md) · [EF-05 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-05-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-06-scrap-baseline-goal.md).

실행 담당: superpowers:executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 작은 작업 단위

1. 기존 변경과 보호 파일 해시, Editor 상태를 기록한다. ObstacleDamageRules의 Scrap 반응, MovementQuery/SettlementResolution의 본체 좌표 이동, 공급 정의/적용/런타임 카운터와 실제 호출부를 확인한다.
2. 기존 ScrapVerification.Data/Supplemental/Edges의 메모리 검사와 호출 범위를 읽고 재사용한다. UI/Start/Restart/Regression과 원본 저장·삭제 경로는 제외한다. Data의 SupplyChecks와 Supplemental의 MovementChecks/InteractionChecks/SupplyEdgeChecks를 대응표로 정리한다.
3. 내구도 1~5, 같은 턴 제한/다음 턴, 일반 매칭·파워 피해/제거 미션, 본체 인덱스/좌표/점유의 낙하 뒤 일치, 벽/포털/덮개와 내용물·바닥 관계를 확인한다. 고정 공급 순서·소진 정책·유지 공급의 목표/누적 한도/카운터 및 공급 실패 원자성을 확인한다. 기존 검사를 복제하지 않고 누락 사례와 실제 값 기록만 보완한다.
4. 필요할 때만 동일 Tests 폴더에 ScrapVerification.Baseline.cs/meta를 추가한다. 시드·메모리 입력·타격/낙하/공급 순서, 실제 내구도/본체 좌표/점유/미션/커서/생성 수/난수·기록을 남긴다. 별도 안전한 Editor 데이터 검사로 실제 종료 코드/새 결과를 확보한다.
5. 보호 파일·diff·GUID·문서 링크를 검사하고 stage-06-progress.md에 변경/실측/미검증/문제를 기록한다. 완료 후 EF-07의 가장 작은 선행 작업(기본 후보: 회수 부품 수집·공급 기준)을 선정해 계획·목표·복사용 실행문을 만든다. EF-07 구현은 시작하지 않는다.

## 분리와 제약

고철과 회수 부품을 한 단계에 묶지 않는다. 이동 가능한 장애물 본체와 그 공급 카운터는 함께 검증해야 하므로 EF-06에 묶고, 출구 수집·회수 미션은 EF-07 후보로 분리한다. EF-05 예외 원복 수정의 정상 경로를 보존한다.

생산 규칙·원본 에셋·저장 포맷·enum/meta GUID·기존 작업을 보존한다. 새 프레임워크·드론 비행·정책·UI/팩 전환 제외. 빌드·커밋·사용자 Unity 종료·씬 저장 금지. 필수 실패를 발견하면 원인·수정 범위를 보고하며 생산 수정까지 임의 확대하지 않는다.

위험: 이동 뒤 본체 좌표만 갱신하거나 점유가 중복되는 오류, 같은 턴 피해 기록 유실, 공급 커서·생성 카운터가 실패 뒤 남는 오류, 유지 공급 한도와 살아 있는 수량을 혼동하는 경우. 전체 저장/표현/봇 경계는 후속 단계에서 따로 확보한다.

## 실행 결과·좌표 계약 확인

작업1~5를 수행했다. 현재 본체 정의의 좌표는 초기 좌표이며 낙하 뒤 위치는 칸의 ObstacleIndex 점유로 확인한다. 이를 생산 오류로 변경하지 않고 기록했다. 실제 NextTurn(2)과 공급 기록만 보완했으며 기존 파일 수정 없음. [완료 보고](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-06-progress.md)를 근거로 한다.

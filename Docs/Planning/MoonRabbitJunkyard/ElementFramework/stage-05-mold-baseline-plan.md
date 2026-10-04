# EF-05 — 곰팡이 제거·턴 종료 번식 기준 확보 계획

상태: 완료. 승인된 예외 원복 최소 수정과 최종 234 PASS/0 FAIL.

목표: 공통 규칙 이관 전 곰팡이의 내용물 보호·제거·턴 종료 번식과 난수/미션 목표 변화의 현재 계약을 확보한다.

연결: [가이드라인](integration-guideline.md) · [EF-04 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-04-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-05-mold-baseline-goal.md).

실행 담당: superpowers:executing-plans. 병렬 에이전트는 별도 요청 시에만 사용한다.

## 범위·작은 작업 단위

1. 기존 변경/원본 해시와 Editor 상태를 확인한다. MoldRules, DamageReaction, 매칭/파워의 덮개 제거, BoardActionExecutor.Ending의 턴 마무리 호출을 읽는다. 기존 MoldVerification.Data/Supplemental/Edges의 종료·저장·다른 기능 호출을 확인한다. UI/Start/Restart/Regression은 포함하지 않는다.
2. 기존 메모리 검사를 대응표로 재사용한다. 일반/파워 내용물 보존·조작/낙하 차단·매칭/직접 파워/자석의 제거 차이·먼지/미션, 제거된 턴의 확산 억제, 무효 입력·비소비 턴·미션 완료·곰팡이 없음·후보 없음·반복 마무리의 결과를 확인한다.
3. 실제 턴 종료 번식의 한 칸 선택, 인접/벽/다른 덮개/장애물/회수/원격 통로 제외, 단일/복수 후보 난수 소비, 같은 시드 재현, 미션 목표 증가와 기존 진행 보존을 확인한다. 기존 검사에 없는 핵심 사례 및 실제 값만 같은 Tests 폴더의 MoldVerification.Baseline.cs/meta로 보완한다. 생산 API나 번식 규칙을 수정하지 않는다.
4. 시드·메모리 초기 레벨·행동/턴 순서·후보 수/선택 좌표·번식 사유·전후 덮개/내용물·미션 목표/진행·난수 소비·기록을 저장한다. 별도 안전한 Editor 데이터 검사로 실제 종료 코드/새 결과·필수 실패 여부를 확인한다. 해시/diff/GUID/문서 링크를 검사한다.
5. Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-05-progress.md에 실측·변경·미검증·문제를 보고하고 목표를 갱신한다. 결과에 맞춰 EF-06의 가장 작은 선행 작업(기본 후보: 고철/회수·공급 기준)을 정해 계획·목표·복사용 실행문을 작성한다. EF-06 구현은 시작하지 않는다.

## 완료 경계·위험

ServeredMeridian, 현재 Unity/9×9/패키지를 유지한다. 생산 규칙·원본 에셋·저장 포맷·기존 enum/meta GUID와 진행 중 작업 보존. 빌드·커밋·사용자 Unity 종료·씬 저장 금지. 프레임워크·새 드론 비행/정책·저장/에디터/UI 전환 제외.

제거 이력의 턴 간 누수, 연쇄 중 번식·한 턴 중복 번식, 목표만 증가하고 진행을 초기화하는 오류, 벽/통로를 혼동한 원격 번식, 단일 후보에서 불필요한 난수 소비를 검사한다. 기준과 현 소스가 다르면 실패를 기록하고 원인을 조사한다. 생산 수정이 필요한 경우 목표를 임의 확대하지 않고 논의한다. 미래 단계 전체 상세 계획은 미리 작성하지 않는다.

## 실행 중 발견사항

기존 Data/Supplemental은 통과했다. Edges에서 행동 조회 예외 후 번식 상태 부분 반영을 재현했다. 생산 수정 금지 제약을 유지하며 독립 검증만 추가했다. 구형 NeedsShuffle 기대값과 현재 자동 재배치/Blocked 계약도 구분했다. [진행 보고](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-05-progress.md)에 실패·관찰·재개 범위를 기록했다. EF-06 준비 조건은 아직 미충족이다.

## 범위 변경 승인·완료

사용자가 “예외 원복만 최소 수정하고 진행해”라고 허용했다. 다음 행동 조회/재배치 예외 원복에 한해 Ending.cs를 수정했고 기존 Edges의 구형 종료 기대값을 현재 계약에 맞췄다. RED 원복 검사를 먼저 확인한 뒤 최종 여섯 진입점 통과. 위의 확인 대기는 해결된 이력이다. 실제 결과와 작업 1~5 완료 근거는 진행 보고의 최종 결과를 따른다.

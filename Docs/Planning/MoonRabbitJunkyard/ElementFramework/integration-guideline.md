# 퍼즐 요소 확장·드론 수정 — 통합 개발 가이드라인

상태: 큰 구간1~3 완료. 3단계는 최종54종690245 PASS/0 FAIL·전량 논리 비교·원본/과거 증거 보존 감사와4단계 문서 인계를 마쳤다. 큰 구간4~5 구현은 미착수다. EF-01~37 이력은 아래 과거 기록으로 유지하며 EF-38 준비안은5구간 체계로 대체했다.

연결: [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [전체 완료 조건](../../../Goals/MoonRabbitJunkyard/ElementFramework/2026-10-04-refactor-goal.md) · [기존 7개 구간 참고안](2026-10-04-refactor-plan.md) · [EF-01 계획](stage-01-obstacle-baseline-plan.md) · [EF-01 목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-01-obstacle-baseline-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-01-command.md)

## 1. 운영 방식

2026-10-05 사용자 결정으로 남은 작업은 5개 큰 구간으로 관리한다. 큰 구간 하나에 계획·목표·실행문을 두고, 내부에서는 완결된 기능 묶음으로 구현·관련 검증을 진행한다. ID 등록이나 조건 하나를 별도 실행 단계로 나누지 않는다. EF-01~37은 과거 기록으로 보존하며 새 큰 구간 번호와 구분한다.

현재 단계와 바로 다음 단계만 상세화한다. 미래 단계의 클래스·메서드·파일 계약을 미리 고정하지 않는다. 현재 단계 완료와 실제 검증 결과를 바탕으로 다음 계획·목표·명령문을 작성한다. 다음 단계 문서를 만드는 것과 다음 단계 구현을 시작하는 것은 구분하며, 완료 보고 후 다음 실행문으로 이어 간다.

한 실행 단계에는 하나의 주요 결과와 그 검증만 포함한다. 피해 규칙 변경과 데이터 전체 변환, 드론 목표 선택과 비행 변경, UI 구조 교체와 리소스 전환처럼 원인을 구분하기 어려운 변경은 나눈다. 의존 관계가 밀접하고 함께 검증해야만 의미가 있는 작업은 같은 단계 안의 체크리스트로 묶는다. 날짜나 파일 수만으로 억지로 쪼개지 않는다.

## 2. 통합 요구사항

- 수백 종류의 요소를 안정적인 `ElementId`, 정의, 인스턴스 상태, 공통 행동, 표현 정의로 관리한다. 같은 행동을 쓰는 새 콘텐츠는 데이터·아트만 추가한다. 실제 새 행동은 구현·등록·검증을 추가한다.
- 기존 매칭·교환·낙하·연쇄 실행 흐름을 유지한다. 반응 조회는 읽기 전용이며 공통 적용 경로가 피해·충전·제거·미션을 변경한다.
- 드론은 미션/목표 특성의 키로 필요한 선택 정책만 활성화한다. 전체 정책 순회와 종류별 분기를 늘리지 않는다. 검색 결과를 공유하고 공통 후보 병합·기여 계산·예약·착탄 재검증을 유지한다.
- 모든 드론은 상승 → 드론별 시간 호버 → 돌진을 사용한다. 수량·조합별 원형 선회는 제거한다. 돌진 중 목표가 무효화되면 현재 위치에서 감속·호버 → 재선정·예약 → 재돌진한다. 필요한 선행 효과 대기는 호버 연장으로 처리한다. 프로펠러 회전과 표적 예고 부재는 유지한다.
- 카탈로그 검색·선택·검사 UI에만 MVVM을 시범 적용한다. 실제 편집은 기존 SerializedObject/Undo 경로를 유지한다. 런타임 uGUI HUD는 Presenter와 표시 상태로 분리하는 MVP 방향이며 블록마다 ViewModel을 만들지 않는다.
- 구형 원본은 호환 경계에서 읽고 버전으로 신형 목록을 구분한다. 새 MemoryPack은 원본에서 재생성하며 50레벨 묶음을 유지한다. 구형 바이너리 자동 호환을 가정하지 않는다.
- 리소스는 배치뿐 아니라 공급·행동·효과가 생성할 종류까지 준비한다. 아틀라스 분할·Addressables·풀 재사용을 유지한다. 공유 아틀라스에 함께 실린 이미지와 불필요한 별도 주소 로딩을 구분한다.
- 자동 플레이는 공개된 상태·행동 특성만 관찰한다. 정의 참조·내부 ID·난수·숨은 내용물·공급·예약 정보를 노출하지 않는다.

드론 비행은 의도적인 동작 변경이다. 기존 선회 경로와 화면 일치를 요구하지 않는다. 목표 선택 결과·피해·미션·규칙 난수는 구조 전환의 보존 대상이다. 시간 변경으로 타격 순서가 달라지는 경우 공통 효과 실행 순서와 정합성을 따로 검증하고 결과 차이를 숨기지 않는다.

## 3. 남은 5개 큰 구간

| 구간 | 완료 결과 |
| --- | --- |
| 1. 요소 정의와 공통 규칙 | 장애물·덮개·바닥·번식·공급의 정의와 실제 반응/적용/미션 연결 |
| 2. 드론 목표 선택과 비행 | 필요한 정책만 조회·후보 공유/예약·모든 드론 상승/호버/돌진·무효 목표 재선정 |
| 3. 저장과 제작 도구 | 정의 ID 저장·선택 변환·MemoryPack·카탈로그 제작·MVVM 시범/Undo |
| 4. 표현과 리소스 | 공통 표현 조회·필요 리소스 로드·풀·HUD·봇 공개 관찰 |
| 5. 확장 검증과 정리 | 데이터 콘텐츠 확장·500개 카탈로그·통합 회귀·검증된 중복 정리 |

기존 A/B의 기준·정의 도입과 C의 완료분은 유지한다. 새1은 C의 남은 범위이고 새2~5는 기존 D~G에 대응한다. 한 구간 내부의 체크포인트마다 다음 단계 문서나 전체 회귀를 반복하지 않는다. 변경 중에는 관련 검사를, 최종 상태에서는 전체 회귀를 수행하며 실패 영향이 넓을 때만 근거에 따라 확대한다.

완료: [1단계 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/phase-01-progress.md) · [2단계 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/phase-02-progress.md) · [3단계 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/phase-03-progress.md). 다음: [4단계 계획](phase-04-presentation-resources-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/phase-04-presentation-resources-goal.md) · [전체 복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/phase-04-command.md). 4~5단계 구현과5단계 상세 문서는 아직 시작하지 않는다. 아래 EF 이력은 과거 기록이며 최신 실행 범위는4단계 계획을 우선한다.

## 4. 단계 시작·진행·종료

### 시작

1. 현재 단계 계획과 목표, 관련 설계 절, 실제 소스/호출부를 읽는다.
2. 기존 변경을 확인하고 이번 작업의 파일·동작 경계를 기록한다. 다른 작업을 되돌리지 않는다.
3. 재현 사례와 관찰 가능한 완료 조건을 확보한다. 기존 실패는 별도 기록한다.
4. 조건을 검증할 최소 작업으로 수행한다. 계획에 없는 주변 정리나 다른 구간 전환을 끼워 넣지 않는다.

### 진행 중 수정

작은 파일 분할·기존 검사 재사용·실측에 따른 구현 세부 조정은 현재 계획에 이유와 결과를 짧게 반영하고 진행한다. 목표 의미, 타격/미션 규칙, 저장 호환성, 사용자 데이터 변경 범위가 달라지면 관련 작업을 보류하고 구체적인 변경안을 논의한다. 독립 작업은 계속할 수 있다.

새 필수 선행 작업이 발견되면 현재 목표를 임의로 크게 늘리지 않는다. 독립 완료가 가능하면 별도 단계로 분리하고 번호·의존성을 갱신한다. 불가능하면 현재 계획·완료 조건을 수정한 뒤 검증 범위를 함께 설명한다. 토큰이나 시간 부족을 완료 조건 축소의 근거로 사용하지 않는다.

### 종료와 다음 단계 준비

실제 검사 결과와 최종 변경 범위를 확인하고 현재 계획·목표 체크박스를 갱신한다. 큰 구간의 검증 기록은 `Docs/Verification/MoonRabbitJunkyard/ElementFramework/phase-NN-progress.md`에 저장한다. 기존 EF의 stage-NN 기록은 보존한다. 실행 결과가 없는 검사 항목은 통과로 표시하지 않는다.

**모든 단계의 완료 보고에는 다음 항목을 포함한다.**

- 변경 결과와 사용자가 확인할 동작, 변경된 핵심 파일.
- 수행한 검사와 결과/증거 경로, 수행하지 못한 검사와 이유.
- 남은 문제, 이번 요구에 따른 의도적 차이, 계획 변경 사항.
- 다음 단계의 목적·범위·이전 단계 의존성과 새 계획/목표 링크.
- 다음 단계 실행 명령문 전체를 복사 가능한 코드 블록으로 제시하고 `Docs/Commands/MoonRabbitJunkyard/ElementFramework/phase-NN-command.md`에도 저장한다.

완료 조건을 충족하지 못한 상태에서는 다음 단계 준비와 착수를 완료처럼 보고하지 않는다. 미충족 항목과 재개 명령문을 제공한다. 독립된 다른 작업으로 이동하려면 그 근거와 의존성을 명시한다. 다음 구현은 사용자의 다음 실행 지시로 시작하며 매 작은 변경마다 재승인을 요구하지 않는다.

## 5. 문서와 공통 제약

- 상세 문서는 현재 실행 단계에만 작성한다. 이미 작성한 전체 설계/완료 조건을 반복 복사하지 않고 링크한다.
- 기획 결정은 논의 종료 또는 새 결정 20건 누적 시 묶어 저장한다. 개발 계획 변경은 단계 경계나 중요한 범위 변경 시 함께 갱신한다. 매 사소한 판단마다 전체 기획서를 다시 저장하지 않는다.
- ServeredMeridian, Unity 6000.3.10f1, 9×9 보드와 현재 패키지·asmdef·폴더 관례를 유지한다. 실제 환경은 실행 전 다시 확인한다.
- 플레이어 빌드·Addressables 콘텐츠 빌드 금지. Unity Editor 컴파일/데이터 검사/필요한 Play Mode 검증은 빌드와 구분한다.
- 사용자 원본·`.meta` GUID·기존 enum 숫자·진행 중 변경을 보존한다. 임의 커밋·씬 저장·Unity 종료·전체 원본 덮어쓰기를 하지 않는다.
- 검사 진입점에 `EditorApplication.Exit`가 있으면 사용자 실행 중인 Editor에서 직접 호출하지 않는다. 에디터 상태를 확인하고 안전한 실행 방법을 선택한다.
- 새 패키지·전면 MVVM·새 asmdef·전체 코드 이동·이미지 재생성은 필수 범위가 아니다. 필요성이 입증되면 해당 단계에서 판단한다.
- 튜토리얼 구현, 고물탑 구현, 상세 매뉴얼은 별도 작업이다. 요소 정의 전환 시 관련 데이터 손실만 방지하며 이번 목표에 구현을 포함하지 않는다.

## 6. 과거 EF 인계 기록

- 완료: [EF-01 고정 장애물 피해 기준](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-01-progress.md), 244 PASS/0 FAIL, 실제 관찰 60건. 런타임 전환·새 드론 비행은 미구현이다.
- 완료: [EF-02 드론 선택·예약·효과 타임라인 기준](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-02-progress.md), 352 PASS/0 FAIL, 실제 관찰 16건. 새 드론 비행은 미구현이다.
- 완료 단계 문서: [EF-03 계획](stage-03-layer-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-03-layer-baseline-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-03-command.md).
- EF-03는 거미줄·먼지 피해 기준만 확보한다. 발전기 전체/번식은 분리하고 다음 상세 계획은 완료 결과를 읽고 작성한다.

- 완료: [EF-03 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-03-progress.md), 198 PASS/0 FAIL, 관찰 13건.
- 완료 단계 문서: [EF-04 계획](stage-04-generator-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-04-generator-baseline-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-04-command.md).

- 완료: [EF-04 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-04-progress.md), 262 PASS/0 FAIL, 관찰 125건.
- 완료 단계 문서: [EF-05 계획](stage-05-mold-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-05-mold-baseline-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-05-command.md).


- 완료: [EF-05 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-05-progress.md), 234 PASS/0 FAIL, 번식 관찰14건. 사용자 승인으로 턴 종료 예외 원복만 수정했다.
- 완료 단계 문서: [EF-06 계획](stage-06-scrap-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-06-scrap-baseline-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-06-command.md).

- 완료: [EF-06 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-06-progress.md), 341 PASS/0 FAIL, 관찰33건. 기존 파일/생산 코드 변경 없음.
- 완료 단계 문서: [EF-07 계획](stage-07-recovery-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-07-recovery-baseline-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-07-command.md).

- 완료: [EF-07 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-07-progress.md), 148 PASS/0 FAIL, 관찰16건. 생산/기존 파일 변경 없음.
- 완료 단계 문서: [EF-08 계획](stage-08-storage-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-08-storage-baseline-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-08-command.md). 저장 변환 구현은 제외하고 메모리 호환 기준만 확보한다.

- 완료: [EF-08 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-08-progress.md), 81 PASS/0 FAIL, 관찰35건. 원본/기존 팩 일치·기존 파일 변경 없음.
- 완료 단계 문서: [EF-09 계획](stage-09-artwork-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-09-artwork-baseline-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-09-command.md). 표현 경로/프레임/아틀라스 대응만 확보하며 리소스 준비/풀·봇은 분리한다.

- 완료: [EF-09 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-09-progress.md), 추가1615 PASS/0 FAIL·기존 파워 검사 통과, 관찰233건. 원본1254px3개와 Editor/월드 바닥 차이는 그대로 기록했다.
- 완료 단계 문서: [EF-10 계획](stage-10-resource-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-10-resource-baseline-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-10-command.md). 리소스 준비·소유권만 확보하고 오브젝트 풀/봇은 분리한다.

- 완료: [EF-10 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-10-progress.md), 200 PASS/0 FAIL·실제 관찰45건. native 번들/핸들/지연 콜백 소유권 확인, 생산 변경 없음.
- 완료 단계 문서: [EF-11 계획](stage-11-pool-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-11-pool-baseline-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-11-command.md). 풀 동작 기준만 확보하고 공용 풀/봇 전환은 분리한다.

- 완료: [EF-11 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-11-progress.md), 82 PASS/0 FAIL·객체 관찰44건·해제 관찰2건. Draw/Reset 책임·실제 풀 재사용/취소/해제 확인, 생산 변경 없음.
- 완료 단계 문서: [EF-12 계획](stage-12-bot-observation-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-12-bot-observation-baseline-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-12-command.md). 봇 공개 관찰/숨은 정보 경계만 확보하고 전략/전체 플레이는 분리한다.

- 완료: [EF-12 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-12-progress.md), 추가102+기존32 PASS/0 FAIL·실제 관찰26건. 공개 계약·숨은 정보 쌍·스냅샷/난수 보존 확인, 생산 변경 없음.
- 완료 단계 문서: [EF-13 계획](stage-13-element-id-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-13-element-id-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-13-command.md). 큰 구간 B의 ID/기존 장애물6종 매핑만 도입하며 기존 소비자/저장/카탈로그 전환은 분리한다.

- 완료: [EF-13 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-13-progress.md), 새49+기존420=469 PASS/0 FAIL·실제 기록24건. 불변 ID/6종 매핑 추가, 기존 파일1,795개 보존.
- 완료 단계 문서: [EF-14 계획](stage-14-catalog-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-14-catalog-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-14-command.md). 읽기 전용 메모리 정의/카탈로그 기본 조회와500개 색인만 구현하며 제작/배포·기존 소비자 전환은 분리한다.

- 완료: [EF-14 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-14-progress.md), 카탈로그1538+기존81=1619 PASS/0 FAIL·관찰1026건. 입력 독립/오류/500개 정순·역순 조회 확인, 기존 파일1806개 보존.
- 완료 단계 문서: [EF-15 계획](stage-15-crate-placement-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-15-crate-placement-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-15-command.md). 대표 상자1종의 배치 크기/최대 내구도만 정의로 연결하며 다른 규칙·저장은 분리한다.

- 완료: [EF-15 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-15-progress.md), 새75+기존1762=1837 PASS/0 FAIL·전후 동일31건/최종41건. Crate 배치 수치만 연결, 기존 보호1812개 중 승인3개 변경·나머지1809개 동일.
- 완료 단계 문서: [EF-16 계획](stage-16-durable-placement-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-16-durable-placement-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-16-command.md). 나머지 내구도형4종의 배치 수치만 연결하고 발전기/피해/저장은 분리한다.

- 완료: [EF-16 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-16-progress.md), 새210+기존2269=2479 PASS/0 FAIL·전후 동일97건/최종101건. 내구도형5종 배치 수치 연결, 보호1818개 중 승인3개 변경·나머지1815개 동일.
- 완료 단계 문서: [EF-17 계획](stage-17-generator-placement-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-17-generator-placement-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-17-command.md). 발전기 배치 크기/충전 허용 수치만 연결하며 실행 행동/저장은 분리한다.

- 완료: [EF-17 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-17-progress.md), 새144+기존2480=2624 PASS/0 FAIL·전후 동일100건/최종110건. 발전기 배치 크기/충전 범위 연결, 보호1820개 중 승인5개 변경·나머지1815개 동일.
- 완료 단계 문서: [EF-18 계획](stage-18-crate-damage-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-18-crate-damage-policy-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-18-command.md). 상자 피해 원인 허용 조회만 연결하며 적용/턴 집계/미션/예약은 유지한다.

- 완료: [EF-18 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-18-progress.md), 새696+기존2721=3417 PASS/0 FAIL·전후 동일559건. 상자 허용 원인 조회만 연결, 보호1824개 중 기존 소스4개 변경·나머지1820개 동일.
- 완료 단계 문서: [EF-19 계획](stage-19-scrap-damage-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-19-scrap-damage-policy-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-19-command.md). 고철1종 허용 조회만 연결하며 고정/공급·적용/미션/예약은 유지한다.

- 완료: [EF-19 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-19-progress.md), 새960+기존3417=4377 PASS/0 FAIL·전후 동일688건. 고철 허용 원인 조회만 연결, 보호1828개 중 기존 생산 파일2개 변경·나머지1826개 동일.
- 완료 단계 문서: [EF-20 계획](stage-20-capsule-damage-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-20-capsule-damage-policy-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-20-command.md). 캡슐1종 허용 조회만 연결하며 미정의 원인 거절/실제 피해·미션·연결 철거는 유지한다.

- 완료: [EF-20 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-20-progress.md), 새592+기존4377=4969 PASS/0 FAIL·전후 동일484건. 캡슐 허용 원인 조회만 연결, 보호1830개 중 기존 생산 파일2개 변경·나머지1828개 동일.
- 완료 단계 문서: [EF-21 계획](stage-21-color-lock-damage-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-21-color-lock-damage-policy-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-21-command.md). 색 자물쇠1종 허용 조회만 연결하며 색 조건/null·미정의 원인·실행 의미는 유지한다.

- 완료: [EF-21 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-21-progress.md), 새4452+기존4969=9421 PASS/0 FAIL·전후 동일2782건. 보호1832개 중 생산2개 변경·나머지1830개 동일.
- 완료 단계 문서: [EF-22 계획](stage-22-appliance-damage-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-22-appliance-damage-policy-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-22-command.md). 금속기둥 허용 조회만 연결하며 칸/hit 집계·실행 의미를 보존한다.

- 완료: [EF-22 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-22-progress.md), 새5085+기존9421=14506 PASS/0 FAIL·전후 동일3040건. 보호1834개 중 생산2줄 변경·나머지1832개 동일.
- 완료 단계 문서: [EF-23 계획](stage-23-generator-reaction-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-23-generator-reaction-policy-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-23-command.md). 발전기 허용 조회만 연결하며 충전/외부 자석 예외·실행 의미를 보존한다.

- 완료: [EF-23 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-23-progress.md), 새24489+기존14506=38995 PASS/0 FAIL·전후 동일8745건. 발전기 허용 조회와 위임 순서만 연결, 생산2파일 최소 변경·원본/GUID 보존.
- 완료 단계 문서: [EF-24 계획](stage-24-color-match-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-24-color-match-policy-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-24-command.md). 색 자물쇠 색 비교 한 조건만 정의로 연결한다.

- 완료: [EF-24 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-24-progress.md), 새20861+기존38995=59856 PASS/0 FAIL·전후 동일8447건. 색 비교 한 조건만 정의 조회로 연결, EF-23 미커밋/원본/GUID 보존.
- 완료 단계 문서: [EF-25 계획](stage-25-damage-aggregation-query-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-25-damage-aggregation-query-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-25-command.md). 내구도형5종의 본체별/칸별 집계 조회 한 책임만 묶어 연결한다.

- 완료: [EF-25 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-25-progress.md), 새75264+기존59856=135120 PASS/0 FAIL·전후 동일38340행. 5종 집계 조회만 정의 연결, EF-23~24/원본/GUID 보존.
- 완료 단계 문서: [EF-26 계획](stage-26-reserved-damage-query-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-26-reserved-damage-query-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-26-command.md). 예약 피해량 조회의 집계 단위만 기존 정책으로 연결한다.

- 완료: [EF-26 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-26-progress.md), 새7793+기존135120=142913 PASS/0 FAIL·전후4540행 전체 바이트 동일. ReservedDamage 한 메서드만 연결, 보호1948개 중1947개 동일.
- 완료 단계 문서: [EF-27 계획](stage-27-damage-record-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-27-damage-record-policy-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-27-command.md). Apply의 본체/칸 기록 선택만 기존 정책으로 연결한다.

- 완료: [EF-27 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-27-progress.md), 새42279+기존142913=185192 PASS/0 FAIL·전후13009행 전체 바이트 동일. Apply 기록 선택만 연결, 보호1950개 중1949개 동일.
- 완료 단계 문서: [EF-28 계획](stage-28-removal-mission-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-28-removal-mission-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-28-command.md). 내구도형 제거 미션 종류 한 책임만 정의로 연결한다.

- 완료: [EF-28 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-28-progress.md),새43431+기존185192=228623 PASS/0 FAIL·전후13475행 전체 바이트 동일. 제거 미션 매핑만 연결,보호1952개 중1948개 동일.
- 다음 실행: [EF-29 계획](stage-29-initial-mission-supply-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-29-initial-mission-supply-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-29-command.md). 최초 배치 본체의 미션 수량 매칭만 기존 정의로 연결한다.

- 완료: [EF-29 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-29-progress.md), 새47201+기존228623=275824 PASS/0 FAIL·전후14558행 전체 바이트 동일. 최초 본체 수량 매칭만 연결, 보호1956개 중1953개 동일·과거3504개/출력37개 보존.
- 완료 단계 문서: [EF-30 계획](stage-30-capsule-adjacent-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-30-capsule-adjacent-policy-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-30-command.md). 회수캡슐 일반 인접의 잔여 종류 거부만 전환하며 자석 경로는 분리해 보존했다. 완료 결과는 아래 EF-30 검증 기록을 따른다.

- 완료: [EF-30 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-30-progress.md), 새48386+기존275824=324210 PASS/0 FAIL·전후15023행 전체 바이트 동일. Safe 일반 인접 조건 한 줄만 연결, 보호1958개 중1957개 동일·과거3905개/출력39개 보존.
- 완료 단계 문서: [EF-31 계획](stage-31-capsule-magnet-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-31-capsule-magnet-policy-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-31-command.md). 회수캡슐 자석 인접의 전단/내부 잔여 거부만 연결한다. 기본false와 다른 종류는 유지했다. 완료 결과는 아래 EF-31 검증 기록을 따른다.

- 완료: [EF-31 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-31-progress.md), 새49166+기존324210=373376 PASS/0 FAIL·최종24종 별도 Editor 종료0·전후16139행 전체 바이트 동일. Safe 자석 두 제한만 연결, 보호1960개 중1958개 동일·과거4159개/출력41개 보존.
- 다음 실행: [EF-32 계획](stage-32-durable-magnet-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-32-durable-magnet-policy-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-32-command.md). 같은 내구도 피해 경로의Crate/Scrap/Appliance 자석 제한을 한 책임으로 묶는다. 기본false와 본체/칸 집계·Safe/ColorLock/Generator 경로는 유지한다. EF-32 구현은 미착수다.

- 완료: [EF-32 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-32-progress.md),427683 PASS/0 FAIL·25종 별도 Editor 종료0. 전후18142행 전체 동일·원본1962개와 과거4440개 보존 감사 통과.
- 다음 준비: [EF-33 계획](stage-33-reaction-behavior-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-33-reaction-behavior-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-33-command.md). 키·등록표·실제 조회 선택을 한 책임으로 연결하며 구현은 미착수다.

- 완료: [EF-33 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-33-progress.md),480736 PASS/0 FAIL·26종 별도 Editor 종료0. 전체 실행18142행 동일·과거4794 변경0·부가 정의 스냅샷5개 복구 근거 보존.
- 다음 준비: [EF-34 계획](stage-34-reaction-apply-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-34-reaction-apply-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-34-command.md). 조회/적용 등록 짝과 실제 공통 효과의 적용 위임을 함께 연결한다. 기존 적용·제거 알고리즘과 상태/기록 소유권은 유지한다. EF-34 구현은 미착수다.

- 완료: [EF-34 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-34-progress.md),534031 PASS/0 FAIL·27종 별도 Editor 종료0. 전체18182행/188팩 동일·기존59출력 복원·과거5134개 보존 감사 통과.
- 다음 준비: [EF-35 계획](stage-35-durability-apply-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-35-durability-apply-policy-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-35-command.md). 등록 내구도 조회/적용의 집계 정책 전달을 연결하고 기존 직접 Apply 호환 경계를 보존한다. 구현은 미착수다.

- 완료: [EF-35 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-35-progress.md),584906 PASS/0 FAIL·28종 실제 종료0. 전체18182행/188팩 동일·63출력 원문 복원·보호1972/WIP98/과거5544/입력37 예상 밖 변경0.
- 완료 단계 문서: [EF-36 계획](stage-36-scrap-supply-mission-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-36-scrap-supply-mission-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-36-command.md). 고철 고정/유지 공급 미션 대상 선택 연결 완료. 아래 최종 결과를 따른다.

- 완료: [EF-36 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-36-progress.md),636061 PASS/0 FAIL·29종 실제 종료0. 정상18182행/188팩 동일·67출력 복원·보호1974/WIP104/과거5964/입력37 예상 밖 변경0.
- 다음 준비: [EF-37 계획](stage-37-scrap-maintain-durability-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-37-scrap-maintain-durability-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-37-command.md). 유지 공급의 내구도 상한 검증만 기존 배치 정의로 연결한다. 구현은 미착수다.

- 완료: [EF-37 검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-37-progress.md),687011 PASS/0 FAIL·30종 실제 종료0. 정상18182행/188팩 동일·71출력 복원·보호1976/WIP110/과거6448/입력38 예상 밖 변경0.
- 다음 준비: [EF-38 계획](stage-38-web-catalog-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-38-web-catalog-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-38-command.md). 기존 거미줄 ID·읽기 전용 정의 등록만 수행하며 덮개 실행/검증 소비는 분리한다. EF38 구현은 미착수다.

- 큰 구간1 완료: [검증](../../../Verification/MoonRabbitJunkyard/ElementFramework/phase-01-progress.md). 다음 큰 구간2 준비: [계획](phase-02-drone-target-flight-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/phase-02-drone-target-flight-goal.md) · [실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/phase-02-command.md). 구간2 구현과 그래픽 검증을 진행했으며 전체 회귀/완료 감사/다음 구간 인계는 진행 중이다.

# 퍼즐 요소 확장·드론 수정 — 통합 개발 가이드라인

상태: **EF-01/02/03/04/05/06/07/08/09/10/11 기준 확보 완료**, 구조 전환 구현 미착수. 현재 준비 단계는 **EF-12**이다.

연결: [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [전체 완료 조건](../../../Goals/MoonRabbitJunkyard/ElementFramework/2026-10-04-refactor-goal.md) · [기존 7개 구간 참고안](2026-10-04-refactor-plan.md) · [EF-01 계획](stage-01-obstacle-baseline-plan.md) · [EF-01 목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-01-obstacle-baseline-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-01-command.md)

## 1. 운영 방식

전체 구조는 아래 7개 큰 구간으로 관리한다. **큰 구간 하나를 한 번의 목표로 실행하지 않는다.** 독립적으로 검증하고 되돌릴 수 있는 작은 작업을 EF-01, EF-02처럼 순서대로 실행한다. 큰 구간 번호와 실행 단계 번호를 구분한다.

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

## 3. 큰 구간과 분할 방향

아래는 진행 순서의 큰 틀이다. 하위 작업은 분할 후보이며 상세 계획을 미리 만든 목록이 아니다.

| 큰 구간 | 결과 | 작은 실행 단계로 나눌 부분 |
| --- | --- | --- |
| A. 현재 기준 확보 | 재현 가능한 동작·데이터 기준 | EF-01 고정 장애물 피해; 이후 드론 선택/효과 순서; 나머지 층·발전기·공급; 저장·표현·봇 경계 |
| B. 정의 기반 도입 | 기존 콘텐츠와 새 정의를 연결 | ID/카탈로그 조회; 구형 종류 어댑터; 대표 한 종류 연결. 런타임 전체 교체는 별도 |
| C. 공통 규칙 전환 | 종류 대신 행동으로 조회·적용 | 단순 장애물 한 계열; 2×2/덮개; 발전기/번식; 미션/공급을 각각 검증 가능한 단위로 이관 |
| D. 드론 목표·비행 | 필요한 정책만 검색하고 새 비행 사용 | 활성 정책 연결; 후보 공유/병합·예약; 무효화/재선정 계약; 모든 드론 상승·호버·돌진; 돌진 중 중단·재돌진 |
| E. 저장·제작 도구 | 정의 기반 레벨 제작·왕복 | 메모리 변환 미리보기; 백업/선택 적용; 팩 버전/50레벨 왕복; 카탈로그 목록; MVVM 시범/Undo |
| F. 표현·리소스·관찰 | 같은 정의를 모든 보드에서 사용 | 표현 조회 통일; 리소스 준비 집합; 풀 초기화/취소; HUD 표시 상태; 봇 공개 관찰 |
| G. 확장 검증·정리 | 실제 제작성과 통합 동작 입증 | 데이터만으로 새 종류 3개; 500개 카탈로그; 전체 회귀/원본 복원; 확인된 중복 분기 정리·인수인계 |

C는 B와 관련 기준 검사 확보 뒤 시작한다. D의 선택 정책은 공통 반응/미션 조회 경계가 준비된 뒤 연결한다. 비행의 중단·재선정은 효과 타임라인의 상태 소유권을 먼저 확정한다. E는 B의 저장 계약이 안정된 뒤 시작할 수 있고 C/D와 한 목표에서 혼합하지 않는다. F는 관련 정의·실행 계약, G는 실제 전환된 기능의 검증 결과에 의존한다. 필요하면 현재 결과를 근거로 구간 순서를 바꾸되 변경 이유와 의존성을 기록한다.

구형 연결 경계를 통해 단계 사이에도 기존 게임과 에디터가 동작해야 한다. 저장과 런타임을 같은 날 전면 교체해야 하는 구조라면 먼저 더 작은 연결 경계를 만든다. 기존 7단계 참고안에 적힌 파일명과 이관 순서는 이 가이드라인 및 승인된 현재 단계 계획을 우선해 조정한다.

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

실제 검사 결과와 최종 변경 범위를 확인하고 현재 계획·목표 체크박스를 갱신한다. 단계별 검증 기록은 `Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-NN-progress.md`에 저장한다. 실행 결과가 없는 검사 항목은 통과로 표시하지 않는다.

**모든 단계의 완료 보고에는 다음 항목을 포함한다.**

- 변경 결과와 사용자가 확인할 동작, 변경된 핵심 파일.
- 수행한 검사와 결과/증거 경로, 수행하지 못한 검사와 이유.
- 남은 문제, 이번 요구에 따른 의도적 차이, 계획 변경 사항.
- 다음 단계의 목적·범위·이전 단계 의존성과 새 계획/목표 링크.
- 다음 단계 실행 명령문 전체를 복사 가능한 코드 블록으로 제시하고 `Docs/Commands/MoonRabbitJunkyard/ElementFramework/stage-NN-command.md`에도 저장한다.

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

## 6. 현재 인계

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
- 다음 실행: [EF-12 계획](stage-12-bot-observation-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-12-bot-observation-baseline-goal.md) · [명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-12-command.md). 봇 공개 관찰/숨은 정보 경계만 확보하고 전략/전체 플레이는 분리한다.

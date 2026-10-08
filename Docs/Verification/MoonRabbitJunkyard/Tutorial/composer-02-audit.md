# 조합형 튜토리얼 2단계 — 완료 조건 대조

기준: [계획](../../../Planning/MoonRabbitJunkyard/Tutorial/composer-02-plan.md), [목표](../../../Goals/MoonRabbitJunkyard/Tutorial/composer-02-goal.md), [확정 기획](../../../Contents/MoonRabbitJunkyard/15_조합형튜토리얼.md). 실행 순서와 실패/수정 이력은 [진행 기록](composer-02-progress.md)에 보존한다.

검사 숫자는 PASS 확인 항목 수이며 독립된 테스트 케이스 수가 아니다. 아래 소스는 모두 `Assets/Scripts/Features/` 아래, 검사는 `Tests/Editor/Features/Tutorial/` 아래에 있다.

## 요구별 증거

| 요구 | 현재 구현과 검증 근거 | 판단 |
|---|---|---|
| 미확정 의미 선확정 | 정확히 N 및 해당 두 파워 조합 원인만 인정하는 사용자 답변을 계획에 기록. `TutorialDurabilityConditionState`, `EffectOrigin` 필터와 3→1/로켓+폭탄 검사 | 충족 |
| A1 개체/종류/영역 | `TutorialTargetQuery/Selection`은 occurrence와 사건 위치를 분리. ConditionsVerification의 실제 이동/소실 조회, 영역 안/밖 선택 검사 | 충족 |
| A2 실제 피해·제거 원인 | `ElementExecutionRecord`의 Before/After/Origin/EventCoordinate를 Adapter가 전달. Effects 검사의 실제 단독/조합 파워와 2×2 최종 타격 | 충족 |
| A3 합계/각각·목록 고정 | `TutorialConditionState`는 Each 목록을 단계 진입 때 고정하고 실제 감소량만 누적. Conditions 검사의 여러 대상/두 행동/사본 분리 | 충족 |
| A4 불가능 요구·대상 부재 | 조건 생성 시 현재 내구도/대상 검사, 정적 역량 검사와 실제 보드 재생 진단. 초과 요구·없는 생성 이름·소실 생성 개체 검사 | 충족 |
| A5 제거 원인·2×2 중복 | 최종 타격 원인 필터와 occurrence 사건 ID 중복 제외. Effects/Conditions의 과잉 피해·본체 제거1개·단독/조합 구분 | 충족 |
| A6 샘플·비활성 이유 | `TutorialSampleCatalog`, `TutorialTargetCapabilities`, Editor 조건 패널. 실제 UI 셀 선택/원인 필터/불가능한 내구도 조건과 독립 샘플 적용 검사 | 충족 |
| A7 경계 검사 | 실제 파워 실행 기반 피해/과잉/2×2, 실제 개체 이동 조회, 사건 좌표 영역 필터, 조건 원인 불일치/중복/누적 검사. 판정 계약 검사와 실게임 검사의 범위를 구분 | 충족 |
| B1 등록 파워·방향 | 카탈로그 Supply.Power 정의를 샘플로 표시. 로켓 양 방향·무관, 폭탄/드론/자석 생성 공용 조건 | 충족 |
| B2 생성 순간 위치 | 생성 기록의 좌표/방향을 보존. 변환 즉시 소모 로켓과 영역 좌표 검사 | 충족 |
| B3 직접 발동/조합 분리 | Adapter가 루트 조작 occurrence와 조합 유무를 전달. 로켓이 건드린 폭탄 연쇄 발동 제외, 조합 별도 집계 검사 | 충족 |
| B4 생성 이름으로 동일 개체 | `TutorialGeneratedBindingState`, firstBinding/secondBinding 해석. 모호/소실/다른 개체 거절 및 생성 후 낙하 로켓을 실게임 다음 단계에서 조작 | 충족 |
| B5 공통 샘플 | 모든 샘플은 공용 동작/조건의 초기값.21종 독립 시험 보드와 예상 결과, 임시 창 소유권 검사. 출시 레벨 자동 생성 없음 | 충족 |
| B6 동시 생성·연쇄·지연 | Conditions의 다중 생성/연쇄 소실/방향/조합 검사, Composer의 이전 단계·시도 지연 사건 거절, 기존 GameIntegration의 실제 지연 드론·표시 완료/재시작 검사 | 충족 |
| C1 영역 입력·기여 안내 | `TutorialBoardAdapter.Guidance`가 독립 실행 사본에서 기여를 판정. 영역 안 입력·벽/인접·무관 후보 거절·캐시/난수 보존,9×9 후보 유무 검사 | 충족 |
| C2 무료 아이템·실제 성공 | 무료 실행 경로와 단계 freeItemCount. 실패/취소 미소진, 실제 효과/연쇄 성공, 세 아이템 실게임 UI, 이동 미소비/재시작 복원 | 충족 |
| C3 미션 증가 | 실제 MissionProgressRecords의 증가량. 미션 선택 식별값 보존, 순서 변경/이미 완료/중복 사건 검사, 미션 샘플 실게임 완료와 스테이지 계속 진행 | 충족 |
| C4 이동 소진·재도전 | 조합형은 표시 종료 후 Cancelled→일반 실패. two 샘플의 실제 실패 팝업 재도전. 기존 무조건형 이동0 무료 체험은 호환 보존 | 충족 |
| C5 안내 오류 정상 복귀 | 실행 중 정상 보드는 일반 공급/입력 복귀, 완료 미기록. 정지/표시/일시정지 경계 검사. 시작/재시작 재생 오류는 진단 후 안내 생략, 손상 데이터/보드 오류는 기존 검사 유지 | 충족 |
| C6 기본 샘플의 실제 경로 | area/mission/hammer/item-swap/shuffle/two와 damage/follow를 저장·재로드 후 Asset/MemoryPack 실제 게임에서 입력.21종 전체는 논리 재생/팩 비교 | 충족 |
| D1 편집→저장→팩→게임 | Workflow의 실제 편집/Undo/오류 이동/저장, Conditions Editor의 새 필드 조립, RunSamples의 내구도 UI 조립과8종×2 실행 경로 | 충족 |
| D2 보고 범위 | 아래 검사 목록/한계와 진행 기록에 실제 수행 범위 명시. 물리 모바일 입력은 미검증 | 충족 |
| D3 다음 단계 인계 | composer-03-plan/goal/command 작성. 공유 흐름·내 샘플·완료 ID·사본 전환/구형 경로 정리 A~E | 충족 |

## 공통 제약과 기획 범위

- 단일 조건 등록 구조와 기존 보드 실행기를 사용한다. 새 장애물별 튜토리얼 엔진을 만들지 않았다.
- 완료 조건은 스테이지 미션과 분리된다. 단계 All/Any, 여러 행동 누적, 초기 충족 상태 자동 진행, 설명의 다음 확인은 기존 Composer 회귀와 Conditions 검사로 확인한다.
- 조건 대상/조작 영역/수동 강조는 분리된다. 보드 전체 조건은 조작 칸만, 특정 개체/종류/영역은 해당 대상과 조작 칸을 합쳐 강조한다. 말풍선은 강조 대상을 피하고 조건 수치는 게임에 표시하지 않는다.
- 고정 공급은 행동/단계 사이에서 소비 위치를 유지하며 완료/진행 가능한 오류 뒤 일반 공급으로 복귀한다. 재시작은 초기 상태로 복원한다.
- 팩3/4 레이아웃과 fixture 재인코딩을 보존하고 팩5 확장에서 대상/원인/연결/아이템/미션 값을 복원한 뒤 검사한다. 팩5 손상13종을 거절한다.50레벨 묶음/Addressables 구성 변경 없음.
- 실제 레벨·팩·메타는 시작 기준 해시와 비교한다. 검사 임시100001 에셋/팩과 하네스는 해제한다. 플레이어 완료 기록을 에디터 시험으로 변경하지 않음을 검사한다.
- 공유 템플릿, 내 샘플 저장, 별도 튜토리얼 완료 ID, 기존 전체 전환과 구형 실행 정리는 기획상3단계이며 이번 완료로 주장하지 않는다.

## 실행 증거

- `TutorialComposerConditionsVerification.Run`: 조건/대상/생성/아이템/미션/편집/21종 샘플/팩 손상/9×9 조회 검사.352개 PASS 기준.
- `TutorialComposerPlayVerification.RunSamples`:8종×Asset/MemoryPack,382개 PASS, 예외0bytes.20261008-144716-869-Run.log.
- `TutorialGameIntegrationVerification.Run`: 기존 세션/취소/이동0/지연 드론/다음 레벨/시작 오류 회귀215개 PASS.20261008-144141-709-Run.log.
- `TutorialComposerVerification.Run`: 기존1단계 core/workflow/play 전체. 최종 실행 결과는 진행 기록의 완료 절에 기록한다.
- 테스트 분리 후 게임 소스 에디터 컴파일1444 exit0. 플레이어 빌드가 아니다.

로그는 `Logs/Tutorial/Composer02`, `Logs/Tutorial/Composer01`, `Logs/Tutorial/Stage03` 및 `Logs/TestHarness`에 있다. 테스트 실행 때 갱신될 수 있으므로 완료 기록에는 실행 시각을 함께 남긴다.

## 한계

- 실제 Unity Play Mode의 합성 포인터/버튼 입력과 가로1280×720·세로720×1280 캡처를 사용했다. Android/iOS 물리 터치·배포 빌드·기기 성능은 검사하지 않았다.
- 21종 모두를 실게임 화면에서 조작한 것은 아니다. 모든 샘플의 논리/팩 재생과 대표8종의 실제 입력을 구분한다.
- 9×9 전체 영역의 후보 없음 검색은 이 PC에서 약0.5초가 측정됐다. 실제 연쇄 계산을 수행하므로 모바일에서 같은 시간이라고 보장하지 않는다. 사전 조회 최적화는 개선되지 않아 원복했다.
- Unity 종료의 JobTempAlloc 경고는 발생한 로그에 기록했으며 원인을 단정하지 않는다. PASS를 실행 예외/컴파일 오류가 없다는 범위 이상으로 해석하지 않는다.

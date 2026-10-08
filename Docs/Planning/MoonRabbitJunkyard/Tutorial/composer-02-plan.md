# 조합형 튜토리얼 2단계 — 조건과 샘플 확장

> 실행: superpowers:executing-plans로 같은 세션에서 진행한다. 하위 에이전트·빌드·커밋·푸시는 실행하지 않는다.

**목표:** 1단계 조립 도구에 내구도·제거·생성·발동·조합·아이템·미션 조건과 대상 지정, 대응 샘플을 확장한다.

**구조:** TutorialHandlerRegistry의 조건 등록을 확장하고 실제 퍼즐 사건/상태를 전달한다. 대상 선택·안내·집계를 분리한다. 장애물 종류마다 별도 튜토리얼 실행기를 만들지 않는다.

**기술:** 현재 C#/Unity, SO 제작, MemoryPack 실행, 기존 UI Toolkit 패널과 테스트 하네스. 신규 패키지/DI/asmdef 도입 없음.

**기획:** [조합형 튜토리얼](../../../Contents/MoonRabbitJunkyard/15_조합형튜토리얼.md)

상태: 완료 · 요구별 증거는 [완료 감사](../../../Verification/MoonRabbitJunkyard/Tutorial/composer-02-audit.md), 실행 이력은 [진행 기록](../../../Verification/MoonRabbitJunkyard/Tutorial/composer-02-progress.md)을 따른다. 다음은 [3단계 계획](composer-03-plan.md)이다.

## 시작 전 확정할 의미

- 확정: 남은 내구도는 정확히 N만 제공한다. 3→1은 정확히2를 충족하지 않는다.
- 확정: 두 파워의 조합 공격은 해당 조합 원인으로만 인정한다. 로켓+폭탄은 단독 로켓/폭탄 제거에 포함하지 않는다.
- 위 두 항목만 관련 구현 전에 한 번에 하나씩 확정한다. 이미 합의한 발동 직접 조작 한정, 제거 지정 원인 한정 등을 다시 묻지 않는다.

## 공통 제약

- 실제 사용자 레벨의 튜토리얼을 대신 만들지 않는다. 기본 샘플과 시험용 보드/예상 결과만 제공한다.
- 기존 WIP·에셋 GUID·출시 팩·플레이어 완료 기록을 보존한다.
- 확정되지 않은 조건을 구현된 것처럼 선택 가능하게 노출하지 않는다.
- 실제 결과와 고정 공급 연속 소비를 사용한다. 조건 UI는 에디터에만 표시한다.
- 팩3/팩4 읽기와 50레벨 묶음·Addressables 정책을 유지한다. 필드 추가 전에 MemoryPack 버전 호환성을 설계한다.
- 소스 외부 Tests/Editor에 검사 유지. 1단계 실제 편집/게임 검사를 회귀로 사용한다.

## A. 대상과 내구도·제거

파일: Tutorial/Data/TutorialConditionDefinition.cs, 신규 Data/TutorialTargetDefinition.cs, Runtime/TutorialConditionEvaluators.cs, TutorialBoardAdapter.cs, Validation/LevelTutorialValidator.cs, Editor/LevelTutorialEditorPanel.Composer.cs.

- [x] 특정 개체/종류/영역 대상을 추가한다. 개체는 이동해도 추적하고 영역은 사건 당시 위치로 판정한다.
- [x] 실제 피해 기록에서 감소량과 최종 제거 원인을 전달한다. 기존 기록이 부족하면 그 기록의 생산자만 좁게 확장한다.
- [x] 합계 감소/각 대상마다 감소를 구현한다. 각 대상마다 목록은 단계 시작에 확정한다.
- [x] 불가능한 감소 요구와 대상 부재를 검사한다. 제거를 부족한 감소량의 대체 성공으로 처리하지 않는다.
- [x] 제거는 허용 원인 중 하나로 실제 제거한 개체 수를 센다. 2×2를 여러 칸 타격해도 제거는 1개다.
- [x] ‘내구도 줄이기’, ‘지정 원인으로 제거’ 샘플과 대상 특성에 따른 비활성/이유 표시를 제공한다.
- [x] 다중 피해·과잉 피해·여러 대상·영역 이탈·이동·원인 불일치·2×2 중복 집계를 실제 사건 기반으로 검사한다.

## B. 생성·발동·조합·생성 대상 연결

파일: 위 조건/대상 계약, TutorialHandlerRegistry.cs, TutorialBoardAdapter.cs, 실제 파워 실행 사건 생산부, TutorialSampleCatalog.cs.

- [x] 가로/세로/방향 무관 로켓, 폭탄·드론·자석 생성 항목을 등록된 콘텐츠 데이터로 제공한다.
- [x] 생성 순간 위치와 보드 전체/선택 영역 필터를 구현한다.
- [x] 직접 조작에 의한 단독 발동만 센다. 연쇄 발동 제외, 조합 발동은 별도 조건으로 분리한다.
- [x] 생성 결과 연결 이름으로 다음 단계의 같은 개체를 추적한다. 여러 생성 결과를 모호하게 선택하거나 소실된 개체를 임의 대체하지 않는다.
- [x] 종류별 샘플은 공통 생성/발동 구성의 기본값으로 제공한다. 전용 진행 코드와 출시 레벨은 추가하지 않는다.
- [x] 생성 뒤 낙하, 여러 동시 생성, 연쇄 소모, 지연 드론 사건, 단독/조합 중복 금지와 각 방향을 검사한다.

## C. 영역 조작·아이템·미션과 안내

파일: TutorialStepHandlers.cs, TutorialProgress.cs, TutorialRuntimeContracts.cs, TutorialBoardAdapter.cs, Editor 패널, 게임 세션 튜토리얼 연결부.

- [x] 허용 영역 안의 유효 행동을 받는다. 안내 후보는 현재 조건에 기여하는 행동에서만 고르고, 후보가 없으면 진단한다.
- [x] 아이템 실제 실행 성공 횟수와 단계별 무료 체험 횟수를 지원한다. 실패/취소로 차감하지 않고 실제 재고를 소비하지 않는다.
- [x] 실제 미션 진행 증가량 조건을 연결한다. 튜토리얼 미션과 스테이지 승리 판단을 혼동하지 않는다.
- [x] 조건 미충족 상태에서 이동 소진 시 정상 실패/재도전으로 복귀한다. 연출 종료 전 승패·단계 전환을 하지 않는다.
- [x] 보드를 계속 진행할 수 있는 안내 오류는 안내만 종료·입력 제한/강조 해제·일반 공급 복귀, 완료 미기록으로 처리한다. 현재 기존 게임 오류 처리와의 차이를 명시하여 검사한다.
- [x] 영역 교환, 아이템, 미션, 고정 공급+두 번 교환 복합 샘플을 추가하고 실제 편집·플레이에서 검증한다.

## D. 통합 검사와 다음 단계 인계

신규 검사 진입점: `Tutorial.Editor.TutorialComposerConditionsVerification.Run`.

```powershell
& Tools/Testing/ProjectTests.ps1 -Action Run -Method Tutorial.Editor.TutorialComposerConditionsVerification.Run
& Tools/Testing/ProjectTests.ps1 -Action Run -Method Tutorial.Editor.TutorialComposerVerification.Run
```

- [x] 조건 조립→저장/재로드→Asset/MemoryPack→실제 실행 동등성을 검사한다.
- [x] 시험 결과와 실제로 확인하지 못한 플랫폼/입력 범위를 구분해 보고한다.
- [x] 완료 뒤 3단계 공유 흐름·사용자 샘플·기존 전환/정리 계획, 목표, 복사 가능한 실행문을 작성한다.

## 중점 검토

피해량과 제거량 혼동(A), 개체 식별자와 좌표 혼동(A/B), 연쇄 발동을 직접 조작으로 잘못 집계(B), 다음 단계로 늦게 도착한 사건(B/C), 오류/이동 소진 중 입력 잠금 잔류(C)를 각각 위 작업의 검사로 고정한다.



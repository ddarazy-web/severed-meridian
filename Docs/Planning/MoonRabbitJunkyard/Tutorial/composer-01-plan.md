# 조합형 튜토리얼 1단계 — 셀 선택부터 실제 매칭 시험까지

> 실행: superpowers:executing-plans를 적용하여 같은 세션에서 순서대로 구현한다. 하위 에이전트·빌드·커밋·푸시는 실행하지 않는다.

**목표:** 제작자가 셀을 선택하여 지정 교환 샘플과 매칭 조건을 조립하고 저장·재로드·게임 시험까지 수행한다.

**구조:** 기존 Tutorial 기능 안에서 편집 데이터, 종류별 조건 판정, 진행 상태, 표현을 분리한다. 새 진행 상태가 기존 실제 퍼즐 사건을 소비하게 하며 퍼즐 규칙을 복제하지 않는다. 기존 레벨은 전환 검증 전 보존한다.

**기술:** 현재 Unity/C#, UI Toolkit 제작 패널, ScriptableObject 제작 데이터, MemoryPack 배포, 기존 테스트 도구. 새 UI/DI 패키지는 추가하지 않는다.

**기획:** [조합형 튜토리얼](../../../Contents/MoonRabbitJunkyard/15_조합형튜토리얼.md)

상태: 2026-10-08 구현 및 Unity 편집·플레이 검증 완료. 실제 사용자 튜토리얼을 대신 제작하는 작업이 아니다.

## 전체 3단계

| 단계 | 완결 범위 |
| --- | --- |
| 1 | 셀 선택·지정 교환 샘플·교환/매칭 조건·실제 게임 시험까지 첫 경로 완성 |
| 2 | 내구도·제거·생성·발동·조합·아이템·미션 조건, 영역 조작과 생성 대상 추적, 해당 샘플 확장 |
| 3 | 공유 흐름과 레벨별 연결, 내 샘플 저장·재사용, 기존 콘텐츠/완료 기록 전환, 구형 경로 제거와 사용 안내 |

각 단계 종료 때 실제 결과를 보고하고 다음 단계의 상세 계획·목표·복사 가능한 실행문을 작성한다. 전체를 다시 수십 단계로 나누지 않는다. 1단계에서도 저장과 실행이 연결되어야 하며 화면 목업만으로 완료하지 않는다.

## 공통 제약

- ServeredMeridian만 수정. 기존 WIP, 실제 Level_01~04와 GUID, 팩을 임의로 덮어쓰지 않는다.
- 9×9, 실제 게임 규칙, 고정 공급 연속 소비, 종료 후 일반 공급 복귀를 보존한다.
- 현재 튜토리얼 팩 버전은 3이다. 기존 payload에 필드를 무조건 추가하지 않는다. 새 버전 읽기/쓰기와 구버전 변환을 명시하여 기존 팩 fixture로 확인한다.
- 신규 데이터는 기존 SO 제작/MemoryPack 실행 정책을 따른다. 에디터 샘플 원본을 런타임 의존성으로 넣지 않는다.
- 자동화 시험은 Tests/Editor에서 관리한다. 테스트를 Assets에 상시 포함시키지 않는다.
- 실제 기기·Unity UI를 확인하지 못하면 해당 검증을 미확인으로 남긴다. 사용자 Unity를 임의 종료하지 않는다.

## 변경 책임과 작업 순서

### A. 데이터·저장 계약

대상: `Assets/Scripts/Features/Tutorial/Data/LevelTutorialDefinition.cs`, `TutorialStepDefinition.cs`, `Assets/Scripts/Features/Levels/Data/LevelPackCodec.Tutorial.cs`.

- [x] 기존 레벨과 팩을 읽는 회귀 fixture를 확보한다. 기존 enum 번호와 직렬화 순서를 확인한다.
- [x] 동작/조건/대상 분리와 신규 직렬화 버전, 구형 데이터를 읽어 새 실행 모델로 옮기는 경계를 ADR에 기록한다. 완료 ID의 레벨별 매핑은 3단계에서 적용하며 지금 자동 추정하지 않는다.
- [x] 신규 `TutorialConditionDefinition.cs`에 교환 성공/매칭, 크기 비교 정확히/이상, 색상, 직접/연쇄, 요구 횟수를 명시한다. 조건 묶음은 All/Any만 지원한다. 영역/내구도 등은 2단계에서 실제 판정과 함께 추가한다.
- [x] 새 데이터의 저장·재로드·팩 왕복과 구버전 읽기 결과를 검사한다. 지원하지 않는 종류는 성공으로 처리하지 않는다.

### B. 실제 판정과 누적 진행

대상: `Runtime/TutorialProgress.cs`, `TutorialBoardAdapter.cs`, `TutorialHandlerRegistry.cs`, `TutorialResultEvaluators.cs`, `Validation/LevelTutorialValidator.cs`, `LevelTutorialReplayValidator.cs` (모두 Tutorial 기능 하위).

- [x] 교환 2회 요구에서 첫 성공 후 실패 종료하지 않고 다음 행동을 받는 실패 검사를 먼저 추가한다.
- [x] 실제 매칭 사건의 발생 지점과 호출부를 확인한다. 필요하면 그 사건에 직접/연쇄 구분과 묶음 크기를 전달한다. 제거 개수나 최종 빈칸으로 매칭을 추정하지 않는다.
- [x] 분리된 3매칭 두 묶음은 2회, 연결 T/L은 1회, 취소 교환은 0회, 직접 전용은 연쇄 제외를 검증하고 등록 판정기로 구현한다.
- [x] 단계 시작 집계 초기화, 같은 단계 여러 행동 누적, All/Any, 이전 단계 지연 사건 무시를 검사한다.
- [x] 조건 충족 후에도 낙하·피해·드론 등 해당 행동의 실제 연출 완료까지 대기한다. 입력·일시정지·종료·고정 공급 복귀는 기존 경계를 사용한다.

### C. 제작 UX와 기본 샘플

대상: `Editor/LevelTutorialEditorPanel.cs`, 기존 `LevelBoardView`의 대상 선택 연결부. 신규 `Editor/TutorialSampleCatalog.cs`는 샘플 메타데이터와 복사할 기본 구성을 소유한다.

- [x] 보드 선택 API와 취소 구독 해제를 확인하고 셀 선택이 배치 데이터에 영향을 주지 않는 검사를 추가한다.
- [x] 단계 목록/중앙 보드/설정/테스트 영역을 기존 편집 화면에 통합한다. 처음부터 새 창과 별도 편집 프레임워크를 만들지 않는다.
- [x] 두 칸 선택 → 지정 교환 샘플 미리보기 → 적용 → 매칭 조건 추가 → 수치 수정 경로를 제공한다. 전체 조건 카탈로그 중 미구현 항목은 사용 가능하게 보이지 않는다.
- [x] 조건 대상·조작 셀·강조 셀은 분리 저장하되 강조는 기본 자동으로 채운다. 선택 목적 안내·취소, 단계 복제/정렬, Undo/Redo, 오류 위치 이동을 제공한다.
- [x] 샘플 적용은 원본과 독립적인 복사다. 적용 후 수정·복제가 원본과 다른 단계에 영향을 주지 않음을 검사한다.
- [x] 시험용 보드와 예상 결과를 테스트/샘플 영역에 마련한다. 실제 사용자 레벨을 덮어쓰거나 새 출시 레벨을 자동 작성하지 않는다.

### D. 사용 가능한 첫 경로 검증

신규 검사: `Tests/Editor/Features/Tutorial/TutorialComposerVerification.cs`, 진입점 `Tutorial.Editor.TutorialComposerVerification.Run`.

- [x] 위 A~C 경계 검사를 해당 진입점에 연결한다. 기존 튜토리얼 회귀 중 영향을 받는 항목도 실행한다.
- [x] 에디터에서 셀 선택 → 샘플 적용 → 조건 수정 → Undo/Redo → 저장·재로드 → Asset/MemoryPack 실행을 확인한다.
- [x] 실제 화면에서 포커스 투명 영역, 조작 제한, 조건 UI 비노출, 매칭과 연출 완료 후 진행을 확인한다. 실행하지 못하면 완료 조건에서 제외하지 말고 미검증으로 보고한다.
- [x] 검증 결과와 알려진 기존 실패를 분리 기록하고 2단계 계획·목표·실행문을 작성한다.

신규 검사 구현 후 실행 명령:

```powershell
& Tools/Testing/ProjectTests.ps1 -Action Run -Method Tutorial.Editor.TutorialComposerVerification.Run
```

검사 진입점 구현 완료: 핵심 조건/저장 → 실제 편집 창 → Asset/MemoryPack 실제 Play Mode 순서로 실행한다. 전체 프로젝트 검사 명령은 아니다. 검증 범위와 실기기 미확인은 완료 보고를 따른다.

## 중점 검토

1. 취소/Undo/대상 변경으로 부분 설정 또는 잘못된 강조가 남지 않는가 — C에서 검사.
2. 직접 매칭과 연쇄, 연결/분리 묶음이 뒤섞이지 않는가 — B에서 검사.
3. 샘플 복사 이후 원본/다른 단계가 함께 변하지 않는가 — C에서 검사.
4. 구버전 팩과 무튜토리얼 레벨이 계속 정상적으로 읽히는가 — A에서 검사.
5. 첫 행동 후 미충족이 오류가 되거나 지난 사건이 다음 단계에 누적되지 않는가 — B에서 검사.

[완료 보고](../../../Verification/MoonRabbitJunkyard/Tutorial/composer-01-progress.md) · [다음 단계 계획](../../../Planning/MoonRabbitJunkyard/Tutorial/composer-02-plan.md) · [다음 목표](../../../Goals/MoonRabbitJunkyard/Tutorial/composer-02-goal.md) · [복사용 실행문](../../../Commands/MoonRabbitJunkyard/Tutorial/composer-02-command.md).

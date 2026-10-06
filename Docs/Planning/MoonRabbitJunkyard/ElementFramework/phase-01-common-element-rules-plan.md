# 큰 구간 1단계 — 요소 정의와 공통 규칙 전환 계획

> 실행자는 `superpowers:executing-plans`로 직접 진행한다. 별도 승인 없이 하위 에이전트·커밋·푸시를 사용하지 않는다.

상태: 완료. 최종34종 실제 종료0/687830 PASS/0 FAIL과 전체 보호 감사 통과. 완료일: 2026-10-06.

**Goal:** 기존 장애물·덮개·바닥·번식·공급의 실행을 정의와 등록된 공통 행동으로 연결하여, 같은 행동의 콘텐츠 추가가 종류별 실행 분기 추가를 요구하지 않게 한다.

**Architecture:** 기존 `ElementId`/불변 정의/카탈로그/행동 등록표를 확장한다. 읽기 전용 판정과 상태 적용을 분리하며 실제 상태·턴 기록의 소유자는 기존 시뮬레이션에 유지한다. 구형 enum은 입력 호환 경계로 남기고 매칭·파워·낙하 엔진을 재작성하지 않는다.

**Tech Stack:** Unity 6000.3.10f1, 기존 C#·Editor 검사·MemoryPack·현재 패키지와 asmdef.

**Spec:** [통합 설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md), [통합 가이드](integration-guideline.md).

연결: [완료 조건](../../../Goals/MoonRabbitJunkyard/ElementFramework/phase-01-common-element-rules-goal.md) · [실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/phase-01-command.md).

## 전체 5구간에서의 위치

1. **이번 구간: 요소 정의와 공통 규칙.**
2. 드론 목표 선택과 상승·호버·돌진.
3. 저장 변환과 레벨 제작 도구, MVVM 시범 적용.
4. 표현 조회·리소스·풀·HUD·공개 관찰.
5. 실제 콘텐츠 확장 검증과 통합 정리.

EF-01~37은 보존하는 과거 실행 기록이다. 이 문서의 1단계는 EF-01을 다시 실행하는 의미가 아니다. 아직 구현하지 않은 EF-38의 ID 전용 계획을 이 범위에 흡수한다. 완료된 작업은 재구현하거나 원복하지 않는다.

## 시작 기준과 범위

EF-37까지 6종 장애물의 배치·피해 정책·집계·반응 조회/적용·제거 미션 및 일부 고철 공급 검증이 연결되었다. 이전 검증 보고는 30개 메서드, 687011 PASS/0 FAIL이며 이번 작업의 검증 결과로 재사용 표기하지 않는다.

남은 실제 경로는 `LayerDamageRules.cs`의 WebRules/DustRules, `MoldRules.cs`의 제거·턴 종료 번식, `GeneratorRules.cs`의 활성화·연결·퇴역, `SettlementResolution.cs`의 공급과 생성이다. 장애물의 남은 종류 분기도 호출부를 대조해 이번 범위 안의 실행 분기를 전환한다.

일반 블록과 파워 블록의 매칭·생성·조합 알고리즘은 보존한다. 공급이 이들을 생성할 때 기존 생성 경로를 호출하며 장애물 행동에 억지로 통합하지 않는다. 일반/파워 콘텐츠의 전체 제작 카탈로그와 표현 전환은 후속 구간의 범위다.

## 공통 제약

- ServeredMeridian의 `work` 작업 상태와 기존 WIP를 보존한다. 시작 시 실제 브랜치·HEAD·변경 목록을 기록한다.
- 9×9 보드, enum 숫자, 기존 정의 ID, 에셋·GUID·오류 계약과 정상 밸런스를 보존한다.
- 빌드·Addressables 콘텐츠 빌드·팩 파일 재생성·이미지 생성·임의 씬 저장·사용자 Editor 종료를 하지 않는다.
- 저장 스키마/DTO·MemoryPack 버전1·50레벨 구간은 변경하지 않는다. 검사는 메모리에서 수행한다.
- 새 패키지·asmdef·전면 MVVM·범용 스크립트 실행기·생산 리플렉션을 도입하지 않는다.
- 드론 정책/비행, 에디터 카탈로그 UI, 리소스 로딩과 표현 변경은 다음 구간으로 남긴다.
- 등록은 실제 행동 키로 직접 조회한다. 모든 등록 행동을 순회해 지원 여부를 묻지 않는다.

## Review Focus

1. 거미줄을 벗긴 같은 타격이 내용물까지 소비하거나 턴당 피해가 중복되지 않아야 한다: 묶음 A 검사.
2. 먼지는 일반 블록의 실제 소비/변환에만 반응하며 파워 범위 접촉만으로 깎이지 않아야 한다: 묶음 A 검사.
3. 곰팡이 제거·번식 억제·벽·후보 순서·난수 소비와 실패 시 원복이 보존되어야 한다: 묶음 B 검사.
4. 2×2 본체의 범위 중첩 피해와 제거 미션은 본체 단위 계약을 유지해야 한다: 묶음 C 및 최종 검사.
5. 공급 소진·고정 순서·회수·신규 블록만의 대각선 낙하·동시 낙하 결과가 바뀌지 않아야 한다: 묶음 C 검사.

## 내부 작업 묶음

아래 네 묶음은 **1단계 안의 체크포인트**다. 각각을 다시 EF 번호로 잘라 별도 계획·목표·전체 회귀·사용자 재승인을 요구하지 않는다. 완료 가능한 결과를 확인하며 순서대로 진행한다.

### A. 덮개·바닥의 정의와 반응 전체 연결

대상: `Assets/Scripts/Features/Elements/{Data,Runtime}/`, `Assets/Scripts/Features/Obstacles/Rules/LevelPlacementRules.cs`, `Assets/Scripts/Features/Obstacles/Runtime/LayerDamageRules.cs`, `DamageReaction.cs`, `Assets/Scripts/Features/PowerBlocks/Runtime/PowerEffectResolution.cs`, `Assets/Scripts/Features/Missions/Runtime/MissionProgressRules.cs`.

계약: 기존 `LegacyElementMap.Get(ObstacleKind)`와 `LegacyElementDefinitions.Get(ObstacleKind)`를 유지하며 `Get(CoverKind)` 오버로드로 Web/Mold를 연결한다. ID는 `cover.web`, `cover.mold`, 먼지는 `floor.dust`로 정한다. 먼지는 현재 별도 enum이 없으므로 새 가상 enum을 만들지 않고 정의 조회 진입점으로 연결한다. 배치 수치와 피해 제한은 실제 현재값을 기준으로 정의하고, 층별 상태 접근과 적용은 해당 규칙이 소유한다. 기존 장애물 반응 서명에 먼지 소비를 가짜 타격으로 끼워 넣지 않는다.

- [x] 현재 입력→판정→적용→미션 호출부와 동작 기준을 기록하고 거미줄 중복 타격/내용물 보존, 먼지 소비/변환/범위 접촉의 실패 검사를 먼저 확보한다.
- [x] ID·정의·배치 검증·행동 등록·실제 적용·제거 미션을 한 묶음으로 연결한다. 메타데이터만 등록하고 종료하지 않는다.
- [x] 관련 기존 층 검사와 새 공개 실행 경로 검사를 통과시키고 정상 상태·턴 기록·미션·난수 일치를 확인한다. 거미줄 최대3 및 곰팡이 최대1의 현재 배치 계약을 유지한다.

### B. 곰팡이 번식과 발전기 생명주기 연결

대상: `Elements/Data`, `Elements/Runtime`, `Obstacles/Runtime/MoldRules.cs`, `Obstacles/Runtime/GeneratorRules.cs`, 기존 턴 종료 호출부와 발전기 연결 검증 경로.

계약: 현재 `MoldRules.FinishTurn(LevelRuntimeState, TurnEffectContext)`와 `GeneratorRules.Query/Apply/TargetRemoved` 호출 계약을 보존한다. 번식은 턴 종료, 발전기는 충전/활성화 경계의 명시적 등록 행동만 실행한다. 피해 정책과 턴 종료 정책의 수명주기를 구분하며 새로운 범용 이벤트 버스를 만들지 않는다.

- [x] 이동 미소비·미션 완료·제거된 턴·후보 없음·벽·후보 정렬·단일 후보 및 다중 후보 난수, 발전기 충전/여러 연결/타겟 제거/퇴역 검사를 확보한다.
- [x] 고유 조건과 수치를 정의/행동에 연결하고 같은 행동의 다른 정의에서도 재사용되게 한다. 현재 상태와 기록 형식은 유지한다.
- [x] 기존 발전기·곰팡이 검사와 실패 원복 사례를 실행해 효과 순서, 미션 목표 증가, 규칙 난수 소비가 동일함을 확인한다.

### C. 장애물·미션·실제 공급의 연결 완결

대상: `Obstacles/Runtime/ObstacleDamageRules.cs`, `Obstacles/Rules/LevelPlacementRules.cs`, `Missions/Rules/LevelMissionRules.cs`, `Missions/Runtime/MissionProgressRules.cs`, `BlockSupply/Rules/LevelSupplyRules.cs`, `BoardFlow/Runtime/SettlementResolution.cs` 및 실제 호출부.

계약: 공급 선택·순서는 현재 엔진이 소유하고 선택된 항목의 생성/초기 상태/미션 연결은 정의를 소비한다. 현재 `SupplyItem`/enum·저장 DTO는 호환 입력으로 유지한다. 제거 미션의 대상·수량은 기존 프로필과 같은 집계 경로를 사용하며 새 콘텐츠마다 MissionKind를 늘리지 않는 내부 연결을 마련한다. 공개 구형 미션 입력은 유지한다.

- [x] 남은 종류 분기를 목록화하여 입력 호환 변환/표시와 실제 행동 분기를 구분한다. 기존 6종에서 이미 연결된 경로를 재작성하지 않는다.
- [x] 최초/고정/유지 공급·회수·소진·일반/파워 생성과 본체 생성의 실제 경로를 정의 기반으로 연결한다. 같은 ID가 필요한 프로필을 잃으면 명확히 거절하고 기본 상자로 대체하지 않는다.
- [x] 2×2 중첩 범위·자석+자석 4피해·본체 제거 1회, 색 제한, 캡슐 피해 거부, 공급 수량·순서·미션·난수, 신규 대각선/상단 직선 우선/동시 낙하의 기존 검사를 통과시킨다.

### D. 데이터 재사용 입증과 1단계 통합 인계

대상: `Assets/Scripts/Features/Elements/Editor/Tests/`의 기존 검사와 이번 구간 검사, `Docs/Verification/MoonRabbitJunkyard/ElementFramework/phase-01-progress.md`.

- [x] 테스트 전용 카탈로그에 같은 행동을 사용하는 다른 ID의 내구도형·덮개형 정의를 넣어 종류별 실행 분기를 추가하지 않고 실제 판정→적용→제거/미션을 수행한다. 단순 Get 성공만으로 확장을 입증하지 않는다. 실제 제작 에셋/UI 지원은 구간3에서 검증한다.
- [x] EF-37 최종 30개 메서드를 기반으로 변경 영역에 필요한 검사를 추가하고 최종 소스 상태에서 전체 회귀를 수행한다. 각 실행의 실제 종료 코드·FAIL·원문 결과를 기록한다.
- [x] 같은 입력/시드의 전체18182행·188개 메모리 팩·225팩 상태를 비교한다. 새 등록 메타데이터 차이는 따로 설명하고 기존 실행 결과 차이를 정규화해 숨기지 않는다.
- [x] 기존 검사 출력은 백업 후 독립 실행 결과를 보존하고 원문으로 복원한다. 원본 에셋/GUID/기존 WIP/과거 증거가 유지되는지 최종 변경 목록으로 확인한다.
- [x] 요구사항별 완료/미완료, 책임/데이터 흐름/확장 방법, 남은 호환 분기를 보고한다. 2단계 드론 계획·목표·복사용 실행문을 작성하되 구현은 자동 시작하지 않는다.

## 검증 운영

묶음별 변경 중에는 해당 실패 재현 검사와 관련 기존 검사만 실행한다. ID 한 개나 조건 하나를 바꿀 때마다 30개 전체 검사와 대용량 비교를 반복하지 않는다. 전체 회귀는 시작 기준 확보와 최종 통합 검증에 묶는다. 중간 검사에서 다른 영역의 영향이 발견된 경우에만 근거를 기록하고 범위를 넓힌다.

현재 관련 검사의 정확한 진입점은 소스의 `Before/Red/Run` 및 EF-37 `verified-results.json`에서 확인한다. 명령은 해당 컴퓨터의 Editor 상태를 확인한 뒤 별도 Unity 검사 프로세스로 실행하고, `EditorApplication.Exit`를 포함하는 검사를 사용자 Editor에 주입하지 않는다. 실패 검사는 실제 동작 누락을 보여야 하며 fixture 오류/컴파일 실패를 기능 RED로 인정하지 않는다.

이번 문서 작성은 구현 완료가 아니다. 실행 중 확인된 세부 조정은 이 계획에 묶어 기록하며, 기존 동작·저장 계약 또는 목표가 바뀌는 결정을 발견하면 해당 변경만 논의한다.

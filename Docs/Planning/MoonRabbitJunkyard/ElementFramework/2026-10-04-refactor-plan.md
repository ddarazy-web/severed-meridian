# 퍼즐 요소 확장 구조 리팩토링 — 7단계 구현 계획

> 2026-10-04 운영 방식 변경: 이 문서는 기존 큰 구간과 영향 파일을 보존하는 참고안이다. 실제 실행 순서와 작은 단계의 범위는 [통합 개발 가이드라인](integration-guideline.md)과 현재 EF 단계 문서를 우선한다. 7개 구간 전체를 한 번에 실행하지 않는다.

> 실행 담당: 승인된 단계를 `superpowers:executing-plans`로 순서대로 수행한다. 병렬 에이전트는 별도 요청이 있을 때만 사용한다. 체크박스는 실제 완료 후 표시한다.

**목표:** 수백 종류를 공통 정의와 소수 행동으로 추가하면서 기존 퍼즐 동작을 보존한다.

**구조:** 정의·상태·행동·표현을 분리하고 구형 종류 연결을 통해 점진 전환한다. 기존 교환·매칭·낙하·연쇄 실행기를 유지한다.

**기술:** 현재 Unity/C#·MemoryPack·Addressables·UniTask와 기존 Editor 검사. 새 패키지·asmdef는 추가하지 않는다.

**설계 기준:** [설계안](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/ElementFramework/2026-10-04-refactor-goal.md)

상태: 전환 계획 제안, 실행 미착수. 각 단계는 앞 단계 검증 후 시작한다. 승인된 단계의 상세 코드 계약을 확정하고 독립적으로 되돌릴 수 있는 단위로 구현한다.

## 공통 제약

- ServeredMeridian만 변경하고 기존 작업·에셋·`.meta` GUID를 보존한다.
- 기존 enum 숫자를 재배열하지 않는다. 저장 순서·타입 변경은 버전과 변환을 함께 처리한다.
- 9×9 보드, 50레벨 MemoryPack, 종류별 Addressables 아틀라스와 기존 풀·취소 처리를 유지한다.
- 플레이어 빌드·Addressables 콘텐츠 빌드는 하지 않는다. 빌드를 포함하는 검사는 제외 사실을 보고한다.
- 실행 중인 Unity 종료·씬 저장·전체 원본 자동 덮어쓰기를 하지 않는다.
- 원본 변환은 미리보기·백업·메모리 비교 후 선택 실행으로 제공한다.
- 사용자 요청 없이 변경을 커밋하거나 무관한 폴더를 재구성하지 않는다.
- 상세 매뉴얼은 최종 제작 흐름이 정리된 뒤 추가한다.

## 검토 위험

1. 2×2 타격별 칸 피해와 일반 본체의 턴당 피해 제한.
2. 덮개·바닥·내용물의 반응 순서와 제거 미션 집계.
3. 구형 바이너리 오판독·신형 빈 배치의 구형 복원·누락 ID.
4. 초기 배치에 없는 공급·고유 행동·파워 효과의 리소스.
5. 봇의 숨은 내용물·난수·공급·내부 ID 관찰 누출.

각 위험의 검사는 해당 단계에 포함한다. 광범위한 스냅샷만으로 성공을 판단하지 않고 피해 횟수·미션·리소스 주소·공개 관찰 등을 직접 비교한다.

## 단계와 의존

| 단계 | 결과 | 의존 |
| --- | --- | --- |
| 1 | 현재 동작 기준과 변환 영향 목록 | 없음 |
| 2 | 정의 카탈로그·구형 종류 연결 | 1 |
| 3 | 배치 저장과 팩 전환 경계 | 2 |
| 4 | 피해·발동·미션·공급 공통 규칙 | 3 |
| 5 | 카탈로그 기반 레벨 에디터 | 4 |
| 6 | 표현·리소스·풀·공개 관찰 연결 | 4, 5 |
| 7 | 통합 전환·규모 검사·분기 정리 | 6 |

## 1단계 — 기준 확보

**기존 파일:** `Assets/Scripts/Features/Obstacles/Editor/Tests/`의 고정 장애물·발전기·덮개·곰팡이·고철 검사, `PowerBlocks/Editor/Tests/`의 효과·조합 검사, `Levels/Editor/Tests/LevelPackVerification.cs`, `AutoPlay/Editor/Tests/BotObservationVerification.cs`, `GameScreen/Editor/Tests/`의 표시·풀·취소 검사를 재사용한다.

**신규 진입점:** `Assets/Scripts/Features/Elements/Editor/Tests/ElementRefactorBaselineVerification.cs`의 `Levels.Editor.ElementRefactorBaselineVerification.Run()`. 결과는 `Logs/ElementRefactorBaseline/`에 시드·행동 목록·실행 버전·성공/실패와 함께 기록한다.

- [ ] 기존 메뉴·실행 메서드와 빌드 호출 여부를 확인하고 빌드를 포함하는 검사를 제외한다.
- [ ] 상자 인접 피해, 캡슐 파워 피해, 색 제한, 2×2 중첩 타격, 발전기 연결, 거미줄·곰팡이·먼지, 파워 조합의 고정 사례를 확보한다.
- [ ] 고정 시드·행동 목록의 칸/본체 상태·이동·미션·효과 기록을 저장한다. 객체 참조·enum 이름만으로 비교하지 않는다.
- [ ] 드론의 남은 미션 우선·색 직접 기여·일반 대체·범위 효과·여러 드론 예약·착탄 재선정과 조회의 난수 미소비를 기존 목표 검사로 기준화한다.
- [ ] 에셋/MemoryPack 왕복, 숨은 정보 변경 쌍, 풀 재사용·취소를 기존 검사로 연결한다. 직렬화 검사는 번들 빌드와 분리한다.
- [ ] 통합 진입점을 실행해 결과 파일과 실제 검사 종료를 확인한다. 기존 실패는 분리해 보고하고 기준 없이 전환을 시작하지 않는다.

**완료:** 규칙 변경 없이 기준 결과와 종류별 수정 위치를 확보한다. 실행하지 못한 검사는 별도 표시한다.

## 2단계 — 카탈로그와 구형 연결

**신규 파일:** `Assets/Scripts/Features/Elements/Data/ElementId.cs`, `ElementDefinition.cs`, `PackedElementDefinition.cs`, `ElementDamagePolicy.cs`, `ElementVisualDefinition.cs`; `Elements/Runtime/ElementCatalog.cs`; `Elements/Editor/Migration/LegacyElementMap.cs`; `Elements/Editor/Tests/ElementCatalogVerification.cs`.

**계약:** namespace `Elements`, `ElementCatalog.Get(ElementId)`는 읽기 전용 정의를 반환하고 누락 ID를 거절한다. 구형 연결은 기존 종류와 일대일 대응하는 영구 ID 표를 제공한다.

- [ ] 중복/누락 ID·잘못된 점유 영역·상충 정책·배포 DTO 왕복 검사를 먼저 작성한다.
- [ ] 기존 장애물·덮개·바닥·네 파워를 같은 의미의 정의로 작성한다. `Safe`의 현재 회수캡슐 표시와 동작을 유지한다.
- [ ] ID 인덱스·정의 변환을 구현하고 모든 정책·표현 키를 비교한다.
- [ ] 카탈로그 검사를 실행하고 1단계 기준을 확인한다. 기존 실행 경로는 아직 교체하지 않는다.

**완료:** 공통 정의 원본을 확보하고 기존 게임은 기존 경로로 실행된다.

## 3단계 — 배치와 저장 전환

**신규 파일:** `Elements/Data/ElementPlacementDefinition.cs`, `Elements/Editor/Migration/ElementLevelMigration.cs`, `Elements/Editor/Tests/ElementMigrationVerification.cs`.

**수정:** `Levels/Data/LevelDefinition.cs`, `PackedLevel.cs`, `LevelPackCodec.cs`와 기존 팩 생성 연결부.

**계약:** `ElementLevelMigration.Preview(LevelDefinition)`은 저장하지 않고 메모리 변환 결과와 변경 목록을 반환한다. 원본 저장은 별도 선택 실행이다. 스키마로 유일한 원본 배치 목록을 선택한다.

- [ ] 본체 ID/정의 ID, 위치·2×2 점유·발전기 연결·내구도·색·충전·덮개·바닥·공급·미션 변환 비교를 작성한다.
- [ ] 새 배치와 구형 읽기 경계를 도입한다. 신형 빈 목록에서 구형 데이터를 되살리지 않는 검사를 추가한다.
- [ ] 스키마·팩 버전을 변경하고 원본에서 새 팩을 재생성한다. 구형 바이너리 직접 읽기 대신 명확한 버전 오류를 유지한다.
- [ ] 카탈로그 호환 버전·누락 ID·미지원 행동 검사와 50레벨 구간 왕복을 확인한다.
- [ ] 미리보기·백업·복구·메모리 비교가 통과한 뒤 선택 원본 적용 도구를 제공한다. 전체 자동 저장은 하지 않는다.

**완료:** 구형/신형 원본을 명확히 구별하고 새 팩이 동일 실행 상태로 복원된다.

## 4단계 — 공통 행동 실행

**신규 파일:** `Elements/Runtime/ElementBehaviorRegistry.cs`, `ElementReactionQuery.cs`, `ElementReactionExecutor.cs`, `Elements/Editor/Tests/ElementRuleParityVerification.cs`. 고유 행동 파일은 실제 이관하는 발전기·번식·파워에 필요한 것만 생성한다.

**수정:** `Obstacles/Runtime/ObstacleDamageRules.cs`, `DamageReaction.cs`, `LayerDamageRules.cs`, `GeneratorRules.cs`, `MoldRules.cs`; `PowerBlocks/Runtime/PowerEffectResolution.cs`, `PowerCombinationResolution.cs`; `Missions/Runtime/MissionProgressRules.cs`; `PuzzlePlay/Runtime/Actions/BoardActionExecutor.cs`와 관련 상태·조회·공급 연결부.

**계약:** 조회는 정의·상태·피해 원인·턴 문맥으로 반응을 계산하고 상태를 변경하지 않는다. 적용 경로만 변경한다. 종류 ID가 아니라 행동 키로 실행 코드를 선택한다.

- [ ] 기준 사례를 구형/신형 양쪽에서 비교하는 검사를 먼저 연결한다.
- [ ] 피해 원인·색·피해량·집계 단위를 공통 정책으로 옮기고 덮개 우선·본체/칸별 제한·제거 집계를 보존한다.
- [ ] 발전기·번식의 기존 알고리즘을 고유 행동 경계로 연결한다. 실행 순서와 한 타격당 적용 횟수를 검사한다.
- [ ] 파워 범위·연쇄는 공통 엔진을 유지하고 정의와 행동을 참조한다. 기본 행동의 새 종류 때문에 파워 엔진에 조건문을 늘리지 않는다.
- [ ] 미션 태그·공개 행동 특성을 연결해 드론·미션·공급의 종류별 연결을 줄인다.
- [ ] 상충 정책·미지원 행동을 거절하고 구형/신형이 한 행동에 중복 적용되지 않는지 검사한다.
- [ ] 칸 상태·내구도·충전·이동·미션·피해·공급 결과의 기준 일치를 확인한다.

**완료:** 기존 콘텐츠를 같은 행동으로 처리하고 새로운 종류가 같은 정책을 재사용한다.

**드론 선택 정책:** 별도 하위 작업으로 `PowerBlocks/Runtime/Targeting/MissionTargetPolicyRegistry.cs`, `ActiveMissionTargetPolicies.cs`, `MissionTargetCandidateIndex.cs`와 `PowerBlocks/Editor/Tests/MissionTargetPolicyVerification.cs`를 추가한다. 기존 `DroneTargetManager`는 후보 병합·예약·재검증의 소유자로 유지한다. 정책은 규칙상 목표 해결 방식마다 등록하며 정의 ID마다 클래스를 만들지 않는다.

- [ ] 키로 필요한 정책만 연결하는 검사부터 작성한다. 등록 정책이 많아도 해당 레벨의 활성 정책만 호출되고 같은 정책의 검색은 공유되는지 호출 횟수로 확인한다.
- [ ] 미션 완료로 비활성화되지만 현재 대상이 없는 공급 미션은 유지되는지 검사한다. 새 요소가 등장하면 후보에 반영한다.
- [ ] 층·색·태그·본체별 후보 검색 결과를 공유한다. 효과 범위가 미션 대상에 닿을 수 있는 주변 조준 중심을 놓치지 않는지 검사한다.
- [ ] 여러 정책의 같은 좌표와 같은 본체 기여를 병합하고 예약·예상 피해·충전을 공통 규칙으로 계산한다.
- [ ] 검색은 순수 조회로 두고 최종 선택에서만 기존 난수를 소비한다. 동률 선택과 미션/일반 대체 순서를 기준 사례와 비교한다.
- [ ] 보드·미션·예약·턴 피해 문맥 변경, 목표 소실, 효과 작업 사본 교체 후 캐시가 잘못 재사용되지 않는지 검사한다.
- [ ] 새 우선순위 밸런스를 섞지 않고 기존 선택 의미를 보존한다. 정책 누락은 설정 오류로 표시한다.
- [ ] 정책 실행·후보·캐시·선택 근거를 진단 기록에 남기고, 미리 표적을 보여주는 플레이 UI는 추가하지 않는다.

## 5단계 — 카탈로그 기반 제작

**신규 파일:** `Elements/Editor/Presentation/ElementDefinitionEditor.cs`, `Elements/Editor/Validation/ElementDefinitionValidator.cs`, `Elements/Editor/Tests/ElementAuthoringVerification.cs`.

**수정:** `LevelEditor/Editor/Presentation/`, `LevelEditor/Editor/Application/LevelCommonEditing.cs`, `Levels/Validation/LevelDefinitionValidator.cs`, `Obstacles/Rules/LevelPlacementRules.cs`.

- [ ] 선택 목록을 분류·검색·태그 필터가 있는 카탈로그 조회로 바꾼다.
- [ ] UI Toolkit의 가상화 목록으로 화면에 보이는 항목만 표시하고, 썸네일은 보이는 항목에 필요할 때 준비한다. 500개 항목의 이미지·편집 UI를 한꺼번에 만들지 않는다.
- [ ] 배치 크기·층·상태 필드를 정의에서 읽고 같은 층 교체·다중 칸 점유·Undo·복제·저장을 보존한다.
- [ ] 기존 행동을 쓰는 새 장애물 3개를 데이터만으로 추가해 배치·실행·미션을 검사한다. enum·규칙 코드 수정은 금지한다.
- [ ] 표현 누락·범위 오류·중복 ID·상충 정책·고유 상태 오류를 제작 검사에 표시한다.
- [ ] 에셋과 재생성 MemoryPack 시험에서 기존/새 ID가 동일하게 해석되는지 확인한다.

**완료:** 종류 추가 때문에 선택 목록이나 기본 배치 로직을 수정하지 않는다.

**MVVM 시범:** 목록·검색·선택·검사 상태에만 `Elements/Editor/Presentation/ElementCatalogViewModel.cs`를 적용한다. 실제 레벨·정의 편집은 기존 SerializedObject/Undo 경로를 유지한다. ViewModel은 저장 원본을 복제하지 않고 편집 명령을 응용 계층에 전달한다.

- [ ] UI 없이 검색·필터·선택·검사 상태와 명령 가능 여부를 검사한다.
- [ ] Undo/Redo 뒤 화면과 원본이 일치하고 필터 변경으로 원본이 저장되지 않는지 검사한다.
- [ ] 창 재개방·선택 복원·구독 해제·가상화 항목 재사용을 확인한다.
- [ ] 직접 UI 연결과 비교해 상태 중복·갱신 누락·유지보수 비용이 줄었는지 기록하고 확장 여부를 판단한다. MVVM 전면 도입을 완료 조건으로 강제하지 않는다.

## 6단계 — 표현·리소스·공개 관찰

**신규 파일:** `Elements/Runtime/ElementVisualResolver.cs`, `ElementResourceRequirements.cs`, `Elements/Editor/Tests/ElementPresentationVerification.cs`.

**수정:** `LevelEditor/Editor/Presentation/Board/LevelBoardArtwork.cs`, `PlayTesting/Editor/Presentation/RuntimeBoardArtwork.cs`, `GameScreen/Runtime/World/PuzzleArtworkPaths.cs`, `PuzzleArtwork.cs`, `PuzzlePowerPlayback.cs`와 풀 소유 코드; `AutoPlay/Runtime/Execution/BotObservationBuilder.cs`, `AutoPlay/Runtime/Observation/BotObservation.cs` 및 탐색에서 규칙을 복원하는 연결부.

- [ ] 세 보드 화면에서 상태·프레임을 같은 표현 정의로 선택한다.
- [ ] 공급·고유 행동의 생성 가능 관계와 효과를 필요 리소스 집합에 넣고 순환 종료를 검사한다.
- [ ] 아틀라스 분할·지연 로딩을 유지하고 불필요한 종류 주소 로딩과 공유 아틀라스 동반 로딩을 구분한다.
- [ ] 다른 정의로 풀을 재사용할 때 프레임·정렬·색·효과·콜백이 남지 않도록 초기화 경로를 연결한다.
- [ ] 관찰에는 화면에 공개된 상태·행동 특성만 복사한다. 원본 정의·내부 ID·숨은 상태는 넘기지 않는다.
- [ ] 숨은 정보 변경 쌍, 신규 종류 행동 조회, 취소·다음 레벨·씬 전환을 검사한다. 봇이 별도의 종류별 피해 규칙을 복제하지 않는지 확인한다.

**완료:** 표시와 자동 플레이가 같은 정의 의미를 사용하고 필요한 무거운 리소스만 준비한다.

**화면 연결:** 기존 uGUI HUD는 Presenter와 읽기 전용 표시 상태로 세션 의존을 줄이는 MVP 방향을 사용한다. 현재 미션 표시 진행값과 효과 재생 타이밍을 유지한다. 보드의 프레임별 이동·파워 Playback을 속성 바인딩으로 대체하거나 런타임 UI 프레임워크를 전환하지 않는다. 필요한 신규 화면 연결 파일은 해당 `GameScreen` UI/표현 경계에 둔다.

**드론 비행 변경:** 2026-10-04 요청에 따라 모든 드론을 상승 → 개별 시간 호버 → 돌진으로 통일한다. 이 항목은 기존 표현 보존과 구분한 승인된 요구사항이며 현재 문서 작성에서는 구현하지 않는다.

- [ ] `PuzzlePowerPlayback.Clips.cs`의 3기 이하/조합 제외 분기와 원형 선회 구간을 제거하는 변경 범위를 조사한다. 단독·드론+드론·로켓/폭탄/자석+드론·4기 이상 모두 같은 상태 흐름을 사용한다.
- [ ] 상승·호버를 동시에 진행하고 드론별 기본 대기를 조금씩 분산한다. 초기 제안값은 상승 0.55초·0.65칸, 호버 0.45~0.75초이며 규칙 난수는 사용하지 않는다. 프로펠러 회전은 보존한다.
- [ ] 효과 타임라인과 공통 목표 관리자에서 돌진 중 무효 목표를 감지하고 예약을 해제한다. 표현에 중단·호버·새 돌진 구간을 전달하며 렌더러에서 피해/목표 선택을 수행하지 않는다.
- [ ] 무효화된 순간의 비행 위치에서 감속·호버하고 활성 정책으로 재선정한다. 새 예약 후 현재 위치에서 돌진하며 필요한 선행 효과 대기를 호버 연장으로 흡수한다.
- [ ] 두 번 이상 목표가 사라지는 경우, 같은 목표 예약 경쟁, 미션 후보 부재 시 일반 대체, 모든 후보 부재 시 종료를 검사한다. 초기 위치로 복귀하거나 무한 호버하지 않는다.
- [ ] 원형 경로 부재·드론별 대기 편차·재돌진 위치 연속성·목표 예고 부재를 표현 검사에 추가한다. 목표/피해/미션 결과 정합성, 일시정지·취소·씬 전환·풀 재사용도 확인한다.

## 7단계 — 규모 검증과 정리

**신규 검사:** `Elements/Editor/Tests/ElementScaleVerification.cs`. 결과는 `Docs/Verification/MoonRabbitJunkyard/ElementFramework/`에 기록한다.

- [ ] 기존 행동을 재사용하는 500개 제작 정의로 검색·ID 조회·배치·왕복·실행을 검사한다.
- [ ] 일부 종류만 쓰는 레벨과 공급·생성 행동이 있는 레벨에서 로드 주소·풀 생성 수를 각각 기록한다.
- [ ] 많은 정책 등록표 중 활성 미션의 정책만 검색하는지 실행 횟수·공유 검색 수를 기록한다. 생성·미션 완료·연쇄와 예약이 바뀌는 사례도 포함한다.
- [ ] 기준 전체와 신규 사례를 실행하고 시간·할당·리소스를 측정한다. 미실행 플랫폼 검사를 분리한다.
- [ ] 사용처 검색과 동등성 검사 후 중복 규칙·이미지 분기·임시 실행 어댑터를 제거한다. 필요한 구형 원본 읽기는 남긴다.
- [ ] 새 종류와 새 행동을 실제로 추가해 필요한 데이터·코드 변경 수를 기록한다.
- [ ] 사용 안내·최종 매뉴얼·검증 기록을 정리하고 목표 체크박스를 실제 근거와 연결한다.

**완료:** 수백 종류 확장과 기존 동작 보존을 증명하고 명확한 콘텐츠 추가 절차를 제공한다.

## 튜토리얼과 복구

튜토리얼 기획은 유지한다. 최소 4~5단계의 정의·규칙·에디터가 검증된 후 새 튜토리얼 구현에 착수하고 6단계의 표현·공개 상태 계약도 확인한다. 구형 종류별 분기를 먼저 늘렸다가 다시 제거하는 작업을 하지 않는다.

원본 적용 전에는 신규 코드·정의를 분리해 되돌린다. 원본 적용 후에는 해당 단계 백업과 같은 버전 코드를 함께 복구한다. 코드·레벨 팩·카탈로그의 버전을 섞지 않는다.

첫 실제 구현은 1단계만 진행한다. 이 계획 작성 중 코드·레벨·프리팹·패키지·Addressables 설정은 변경하지 않았다.

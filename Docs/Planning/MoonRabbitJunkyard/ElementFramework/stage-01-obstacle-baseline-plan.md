# EF-01 — 고정 장애물 피해 기준 확보 계획

> 실행 담당: `superpowers:executing-plans`로 아래 작은 작업을 순서대로 수행한다. 병렬 에이전트는 별도 요청 시에만 사용한다. 구현 전 설계와 통합 가이드라인을 함께 읽는다.

**목표:** 회수캡슐·색 제한 장애물·2×2 금속기둥 상자의 현재 피해/제거/미션 결과를 재현 가능하게 확보한다.

**구조:** 기존 고정 장애물 검사와 실행 경로를 재사용한다. 데이터 정의나 런타임 규칙을 전환하지 않는다. 빠진 기준 사례가 확인될 때만 같은 검사 계열에 추가한다.

**기술:** Unity 6000.3.10f1, C#, 기존 Editor 검사. 신규 패키지·asmdef·검사 프레임워크 없음.

**설계:** [통합 가이드라인](integration-guideline.md) · [전체 설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [이 단계 목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-01-obstacle-baseline-goal.md).

상태: 완료. 2026-10-04, 244 PASS/0 FAIL, 관찰 60건. [실제 결과와 작업 판단](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-01-progress.md).

## 범위와 제약

고정 장애물의 허용 피해 원인, 색 제한, 본체별 턴당 제한, 2×2 점유 칸별 타격, 제거/미션 집계와 원본 상태 보존만 다룬다. 발전기·번식·낙하·드론·전체 레벨팩·화면 전환은 이 단계에서 변경하거나 전체 검사하지 않는다. `Safe`, `Appliance`는 현재 저장/코드의 구형 식별자이며 사용자 표시명과 별개로 숫자와 호환성을 유지한다.

빌드·원본 레벨 저장·직렬화 전환·Unity 강제 종료·임의 커밋 금지. 검사를 위해 사용자 편집 중인 씬을 임의 저장하지 않는다. 검사에 필요한 임시 데이터는 메모리에서 생성·해제한다. 관련 없는 기존 수정은 보존한다.

## 파일과 산출물

- 읽기: `Assets/Scripts/Features/Obstacles/Runtime/ObstacleDamageRules.cs`, `DamageReaction.cs`, `LayerDamageRules.cs`; `Assets/Scripts/Features/Missions/Runtime/MissionProgressRules.cs`; `Assets/Scripts/Features/PuzzlePlay/Runtime/Actions/BoardActionExecutor.cs`와 타격 문맥 호출부.
- 검사 재사용: `Assets/Scripts/Features/Obstacles/Editor/Tests/FixedObstacleVerification.cs`, `.Overlap.cs`, `.Edges.cs`, `.Regression.cs`, `.Supplemental.cs` 및 이들이 호출하는 데이터 검사.
- 필요할 때만 추가: 같은 폴더의 `FixedObstacleVerification.Baseline.cs`와 정상 `.meta`. namespace `Levels.Editor`, 기존 partial 클래스. 기존 검사에 없는 사례/기록만 추가하며 공통 검사 래퍼 전체를 만들지 않는다.
- 기록 생성: `Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-01-progress.md`. 실행 원시 결과는 `Logs/ElementFramework/Stage01/`에 보존한다. 기존 검사 출력 경로를 바꾸지 않고 실행 직후 단계 폴더에 사본을 보관한다.

**인계 계약:** 이후 단계는 현재 런타임의 관찰값을 기준으로 비교한다. 사례별 종류 숫자·초기 배치·색·내구도·시드·행동/타격 구분과 결과 내구도·점유 해제·미션·효과 기록·원본 보존 여부를 남긴다. PASS 개수만으로 기준을 대체하지 않는다. 검사와 기록은 다음 단계 구현을 요구하지 않는다.

## 검토할 위험

1. 구형 명칭을 새 명칭으로 바꾸며 종류 숫자나 저장 의미를 변경하는 경우.
2. 같은 공격의 같은 2×2 칸을 중복 피해 처리하는 경우.
3. 다른 공격/다른 점유 칸의 정당한 피해를 본체 제한으로 막는 경우.
4. 색 불일치·피해 원인 제한을 제거/미션 계산에서 누락하는 경우.
5. 검사 실행이 사용자 Editor 종료나 원본 에셋 저장을 유발하는 경우.

## 작업 1 — 안전한 검사 범위와 기준표 확보

- [x] 작업 시작 시 기존 변경·대상 원본 파일 상태를 기록한다. Unity 프로세스의 프로젝트 경로를 확인하고 ServeredMeridian 외의 에디터를 조작하지 않는다.
- [x] 기존 검사 진입점 `Levels.Editor.FixedObstacleVerification.Data`, `FinalData`, `OverlapData`, `Edges`, `Regression`, `Supplemental`을 읽어 빌드 호출·에디터 종료·파일 저장 여부와 실제 사례 범위를 확인한다. 메서드가 존재한다는 이유만으로 모두 실행하지 않는다.
- [x] 진행 기록에 사례/입력/기대 결과/기존 검사 위치 표를 작성한다. 확인된 제한과 타격 단위를 직접 적는다. 덮개·발전기 등 별도 미확보 범위는 다음 기준 단계 항목으로 분리한다.

**확인:** 안전한 실행 방식, 최소 실행할 데이터 진입점, 각 기준 사례의 소유 검사가 명확하다.

## 작업 2 — 기준 사례의 누락만 보완

- [x] 회수캡슐의 인접 일반 피해 거절과 허용 파워 피해, 색 제한 장애물의 일치/불일치 반응, 일반 본체의 턴당 피해 제한 사례가 기존 검사에 있는지 확인한다.
- [x] 2×2의 가로/세로 로켓 2칸 피해, 폭탄 부분 중첩, 확대 조합 중첩, 자석+자석 4칸 피해와 같은 공격/같은 칸 중복 제외 사례를 확인한다. 각 사례는 시작 내구도와 타격 후 값을 명시한다.
- [x] 제거 시 4칸 점유 해제·미션 단일 완료, 제거되지 않은 경우 본체 유지, 실행 전 원본 상태 보존을 확인한다.
- [x] 누락이 있는 경우에만 기존 partial 검사에 사례를 추가한다. 기준 동작과 충돌하는 기대값이나 기존 실패는 숨기지 않고 원인/범위를 기록한다. 이 단계에서는 런타임 버그 수정을 섞지 않는다.
- [x] 추가 기록이 필요하면 테스트 전후의 실제 값과 효과 목록을 저장한다. 새로운 런타임 공개 API나 규칙 분기를 만들지 않는다.

**확인:** 피해·점유·미션을 직접 비교하는 사례가 확보되고, 새 검사가 기대하는 현재 값의 근거를 추적할 수 있다.

## 작업 3 — 실행·증거 보관·다음 단계 인계

- [x] 실행 가능한 방법으로 최소 관련 Editor 데이터 검사를 수행한다. 배치 방식일 경우 해당 Unity 실행 파일과 프로젝트 경로를 확인하고 `-executeMethod Levels.Editor.FixedObstacleVerification.OverlapData` 등 작업 1에서 선정한 진입점을 사용한다. 종료 코드와 결과 파일의 FAIL/예외를 함께 검사한다.
- [x] 로그·결과·실행 버전·명령·사례 입력/결과를 단계 증거 경로와 진행 기록에 보관한다. 새 실행이 없으면 과거 PASS 기록을 이번 실행으로 쓰지 않는다.
- [x] 대상 원본 변경 여부와 최종 diff를 확인한다. 필요한 검사 보완 이외 런타임·에셋·패키지 변경이 없는지 확인한다.
- [x] 완료 조건을 실제 결과로 갱신하고 변경/검증/남은 제약을 보고한다. 수행하지 못한 필수 검사는 미완료로 남긴다.
- [x] 완료 후 드론 선택·예약·효과 타임라인 기준 확보를 대상으로 EF-02 계획·목표·복사용 명령문을 작성한다. 해당 작업을 아직 구현하지 않고 완료 보고에 세 문서 링크와 명령문 전체를 포함한다.

**완료:** [EF-01 목표 문서](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-01-obstacle-baseline-goal.md)의 필수 조건을 충족하고, 다음 실행이 가능한 인계 자료를 제공한다.

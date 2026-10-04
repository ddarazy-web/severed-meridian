# EF-03 — 거미줄·먼지 피해 기준 확보 계획

> 실행 담당: `superpowers:executing-plans`로 현재 단계만 순서대로 수행한다. 병렬 에이전트는 별도 요청 시에만 사용한다.

**목표:** 덮개 우선·내용물 보존과 일반 블록 소비/변환에 따른 바닥 피해의 현재 결과를 재현 가능한 기준으로 확보한다.

**구조:** 기존 LayerVerification의 데이터 사례를 재사용한다. 겹치는 타격·턴 제한·제거 미션과 낙하 뒤 바닥 잔류를 직접 비교한다. 공통 규칙 전환은 하지 않는다.

**기술:** 현재 Unity/C#, 기존 Editor 검사·JsonUtility. 새 패키지/asmdef 없음.

**설계:** [가이드라인](integration-guideline.md) · [전체 설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-02 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-02-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-03-layer-baseline-goal.md).

상태: 완료. 198 PASS/0 FAIL. 생산 코드 전환 미착수.

## 범위·파일·인계 계약

읽기: `Assets/Scripts/Features/Obstacles/Runtime/LayerDamageRules.cs`, `DamageReaction.cs`; `PowerBlocks/Runtime/PowerEffectResolution.cs`, `Missions/Runtime/MissionProgressRules.cs`와 실제 일반 블록 소비/변환·낙하 호출부(뒤 두 경로 기준은 `Assets/Scripts/Features/`).

기존 검사: `Assets/Scripts/Features/Obstacles/Editor/Tests/LayerVerification.cs`, `.Supplemental.cs` 및 기존 매칭/목표/조합 사례. 필요할 때만 같은 폴더에 `LayerVerification.Baseline.cs`/`.meta` 또는 기록을 보완한다. 결과는 `Logs/ElementFramework/Stage03/`와 `Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-03-progress.md`에 둔다.

인계: 시드·메모리 레벨·내용물/색/덮개/먼지 초기 상태·행동/타격 순서와 실제 남은 내구도·내용물·미션·효과 기록을 남긴다. 거미줄은 벗기는 타격에서 내용물을 보존하고, 먼지는 실제 일반 블록 소비/변환에서만 피해를 받으며 둘 모두 현재 턴 문맥 제한을 보존한다. 기대값은 손으로 확인 가능한 사례 값으로 둔다.

생산 규칙/원본/저장 포맷/기존 작업 변경, 빌드·임의 커밋·사용자 Unity 종료·씬 저장 금지. 발전기 전체·곰팡이 번식·드론/낙하 연출·새 정책·에디터/팩 전환은 제외한다.

## 검토 위험

덮개 제거와 내용물 소비를 같은 타격으로 혼동, 동일 턴 반복 피해, 파워 접촉만으로 먼지 감소, 변환과 후속 효과의 바닥 중복 피해, 낙하로 바닥까지 이동/중복 미션 완료하는 경우를 아래 사례로 검사한다.

## 작업 1 — 대응표와 안전 실행 범위

- [x] EF-01/02 결과와 현 소스를 확인하고 대상 원본/기존 변경을 기록한다.
- [x] 실제 덮개 조회/적용·먼지 소비/변환·미션/낙하 호출 순서를 읽고 기존 검사 대응표를 만든다.
- [x] LayerVerification.Data/Supplemental의 종료·저장·빌드/다른 기능 호출을 확인하고 최소 데이터 범위를 선정한다. UI/Regression은 자동 포함하지 않는다.

## 작업 2 — 누락 기준과 실제 값 기록

- [x] 거미줄 내구도 1~3의 일반/파워 내용물 보존, 제거 시 미션 1과 동일 턴 중복 제한/다음 턴 허용을 확인한다.
- [x] 먼지 내구도 1~3의 일반 블록 소비 피해와 파워/장애물 접촉만으로 무피해, 제거 미션 단일 집계를 확인한다.
- [x] 매칭/파워 조합/색 변환과 후속 타격에서 실제 피해 단위와 중복 제한을 확인한다. EF-01/02 전체 검사를 복제하지 않는다.
- [x] 낙하 뒤 거미줄/먼지의 좌표·내용물 관계와 먼지 바닥 잔류를 확인한다. 낙하 알고리즘을 바꾸지 않는다.
- [x] 기존 사례가 없는 경우에만 메모리 fixture를 추가하고 실제 입력/전후/효과를 기록한다. 생산 API를 추가하지 않는다.

## 작업 3 — 실행·보고·다음 단계

- [x] 선정한 Editor 데이터 검사를 안전하게 실행하고 실제 종료 코드/새 결과 파일의 FAIL/예외를 확인한다.
- [x] 기존 파일 해시/최종 diff를 비교하여 검사 보완 외 변경이 없는지 확인한다.
- [x] 실제 검증 기록과 목표 체크박스를 갱신하고 변경/검사/미검증/남은 문제를 보고한다.
- [x] 완료 후 EF-04의 가장 작은 선행 작업을 선정해 계획·목표·복사용 실행문을 만든다. 기본 후보는 발전기 피해/연결 기준이며 실제 결과에 따라 조정 이유를 남긴다. EF-04 구현은 시작하지 않는다.

필수 실패나 미실행이 남으면 미완료로 보고한다. 이번 단계는 기준 확보이며 새 확장 프레임워크 구현이 아니다.

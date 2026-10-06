# EF-36 — 고철 공급의 미션 수량 연결 계획

상태: 완료. 새 Run51155+기존28종584906=636061 PASS/0 FAIL, 67출력 복원·전체 원문/보호 감사 통과. 다음 EF37 문서만 준비했다.

> 실행 담당: superpowers:executing-plans. 현재 work 체크아웃을 사용하며 에이전트는 별도 요청 시에만 사용한다.

목표: 고정·유지 공급으로 등장할 고철의 미션 수량을 같은 고철 정의의 RemovalMissionProfile에 연결한다.

구조: 기존 LevelMissionRules.Supply와 MissionSupplySummary를 재사용한다. 고정·유지 공급의 미션 대상 선택이라는 한 책임을 함께 전환하며, 공급 생성·이동·제거 알고리즘은 그대로 유지한다. 새 정책/카탈로그/실행기를 만들지 않는다.

환경: ServeredMeridian, Unity6000.3.10f1, 현재 C#/asmdef·패키지·9×9 관례, MemoryPack 버전1/50레벨 묶음.

연결: [가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-35 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-35-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-36-scrap-supply-mission-goal.md).

## 한 단계의 결과와 근거

EF-29는 최초 배치 본체의 미션 수량을 정의에 연결했다. 현재 LevelMissionRules.Supply의 fixedCount와 scrap 선택은 여전히 MissionKind.Scrap에 고정돼 있다. 같은ID 고철 정의의 제거 미션을 임시로 다른 내구도형 미션에 연결하면 최초 수량/실제 제거와 공급 수량의 대상이 어긋날 수 있다. 고정 공급과 유지 공급을 별도 단계로 나누지 않고 같은 미션 수량 책임으로 검증한다.

기존 정상 정의에서는 Initial/Fixed/Maintained/Dynamic/GoalBased/Maximum 및 검증 결과·게임 결과가 모두 같아야 한다. 임시 비기본 정의의 공급 미션 대상 변경만 별도 증거로 설명한다.

## 파일과 계약

| 파일 | 책임 |
| --- | --- |
| Assets/Scripts/Features/Missions/Rules/LevelMissionRules.cs | Supply의 고철 고정/유지 수량 대상 선택을 기존 정의 프로필로 연결 |
| Assets/Scripts/Features/Elements/Editor/Tests/ScrapSupplyMissionPolicyVerification.cs(+meta) | 실제 공개 Supply/Validate·런타임 구성의 RED/GREEN 및 정상 전후 비교 |

`public static MissionSupplySummary Supply(LevelDefinition level, LevelMissionDefinition mission)`과 반환 필드/long 수량을 유지한다. LevelSupplyRules.FixedCount/HasMode, 기존 RemovalMissionProfile을 소비한다. 구현 전 public Supply→Validate→런타임 구성 호출부·ItemError의 배치 프로필 조회·누락 오류 순서를 확인하고 최소 내부 계약을 기록한다. 전체 종류별 분기나 새 공개 수정 API는 추가하지 않는다.

## 검토 초점

### 실제 호출 조사와 내부 계약

- LevelStateBuilder.Build는 v4 검사 뒤 LevelDefinitionValidator.Validate를 호출한다. 검증 문제가 있으면 상태를 만들지 않는다. 전체 검증은 배치/층/흐름/연결→LevelSupplyRules.Validate→LevelMissionRules.Validate 순서이며 마지막 단계가 공개 Supply를 소비한다.
- FixedCount는 Fixed 모드·Items 비null·같은 공급 종류·ItemError 성공 항목만 long으로 합산한다. ItemError는 enum→양수 수량→색/방향→고철 내구도와 필수 배치 프로필 순서다. 새 미션 프로필 조회로 이 순서를 앞지르지 않는다.
- 최초 수량 계산과 Color/Mold 조기 반환을 먼저 보존한다. Recovery와 비제거/미지원 미션은 고철 프로필을 불필요하게 조회하지 않는다. 고철 고정/유지 공급이 실제 양수로 기여할 경우에만 제거 미션 프로필이 필요하다. 공급 없음·null/빈 목록·유효 수량0·유지 목표/limit0 경계는 별도 검사로 확정한다.
- 임시 정의 변경은 고철의 RemovalMissionProfile 한 필드만 바꾼다. 검사에서 공개 Supply→미션 검증→전체 검증→Build 결과를 실제 호출하고, finally 원래 정의 참조를 복원한다. 내부 조회의 구체적인 구현은 RED와 경계 검사 확인 후 확정한다.
- 확정 경계: 기존 Scrap 미션의 FixedCount/ItemError 순서를 그대로 유지한다. 다른 내구도 미션은 Fixed 고철 항목이 있고 제거 프로필이 같은 미션 또는 누락된 경우에만 FixedCount를 검사한다. 알려진 다른 미션이면 배치 프로필 검사를 건너뛰어 기존에 없던 배치 누락 예외를 만들지 않는다. 유효 고정 수량 또는 0 하한 적용 후 유지 한도가 양수인 경우에만 RequireRemovalMissionProfile로 필수 조회한다. 별도 공개 API/정책은 추가하지 않는다.
- 고철 Fixed와 MaintainScrap의 동시 사용은 기존 SupplyConflict 계약이다. 유효 혼합은 고철 유지와 회수/로켓 고정 공급으로 구성하고 기존 충돌 규칙을 변경하지 않는다. JSON null 덮어쓰기는 Unity에서 실제 null이 되지 않아 새 검사 fixture에서 해당 필드만 직접 null로 설정한다.

- Color/Mold의 기존 동적 조기 반환, Recovery의 고정/목표 기반 유지 수량과 GoalBased, 미지원 미션 의미를 보존한다.
- 유효 고철 고정 항목만 세고 다른 공급 종류·잘못된 항목의 기존 필터를 유지한다. 유지 모드/목표/limit의 조건과 0 하한, long 합산을 유지한다.
- 공급이 없거나 null/빈 목록·수량0이면 기존 반환/오류 순서를 보존한다. 관련 없는 미션에서 불필요한 프로필 조회와 새 예외를 만들지 않는다.
- 실제 고철 공급의 미션 프로필이 필요한 경계에서는 누락을 ID 오류로 거절하고 기본 Scrap 대상으로 숨기지 않는다. 최소 조회 시점은 실제 호출 조사와 검사로 확정한다.
- 임시 정의는 같은ID·기존 배치/피해/행동 프로필을 유지하고 RemovalMissionProfile만 대체한다. 모든 교체는 finally 정확한 원래 참조로 복원한다.

## 작업과 검증

- [x] 현재 work/HEAD·미커밋·원본/GUID/enum·과거 주/부가/조건부 출력·무시 Addressables/패키지를 보호하고 Supply/Validate/런타임 구성의 호출·오류 순서를 조사한다. EF-35 정상 전체 기준과 기존28종을 확보한다.
- [x] 검사부터 작성한다. 유효 고정/유지/혼합 공급 입력에서 같은ID 고철의 제거 미션 프로필만 임시 대체하여 공개 Supply의 Fixed/Maintained/Maximum 불일치를 RED로 확인한다. 실제 Validate/런타임 구성 소비와 누락 ID 오류·상태/원본/규칙·전역 난수 무변경도 검사한다. 기대 수량을 강제 설정하지 않는다.
- [x] 고정·유지 공급의 대상 선택을 같은 정의로 최소 연결한다. 계산/필터/조기 반환/오류 순서와 다른 종류·공급 알고리즘을 보존해 GREEN을 확인한다.
- [x] 정상 정의 전체 상태/문맥/난수/피해·미션·효과/예약·취소·재선정/발전기·공급/검증/MemoryPack 전체 바이트·본체ID·버전1/50구간을 비교한다. Red의 의도한 실제 실패/종료1을 먼저 확인하고, Before/Run과 EF-35 verified-results.json의 최종28종은 각각 별도 Editor에서 정확한 메서드/검사 수·실제 종료0/FAIL0으로 대조한다. 모든 출력의 사전 조사→백업→삭제→새 생성→증거 복사→finally 복원을 확인한다.
- [x] 요구사항별 최종 감사·완료 보고 후 바로 다음 한 단계 계획/목표/전체 명령문만 작성하고 다음 구현은 시작하지 않는다.

신규 진입점: Elements.Editor.ScrapSupplyMissionPolicyVerification.Before/Red/Run. 실행 중인 핸들을 확인하고 관찰 만료만으로 재시작하지 않는다. PowerShell 선택 값 경로는 해시테이블 인덱스로 읽고 PS5 JSON 배열을 불필요하게 중첩하지 않는다.

## 제외와 제약

미션 실제 제거/집계·공급 생성/낙하/이동·현재 피해/충전/행동 등록·드론·파워 소비/범위/조합·덮개/바닥/번식·UI/MVVM/표현/풀/봇·저장/변환·원본 에셋은 전환하지 않는다. 새 종류/행동/패키지/asmdef·전체 실행기·이미지 재생성은 제외한다. 커밋/푸시/빌드/재패킹/팩 재생성/사용자 Unity 종료/씬 저장 금지. 현재 단계 구현 결과는 EF-35와 섞지 않는다.

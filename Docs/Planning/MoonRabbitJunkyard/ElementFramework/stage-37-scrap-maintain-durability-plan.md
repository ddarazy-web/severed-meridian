# EF-37 — 고철 유지 공급의 내구도 검증 연결 계획

상태: 완료. 새 Run+기존29종687011 PASS/0 FAIL,71출력 복원·과거6448 변경0. 다음 EF38은 준비만 완료했다.

목표: LevelSupplyRules.Validate에 남은 고철 유지 설정의 최대 내구도 상수5를 기존 고철 배치 정의에 연결한다. 공급 생성/내구도 적용/낙하는 변경하지 않는다.

연결: [가이드](integration-guideline.md) · [설계](../../../Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md) · [EF-36 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-36-progress.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-37-scrap-maintain-durability-goal.md).

## 선정 근거와 범위

EF-36은 공급 미션 수량 선택을 연결했다. 고정 공급 항목의 ItemError는 이미 LevelPlacementRules.MaxDurability(Scrap)를 소비하지만 유지 설정의 ScrapDurability는 여전히 >5로 검사한다. 같은 고철 정의의 최대 내구도를 변경하면 두 제작 검증이 어긋날 수 있다. 이 상한 검증 한 책임만 다음 단계로 전환한다.

| 파일 | 변경 책임 |
| --- | --- |
| Assets/Scripts/Features/BlockSupply/Rules/LevelSupplyRules.cs | Validate의 고철 유지 내구도 상한 선택만 연결 |
| Assets/Scripts/Features/Elements/Editor/Tests/ScrapMaintainDurabilityVerification.cs(+meta) | 실제 공개 공급/전체 검증·Build의 RED/GREEN과 경계/전체 비교 |

기존 ElementPlacementProfile과 LevelPlacementRules.MaxDurability를 재사용한다. 새 정책/행동/키/공개 API/전체 실행기는 추가하지 않는다. 실제 공급 생성, 고정 ItemError의 필터/문구, 미션 수량, 다른 공급과 충돌 규칙은 변경하지 않는다.

## 내부 계약을 확정할 조사

LevelSupplyRules.Validate→전체 검증→LevelStateBuilder.Build 호출과 item/중복/충돌/고철 설정/회수 설정의 오류 순서를 조사한다. 기존 조건의 단락 평가와 미사용 기본 설정, null/빈 목록, 값0/음수 및 다른 공급만 있는 설정에서 불필요한 배치 조회를 추가하지 않는다. 실제 상한 판정이 필요한 설정의 프로필 누락은 ID 오류로 설명한다. 최소 조회 시점은 실제 코드와 검사로 확정하고 계획서에 기록한다. 기본값을 숨긴 대체 상한이나 정상 결과 정규화는 사용하지 않는다.

## 작은 실행 단위와 검증

- [x] 현재 work/HEAD·미커밋·원본/GUID/enum·과거 출력·무시 Addressables/패키지의 보호 기준과 실제 호출/오류 순서를 확보한다. EF-36 전체 정상 원문과 정확한 최종29종을 기준으로 삼는다.
- [x] 검사부터 작성한다. 같은ID 고철의 Size를 유지하고 Placement.MaxDurability만 2/7 등으로 임시 대체한다. 실제 유지 설정 내구도3/6의 허용/거절 불일치를 공개 Validate에서 RED로 확인하고 전체 검증/Build 소비도 확인한다. 유효 기반 입력을 확보하고 기대 결과나 상태 수량을 강제하지 않는다. finally 정확한 원래 정의를 복원한다.
- [x] 최대 내구도 상한 선택만 최소 연결한다. 원래 오류 순서·코드/경로/문구·목표/한도/0 하한/모드/충돌·회수/기타 공급과 미사용/null 경계를 보존하고 GREEN을 확인한다.
- [x] 새 Before/Red/Run과 기존 정확한29종을 각각 별도 Editor에서 검사한다. 정상 전체 상태/비공개 문맥/난수/피해·미션·효과/예약·취소·재선정/발전기·공급/전체 검증/188 MemoryPack 전체 바이트·225팩 상태·본체ID·버전1/50구간을 비교한다. 모든 주/부가/조건부 출력은 사전 조사→백업→삭제→새 생성 확인→증거 보존→finally 원문 복원한다.
- [x] 원본/기존 작업/과거 증거/GUID/diff check와 요구사항별 최종 감사·보고를 완료하고 다음 한 단계 계획/목표/전체 명령문만 작성한다. 다음 구현은 시작하지 않는다.

## 실행과 제외

신규 진입점은 Elements.Editor.ScrapMaintainDurabilityVerification.Before/Red/Run이다. Red의 실제 실패/종료1과 Before/Run·기존29종의 정확한 검사 수/실제 종료0/필수FAIL0을 확인한다. PowerShell 선택 경로는 ['values']로 읽고 PS5 배열을 불필요하게 중첩하지 않는다. 실행 중인 핸들을 보존하며 관찰 만료로 재시작하지 않는다. 같은 해시의 증거를 공유할 경우 실제 새 생성/원문 복원과 원본 파일의 독립성을 검증한다.

커밋/푸시/빌드/재패킹/팩·이미지 재생성/사용자 Unity 종료/씬 저장/에이전트 금지. 새로운 종류·행동·패키지·asmdef, 피해/충전/제거·미션 집계·실제 공급/낙하/이동·드론·파워·덮개/바닥/번식·UI/MVVM/표현/풀/봇·저장/변환·원본 에셋 전환은 제외한다.

## 조사한 내부 계약 (구현 전)

전체 검증은 배치/층/흐름/연결 뒤 공급 검증, 그 뒤 미션 검증을 수행한다. Build는 전체 검증의 오류가 있으면 상태를 만들지 않는다. 공급 검증 내부 순서는 원래대로 생성구의 위치/중복 → 모드/소진 정책 → 고정 항목 → 비고정 목록 충돌 → 고정·유지 충돌 → 고철 설정 → 회수 설정 → 회수 부품이다. 고정 ItemError가 먼저 배치 누락을 발견하는 순서도 보존한다.

고철 설정의 음수 목표/한도와 내구도 하한 위반은 기존 OR의 앞 조건에서 단락 평가한다. ElementPlacementProfile 생성자는 MaxDurability>=1을 보장하므로 기본 내구도1은 상한 조회 없이 판단할 수 있다. 내구도>1인 실제 상한 판단에는 기존 MaxDurability 조회가 필요하다. 미사용 설정이라도 명시적 내구도>1의 기존 상한 검증을 없애지 않는다. 이 최소 조회 시점은 새 공개 Validate 경계 검사로 검증하며, 아직 GREEN 완료를 주장하지 않는다.



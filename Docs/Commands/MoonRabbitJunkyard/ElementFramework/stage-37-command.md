# EF-37 목표 실행 명령문

아래 전체를 복사해 실행한다. 현재 구현은 시작하지 않았다.

```text
ServeredMeridian의 EF-37 고철 유지 공급의 내구도 검증 연결을 목표로 설정하고 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-36-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-37-scrap-maintain-durability-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-37-scrap-maintain-durability-goal.md

이번 단계만 수행하고 현재 work/HEAD와 모든 미커밋 변경을 유지해. LevelSupplyRules.Validate의 고철 유지 설정 ScrapDurability 최대 상한5를 기존 고철 배치 정의와 LevelPlacementRules.MaxDurability 조회에 연결해. 상한 검증이라는 한 책임만 처리하며 실제 공급 생성/이동/낙하/내구도 적용·미션 수량과 다른 요소는 변경하지 마. 기존 공개 서명·오류 코드/경로/문구·목표/한도/모드/충돌·고정 ItemError 필터/다른 공급을 유지해. 실제 공급 검증→전체 검증→LevelStateBuilder.Build 호출과 item/중복/충돌/고철/회수 설정의 오류 순서·단락 평가를 조사하고 최소 내부 계약을 계획서에 기록해. 기존 조회·프로필을 재사용하며 새 정책/키/행동/공개 API/생산 리플렉션/전체 실행기를 만들지 마.

검사부터 작성해. 같은ID 고철의 Size를 유지하고 Placement.MaxDurability만 2/7 등으로 임시 대체하여 유지 설정 내구도3/6의 허용/거절 불일치를 실제 공개 LevelSupplyRules.Validate에서 RED로 확보해. 전체 LevelDefinitionValidator.Validate와 런타임 Build의 소비도 확인해. 유효 기반 입력을 먼저 확보하고 기대 검증 결과나 상태 수량을 강제로 설정하지 마. ID/이름/Size·기존 피해/집계/미션/행동 프로필은 유지하며 모든 임시 교체는 finally 정확한 원래 정의 참조로 복원해. 실제 상한 판정에 필요한 배치 프로필 누락은 ID 오류로 거절하고 상태/원본/규칙·전역 난수 무변경을 확인해.

공급 없음·null/빈 목록·미사용 기본 설정·0/음수·다른 공급만 있는 설정에 불필요한 조회나 새 예외를 만들지 마. 기존 단락 평가와 오류 순서, 고정 항목의 ItemError와 필터, 고철 목표/추가 한도/유지 모드/고정·유지 충돌, 회수 설정과 기타 공급 검증을 유지해. 최소 조회 시점은 실제 호출과 검사 근거로 확정해. 기본값을 숨긴 대체 상한을 만들지 마. 정상6종의 ID/키/이름/정책 bool/프로필 값·참조/부분 생성자와 기존 오류 계약을 유지해. 실패한 새 fixture만 최소 조정하고 실패 근거를 보존해.

원본/GUID/enum/EF-05/09/11·기존 작업·과거 주/부가/조건부 출력·무시 Addressables/패키지 서명을 보호해. 동일 입력/시드 정상 정의의 전체 상태/비공개 문맥/규칙·전역 난수/피해·미션·효과/예약·완료 예상·취소·무효화·재선정/발전기 충전·연결/공급/전체 검증/MemoryPack 전체 바이트·본체ID·버전1/50레벨 구간을 비교해. 임시 비기본 정의의 의도적 차이는 별도 연결 증거로 설명하고 정상 결과를 정규화해 숨기지 마.

새 Elements.Editor.ScrapMaintainDurabilityVerification.Before/Red/Run을 만들고 Red의 의도한 실제 실패/종료1을 먼저 확인해. Before/Run과 EF-36 verified-results.json의 최종29종을 정확한 메서드/검사 수로 대조하고 각각 별도 Editor 실제 종료0/필수FAIL0을 확인해. 모든 출력 작성 경로를 먼저 조사하고 백업→삭제→새 생성 확인→증거 보존→finally 전체 바이트 복원을 수행해. 과거 증거 전체 해시/원본/기존 작업/GUID/diff check를 감사해. 같은 해시의 증거를 공유하면 실제 새 생성과 원본 복원·파일 독립성을 확인해. 선택 값 경로는 해시테이블 ['values']로 읽고 PowerShell5 JSON 배열을 불필요하게 중첩하지 마. 실행 중인 핸들을 확인하고 관찰 시간 초과만으로 재시작하지 마.

피해/충전/제거·미션 집계·실제 공급 생성/낙하/이동·드론·파워·덮개/바닥/번식·UI/MVVM/표현/풀/봇·저장/변환·원본 에셋은 전환하지 마. 새 종류/행동/패키지/asmdef, 커밋/푸시/빌드/재패킹/팩·이미지 재생성/사용자 Unity 종료/씬 저장 금지. 에이전트는 별도 요청 시에만 사용해.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-37-progress.md에 요구사항별 변경·실측·의도적 차이·미검증·남은 문제를 기록해. 전체 완료 후 보고하고 바로 다음 한 단계 계획/목표/전체 명령문만 저장해. 완료 보고에 전체 명령문을 복사 가능한 코드 블록으로 제시하고 다음 단계 구현은 시작하지 마.
```

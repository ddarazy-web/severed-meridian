# EF-36 실행 명령문

아래 전체를 복사해 다음 목표 실행에 사용한다. EF-36 구현은 미착수다.

```text
ServeredMeridian의 EF-36 고철 공급의 미션 수량 연결을 목표로 설정하고 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-35-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-36-scrap-supply-mission-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-36-scrap-supply-mission-goal.md

이번 단계만 수행하고 현재 work/HEAD와 모든 미커밋 변경을 유지해. LevelMissionRules.Supply의 고철 고정·유지 공급 미션 대상 선택을 기존 고철 정의의 RemovalMissionProfile에 연결해. 같은 책임인 두 경로를 함께 처리하되 공급 생성·이동·제거 알고리즘과 다른 요소는 전환하지 마. 공개 Supply 서명과 MissionSupplySummary의 Initial/Fixed/Maintained/Dynamic/GoalBased/Maximum·long 수량을 유지해. 실제 Supply→Validate→런타임 구성 호출부, FixedCount/ItemError의 배치 프로필 조회와 누락 오류 순서를 조사하고 최소 내부 계약을 계획서에 기록해. 기존 조회·프로필을 재사용하고 새 정책/키/행동/공개 수정 API/생산 리플렉션/전체 실행기를 만들지 마.

검사부터 작성해. 유효 고정·유지·혼합 공급 입력에서 같은ID 고철의 RemovalMissionProfile만 다른 내구도형 미션으로 임시 대체해 실제 공개 Supply의 Fixed/Maintained/Maximum 불일치 RED를 확보해. 실제 Validate와 런타임 구성의 소비도 확인하고 수량을 강제로 설정하지 마. 기존 배치/피해/행동 프로필은 유지하고 모든 임시 교체는 finally 정확한 원래 정의 참조로 복원해. 실제 필요한 프로필 누락은 ID 오류로 거절하고 상태/원본/규칙·전역 난수 무변경을 확인해. 공급 없음·null/빈 목록·수량0·관련 없는 미션의 기존 반환/오류 순서를 보존하며 불필요한 프로필 조회로 새 예외를 만들지 마.

고정 유효 항목 필터와 다른 공급 종류, 유지 모드/목표/limit의 조건·0 하한, Color/Mold 동적 조기 반환, Recovery 고정·목표 기반 유지와 GoalBased, 미지원 미션 의미를 유지해. 정상6종 정의의 ID/키/이름/정책 bool/프로필 값·참조/부분 생성자와 기존 오류 순서를 바꾸지 마. 실패한 새 fixture만 최소 조정하고 실패 근거를 보존해.

원본/GUID/enum/EF-05/09/11·기존 작업·과거 주/부가/조건부 출력·무시 Addressables 상태/패키지 서명을 보호해. 동일 입력/시드 정상 정의의 전체 상태/비공개 문맥/규칙·전역 난수/피해·미션·효과/예약·완료 예상·취소·무효화·재선정/발전기 충전·연결/공급/전체 검증/MemoryPack 전체 바이트·본체ID·버전1/50레벨 구간을 비교해. 임시 비기본 정의의 의도적 차이는 별도 연결 증거로 설명하고 정상 실행 결과를 정규화해 숨기지 마.

새 Elements.Editor.ScrapSupplyMissionPolicyVerification.Before/Red/Run을 만들고 Red에서 의도한 실제 실패와 종료1을 확인해. Before/Run과 EF-35 verified-results.json의 최종28종은 정확한 메서드/검사 수로 대조하고 각각 별도 Editor 실제 종료0/필수FAIL0을 확인해. 모든 출력 경로를 먼저 조사해 백업→삭제→새 생성 확인→증거 복사→finally 전체 바이트 복원을 수행하고 과거 증거 전체 해시/원본/기존 작업/GUID와 diff check를 감사해. 선택 값 경로는 해시테이블 인덱스로 읽고 PowerShell5 JSON 배열을 불필요하게 중첩하지 마. 실행 중인 핸들을 확인하고 관찰 시간 초과만으로 재시작하지 마.

피해/충전/제거·실제 미션 집계·공급 생성/낙하/이동·드론·파워·덮개/바닥/번식·UI/MVVM/표현/풀/봇·저장/변환·원본 에셋은 전환하지 마. 새 종류/행동/패키지/asmdef를 만들지 마. 커밋/푸시/빌드/재패킹/팩·이미지 재생성/사용자 Unity 종료/씬 저장 금지. 에이전트는 별도 요청 시에만 사용해.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-36-progress.md에 요구사항별 변경·실측·의도적 차이·미검증·남은 문제를 기록해. 전체 완료 후 보고하고 바로 다음 한 단계 계획/목표/전체 명령문만 저장해. 완료 보고에 전체 명령문을 복사 가능한 코드 블록으로 제시하고 다음 단계 구현은 시작하지 마.
```

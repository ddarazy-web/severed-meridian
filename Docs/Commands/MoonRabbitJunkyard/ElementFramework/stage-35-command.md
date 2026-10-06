# EF-35 실행 명령문

아래 전체를 복사해 다음 목표 실행에 사용한다. EF-35 구현은 미착수다.

```text
ServeredMeridian의 EF-35 내구도 적용의 집계 정책 전달을 목표로 설정하고 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-34-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-35-durability-apply-policy-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-35-durability-apply-policy-goal.md

이번 단계만 수행하고 현재 work/HEAD와 모든 미커밋 변경을 유지해. EF-34의 두 행동 키와 Query/Apply 짝을 재사용해 등록 내구도 조회와 실제 적용이 같은 정의의 필수 DamageAggregationPolicy를 사용하도록 연결해. 기존 Apply의 Crate~Appliance 종류 범위 판정이 등록 행동의 정책을 우회하지 않도록 하되, 기존 직접 Apply 서명/반환값과 구형 종류·부분 정의 의미는 호환 진입점에 보존해. 실제 Query/ApplyDurability/Apply/공통 효과/public Swap 및 직접 호출부와 반환값/문맥 소비·오류 순서를 조사하고 최소 내부 서명을 계획서에 기록해. 공통 상태 변경 본문은 하나만 두고 콘텐츠별 복제나 새 키/행동/공개 수정 API/생산 리플렉션을 만들지 마.

검사부터 작성해. 유효 런타임과 실행기를 먼저 구성한 후 같은ID의 발전기를 호환 크기의 내구도 배치와 PerHitCell 정책을 가진 정의로 임시 교체해 실제 public Swap→로켓 발동/소비→Evaluate→등록 조회/적용에서 hit/좌표 기록과 본체 기록 불일치 RED를 확보해. 원래 내구도1로도 기록 방식을 검증할 수 있으므로 불필요하게 내구도를 강제 설정하지 마. 호출 횟수나 강제 Apply만으로 성공을 만들지 말고 실제 내구도/제거/미션/효과와 기록을 확인해. 모든 임시 교체는 finally 정확한 원래 정의/등록 참조로 복원해. 필수 집계 정책 누락은 정의 ID 오류로 거절하고 상태/비공개 문맥/원본/규칙·전역 난수 무변경을 확인해.

등록 내구도 적용은 전달받은 정의의 집계 정책을 공통 적용 본문에 전달하고, 조회도 같은 정의의 정책을 소비하도록 해. 기존 직접 Apply는 필요한 구형 판정을 호환 경계에서 수행해 같은 본문을 재사용해. hit/턴 기록→내구도 감소→제거·미션·연결 수명과 공통 효과의 무효화/기록 순서를 보존해. 기존6종 정상 정의의 키/ID/이름/정책 bool/프로필 값·참조/부분 생성자와 기존 누락 프로필·오류 순서·직접 Apply 기대값을 바꾸지 마. 실제 실패한 새 fixture만 최소 조정하고 실패 근거를 보존해.

원본/GUID/enum/EF-05/09/11·기존 작업·과거 주/부가 출력·무시 Addressables 상태/패키지 서명을 보호해. 동일 입력/시드의 정상 정의 전체 실행 상태/비공개 문맥/규칙·전역 난수/전체 내구도·2×2/hit/턴·색5개/null/네 원인과 -1/4/99·벽/덮개/보호/삭제/비활성/피해·미션·효과/예약·완료 예상·취소·무효화·재선정/발전기 충전 임계·연결 철거·마지막 연결 제거/기존 자석 예외/공급/전체 검증/MemoryPack 전체 바이트·본체ID·버전1/50레벨 구간을 비교해. 임시 비기본 정의의 의도적 기록 차이는 별도 연결 증거로 설명하고 정상 실행 결과를 정규화해 숨기지 마.

새 Elements.Editor.ElementDurabilityApplyPolicyVerification.Before/Red/Run과 EF-34 verified-results.json의 최종27종을 정확한 메서드/검사 수로 대조하고 각각 별도 Editor 실제 종료0/필수FAIL0을 확인해. 모든 주/부가/조건부 출력 작성 경로를 먼저 조사해 백업→삭제→새 생성 확인→증거 복사→finally 전체 바이트 복원하고 과거 증거 전체 해시/원본/기존 작업/GUID와 diff check를 감사해. 선택 값 경로는 해시테이블 인덱스로 읽고, PowerShell5 JSON 배열을 불필요하게 중첩하지 마. 실행 중인 검사 핸들을 확인하며 관찰 시간 초과만으로 재시작하지 마.

미션 매핑/Remove·예약 피해량·허용 원인/색·충전/생성 알고리즘·드론/공급/낙하·파워 소비/범위/조합·덮개/바닥/번식·UI/MVVM/표현/풀/봇·저장/변환·원본 에셋은 전환하지 마. 새 종류/행동/패키지/asmdef·전체 실행기를 만들지 마. 커밋/푸시/빌드/재패킹/팩·이미지 재생성/사용자 Unity 종료/씬 저장을 하지 마. 에이전트는 별도 요청 시에만 사용해.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-35-progress.md에 요구사항별 변경·실측·의도적 차이·미검증·남은 문제를 기록해. 전체 완료 후 보고하고 바로 다음 한 단계 계획/목표/전체 명령문만 저장해. 완료 보고에 전체 명령문을 복사 가능한 코드 블록으로 제시하고 다음 단계 구현은 시작하지 마.
```

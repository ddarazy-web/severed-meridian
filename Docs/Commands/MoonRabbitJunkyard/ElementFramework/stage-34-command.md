# EF-34 실행 명령문

아래 전체를 복사해 다음 목표 실행에 사용한다. EF-34 구현은 미착수다.

```text
ServeredMeridian의 EF-34 반응 행동 등록과 실제 적용 위임 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-33-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-34-reaction-apply-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-34-reaction-apply-goal.md

이번 단계만 수행하고 work/HEAD와 모든 미커밋 변경을 유지해. EF-33의 두 행동 키와 명시적 등록표를 재사용해 조회/적용 짝과 실제 공통 효과 실행의 적용 위임을 하나의 책임으로 연결해. 적용 필드만 추가하고 실제 사용을 미루거나 여섯 종류별로 같은 작업을 나누지 마. 실제 ElementBehaviorRegistry/ObstacleDamageRules.Query·Apply/GeneratorRules.Query·Apply/PowerEffectResolution.ApplyCore와 public Swap 및 직접 Apply 호출부를 조사해. 실제 반환값·효과 기록 소비를 확인해 내부 서명을 확정하고 계획서에 기록해. 원본/GUID/enum/EF-05/09/11·기존 작업/과거 출력·부가 정의 스냅샷·무시된 Addressables 상태/패키지 서명을 보호하고 동일 입력/시드의 전체 전환 전 기준을 확보해.

검사부터 작성해. 두 행동 등록의 조회/적용 짝, 키로 필요한 적용 하나만 선택하는 실제 경로, 기존6종의 키/ID/표시명/정책 bool·모든 프로필 값/참조와 기존 부분 생성자 호환을 확인해. 키 누락/미지원·잘못된 프로필 조합을 기본 행동으로 추론하지 말고 정의 ID 포함 오류로 거절해. 기존 조회의 Supports/원인 정책 거부와 프로필 누락 오류 순서를 보존하고 실제 실패 근거→GREEN, 조회 상태/비공개 문맥/원본/규칙·전역 난수 무변경을 기록해.

ElementBehaviorRegistry의 명시적 등록 하나가 Query와 Apply 위임을 함께 제공하도록 해. 키로 한 항목만 선택하고 전체 등록표 순회·콘텐츠별 행동 클래스·리플렉션/문자열 실행·공개 등록 수정 API를 만들지 마. 공통 효과 실행에서 내구도/충전 응답을 처리할 때 등록된 적용 하나를 정확히1회 호출해. 응답에 따른 공통 효과 기록은 유지하되 구체 적용 선택을 종류/응답 분기로 우회하지 마. 기존 ObstacleDamageRules.Apply와 GeneratorRules.Apply의 알고리즘을 재사용하고 피해·충전·hit/턴 기록·제거·미션·연결 수명·목표 무효화·효과 기록의 순서를 바꾸지 마. 덮개/일반 블록/파워 발동을 장애물 적용으로 보내지 마.

실제 public Swap→파워 발동/소비→Evaluate→등록 조회→등록 적용에서 내구도/충전/미션/효과를 확인해. 강제 Apply나 호출 횟수만으로 성공을 만들지 마. 전체 내구도/2×2 본체·점유칸/hit/턴/벽·덮개·보호/삭제·비활성/색5개·null/네 피해 원인·-1/4/99, 발전기 충전 임계/연결 철거/마지막 연결 제거와 기존 자석 예외를 검증해. 같은ID 임시 교체는 런타임 구성 후 수행하고 finally 정확한 원래 등록 참조를 복원해. 기존 검사의 의도적 누락 프로필과 생성자/응답/오류 기대값을 유지하며 필요한 키 전달만 최소 보완해. 실제 실패한 새 fixture만 최소 조정하고 실패를 숨기지 마.

동일 입력/시드의 전체 실행 상태/비공개 문맥/규칙·전역 난수/피해·미션·효과/예약·완료 예상·취소·무효화·재선정/발전기·공급 요약/전체 검증/MemoryPack 전체 바이트·본체ID·버전1·50레벨 구간을 비교해. 새 Elements.Editor.ElementReactionApplyVerification.Before/Red/Run을 준비하고 새 Run과 EF-33 최종26종을 각각 별도 Editor 정확한 메서드·실제 입력/응답·종료0/필수FAIL0으로 확인해. EF-33 verified-results.json과 정확한 메서드 및 검사 수를 대조해. 주 결과/값뿐 아니라 정의 스냅샷 등 실제 검사에서 기록하는 모든 부가 출력을 먼저 조사해 백업→삭제→새 생성 확인→증거 복사→finally 전체 바이트 복원하고 과거 증거 전체 해시/원본/기존 작업/GUID와 diff check를 감사해. 정의 메타데이터 차이는 별도로 기록하고 실행 결과를 정규화해 숨기지 마.

기본 정책·밸런스/새 행동·종류/Remove·미션 진행·예약 피해량/생성·충전 알고리즘/드론 정책·비행/공급·낙하/파워 색·소비·범위·조합/덮개·바닥·번식/UI·MVVM/표현·풀·봇/저장·변환/원본 에셋은 전환하지 마. 새 패키지/asmdef·전체 실행기·UI 프레임워크를 만들지 마. 커밋/푸시/빌드/재패킹/팩·이미지 재생성/사용자 Unity 종료/씬 저장을 하지 마. 에이전트는 별도 요청 시에만 사용해.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-34-progress.md에 변경·실측·의도적 차이·미검증·남은 문제와 요구사항별 근거를 기록해. 전체 완료 후 보고하고 다음 한 단계 계획/목표/명령문만 작성해. 전체 명령문을 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

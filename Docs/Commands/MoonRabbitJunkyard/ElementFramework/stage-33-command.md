# EF-33 — 복사용 목표 실행 명령문

상태: 준비. 아래 전체 명령문을 복사해 실행한다. EF-33 구현은 시작하지 않았다.

```text
ServeredMeridian의 EF-33 반응 행동 키·조회 등록표 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-32-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-33-reaction-behavior-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-33-reaction-behavior-goal.md

이번 단계만 수행하고 work와 모든 미커밋 변경을 유지해. 기존6종의 불변 반응 행동 키와 명시적 등록표 및 실제 Query 선택을 하나의 책임으로 연결해. 키만 추가하고 실제 사용을 다음으로 미루거나 종류별로 같은 작업을 나누지 마. 실제 ElementDefinition 생성/임시 복사·LegacyElementDefinitions·ObstacleDamageRules.Query·GeneratorRules.Query와 public Swap→공통 효과 실행 호출부를 조사해. 원본/GUID/enum/기존 작업/과거 출력·무시된 Addressables 상태/패키지 서명을 보호하고 같은 입력/시드의 전체 전환 전 기준을 확보해.

검사부터 작성해. 기존 내구도형5종과 발전기1종에 내구도 피해/발전기 충전 행동 키를 명시하고 ID/표시명/기본 정책 bool·모든 기존 프로필 값과 참조를 보존해. 기존 메타데이터 전용/부분 생성자는 유지하며 누락 키를 내구도 행동으로 추론하지 마. 실제 사용할 때 누락/미지원 키를 정의 ID 포함 오류로 거절해. 기존 프로필 누락 오류도 다른 오류로 덮지 않도록 거부 순서를 검증해. 실제 실패 근거와 GREEN, 조회 상태/비공개 문맥/원본/난수 무변경을 기록해.

명시적 ElementBehaviorRegistry가 키로 필요한 조회 하나만 선택하도록 해. 전체 등록표 순회·콘텐츠 ID별 행동 클래스·리플렉션/문자열 함수 실행·공개 등록 수정 API를 만들지 마. 기존 Supports 및 원인 정책 거부 뒤의 행동 선택만 연결해. GeneratorRules.Query와 기존 내구도 조회를 재사용하고 새 키를 무시해 ObstacleKind로 대신 선택하지 않는 실제 사례를 확인해. 구형 종류→정의 ID 어댑터는 유지해. Safe 미정의 원인 예외·ColorLock 색 조건과 순서·내구도 집계·제거된 본체·전체 응답/Amount/메시지·EF-30~32 자석 전단 및 발전기 예외는 보존해.

실제 public Swap→파워 발동/소비→Evaluate→행동 조회→기존 공통 적용에서 내구도/충전/미션/효과까지 확인해. 강제Apply나 등록표 호출 횟수만으로 성공을 만들지 마. 전체 내구도/본체·점유칸/hit/턴/벽·덮개·보호/삭제·비활성/색5개·null/네 피해 원인·-1/4/99/발전기 연결·철거를 검증해. 의미가 맞지 않는 키/프로필 조합은 기본 행동으로 몰래 대체하지 마. 새 필드에 따른 정의 메타데이터 차이와 누락 오류 노출 차이는 별도 기록하고 실행 상태 변화로 숨기지 마.

기존 검사의 임시 정의 교체가 새 키를 잃는 호출부는 키 전달만 최소 보완해. 의도적으로 누락한 기존 프로필은 그대로 누락하고 생성자/반응/누락 기대값을 낮추지 마. 같은 ID 임시 교체는 런타임 구성 후 수행하고 finally 정확한 원래 등록 참조를 복원해. 기존 테스트를 삭제하거나 새 기본값으로 실패를 감추지 마.

기본 등록의 전체 실행 상태/비공개 문맥/규칙·전역 난수/피해·미션·효과/예약·완료 예상·취소·무효화·재선정/발전기·공급 요약/전체 검증/MemoryPack 전체 바이트·본체ID·버전1·50레벨 구간을 동일 입력으로 비교해. 새 검사와 EF-32 최종25종을 각각 별도 Editor 정확한 메서드·실제 입력/응답·종료0/필수FAIL0으로 확인해. 기존 결과/값은 백업→삭제→새 생성 확인→증거 복사→finally 전체 바이트 복원하고 원본/기존 작업/과거 증거 감사와 diff check를 완료해. 실제 실패한 fixture만 최소 조정해.

Apply/Remove/미션 진행·예약 피해량/생성·충전 적용, 기본 정책·밸런스/드론 정책·비행/공급·낙하/파워 색 선택·소비·범위·조합/덮개·바닥·번식/UI·MVVM/표현·풀·봇/저장·변환/원본 에셋은 전환하지 마. 새 패키지/asmdef·전면 실행기나 UI 프레임워크를 만들지 마. EF-05/09/11과 모든 기존 작업을 보존하고 커밋/푸시/빌드/재패킹/이미지·팩 재생성/사용자 Unity 종료/씬 저장을 하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-33-progress.md에 변경·실측·의도적 차이·미검증·남은 문제를 기록해. 전체 완료 후 보고하고 다음 한 단계 계획/목표/명령문만 작성해. 전체 명령문을 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

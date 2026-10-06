# EF-38 실행 명령문

> 2026-10-05: 이 ID 전용 준비안은 큰 구간 1단계 계획으로 대체되었다. 이 문서의 실행문은 사용하지 않고 [새 통합 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/phase-01-common-element-rules-plan.md)을 따른다. 과거 준비 내용은 참고용으로 보존한다.

아래 전체를 복사해 실행한다. 현재 문서는 준비만 완료했으며 EF38 구현은 시작하지 않았다.

```text
ServeredMeridian의 EF-38 거미줄 ID·카탈로그 기본 등록을 목표로 설정하고 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-37-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-38-web-catalog-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-38-web-catalog-goal.md

이번 단계만 수행하고 현재 work/HEAD와 모든 미커밋 변경을 유지해. 기존 CoverKind.Web를 영구 ID cover.web과 기존 불변 ElementCatalog에 등록해. LegacyElementMap.Get(CoverKind kind)와 LegacyElementDefinitions.Get(CoverKind kind)를 추가하여 Web의 같은 불변 정의 참조를 반환해. 표시명은 거미줄, 기존 ElementPlacementProfile의 Size1/MaxDurability3만 등록하고 나머지 피해/색/집계/충전/제거 미션 프로필과 ReactionBehavior는 null로 유지해. 새 정책/키/행동/종류/카탈로그 수정 API/생산 리플렉션/범용 실행기는 만들지 마. 새 조회는 Web만 지원하고 Mold·미정의 CoverKind 값은 입력을 포함한 ArgumentOutOfRangeException으로 거절해. 기존 Get(ObstacleKind)의 서명·매핑·오류, 기존6종의 ID/enum 숫자/키/이름/정책 bool/프로필 값·참조/부분 생성자와 ElementCatalog Count/Get API를 유지해.

검사부터 작성해. 아직 없는 오버로드는 새 Editor 검사에서 정확한 인자 타입의 MethodInfo로 조회하여 컴파일 실패가 아닌 실제 공개 매핑/조회 부재 RED·종료1을 먼저 확인해. cover.web/표시명/1·3/같은 참조 반환, Web 외 값의 거절, 기존6종과 원본/상태/규칙·전역 난수 무변경을 확인해. 기존 검사의 Get 오버로드 선택과 전역 카탈로그/기존6종을 복사한 별도 검사 카탈로그 소유권을 조사하고 정상 Count6 검사를 임의 변경하지 마. 임시 교체가 필요하면 ID/기존 프로필을 보존하고 finally 정확한 원래 참조로 복원해. 실패한 새 fixture만 최소 보정하며 기대 결과나 수량을 강제하지 말고 실패 근거를 보존해.

Elements.Editor.WebCatalogVerification.Before/Red/Run을 만들고 정상 Before와 실제 RED, GREEN을 확인해. EF37 verified-results.json의 최종30종을 정확한 메서드/검사 수로 대조하고 각각 별도 Editor 실제 종료0/필수FAIL0을 확인해. 정상 정의의 전체18182행/188 MemoryPack 전체 바이트·225팩 상태/본체ID/버전1/50레벨 구간, 전체 상태/비공개 문맥/규칙·전역 난수/피해·미션·효과/예약·완료 예상·취소·무효화·재선정/발전기 충전·연결/공급/전체 검증을 같은 입력/시드로 비교해. 등록 메타데이터의 의도적 추가는 별도 연결 증거로 설명하고 정상 실행을 정규화해 숨기지 마.

모든 출력 작성 경로를 먼저 조사하고 과거 주/부가/조건부 출력을 백업→삭제→새 생성 시간/해시 확인→증거 보존→finally 전체 바이트 복원해. 같은 해시의 불변 증거를 공유하면 실제 새 생성과 원본 복원·파일 독립성을 확인해. 원본/GUID/enum/EF05·09·11·기존 작업·과거 증거 전체 해시·무시 Addressables/패키지 서명/work/HEAD/diff check를 감사해. 선택 값 경로는 해시테이블 ['values']로 읽고 PowerShell5 JSON 배열을 불필요하게 중첩하지 마. 실행 중인 핸들을 확인하고 관찰 시간 초과만으로 재시작하지 마.

LevelPlacementRules.CoverValueError와 거미줄 배치 검증/피해/제거/내용물/미션은 전환하지 마. 공급 생성/낙하/이동/실제 피해·미션 집계/드론/파워/바닥·번식/UI/MVVM/표현/풀/봇/저장·변환/원본 에셋도 변경하지 마. 등록을 실제 소비자 전환 완료로 보고하지 마. 커밋/푸시/빌드/재패킹/팩·이미지 재생성/사용자 Unity 종료/씬 저장/새 패키지·asmdef를 금지해. 에이전트는 별도 요청 시에만 사용해.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-38-progress.md에 요구사항별 변경·실측·의도적 차이·미검증·남은 문제를 기록해. 전체 완료 후 보고하고 다음 한 단계 계획/목표/전체 명령문만 저장해. 완료 보고에 전체 명령문을 복사 가능한 코드 블록으로 제시하고 다음 단계 구현은 시작하지 마.
```


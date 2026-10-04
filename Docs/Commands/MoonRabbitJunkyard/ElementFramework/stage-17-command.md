# EF-17 복사용 목표 명령문

```text
ServeredMeridian의 EF-17 발전기 배치 크기·충전 수치 정의 연결을 진행해.

다음 문서를 읽어:
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md
- Docs/Systems/MoonRabbitJunkyard/2026-10-04-element-framework-design.md
- Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-16-progress.md
- Docs/Planning/MoonRabbitJunkyard/ElementFramework/stage-17-generator-placement-plan.md
- Docs/Goals/MoonRabbitJunkyard/ElementFramework/stage-17-generator-placement-goal.md

이번 단계만 수행해. work 브랜치를 유지하고 실제 발전기 배치/편집/검사·연결/런타임 구성 호출부를 확인해. 기존 작업/원본/GUID와 전환 전 실제 입력·크기2/최대 내구도0/충전2~6·2×2 경계/내부벽/중복·본체 ID/전선 결과를 보호해. 기존 양수 내구도 프로필을 완화하지 말고 발전기의 크기와 배치 충전 허용 범위를 작은 불변 프로필로 정의에 조합해. obstacle.generator를 한 번 준비하는 카탈로그에 등록하고 LevelPlacementRules.Size와 ObstacleValueError의 발전기 배치 충전 조회만 연결해. 실제 기본값2/3~5, 최대 내구도0·기존5종/미지원 결과·2인자 정의 계약을 유지해. 누락 정의/필수 프로필은 ID 포함 오류로 거절하고 기본값 대체를 하지 마. 다른 메모리 수치도 같은 카탈로그/프로필 조회에 반영됨을 검사해.

충전3~5 허용/2·6 거절·실제 경계/내부벽/중복·ID/전선 연결을 전후 비교해. 같은 저장 메모리 입력으로 실제 충전/활성 연결/대상 제거·피해/미션/규칙·전역 난수/MemoryPack 바이트·버전1·50구간 보존을 확인해. EF-15 missing Generator fixture는 이제 카탈로그의 미등록 유효 ID 오류 검사로 조정하되 누락 오류 검증은 유지해. 별도 Editor에서 새 검사, DurablePlacementVerification.Run, CratePlacementVerification.Run, ElementCatalogVerification.Run, ElementIdVerification.Run, FixedObstacleVerification.Data, GeneratorVerification.Data, BotObservationVerification.Run, ScrapVerification.Data를 실행하고 실제 입력/수치/오류/각 종료0·필수 FAIL0을 기록해. 과거 증거는 보존해.

발전기 실행 정책·피해/미션/낙하/공급·드론·봇 DTO·UI/MVVM·표현/풀·제작/배포 에셋·저장 포맷·원본 변환·팩 재생성은 전환하지 마. EF-05/09/11·원본/GUID/enum 숫자/기존 변경을 보존해. 빌드·재패킹·이미지 작업·임의 커밋·사용자 Unity 종료·씬 저장 금지. 필수 실패를 숨기거나 목표를 축소하지 마.

Docs/Verification/MoonRabbitJunkyard/ElementFramework/stage-17-progress.md에 변경·실측·검증·미검증·남은 문제를 저장해. 완료 조건 충족 후 보고하고 결과에 맞춘 다음 한 단계 계획서·목표문서·명령문만 작성해. 명령문은 Docs/Commands/MoonRabbitJunkyard/ElementFramework에 저장하고 완료 보고에도 전체를 복사 가능한 코드 블록으로 제시해. 다음 단계 구현은 시작하지 마.
```

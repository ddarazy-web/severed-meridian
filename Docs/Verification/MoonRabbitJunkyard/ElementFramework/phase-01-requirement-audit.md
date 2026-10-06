# 큰 구간 1단계 요구사항별 감사

상태: 완료. 아래 요구사항과 최종 원문·종료·복원·보호 증거를 모두 대조했다. 2026-10-06.

기준: [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/phase-01-common-element-rules-goal.md), [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/phase-01-common-element-rules-plan.md), [실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/phase-01-command.md).

## 구현과 검증 근거

| 목표 조건 | 현재 직접 근거 | 최종 판정 근거 |
|---|---|---|
| 1. 기존6종·Web/Mold/Dust의 실제 정의 연결과 누락 오류 | LegacyElementDefinitions/LegacyElementMap, CommonLayerRulesVerification의 DefinitionChecks/ConnectionChecks, 기존 정의 오류 검사 | 완료: RegressionFinal2의30종 실제 종료0/FAIL0 |
| 2. 거미줄/먼지 실제 피해·소비·내용물·미션 | LayerDamageRules와 ElementLayerBehaviorRegistry, PowerEffectResolution·매칭 소비 호출 경로, 새 층 검사172 PASS와 기존 층 내부 검사, 실제 상태30개 기준 비교 | 완료: RegressionFinal2의30종 실제 종료0/FAIL0 |
| 3. 번식·발전기 수명주기 | ElementTurnBehaviorRegistry→MoldRules.FinishTurnDefinition, 등록 충전 행동→GeneratorRules.QueryDefinition/ApplyDefinition→기존 활성화/타겟 해제/퇴역 경로. 새 수명주기400 PASS, 고정 입력 상태15개 기준 비교와 비기본 초기 내구도/충전량 검사 | 완료: RegressionFinal2의30종 실제 종료0/FAIL0 |
| 4. 실제 공급/회수 생성과 순서·소진·미션 | SettlementResolution→선택된 정의의 공급 등록 행동→LevelRuntimeState.SupplyObstacle. 새 공급226 PASS,9종×3시드 실제 상태27개 기준 비교와 로켓 공급 정의를 폭탄으로 바꾼 실제 보드 검사 | 완료: RegressionFinal2의30종 실제 종료0/FAIL0 |
| 5. 기존 중첩 피해·색·캡슐·낙하 | 최종 소스의 전체18182행 원문과 EF37 원문 SHA256 동일. 신규 검사에 기존 정착 검사 포함 | 완료: final-runtime-audit.json의30개 실제 종료0/FAIL0 및 전체 원문 비교 |
| 6. 다른 ID 실제 판정→적용→제거/미션 | CommonElementExtensionVerification.Durable의 테스트 카탈로그/등록 Query·Apply·제거 미션, CommonLayerRulesVerification.AlternateChecks의 다른 ID 덮개 실제 적용. 공급도 다른 ID 재사용. 확장21 PASS | 완료: 최종30종 종료0/FAIL0. 제작 UI/저장은 구간3 범위 |
| 7. 직접 행동 키 조회/상태 소유권 보존 | Layer/Turn/Supply 등록표의 Dictionary.TryGetValue, 기존 반응 등록표. 공급 선택/커서/난수는 정착 실행기, 실제 본체·층·미션·턴 기록은 기존 상태/규칙 소유 | 완료: final-runtime-audit.json의498개 최종 소스 해시 일치 |
| 8. 신규+기존30종·전체18182행/188팩/225상태·메타데이터 | 신규4개 실제 종료0,819 PASS/0 FAIL. final-runtime-audit.json의 전체 원문 비교/기록 종류 수량 | 완료: final-runtime-audit.json과 final-metadata-audit.json 모두 통과 |
| 9. 원본/GUID/enum/저장/기존 WIP/과거 증거 보호 | 기존 변경은17개 생산 소스+2개 검사,19개이며 상자 검사는 오버로드 서명 선택 한 줄만 보정. 최종 자산·메타·설정1489개 차이0. 추가10개 소스+10개 메타, 각 GUID 형식 확인. 기존 공개 입력/DTO/enum/팩 형식 소스는 변경하지 않음 | 완료: final-protection-audit.json 및 final-output-independence-audit.json. 원문75개 복원/독립, 과거6941개 차이0, GUID10개 유일, work/HEAD 동일 |
| 10. 완료 보고와 다음2단계 인계 | 진행 기록과 책임/확장 경계 기록. 다음 드론 소스 읽기 전용 조사 메모 | 완료: phase-01-progress.md 최종 보고, phase-02 계획/목표/전체 실행문과 문서 색인 연결. 다음 구현 미착수 |

증거 기본 경로는 `Logs/ElementFramework/Phase01`이다. 최종 기존 검사의 새 결과는 `RegressionFinal2`이며 실패한 이전 `Regression`/`RegressionFinal` 결과를 덮어쓰지 않는다. 상자 검사 코드의 오버로드 선택 보정 후 신규4개와 기존30개 전체를 최종 소스로 다시 실행하여 모두 통과했다. PASS 수량만으로 범위 충족을 주장하지 않고 해당 검사 입력·실제 상태/효과·종료 영수증을 함께 확인한다.

## 실제 연결의 책임

- 정의: 불변 수치와 Layer/Turn/Supply 행동 키를 제공한다. 카탈로그는 기존6종, 덮개2종, 먼지1종과 공급9종을 공유한다.
- 반응: 본체 등록 행동과 층 등록 행동이 선택된 정의를 실제 판정/적용에 소비한다. 등록 내구도 제거는 선택한 정의의 제거 미션을 유지한다.
- 턴 종료: 곰팡이 번식 등록 행동은 기존 후보 정렬/벽/난수/턴 중복 기록을 유지하며 정의의 초기 내구도와 층 미션 목표를 소비한다.
- 공급: 정착 실행기는 생성 위치/생성구 선택/목록 소진/커서를 소유한다. 공급 등록표는 선택된 생성 작업만 처리하며 새 본체 인스턴스와 이력은 상태가 소유한다.
- 미션: 정의의 층/제거 미션 값을 실제 예측·완료·최초 가용 수량에 사용한다. 기존 Color/Recovery 입력과 회수 기능의 계약은 유지한다.

## 남겨 둔 종류 분기의 분류

| 위치 | 유지한 분기 | 유지 이유와 후속 경계 |
|---|---|---|
| LegacyElementMap와 LegacyElementDefinitions 진입점 | 기존 ObstacleKind/CoverKind/SupplyKind→ID | 저장/제작 입력 호환. ID 제작·저장 전환은 구간3 |
| ObstacleDamageRules.Supports/Mission/Apply/Remove | 미지원 종류 거절/구형 직접 함수의 집계·제거 계약 | 기존 공개 입력과 직접 함수 검사 보존. 실제 등록 내구도 행동의 선택은 ReactionBehavior 키이며 정의 미션을 소비 |
| ObstacleDamageRules.QueryDurability | Safe의 미정의 DamageCause 경계, ColorLock 필수 색 정책 거절 | 정의 값으로 일반 색 조건을 처리하며 기존 오류/미지원 원인 계약만 보존 |
| ObstacleDamageRules.RemoveDefinition | GeneratorCharge는 제거 미션 없음, 구형 Generator의 제거 미션 없는 내구도 교체 사례 | 다른 정의의 지정 미션은 사용. 기존 실제 Swap 회귀 두 사례로 호환 경계 확인 |
| LevelMissionRules/MissionProgressRules | 기존 공개 미션 종류의 Normal/Recovery/본체/층 구분과 공급 상한 집계 | 새 콘텐츠마다 미션 enum을 추가하는 구조로 확대하지 않음. 공급/층의 실제 미션 값은 정의를 소비. 활성 드론 검색 정책은 구간2 |
| SettlementResolution | 고정 고철/회수 생성구의 기존 가용성 검사, 유지 모드와 마지막 이동 보너스 | 공급 선택 알고리즘/저장 계약 보존. 선택된 일반 공급 생성은 등록 행동을 사용 |
| ElementSupplyBehaviorRegistry.RandomPower | 기존 Rocket 선택 시 방향 난수 소비 | 기존 후보3종과 규칙 난수 소비 순서 보존. 임의 콘텐츠 제작 카탈로그 지원은 구간3 |

타입 ID가 다르다는 이유로 같은 피해/소비/번식/생성 함수를 복사하지 않는다. 새 ID의 테스트 정의는 기존 행동 키와 다른 수치/미션 값을 사용한다. 현재 구형 레벨 DTO로 임의 ID를 배치·저장할 수 있게 된 것으로 보고하지 않는다.

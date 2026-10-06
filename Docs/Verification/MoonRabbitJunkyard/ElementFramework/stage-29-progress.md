# EF-29 — 최초 장애물 미션 수량 정의 연결 완료 기록

상태: 완료. 새47201+기존228623=275824 PASS/0 FAIL. 최종22종 각각 별도 Editor 실제 종료0. 원본·과거 출력·기존 작업 감사 통과. work/6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783와 EF-23~28 미커밋 변경을 유지한다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-29-initial-mission-supply-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-29-initial-mission-supply-goal.md).

## 변경과 실제 호출

LevelMissionRules.Supply의 최초 장애물 매칭 predicate와 Elements using만 변경했다. 제거 미션5종/내구도형5종에만 기존 Get→RequireRemovalMissionProfile→Kind를 적용한다. Color/Web/Mold/Dust/Recovery, 고정/유지 공급 산식, Validate, Name은 그대로다. 목록의 본체 항목 수를 세며 내구도/2×2 점유 칸수를 곱하지 않는다.

Supply는 편집기 미션 수량 표시와 Validate에 사용되며 LevelDefinitionValidator→LevelStateBuilder가 해당 검증을 호출한다. 실제 위치는 Logs/ElementFramework/Stage29/call-sites.txt에 기록했다. 생산 변경을 기존 predicate/using으로 되돌린 메모리 문자열이 이전 파일 전체와 정확히 일치하는 것으로 변경 범위를 증명했다.

기존 두 fixture는 별도 FixtureProbe에서 실제 Build→LevelDefinitionValidator→LevelMissionRules.Validate→Supply→RequireRemovalMissionProfile의 누락 오류를 확인했다. 12 PASS/0 FAIL·종료0으로 두 예상 오류와 원래5종 등록 참조 복원을 검사했다. 집계 검사 임시 정의에는 원래 제거 미션 프로필을 보존하고, 제거 미션 조회용 상태는 등록 교체 전에 준비하도록 최소 수정했다. 두 파일을 해당 수정만 원복한 메모리 문자열도 각각 이전 파일 전체와 동일하다. 생성자/누락 오류 기대값은 낮추지 않았다. FixtureProbe는 수정 전 임시 정의의 영향 진단이며 최종 회귀의 대체가 아니다.

## 실제 RED→GREEN과 실패 이력

- RED: 유효한 Crate/Scrap 본체와 양쪽 미션의 저장 입력에서 같은 ID Crate 프로필을 Scrap으로 교체했다. 실제 Initial1/1, 실제 Validate[]였으며 기대값0/2와 Crate 부족 공급 판단에 대해2 PASS/1 FAIL·종료1이었다. finally의 정확한 원래 등록 참조 복원 PASS를 포함한다. 공개 수정 API나 문자열로 만든 실패는 없다.
- Before: 생산 수정 전47132 PASS/0 FAIL·종료0.
- GREEN:47201 PASS/0 FAIL·종료0. 같은 ID 교체 시 actual0/2이고 InsufficientSupply, 경로 missions.Array.data[0], 메시지 ‘나무상자 제거 목표 1: 최초 0 + 고정 0 + 유지 0 = 0’을 실제로 기록했다.
- 유효5종의 프로필 누락은 Supply/전체 Validate/Build 모두 ID 포함 오류다. 비제거/미지원 미션은 누락 등록에서도 전체 요약을 유지하고 null 장애물 목록은0이다. 모든 임시 등록은 finally에서 원래 참조로 복원했다.

제품 검사·컴파일의 추가 실패는 없었다. 보조 감사 스크립트는 Windows PowerShell5의 UTF-8 BOM/결과 읽기, 빈 목록의 와일드카드 해석, JSON 배열 파이프 열거 차이로 네 차례 실패했다. BOM·명시적 UTF8 읽기·리터럴 Contains·파싱 후 배열 필터로 수정했다. 실제 결과/예상값/생산 코드/검사 입력은 바꾸지 않았고 21종 메서드 일치와 RED 복원/빈 오류 목록 조건도 유지했다. 이 이력은 Stage29/progress.txt에 남겼으며 최종 전체 감사는 실제 종료0이다.

## 전체 입력·실측·동등성

시드12345, 입력176개(본체168·공급8)를 최초 생성 후 재사용한다. 전후14558행/466591465바이트 전체 파일이 동일하다. SHA256: 0DAF128BCCEE6EE8A3307B52C95CC4B1B14135725A3983C3F4CDC5B2A2209D1C. 식별자·지문·상태·메시지를 제외하거나 정규화하지 않았다. 정의 메타데이터 전후 SHA256도 3B437F85E5ABE86B7106B499E929DB2C9F14E0E2EA37789F9A9AF8DF45E2A148로 동일하다.

- 최초 수량672행: 내구도28개 수치×본체0/1/2×다른종 혼합 유무×목표0/1/2/3. 전체 요약 필드/문자열·미션/전체 오류·실제 Build 응답·입력/전역 난수/dirty 보존. 2×2 내구도9 본체2개+다른종/목표2 사례도 Initial2, 오류[], 실제 구성 성공이다.
- 공급384행: 무작위/고정/유지, 목표0/1/9/intMax, 모든 미션/미지원 종류. 유지 생성구 중복, 유지 비활성·음수 상한, 잘못된 고정 항목 제외, long 고정 합4294967294를 포함한다. 정상·오류 입력의 전체 검증 목록과 Build 응답을 비교했다.
- 발전기/미지원 장애물20행, null 장애물 목록7행: 필수 프로필 강제 없이 기존0 반환.
- 기존13475행: 미션 조회112·실제 제거 타격306·발전기48와 이전13009행. 전체 실행 상태/비공개 문맥/규칙·전역 난수·미션/효과·예약/기여/완료 예상/취소/무효화/재선정·발전기 충전/연결/철거를 보존했다.
- MemoryPack188개 전체 바이트/본체ID와 버전1/레벨1·50·51·100·101의 실제 Encode→ReadLevel→Encode, 50구간·원본/왕복 상태225행이 동일하다. 원본 에셋/배포 팩 재생성은 하지 않았다.
- 같은 ID 연결/누락 응답41행: 실제 수량/부족 공급1, 누락 프로필5, 누락 상태의 비제거/미지원35. 원래 등록 참조 복원과 기본값 대체 부재를 검사했다.

## 별도 Editor 최종 검사

아래22종을 각각 별도 Editor에서 실행했다. 각 결과는 PASS>0/필수FAIL0/실제 종료0이며 정확한 기존21종 메서드를 EF-28 최종 실행 표와 대조했다. Before/RED/FixtureProbe는 아래 합계에 포함하지 않는다.

| 검사 | 실제 메서드 | PASS | FAIL | 종료 |
| --- | --- | ---: | ---: | ---: |
| after | Elements.Editor.InitialMissionSupplyVerification.Run | 47201 | 0 | 0 |
| removal-mission | Elements.Editor.RemovalMissionProfileVerification.Run | 43431 | 0 | 0 |
| record | Elements.Editor.DamageRecordPolicyVerification.Run | 42279 | 0 | 0 |
| reserved | Elements.Editor.ReservedDamagePolicyVerification.Run | 7793 | 0 | 0 |
| aggregation | Elements.Editor.DamageAggregationPolicyVerification.Run | 75264 | 0 | 0 |
| color-match | Elements.Editor.ColorMatchPolicyVerification.Run | 20861 | 0 | 0 |
| generator-reaction | Elements.Editor.GeneratorReactionPolicyVerification.Run | 24489 | 0 | 0 |
| appliance-damage | Elements.Editor.ApplianceDamagePolicyVerification.Run | 5085 | 0 | 0 |
| color-lock-damage | Elements.Editor.ColorLockDamagePolicyVerification.Run | 4452 | 0 | 0 |
| capsule-damage | Elements.Editor.CapsuleDamagePolicyVerification.Run | 592 | 0 | 0 |
| scrap-damage | Elements.Editor.ScrapDamagePolicyVerification.Run | 960 | 0 | 0 |
| crate-damage | Elements.Editor.CrateDamagePolicyVerification.Run | 696 | 0 | 0 |
| charge-placement | Elements.Editor.GeneratorPlacementVerification.Run | 144 | 0 | 0 |
| durable | Elements.Editor.DurablePlacementVerification.Run | 210 | 0 | 0 |
| crate | Elements.Editor.CratePlacementVerification.Run | 75 | 0 | 0 |
| catalog | Elements.Editor.ElementCatalogVerification.Run | 1541 | 0 | 0 |
| id | Elements.Editor.ElementIdVerification.Run | 49 | 0 | 0 |
| fixed | Levels.Editor.FixedObstacleVerification.Data | 142 | 0 | 0 |
| generator | Levels.Editor.GeneratorVerification.Data | 246 | 0 | 0 |
| bot | Levels.Editor.BotObservationVerification.Run | 32 | 0 | 0 |
| scrap | Levels.Editor.ScrapVerification.Data | 186 | 0 | 0 |
| power | Levels.Editor.PowerEffectVerification.Data | 96 | 0 | 0 |
과거 결과/값37개를 각각 백업→삭제→새 생성→Stage29 증거 복사→finally 전체 바이트 동일 복원했다. 과거 증거3504개 전체도 시작 해시와 동일하다. 실제 입력·응답·시작/종료·메서드는 Stage29의 execution.json/results.txt/values.jsonl과 verified-results.json에 기록했다.

## 보존·검토·미검증·다음 단계

보호1956개 중 승인3개만 변경,1953개 동일이다. 무시된 Android addressables_content_state.bin/meta와 패키지 서명4개도 보호 목록에 포함했다. 기존 enum/GUID·EF-05 예외 원복·EF-09 표현·EF-11 풀과 원본 에셋은 그대로다. 신규 Assets 파일은 검사와 meta2개만이며 새 GUID는 유효하고 유일하다. 기존 미커밋은 두 fixture의 최소 조정과 이번 완료/다음 단계 문서 갱신 외에 보존했다. 최종 감사·git diff --check 통과.

자체 검토에서 2×2 증폭/발전기 기본상자 집계/비제거 필수 조회/주변 산식 변경/fixture 검증 시점 혼동을 실제 경계와 파일 전체 대조로 확인했다. 별도 에이전트는 계획의 ‘별도 요청 시’ 규칙에 따라 사용하지 않았다.

사용자 기기 화면·성능/Android·iOS IL2CPP/Addressables 콘텐츠 빌드는 수행하지 않았으며 이 데이터 검사로 주장하지 않는다. 커밋/푸시/플레이어 또는 콘텐츠 빌드/재패킹/이미지·팩 재생성/사용자 Unity 종료/씬 저장을 하지 않았다. 다른 요소·공급 생성·저장/제작 도구·드론 목표/비행·표현/리소스 전환은 남아 있다.

다음 한 단계는 [EF-30 회수캡슐 일반 인접 정책 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-30-capsule-adjacent-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-30-capsule-adjacent-policy-goal.md) · [전체 복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-30-command.md)이다. SourcePolicy가 먼저 있어도 Safe 일반 인접을 종류로 다시 거부하는 조건을 다음 단계에서만 다룬다. 자석 인접의 전단/내부 제한은 별도로 남긴다. EF-30 구현은 시작하지 않았다.

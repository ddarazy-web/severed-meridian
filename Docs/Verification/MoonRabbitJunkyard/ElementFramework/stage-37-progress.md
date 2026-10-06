# EF-37 — 고철 유지 공급 내구도 검증 연결 결과

상태: 구현·검증·복원 감사 완료. 다음 단계 문서 및 최종 인계 문서 감사 완료.

## 변경과 내부 계약

생산 변경은 Assets/Scripts/Features/BlockSupply/Rules/LevelSupplyRules.cs의 Validate 상한 조건 하나뿐이다. 내구도>1일 때 기존 LevelPlacementRules.MaxDurability(Scrap)를 조회한다. ElementPlacementProfile 생성자가 양수 최대값을 보장하므로 기본1은 조회 없이 판정한다. 미사용 설정도 명시적 내구도>1의 기존 상한 검증을 유지하며 숨긴 대체 상한은 없다.

원래 생성구 위치/중복 → 모드/소진 → 고정 항목 → 비고정 목록 충돌 → 고정·유지 충돌 → 고철 설정 → 회수 설정 → 회수 부품 순서를 유지했다. 목표/한도 음수와 내구도 하한의 단락 평가, 공개 서명·오류 코드/경로/문구·ItemError/FixedCount 필터·회수/기타 공급은 원문 동일하다. 전체 검증은 공급 뒤 미션 검증을 수행하고 Build는 오류가 있으면 상태를 생성하지 않는다. 구체 계약은 [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-37-scrap-maintain-durability-plan.md)에 기록했다.

실제 공급 생성·이동·낙하·피해·미션 집계와 다른 요소는 변경하지 않았다. 다른 기존 C#486개는 전체 파일 해시가 동일하다. 새 정책/키/행동/공개 API/생산 리플렉션/실행기는 없다.

## 검사 먼저 확보한 실패와 GREEN

최종 Red 실제 종료1, PASS93/FAIL6. 같은ID/Size 고철의 Placement.MaxDurability만2/7로 교체해 유지 내구도3/6의 잘못된 허용/거절2건, 필요한 배치 누락 ID 오류2건, 미사용 명시 설정의 동일 상한2건을 확인했다. 실제 공개 Supply Validate와 전체 Validate/Build를 호출했고 유효 기반 입력을 먼저 확인했다. ID/이름·크기·기존 피해/집계/미션/행동 참조를 복사하고 모든 교체는 finally 정확한 원래 정의 참조로 복원했다.

Before50907/Quick111/Run50950 모두 별도 Editor 실제 종료0/FAIL0. 경계17건, 실제 상한 연결4건, 필요한 프로필 누락2건을 확인했다. 상태/원본/규칙·전역 난수/dirty 무변경, 공급 없음/null/빈 목록/미사용 기본/0·음수/다른 공급·오류 순서와 불필요한 조회/예외 방지도 확인했다.

준비 실패는 runs에 보존했다. 새 미사용 fixture의 빈 미션 때문에 InvalidMission이 발생한 Quick(종료1/PASS82/FAIL1), JSON이 실제 null 목록을 만들지 않은 Red(종료1/PASS1/FAIL1)는 새 fixture만 보정했다. 기대 결과나 상태 수량을 강제하지 않았고 이 준비 실패를 요구한 상한 RED로 인정하지 않았다. 초기 Before50874 이후 경계를 보강해 최종 Before를 다시 실행했다.

## 정상 전체 비교와 의도적 차이

정상 baseline/before/after 전체18182행·616394258바이트는 SHA256 53B9D59B4B742EAF2B021749FF00FF2A155E5741CE98FB4634159393A353912F로 동일하며 EF36 전체 원문과도 동일하다. 정의 전후2756바이트는 SHA256 408B3C7DC00698AD84DDDA484AABC99DF2B6E8DFBAE4845BD1768A662B10902F로 동일하다. 정상6종의 ID/키/이름/정책 bool/프로필 값·참조/부분 생성자와 기존 오류 계약을 보존했다.

188 MemoryPack 전체 바이트·225팩 상태·본체ID·버전1/50레벨 구간, 공개 교환40/직접 적용405/최초 공급672/공급 요약384/sequence8064행, 전체 상태/비공개 문맥/규칙·전역 난수/피해·미션·효과/예약·완료 예상·취소·무효화·재선정/발전기 충전·연결/공급/전체 검증을 확인했다. 정상 실행은 정규화하지 않았다.

비기본 최대2에서는 내구도3을 거절하고 최대7에서는 내구도6을 허용한다. 유지/미사용 명시 설정과 실제 전체 검증/Build가 이를 소비한다. 필요한 배치 프로필 누락은 obstacle.scrap ID 오류로 거절한다. 이 의도적 차이는 after-connection-values.jsonl에 별도로 기록했다.

## 정확한 회귀와 출력 복원

EF36 verified-results.json의 최종29개 메서드와 PASS 수를 정확히 대조했다. 기존29종636061 + 새 Run50950 = 687011 PASS/0 FAIL, 총30종 별도 Editor 실제 종료0이다. Before/Quick/Red는 위에 별도로 기록했다.

작성 경로를 먼저 조사한 뒤 필수71출력/조건부 자석 실패2경로에 사전 해시 → 백업 → 삭제 → 새 생성 시간/해시 확인 → 증거 보존 → finally 원문 복원을 수행했다. 71개 원본 모두 독립 파일임을 fsutil로 확인했다. 같은 해시의 불변 증거만 공유하며 새 생성과 원본 복원을 생략하지 않았다.

과거 정의 메타데이터9개 중4개 원문 동일,5개는 기존 EF33 행동 키 추가만 다르다. 이 차이는 metadata-differences.json에 기록했고 런타임 결과에는 적용하지 않았다. 실행기 최종 콘솔의 이전 템플릿 '28' 표기는 실제 선언/필수/실행/영수증29종과 별개인 표기 오류였으며 Stage37 실행기 문구만29로 정정했다. 검사를 재실행하거나 결과를 변경하지 않았다.

## 보호·감사·남은 작업

보호1976개 중 생산1파일만 변경, 기존작업110개에서 승인된 현재 계획/목표만 변경, 과거 증거6448개와 입력38개 변경0. 원본/GUID/enum/EF05·09·11/무시 Addressables·패키지 서명/work/HEAD/diff check 감사 통과. 신규 Assets는 검사·meta2개뿐이며 GUID 51e07d764a0e466c9eca7404b7b42e5b 유일성을 확인했다. work/HEAD 6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783 유지.

checkpoint/metadata/독립성/보호/최종 집계/new-values/coverage/source 감사 실제 종료0. 증거는 Logs/ElementFramework/Stage37에 있다. 최종29종+새 Run의 정확한 목록은 verified-results.json 및 아래 표를 따른다.

커밋/푸시/빌드/재패킹/팩·이미지 재생성/사용자 Unity 종료/씬 저장/에이전트/새 종류·행동·패키지·asmdef를 수행하지 않았다. UI/MVVM/표현/풀/봇/저장·변환/원본 에셋은 전환하지 않았다. 미검증은 제외된 플레이어/Addressables 빌드와 실제 기기 화면이다. 구현 요구의 남은 오류는 없으며, 다음 한 단계 계획/목표/전체 복사용 명령문을 작성했다. 최종 인계 문서 감사 결과는 마지막에 기록한다.

| 메서드 | PASS | FAIL | 종료 |
| --- | ---: | ---: | ---: |
| Elements.Editor.ScrapMaintainDurabilityVerification.Run | 50950 | 0 | 0 |
| Elements.Editor.ScrapSupplyMissionPolicyVerification.Run | 51155 | 0 | 0 |
| Elements.Editor.ElementDurabilityApplyPolicyVerification.Run | 50875 | 0 | 0 |
| Elements.Editor.ElementReactionApplyVerification.Run | 53295 | 0 | 0 |
| Elements.Editor.ElementReactionBehaviorVerification.Run | 53053 | 0 | 0 |
| Elements.Editor.DurableMagnetPolicyVerification.Run | 54307 | 0 | 0 |
| Elements.Editor.CapsuleMagnetPolicyVerification.Run | 49166 | 0 | 0 |
| Elements.Editor.CapsuleAdjacentPolicyVerification.Run | 48386 | 0 | 0 |
| Elements.Editor.InitialMissionSupplyVerification.Run | 47201 | 0 | 0 |
| Elements.Editor.RemovalMissionProfileVerification.Run | 43431 | 0 | 0 |
| Elements.Editor.DamageRecordPolicyVerification.Run | 42279 | 0 | 0 |
| Elements.Editor.ReservedDamagePolicyVerification.Run | 7793 | 0 | 0 |
| Elements.Editor.DamageAggregationPolicyVerification.Run | 75264 | 0 | 0 |
| Elements.Editor.ColorMatchPolicyVerification.Run | 20861 | 0 | 0 |
| Elements.Editor.GeneratorReactionPolicyVerification.Run | 24489 | 0 | 0 |
| Elements.Editor.ApplianceDamagePolicyVerification.Run | 5085 | 0 | 0 |
| Elements.Editor.ColorLockDamagePolicyVerification.Run | 4452 | 0 | 0 |
| Elements.Editor.CapsuleDamagePolicyVerification.Run | 592 | 0 | 0 |
| Elements.Editor.ScrapDamagePolicyVerification.Run | 960 | 0 | 0 |
| Elements.Editor.CrateDamagePolicyVerification.Run | 696 | 0 | 0 |
| Elements.Editor.GeneratorPlacementVerification.Run | 144 | 0 | 0 |
| Elements.Editor.DurablePlacementVerification.Run | 210 | 0 | 0 |
| Elements.Editor.CratePlacementVerification.Run | 75 | 0 | 0 |
| Elements.Editor.ElementCatalogVerification.Run | 1541 | 0 | 0 |
| Elements.Editor.ElementIdVerification.Run | 49 | 0 | 0 |
| Levels.Editor.FixedObstacleVerification.Data | 142 | 0 | 0 |
| Levels.Editor.GeneratorVerification.Data | 246 | 0 | 0 |
| Levels.Editor.BotObservationVerification.Run | 32 | 0 | 0 |
| Levels.Editor.ScrapVerification.Data | 186 | 0 | 0 |
| Levels.Editor.PowerEffectVerification.Data | 96 | 0 | 0 |

다음 [EF38 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-38-web-catalog-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-38-web-catalog-goal.md) · [전체 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-38-command.md)을 준비했다. 기존 거미줄의 영구 ID/불변 카탈로그 등록만 수행하며 실제 덮개 실행/검증 전환은 이후로 분리한다. 다음 구현은 시작하지 않았다.


최종 문서 감사 실제 종료0: 현재 계획5/목표8 완료, 다음 계획5/목표8 미완료·다음 구현 없음, 문서8개/상대 링크172개/전체 복사용 명령문2563자를 확인했다. 원문과 같은 전체 실행문을 완료 보고에 제공한다.


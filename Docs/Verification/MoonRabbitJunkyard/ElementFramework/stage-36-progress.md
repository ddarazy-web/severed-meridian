# EF-36 — 고철 공급의 미션 수량 연결 완료 기록

상태: 구현·전체 회귀·출력 복원·보호 감사 완료. 다음 한 단계 문서만 작성했으며 다음 구현은 시작하지 않았다.

최종 결과: 새 Run51155 + 기존28종584906 = 636061 PASS/0 FAIL, 별도 Editor29종 실제 종료0. 필수67출력 원문 복원, 정상18182행/188팩 전체 바이트 동일, 과거5964증거 변경0. 생산 변경은 LevelMissionRules.cs의 공급 대상 선택 영역 한 곳이다. 신규 검사는 ScrapSupplyMissionPolicyVerification.cs와 meta이며 기존 검사는 수정하지 않았다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-36-scrap-supply-mission-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-36-scrap-supply-mission-goal.md) · [증거](../../../../Logs/ElementFramework/Stage36/completion-audit.json).

## 호출 조사와 변경

LevelStateBuilder.Build는 v4 확인→전체 검증→상태 구성을 수행한다. 전체 검증은 배치/층/흐름/연결→공급→미션 검증 순서이며 미션 검증이 공개 Supply를 소비한다. FixedCount는 Fixed 모드·같은 종류·비null 항목·ItemError 성공 항목만 long으로 합산한다. ItemError의 enum/양수/색·방향/고철 배치 프로필 순서를 유지했다.

기존 Scrap 미션의 FixedCount 순서는 보존했다. 다른 내구도 미션은 고정 고철 항목이 있고 제거 프로필이 같은 미션 또는 누락된 경우에만 FixedCount를 검사한다. 알려진 다른 미션에는 기존에 없던 배치 누락 오류를 만들지 않는다. 유효 고정 수량 또는 0 하한 적용 후 유지 한도가 양수일 때 같은 고철 정의의 필수 제거 미션 프로필로 대상 여부를 판정한다. 최초 수량/동적 조기 반환/Recovery 계산·GoalBased/최종 반환은 원문 동일하다. 새 정책·키·행동·공개 API·생산 리플렉션은 없다.

## 검사 먼저 확보한 실패와 수정

최종 선행 Red: 실제 종료1, PASS12/FAIL6. 원래 정의에서 고정/유지/혼합 입력의 전체 검증과 Build가 통과한 뒤 같은ID 고철의 RemovalMissionProfile만 Crate로 교체했다. 공개 Supply의 Fixed/Maintained/Maximum과 실제 Validate/Build의 연결3건, 필요한 프로필 누락 ID 오류3건이 의도대로 실패했다. 각 정의는 finally 정확한 원래 참조로 복원했다. 수량/결과를 강제하지 않았다.

Before: 실제 종료0, PASS51126/FAIL0. 최종 Quick: 실제 종료0, PASS316/FAIL0. Run: 실제 종료0, PASS51155/FAIL0. 공개 수량/검증/Build와 누락 ID 오류, 원본/상태/규칙·전역 난수/dirty 무변경, 288개 공급 없음/null/빈/무효·0 수량/미사용 유지/비관련 미션 경계와 배치 오류 순서4건을 검사했다.

준비 실패와 수정은 runs 폴더에 원문을 보존했다. 신규 검사의 Editor namespace/internal 생성자 접근 오류, 입력 사본 누락, 내부 블록 없는 덮개·담당 생성구 없는 유지 설정, 고철 Fixed+MaintainScrap의 기존 SupplyConflict를 새 fixture에서만 조정했다. 유효 혼합은 고철 유지와 회수/로켓 고정 공급을 사용한다. Unity JSON null 덮어쓰기가 실제 null을 만들지 않아 새 fixture의 필드를 직접 null로 구성했고, Exception snapshot 재귀 오류는 예외 ToString 원문 기록으로 수정했다. 이 준비 실패를 요구한 RED로 인정하지 않았다.

첫 생산 연결의 무관한 Crate 조회가 고철 배치 누락 오류를 추가하는 문제는 새 공개 Supply 검사에서 Quick 실제 종료1/PASS289/FAIL1로 확인했다. 필요한 고정 수량 조회 범위를 최소 조정한 뒤 해당 오류 순서와 전체 Quick이 통과했다. 기존 fixture나 기대값은 바꾸지 않았다. Before의 정상 전체 기준과 최종 Run 원문은 동일하며 실제 null 구성 보정은 새 검사에만 적용했다.

## 정상 전체 비교와 비기본 차이

baseline/before/after 전체18182행·616394258바이트는 SHA256 53B9D59B4B742EAF2B021749FF00FF2A155E5741CE98FB4634159393A353912F로 동일하다. EF35 전체 실행 원문과도 동일하다. 정의 전후2756바이트는 SHA256 408B3C7DC00698AD84DDDA484AABC99DF2B6E8DFBAE4845BD1768A662B10902F로 동일하다. 정상 실행 결과를 정규화하지 않았다.

전체188 MemoryPack/225팩 상태/정상 공개 교환40건/직접 적용405건/최초 공급672건/공급 요약384건/sequence8064행을 확인했다. 전체 상태/비공개 턴 문맥/규칙·전역 난수/피해·미션·효과/예약·완료 예상·취소·무효화·재선정/발전기 임계·충전·연결/공급/전체 검증/본체ID·버전1/50구간은 기존29종과 정상 전체 원문으로 보존했다.

비기본 고철 정의의 제거 미션만 Crate로 바꾸면 Fixed7 또는 Maintained6이 새 미션의 상한에 기여하고 기존 Scrap 대상에는 기여하지 않는다. 실제 검증/Build도 이 수량을 소비한다. 연결3건과 누락3건·오류 순서1건은 별도 connection 원문에 기록했다. 정상6종 메타데이터·부분 생성자·기본 실행에는 차이가 없다.

## 출력과 보호 감사

모든 작성 경로와 필수67출력/조건부 자석 실패2경로를 먼저 조사했다. 사전 원문 해시→백업→삭제→별도 Editor 새 생성 시간/해시 확인→증거 보존→finally 전체 바이트 복원을 수행했다. 조건부 파일은 원래 부재 상태를 유지했다. 같은 해시의 보관 증거만 hardlink로 공유했으며 실제 새 출력은 생성했다. 원본 복원은 독립 파일로 수행했고 fsutil로 Stage35 원본이 보관 proof와 별도임을 확인했다. 각 lifecycle과 최종 감사가 모든 새 생성/증거/복원 해시를 검증했다.

부가 정의 snapshot8개 중3개는 원문 동일하고 과거 EF28~32의5개는 이미 도입된 EF33 반응 행동 키 추가만 있다. metadata-audit -RequireAll로 차이를 설명했으며 실행/팩 원문에는 정규화를 적용하지 않았다. source/protection/coverage/new-values/checkpoint/final 감사는 모두 실제 종료0이다.

work/6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783 유지. 보호1974파일 중 승인된 생산1파일만 변경, 기존작업104파일의 승인된 현재 단계 문서 외 예상 밖 변경0, 과거5964증거/기준입력37 변경0, 나머지 기존 C#485개 동일. 신규 Assets는 검사와 meta2개만 있으며 GUID78a50dc9f5274cebb4b8f03b90022428는 유일하다. enum/EF05/09/11·무시 Addressables/패키지/diff check를 함께 감사했다.

## 요구사항별 결론

| 요구사항 | authoritative evidence | 결론 |
| --- | --- | --- |
| 실제 호출·필터·오류 순서 조사 | callers-before/계획서/source-audit/배치 순서 검사 | 통과 |
| 고정·유지의 같은 정의 소비·최소 변경 | source-audit, 실제 공개 연결3건 | 통과 |
| 공개 서명/long/반환 필드·동적/회수/미지원/0 하한 보존 | 앞/뒤 원문 동일, 공급384건/경계288건/전체 비교 | 통과 |
| 유효 입력의 RED→GREEN·Validate/Build·finally 참조 복원 | Red/Quick/Run 실제 종료·결과·connection | 통과 |
| 필요한 누락 ID/원본·상태·난수 보존/새 예외 방지 | 누락 공개 호출3건·상태/RNG 검사·오류 순서4건 | 통과 |
| 정상6종 값/참조/부분 생성자/오류 보존 | 정의 전후 동일·카탈로그/ID 등 정확한 기존28종 | 통과 |
| 정상 전체 실행/예약/발전기/공급/검증/전체팩 | values-summary/coverage, 전체18182행/188팩 동일 | 통과 |
| 새 Run+기존28종 실제 종료0/정확한 검사 수 | verified-results29종/completion-audit | 통과 |
| 모든 출력·조건부 경로 원문 복원 | lifecycle28개/필수67출력·조건부2/최종 감사 | 통과 |
| 원본/작업/GUID/enum/과거/패키지/브랜치/diff | protection/new-files/source/final 감사 | 통과 |
| 제외 범위와 금지 작업 준수 | 승인 생산1파일·신규 검사2파일·보호 감사 | 통과 |
| 다음 한 단계 문서/전체 명령문·다음 구현 없음 | EF37 계획/목표/명령문·문서 감사 | 통과 |

## 실제 별도 Editor 결과

| 검사 | 실제 메서드 | PASS | FAIL | 종료 |
| --- | --- | ---: | ---: | ---: |
| scrap-supply-mission | Elements.Editor.ScrapSupplyMissionPolicyVerification.Run | 51155 | 0 | 0 |
| durability-apply-policy | Elements.Editor.ElementDurabilityApplyPolicyVerification.Run | 50875 | 0 | 0 |
| reaction-apply | Elements.Editor.ElementReactionApplyVerification.Run | 53295 | 0 | 0 |
| reaction-query | Elements.Editor.ElementReactionBehaviorVerification.Run | 53053 | 0 | 0 |
| durable-magnet | Elements.Editor.DurableMagnetPolicyVerification.Run | 54307 | 0 | 0 |
| capsule-magnet | Elements.Editor.CapsuleMagnetPolicyVerification.Run | 49166 | 0 | 0 |
| capsule-adjacent | Elements.Editor.CapsuleAdjacentPolicyVerification.Run | 48386 | 0 | 0 |
| initial-supply | Elements.Editor.InitialMissionSupplyVerification.Run | 47201 | 0 | 0 |
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

## 제약과 다음 단계

사용자 지시에 따라 커밋/푸시/플레이어·Addressables 빌드/재패킹/팩·이미지 재생성/사용자 Unity 종료/씬 저장/에이전트/새 패키지·asmdef·종류·행동을 수행하지 않았다. 피해·충전·제거/실제 미션 집계/공급 생성·낙하·이동/드론·파워·덮개·바닥·번식/UI·MVVM·표현·풀·봇/저장·변환/원본 에셋은 변경하지 않았다. 최종 검토는 작성자가 source 범위·오류 순서·콜러·검사/전체 바이트·복원 증거를 대조했다.

미검증: 제외된 플레이어/Addressables 빌드와 실제 기기 화면은 검사하지 않았다. EF36 구현 요구의 남은 문제는 없다. 전체 요소 전환은 아직 완료되지 않았다.

다음 [EF-37 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-37-scrap-maintain-durability-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-37-scrap-maintain-durability-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-37-command.md)을 작성했다. 유지 설정의 최대 내구도 상수5를 기존 배치 정의에 연결하는 한 책임만 다룬다. 다음 소스/검사 구현은 시작하지 않았다.
문서 감사 실제 종료0: 현재 계획5/목표8 완료, 다음 계획5/목표8 미완료와 다음 구현 없음, 문서8개/상대 링크170개/전체 복사용 명령문2694자를 확인했다. 전체 실행문은 저장 문서와 동일하게 완료 보고에 제공한다.

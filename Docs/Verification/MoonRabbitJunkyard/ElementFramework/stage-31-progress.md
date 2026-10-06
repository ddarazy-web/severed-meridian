# EF-31 — 회수캡슐 자석 인접 반응 정책 연결 기록

상태: 완료. 새49166+기존324210=373376 PASS/0 FAIL. 최종24종 각각 별도 Editor 종료0. 원본·과거 출력·기존 작업 감사 통과. work/6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783와 EF-23~30 미커밋 변경을 유지한다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-31-capsule-magnet-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-31-capsule-magnet-policy-goal.md).

## 변경과 현재 증거

DamageReaction.Evaluate의 자석 전단 제한에 Safe의 기존 RequireDamageSourcePolicy().MagnetAdjacent 조회만 추가했다. 기본false에서는 기존 종류 제한의 순서·전체 응답·메시지가 같다. ObstacleDamageRules.Query에서는 정책을 이미 통과한 Safe 자석 인접만 내부 잔여 종류 거부에서 제외했다. EF-30 일반 인접 연결과 다른 종류의 자석 제한은 유지한다. 정의·기본 정책·Apply/Remove/미션/자석 소비·범위/드론/저장/에셋은 수정하지 않았다.

실제 호출은 BoardActionExecutor.Swap→자석 발동→일반 블록 소비→PushAdjacent(MagnetAdjacent)→DamageReaction.Evaluate→ObstacleDamageRules.Query→허용된 반응의 공통 Apply다. 강제Apply나 Query 직접 호출만으로 실제 실행 성공을 만들지 않았다. call-sites.txt에 호출부를 기록했다.

새 CapsuleMagnetPolicyVerification은 런타임 상태를 먼저 구성한 뒤 테스트 메모리의 같은 Safe ID만 임시 교체한다. MagnetAdjacent 외 정책 값 및 다른 정의/프로필의 원래 참조를 유지하며 모든 교체는finally에서 정확한 원래 참조로 복원한다. 공개 등록 수정 API는 없다.

- RED: 실제 Evaluate는None/0, 기대Damage/1. 6 PASS/1 FAIL·종료1. 조회 상태/비공개 문맥/전역 난수 무변경 및finally 정확한 등록 복원 통과.
- 새 입력 최초 Before: 실제 교환이StartNotSatisfied로 거부됐다. 시작 보드의0,0/1,0/2,0에 같은 색3개가 이어진 새 fixture 오류다. 실패 입력·진단·결과를Logs/ElementFramework/Stage31/failed-fixtures에 보존했다. 자석을7,0, 교환 상대를7,1로 옮겨 초기 매칭을 제거했다. 기대값·생성자·누락 기대값·기존 fixture는 낮추거나 수정하지 않았다.
- Before 재검사:48414 PASS/0 FAIL·종료0. 생산 수정 전에 동일 입력의 전체 기준을 확보했다.
- Run:49166 PASS/0 FAIL·종료0. 실제 같은 ID 자석 인접Damage/1 및 공통 실행 확인.
- 실제 자석 적용10건: 내구도1~5×벽 유무, 두 본체 내구도·본체별 턴 기록·제거 미션·MagnetAdjacent 효과/내구도 전후/제거 본체 수 확인. 기본false의 같은10건에서는 해당 피해가 없다.
- 허용 조회:13조건×내구도1~5, false→true→false·다른6원인 전체 응답/메시지·조회 무변경. 색5개+null×내구도1~5도Damage/1이며 Safe에 색 비교를 추가하지 않았다.
- 의도적 차이: 유효 Safe의 정책 누락은 이전 전단 종류 제한의None/0 대신ID 포함InvalidOperationException으로 드러난다. false 대체가 없다. 거리/벽/비활성/삭제는 기존 선행 거부가 유지된다.

## 전체 동등성과 보존

시드12345, 새 저장 입력25개(조회10·자석10·다른 종류5)를 재사용한다. 전후16139행/528691048바이트 전체 파일 동일. SHA256:E6117588EE30D0341450B80D470881B0B99B8F1D596EDA3C81B619126BE6D0A7.

기본 자석 조회455행·실제 자석10행, 다른5종/7원인455행·비장애물196행과 EF-30 기본15023행을 포함한다. 기존 상태·비공개 문맥·규칙/전역 난수·피해/미션/효과·예약/완료 예상/취소/무효화/재선정·발전기/공급·전체 검증·MemoryPack188개 전체 바이트·본체ID·버전1·50레벨 구간/원본·왕복 상태225행을 비교했다. 허용 연결111행은 실제같은ID1·경계65·실제자석10·색/null30·누락1·선행거부4다.

초기 보호1960개 중1958개 동일이며 생산 변경은 위 두 자석 제한뿐임을 원본 전체 문자열 복원 비교로 확인했다. 과거4159개 모두 해시 동일, 검사출력41개는 백업·삭제·새 생성·증거 복사 후finally 전체 바이트 복원했고 새 출력의 실행 시간 내 생성도 확인했다. 새 검사meta GUID는ccbab5c284b24f11a39a40b887434aa8이며 중복0이다. enum/원본·EF-05/09/11·무시된 Android Addressables 상태/패키지 서명4개도 보호 목록에 포함해 보존했다.

## 별도 Editor 최종 검사

| 검사 | 실제 메서드 | PASS | FAIL | 종료 |
| --- | --- | ---: | ---: | ---: |
| after | Elements.Editor.CapsuleMagnetPolicyVerification.Run | 49166 | 0 | 0 |
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

최종 합계373376 PASS/0 FAIL·별도 Editor24종. Before/예상 RED/실패 fixture 진단은 합계에서 제외한다. 실제 기존23종 메서드를 EF-30 최종 실행 표와 대조했고, 과거 증거4159개와 결과·값41개 바이트 복원·보호1960개/승인된 두 생산 제한 변경만 허용·기존 작업 보존·새 GUID·diff check를 감사했다. 새 입력 오류 외 제품/회귀 검사 실패는 없으며, 기대값을 낮춘 항목은 없다.

## 남은 문제와 다음 단계

EF-31 완료 조건의 남은 문제는 없다. 기본 정책false이므로 현재 게임의 회수캡슐 밸런스는 같고, 테스트 메모리에서 허용한 반응만 새 경로를 통과한다. 다른 내구도형/발전기 등의 잔여 자석 종류 제한은 이번 범위에서 유지한다.

다음은 [EF-32 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-32-durable-magnet-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-32-durable-magnet-policy-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-32-command.md). 같은 내구도 피해 경로의Crate/Scrap/Appliance 세 종류를 한 책임으로 묶고 본체/칸 집계 차이는 각각 검증한다. 발전기의 충전/기본true 예외는 별도다. EF-32 구현은 시작하지 않았다.

플레이어/Addressables 빌드·재패킹·배포 팩/이미지 재생성·커밋·푸시·사용자 Unity 종료·씬 저장은 하지 않았다. Android/iOS 기기·IL2CPP·실제 Addressables 콘텐츠 빌드는 미검증이며 이 단계의 Editor 데이터 검사와 구분한다.

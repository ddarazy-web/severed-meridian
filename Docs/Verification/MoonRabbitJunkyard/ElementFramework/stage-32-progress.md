# EF-32 — 상자·고철·금속기둥 자석 인접 정책 연결 기록

상태: 완료. 새54307+기존373376=427683 PASS/0 FAIL. 최종25종 각각 별도 Editor 종료0. 원본·기존 작업·과거 출력 감사 통과.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-32-durable-magnet-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-32-durable-magnet-policy-goal.md) · [EF-31 결과](stage-31-progress.md).

## 보호 기준과 호출부

work/6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783와 EF-23~31 미커밋 작업을 유지한다. 초기 보호1962개, 기존 미커밋75개, 과거 증거4440개의 SHA256을 확보했다. hidden/no-ignore로 원본/GUID/enum·Android Addressables 상태/패키지 서명을 포함하고 두 생산 파일 원문과 기존 작업을 복사했다. 사용자 ServeredMeridian Editor는 실행 중이 아니며 NCloud_Unit_CV Editor/worker는 건드리지 않았다.

전환 전 Evaluate의 자석 전단은 ColorLock과 정책 허용Safe만 통과시키고, Query는 기존SourcePolicy.Allows 뒤에도 Crate/Scrap/Appliance 자석을 종류 조건으로 거절한다. 실제 Swap의 일반 블록 소비는PushAdjacent(MagnetAdjacent)로 인접 반응을 생성하고Evaluate→Query→공통 적용으로 이어진다. 호출부는Logs/ElementFramework/Stage32/call-sites.txt에 기록했다. Before 기준을 확보한 뒤 Evaluate의 내구도형 범위(Crate~Appliance, ColorLock 기존 예외 유지)에서 기존MagnetAdjacent를 조회하도록 연결하고, Query의 잔여 자석 종류 제한만 제거했다. Safe 미정의 원인 제한·기본false·발전기 외부 제한을 유지한다.

## 실제 RED

새 DurableMagnetPolicyVerification(+meta)을 먼저 작성했다. 각 종류의 저장 입력에서 두 유효 본체와 제거 미션2를 준비하고 런타임 상태를 먼저 구성했다. 테스트 메모리의 같은 ID에서MagnetAdjacent만true이며 다른 세bool 및 나머지 정의/프로필 원래 참조를 유지한다. 공개 등록 수정 API를 추가하지 않았다.

별도 Editor의Elements.Editor.DurableMagnetPolicyVerification.Red는18 PASS/3 FAIL·종료1이다. 첫 실패 후에도 계속해 세 종류를 각각 확인했다.

- Crate: 실제Evaluate None/0, 기대Damage/1.
- Scrap: 실제Evaluate None/0, 기대Damage/1.
- Appliance: 실제Evaluate None/0, 기대Damage/1.

세 종류 모두 조회 상태/비공개 문맥/입력/전역 난수 무변경과finally 정확한 원래 등록 참조 복원을 확인했다. 각 실제 입력/응답은red-connection-values.jsonl의3행에 기록했다. 문자열로 만든 실패가 아니라 실제 Response/Amount 조건의 실패다. 컴파일 오류는 없었다.

## Before→GREEN과 전체 동등성

전체 내구도Crate1~6/Scrap1~5/Appliance1~9,14조건×7원인=1960조회·실제 자석Swap40건·반복 소비3건과 EF-31 기존16139행을 준비했다. 초기Before50803 PASS/0 FAIL 뒤 다른 종류/비장애물 비교 입력을 보완했다. 최종Before51253 PASS/0 FAIL·종료0을 생산 수정 전에 확보했고 입력90개를 고정했다. fixture/기대값을 낮추거나 기존fixture를 수정한 항목은 없다.

GREEN54307 PASS/0 FAIL·종료0. 두 본체의 실제 내구도, 본체/칸/hit 기록, 중첩 범위와 같은칸 중복 거부, 제거 미션·효과/원인/내구도 전후·제거 후 후속 인접 중복 부재를 확인했다. 실제 반복3건은 첫 자석은BoardActionExecutor.Swap, 두 번째는 같은TurnEffectContext의PowerEffectResolution.ApplyWithColor 공통 실행 경로에서 실제 자석 발동/일반 블록 소비를 처리한다. 두 번째를 금지된 낙하 대기 중Swap으로 만들거나 강제ObstacleDamageRules.Apply하지 않았다. Appliance의 같은 네 칸이 서로 다른 두hit로 각1회 피해를 받으며 Crate/Scrap은 두 번째hit에도 본체별 턴1회다.

전후18142행/614480385바이트 전체 동일. SHA256:F0D30C5DFF747D5C6F6EF88C9D95F018A3355292B7615F6DCE1F45762F8C4F84. 상태/비공개 문맥/규칙·전역 난수/미션·예약/재선정·발전기·공급·전체 검증·MemoryPack188개 전체 바이트/ID/버전1/50구간·왕복 상태225행을 포함한다. 비대상 비교도 전후 동일 입력의 전체 응답 문자열을 비교했다.

허용/비대상 연결2414행: 세 같은ID3·경계280·실제Swap40·반복3·색/null120·누락15·다른 종류1365·비장애물588. 각 임시 등록의finally 원래 참조 복원을 확인했다. 의도적 차이: 유효 세 내구도형의SourcePolicy 누락은 이전 전단None/0 대신ID 포함 오류로 드러나며false 대체는 없다. 벽/거리/비활성/삭제 선행 거부는 유지한다.

보호1962개 중1960개 동일이며 두 생산 변경만 원본 전체 문자열로 복원해 정확히 비교했다. 기본 등록·프로필·enum·원본 에셋·집계/Apply/Remove·미션/발전기/드론/저장은 변경하지 않았다.

## 별도 Editor 최종 검사

| 검사 | 실제 메서드 | PASS | FAIL | 종료 |
| --- | --- | ---: | ---: | ---: |
| after | Elements.Editor.DurableMagnetPolicyVerification.Run | 54307 | 0 | 0 |
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

## 최종 보존 감사와 한계

Logs/ElementFramework/Stage32/audit.ps1가 실제 전체 증거를 읽어 통과했다. 원본1962개 중1960개 동일이며 변경된 두 생산 파일은 원문 복원 비교로 두 자석 제한만 달라졌음을 증명했다. 과거4440개 전체 해시 동일, 기존 출력43개는 백업·삭제·새 생성·증거 복사 후finally 전체 바이트 복원됐고 실행 시간 내 새 출력 생성도 확인했다. 기존 미커밋75개와 work/6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783를 유지했다. 무시된 Android Addressables 상태/패키지 서명·enum·원본/GUID·EF-05/09/11도 보호 목록으로 확인했다.

새 검사meta GUID는7d371bf1e9a742b89131029c11091f18, 중복0이다. Assets 추가는 검사와meta 두 파일뿐이다. 새 입력90개 모두 고정 해시 동일, 기본 정의 전후 전체 동일. source/test review는manual-review.md, 요구사항별 근거는requirement-evidence-map.md와 최종requirement-completion-audit.json에 기록한다. final-doc-audit.ps1가 요구사항8개·실제 실행 표25행·문서 링크·현재5/8개 완료 체크·다음5/8개 미착수 체크·전체 명령문2683자와 다음 구현 부재를 확인해 통과했다. 문서 갱신 후final-work-audit.ps1도75개 중64개 동일·승인된 두 소스/문서11개 갱신 외 손실 없음과 work/HEAD·Assets 추가2개·diff check를 확인했다. 과거 검사나 기대값을 낮춘 항목은 없다. 제품/회귀 실패는 없고 RED3건은 변경 전 실제 실패 증거다.

기기/Android·iOS/IL2CPP·실제 Addressables 콘텐츠 빌드는 미검증이며 이번 Editor 데이터 검사와 구분한다. 커밋/푸시/빌드/재패킹/배포 팩·이미지 재생성/사용자 Unity 종료/씬 저장은 하지 않았다.

## 남은 문제와 다음 단계

EF-32 실행·회귀·보존 완료 조건의 남은 문제는 없다. 기본MagnetAdjacent=false이므로 게임의 상자·고철·금속기둥 밸런스는 같다. 테스트 메모리에서true로 허용한 정책만 공통 자석 소비 경로를 통과한다. 유효 정의의 정책 누락ID 오류 노출 차이는 위에 명시했다. 발전기의 충전/기본true 외부 자석 예외와 다른 요소·공급·저장·드론 전환은 별도다.

다음은 [EF-33 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-33-reaction-behavior-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-33-reaction-behavior-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-33-command.md). 반응 행동 키·명시적 조회 등록표·실제 조회 선택을 함께 연결한다. 단순 키 추가만 별도 단계로 떼거나 여섯 종류를 반복 분할하지 않는다. 적용 행동·저장·UI 전환은 묶지 않는다. EF-33 구현은 시작하지 않았다.
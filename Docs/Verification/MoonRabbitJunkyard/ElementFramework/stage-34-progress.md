# EF-34 — 반응 행동 등록과 실제 적용 위임 연결 기록

결과: 명시적 등록의 Query/Apply 짝과 실제 적용 위임을 연결했다. 기존 정상 게임 결과와 정의 메타데이터는 그대로다. 변경 생산 파일은 ElementBehaviorRegistry/ObstacleDamageRules/PowerEffectResolution 3개, 추가 검사는 ElementReactionApplyVerification.cs와 meta다. 기존 C# 검사는 수정하지 않았다.

상태: 완료. 새 최종 Run과 기존26종을 각각 별도 Editor에서 확인했다. 합계534031 PASS/0 FAIL·실제27종 종료0, 기존59출력 복원 및 보호/전체 실행 감사 통과.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-34-reaction-apply-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-34-reaction-apply-goal.md).

## 보호와 조사

시작 기준 work/6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783. 보호 파일1970개, 기존 작업91개, 과거 증거5134개, 동일 입력37개를 Stage34/start.ps1과 JSON 명세에 기록했다. 생산 직접 Apply 호출은 PowerEffectResolution.ApplyCore의 충전/내구도 두 곳이다. 내부 적용 서명과 반환값 소비, 무효화/효과 기록 순서를 계획서에 기록했다.

## 검사 먼저 확보한 실패

ElementReactionApplyVerification.Red를 별도 Unity6000.3.10f1 Editor에서 실행했다. 실제 종료1, PASS36/FAIL6. Crate/Scrap/Safe/ColorLock/Generator는 실제 등록 적용0회 대 예상1회, Appliance는0회 대 예상2회로 실패했다. 앞선 실제 public Swap·로켓 발동·내구도/충전·미션·효과·원본/난수 검사는 통과했다. 따라서 단순 컴파일 실패나 강제 적용 검사가 아니라 기존 실제 실행이 등록 적용 경로를 우회한다는 증거다. red-results.txt, red-values.jsonl, red-connection-values.jsonl, Red-execution.json과 Red.log를 보존한다.

Before는 실제 종료0, PASS50838/FAIL0. 두 행동의 Query/Apply 짝과 실제 공통 효과 호출을 생산3파일에서 연결했다. 기존 Apply/Remove와 Query 전단/정책/미션/예약 원문, 발전기·정의·Evaluate 원문, 공통 효과의 정확한 두 호출 치환과 CRLF를 source-audit 12항목으로 확인했다.

첫 Run은 PASS50869/FAIL1이었다. 발전기 fixture에는 별도 연결 대상 상자도 있어 같은 로켓이 발전기와 상자를 각각 타격했다. 신규 검사의 “게임 전체에서 다른 행동0회” 가정이 잘못됐다. 관찰에 적용 본체 인덱스를 기록하여 대상 본체별 선택 행동/반대 행동0회와 전체 Damage/Charge 효과마다 적용1회를 모두 검사하도록 수정했다. 실제 기존 규칙·입력·내구도/미션/효과 기대값은 바꾸지 않았다. first-run-failed-results/values/connections/execution에 실패를 보존했다.

첫 보호 감사는 입력 명세의 파일명이 Stage34 상대경로라는 점을 잘못 읽어37개를 누락으로 보고했다. 상대경로를 바로잡은 재감사에서 보호1970·기존작업91·과거5134·입력37의 예상 밖 변경0을 확인했다. 이어 PowerShell5의 NativeCommandError 처리에서 기존 Git 줄바꿈 경고를 예외로 취급해 중단했다. Git 실제 종료값을 따로 검사하도록 감사 스크립트를 수정했으며 최종 감사는 다시 수행한다.

본체별 가정을 수정한 전체 Run은 실제 종료0, PASS53283/FAIL0으로 GREEN이다. 이어 같은ID 대체 키의 실제 public Swap을 두 방향으로 추가했다. 첫 추가본은 BoardCoordinate의 `==` 미지원 컴파일 오류가 있어 기존 Equals API로 수정했다. 다음 실행에서는 상자의 충전 임계값을0으로 가정한 새 fixture 기대가 실패했다. 실제 저장된 상자 입력에도 requiredCharge=3이 있으므로, 원래 입력의 임계값에 따라 충전/활성화를 판단하도록 신규 기대만 수정했다. 원본 입력/규칙을 수정하거나 적용을 강제하지 않았다. runs 하위 이전 실행 출력·컴파일 실패 로그와 실행 영수증을 보존했다.

최종 좁은 Quick은 실제 종료0, PASS2296/FAIL0. 두 키의 읽기 전용 Query/Apply 짝, Apply 누락/미지원/프로필 오류24건의 ID 오류·무변경, 기존 계약/조회 경계와 대체키 public Swap2건을 확인했다. 대체키 사례는 runtime/BoardActionExecutor 구성 후 정의를 같은ID/다른 행동/호환 프로필로 교체했다. 실제 키에 따른 응답/적용1회·내구도/충전/임계/제거/턴 기록과 원본/규칙·전역 난수를 확인하고 finally 정확한 원래 정의·등록 짝 참조를 복원했다.

새 최종 Run은 실제 종료0, PASS53295/FAIL0. 동일 입력/시드의 baseline/before/after 전체616394258바이트가 SHA256 53B9D59B4B742EAF2B021749FF00FF2A155E5741CE98FB4634159393A353912F로 모두 동일하다. 정의 snapshot 전후2756바이트도 SHA256 408B3C7DC00698AD84DDDA484AABC99DF2B6E8DFBAE4845BD1768A662B10902F로 동일하여 이번 단계의 정의 메타데이터 차이는 없다. new-run-hashes.json에 원본 해시와 크기를 기록했다. 실행 상태를 정규화하지 않았다.

EF-33 정확한26종 별도 Editor 순차 회귀를 완료했다. 모든 기존 주 출력47개와 부가 정의/연결12개, 조건부 실패 출력까지 사전 백업/삭제·새 시간/해시 확인·증거 복사·finally 복원한다. 캐시 입력은 조사된 기존 파일만 읽는다. 실행 중인 단일 셸 세션과 current-regression.json의 실제 batch Editor를 확인하며, 시간 초과만으로 재시작하지 않는다.

과거 EF-28~32의 보호된 부가 정의 snapshot은 EF-33 키 도입 전 형식이다. 현재 소스로 다시 생성한 원문에는 이미 구현된 ReactionBehavior 필드가 나타난다. metadata-audit는 오직 과거 정의 메타데이터 비교에서만 정확한 EF-33 키 토큰 차이를 분리하여, 그 밖의 전체 원문이 동일함을 확인한다. 새 원문/이전 원문/각 해시를 별도로 보존하고 과거 파일은 finally 이전 원문으로 복원한다. EF-34 자체 정의 전후에는 차이가 없으며, 실행/상태/팩 비교에는 이 처리를 적용하지 않는다. 전체6개 메타 snapshot을 metadata-audit -RequireAll로 확인했고 예상 밖 차이0이다.

최종 전체 회귀·보호/원본/GUID/과거 증거 감사와 다음 EF-35 문서 작성을 완료했다. 빌드/커밋/푸시/재패킹/에셋 재생성/사용자 Editor 종료/씬 저장은 수행하지 않았다.

기존21종은 각각 실제 종료0/FAIL0 및 이전 검사 수와 동일하게 끝났다. 다음 FixedObstacleVerification.Data 준비에서 실행기만 종료1로 중단됐다. 값 경로가 없는 해시테이블의 `.values`가 네이티브 Values 컬렉션으로 해석되어 가짜 경로를 검사한 오류다. 실제 원본 data-results는 존재하고, 이 단계에서는 백업만 진행했으며 삭제/새 실행은 시작하지 않았다. 선택 키를 인덱스로 읽도록 수정하고, 완료21종 영수증/결과를 검증한 뒤 나머지5종만 Resume한다. 첫 Resume 시도는 PowerShell5의 ConvertFrom-Json 배열을 @()로 한 번 더 감싼 오류로 실행 전 중단됐고, 배열을 직접 대입하는 기존 방식으로 수정했다. 게임 소스·C# 검사·기존 결과 기대값은 바꾸지 않았다. 최초 오류는 runner-guard-failure.json과 셸 실제 종료 기록에 보존한다.

## 요구사항별 현재 근거

| 요구사항 | 실제 근거 | 현재 판정 |
| --- | --- | --- |
| 현재 정의 키로 Query/Apply 한 등록 선택 | Registry의 공통 Get→RequireReactionBehavior→TryGetValue, 불변2짝; ApplyContract와 같은ID public Swap2건 | 통과 |
| 실제 게임 경로의 적용·소비·피해·충전·미션·효과 | 정상 public Swap40건, 실제 등록 적용70기록과 대체키2건; 상태/문맥/반환값 및 피격 효과마다1회 | 통과 |
| 기존 정의/부분 생성자/프로필 참조와 선행 거부 오류 순서 | 새 계약·조회 경계1008·조회 프로필 오류24·직접 적용 오류24·원인 거부6, EF-33.Run53053/0 | 통과 |
| 기존 알고리즘과 적용/제거/미션/연결/무효화 순서 | source-audit12항목: 원래 Apply/Remove·Query 전단 원문, GeneratorRules/정의/Evaluate 전체 해시, ApplyCore 정확한2치환 | 통과 |
| 전체 실행 상태/비공개 문맥/난수/예약·취소·재선정/공급/검증/팩 | new-values-audit 실제 종료0, 전체18182행·616394258바이트 동일, MemoryPack188·왕복상태225; 기존 EF-33 전체18142행/614480385바이트 접두 SHA256도 동일 | 통과 |
| 정의 메타데이터와 실행 비교를 구분 | 정의 전후 해시 동일, 실행 기록 정규화 없음; values-summary/new-run-hashes | 통과 |
| 새 Run과 기존 정확한26종 별도 Editor·종료0/FAIL0 | 새 Run53295/0, 기존26종480736/0·정확한 이전 검사 수 일치, 27종 전체534031/0 | 통과 |
| 주/부가 출력 백업·삭제·새 생성·증거 복사·finally 복원 | run-regression 사전 조사59출력+조건부실패파일, 전체59출력의 새 생성 시간/해시/정확한 복원 확인 | 통과 |
| 원본/GUID/enum/EF-05/09/11·무시 Addressables/패키지/WIP·과거 | 시작1970/91/5134/37 명세와 보호 감사 예상 밖 변경0; 종료 후 보호1970/기존작업91/과거5134/입력37 예상 밖 변경0·새 Assets2개만 허용 | 통과 |
| 완료 보고와 다음 한 단계 문서/명령문 | EF-35 계획/목표/전체 명령문 작성·다음 구현 없음 | 통과 |

## 별도 Editor 실제 최종 결과

| 검사 | 실제 메서드 | PASS | FAIL | 종료 |
| --- | --- | ---: | ---: | ---: |
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

## 최종 감사·제약·다음 단계

final-audit 실제 종료0: 합계534031/0·27종 별도 Editor, 기존 정확한26메서드/검사 수 일치, 주/부가59출력 새 생성 시간·해시·원본 복원, 전체18182행과188팩/225왕복상태, EF-33 전체18142행 접두 원문 해시, 정의 전후 동일 및 과거 메타 차이6개를 확인했다. protection-audit와 source-audit도 실제 종료0이다. 보호1970개 중 승인된 생산3개만 변경했고 기존작업91개와 과거5134개/입력37개에 예상 밖 변경0이다. 새 Assets/Packages/ProjectSettings 파일은 검사/메타2개뿐이며 새 GUID는 유일하다. work/HEAD와 enum·EF-05/09/11·원본/GUID·무시 Addressables/패키지 서명·기존 다른 작업을 보존했다. diff check 종료0이다.

미검증: 플레이어/Addressables 빌드와 실제 기기 화면 검사는 수행하지 않았다. 요청대로 Editor 컴파일과 실제 공개 교환을 사용하는 규칙 검사만 실행했다. 렌더/UI/비행 코드와 원본 에셋은 이번 변경 범위가 아니다. EF-34 요구사항의 남은 문제는 없다. 전체 요소 전환은 아직 끝나지 않았으며 내구도 Apply의 구형 종류 범위 판정은 다음 단계에서 다룬다.

다음 [EF-35 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-35-durability-apply-policy-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-35-durability-apply-policy-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-35-command.md)을 작성했다. 등록 내구도 조회와 적용이 같은 정의의 집계 정책을 소비하도록 연결하되 기존 직접 Apply는 호환 경계로 보존한다. 다음 단계 소스 구현/검사 추가는 시작하지 않았다. 사용자 제약에 따라 skill의 기본 워크트리/커밋/에이전트 흐름을 사용하지 않았고, 프로젝트 지정 문서 경로와 Logs/Stage34 증거를 유지했다.

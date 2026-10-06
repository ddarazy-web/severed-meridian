# 큰 구간 1단계 진행 기록

계획: [요소 정의와 공통 규칙](../../../Planning/MoonRabbitJunkyard/ElementFramework/phase-01-common-element-rules-plan.md).

상태: 진행 중. 완료 조건은 아직 충족하지 않았다.

## 시작 기준

- `work`, HEAD `6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783`.
- 기존 WIP 목록·소스 사본·1146개 코드/패키지/설정 해시: `Logs/ElementFramework/Phase01/initial-*`, `source-before`.
- 프로세스 확인 결과 ServeredMeridian 사용자 Editor는 열려 있지 않다. 다른 프로젝트 Editor에는 조작하지 않는다.
- 사용자 지시에 따라 기존 작업 경로에서 직접 구현하며 에이전트/커밋/푸시/빌드는 사용하지 않는다.

## 판단과 작업 순서

- A의 덮개/먼지 프로필은 실제 층 상태에 적용하고 장애물 내구도 프로필을 복제하지 않는다. 기존 장애물의 미션 프로필 거절 계약을 유지하도록 층용 행동/미션 설정을 별도로 둔다.
- B는 A의 덮개 정의를 사용한다. C는 기존 장애물 정의와 미션 계약을 재사용한다. D는 A~C 최종 상태에서 전체 검증한다.
- 새 검사 `CommonLayerRulesVerification`는 기존 층 검사 내부 진입점과 실제 효과 실행을 재사용하고 기존 증거 경로에는 쓰지 않는다.

## A 중간 검증

- 변경 전 Before 실제 종료0. 정의 조회 부재 RED 실제 종료1/FAIL2 확인.
- 정의 피해량2/미션 변경/최대2를 실제 소비하는 추가 RED에서 예측2건·먼지 편집1건 실패를 확인했다.
- 연결 후 Run 실제 종료0, PASS168/FAIL0. 실제 상태/문맥/효과30개 기록이 Before와 동일하다.
- 새 검사 작성 중 MissionContribution 속성명 오타로 발생한 컴파일 실패는 별도 runs에 보존했고 기능 RED로 인정하지 않았다.
- A는 데이터 다른 ID 실행과 최종 전체 회귀가 아직 남아 있다. 전체 단계 완료가 아니다.


## B/C 구현과 검증 진행

- B 고정 입력 Before/RED/GREEN을 확보했다. 초기 Capture는 편집 시 생성되는 본체 GUID가 달라 비교에 실패했으므로 입력 JSON을 고정했다. 초기 실패와 입력 고정 전 결과는 lifecycle-unfrozen-fixture 및 runs에 보존했다. 해당 fixture 실패를 기능 RED로 계산하지 않는다.
- 고정 입력 RED는 기존 상태 비교 PASS이며 턴 행동 등록/충전량 소비의 실제 실패를 보였다. GREEN은 실제 종료0이며 번식/발전기15개 상태·문맥·효과 기록이 동일하다. 비기본 충전량2의 예측·적용·턴 중복 억제·활성화, 번식 초기 내구도2/미션 목표 증가도 통과했다.
- C 공급9종의27개 상태/기록을 고정 입력으로 확보했다. 정의 조회 부재 RED 실제 종료1, 정의 기반 생성 후 GREEN 실제 종료0. 선택 순서/난수/소진 커서/기존 낙하 검사를 유지한다. 최종 검사에 레시피 변경의 실제 보드 적용과 필수 프로필 누락 거절을 추가했다.
- 다른 ID 내구도 정의의 색 조건과 제거 미션이 기존 body.Kind로 재조회되는 문제를 RED로 확인했다. 제공된 정의를 공통 적용/제거까지 전달해 실제 종료0을 확인했다. 다른 ID의 공급 정의도 같은 등록 행동을 사용한다.
- 층 정의의 미션 변경이 최초 가용 수량에 반영되지 않는 RED를 확인하고 LevelMissionRules/초기 동적 목표를 연결했다. 관련 GREEN 실제 종료0.
- ElementCatalogVerification의 불변 필드 타입 화이트리스트에 새3개 불변 프로필 타입을 추가했다. 기존 Count6·동작 기대값·수량은 유지한다. 새 검사에서 해당3개 클래스의 sealed/읽기 전용 속성/readonly 필드도 검사한다.

## 최종 검증 진행

- 실행 스크립트: Logs/ElementFramework/Phase01/run-final.ps1.
- 신규4개 실제 검사 후 EF-37 verified-results.json의 기존30개 정확한 메서드를 독립 Editor에서 실행한다. 기존 출력 백업/실제 신규 결과 보존/finally 원문 복원 절차를 사용한다.
- 실행 세션 33548을 시작했다. 재개 시 해당 세션 또는 runs/current-regression.json의 실제 PID 생존 여부를 먼저 확인하며 관찰 시간 초과만으로 재실행하지 않는다.
- 전체 회귀/팩 비교/보호 감사/다음2단계 문서는 아직 완료하지 않았다. 검사 중에는 소스를 추가 변경하지 않는다.

## 전체 회귀에서 발견한 원복 경계

- 첫 회귀의 DamageRecordPolicyVerification.DirectChecks는 미지원 종류 -1 등의 구형 직접 Remove를 호출한다. 새 Remove가 LegacyElementDefinitions.Get을 먼저 호출하여 기존 제거/미션 동작 대신 예외를 내는 원인을 확인했다.
- 기존 결과 기대값은 변경하지 않았다. 별도15개 경계 사례를 추가해 미지원 종류/발전기의 직접 제거를 RED로 재현했다. 구형 Remove 호환 경계는 기존 종류 판정을 유지하고, 등록된 내구도 행동의 제거는 전달받은 정의를 사용하도록 공통 RemoveCore를 분리했다.
- 실패한 첫 회귀는 요구 출력 생성 이전에 중단되어 전체 suite PASS나 종료 코드 영수증으로 인정하지 않는다. 원문 실패 결과·Editor 로그·원문 출력 복원 증거는 Phase01 루트에 보존했다.
- 새 회귀는 별도 Regression 폴더에서 실행하고 Unity 종료 영수증을 출력 존재 검사 전에 기록한다. 실패한 실행을 덮어쓰거나 관찰 시간 초과만으로 재시작하지 않는다.

## 현재 확인한 증거

- 최종 신규4개: layers172 / lifecycle400 / supply226 / extension19, 합817 PASS/0 FAIL, 각각 실제 Editor 종료0. 결과는 verified-new.json과 각 runs 실행 영수증에 기록했다.
- 보정 후 첫 기존 검사 ScrapMaintainDurabilityVerification.Run: 50950 PASS/0 FAIL, 실제 종료0. 새로 생성한 전체18182행 원문 SHA256은 53B9D59B4B742EAF2B021749FF00FF2A155E5741CE98FB4634159393A353912F로 EF-37 원문과 동일하다. 다른 기존29개는 아직 종료하지 않았다.
- EF-37 자산/메타/패키지/설정1489개 참조 해시와 차이0을 확인했다. 시작 시 코드/설정1146개 보호 기록과는 별개로 감사한다.
- 실패한 재호출에서 부모 run-final의 경로가 옛 루트 회귀 스크립트를 선택하여 기존 증거 충돌 보호가 중단시켰다. 출력 삭제·Editor 실행 전에 중단되었다. 경로를 보정하고 새 검사를 불필요하게 반복하지 않고 Regression 스크립트만 실행했다.
- 현재 살아 있는 회귀 실행 세션은28561이며 Regression/current-regression.json에서 실제 현재 PID와 메서드를 확인한다. 이전 세션33548/53256은 종료되었다.

## 책임과 확장 경계

- Elements/Data: 불변 Layer/Turn/Supply 프로필. Elements/Runtime: 같은 카탈로그와 각 수명주기 키의 명시적 행동 등록표.
- Obstacles의 규칙: 본체/덮개/먼지 상태 적용, 턴 이력, 충전/연결 해제. 등록된 내구도 적용은 전달된 정의를 제거까지 유지한다. 구형 직접 Remove는 기존 경계를 보존한다.
- BoardFlow: 생성구 선택·목록 소진·커서·낙하를 소유하고 선택된 생성 작업만 공급 등록표로 위임한다. PuzzlePlay 상태는 본체 목록과 인스턴스 ID를 소유한다.
- Missions: 정의의 제거/층 미션 설정으로 예측·적용·최초 가용 수량을 연결한다. 기존 공개 미션 종류와 입력 형식은 유지한다.
- 다른 ID 정의의 등록 행동 재사용은 테스트 카탈로그에서 입증했다. 실제 새 ID 배치를 편집하고 저장하는 기능은 구간3이며, 현재 레벨 DTO의 enum 연결을 전면 제거하지 않는다.

## 회귀 재개 2026-10-05 — 발전기 내구도 교체 경계

- 세션28561은 실제 종료1. Stage35에서 발전기 정의를 내구도 행동으로 교체하는 공개 Swap 사례가 제거 미션 프로필 부재로 실패했다. 원문 결과·Unity 로그·실제 종료 영수증·출력 원문 복원 증거를 Regression에 보존했다. 출력 부재 자체가 원인은 아니었다.
- 등록 정의의 제거 미션은 계속 소비하되, 구형 발전기에 제거 미션이 없는 경우 기존 미션 미완료 계약을 유지했다. 해당 기존 공개 Swap 두 사례를 신규 확장 검사에서도 직접 실행한다. 기대값은 수정하지 않았다.
- 보정 후 최종 신규4개 실제 종료0: layers172 / lifecycle400 / supply226 / extension21, 합819 PASS/0 FAIL.
- 소스498개의 최종 해시를 final-source-hashes.json에 동결했다. 시작1146개 파일과 비교하여 변경된 기존 파일18개 모두 계획 범위이며 예상 외 변경0이다. EF37 자산·메타·패키지·설정1489개 원문과 차이0이다.
- 전체30개는 RegressionFinal의 독립 새 증거 경로에서 다시 실행한다. 현재 실행 세션21799. 이전 실패 증거는 덮어쓰지 않는다. 현재 상태의 근거는 continuation-state.json과 실제 세션/Unity 프로세스를 함께 확인한다.
- 전체 회귀 종료 후 실행할 감사: audit-final-evidence.ps1(30개 실제 종료/원문 출력 복원/전체18182행·188팩·225상태/동결 소스), audit-definition-metadata.ps1(10개 정의 스냅샷의 의도적 추가 필드), audit-protection-final.ps1(기존 WIP·과거 증거·새 GUID·브랜치/HEAD).

- 보정된 최종 소스의 첫 기존 검사도 실제 종료0/50950 PASS/0 FAIL이다. 새 원문18182행을 직접 읽어 기존 모든 기록 종류별 수량, 전체188팩,225팩 상태를 재확인했다. 전체 SHA256은 EF37 원문과 같으며 정규화하지 않았다(current-raw-coverage.json). 전체30개의 성공을 대체하는 근거로 사용하지 않는다.
- 다음2구간 정식 문서는 아직 작성하지 않았다. 읽기 전용 현재 소스 조사만 phase-02-source-notes.md에 보존했다. 특히 완료된 final 상태로 비행 중 표적 유효성을 판정하면 오판하므로, 예약/재탐색/효과 기록과 시각적 시간표를 함께 계획해야 한다.

## 보정 후 실제 실패 검사 통과 2026-10-06

- ElementDurabilityApplyPolicyVerification.Run도 실제 종료0/50875 PASS/0 FAIL. 앞서 실패한 발전기 내구도 교체 경계를 포함하며 기존 기대값은 그대로다. 최종 기존 검사는 현재3/30종 통과했다(50950,51155,50875).
- 해당3종의 정의 스냅샷은 Layer/Supply/Turn=null 추가와 발전기 ChargePerHit=1 추가만 차이가 나며 기존6종 값은 동일하다. 실제 상태/팩 원문에는 어떠한 정규화도 적용하지 않았다(current-metadata-audit.json). 전체10종 메타데이터 감사는 아직 남아 있다.
- 요구사항별 현재 증거와 미완료 항목 및 남긴 종류 분기의 근거를 phase-01-requirement-audit.md에 작성했다. 전체 완료 판정은 아직 하지 않는다.

- 최종 회귀의 완료된3종 출력은 실제 원문 해시로 복원됐고 각 실제 종료 영수증/결과 수량/FAIL0을 재확인했다(current-receipt-restoration-audit.json). 동결한498개 소스와 차이0이다.
- 추가한 소스10개/메타10개를 시작 목록과 대조하고 새 GUID10개가 전체 Assets 메타 목록에서 각각1회 존재함을 확인했다(current-guid-audit.json). 과거 증거6941개 전부의 최종 감사는 실행 중인 기존 출력이 모두 복원된 뒤 수행한다.

- ElementReactionApplyVerification.Run 실제 종료0/53295 PASS/0 FAIL. 최종 회귀4/30종 통과. 같은 실행 세션21799를 유지한다.

- ElementReactionBehaviorVerification.Run 실제 종료0/53053 PASS/0 FAIL, 최종5/30종 통과. 이전 반응 적용의 출력4개 원문 해시 복원도 확인했다(reaction-apply-restoration-audit.json). 통합 가이드/설계/현재 실행문은 미착수 대신 최종 검증 중 상태로 맞췄다. 다음2단계는 여전히 미착수이며 정식 인계 문서 작성 전이다.

- DurableMagnetPolicyVerification.Run 실제 종료0/54307 PASS/0 FAIL, 최종6/30종 통과. 전체 실행 목록은 EF37의 서로 다른30개 메서드와 정확히 동일하다(method-scope-audit.json). 현재1단계 문서의 상대 링크9개도 모두 존재한다(current-doc-link-audit.json).

- CapsuleMagnetPolicyVerification.Run 실제 종료0/49166 PASS/0 FAIL, 최종7/30종 통과. 복원된 해당4개 출력은 fsutil hardlink list로 각각 독립 파일임을 확인했다(capsule-magnet-independence-audit.json). 전체30종 종료 후 audit-output-independence.ps1로 모든 필수 원본 출력의 물리적 독립성/해시를 감사한다. 불변 증거끼리의 동일 바이트 공유와 다시 쓰이는 원본 출력은 구분한다.

- CapsuleAdjacentPolicyVerification.Run 실제 종료0/48386 PASS/0 FAIL, 최종8/30종 통과. 완료된7종 정의 스냅샷 대조에서 기존6종 수치·정책 유지, 이번 추가 Layer/Supply/Turn=null·ChargePerHit=1 및 이전 EF33 반응 키 추가 외 예상 밖 차이0을 확인했다(current-metadata-audit.json).

- InitialMissionSupplyVerification.Run 실제 종료0/47201 PASS/0 FAIL. 최종9/30종 통과. 동일 세션21799에서 다음 기존 검사를 진행하며 소스 변경은 없다.

- RemovalMissionProfileVerification.Run 실제 종료0/43431 PASS/0 FAIL, 최종10/30종 통과.
- 정의 메타데이터 전체10개 감사 통과(final-metadata-audit.json). 각 원본 스냅샷은 기존 종류의 서로 다른 부분집합1/3/5/6종을 담는다. 새 필드3개와 기본 충전량, 이전 EF33 반응 키 추가를 정의 메타데이터에서만 구분한 후 원본 전체와 정확히 동일함을 확인했다. 실제 실행/팩 결과는 정규화하지 않는다.
- 초기 감사 스크립트가 모든 스냅샷을6종으로 가정하여 durable-magnet에서 실패했다. 감사용 가정 오류이며 생산 소스/기존 검사 기대값은 수정하지 않았다. 초기 스크립트와 실제 관측 범위를 audit-definition-metadata-initial.ps1 및 metadata-audit-fixture-failure.json에 보존하고 원본별 범위를 정확히 대조하도록 보정했다.

- DamageRecordPolicyVerification.Run 실제 종료0/42279 PASS/0 FAIL, 최종11/30종 통과. 처음 발견했던 미지원 종류 직접 Remove 경계를 포함한 기존 검사도 최종 소스로 통과했다.

- ReservedDamagePolicyVerification.Run 실제 종료0/7793 PASS/0 FAIL, 최종12/30종 통과. 이어지는 DamageAggregationPolicyVerification.Run은 기존75264 PASS 범위의 큰 검사이며 실제 PID104004/세션21799가 살아 있음을 확인했다. 관찰 대기 중이며 시간 경과를 종료나 실패로 간주하지 않는다.

- DamageAggregationPolicyVerification.Run 실제 종료0/75264 PASS/0 FAIL, 최종13/30종 통과. 큰 집계 검사의 이전 실제 실행도 약8분36초였으며 이번에는 프로세스와 같은 세션을 관찰하여 정상 종료를 확인했다. 시간 경과로 중복 실행하지 않았다.

## 검사용 오버로드 선택 보정과 최종 재실행

- 직전 실행은22종 통과 후 CratePlacementVerification.Run에서 실제 종료1/61 PASS/1 FAIL로 종료했다. 배치/실제 피해/미션/저장 바이트 비교는 통과했으나 ProfileChecks의 GetMethod("Get")가 새 필수 CoverKind 오버로드와 충돌해 AmbiguousMatchException이 발생했다. 기대75개 검사를 완료한 것으로 처리하지 않는다.
- 검사 코드 한 줄만 GetMethod("Get", new[] {typeof(ObstacleKind)})로 보정했다. 실제 런타임 소스와 기존 검사 기대값은 변경하지 않았다. 동결된 전체498개와 비교해 해당 Editor 검사1개만 변경됐음을 fixture-signature-fix-audit.json에 기록했다. 기존 변경 파일은17개 생산 소스+2개 검사,19개다.
- 목표의 최종 소스 상태 검증을 유지하기 위해 이전22개 통과 결과를 보존하고 신규4개 및 기존30개 전부를 다시 실행한다. 이전 실패/종료/출력 복원 기록은 RegressionFinal에 남기고 새 증거는 RegressionFinal2에서 생성한다. 현재 세션61910, 이전21799는 실제 종료1이다. 시간 초과로 재실행한 것이 아니다.
- 최종 감사 스크립트3개도 RegressionFinal2를 참조하도록 맞췄다. 이전 메타데이터 감사 결과는 생산 소스 값이 그대로임을 보이지만 새 최종 실행의 감사 완료로 대체하지 않는다. 소스498개 최종 해시도 보정 후 다시 동결했다.

- 검사 서명 보정 후 신규4개 실제 종료0/819 PASS/0 FAIL. 새 최종 회귀 첫 ScrapMaintainDurabilityVerification.Run 실제 종료0/50950 PASS/0 FAIL. 현재 기준은 RegressionFinal2의1/30종이며 이전22종의 통과를 새30종 완료로 재사용하지 않는다. 동일 세션61910을 유지한다.

- 새 최종 회귀의 ScrapSupplyMissionPolicyVerification.Run 실제 종료0/51155 PASS/0 FAIL,2/30종 통과. 검사 서명 보정 후 새로 생성한 전체 원문도 EF37 원문과 SHA256/바이트 길이가 동일하다(fixture-corrected-raw-byte-audit.json). 행별 범위와 전체30종 감사는 최종 종료 후 수행한다.

- 검사 서명 보정 후 ElementDurabilityApplyPolicyVerification.Run도 실제 종료0/50875 PASS/0 FAIL, 새 최종3/30종 통과. 같은 세션61910의 다음 실행을 유지한다.

- 보정 후 ElementReactionApplyVerification.Run 실제 종료0/53295 PASS/0 FAIL, 새 최종4/30종 통과. 동일 실행을 계속 관찰한다.

- 보정 후 ElementReactionBehaviorVerification.Run 실제 종료0/53053 PASS/0 FAIL, 새 최종5/30종 통과. 동일 세션61910을 유지한다.

- 보정 후 DurableMagnetPolicyVerification.Run 실제 종료0/54307 PASS/0 FAIL, 새 최종6/30종 통과. 동결498개 소스 차이0, 시작1146개 중 변경19개 모두 허용 범위/예상 밖 차이0을 재확인했다(fixture-corrected-source-audit.json).

- 보정 후 CapsuleMagnetPolicyVerification.Run 실제 종료0/49166 PASS/0 FAIL, 새 최종7/30종 통과. 동일 세션61910을 유지한다.

- 보정 후 CapsuleAdjacentPolicyVerification.Run 실제 종료0/48386 PASS/0 FAIL, 새 최종8/30종 통과. 동일 실행을 유지한다.

- 보정 후 InitialMissionSupplyVerification.Run 실제 종료0/47201 PASS/0 FAIL, 새 최종9/30종 통과. 동일 세션61910을 유지한다.

- 보정 후 RemovalMissionProfileVerification.Run 실제 종료0/43431 PASS/0 FAIL, 새 최종10/30종 통과. RegressionFinal2의 정의 메타데이터10개도 감사 스크립트로 실제 재대조하여 통과했다(final-metadata-audit.json). 기존 부분집합 범위와 모든 원본 값은 보존되고 의도적 추가 필드만 차이가 난다.

- 보정 후 DamageRecordPolicyVerification.Run 실제 종료0/42279 PASS/0 FAIL, 새 최종11/30종 통과. 동일 실행을 유지한다.

- 보정 후 ReservedDamagePolicyVerification.Run 실제 종료0/7793 PASS/0 FAIL, 새 최종12/30종 통과. 집계 검사 프로세스 PID60240이 실제 실행 중임을 확인하고 동일 세션61910을 유지한다.

- 보정 후 DamageAggregationPolicyVerification.Run 실제 종료0/75264 PASS/0 FAIL, 새 최종13/30종 통과. 이전 실행과 동일하게 큰 집계 검사를 동일 세션61910에서 정상 완료했으며 다음 검사를 계속 실행한다.

- 보정 후 ColorMatchPolicyVerification.Run 실제 종료0/20861 PASS/0 FAIL, 새 최종14/30종 통과. GeneratorReactionPolicyVerification.Run을 동일 세션61910에서 실행 중이다.

- 보정 후 GeneratorReactionPolicyVerification.Run 실제 종료0/24489 PASS/0 FAIL, 새 최종15/30종 통과. 동일 세션61910의 다음 검사로 진행했다.

- 보정 후 ApplianceDamagePolicyVerification.Run 5085、ColorLockDamagePolicyVerification.Run 4452、CapsuleDamagePolicyVerification.Run 592、ScrapDamagePolicyVerification.Run 960 PASS를 각각 실제 종료0/0 FAIL로 확인했다. 새 최종19/30종 통과이며 같은 세션61910을 유지한다.

- 보정 후 상자 피해696, 발전기 배치144, 내구도형 배치210, 상자 배치75 PASS를 각각 실제 종료0/0 FAIL로 확인했다. 새 최종23/30종 통과. 직전 실패했던 상자 오버로드 검사도 기대75항목 전체 통과했으며 생산 소스나 기대값 변경 없이 검사 서명 선택 보정만으로 해결됨을 확인했다.

## 큰 구간 1단계 최종 완료 보고 — 2026-10-06

- 신규4종172+400+226+21=819 PASS/0 FAIL, 기존30종687011 PASS/0 FAIL. 총34종687830 PASS이며 모든 실제 종료 코드0이다. 검사 서명 보정 후 최종 소스로 새 실행한 RegressionFinal2만 완료 근거로 사용했다.
- 전체18182행/188개 MemoryPack/225개 팩 상태,616394258바이트가 EF37 원문과 완전히 같다. SHA256은53B9D59B4B742EAF2B021749FF00FF2A155E5741CE98FB4634159393A353912F이다. 실행 결과의 정규화는 없다.
- 정의 메타데이터10개는 기존 원문 값이 보존됐고 새 null Layer/Turn/Supply 및 기본 ChargePerHit1만 의도적으로 추가됐다. 과거 EF33 키 유무도 따로 확인했다.
- 기존 출력75개는 원문 해시로 복원됐고 보존 증거와 물리적으로 독립이다. 과거6941개 증거 차이0, 시작 보호1146개 중 기존 소스19개(생산17/검사2)만 허용된 변경, 예상 밖 변경0이다. 신규10개 소스와 meta10개, 신규 GUID10개 유일성을 확인했다.
- work/HEAD6b41ce45c3b2f2e4c5d42c88024c2258ecd0b783 유지. 에셋/meta/패키지/설정1489개 차이0. 빌드·Addressables 빌드·팩 재생성·이미지·커밋·푸시·사용자 Editor 종료·씬 저장은 하지 않았다.
- A: Web/Mold/Dust 정의와 실제 배치/판정/적용/제거/미션 연결. B: 곰팡이 턴 종료와 발전기 충전량의 정의 소비. C: 실제 공급9종의 공통 생성 등록과 제거 미션/색 조건 소비. D: 다른 ID 내구도/덮개/공급의 실제 재사용과 통합 보존 검증을 완료했다.
- 상태/턴 기록/후보 순서/규칙 난수는 기존 실행기가 소유한다. Layer/Turn/Supply 행동 선택은 등록 키 직접 조회다. 같은 행동에 종류별 새 실행 함수를 복사하지 않는다.
- 미지원 종류의 구형 직접 Remove 기본 계약, 발전기 제거 미션 없는 구형 내구도 교체 계약은 유지했다. 기존 enum/DTO/제작 UI를 임의 ID 저장으로 전환한 것으로 보고하지 않는다. 저장/제작 도구는 구간3, 표현/리소스는 구간4 범위다.
- 이전 실패들은 별도 증거로 보존했다. 실제 회귀2건은 구형 Remove 경계와 발전기 no-mission 계약을 최소 수정했고, 상자 fixture는 명시적 Get(ObstacleKind) 선택 한 줄만 수정했다. 기대 검사 수량/기존 실행 원문을 약화하지 않았다.
- 플랫폼 빌드나 기기 검사는 이번 범위에서 수행하지 않았다. 런타임 데이터와 Editor 검사로 범위 내 논리 동작을 검증했으며 드론 화면 변경은 이번에 구현하지 않았다.

최종 직접 근거는 Logs/ElementFramework/Phase01의 final-runtime-audit.json, final-metadata-audit.json, final-output-independence-audit.json, final-protection-audit.json, final-asset-audit.json과 verified-new.json, RegressionFinal2의 실제 terminal/results/output-lifecycle 원문이다.

요구사항별 연결과 보존한 종류 분기는 [최종 감사](phase-01-requirement-audit.md)에 기록했다.

다음 큰 구간2: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/phase-02-drone-target-flight-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/phase-02-drone-target-flight-goal.md) · [전체 복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/phase-02-command.md). 구간2 구현은 미착수다.

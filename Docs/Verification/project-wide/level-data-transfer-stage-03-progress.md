# SDD ledger — plan: Docs/Planning/project-wide/level-data-transfer-stage-03-plan.md

2026-10-17 · 완료

- 이전 턴: 사용자 완료 응답 확인만 요청하여 구현 진전 없음. 현재 작업 트리로 미착수 상태 재확인 후 재개.
- Ruling: 기존 work 작업 트리와 이 영구 기록을 사용한다. 커밋 및 로그 삭제 금지에 따라 스킬의 커밋/임시 기록 삭제 절차는 적용하지 않는다. 비용: 기존 변경과 이번 변경을 파일별로 구분하여 검토해야 한다.
- Pre-flight: 작업 1의 기존 CreateBytes 결과를 작업 2의 Publication/Registration이 사용한다. 작업 3은 동일한 generation manifest를 읽어 상태를 계산한다. 작업 4는 동일 트랜잭션의 실패 복구, 작업 5는 기존 런타임 로더를 검사한다.
- 작업 1/3: 현재 1~15 및 경계 번호 변환, 미생성/최신/원본 변경/출력 손상 상태 검사 준비.
- 작업 1/3 검증: `ImportedContentPackVerification.Run` RED `20261017-014750-668-Run.log`에서 20개 레벨 왕복은 통과, 상태 판독기 부재로 실패. `ContentPackStatus` 추가 후 GREEN `20261017-014927-012-Run.log`, Unity exit 0. 미생성/최신/원본 변경/손상 검사 통과. 실제 출력은 격리 Logs 폴더에만 기록.
- 작업 2/3 UI: `RunUI`에서 변환 버튼과 상태 표시 부재 검사 실행 중. 전체 완료가 아니며 등록/복구/게임 비교/최종 리뷰가 남아 있음.
- 작업 2/3 UI RED `20261017-015117-033-Run.log`: 변환 버튼 없음. ContentImportWindow에 변환 호출·원본 변경 감지·생성 상태/해시/시간/경로 표시·복구 결과 안내 추가 후 GREEN `20261017-015242-861-Run.log` exit 0. 이 검사는 창 요소 생성만 확인하며 실제 클릭 변환, 시각 검수, 등록/복구 성공을 증명하지 않는다.
- 이어서 할 일: 변환 클릭을 격리 설정으로 검사하고 실제 Addressables API의 등록 축소/실패 복구를 검증. 현재 원본 고정 검사는 UI 내부 콜백에 있으므로 동시 변경/취소 실패 테스트 필요. 상태의 Current는 파일 검증일 뿐 주소 등록 검증은 아직 없음.
- 테스트에 추가한 panel 연결 및 RecoveryRequired assertion은 마지막 GREEN 이후 변경되어 재실행 필요. 기존 테스트 Run은 변환만 약 90초 소요하며 제품 빌드 없음.
- 마지막 수정 파일: ContentPackStatus.cs(+meta), ContentImportWindow.cs, ImportedContentPackVerification.cs(+meta), 본 기록. 기존 원본/리소스/등록 파일은 수정하지 않음.
- 작업 2/4: ImportedPackRegistrationVerification 추가. 원래 팩/등록 파일 전체를 Logs의 originals.json에 보관하고 finally에서 복원하며 실제 API로 큰 구간→작은 구간→등록 실패→선택 변경→UI 클릭을 검사한다.
- RED `20261017-015509-507-Run.log`: 공통 선택 고정 진입점 부재. JsonPackRegistration.PublishSelected로 UI 내부 원본 고정 코드를 이동.
- 실제 회귀 RED `20261017-015607-582-Run.log`: 등록 실패 시 디스크 바이트는 복원되나 FindAssetEntry가 실패한 세대의 엔트리를 반환. Addressables 패키지 코드 확인 결과 비직렬화 hash/cache가 재수입으로 초기화되지 않음. 복원 후 각 그룹 SetDirty(BatchModification, ..., false, false)로 저장 없이 조회 해시 무효화.
- 중간 검사 `20261017-015818-861-Run.log`는 새 플레이 검사에서 internal API 직접 호출로 컴파일 실패. 대표 교환 튜토리얼로 검사 범위를 명시하여 수정. 컴파일 실패 중 생성된 불완전 폴더 메타는 삭제하지 않고 Logs/LevelDataTransferStage03/incomplete-folder-meta-20261017-0159에 이동한 뒤 기존 연결 해제 절차를 실행.
- 현재 재검사 `20261017-015921-548-Run.log`: 실제 등록 축소, 파일 보존, 등록 실패 복원, 메모리 조회 복원, 원본 선택 변경 롤백까지 PASS 확인. UI 클릭 및 최종 원본 복원 결과를 기다리는 중.
- 신규 ImportedPackPlayVerification은 대표 레벨 1/3/7에 대해 production LevelPackLoader→시작 보드/미션→튜토리얼 완료→일반 교환/공급 결과를 JSON과 비교하며 전체 제품 프로필을 읽기 전용 감사한다. 아직 실행 전.
- 작업 2/4 GREEN: `20261017-015921-548-Run.log` exit 0, `registration-20261017-015940-320/result.txt` PASS 12 / FAIL 0. UI 실제 클릭 완료/중복 실행 방지 및 원본 복원까지 통과. Assets/Data, Assets/AddressableAssetsData, ProjectSettings git 변경 없음.
- 작업 5 GREEN: `20261017-020131-504-Run.log` exit 0. `play-20261017-020154-347/result.txt`: 대표 레벨 1·3·7의 실제 런타임 로더 바이트 입력, 튜토리얼 완료/제어 해제, 일반 교환 및 보드·미션·공급 결과 일치. `product-audit.txt`: Android Game, iOS Game, Windows Steam Game, Windows/Android Level Editor 5개 감사 통과. 제품 빌드/실기기 실행은 범위 밖으로 미실행.
- 추가 회귀 실행 중(session 30691): ImportedContentPackVerification.Run(취소/복구 실패 후 재복구 포함) → RunUI(panel 포함) → GenerationLoaderVerification.Run → JsonPackPublicationVerification.Run. 첫 로그 `20261017-020239-184-Run.log`. 마지막 최종 리뷰 및 문서 완료 감사는 아직 남아 있음.
- 추가 회귀 모두 GREEN: `20261017-020239-184-Run.log`(변환/상태/취소/복구 실패 후 재복구), `20261017-020436-937-Run.log`(UI panel), `20261017-020512-429-Run.log`(runtime generation loader), `20261017-020541-739-Run.log`(publication 원본 동시변경/중단 복구). 각 Unity exit 0, 연결 해제. 작업 묶음 1~5 구현·자동 검증 완료, 최종 독립 검토 및 완료 감사 남음.
- 실제 UI 확인: 검사용 Unity PID 610312에서 ContentImportWindow.Open으로 창을 열고 computer-use로 생성 정보 foldout을 펼침. 원본/생성 해시·15레벨·갱신 필요·출력 경로 모두 표시 확인. 검사 종료 후 직접 연 Unity만 종료. UI에서 실제 원본 변환은 누르지 않음.
- Final review: fresh gpt-6-astra `/root/transfer_stage03_review`. Important 2건 채택: (1) 원본 오류가 pending 복구 및 복구 필요 표시를 막음, (2) 취소/복구 검사 입력이 baseline과 같아서 실제 바이트 변경을 입증하지 못함. 한 번의 RED→GREEN 수정 패스로 해결 예정. 재리뷰하지 않음.
- Final: minor (deferred): 변환 UI 취소는 변환 시작 전 한 번이며 변환 중 사용자 취소 버튼은 제공하지 않음. 트랜잭션 취소 검증과 UI 중간 취소를 혼동하여 보고하지 않는다.
- Final: minor (deferred): 생성 시간은 세대 파일 수정 시각이다. 동일 바이트 재변환 시 유지되므로 마지막 실행 시각으로 설명하지 않는다.
- Final: Ruling: 제품 빌드·실기기·번들 배포/실제 Addressables 번들 로드는 범위 밖으로 유지한다. 런타임 loader에는 격리 바이트 공급자를 사용한다. 비용: 실제 출시 환경의 번들 접근은 별도 검증이 필요하다.
- Final: Ruling: 이전 단계 ZIP/채택과 Publication 전체 재설계는 범위 밖으로 유지하되 기존 Publication 회귀는 실행했다. 파일 최신 상태는 등록 상태와 동일시하지 않는다. 비용: UI에서 주소 등록 자체를 별도로 진단하지 않으며 등록은 변환 완료 결과로 확인한다.
- Final fix RED 검사 준비: 두 체크포인트에서 실제 변경 바이트 assertion 추가, 손상된 원본 선택 상태에서도 pending 복구가 수행되는 등록 회귀 추가. 첫 RED 실행 session14398 / `20261017-020855-871-Run.log`.
- Final RED 확인: `20261017-020855-871-Run.log`에서 cancel checkpoint 실제 바이트 변경 assertion 실패. 입력 레벨 이동 수를 변경한 뒤 취소/복구 실패를 주입하도록 fixture 수정. `20261017-021045-517-Run.log`에서 실제 변경된 팩/등록/journal fixture 구성 후 원본 오류 시 복구 필요 표시 실패 재현(원본 파일은 finally 복원).
- Final 수정: PublishSelected/Publish가 pending 복구를 먼저 실행하며 등록 재수입/캐시 무효화를 공통 RefreshRegistration으로 처리. ContentPackStatus와 창도 journal을 원본 읽기보다 먼저 확인. session40313에서 관련 6개 검사 전체 GREEN 검증 실행 중, 첫 로그 `20261017-021310-503-Run.log`.
- Final fixed #2 GREEN `20261017-021310-503-Run.log`: 취소와 복구 실패 시점에 실제 변경된 바이트가 있음을 먼저 확인한 뒤 이전 바이트 복원 통과.
- Final fixed #1 GREEN `20261017-021523-875-Run.log`: 원본이 읽히지 않아도 pending 복구 안내, 창 상태 표시, 이전 팩/등록 복구 및 조회 복원 통과. 두 Important 모두 RED→GREEN. 이후 4개 회귀 검사 계속 진행 중.

## 최종 완료 감사

최종 관련 검사 6/6 성공. 모두 Unity 배치 에디터 검사이며 제품 빌드는 아니다. 로그 경로는 `Logs/TestHarness/` 아래다.

각 검사 Check 성공 건수(요약 PASS 행 제외): 변환·상태 34, 등록·복구 17, 플레이 비교 103, UI panel 3, generation loader 3, publication 34. 합계 PASS 194 / FAIL 0. 예외 거부 검사는 해당 메서드의 정상 종료와 함께 확인했다.

| 요구사항 | 확인한 근거 |
|---|---|
| 1~15 및 49/50/51/100/101 구간, 요소/튜토리얼 왕복 | ImportedContentPackVerification.Run · `20261017-021310-503-Run.log` exit 0 |
| 미생성/최신/갱신 필요/손상/복구 필요, 실제 바이트 변경 후 취소 및 복구 실패 재시도 | 같은 Run: 변경 바이트 assertion 및 baseline 복원 통과 |
| 실제 Addressables 등록, 구간 축소 후 이전 파일 유지, 실패·선택 변경 롤백, 원본 오류 중 복구/캐시 복원 | ImportedPackRegistrationVerification.Run · `20261017-021523-875-Run.log` exit 0 · `registration-20261017-021538-325/result.txt` PASS 17 / FAIL 0 |
| 변환 버튼 실제 callback, 중복 실행 차단, 상태 표시, 실제 panel | 위 등록 검사 + ImportedContentPackVerification.RunUI · `20261017-021820-541-Run.log` exit 0 |
| 대표 JSON/팩 게임 비교 및 플랫폼 분리 | ImportedPackPlayVerification.Run · `20261017-021741-133-Run.log` exit 0 · `play-20261017-021755-790/result.txt`, `product-audit.txt` |
| 세대 혼합 거부/로딩 취소 및 원본 동시 변경/중단 재복구 회귀 | GenerationLoaderVerification.Run · `20261017-021857-150-Run.log`, JsonPackPublicationVerification.Run · `20261017-021924-358-Run.log`, 둘 다 exit 0 |
| 기존 데이터·설정 보존 | 등록 검사에서 원본 바이트 전수 일치, 최종 Assets/Data·Assets/AddressableAssetsData·ProjectSettings git 변경 없음. pending publication 없음, 테스트 연결 해제 |
| 실제 UI 표시 | 위 computer-use 확인 기록: 창의 일반 상태와 펼친 해시/경로 표시 확인. 원본 손상 상태 표시는 실제 EditorWindow 자동 검사로 확인 |

검증 결과는 해당 범위에 한정한다. Android/iOS/Steam 제품 실행, Addressables 배포 번들의 실제 로드, 클라우드 업로드는 수행하지 않았다. 실제 UI의 변환 중 취소도 제공하지 않는다. 생성 시간은 세대 파일 수정 시각이다. 이 두 사소한 항목은 위 deferred 목록대로 유지한다.

독립 리뷰 Important 2건은 각각 RED→GREEN으로 해결했고, 수정 후 전체 관련 검사 6/6을 재실행했다. 추가 리뷰는 하지 않았다. 소스/문서 변경만 유지하며 운영 팩과 등록은 검사 전 상태로 복원했다. 통합 3단계를 종료하고 별도 운영 실행문을 command 문서에 제공했다. 커밋·푸시·제품 빌드·HTML 갱신 없음.

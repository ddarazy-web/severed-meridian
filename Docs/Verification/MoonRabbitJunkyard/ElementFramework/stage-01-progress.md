# EF-01 — 고정 장애물 피해 기준 확보 결과

상태: **완료**. 2026-10-04(KST). EF-02 문서 준비 완료, EF-02 검사/구현 미착수.

연결: [가이드라인](../../../Planning/MoonRabbitJunkyard/ElementFramework/integration-guideline.md) · [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-01-obstacle-baseline-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-01-obstacle-baseline-goal.md).

## 실제 실행 결과

Unity 6000.3.10f1의 별도 배치 에디터에서 `Levels.Editor.FixedObstacleVerification.Baseline`을 실행했다. **244 PASS, 0 FAIL, 종료 코드 0**, 사례별 관찰 60건을 확보했다. 플레이어/Addressables 빌드, Play Mode, 실기기 검사는 하지 않았다. 완료 범위는 고정 장애물 데이터 기준이며 새 드론 연출 또는 게임 전체 검증이 아니다.

| 증거: `Logs/ElementFramework/Stage01/` 아래 | 결과 |
| --- | --- |
| `baseline-results.txt` | 기존 DataChecks + MatchChecks, 153 PASS / 0 FAIL |
| `overlap-results.txt` | 기존 13개 중첩 사례 + 제거 미션 확인, 91 PASS / 0 FAIL |
| `baseline-observations.jsonl` | 직접 피해 17, 동일 턴 반복 14, 다음 턴 6, 색 조회 5, 캡슐 인접 조회 1, 매칭/후속 4, 범위 13: 총 60 |
| `baseline-verified-execution.json` | 실행 파일/인자/시작·종료 UTC/PID 103508/종료 코드 0 |
| `baseline-verified-editor.log` | 최종 컴파일/검사 로그 |
| `initial-hashes.json`, `initial-git-status.txt`, `integrity-audit.json` | 작업 전후 보존 증거 |

최종 실행은 2026-10-04 01:33:28~01:33:51(KST). 규칙 버전 `power-effects-items-v9`, 관찰 사례 시드 12345. 다음 표의 좌표는 코드 기준 0부터 세며 UI는 1부터 센다.

관찰에는 메모리 레벨 JSON, 행동, 실행 전후 상태, 본체 종류 숫자/색/위치/내구도/충전/점유, 미션 진행, 이동 수, 턴/타격과 실제 효과의 단계별 피해·제거 본체를 저장했다. 문자열인 입력 JSON과 상태 스냅샷은 필요 시 다시 파싱한다. `Logs`는 로컬 증거이며 추적 여부와 별개로 핵심 결과를 이 문서에 남긴다.

## 사례와 실제 기준

| 사례 | 입력/확인 결과 | 기존 검사 |
| --- | --- | --- |
| 회수캡슐 | Safe=2, 내구도 1~6, (4,4), 직접 파워 피해 1; 1이면 제거·미션 1 | DataChecks |
| 캡슐 일반 인접 | 내구도 5, (4,3)→(4,4), AdjacentMatch=None, 상태 유지 | DataChecks |
| 색 제한 | ColorLock=3, 5색 각각 일치=Damage/불일치=None, 조회 무변경 | DataChecks |
| 일반 본체 제한 | 동일 턴 후속 타격 피해 없음, 다음 턴 피해 1 | DataChecks |
| 2×2 본체 | Appliance=4, 내구도 1~9; 별도 타격은 동일 턴에도 피해, 0이면 4칸 해제·미션 1 | DataChecks |
| 단일/독립 매칭 | 하나의 매칭 접촉 9→7/타격 그룹 1, 두 매칭 접촉 9→5/그룹 2 | MatchChecks |
| 같은 공격/같은 칸 | 복사 문맥에서도 AlreadyDamaged; 다른 후속 타격은 7→6 또는 5→4 | MatchChecks |

| 2×2 범위 사례 | 본체 시작 좌표 | 내구도 전→후 | 실제 피해/남은 점유/미션 |
| --- | --- | --- | --- |
| 가로 로켓, 발사 (4,0) | (4,4) | 9→7 | 2 / 4 / 0 |
| 세로 로켓, 발사 (0,4) | (4,4) | 9→7 | 2 / 4 / 0 |
| 폭탄 모서리, 발사 (4,4) | (2,2) | 9→8 | 1 / 4 / 0 |
| 폭탄 한 변, 발사 (4,4) | (2,3) | 9→7 | 2 / 4 / 0 |
| 폭탄 범위 밖 | (1,1) | 9→9 | 0 / 4 / 0 |
| 로켓+로켓 | (3,6) | 9→7 | 2 / 4 / 0 |
| 로켓+폭탄 네 칸 | (3,6) | 9→5 | 4 / 4 / 0 |
| 로켓+폭탄 두 칸 | (2,7) | 9→7 | 2 / 4 / 0 |
| 폭탄+폭탄 한 칸 | (1,2) | 9→8 | 1 / 4 / 0 |
| 폭탄+폭탄 두 칸 | (1,3) | 9→7 | 2 / 4 / 0 |
| 폭탄+폭탄 네 칸 | (2,3) | 9→5 | 4 / 4 / 0 |
| 자석+자석 네 칸 | (0,0) | 9→5 | 4 / 4 / 0 |
| 자석+자석 하한 | (0,0) | 3→0 | 3 / 0 / 1 |

조합은 (4,4)/(4,5) 교환이다. 하한 사례는 범위 4칸이지만 내구도 0에서 제거되어 실제 피해 기록은 3개이고 미션은 1회다. 각 범위 사례는 중복 타격 제외·피해 단계·입력 상태 보존·점유/미션을 직접 검사했다.

## 변경·보존·안전

- 새 `FixedObstacleVerification.Baseline.cs`/`.meta`: 기존 DataChecks/MatchChecks/OverlapData를 실행하고 Unity JsonUtility로 관찰을 기록하는 배치 전용 검사. 생산 코드·패키지·전체 검사 프레임워크 추가 없음.
- 기존 `.cs`/`.Supplemental.cs`: 기존 실제 값을 기록하고 색/인접 조회 무변경 확인을 보완. 기존 피해 기대값 유지.
- 기존 `.Overlap.cs`: 미션을 지정하고 제거 시 단일 집계 확인. 기존 범위/피해 기대값 유지.
- 시작 시 Scripts/Data/Prefabs/Scenes/Packages/ProjectSettings의 기존 파일 1,091개를 SHA-256으로 기록했다. 비교 결과 기존 Editor 검사 3개만 변경, 삭제 0, 신규 검사와 `.meta` 2개. 런타임·원본 에셋·패키지/설정·기존 팝업/UI 변경·기존 GUID/enum 숫자는 보존했다.
- ServeredMeridian Editor는 시작 시 실행 중이 아니었다. NCloud_Unit_CV는 조작하지 않았다. 종료 API가 있는 검사는 전용 배치로 실행했다.
- UI Start/Restart는 에셋 저장/창, Regression은 다른 기능/임시 에셋을 포함해 제외했다. FinalData/Edges/Supplemental 전체 실행 대신 필요한 DataChecks/MatchChecks/OverlapData만 재사용했다. 대상 검사 계열에는 빌드 호출이 없다.
- 첫 sandbox 실행은 사용자 Curl 캐시 접근 오류로 검사 전 크래시했다. 명령행을 확인해 이번 검사 PID 107112만 정리하고 정상 권한으로 재실행했다.
- 기록 코드의 Newtonsoft 참조 부재는 JsonUtility로 교체했고 지역 변수 이름 충돌도 수정했다. `baseline-editor.log`와 `compile-variable-error-editor.log`에 중간 실패를 보존했다. 최종 로그/실행 메타데이터와 구분한다.
- 초기 Data 실행의 Unity 임시 할당 정리 경고는 기록했다. 최종 메모리 레이블 출력은 검사 FAIL이 아니다. `git diff --check` 통과, CRLF 안내는 Git 설정 안내다.

## 작업 판단과 인계

Pre-flight: 작업 1의 안전 범위/사례 → 작업 2의 관찰 보완 → 작업 3의 실제 실행/증거. 공유 계약은 현재 피해 단위이며 런타임/저장 변경과 충돌하지 않는다.

판단: 동일 칸 중복 조회는 기존 MatchChecks가 이미 검증하므로 중복 fixture를 추가하지 않았다. 이번 작업은 기존 동작의 기준 확보이며 생산 코드 변경을 위한 RED/GREEN 구현은 수행하지 않았다. 사용자 요구대로 워크트리 이동·임의 커밋·에이전트 위임은 하지 않았다. 최종 자체 검토는 이번 검사 diff, 실제 관찰값과 파일 보존 목록을 대상으로 수행했다.

다음은 드론 선택·예약·효과 타임라인 기준 확보다. EF-01 피해 기준은 재사용하고 정책/비행 구현은 섞지 않는다. 미리 계산한 최종 상태/표적과 현재 표시 상태의 경계를 확인하여 이후 돌진 중 중단·재선정의 선행 조건을 찾는다.

- [EF-02 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-02-drone-baseline-plan.md)
- [EF-02 목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-02-drone-baseline-goal.md)
- [EF-02 복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-02-command.md)

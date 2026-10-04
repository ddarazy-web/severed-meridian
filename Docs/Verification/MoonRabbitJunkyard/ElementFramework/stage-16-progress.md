# EF-16 — 나머지 내구도형 장애물 배치 수치 연결 검증

상태: 완료. 2026-10-04, ServeredMeridian/Unity6000.3.10f1. work/HEAD b353077 유지. 시작 시 EF-13/14/15의 미커밋 소스·문서가 있었으며 그대로 보존했다. 커밋/푸시하지 않았다.

## 변경과 흐름

```text
Elements/Runtime/LegacyElementDefinitions.cs           기존 ID4개/배치 프로필 데이터 추가
Obstacles/Rules/LevelPlacementRules.cs                 내구도형5종 공통 정의 수치 조회
Elements/Editor/Tests/CratePlacementVerification.cs    미등록 오류 입력 Scrap→Generator
Elements/Editor/Tests/DurablePlacementVerification.cs 전환 전후 실제 입력/수치/편집/런타임 검사
```

기존 소스3개만 변경하고 새 Editor 검사1개/meta1개를 추가했다. 새 런타임 타입/폴더/asmdef/패키지는 없다. 기존 ElementPlacementProfile/ElementDefinition/ElementCatalog/LegacyElementMap은 변경하지 않았다. 양수 프로필과2인자 정의 계약을 유지했다.

private static readonly 카탈로그에 아래4종을 한 번 준비한다. Size/MaxDurability에서 기존 내구도형5종을 하나의 공통 조회 경로로 처리하며 각 종류별 수치 반환 분기를 제거했다. 반복 조회의 동일 정의 객체·실제 정의 수치와 공통 조회 일치를 검증했다. 카탈로그 Get은 기존 Dictionary 조회이고 매 조회 생성/종류 전체 검색/변경 가능한 등록 API가 없다. enum 그룹은 기존 구형6종을 연결하는 과도기 경계이며 신규 영구 ID의 최종 제작/실행 체계는 아직 아니다.

| 정의 ID | 종류/숫자 | Size/MaxDurability | 전후 |
| --- | --- | --- | --- |
| obstacle.scrap | Scrap1 |1/5| 동일, 연결 |
| obstacle.recovery-capsule | Safe2 |1/5| 동일, 연결 |
| obstacle.color-lock | ColorLock3 |1/3| 동일, 연결 |
| obstacle.metal-rod-box | Appliance4 |2/9| 동일, 연결 |
| obstacle.crate.wood | Crate0 |1/6| 기존 연결 유지 |
| obstacle.generator | Generator5 |2/0| 기존 조회 유지·정의 미등록 |
| 미지원 -1/6/999/int.MinValue/int.MaxValue | — |1/0| 기존 의미 유지 |

발전기는 양수 내구도 정의에 포함하지 않았다. ObstacleValueError의 충전3~5 의미와 실행 규칙은 그대로다. 고철 공급 ItemError는 기존 공통 최대 내구도 조회를 통해5를 받으며 호출부/메시지/실행 규칙을 변경하지 않았다. GUI/편집/게임 보드 소비자 개별 수정도 없다.

## 전환 전후 실제 검사

생산 변경 전에 Before를 실행해 메모리 입력78개·결과97건을 저장했다. Run은 저장된 동일 입력을 사용한다. 구현 전 RED는 기존97건 일치 후 obstacle.scrap 미등록 KeyNotFoundException으로202 PASS/1 FAIL·종료1이었다. 컴파일 오류가 아니다. 구현 후97건 모두 일치하고4종 정의 관찰을 추가해 최종101건이다.

| 기록 범주 | 건수 | 실제 입력/결과 |
| --- | ---: | --- |
| 전체 종류/미지원 수치 |11| 위 표 동일 |
| 내구도 실제 편집 |30| Scrap0~6/Safe0~6/ColorLock0~4/Appliance0~10 |
| 점유 경계 실제 편집 |13| 1×1 (8,8),2×2 (7,7) 허용;2×2 (8,8),모든 (9,8)/(-1,0) 거절 |
| 중복 본체 |4| 다른 ID2개 같은 footprint, 각 점유 칸 Find=-2·편집Changed0·레벨 검사 오류 |
| 내부 벽 |1| Appliance (4,4)~(5,5) 내부 (4,4)-(4,5) 벽, Changed0 |
| 자물쇠 색 |3| 사용 색0 허용·목록 제외4/미지원-1 거절 |
| 발전기 배치 충전 |5| charge2~6 중3/4/5 허용·2/6 거절 |
| 고철 공급 내구도 |7|0~6 중1~5 허용·0/6 거절 |
| 실제 피해/미션/바이트 |22|4종 모든 허용 내구도 |
| 발전기 실제 연결/바이트 |1| Generator→Appliance, 두 본체 ID/전선·활성 연결1 |
| 구현 후 정의 관찰 |4| 각 ID/프로필 수치/동일 정의 객체 |

모든 허용 내구도22건의 실제 편집은 Changed1/Skipped0, 전체 레벨 검사0이다.0/최대+1의8건은 Changed0/Skipped1이다. 실제 메시지는 `내구도는 1~5입니다.`, `1~3`, `1~9`이다. 내부 벽은 `2×2 본체 내부에 고철 벽이 있습니다.`, 중복은 `중복 배치: 기존 Inspector 목록에서 수정하세요.`, 색은 `자물쇠 색은 레벨 사용 색 중 하나여야 합니다.`로 거절됐다. 충전은 `필요 충전량은 3~5입니다.`, 공급은 `고철 내구도는 1~5입니다.`이며 전후 문장도 동일하다. 좌표는 코드 기준0부터 표시한다.

## 피해·미션·난수·저장·ID

22개 메모리 입력에서 실제 편집으로 생성한 본체 ID·초기 내구도·좌표(4,4)·해당 종류 미션1을 저장했다. 별도 Editor의 전환 후 동일 JSON/seed12345로 만든 런타임 전체 스냅샷이 일치했다. 실제 Power Hit1회는 내구도 d-1이고 d=1만 제거 미션Progress1, 나머지는0이다. 효과 기록/런타임 후 상태도 전후 같다. Hit의 규칙 DrawCount는 동일하고 Unity 전역 난수는 각 실행 내 전후 무소비다. 원본 JSON 및 배치 ID·Encode 바이트가 보존됐다.

발전기 연결 fixture는 두 본체 ID·전선 좌표·활성 연결1·런타임 스냅샷·바이트가 동일하다. 기존 ID 검사의 enum0~5, 본체 ID/표시명/배열순서가 다른 입력과 실제 발전기 연결도 통과했다. 포맷버전1/50레벨 묶음은 유지됐다. 원본/배포 팩 파일을 생성하지 않고 메모리 Encode 바이트만 base64로 기록했다.

각 실제 입력·본체 ID와 전체 값은 input-*.json/after-values.jsonl 및 runtime-values-summary.json에 있다. 바이트 전체의 전후 동일 SHA256은 다음과 같다.

| 종류/초기 내구도 | 바이트 수 | 전후 동일 SHA256 |
| --- | ---: | --- |
| Scrap/1 | 1895 | `8424cc874e170f20e441c8a3d829a3e8589264946ffacc6c01e0b7525a36f1ff` |
| Scrap/2 | 1895 | `902e444fd264903fb520e5745d675b067c03a8dbf67aab54fd1142cc884a616f` |
| Scrap/3 | 1895 | `ca47e0712dfc4ccf7ec3deba92c42558d7d028815c621ad744f720c59fde1845` |
| Scrap/4 | 1895 | `bd0478926d85586c5d58ee9ff123945ab5cf30edc34ee14c36163fde96781498` |
| Scrap/5 | 1895 | `68f29ce16063a2dc1f2024cd370b751e87a66913bebcdeea1050299ca7dfccc7` |
| Safe/1 | 1895 | `5f4f80d7b68c708f3f45c1adc710b9843c7c775b5982e493d6990c9b369d91a7` |
| Safe/2 | 1895 | `7784a38d178a29b0c9b10dfefda527f216bf1c7841a851c1fc80be9e1bca2606` |
| Safe/3 | 1895 | `e933947e721d298acb1b79a5c59028070c01dd3bb22222be4a5612cbf5c0b24e` |
| Safe/4 | 1895 | `89622d667cf83cb66737a8de45939483730225a9abfcdccdcfe48c8980c64359` |
| Safe/5 | 1895 | `ba1d52071cfccd61dc76a40b19591aa495a0f87f29436fc5bccb8557896b9022` |
| ColorLock/1 | 1895 | `1badd85fb60831ffa26ae3e7276d1ccdc6159a4c6df1afe8e25f78675bd77c89` |
| ColorLock/2 | 1895 | `6d01e13696f55b20e4e46dddacbf582a4847ad8d64a3ad7c17615b3d9fdeb140` |
| ColorLock/3 | 1895 | `38a908fa2b06bb4e6a61817fca703f1f40baf72478893c064f517269d9780e06` |
| Appliance/1 | 1835 | `3aefc23f22b0c6b0f1bd96bb5848b4fe69f88c3a9bed251007d160390621d55a` |
| Appliance/2 | 1835 | `529676d52fbe56f38d93b7c1d9e273ae0aec377a6bac8bda629f39bfef52f60a` |
| Appliance/3 | 1835 | `2d434813ec42c036422e05e88e8a9ca41b216948bfb3bb066b5bc7a307be16dc` |
| Appliance/4 | 1835 | `4fc3dace54946124c7875ef9e8bb66a10d7a12715871cf501381899f0776bdd1` |
| Appliance/5 | 1835 | `89b93f7848b0994a048f5767fe05c8d5cb43be5f4d2c20cedf6d0c56035092b3` |
| Appliance/6 | 1835 | `1a809c59254a2018797124362fa2a9cfdd6fcd2d89cb66603050d3f633f1ad5c` |
| Appliance/7 | 1835 | `3c71435f53ec72930a20ce1693e41661202def15165f93657e4077a35b08dd26` |
| Appliance/8 | 1835 | `d8100fed7a847f26f0b646973dce45694b70d34f0866020335ce6ef740f38733` |
| Appliance/9 | 1835 | `79d855e3d4f6fde6deb9dedcf2b0487253e43a65dc04358dce5c9c026bb4be2b` |
| Appliance/9 + Generator | 1921 | `e4f828bb036328fb83363d7f5b59cde3b7acc780abfe0aaf27b072b86c3d4208` |

기존 GeneratorVerification.Data는 충전3~5·대상 제거/전선/피해/미션을, ScrapVerification.Data는 고철1~5의 고정 공급/유지 공급/생성 카운터·난수/원본 보존을 실제 실행한다. 이 검사의 안전한 메모리 경로만 사용했다. 원본 에셋/임시 에셋을 생성하는 LevelSupplyVerification.Prepare/Restart는 실행하지 않았다.

## 오류 계약과 검사 조정

EF-15의 미등록 Scrap fixture는 이제 실제 등록된 종류라 실패 입력으로 쓸 수 없다. 아직 미등록인 유효 ID obstacle.generator로 바꾸어 KeyNotFoundException/해당 ID 포함 검사 수와 의미를 유지했다. 기존 메타데이터 정의 element.no-placement의 프로필 누락 InvalidOperationException/ID, 빈 카탈로그 미등록 ID, profile Size0/-1·Max0/-1 거절도 계속 통과했다. 누락 프로필/정의를 기본 상자로 대체하지 않았다.2인자 계약과500개 카탈로그/입력 독립성 검사는 유지됐다.

## 별도 Editor 실행 결과

| 검사 | PASS | FAIL | 종료 |
| --- | ---: | ---: | ---: |
| DurablePlacementVerification.Before, 생산 전 |202|0|0|
| DurablePlacementVerification.Run, 최종 |210|0|0|
| CratePlacementVerification.Run |75|0|0|
| ElementCatalogVerification.Run |1539|0|0|
| ElementIdVerification.Run |49|0|0|
| FixedObstacleVerification.Data |142|0|0|
| GeneratorVerification.Data |246|0|0|
| BotObservationVerification.Run |32|0|0|
| ScrapVerification.Data, 공급 포함 |186|0|0|
| 최종 필수8종 합계 |2479|0|각0|

모두 Unity6000.3.10f1 batchmode/nographics 별도 Editor이며 플레이어/콘텐츠 빌드가 아니다. 기존7종의 결과/값 파일은 실행 전 백업→새 실측을 Stage16으로 복사→원래 파일 복원했다. 과거 파일의 바이트 일치를 별도로 확인했다.

증거: [Stage16](../../../../Logs/ElementFramework/Stage16)의 before/red/after.log 및 execution.json/results.txt, baseline-values.jsonl/after-values.jsonl/input-*.json, run-regression.ps1, crate/catalog/id/fixed/generator/bot/scrap.log 및 각 execution/results/values, verified-results.json/runtime-values-summary.json, protected-before.json/existing-work-before.json/git-before.txt/preservation-audit.json/source-final.json/progress.md/completion-audit.json. 로그는 Git 제외 대상이다.

## 보존·검토·한계

보호한 기존 파일1818개 중 이번3개만 변경됐고 나머지1815개 SHA256 동일, 예상 밖 변경0이다. 기존 meta/GUID 변경0, 새 검사 meta1개 GUID 중복0이다. 기존 EF-13/14/15의 작업은 허용된 소스3개와 단계 상태/목차 갱신 외에 그대로다. 기존 데이터/팩/씬/프리팹/아틀라스/Addressables·패키지/프로젝트 설정은 보존했다.

EF-05 예외 원복 소스, EF-09 원본1254px3개 및 Editor/월드 바닥 차이, EF-11 Draw/Reset/해제 책임과 드론/피해/미션/낙하/공급 실행·봇 DTO/UI/MVVM/표현/풀은 보존했다. 빌드·재패킹·이미지·원본 변환·팩 재생성·커밋·사용자 Unity 종료·씬 저장을 하지 않았다. NCloud_Unit_CV 등 다른 프로젝트는 건드리지 않았다.

최종 소스/명세/호출부를 직접 검토했다. 사용자 계획의 별도 요청 규칙에 따라 에이전트를 사용하지 않았으며 독립 리뷰보다 검토 강도가 낮다. 계획 실행 스킬의 커밋/새 worktree/자동 정리 대신 기존work/미커밋 작업과 증거를 유지했다. 최종 검토의 추가 결함은 없고 필수 검사 실패는 남지 않았다.

이번은 배치 수치 연결만 완료했다. 전체 행동/피해 정책·제작/배포 카탈로그·드론 수정은 미구현이다. 전체 플레이/렌더링/실기기/IL2CPP/프레임 성능은 검사하지 않았다. 발전기 Size/충전 배치 조회는 아직 기존 수치다.

## 다음 단계

**EF-17 발전기 배치 크기·충전 수치 정의 연결**. 양수 내구도 프로필을 완화하지 않고 별도 배치 수치를 불변 정의에 조합한다. 크기/배치 허용 충전만 연결하고 실제 충전 실행/피해/미션은 별도로 둔다. [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-17-generator-placement-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/stage-17-generator-placement-goal.md) · [전체 복사용 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-17-command.md). 다음 구현은 시작하지 않았다.

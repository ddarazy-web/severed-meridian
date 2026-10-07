# 큰 구간 4단계 — 진행 기록

상태: 완료. A~D 연결과 최종79종691470 PASS/0 FAIL·실제 종료0, 전량 논리 비교·원본 보존 감사를 확인했다. 2026-10-07. 아래 중간 미완료/실패 기록은 당시 원문 이력이며 최종 결과는 마지막 완료 보고를 따른다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/phase-04-presentation-resources-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/phase-04-presentation-resources-goal.md).

## 시작 기준 — 2026-10-06

- 실제 브랜치 `work`, HEAD `b03ac0539e0286bd67763c88fcbd16f8316ac452`. 시작 `git status --porcelain`은 비어 있다. 계획의 과거 HEAD를 복원하지 않고 현재 사용자 상태를 보존한다.
- `Logs/ElementFramework/Phase04/start-baseline.json`에 소스/메타/원본/설정/문서 시작 해시를 기록했다. 이미지·기존 팩·과거 증거의 별도 보호 manifest도 수집한다.
- 프로젝트 규칙, 실행 계획, TDD를 적용한다. 사용자 지시에 따라 현재 work에서 직접 작업하고 커밋·하위 에이전트·별도 worktree를 만들지 않는다. 스킬의 커밋/리뷰 에이전트 절차보다 사용자 금지 사항을 우선한다.
- 열린 Editor 프로세스를 읽어 ServeredMeridian 사용자 Editor가 없는 것을 확인했다. 다른 프로젝트 Editor와 AssetImportWorker는 종료하지 않는다.

## 구현 순서와 연결 경계

1. A: 제작 DTO → 불변 시각 카탈로그 → 상태 조회 → 기존 세 보드. 등록 누락/중복/상태 오류를 ID로 거절한다.
2. B: A의 등록 경로와 생성 참조를 통해 필요 주소 집합을 계산하고 기존 native Addressables 소유권에 연결한다.
3. C: A의 표현 값으로 공통 슬롯을 재사용하고 반환/취소/전환 오염을 실제 객체로 검사한다.
4. D: 기존 HUD 표시 진행값과 봇 공개 정보의 경계를 분리한 뒤 전체 회귀를 한 번 실행한다.

A의 상태/키 계약은 B의 필요 주소와 C의 표시 초기화가 소비한다. 제작 카탈로그는 게임 진입 경계에서 값으로 전달하며 규칙 팩2/지문에는 추가하지 않는다. D는 실행 규칙을 변경하지 않고 기존 표시 진행값을 사용한다.

## 현재 증거

시작 조사와 보호 기준 수집을 마쳤다. A의 기반 조회와 실제 세 보드/진입 사본 연결을 검증했다. A의 전체 상태 표현과 B~D, 최종 회귀는 아직 완료되지 않았다.

## A — 실패 검사와 조회 구현

신규 조회 검사는 실제 Unity 종료1로 시각 계약 부재를 확인했다. 원문: Logs/ElementFramework/Phase04/runs/visual-red-20261006-103049. ID별 그림/공유 별칭/내구도·프레임 누락/중복/입력 변경 독립성을 검사한다. DTO와 불변 조회 구현을 추가했으며 세 보드 연결과 자원 준비는 아직 남아 있다.

### 첫 연결 체크포인트

이 절이 최신 상태이며 위 문장은 최초 실패 검사 시점의 기록이다.

- `ElementVisualCatalogDto`/제작 에셋, 불변 시각 정의/프레임/카탈로그, `ElementVisualState`/Resolver/Lookup을 추가했다. 상태 선택자는 겹치면 거절한다. 배열·효과 목록·생성 참조는 복사하며 조회는 원본/난수를 변경하지 않는다.
- `LegacyElementVisuals`는 기존 이미지의 상태 등록표다. 가로 로켓 크기1.12와 중심 보정, 기존 내구도 범위와 드론 회전4컷을 등록했다. 신규 ID는 명시적 별칭만 허용한다. 프레임0은 정지 이미지, 드론1~4는 시트0~3을 선택한다.
- 편집 신형 보드, 플레이 시험 보드, 월드 보드가 실제 선택 정의 ID를 조회한다. 새 ID의 미등록 시각은 편집 칸의 오류/툴팁에 표시하고 임의의 일반 블록/기존 종류 그림으로 대체하지 않는다. 카탈로그를 칸마다 재구성하지 않고 편집 redraw에서 한 번 구성한다.
- 기존 `ElementCatalogAsset`에 시각 제작 연결을 추가했다. 게임 진입 요청은 규칙 팩과 별도의 표현 JSON 사본을 보유한다. 기존 SessionState 진입 경계에서 DTO를 세션에 전달하고, 세션의 Inspector 표현 DTO/주입 값으로 재시작·다음 레벨의 후보 아트를 구성한다. 새 Resources/Addressables 로더나 팩3을 만들지 않았다. MemoryPack 시험에서도 표현 설정은 선택한 제작 원본의 설정을 사용한다.
- 원본 uGUI·월드 프리팹/씬과 이미지 파일은 편집하지 않았다. 실제 게임 씬 전환/수명과 모든 표현 수치/프레임 적용은 후속 검사 대상이다. 이 체크포인트의 세 보드 검사는 native SpriteRenderer/UI Toolkit의 실제 바인딩을 검증하며 GPU 화면/실기기 보증은 아니다.

| 검사 | 실제 종료 | PASS | 원문 |
| --- | ---: | ---: | --- |
| 시각 입력·불변·7개 상태 축·오류·원본 변경/파기 | 0 | 27 | `runs/visual-id-error-green-20261006-104139` |
| 구형 색/내구도/충전·로켓 중심·드론4컷·미등록/상한 무대체 | 0 | 79 | `runs/legacy-visual-green-20261006-104444` |
| 실제 세 보드·새 두 ID와 공유 별칭·규칙 독립·진입 사본·편집 누락 오류 | 0 | 11 | `runs/visual-editor-error-green-20261006-105900` |

증거 경로의 기준은 `Logs/ElementFramework/Phase04/`다. **누적 확인117 PASS/0 FAIL**이며 서로 다른 구현 체크포인트에서 실행했다. 최종 고정 소스의 전체 회귀 결과로 집계하지 않는다. 최초 실패, 제작 오류의 정확한 ID 누락, 미션 없는 검사 입력, native Sprite 이름 비교와 진입/편집 오류 실패의 원문을 모두 보존했다. 원래 기대값이나 테스트의 실패를 삭제하지 않았다.

실제 native SpriteAtlas.GetSprite는 `이름(Clone)`을 반환한다. 진단 실행에서 세 화면 모두 기대한 그림의 Clone임을 확인한 뒤 새 검사의 이름 기대에 해당 native 접미사를 명시했다. 런타임의 선택을 바꿔 검사를 맞추지 않았다. 테스트 입력의 미션 누락도 입력에만 미션을 추가했으며 규칙 검증을 완화하지 않았다.

### 남은 범위

첫 연결 후 보호 감사는 실제 종료0, 보호 manifest에서 소스를 제외한 **6843개 파일의 해시 차이0**이다. 원본 에셋/설정/이미지와 해당 manifest에 포함된 Phase01~03 증거를 확인했다. 이전3단계의 더 넓은 과거 증거9137개 전체 감사와 같다고 확대하지 않는다. 시작 기준의 기존 소스 변경14개는 모두 표현/진입 연결 범위이며 원래 검사 소스를 수정하지 않았다. 신규 `.meta`11개의 GUID 충돌0, work/HEAD 유지와 `git diff --check` 종료0을 확인했다. 근거: `first-connection-protection.json`, `first-connection-source-changes.json`, `first-connection-guids.json`.

- A: 구형 보드 호환 조회의 통합, 모든 수치/피벗/정렬/프레임·효과 적용과 명시적 상태 준비 검사, 게임 씬에서 설정 전달/재시작 확인. 제작 입력에 기본 시각 참조를 포함한 생성 의존성이 있는 경우의 합성 경계도 검증한다.
- B: 모든 공급·행동/매칭/부스터/조합·미션 생성 참조의 자원 폐쇄와 실제 요청 집합/누락·미사용 주소, 취소/pending/소유권.
- C: 반환 슬롯 전체 오염 초기화, 실제 반복/취소/전환/Destroy/Unload와 추가 생성/잔상 검사.
- D: HUD Presenter/표시 진행값, 봇 공개 특성 및 동일 공개 입력 쌍.
- 이후 기존30/드론9/3단계12/그래픽3와 신규 검사, 전량 논리/원본/과거 증거 보존 감사,5단계 문서. **전체 목표는 계속 진행 중**이며 완료/차단 상태로 변경하지 않았다.

### B — 공급·생성 참조 계획과 실제 번들 누락 진단

2026-10-06 후속 작업에서 `ElementResourcePlan`을 추가했다. 현재 배치의 실제 정의와 곰팡이에 가려진 내용물, 고정/랜덤/유지 공급, 공급 본체/파워 선택, 매칭·부스터·조합의 기본 생성, 시각 생성 참조를 방문한다. 방문 ID·이미지·효과 경로·아틀라스 주소는 중복을 제거하며 순환을 종료한다. 기존 `PuzzleArtwork`가 이 계획으로 준비하도록 연결했고 기존 pending/취소/Dispose 코드는 유지했다. 구형 효과 참조도 `LegacyElementVisuals` 등록 데이터로 수집한다.

신규 검사의 최초 RED는 실제 종료1로 계획 타입 부재를 확인했다 (`runs/resource-plan-red-20261006-111100`). 검사 입력의 고정 고철/유지 공급 충돌, 회수 도착 바닥·미션과 공급 수량 오류는 기존 규칙을 그대로 두고 입력만 정정했다. 고정 공급과 유지 공급은 별도 유효 입력에서 검사한다. 해당 실패 원문도 각 실행 폴더에 보존했다.

참조 계산17개 항목은 통과했다. 실제 요청 주소의 누락0/미사용 별도 종류 요청0과 native 핸들 로드는 준비 연결 이후 통과했지만 **전체 native Sprite 검사는 실패한다**. 이 부분을 GREEN 또는 B 완료로 집계하지 않는다.

- `runs/resource-plan-native-red-20261006-111909`: 연결 전 실제 로더와 계획의 주소 집합 불일치.
- `runs/resource-plan-native-green-20261006-112000`: 연결 후 주소 집합/핸들은 일치하지만 드론 회전 시트 조회 실패. 실행 이름의 `green`은 의도한 실행명이며 결과는 실제 종료1이다. 주소 출력 파일 생성 전 실패하여 runner의 누락 출력 오류도 보존했다.
- `runs/resource-plan-native-inventory-20261006-112134`, `resource-plan-import-diagnosis-20261006-112256`: native 목록과 원본 임포트 상태 확인.
- 원본 아틀라스 재읽기·에디터 미리보기 패킹·그래픽 모드 진단에서도 같은 실패가 발생했다. 이미지/아틀라스/메타 내용을 바꾸거나 Addressables 콘텐츠를 빌드하지 않았다.
- `runs/resource-plan-native-source-comparison-20261006-112712`: AssetDatabase의 원본 파워 아틀라스는 Sprite12개이며 회전 시트 조회가 성공한다. Addressables의 별도 로드 객체는 Sprite11개이며 회전 시트가 없다. 원본은 Sprite/Single로 정상 임포트돼 있다. 단순 원본 매핑 오류와 실제 로드 결과의 차이를 구분했다.
- `runs/resource-plan-preparation-id-error-20261006-112857`: `ValidateResources`로 모든 계획 경로를 준비 단계에서 확인한다. 현재 로드 자원의 누락은 `요소 ID [power.drone] 자원 준비 실패: PowerBlocks/collection-drone-rotor-4frames-v1`로 시작 전에 보고된다. 실제 종료1이며 해당 번들 문제 해결을 증명한 것은 아니다.

현재 필요한 다음 작업은 실제 로드 위치/공급자를 확인해 기존 로딩 정책과 콘텐츠 빌드 금지 조건 안에서 번들 차이를 해결할 수 있는지 판단하는 것이다. 검사 소유 아틀라스로 바꿔 기존 번들 실패를 숨기지 않는다. 모든 상태의 준비 전 유효성, 생성 참조의 등록 경계, 효과 Playback 적용·소유권, C/D와 최종 전체 회귀도 여전히 남아 있다. 이번 부분 검사로 전체 목표를 축소하거나 완료 처리하지 않는다.

후속 위치 진단 `runs/resource-plan-native-location-20261006-113105`에서 실제 공급자가 `BundledAssetProvider`이고 의존 번들이 `Library/com.unity.addressables/aa/Android/Android/boardartwork_assets_moonrabbitboard-powerblocks_0e75b09bc753ff560d1dcc943b79715f.bundle`임을 확인했다. 원본 에셋 DB를 읽는 로더와 다르다. 이 실행은 명시적 Initialize 완료 콜백 안에서 기존 편집 모드 WaitForCompletion을 호출해 재진입 오류로 실제 종료1이었다. 이는 앞서 증명한 회전 시트 누락과 별도의 검사 호출 경계 오류다. 명시적 Initialize를 제거하고 위치 기록을 기존 준비 완료/실패 이후 finally로 이동했으며 이 마지막 검사 소스 변경은 아직 재실행 전이다. 현재 native 검사 통과로 보고하지 않는다. 콘텐츠 빌드·로딩 정책 변경은 하지 않았다.

후속 원본 보호 감사는 실제 종료0으로 완료했다. 보호 manifest에서 소스를 제외한6843개 파일의 해시 차이0, work/시작 HEAD 유지, git diff --check 종료0을 확인했다 (`resource-plan-protection.json`). 이전의 더 넓은 과거 증거9137개 전량 감사와 같다고 확대하지 않는다. 이번 관련 검사/감사 세션은 모두 종료했다. 다음에는 검사 호출 경계 정정을 확인하고 A의 상태 검증/제작 합성, C/D를 계속한다. 실제 기존 번들의 누락을 해결하거나 목표 전체 완료로 처리하지 않았다.

### 실제 Play Mode 자원과 제작 합성·상태 준비 — 최신 체크포인트

앞의 번들 진단은 Edit Mode의 결과다. 별도 배치 Editor에서 메모리 씬으로 실제 Play Mode에 진입하자, 기존 설정이 사용하는 공급자는 `AssetDatabaseProvider`였고 원본 파워 아틀라스의 드론 회전 시트도 정상 조회됐다. 콘텐츠 빌드·설정/로딩 정책 변경·검사 전용 아틀라스 대체 없이 native 준비 검사가 **98 PASS/실제 종료0**으로 통과했다 (`runs/resource-plan-actual-play-mode-20261006-113915`). 오래된 Android 번들의 시트 누락을 수정했다는 뜻은 아니며, 현재 에디터 게임 실행 경계와 기존 번들 결과를 구분한다. 강제 재임포트/미리보기 패킹 진단은 검사에서 제거했다. 모든 실패 원문은 남겼다.

제작 시각 에셋은 기본 등록표와 사용자 재정의를 먼저 합성한 뒤 생성 참조를 검증한다. 사용자 정의가 기본 `power.drone`을 생성하는 실제 제작 입력, 이후 원본 변경/파기와 값 사본 독립성을 검사했다. RED는 기본 참조를 합성 전에 거절한 실제 종료1 (`runs/visual-authoring-composition-red-20261006-113646`), GREEN은 **29 PASS/실제 종료0** (`runs/visual-authoring-composition-green-20261006-113755`)이다.

`ElementVisualPreparation`은 필요한 정의의 현재 상태와 실행 중 도달할 내구도 감소·충전 증가·파워 프레임을 준비 전에 조회한다. 정의 상한13이지만 현재2인 본체는1~2만 요구하며, 실제13은 명시적 상태가 없으면 ID/상태 오류로 거절한다. 고정/랜덤/유지 공급과 생성 참조의 상태도 검사하며 내구도를 이미지 상한으로 자르지 않는다. 별칭 공유 프레임은 명시된 와일드카드로 허용한다. RED 실제 종료1 후 **8 PASS/실제 종료0** (`runs/visual-preparation-red-20261006-115550`, `runs/visual-preparation-green-20261006-115756`)을 확인했다. 상태 준비 추가 후 native 자원 계획도 다시 **98 PASS/실제 종료0** (`runs/resource-plan-state-coverage-green-20261006-115952`)이다. 모든 행동 조합 상태의 최종 검증을 대신하지 않는다.

세 보드 검사는 기존 Edit Mode에서 실제 Play Mode 진입 방식으로 바꿨다. 기존 세 화면 바인딩/ID 선택/공유 별칭/규칙 독립/진입 사본 검사는 유지하며 **11 PASS/실제 종료0**을 확인했다 (`runs/visual-boards-preparation-green-20261006-120117`). 아직 모든 피벗·정렬·시트 프레임의 실제 적용이 완료됐다는 의미는 아니다.

### D — HUD 표시 상태와 실제 씬 검사

기존 uGUI 프리팹을 유지하며 `PuzzleHudPresenter`가 표시용 미션·이동·수집 비행 값을 복사한다. `PuzzleHudState`는 Presenter가 갱신하는 읽기 전용 표시 버퍼이며 과거 시점의 불변 스냅샷으로 설명하지 않는다. 실행 세션/정의/비행 예약 객체를 View 상태에 전달하지 않고 미션 숫자는 `DisplayedMissionProgress`를 사용한다. 미션/비행 목록을 재사용하며 이동 문구는 값이 바뀔 때만 갱신한다. 화면의 세션 이벤트와 선택 이벤트를 분리해 HUD 중복 갱신을 줄였으며 Presenter 반환 시 구독과 표시를 정리한다.

- HUD 타입 부재 RED: `runs/hud-presenter-red-20261006-114252`, 실제 종료1.
- 표시 진행값/읽기 전용/원본·난수 무변경/기존 프리팹/반복/구독 해제: **12 PASS/실제 종료0**, `runs/hud-presenter-green-20261006-114504`.
- 실제 씬의 Asset·MemoryPack 진입, 수집 전후·일시정지·잠금, 반복 다시하기·후보 취소·실패·복구·씬 해제: **183 PASS/실제 종료0**, `runs/hud-presenter-current-retry-contract-20261006-115314`. 이 실행은 HUD 버퍼와 이벤트 정리까지 포함한다. 과거 Stage09 출력109개를 실행 전/후 별도 보관하고 원문으로 복원했으며 해시 차이0이다.

**기존 검사 계약 변경과 런타임 변경을 구분한다.** 실제 씬 검사에서 기존 고정 ResultPanel은 현재 HEAD의 프리팹에 없고 PopupService가 결과 팝업을 관리하는 것을 확인했다. 검사만 서비스의 실제 view/handle·중복 생성 방지·현재 결과 값을 확인하도록 바꿨다. 또 현재 HEAD의 RestartAsync는 비활성 상태에서 아무 작업을 하지 않고, 준비 후보 취소 시 기존 세션/아트/연출을 유지한다. 검사의 비활성 재시작 기대를 현재 계약으로 정정하고 유효 다시하기는 활성화 후 실행했다. 팝업/재시작 런타임 동작을 검사에 맞춰 수정하지 않았다. 실패 원문 `runs/hud-presenter-existing-progress-scene-20261006-114700`, `runs/hud-presenter-current-popup-contract-20261006-114957`을 보존했다. 변경 검사 파일은 `PuzzleProgressFeedbackVerification.Scene.cs`와 `.Lifetime.cs` 두 개다.

### C — 효과 슬롯 반환 첫 검사

기존 `PuzzleEffectSprite.Hide`는 비활성화만 했다. 실제 SpriteRenderer/SpriteMask/SortingGroup에 시트 이미지·색·회전·뒤집기·정렬·마스크 범위를 고의로 남긴 검사에서 Sprite/마스크 참조 반환이 실패했다 (`runs/effect-slot-reset-red-20261006-120521`, 실제 종료1). 슬롯 반환에 참조·표시·변환·색·정렬·마스크 초기화를 추가하고 다음 Paint에서 공통 클리핑 Sprite를 다시 연결한다. 슬롯이 아틀라스/외부 Sprite를 파기하지 않는다.

수정 후 **65 PASS/실제 종료0** (`runs/effect-slot-reset-green-20261006-120622`): 시트/단일 이미지 전환30회, 준비 용량 안 추가 객체 생성0, 반환 잔상0, 보드 Destroy의 슬롯/마스크 파기와 외부 Sprite 소유권 보존을 확인했다. 이는 독립 실제 렌더러 검사이며 세션 전체 취소/전환/Unload와 실제 파워 연출 검사를 대신하지 않는다.

효과 반환 수정 후 기존 실제 파워 씬 검사도 **826 PASS/실제 종료0**을 확인했다 (`runs/effect-slot-existing-power-scene-20261006-120715/after/scene-results.txt`). 기존 파워/조합·동시 낙하·드론·효과 취소와 반복 다시하기를 검사했다. 기존 Stage08 출력142개는 실행 전/후 원문을 보관하고 모두 원래 해시로 복원했다 (`restoration.json`: mismatchCount0). 기존 검사 소스를 추가로 변경하지 않았다. 이826개는 현재 효과 반환 수정의 관련 회귀이며 아직 최종 전체54종의 실행으로 집계하지 않는다.

이 절의 검사는 서로 다른 관련 체크포인트다. 최종 고정 소스 전체 회귀·전량 논리/보존 감사로 집계하지 않는다. A의 전체 표현 적용, B의 전체 소유권 수명, C의 공통 보드/공급/Playback 재사용, D의 봇 공개 특성, 마지막 전체 검증과5단계 인계 문서는 여전히 남아 있다.

후속 보존 감사에서 시작 manifest의 소스 제외6843개 파일의 해시 차이0과 work/시작 HEAD 유지를 확인했다 (`effect-slot-protection.json`). 신규 메타19개의 GUID도 Assets 전체 메타와 대조해 충돌0이다 (`effect-slot-guids.json`). 최초 GUID 검사기는 CRLF 끝을 받지 않아 자기 파일도 검색하지 못했다. 해당 실패 출력은 `effect-slot-guids-crlf-matcher-failure.json`으로 보존하고 검색 패턴만 CRLF/LF 모두 허용하도록 정정했다. GUID를 바꾸거나 메타를 재생성한 수정은 없다. 이 감사는 최종1489개/과거9137개 전체 감사와 별개다.

### C — 교환 연출의 실제 정의 보존

`SwapPresentationState`에서 Content setter가 정의 참조를 지워 커스텀 ID의 그림을 구형 기본 그림으로 바꿀 수 있었다. 표시용 실행 사본에 같은 일반 행동의 서로 다른 두 정의를 넣고 실제 교환 메서드로 검사했다. RED 실제 종료1은 `runs/swap-visual-id-copy-red-20261006-121413`이며, 교환 전에 정의 두 개를 보관하고 점유 종류 교환 후 함께 넘기는 생산 코드2줄로 수정했다. **3 PASS/실제 종료0** (`runs/swap-visual-id-green-20261006-121453`): 실제 정의 이동, 색 이동·덮개 고정, 원본·난수 무변경을 확인했다. 규칙 교환 알고리즘은 변경하지 않았다.

최초 검사 `runs/swap-visual-id-red-20261006-121319`는 Editor 어셈블리에서 내부 실행 사본 생성자에 직접 접근한 컴파일 오류였다. 이를 테스트의 리플렉션 경계로 정정한 뒤 위 동작 RED를 얻었으며, 생산 리플렉션을 추가하지 않았다. 두 실패 원문 모두 보존했다.

수정 후 팩2 에디터 요청으로 들어가는 기존 실제 파워 씬 검사도 **827 PASS/실제 종료0**이다 (`runs/swap-id-existing-element-power-scene-20261006-121540/after/scene-results.txt`). 효과 반환과 교환 정의 보존을 함께 포함한 현재 생산 소스로 파워/조합·교환·낙하·취소/다시하기를 확인했다. 기존 출력142개의 해시 복원 차이0이며 이 검사의 소스는 변경하지 않았다. 모든 이번 검사/감사 세션은 종료했고 전체 목표는 계속 진행 중이다.

### C — 공급 당시 정의·표시 수치·반환과 제거 비교

후속 native Play Mode 검사에서, 같은 일반 공급 행동의 두 신규 ID가 공급 연출 중 구형 토끼 그림으로 바뀌는 실제 실패를 재현했다 (`runs/supply-visual-red-20261006-121911`, 종료1). 정착이 끝나면 생성 칸은 비어 있으므로 최종 칸 상태를 다시 읽어서는 공급 당시 ID를 복원할 수 없다. `SettlementRecord`가 공급 당시 불변 규칙 정의를 내부 값으로 보존하며 `ElementVisualLookup.Supply`가 그 정의와 기록의 색/방향으로 조회한다. 규칙 정의에 아트 값을 넣거나 공개/저장 DTO·팩2를 변경하지 않았다.

`PuzzleWorldBoard.SupplyImage`의 종류별 그림 경로 계산을 제거하고 공통 시각 프레임의 크기·회전·오프셋·정렬을 적용한다. 마스크는 기존 공급 전용 쌍을 사용한다. 다음 실제 실패는 슬롯 반환에 Sprite/마스크/변환이 남는 문제였다 (`runs/supply-visual-return-red-20261006-122034`, 종료1). 임시 Snapshot Image의 Hide/Restore는 표시 참조와 축척·회전·색·뒤집기·order를 초기화하고, ReleaseClip은 마스크만 반환해 착지 그림을 유지한다. 다음 공급은 같은 마스크에 보드 소유 클리핑 Sprite를 다시 연결한다. 외부 아틀라스/이미지를 파기하지 않는다.

수정 후 **124 PASS/실제 종료0** (`runs/supply-visual-green-20261006-122156`)이다. 실제 두 신규 ID 공급, 등록 표시 수치, 반환20회/추가 생성0, 외부 아트 소유권·보드 Destroy의 마스크 파기를 확인했다.

같은 검사에 제거 연출 비교를 더하자, 바뀌지 않은 신규 ID 그림도 구형 기본 그림과 비교해 제거 대상으로 오인했다 (`runs/removal-visual-id-red-20261006-122323`, 종료1). `PuzzleBoardRemovalPlayback`의 비교 그림도 실제 시각 정의 조회로 연결했다. 이후 **125 PASS/실제 종료0** (`runs/supply-removal-visual-green-20261006-122415`)이다. 공급·제거의 조회를 고쳤으며 일반 블록 대체나 새 enum/ID 분기를 추가하지 않았다.

기존 낙하 관련 검사 `runs/supply-reset-existing-settlement-20261006-122455`는 비워진 렌더러에도 이전 축척/색/order를 요구하는 항목에서 실제 종료1이었다. 이를 아직 관련 회귀 통과로 집계하지 않는다. 원문 출력은 실행 전/후 보관 후 복원했다. 빈칸 반환 자체도 별도 고의 오염 검사에서 축척·뒤집기가 남는 실패를 확인했다 (`runs/empty-cell-slot-red-20261006-122705`, 종료1). 현재 빈 슬롯 초기화와 기존 검사 계약 차이를 구분하는 진단을 진행한다. 전체 목표·최종 검증은 계속 미완료다.

#### 빈 슬롯 초기화와 기존 검사 계약의 차이

진단 실행 `runs/settlement-empty-slot-diagnosis-20261006-122754`의 실제 실패 슬롯은 `Content sprite=null`, 축척/색은 이전과 같고 order만0/10이었다. A의 데이터 Draw가 비워진 슬롯의 order를0으로 초기화하는 기존 변화이며 낙하 경로/속도 문제가 아니었다. 진단 원문과 실제 종료1을 보존했다. 한편 별도 고의 오염 검사에서 증명한 빈 슬롯 축척·뒤집기 잔류는 실제 초기화 누락이다. `PuzzleCellView.Set`은 Sprite가 없으면 축척1을, SetVisual은 뒤집기false를 적용하도록 생산 코드2줄을 수정했다.

신규 검사 최종 **126 PASS/실제 종료0** (`runs/supply-empty-removal-green-20261006-122938`)은 공급 당시 두 ID·메타데이터·풀 반환/마스크/Destroy·빈 슬롯 반환·제거 비교를 함께 확인했다. 최종 전체 회귀로 집계하지 않는다.

기존 낙하 검사의 `.Routes.cs`는 활성 그림에는 이전 복원 조건을 그대로 요구하고, Sprite가 없는 슬롯에는 비활성/축척1/흰색/order0/뒤집기false를 요구하도록 정정했다. 이전 그림 없는 슬롯까지 order10을 유지시키기 위해 생산 코드를 되돌리지 않았다. `.cs`의 제거 종료 확인도 같은 빈 슬롯 계약으로 분리했다. 이 두 테스트 파일 변경과 빈 슬롯 생산 코드2줄을 구분한다. 제거 종료의 과거 축척 기대 실패는 `runs/settlement-slot-return-contract-20261006-123033`에 보존했다.

정정 후 기존 낙하·공급·포털 관련 검사는 **280 PASS/실제 종료0** (`runs/settlement-slot-contract-green-20261006-123209/after/results.txt`)이다. 동시 출발·속도, 신규 대각선·경로/포털, 공급 마스크의 실제 픽셀 격리, pause·취소/오류, 원본/난수/공급/회수/연쇄의 직접 실행기 비교를 포함한다. 각 실행의 기존 출력95개를 원문으로 복원했고 mismatchCount0이다. 실제 낙하 규칙·속도·동시 실행을 이 검사 기대에 맞춰 바꾸지 않았다.

동일한 현재 소스로 기존 팩2 파워 씬 검사도 **827 PASS/실제 종료0**이다 (`runs/supply-slot-existing-element-power-scene-20261006-123318/after/scene-results.txt`). 기존 파워/조합·드론·취소/다시하기를 유지했고 출력142개 복원 차이0을 확인했다. 신규 메타20개 GUID 충돌0 (`supply-visual-guids.json`), work/시작 HEAD 유지와 diff 공백 검사 종료0이다. 이번 검사들은 모두 종료했다. 전체 표현 수치/시트/효과 적용과 자원 소유권·풀의 나머지 경계, 봇 공개 행동 특성, 최종 전체 회귀와 전량 보존 감사·5단계 문서는 여전히 남아 있다.

### A/B — 시트 프레임과 편집·시험 표시 수치

기존 세 보드 검사의 별도 Metadata 진입을 추가했다. 같은 내구도 행동의 새 ID 중 하나에 기존 드론 시트의 프레임3·피벗(.25,.75)·크기1.2·오프셋(.1,-.15)·회전32도·order47을 등록하고, 같은 칸의 덮개 order20과 비교한다. 실제 SpriteRenderer/UI Toolkit 바인딩과 native 자원을 사용하며 이미지 원본은 수정하지 않는다.

최초 RED는 시트 전체를 반환하는 실제 실패였다 (`runs/visual-sheet-metadata-red-20261006-124326`, 종료1). `BoardSpriteAtlas.GetFrame`으로 같은 아틀라스 텍스처의 프레임 Sprite를 소유자 안에서 준비/캐시/반환한다. 별도 이미지 로드·텍스처 복제·정의별 풀을 만들지 않는다. `PuzzleArtwork.GetVisual`과 편집/시험의 `LevelBoardArtwork.Visual`이 이 프레임을 사용한다. 리소스 계획은 방문한 시각 정의의 프레임도 준비하며, 준비 실패는 해당 ID/경로/프레임으로 보고한다. 기존 pending/취소/아틀라스 주소 분할은 유지한다.

중간 실패 두 실행 (`runs/visual-frame-applied-metadata-red-20261006-124542`, `runs/visual-native-frame-geometry-diagnosis-20261006-124815`)을 통해 native 시트의 원본 rect는512×512이나 textureRect는 메시 여백을 제외한503.85×485.90임을 확인했다. textureRect만 사등분하면 프레임 경계가 틀린다. 원본 정점/UV 대응에서 회전 없는 전체 시트 사각형을 복원해 원래256×256 경계를 유지했다. 검사 기대 크기를 잘린 메시 크기로 완화하거나 이미지/임포트를 수정하지 않았다. 현재 아틀라스의 회전/타이트 패킹 금지는 원본 설정 그대로이며, 지원되지 않는 패킹은 준비 오류로 처리한다.

프레임 연결 후 다음 RED는 편집 보드의 피벗·회전·오프셋 미적용이다 (`runs/visual-sheet-margin-metadata-red-20261006-125103`, 종료1). 공통 Editor `ElementVisualStyle`이 UI의 y/회전 방향으로 값을 변환하고, Sprite 기준 피벗과 등록 피벗 차이·크기·정렬을 적용한다. 내용물뿐 아니라 먼지/덮개도 같은 수치를 사용한다. 플레이 테스트의1칸 장애물 크기를 무시하던 경계도 수정했다.2칸 이상은 기존 전체 표시 영역에 크기를 중복 적용하지 않는다. uGUI/월드 프리팹과 팔레트 디자인은 변경하지 않았다.

- 첫 메타데이터 GREEN: `runs/visual-sheet-metadata-green-20261006-125313`, 실제 종료0.
- 행/열 선택·소유권 강화 후 **26 PASS/실제 종료0**: `runs/visual-frame-ownership-green-20261006-125734`. 네 프레임의 실제 크기/행·열/동일 텍스처/캐시, 두 아트 소유자의 독립 Sprite와 한 소유자 Dispose 후 다른 프레임 유지, 실제 세 보드의 시트·피벗·크기·회전·order·별칭, 원본/팩2/지문/난수 독립성을 확인했다.
- 현재 프레임 준비를 포함한 native 자원 계획: **98 PASS/실제 종료0**, `runs/frame-cache-resource-plan-green-20261006-130005`. 요청 주소 누락0/미사용 별도 종류0·재준비 추가 주소0·핸들 반환을 유지한다.
- 원래 세 보드 진입: **11 PASS/실제 종료0**, `runs/frame-cache-existing-board-green-20261006-130735`.

이 검사는 native Sprite/스타일 바인딩과 자원 소유권의 관련 검증이다. 모든 GPU 픽셀/실기기/플레이어 빌드에서의 패킹을 검증했다고 확대하지 않는다.2×2의 서로 다른 UI 부모 사이 정렬, 구형 편집 보드 호환 조회, 효과 Playback의 등록 데이터 연결·풀 수명 나머지, 봇 공개 특성과 최종 전체 검증은 남아 있다. 최종 전체54종의 완료 결과로 집계하지 않는다.

### B — 실제 도달 상태의 자원과 종료 공급

시트 연결 이후 기존 팩2 파워 씬 검사는 **827 PASS/0 FAIL·실제 종료0** (`runs/frame-cache-existing-element-power-scene-20261006-131504`)이다. 과거 출력142개 복원 차이0을 확인했다. 이는 이후 자원 준비 범위 수정 전 체크포인트다.

자원 계획이 방문한 정의의 모든 등록 상태를 수집하던 문제를 실제 실패로 확인했다. 현재 내구도2인 정의에 내구도13 전용 이미지/효과를 함께 등록하면 사용하지 않는 주소도 준비했다 (`runs/reachable-frame-plan-red-compiled-20261006-132119`, 실제 종료1). 상태 검사에서 실제 선택된 프레임을 계획으로 전달하도록 최소 연결했다. 현재/피해 후 상태·공급·충전·파워 프레임·생성 참조 검사는 유지하며, 불변 카탈로그 자체의 등록 상태를 삭제하지 않는다. 앞 절의 ‘방문한 시각 정의의 프레임 준비’ 구현은 이제 실제 도달 상태가 선택한 프레임 준비로 바뀌었다.

첫 수정은 **10 PASS/실제 종료0** (`runs/reachable-frame-plan-green-20261006-132206`)이다. native 검사에도 생성 정의의 도달 불가능한 내구도13을 별도 Generator 아틀라스로 등록했다. 필요한 생성/공급/효과와 순환은 유지하면서 해당 별도 주소를 요청하지 않았다 (`runs/reachable-frame-native-plan-green-20261006-132303`, **98 PASS/실제 종료0**).

반대로 기존 라스트팡 공급은 레벨 팔레트와 관계없이 다섯 색을 사용한다. 네 색 레벨에 생성구를 두고 다섯 번째 시각 매핑을 제거했을 때 시작 전 거절하지 않는 실패를 확인했다 (`runs/last-pang-source-four-color-plan-red-20261006-132659`, 실제 종료1). 생성구가 있는 레벨은 기존 종료 공급의 다섯 색을 상태 검사/자원 계획에 포함한다. 종료 공급 규칙·색 추첨·난수·저장 계약은 변경하지 않았다.

- 최종 준비 검사: **13 PASS/0 FAIL·실제 종료0**, `runs/reachable-and-last-pang-source-plan-green-20261006-132749`. 도달 불가 이미지/효과/프레임 제외, 현재/피해 후 이미지 포함, 누락 내구도/로켓/드론 상태 오류, 네 색+생성구의 다섯 번째 색 누락 오류와 준비를 확인했다.
- 최종 native 검사: **99 PASS/0 FAIL·실제 종료0**, `runs/reachable-and-last-pang-native-plan-green-20261006-132835`. 실제 네 색 레벨에서 다섯 번째 종료 공급 Sprite도 준비하고 도달 불가 Generator 주소는 제외했다. 필요한 주소 누락0·미사용 별도 종류 요청0·재준비 추가 주소0·Dispose 핸들 반환을 확인했다. 공유 아틀라스 동반 이미지는 별도 주소 요청으로 세지 않는다.
- 테스트 준비 오류도 원문으로 보존했다. 초기 프레임 API와 색 enum 이름 오류는 컴파일 실패였으며 제품 실패로 집계하지 않는다. 종료 공급의 초기 입력은 기본 다섯 색이라 이미 거절됐고, 다음 입력은 생성구가 없어 종료 공급 조건에 해당하지 않았다. 유효한 네 색+생성구 입력으로 정정한 RED/GREEN을 위 근거로 사용한다. `runs/reachable-frame-plan-red-20261006-132043`, `runs/last-pang-color-plan-red-20261006-132401`, `runs/last-pang-color-plan-red-compiled-20261006-132436`, `runs/last-pang-four-color-plan-red-20261006-132514`, `runs/reachable-and-last-pang-plan-green-20261006-132557`을 삭제하지 않았다.

신규 메타21개 GUID 충돌/빈 GUID0 (`frame-resource-guids.json`), work/시작 HEAD 유지와 diff 공백 검사 종료0을 확인했다. 이번에는 관련 준비/native 검사만 실행했으며 전체54종은 반복하지 않았다. 앞 절에 적힌 남은 A/C/D와 최종 전량 감사·5단계 문서는 여전히 미완료다.

### A — 구형 편집 보드의 같은 시각 조회

스키마4 편집 보드가 제작 시각 별칭을 무시하고 종류별 경로를 직접 계산하는 실패를 native 보드 검사에 추가했다 (`runs/legacy-editor-visual-red-20261006-133220`, 실제 종료1). 별도 마이그레이션이나 디스크 저장 없이 기존 `LegacyElementLevelAdapter.Preview`의 영구 ID/배치 값을 동일 카탈로그와 `ElementVisualLookup.Placement`에 전달한다. 구형 종류는 이미 존재하는 어댑터에서만 ID로 변환하며 새 ID/표현 분기는 추가하지 않았다.

구형 일반 블록·파워·장애물·덮개·먼지·회수 부품의 그림과 2×2 본체도 등록 프레임에서 선택한다. 기존 편집 입력·선택·포털·진단·팔레트와 라벨 코드는 유지한다. 등록 시각 상태가 없으면 해당 칸/본체의 툴팁에 ID/상태 오류를 표시하고 그림은 비우며, 구형 이미지로 대체하지 않는다. 동일 `ElementVisualStyle`로 크기·피벗·오프셋·회전·정렬을 적용한다. 기존 종류별 `LevelBoardArtwork` 메서드는 팔레트/다른 기존 호출부 때문에 남겨 두었지만 이 편집 보드의 요소 이미지는 사용하지 않는다.

- 최초 연결 후 **13 PASS/실제 종료0**, `runs/legacy-editor-visual-green-20261006-133331`.
- 구형 2×2와 재사용 검사를 강화한 뒤 **16 PASS/0 FAIL·실제 종료0**, `runs/legacy-editor-visual-return-green-20261006-133535`. 실제 native Sprite의 다섯 층 별칭 선택·등록 크기/회전, 2×2 전체 크기, 누락 상태의 ID 오류, 원본/팩 무변경을 확인했다. 제작 별칭을 제거하고 같은 보드를 다시 그리면 기본 가로 로켓1.12·2×2 크기2.16으로 돌아가며 이전 회전이 남지 않는다.
- 기존 `RabbitArtworkVerification.Run`도 **22 PASS/0 FAIL·실제 종료0**이다 (`runs/legacy-editor-existing-rabbit-green-20261006-133643`). 과거 출력 파일은 실행 후 원문으로 복원했다. 기존 검사 소스나 기대를 변경하지 않았다.

구형 편집 조회 연결은 완료했지만, 서로 다른 UI 부모에 있는 2×2 본체/덮개의 데이터 정렬 문제는 아직 남아 있다. 효과 Playback의 등록 데이터 연결·실제 풀 취소/전환/Unload 수명, 봇 공개 특성과 최종54종/전량 보존 감사·5단계 문서도 미완료다. 관련 검사만 실행했고 전체 회귀를 반복하지 않았다. 모든 이번 Unity 검사 프로세스는 종료했다. 빌드·씬 저장·콘텐츠 빌드·팩 재생성·커밋·푸시는 하지 않았다.

### D — 본체의 공개 행동 특성과 소비 경계

봇이 신규 정의의 피해 허용·색 조건·칸별 집계·제거 미션을 잃고 구형 종류로 추론하던 문제를 실패 검사부터 연결했다. 공개 본체에는 네 원인 허용 여부, 인접 색 조건, 칸별 집계, 충전 여부/타격당 충전, 논리 크기, 제거 미션을 읽기 전용 primitive/enum 값으로 복사한다. 실제 정의 객체/ID/제작명/최대 허용치/원본 참조는 전달하거나 보관하지 않는다. 현재 내구도·색·충전·공개 연결과 좌표 기반 키는 기존 계약을 유지한다.

`BotMissionEvaluation`은 복사된 허용·색·집계·충전·미션 값을 소비한다. 기존 점수 가중치나 탐색 전략을 개선하지 않는다. 기존 자석 인접 평가에서 충전 본체를 제외하던 보수적 범위도 유지했다. `PlanningBranch`는 공개 값만으로 자기 가정 내부의 본체 규칙을 만든다. 실제 정의와 공급/난수는 받지 않고, 내부의 합성 ID는 원본 콘텐츠 ID가 아니다. 구형 행동과 같은 공개 값의 시드 정규화는 유지하고, 다른 공개 행동 값만 추가한다. 모든 구형 시드/실기기 성능에 대한 전량 증명은 최종 감사에서 구분한다.

실패/수정 경로는 모두 원문으로 남겼다.

- 공개 필드 누락: `runs/bot-public-traits-red-compiled-20261006-134341`, 실제 종료1. 초기 `bot-public-traits-red-20261006-134213`은 검사 전용 internal 접근 컴파일 오류이며 제품 실패로 세지 않는다.
- 값 복사 GREEN: `runs/bot-public-traits-copy-green-20261006-134427`.
- 후보 평가가 인접 매칭 거부를 무시: `runs/bot-public-traits-consumer-red-20261006-134608`, 실제 종료1.
- 후보 평가 연결 후 가정 실행이 구형 기본 행동으로 복귀: `runs/bot-public-traits-planning-red-20261006-134714`, 실제 종료1.
- 소비 연결 GREEN: `runs/bot-public-traits-consumer-green-20261006-134819`, **39 PASS/실제 종료0**.
- 기존 값 생성자 회귀: `runs/bot-public-existing-score-green-20261006-135104`, 실제 종료1/생성자 누락. 기존 검사가 사용하는 구형8인자 생성자를 호환 경계로 유지했다. 실제 상태 관찰 경로는 새 primitive 값 생성자를 사용한다. 검사 기대/소스는 바꾸지 않았다.
- 제거 이력/인덱스 비교 강화: `runs/bot-public-history-pair-green-20261006-135439`, **40 PASS/실제 종료0**.
- 비교 실행 후 현재 소스 복원과 시드 동일성 강화: `runs/bot-public-restored-current-green-20261006-140219`, **41 PASS/0 FAIL·실제 종료0**. 신규 ID·이름·최대 허용치·인스턴스 ID·실제 제거 이력/내부 인덱스·난수 소비가 다른 두 상태의 관찰/실제 후보/점수/선택/가정 시드/가정 관찰 동일, 이전 관찰 독립, 원본/정의/난수 무변경과 정의 객체 참조 부재를 확인했다.

기존 관련 검사도 별도 실행했다.

| 검사 | 실제 결과 | 근거 |
| --- | --- | --- |
| 공개 관찰·숨은 내용/공급/커서 차단 | 32 PASS/0 FAIL·종료0 | `runs/bot-public-existing-observation-green-20261006-135015` |
| 기존 후보 점수·이유 | 50 PASS/0 FAIL·종료0 | `runs/bot-public-existing-score-compatible-green-20261006-135217` |
| 대표 본체/발전기/덮개/먼지의 공개 재구성·실제 규칙 피해 비교 | 45 PASS/0 FAIL·종료0 | `runs/bot-public-existing-planning-rules-green-20261006-135306` |

기존 BotBasic 출력60개와 BotPlanning 출력107개를 각 실행 뒤 원문으로 복원했고 차이0이다. 신규 메타22개 GUID 충돌/빈 GUID0 (`bot-public-guids.json`)이며 work/시작 HEAD와 diff 공백 검사를 유지했다. 저장 스키마/팩·콘텐츠·시각 디자인·로딩 정책·전략 가중치·공통 규칙 실행은 변경하지 않았다.

추가 `PlanningVerification.Boundaries`는12항목 이후, 과거10×10 판에서 자연적으로 가정 표본이 모두 실패해야 한다는 기대에서 실패했다 (`runs/bot-public-existing-planning-boundaries-green-20261006-135609`, 종료1). 실제 현재 입력은9×9이며 표본3개가 정상 완료되고 거절0이다. 이번 봇 변경 네 파일을 시작 HEAD의 코드로 비교해도 같은 실패와 같은 선택 파일 해시였다 (`runs/bot-boundary-unchanged-head-runtime-baseline-20261006-140041`, 종료1). 이는 이번 소비 연결 회귀로 처리하지 않았고 검사 기대나 실제 탐색을 억지로 변경하지 않았다. 비교 전 현재 소스를 바이트로 백업하고 실행 후 네 파일 해시 차이0으로 복원했다 (`bot-boundary-before-runtime-20261006-140040/restoration.json`). 첫 비교 시 git blob 출력 도구가 빈 파일을 만들었던 컴파일 실패도 별도 보존했으며, 해당 실행에서도 원래 소스 네 파일을 정확히 복원했다. 유효한 HEAD 비교 증거와 혼동하지 않는다.

본체 공개 행동 연결은 진행했지만 덮개/먼지의 추가 공개 행동 값과 가정 소비 연결은 아직 남아 있다. 2×2 UI 부모 간 정렬·효과 Playback·실제 풀 수명·최종54종/전량 감사·5단계 문서도 미완료다. 추가 경계 검사의 기존 기대 불일치를 전체 통과로 세지 않는다. 이번에는 관련 검사만 실행했고 전체 회귀는 반복하지 않았다. 모든 이번 Unity/비교 프로세스는 종료했다.

### D — 덮개·먼지 공개 행동 값과 소비자 연결 (2026-10-06 23:20 KST)

`BotCell`에 덮개 피해량/내구도형 여부/제거 미션/번식 여부/번식 초기 내구도와 먼지 피해량/제거 미션을 읽기 전용 값으로 추가했다. 실행 정의는 Builder 경계에서만 읽고 DTO에 객체나 ID를 보관하지 않는다. 기존 9인자 생성자는 구형 공개 값 호환을 유지한다. PlanningBranch는 공개 값만으로 독립적인 가정 층 정의를 구성하며, 구형 기본 값과 달라진 공개 값만 가정 시드에 추가한다. 미션 평가는 기존 완료8점/진행 점수 체계를 유지하면서 등록 피해량을 사용한다.

검사 순서와 원문은 `Logs/ElementFramework/Phase04/runs`에 보존했다. `bot-public-layer-copy-20261006-141258`은 공개 값 복사는 통과했으나 가정 생성이 구형 기본 값을 사용하여 실제 종료1이었다. 이후 가정 생성/미션 평가를 연결했다. `bot-public-layer-consumers-20261006-141400`, `bot-public-layer-horizontal-20261006-141503`, `bot-public-layer-diagnostic-20261006-141557`, `bot-public-layer-action-diagnostic-20261006-141644`의 실패는 테스트 입력 문제로 구분했다. 처음 로켓 방향을 명시하지 않았고, 방향 명시 후에도 실제 판의 초기 매칭 때문에 StartNotSatisfied로 거절되었다. 런타임 시작 조건을 완화하지 않고 테스트에서 가로 로켓과 유효 시작판을 확보했다. 실제/가정 피해 비교의 합격 조건은 유지했다.

- `bot-public-layer-valid-start-20261006-141743`: 새 층 검사20 PASS, FAIL0, Unity 실제 종료0. 서로 다른 덮개/먼지/곰팡이 ID의 동일 공개 관찰, 번식 초기 내구도2와 숨은 내용 은폐, 가정 관찰 보존, 피해량2의 미션 평가, 원본/난수 무변경, 실제/가정 로켓의 덮개·먼지 내구도2 제거를 확인했다. 번식 초기값의 실제 턴 종료 생성까지 이 검사로 검증했다고 확대하지 않는다.
- `bot-public-body-after-layers-20261006-141828`: 본체 공개 경계41 PASS, FAIL0, 실제 종료0.
- `bot-layer-existing-score-20261006-141903`: 기존 점수50 PASS, 실제 종료0. 다른 보존 출력은 이번 실행의 신규 PASS로 합산하지 않는다.
- `bot-layer-existing-planning-rules-20261006-141934`: 기존 가정 규칙45 PASS, 실제 종료0. 과거 봇 출력107개 복원 해시 차이0. 기존 점수 출력 보호60개는 해당 실행의 restoration.json으로 별도 보존했다.

work/HEAD b03ac0539e0286bd67763c88fcbd16f8316ac452를 유지했다. 전체4단계는 진행 중이다. A의2×2 부모 간 정렬/효과 표현, C의 실제 취소·재시작·전환·Destroy/Unload 수명, 최종54종+신규/전량 보존 감사 및5단계 인계 문서가 남아 있다. 기존 Planning Boundaries의 구형10×10 전제 실패에 관한 이전 기록도 그대로 유효하다.

### A/C/D — 공개 번식 검증과 로켓·드론 등록 프레임 재생 (2026-10-06 23:30 KST)

D의 신규 공개 층 검사를 확장했다. `bot-public-layer-turn-spread-20261006-142147`은42 PASS/FAIL0/실제 종료0이다. 실제 판과 공개 값으로 생성한 가정 판의 턴 종료 모두 초기 내구도2로 번식하고, 생성된 층에 제거 미션/다음 번식 값이 유지된다. 정의 ID만 다른 동일 공개 입력의 가정 시드 동일, BotCell에 정의 객체 참조 없음, 실제/가정 실행 후 과거 DTO와 비교 원본 독립도 확인했다. 규칙 난수의 실제/가정 값이 같다고 주장하지 않는다.

`PuzzleEffectSprite.PaintVisual`은 기존 공통 슬롯에서 등록 프레임의 크기·피벗·오프셋·회전·order를 사용한다. 이동하는 본체는 기존 비행 우선순위45를 하한으로 유지하며 그보다 높은 등록 order를 반영한다. 단일 Sprite 프레임은 기존 아틀라스 소유자의 GetVisual 캐시를 사용하고 반환은 기존 Hide 초기화 경로를 유지한다. `effect-visual-metadata-red-20261006-142332`은 실제 종료1로 미연결을 확인했고, `effect-visual-metadata-green-20261006-142425`은69 PASS/FAIL0/실제 종료0으로 적용과 반환을 확인했다. 테스트의 회전 확인은 부모 이동 방향과 자식 시각 회전의 합을 검사하도록 작성했다.

`PuzzlePowerPlayback`의 로켓/드론 비행 클립에 불변 VisualFrames를 연결했다. `ElementVisualLookup.Animation`은 원본 셀의 실제 ContentElement를 사용한다. 조합으로 생성되어 원본 셀이 해당 파워가 아닌 경우에만 기존 생성 파워의 명시적 구형 ID를 사용한다. 신규 ID가 미등록일 때 구형 그림으로 대체하지 않는다. 로켓4컷의 경로·가로 비율/프레임별 중심 보정과 드론4컷의 시트 선택을 등록 데이터로 조회하며, 기존0.06초 발사 준비/프레임 시간과 드론 상승·호버·돌진 위치 계산은 유지한다. 제거된 로켓 전용 오프셋 배열과 시트 클립 필드는 정리했다. 폭발·타격·충전 등 일반 효과는 아직 기존 경로 선택 코드가 남아 있어 전체 효과 데이터 연결 완료로 보지 않는다.

- `flight-visual-clips-red-20261006-142558`: 신규 로켓 실제 발동은 유효했으나 등록 프레임 보관이 없어 실제 종료1. 실패 원문 보존.
- `flight-visual-clips-green-20261006-142730`: 실제 신규 로켓/드론 정의 ID별 그림/크기/order 선택14 PASS/FAIL0/실제 종료0. 이 검사는 클립 생성 검사이며 native 자원 로드/재생 검증으로 확대하지 않는다.
- `registered-flight-existing-power-scene-20261006-142828`: 잘못 지정한 존재하지 않는 검사 진입점으로 실제 종료1. 런타임 실패가 아닌 실행 명령 오류로 구분하여 보존했다.
- 확인한 진입점 RunElementScene의 `registered-flight-existing-element-scene-20261006-142904`: 실제 Play Mode827 PASS/FAIL0/종료0. 현재 등록 프레임 소비 코드에서 기존 요소 파워 씬의 재시작/취소/잔상 검사를 수행했고 기존 Stage08 원문142개 복원 차이0이다. 신규 커스텀 비행 프레임을 native 로드/재생하는 전체 수명 검증은 별도 남았다.

전체4단계 완료는 아직 미입증이다. A의2×2 부모 간 정렬, 일반 효과의 등록 선택, B/C의 native pending·취소·전환·Destroy/Unload 전체 경계, 최종54종+신규/전량 논리·원본·팩·GUID·과거 증거 감사,5단계 문서를 계속 진행한다. 빌드/콘텐츠 빌드/커밋/푸시/하위 에이전트는 수행하지 않았다.

### A/B/C — 신규 비행 프레임의 native 준비·재생·반환 (2026-10-06 23:40 KST)

ElementVisualBoardVerification.RunFlights를 기존 native Play Mode 검사에 추가했다. 테스트는 메모리의 스키마5 레벨에 서로 다른 신규 로켓/드론 정의를 실제 배치하고, 각 프레임에 서로 다른 기존 Crate/Scrap 이미지와 크기·피벗·오프셋·회전·order를 등록한다. 원본 에셋/이미지는 수정하지 않는다. 기존 프리팹의 공통 효과 슬롯을 사전 확보한 뒤 실제 PrepareAsync/Begin/Tick/Reset을 사용한다.

`custom-flight-native-red-20261006-143342`은 스키마4 실행기가 전달된 신규 카탈로그를 사용하지 않는 상태에 테스트가 직접 ID를 끼워 넣어 KeyNotFoundException으로 종료1이었다. 이는 잘못된 검사 입력으로 기록한다. 테스트를 실제 메모리 스키마5 마이그레이션/배치 경로로 수정한 `custom-flight-native-valid-red-20261006-143447`에서는 입력 유효 후 **초기 신규 파워의 모든 비행 프레임 준비 누락**으로 실제 종료1을 확인했다. 최초 실패를 유효한 RED 증거로 확대하지 않는다.

ElementVisualPreparation은 초기 셀의 실제 정의가 Power 공급 행동인 경우 해당 파워의 발사 프레임 전체를 준비한다. 로켓의 조합에서 가능한 방향도 기존 Power 상태 검사를 재사용한다. 구형 기본 파워 집합만 준비하고 신규 초기 ID는 정지 그림만 준비하던 구멍을 닫았으며, 실행 규칙/난수/저장 형식은 변경하지 않았다.

- `custom-flight-native-green-20261006-143537`: 초기 신규 파워의 모든 프레임 준비, 실제 native 주소 로드/등록 그림·회전·피벗·오프셋·order 표시, 각5회 취소·반환/재준비 중 객체/주소 증가0, 보드 Destroy 확인. 실제 종료0.
- `custom-flight-native-completion-20261006-143704`: 위 검사를 포함해 **57 PASS/FAIL0/실제 종료0**. Tick 시간 진행 중 등록된 두 그림 교대, 정상 종료 후 효과 Sprite 잔상0/용량 유지, Destroy 후 renderer 소멸, 아트 Dispose 후 모든 캡처한 비행 Sprite 파기와 AtlasCount0까지 확인했다. 이 테스트가 취소한 것은 준비 완료된 재생이며 pending 취소/씬 Unload까지 다룬다고 확대하지 않는다.
- `initial-power-frame-preparation-regression-20261006-143801`: 기존 준비13 PASS/FAIL0/실제 종료0. 누락 로켓3/드론4 프레임의 시작 전 ID 오류, 네 색 레벨의 라스트팡 다섯 번째 색 준비 보존.
- `initial-power-native-resource-regression-20261006-143922`: 기존 native 자원 계획99 PASS/FAIL0/실제 종료0. 실제 주소/위치/Sprite 목록을 실행 폴더에 보존했고 별도 주소 추가0/필수 주소 누락0/Dispose 반환 계약을 확인했다.

이전 목표 턴은 등록 프레임 재생 연결과 관련 검증으로 진행이 있었으며, 이번 턴도 실행 준비 누락의 재현·수정·native 검증으로 진행했다. 모든 시작한 Unity 검사 프로세스가 실제 종료했다. 전체4단계는 진행 중이며 일반 효과 등록 선택과2×2 부모 간 정렬, native pending/전환/Unload 전체 경계, 최종54종+신규/전량 보존 감사 및5단계 문서가 남아 있다. 전체 회귀를 반복하거나 커밋/빌드를 실행하지 않았다.

### A/B/C — 불변 효과 애니메이션 등록과 본체 타격·충전 연결 (2026-10-06 23:54 KST)

기존 표현 DTO에 ElementVisualEffectDto(key, frames)를 추가했다. 상태별 effectAnimations는 기존 ElementVisualFrameDto 프레임 값을 재사용한다. 원본/배포 배열을 깊게 복사한 읽기 전용 효과 키→프레임 목록이며, 효과 안에서 또 효과를 생성하는 중첩은 허용하지 않는다. ResolveEffect는 실제 ID/상태/효과 키로 조회하고 누락 시 해당 ID 오류를 반환한다. 범용 정책 언어나 새 패키지/asmdef를 도입하지 않았다. 기존 effects 주소 목록은 호환성을 유지하며 등록 프레임 주소도 준비 집합에 포함한다. ElementVisualPreparation의 선택 콜백은 효과 프레임의 시트/피벗 값까지 기존 native 프레임 준비에 전달한다.

LegacyElementVisuals의 호환 등록 경계에서 match/creation/trail/impact/blast/pull/transform/damage/charge/clear 이벤트의 기존 경로와 order40을 등록했다. 종류/경로 선택은 이 등록 경계에 두고, 새 ID는 직접 효과 키를 등록한다. PuzzlePowerPlayback의 본체 손상·충전·연결 해제 클립은 실제 body.Element와 선택한 상태의 effectAnimations를 사용한다. 본체의 논리 크기도 정의에서 읽으며 기존 물리적 연출 범위/시간은 유지한다. 재생에는 이전 PaintVisual/GetVisual 경로를 사용한다. 진단용 본체 손상 클립 이름은 Body-damage이고 충전은 Generator-charge를 유지한다.

- `effect-registration-red-20261006-144232`: 실제 종료1, 효과 키 조회 경로 부재 확인.
- `effect-registration-green-20261006-144353`: 효과 불변 등록/공유 별칭/배포 왕복/누락 키·값 검사 종료0.
- `legacy-effect-registration-red-20261006-144508`: 기존 본체의 damage 키 미등록 오류로 종료1.
- `legacy-effect-registration-green-20261006-144623`:11 PASS/FAIL0/종료0. 구형 본체 타격4컷/기존 WoodBreak 경로/order40 보존 포함.
- 타격 클립 테스트의 초기 `registered-damage-clips-red-20261006-144817`은 테스트 namespace 참조 컴파일 오류, `registered-damage-clips-valid-red-20261006-144904`는 기존 블록을 지우지 않아 본체 배치가 없는 잘못된 입력이었다. 둘 다 원문 보존하며 동작 RED로 확대하지 않는다.
- `registered-damage-clips-placement-red-20261006-144948`: 실제 스키마5 본체 신규 ID 입력과 로켓 발동이 유효했으나 등록한 타격 효과를 사용하지 않아 종료1. 유효 실패 증거다.
- `registered-damage-clips-green-20261006-145105`:8 PASS/FAIL0/종료0. 같은 행동의 두 신규 본체 ID가 실제 타격 클립에서 각각 다른 등록 이미지와 order48을 선택한다. 클립 생성 검사이며 신규 효과의 native 재생까지 확인했다고 확대하지 않는다.
- `registered-body-effects-existing-scene-20261006-145205`: 실제 Play Mode827 PASS/FAIL0/종료0. Stage08 기존 원문142개 복원 차이0.
- `registered-effects-existing-visual-values-20261006-145304`: 기존 시각 카탈로그/상태 오류/불변 값 검사 종료0. 신규 effectAnimations 때문에 구형 effects 목록의 읽기 전용 계약이 바뀌지 않는지 확인했다.

일반 매칭·생성·폭발·자석·드론 타격·덮개/먼지 제거의 재생 호출부는 아직 AddEffect의 기존 경로 계산을 사용하므로 일반 효과 전체 연결 완료가 아니다. 등록된 효과 키가 실제 행동에 필요한지 시작 전에 검사하는 경계도 남아 있다.2×2 부모 간 정렬, native pending/취소/전환/Unload 전체 경계, 최종54종+신규/전량 원본·팩·GUID·증거 감사와5단계 문서를 계속 진행한다. work/현재 HEAD·WIP를 보존했고 빌드/커밋/푸시/하위 에이전트를 실행하지 않았다.

### A/B/C — 일반 효과 재생을 등록 데이터로 전환 (2026-10-07 00:04 KST)

PuzzlePowerPlayback.Clips에서 매칭·파워 생성·덮개/먼지 제거·자석 변환/끌어당김·드론 타격·폭탄 폭발·로켓 궤적/타격의 AddEffect 경로 조합을 제거하고 기존 AddRegisteredEffect로 연결했다. 실제 셀/본체 정의의 선택 상태에서 effectAnimations 키를 조회한다. 조합에 포함된 기존 파워는 원본/조합 두 입력 셀의 실제 ID를 먼저 조회하며, 원본에 해당 파워가 없어 조합으로 생성된 경우만 명시적 기본 생성 정의를 사용한다. 신규 실제 ID의 누락을 구형 효과로 숨기지 않는다. 매칭/생성은 기록의 OriginalColor를 사용하고, 층은 실제 CoverElement/DustElement를 사용한다. 범위·위치·시간·규칙 기록과 드론 이동 계산은 유지한다. 진단용 일반 매칭 클립은 색 이름을 붙이는 대신 match로 표시한다.

- `registered-power-impacts-red-20261006-145601`: 유효 신규 로켓 입력에서 기존4컷 경로가 등록한1컷과 달라 검사 Single이 실패했다. `registered-power-impacts-explicit-red-20261006-145644`는 동일 기대값을 길이/경로 조건으로 명시해 실제 미연결 FAIL/종료1을 확보했다. 기대 결과를 완화한 것이 아니다.
- `registered-power-impacts-green-20261006-145837`:16 PASS/FAIL0/실제 종료0. 실제 신규 로켓/드론의 비행뿐 아니라 타격 효과도 해당 원본 ID의 등록 이미지와 order48을 선택한다.
- `registered-all-effects-existing-scene-20261006-145943`: 실제 요소 파워 씬827 PASS/FAIL0/종료0. Stage08 기존 원문142개 복원 차이0. 일반 효과 전체를 등록 프레임으로 재생하는 현재 코드에서 기존 매칭/조합/층/본체/취소/재시작 검사를 수행했다.
- `registered-flight-and-impact-native-20261006-150047`:59 PASS/FAIL0/종료0. 스키마5의 신규 로켓/드론 ID가 실제 native 재생에서 등록한 타격 그림을 사용하는 항목을 추가했다. 기존 비행 교대/5회 반환·반복/정상 종료/Destroy/Dispose도 통과했다. 테스트 입력은 새 런타임 계약에 맞게 trail/impact 효과 키를 직접 등록하며 기본 효과 추론을 사용하지 않는다.
- `registered-effects-native-resource-plan-20261006-150300`:99 PASS/FAIL0/종료0. 등록 효과 프레임을 포함하는 실제 주소/위치/Sprite 준비와 미사용 별도 주소 추가0·필요 주소 누락0·소유권 반환을 확인했다.

현재 PuzzlePowerPlayback 파일에서 Effects/ 경로 문자열 및 AddEffect 호출은 검색되지 않는다. 이 검색은 코드 경계 확인이며 다른 시스템 전체 효과 로딩의 완전성을 증명한다고 확대하지 않는다. 필요한 효과 키 누락은 현재 재생 클립 생성 때 ID 오류이며 **시작 전 필요한 키 검사**는 아직 남아 있다.2×2 부모 간 정렬, native pending/취소/전환/Unload 전체 검증, 최종54종+신규/전량 원본·팩·GUID·과거 증거 감사와5단계 인계 문서를 계속 진행한다. 이번 턴에서 실행한 모든 Unity 검사 핸들은 실제 종료했다. 빌드·커밋·푸시·하위 에이전트는 사용하지 않았다.

### A/B — 준비 전 필수 효과 검사와 관련 native 검증 (2026-10-07 00:21 KST)

필수 효과 키가 빠진 정의를 로드 전에 해당 ID 오류로 거절하도록 ElementVisualPreparation에 행동 특성별 키 검사를 추가했다. Layer는 clear, 충전 본체는 charge/damage, 피해 본체는 damage, 일반 공급은 match/creation, 파워 행동은 trail/impact/blast/pull/transform을 명시적으로 요구한다. 파워는 논리 frame0의 효과 등록을 사용하며 나머지 비행 프레임은 기존 상태 준비를 유지한다. 새 ID별 분기는 추가하지 않았다. ElementVisualFrame.Effects는 명시적 주소 의존 목록만 보유하고, 선택 콜백은 실제 필요한 효과 애니메이션만 준비한다. 등록만 된 미사용 효과를 자동으로 전체 준비하지 않는다.

- `required-effect-keys-red-20261006-150621`: 필수 impact가 없어도 준비 통과, 실제 종료1.
- `required-effect-keys-green-20261006-150801`: 키 거절은 확인했지만 미사용 등록 효과의 Generator 주소가 자동 합쳐져 실제 종료1. 부분 통과를 완료로 사용하지 않는다.
- `required-effect-selection-green-20261006-150915`: 4 PASS/FAIL0/실제 종료0. 필수 impact 누락의 준비 전 ID 오류, 미사용 등록 효과의 별도 주소 제외, 필요한 효과 프레임/주소 포함을 확인했다.
- 신규 준비/자원/공급/보드 검사 입력에 필요한 damage 또는 match/creation 효과를 명시적으로 추가했다. 검사 전용 FixtureEffects는 선택한 구형 시각 키의 새 DTO 복사만 반환하며 런타임 fallback이 아니다. unreachable 내구도13은 계속 제외한다.
- `explicit-required-effects-preparation-20261006-151429`: 13 PASS/FAIL0/실제 종료0.
- `required-effects-native-resource-20261006-151515`: 106 PASS/FAIL0/실제 Unity 종료0. 실제 Addressables 주소와 Sprite 준비·필요 주소 누락0·미사용 별도 요청0·Dispose를 확인했다. 호출 시 Outputs 배열이 하나의 쉼표 문자열로 전달되어 검증 후 영수증 수집만 실패했다. terminal.json의 실제 종료0·시작 이후 생성된 네 출력·소스 해시 변경0을 확인하여 출력 복사/영수증만 복구했다. Unity를 재실행하지 않았고 원문을 보존했다.
- `required-effects-native-supply-20261006-151616`: 126 PASS/FAIL0/실제 종료0. 두 신규 공급 ID와 20회 반복, 준비 용량 내 추가 생성0, Sprite/변환/order/마스크 반환, 원본/난수/아트 소유권 유지.
- `required-effects-native-board-metadata-20261006-151948`: 26 PASS/FAIL0/실제 종료0. 세 보드 ID/공유 별칭과 시트/피벗/크기/회전/order, 원본·팩2·난수 및 진입 요청 값 보존.

### A — 2×2 부모 간 정렬 실패 확보 (2026-10-07 00:21 KST)

ElementVisualBoardVerification.RunOrdering은 실제 대형 철근 상자(3,3)와 인접 웹(3,5)을 사용한다. 논리 점유가 겹치지 않는 유효 입력이고, 2.16칸 본체의 시각 범위가 인접 칸으로 확장된다. 본체10/덮개20 등록 값과 실제 Sprite 존재를 확인한 뒤 실제 UI 트리의 서로 다른 루트 가지 합성 순서를 검사한다.

- `large-body-editor-order-red-20261006-151753`: enum3(색 자물쇠)을 잘못 입력해 대형 본체가 없는 검사 입력 오류. 동작 RED로 사용하지 않는다.
- `large-body-editor-order-valid-red-20261006-151857`: 유효 입력/실제 Sprite/등록 크기 및 order 확인 후 '인접 덮개20은 2×2 본체10보다 앞에 합성' FAIL, 실제 종료1. 셀 자식 덮개와 루트 large-bodies가 다른 부모라 현재 정렬이 틀리는 유효 재현이다. 런타임 수정은 아직 하지 않았으며 이 실패는 해결 전 상태다.

다음 작업은 편집 보드 아트의 부모 간 공통 합성 순서 수정과 위 실패의 GREEN이다. 기존 CellAt 자식 쿼리 검사 계약을 바꾸는 경우 실제 렌더링 변경과 구분해 기록하고, 가짜 중복 이미지/임의 크기 축소로 숨기지 않는다. B/C pending 취소·다시하기·전환·Destroy/Unload 전체 수명, 최종54종+신규 및 전량 논리/원본/팩/GUID/과거 증거 감사와5단계 문서는 미완료다. 모든 실행 Unity 프로세스는 실제 종료했고 work/HEAD b03ac0539e0286bd67763c88fcbd16f8316ac452와 기존 WIP를 보존했다. 빌드·커밋·푸시·하위 에이전트를 실행하지 않았다.

### A — 편집 보드의 공통 합성 순서 수정 (2026-10-07 00:48 KST)

이전 목표 턴은 필수 효과 준비와 유효 2×2 정렬 RED 확보로 progress였고, 이번 턴은 실제 공통 합성 수정과 native 수명 검증으로 progress다. LevelBoardView의 입력/바닥 셀은 유지하고 실제 아트 VisualElement를 board-artwork 한 부모에 배치했다. 개별 칸의 먼지/내용물/덮개와 확장된 본체를 등록 order로 함께 정렬한다. 이미지나 소유자를 복제한 가짜 쿼리 호스트를 두지 않는다. 2.16칸 크기를 줄이거나 셀 바닥 뒤에 숨기지 않는다. 셀 border와 포털 inset을 전역 아트 위치/크기에 반영하고, 숫자/오류는 별도 최상단 annotations에 둔다. 공급 표시와 입력 좌표는 유지한다.

- `large-body-editor-order-green-20261006-152510`: 유효 RED와 같은 등록 본체10/인접 덮개20 합성 기대값으로5 PASS/FAIL0/실제 종료0.
- `flat-editor-order-both-schemas-20261006-153105`:11 PASS/FAIL0/실제 종료0. 구형/스키마5 둘 다 공통 부모의 합성 순서를 확인하고, 본체 order47 재정의 후3회 표시에서 덮개20보다 앞에 오며 원본/팩이 그대로임을 확인했다. 실제 Sprite와 UI 트리 합성 순서 검사이며 픽셀 색상 캡처 비교까지 수행했다고 확대하지 않는다.
- `flat-editor-art-native-metadata-20261006-152710`:26 PASS/FAIL0/실제 종료0. 서로 다른 두 ID와 공유 별칭의 세 보드 Sprite, 피벗/크기/회전/order/시트·아트 소유권·규칙 독립을 확인했다.
- `flat-editor-art-existing-rabbits-20261006-153303`:22 PASS/FAIL0/실제 종료0. 기존 토끼/팔레트/무작위/은폐/삭제/원본 검사를 유지했다. 기존 RabbitArtwork 원문9개 해시 복원 차이0.
- `flat-editor-art-legacy-mappings-20261006-154638`:16 PASS/FAIL0/실제 종료0. 구형 층/2×2의 등록 공유 별칭과 크기/회전·누락 상태 오류·기본 로켓/2×2 복귀 확인.

기존 BoardArtworkVerification.cs와 RabbitArtworkVerification.cs의 CellAt().Q 아트/배지 조회만 실제 ArtworkAt/AnnotationAt/LargeBodies 조회로 바꿨다. 이미지/크기/데이터 기대값을 약화하지 않았고, 바닥→먼지→내용물의 검사는 실제 바닥 가지와 공통 아트 부모의 합성 순서로 변경했다. 변경 전 두 원문은 `editor-query-contract-20261007/`에 보관했다. CellAt은 그대로 입력·바닥·진단 좌표의 실제 셀이다.

`flat-editor-art-existing-board-20261006-152829`는474 PASS/1 FAIL/실제 종료1이다. 실패는 Edit Mode의 기존 BundledAssetProvider Android 번들에 collection-drone-rotor-4frames-v1이 없는 항목이며, 앞의11:27 진단과 같은 경계다. 편집 이미지/층/장치/2×2/삭제/진단 검사는 통과했지만 이 종합 검사를 GREEN으로 집계하지 않는다. 기존 BoardArtwork 결과1개를 해시 차이0으로 복원했고 추가 캡처 출력은 생성됐다. 현재 Play Mode의 AssetDatabaseProvider native 시트 로드는 별도 통과 증거다. 오래된 번들을 수정/빌드하거나 로딩 정책을 바꾸지 않았다.

### B — native pending·지연 콜백·후보 아트 소유권 (2026-10-07 00:48 KST)

ElementResourcePlanVerification.RunLifetime을 기존 테스트 파일에 추가했다. 별도 메모리 Play Mode와 실제 Addressables 핸들을 사용하며 대기에는 제한 시간을 둔다. 공유 소유자 수명과 단독 pending 반환은 다른 검증으로 구분한다. 공유 Addressables 내부 연산의 IsValid를 한 소유자의 반환 증거로 오해하지 않는다.

- `native-atlas-pending-dispose-red-20261006-153437`: 종료/결과가 남지 않은 검사였다. 실행 명령과 PID93296으로 이 작업 소유의 batch 인스턴스를 확인한 뒤 중단했다. 실제 종료-1, timeout-observation.json과 원문을 보존했다. 사용자 Editor는 종료하지 않았다. 검사 코드의 성공 return이 출력/Exit를 건너뛰는 경계를 finally로 옮기고 제한 시간을 추가했다. 이 실행은 동작 RED나 PASS로 사용하지 않는다.
- `native-atlas-pending-dispose-bounded-red-20261006-153856`: 이름에 red가 있으나 실제6 PASS/FAIL0/종료0이다. 단독 native pending을 확보한 뒤 Dispose해도 완료까지 핸들이 유효하며, 완료 후 사용 거절/핸들 반환과 공유 텍스처·독립 클론·다른 소유자 유지·마지막 클론 파기가 기존 코드로 통과했다. 새로운 pending 카운터나 반환 정책은 추가하지 않았다.
- `native-atlas-delayed-callback-red-20261006-154053`: 실제 native 지연 요청/해제 후 콜백0·핸들 반환은 통과했으나 정상 Dispose가 ObjectDisposedException 로그를 남겨 종료1. 유효 RED다. 테스트는 OnAtlasRequested 진입을 호출하되 로드/완료는 실제 native Addressables다. SpriteAtlasManager의 엔진 이벤트를 직접 발생시킨 검증으로 확대하지 않는다.
- BoardSpriteAtlas.BindAsync는 LoadAsync의 ObjectDisposedException을 정상 종료로 처리하고 해제된 소유자에 콜백을 전달하지 않는다. 그 외 로드 실패나 유효 콜백 오류는 기존 방식으로 남긴다. 주소/그룹/핸들 반환/클론 소유권 정책은 바꾸지 않았다.
- `native-atlas-delayed-callback-green-20261006-154216`:11 PASS/FAIL0/실제 종료0. native pending/공유/클론 반환에 더해 해제 콜백0·종료 예외0, 유효 소유자의 실제 완료 콜백1회·조회와 반환 후 잔류0 확인.
- `native-artwork-candidate-lifetime-20261006-154505`:21 PASS/FAIL0/실제 종료0. 위11개에 현재/후보의 유효 레벨, 기존 소유자에 없는 자원의 실제 pending, 취소+Dispose 후 완료까지 보유와 전량 반환, 이전 Sprite 유지, 필수 damage 누락의 native 요청 전 ID 오류, 재준비한 후보의 독립 클론/텍스처 공유, 이전 반환 후 새 소유자 유지,3회 재준비의 주소/클론 추가0, 최종 Dispose 잔류0을 추가했다. 아트 후보 경계이며 실제 PuzzleGameSession의 다시하기/전환 전체 검증을 대신하지 않는다.

`editor-order-protection.json`은 보호 시작 manifest에서 소스를 제외한6843개 변경0을 확인했다. 원본/과거 증거9137개 전량 최종 감사와 같다고 확대하지 않는다. work/HEAD b03ac0539e0286bd67763c88fcbd16f8316ac452는 유지됐고 git diff --check가 통과했다. 이번 실행한 모든 프로세스/감사는 실제 종료했다. 빌드·콘텐츠 빌드·팩 재생성·커밋·푸시·하위 에이전트를 실행하지 않았다.

남은 작업: 조합 파워의 비행 프레임이 원본 실제 ID를 유지하는지 공통 원본 선택 경계를 추가 감사한다. C의 실제 세션 다시하기/전환/Destroy/메모리 씬 Unload와 보드/장식/마스크 반환 전체 수명을 확인한다. 이후 최종54종+신규, 전량 논리/원본/팩/에셋/GUID/과거 증거 감사와5단계 계획·목표·복사용 명령문을 작성한다. 전체4단계는 아직 완료가 아니다.

### A/C — 조합 파워 실제 ID와 월드 슬롯 반환 (2026-10-07 01:07 KST)

직전 사용자 요청은 커밋 메시지 제공이어서 소스 변경 없이 응답했다. 목표 작업을 재개한 이번 턴은 유효 실패의 최소 수정과 실제 Unity 검증으로 progress다. work/HEAD b03ac0539e0286bd67763c88fcbd16f8316ac452와 기존 WIP를 유지한다.

- `actual-combination-flight-id-red-20261006-155053`: 유효 스키마5 신규 로켓 ID와 실제 로켓+폭탄 교환 조합에서 비행 프레임의 경로/크기/order가 실제 로켓 정의를 잃어 실제 종료1. 기존 AddPowerEffect의 원본/조합 중심 선택을 PowerSource로 공유하고 드론/로켓 비행 Animation도 같은 실제 원본을 사용하게 했다. 규칙/타이밍/난수는 바꾸지 않았다.
- `actual-combination-flight-id-green-20261006-155333`: 38 PASS/FAIL0/실제 종료0. 신규 ID의 로켓+폭탄 및 드론+폭탄 실제 실행 기록에서 등록 비행 프레임 선택을 확인했다. 이 검사는 실제 조합 실행의 재생 클립이며 조합 비행의 픽셀 렌더 캡처까지 검증했다고 확대하지 않는다.
- `world-floor-decoration-return-red-20261006-155642`와 `world-floor-decoration-input-diagnostic-20261006-155816`는 비활성 칸에 고정 블록을 남긴 입력 오류다. 작은 검사 레벨의 해당 블록만 지우고 비활성화해 유효 입력을 확보했다. 런타임 RED로 집계하지 않는다.
- `world-floor-decoration-valid-red-20261006-155907`: 유효 입력/실제 아트 준비 뒤 바닥 색/order/마스크/변환 오염 초기화 실패, 실제 종료1.
- PuzzleCellView의 네 층 반환과 바닥 재표시, PuzzleWorldBoard의 비활성 셀/미사용 본체·장식 반환 및 Take를 기존 슬롯 초기화로 연결했다. Sprite·enabled·색·flip·마스크·order·위치·크기·회전을 초기화하며 외부 아트 소유권은 해제하지 않는다. 셀 루트 축척/회전도 재표시에서 복원한다.
- `world-slot-reset-after-return-fix-20261006-160327`: 5회 슬롯 오염/반환/용량 검사는 통과했으나 높은 order90의 이웃보다 고정50의 선택 블록이 뒤에 표시되어 실제 종료1. 선택 순간 활성 셀/본체/장식의 최고 order보다 높이 올리고 ClearPreview/ReleasePreview의 기존 복원을 유지했다. 기본 최저 order50은 유지하고 매 프레임 탐색/할당은 추가하지 않았다.
- `world-slot-reset-swipe-green-20261006-160425`: 37 PASS/FAIL0/실제 종료0. 실제 프리팹과 native 아트로 5회 고의 오염 후 색/프레임/변환/order/마스크 복원, 비활성 네 층과 본체/장식의 참조 잔류0, 준비 용량 내 추가 Transform 생성0, 높은 order 이웃 위의 선택 및 원래 order 복원, 원본/규칙 난수 불변을 확인했다. 실제 메모리 씬 Unload 후 셀/본체/장식 렌더러 파기와 외부 아트 소유권 유지도 확인했다.
- `world-reset-related-native-supply-20261006-160503`: 126 PASS/FAIL0/실제 종료0. 공용 Take 초기화 변경 후 두 실제 공급 ID, 20회 슬롯 반복·추가 생성0·공급 마스크/반환·Destroy·아트 소유권 보존을 재확인했다.

실제 게임 세션의 기존 취소/다시하기/씬 교체 검사 `slot-reset-existing-session-lifetimes`를 실행 중이다. 이 기록은 완료 주장이나 최종 전체 회귀가 아니다. 전체54종+신규, 전량 논리 비교와 원본/팩/에셋/GUID/과거 증거 감사, 5단계 계획/목표/복사용 실행문은 아직 남아 있다. 빌드·Addressables 콘텐츠 빌드·팩 재생성·커밋·푸시·하위 에이전트·사용자 Editor 종료·씬 저장은 수행하지 않았다.

slot-reset-existing-session-lifetimes-20261006-160556: 실제 종료0, 이번 실행의 scene-results.txt 183 PASS/FAIL0. Asset/MemoryPack 실제 씬 교체에서 이전 세션/HUD/아틀라스 파기, 수집 반복/취소 후보 보존/재시작/실패 후 pending 완료 및 재시작을 확인했다. 과거 Stage09 출력 109개 해시 복원 차이 0. 이전 파일의 FAIL 기록을 새 실행 결과로 합산하지 않는다. 전체 보드 효과/공급 풀의 세션 경계 및 실제 다음 레벨 전환에 대한 추가 직접 관찰은 아직 남아 있다. 이 작업의 Unity 프로세스는 종료했다.

### C — 실제 세션과 다음 레벨의 슬롯 경계 (2026-10-07 01:14 KST)

이전 턴은 실제 월드/공급 초기화의 RED→GREEN으로 progress였고 이번 턴은 실제 세션 경계 관찰 보강 및 다음 레벨 검증으로 progress다. 기존 검사의 기대값을 약화하지 않고 반환 참조와 실제 객체 ID 관찰을 추가했다.

- `actual-session-board-pool-boundaries-20261006-160858`: 221 PASS/FAIL0/실제 종료0. 기존183 검사에 Asset/MemoryPack 실제 씬 교체 후 이전 보드/공급/효과/마스크 Transform 파기, 동일 행동3회 반복에서 준비된 보드 전체 Transform ID 동일(추가 생성0), 각 반환 및 취소 후/실패 후 재시작의 임시 Sprite/색/flip/변환/order/마스크 참조 잔류0, 이전 effectLoad/powerPlayback 소유자 잔류0을 추가했다. 과거 Stage09 출력109개 해시 복원 차이0.
- `actual-next-level-pool-lifetimes-20261006-161049`: 실제 종료1. 새 판 준비/81칸 재사용/논리 상태/소유권 교체는 통과했으나 이전 검사가 아틀라스4종만 기대했다. A/B의 도달 가능한 필수 효과 진입 준비에 맞지 않는 검사 계약 차이다. 원문 결과와 변경 전 검사 `transition-atlas-contract-before.cs.txt`를 보존했다.
- 필요 주소 기대값을 독립적인 명시 목록11개(Blocks/PowerBlocks/바닥/MetalRodBox + Match/PowerCreation/Rocket/BombExplosion/Drone/Magnet/MetalBreak)로 갱신했다. 검사 기대값을 런타임 계획 결과에서 복제하지 않는다. 미사용 별도 장애물/효과 주소는 여전히 목록에 없으며 실제 loaded.Keys 전체 집합 동등을 검사한다. 런타임/로더 정책 변경은 없다.
- `actual-next-level-pool-contract-green-20261006-161242`: 152 PASS/FAIL0/실제 종료0. 실제 EventSystem 다음 버튼·native Addressables 후보 성공, 기존81칸 재사용, 이전 아트 반환·새 아트 유지 및 새 판 전체 논리 상태가 동일함을 확인했다. 이전 공급/효과 Sprite·변환/order·마스크와 effectLoad/powerPlayback 잔류0, 전환 후 같은 레벨3회 재시작의 전체 Transform ID 동일/추가 생성0과 전체 논리 상태 동등을 추가했다. 검사 소유 임시 bytes/locator와 기존 파일은 종료 시 복원했다. 디스크 배포 레벨 팩은 재생성하지 않았다.

`actual-next-level-cancel-destroy-boundaries`는 실제 후보 준비 취소/오류/50→51 및 준비 중 씬 종료 검사를 실행 중이다. 별도 Phase04/RegressionFinal에 기존30종 실행 스크립트와 정확한 진입점 목록을 준비했으며 Phase03 원문 증거는 덮어쓰지 않았다. 최종54종+신규 실행/전량 논리 및 보존 감사/5단계 문서는 아직 미완료다. 빌드·콘텐츠 빌드·커밋·푸시·하위 에이전트·사용자 Editor 종료·씬 저장은 하지 않았다.

`actual-next-level-cancel-destroy-boundaries-20261006-161402`: 32 PASS/FAIL0/실제 종료0. native 후보 반복 취소/잘못된 bytes/아틀라스 준비 실패/50→51 오류 후 재시도/같은 시드/Retry를 보존했다. 후보 준비 중 실제 Single 씬 로드에서 세션과 이전 보드/공급/효과/마스크 Transform이 모두 파기되고 늦은 Changed0·아트 반환을 확인했다. Stage13 과거 출력189개 해시 복원 차이0. `actual-next-level-pool-contract-green-20261006-161242`도 Stage13 출력189개 복원 차이0이었다.

최종 소스 고정 후 `Phase04/RegressionFinal/run-regression.ps1`으로 정확한 기존30종 회귀를 시작했다. 새 증거는 Phase04 경로에 남기고 기존 Stage 출력은 실행마다 해시 확인 후 복원한다. 빌드나 Phase03 원문 증거 덮어쓰기는 없다. 아직 실행 중이므로 통과 수나 전량 보존 완료를 주장하지 않는다. 실행 세션과 현재 검사/PID는 continuation-state.json과 RegressionFinal/current-regression.json에 기록한다. 실행 중 C#/meta를 수정하지 않는다.

### 최종 회귀 실행 중 검사 도구 준비 (2026-10-07 01:24 KST)

이번 턴은 동일 실행 세션13418의 실제 살아 있는 검사 프로세스를 확인하고 나머지 최종 검사/보존 도구를 준비해 progress다. PID81172의 명령은 이 작업의 DamageAggregationPolicyVerification.Run이며 실제 Unity/CPU453초/응답 상태를 읽었다. 관찰 대기만으로 종료나 정지로 판단하지 않았고 검사 재시작/프로세스 종료를 하지 않았다. 직전 3단계의 같은 검사 실제 종료 기록은 약11분이었다.

- `audit-final-regression.ps1`: Phase04/RegressionFinal의 새30종 종료/개수/출력 복원/전량 값 비교를 감사하도록 경로를 분리했다. 현재 work/HEAD b03을 검사하며 3단계 raw/canonical 비교 근거 경로는 보존한다. 아직 실행하지 않았다.
- `run-final-drone.ps1`: 기존9종(예상651 PASS)의 native 검사와 과거 Phase02 출력 복원을 유지하고 새 증거 경로/소스 고정 경계를 Phase04로 변경했다. 아직 실행하지 않았다.
- `run-final-related.ps1`과 `run-preserved-verification.ps1`: 기존12종은 실제 테스트가 쓰는 Phase03/Stage08 출력 경로를 유지하되 실행 전 복사→새 Phase04 검사 증거 수집→원본 해시 복원을 추가했다. 3단계 결과를 덮어쓴 채 남기지 않는다. 아직 실행하지 않았다.
- `audit-final-related-graphics.ps1`: 기존12종453 및 그래픽3종2130의 정확한 진입점/개수/종료/동일 소스 검사를 유지하고 새 Phase04 증거 및 원본 출력 복원 영수증을 검사한다. 아직 실행하지 않았다.
- `audit-final-preservation.ps1`: 고정1234개 소스/메타 inventory 및 해시, 원본 에셋/설정1489개, 과거 증거9137개, 4단계 시작 보호 manifest의 비소스, 원본 meta/GUID 불변과 새 파일 짝/GUID 충돌, work/HEAD를 검사한다. 검사 실행 중 임시 결과 변경이 있으므로 실제 감사는 종료 후에만 수행한다. 아직 실행하지 않았다.
- `final-runner-preflight.json`: 실행 중 소스/메타1234개 해시 변경0, 준비한6개 PowerShell 스크립트 파싱 오류0을 확인했다. 도구 준비를 실제 Unity 검증 완료로 확대하지 않는다.

최종30종은 동일 세션13418에서 계속 실행 중이다. C#/meta는 수정하지 않았다. 전체54종+신규와 전량 논리/보존 감사/5단계 문서는 여전히 미완료다.

최종30종의 첫 aggregation 검사가 실제 종료0·75264 PASS/FAIL0으로 끝났고 이전 출력 복원 영수증 차이0을 확인했다. 같은 실행 세션13418이 RemovalMissionProfileVerification.Run으로 이어졌다. 전체30종 완료로 집계하지 않는다.

### 최종 회귀 부분 감사와 공급 그룹 정렬 추가 발견 (2026-10-07 01:32 KST)

같은 세션13418의 최종 기존30종은 aggregation 75264 + removal-mission 43431 = 2종118695 PASS/FAIL0·각 실제 종료0까지 진행했다. 두 종료 원문과 출력 복원 영수증 차이0은 `RegressionFinal/partial-audit.json`에 확인했다. 현재 DurableMagnetPolicyVerification.Run이 실행 중이며 전체30종 완료로 표현하지 않는다.

읽기 전용 소스 검토에서 추가 감사가 필요한 C 경계를 발견했다. PuzzleWorldBoard.SupplyImage는 최초 SortingGroup의 order를10으로 고정한 뒤 자식 SpriteRenderer에는 등록 프레임 order를 적용한다. ElementSupplyVisualVerification의 두 신규 ID는 order13/14를 등록하지만 기존 검사는 자식 renderer.sortingOrder만 비교하므로 부모 그룹의 실제 합성 순서를 검증하지 않는다. 원본 아트/로딩 정책 문제와 구분되는 표현 적용/검사 범위 문제다. 실제 native 그룹/재사용/반환의 유효 실패 검사를 추가한 뒤 최소 수정과 관련 검증이 필요하다. 아직 새 검사를 실행하거나 런타임을 수정하지 않았다. 기존126 PASS를 이 그룹 경계까지 증명한 것으로 확대하지 않는다.

현재 검사 중 C#/meta 수정 금지를 유지한다. 같은 회귀 세션의 종료/출력 복원을 먼저 확인한 뒤 그룹 정렬 검사를 추가한다. 최종 소스 고정·회귀 증거는 이 실제 변경의 영향을 반영해 다시 감사해야 하므로 현재 실행만으로 전체4단계 완료를 주장하지 않는다. 5단계 문서는 아직 작성하지 않는다.

같은 세션13418에서 durable-magnet 검사가54307 PASS/FAIL0·실제 종료0으로 끝났다. 현재3종173002 PASS이며 각 종료/출력 복원 차이0은 partial-audit.json에 기록했다. 전체30종은 계속 실행 중이다. pending-supply-group-native-red.patch에 두 신규 공급 ID의 실제 부모 SortingGroup 등록 order/기본 변환과 고의 오염 후 반환 검사를 준비했지만 아직 적용하거나 실행하지 않았다. C#/meta 고정과 기존 WIP를 유지하며 실제 RED 확보 후 최소 런타임 수정을 진행한다.

최종 회귀가 같은 세션13418에서 capsule-magnet49166 및 capsule-adjacent48386 PASS/FAIL0·각 실제 종료0을 추가로 확인했다. 현재5종270554 PASS이며 각 출력 복원 영수증 차이0이다. InitialMissionSupplyVerification.Run이 계속 실행 중이다. pending-supply-group-minimal-fix.patch에 공급 부모 그룹의 등록 order 적용 및 반환 시 기본 order/위치/축척/회전 복원 수정안을 준비했지만 아직 적용하지 않았다. Native RED 패치를 먼저 적용/실행한 뒤 필요한 최소 수정만 적용해야 한다. C#/meta1234개 고정 해시 변경0을 재확인했다. 전체30종이나4단계 완료로 주장하지 않는다.

같은 세션13418의 최종 회귀가 initial-supply47201, record42279, reserved7793, color-match20861, generator-reaction24489, appliance-damage5085 PASS/FAIL0·각 실제 종료0을 추가 확인했다. 현재11종418262 PASS이다. 종료 원문/새 결과 파일 PASS·FAIL 개수/원래 expectedPass/출력 복원 영수증을 대조해 partial-audit.json을 갱신했다. 다음 ColorLockDamagePolicyVerification.Run이 실행 중이며 C#/meta 고정 해시1234개 변경0을 확인했다. 공급 부모 그룹 native 검사와 최소 수정 패치는 적용 전 상태로 유지한다. 실제 실패 확보 후 수정하며 전체30종·4단계 완료로 확대하지 않는다.

color-lock-damage도4452 PASS/FAIL0·실제 종료0으로 끝나 현재12종422714 PASS다. 같은 실행은 다음 검사로 이어진다.

후속 검사는 빠르게 이어져 부분 개수 기록과 현재 실행 항목이 달라질 수 있다. 최신 개수는 verified-regression.json의 실제 종료 영수증을 기준으로 확인한다.

이번 영수증 스냅샷 기준 15 종 424962 PASS/FAIL0이며 같은 세션13418은 계속 살아 있다. 전체 완료가 아니며 공급 그룹 검사 패치는 적용 전 상태다.

최종30종의 같은 세션13418은 이번 스냅샷 기준 26 종 478633 PASS/FAIL0·각 실제 종료0까지 진행했다. 새 결과 개수와 원래 expectedPass/종료/출력 복원 영수증을 대조했다. C#/meta 수정 없이 run-final-new.ps1에 신규 표현/준비/구형/세 보드/프레임/정렬/월드 반환/자원 계획·수명/공급/봇 두 경계/효과·교환·조합·피해/HUD의 정확한22개 진입점을 준비하고 PowerShell 파싱 오류0을 확인했다. 원본 소스 고정 manifest를 매 검사 전후 대조하며 필요한 native 검사는 graphics 실행기를 사용한다. 아직 실행하지 않았고 준비 도구를 검사 완료로 집계하지 않는다. 공급 그룹 native RED 및 최소 수정 패치도 아직 적용 전이며 30종 종료 후 진행한다.
최종30종의 같은 실행 세션13418에서 scrap-supply-mission이51155 PASS/FAIL0·실제 종료0으로 끝났다. 완료된27종529788 PASS의 실제 종료 원문, 새 결과의 PASS/FAIL 개수, 기존 기대 개수, 출력 복원 해시를 다시 대조했으며 차이0을 partial-audit.json에 기록했다. 현재 ElementDurabilityApplyPolicyVerification.Run의 실제 Unity PID113560을 읽기 전용으로 확인했고 Responding=True였다. 관찰 시간 만료를 종료로 취급하거나 검사를 재시작하지 않았다. C#/meta 변경 없이 같은 세션을 기다리며, 공급 부모 SortingGroup의 native 실패 검사→최소 수정은 전체30종 종료와 출력 복원 후에 진행한다. 4단계 전체 완료와 최종 소스 검증은 아직 미달성이다.
같은 최종 회귀 세션13418에서 durability-apply-policy가50875 PASS/FAIL0·실제 종료0으로 끝났다. 해당 새 결과의 실제 PASS/FAIL 개수와 기존 기대 개수, 종료 영수증, 출력 복원 해시를 대조해 차이0을 확인했다. 누적28종580663 PASS이며 partial-audit.json을 갱신했다. 검사 실행 중 고정 C#/meta1234개 해시 변경0도 live-source-freeze-audit.json에 기록했다. 나머지 reaction-apply/reaction-query가 끝나기 전 소스는 수정하지 않는다. 5단계 범위는 기존 가이드의 데이터 확장/500개 제작 카탈로그/통합 회귀/검증된 중복 정리와 전체 목표의 데이터만으로 새3종 추가·신규 행동 등록·제작/왕복/공개 관찰/리소스·풀 검증을 대조했다. 5단계 문서 확정은4단계 실제 완료 근거에 맞춰 진행하며 구현은 시작하지 않는다.
최종30종의 같은 세션13418에서 reaction-apply가53295 PASS/FAIL0·실제 종료0으로 끝났다. 완료29종633958 PASS이며 새 결과 개수/기존 기대 개수/실제 종료/출력 복원 해시 차이0을 partial-audit.json에 추가했다. 마지막 ElementReactionBehaviorVerification.Run(PID89904)이 시작됐으며 검사 소스 고정과 공급 정렬 패치 미적용을 유지한다. 이번29종을 최종30종 완료나 공급 부모 합성 정렬 검증으로 확대하지 않는다.
2026-10-06 17:22 UTC에 기존30종의 같은 세션13418이 실제 종료0으로 끝났다. 687011 PASS/FAIL0이며 audit-final-regression.ps1도 실제 종료0으로 전량18182행·188팩·225상태 논리 차이0, 출력 복원76건 차이0, 고정 소스1234개, work/HEAD b03 보존을 확인했다. 이 결과는 공급 부모 그룹 수정 전 소스의 증거로 RegressionFinal에 보존한다.

그 뒤 공급 부모 SortingGroup을 확인하는 native 실패 검사를 적용했다. supply-group-native-red-20261006-172323은 자식 renderer의 등록 order 통과 후 부모 그룹 등록 order 검사에서 실제 종료1로 실패했다. 기존126 PASS가 부모 합성 순서까지 보장하지 못했음을 실제로 확인했다. SupplyImage에서 부모 그룹의 등록 order/기본 변환을 적용하고 ReturnTemporary에서 그룹 기본 order10·위치0·크기1·회전0을 복원하는 최소 수정만 추가했다. supply-group-native-green-20261006-172409은166 PASS/FAIL0·실제 종료0이다. 서로 다른 두 ID와20회 고의 오염/반환/재사용, 준비 용량 내 추가 생성0, 소유권/원본/난수 무변경을 확인했다.

수정 전30종 소스와 현재 소스1234개를 대조해 변경은 검사1개와 관련 월드 런타임2개뿐이며 supply-group-source-boundary.json에 해시를 기록했다. final-source-hashes-after-supply-group.json으로 현재 소스를 고정하고 신규22종을 실행 중이다(세션37866). 수정 전30종을 현재 최종 소스 증거로 재해석하지 않는다. 실제 런타임 변경이 있으므로 최종30종은 신규/관련 검증 후 RegressionAfterSupplyGroup에서 실행하도록 준비했으며 아직 시작하지 않았다. 이전 원문을 덮어쓰지 않는다. 빌드/콘텐츠 빌드/디스크 팩 생성/커밋/푸시/하위 에이전트/사용자 Editor 종료/씬 저장은 하지 않았다. 4단계 전체 완료·5단계 구현은 아직 아니다.
현재 공급 부모 정렬 수정본에서 신규22종이 모두 실제 종료0으로 끝났다(세션37866 종료0). audit-final-new.ps1의 실제 감사 종료0으로22개 정확한 진입점·820 PASS/FAIL0·각 보관 결과 해시·현재 고정 소스1234개 동일을 확인했다. 표현/효과 등록·준비·구형 상태·세 보드·메타데이터·native 비행·정렬·월드 슬롯 반환/Unload·native 필요 주소·pending/공유/취소/Dispose·공급166·공개 봇 본체/층·효과 반환·실제 교환/파워 조합/피해 클립·HUD가 포함된다. final-new-audit.json에 개별 시간/결과 경로/개수/종료를 보관했다. 신규 검사 통과를 기존54종이나 실기기 성능 검증으로 확대하지 않는다.

현재 같은 수정본에서 실제 세션/다음 레벨/취소·Destroy 수명3종과 기존 그래픽3종을 run-final-session-graphics.ps1로 실행 중이다(세션88065). 소스 고정과 과거 Stage09/13/08 출력 복원을 유지한다. 최종 기존30종의 수정 후 실행, 드론9종, 저장/제작12종, 전량 비교/원본1489·증거9137/GUID 보존 감사, 완료 보고와5단계 문서는 아직 남아 있다.
최종 native 수명3종은 현재 공급 그룹 수정 런타임에서221+152+32=405 PASS/FAIL0·각 실제 종료0으로 통과했다. 원본 Stage09의109개, Stage13의189개씩 복원 차이0이다. 뒤의 그래픽 검사는 구형 검사 계약 두 곳에서 멈췄다. final-graphics-power-20261006-173509는 색상별 오브젝트 이름(match-pink)을 찾던 검사에서 실패했다. 실제 등록 효과 라벨은 공통match이므로 Layers.cs의 한 검사를 색상별 native Sprite의 두 번째 프레임과 실제 활성 renderer 비교로 강화했다. 그 다음 final-graphics-power-contract-20261006-173732는 이미 시작 전에 준비된Rocket 효과 주소를 검사 fixture가Dictionary.Add로 중복 추가하면서 실패했다. Render.cs에서 기존 로더를 반환하고 잘못된 로더로 교체해 누락 프레임을 재현하며, Destroy 검사는 재생/소유권 유지 상태를 검사하도록 맞췄다. pending/취소 자체는 앞의 native 자원 수명 검사로 별도 확인한다.

final-graphics-power-current-contract-20261006-173939는 기존477 PASS/FAIL0·실제 종료0이며 출력142개 복원 차이0이다. 변경은 검사2개뿐, 다른1232개와 런타임은 동일하다. 수정 전두 소스와 실패원문을 보존했고 match-effect-test-contract-audit.json에 해시 경계를 감사했다. 앞서 통과한 신규22종820과 실제 수명3종405는 재실행하지 않았다. final-new-audit.json/final-native-lifetime-audit.json은 동일 런타임과 이 검사2개 변경을 명시적으로 대조한다. 모든 검사가 같은 테스트 파일SHA를 사용했다고 표현하지 않는다.

구형 게임 씬 그래픽은 final-graphics-legacy-scene-20261006-174200에서 실제 종료0으로 끝났으며, 신형 요소 씬을 같은 세션85464에서 이어 실행 중이다. 최종54종·전체 보존 감사·5단계 문서는 아직 남아 있다.
기존 그래픽3종의 현재 최종 검사 결과는477+826+827=2130 PASS/FAIL0·각 실제 종료0이다. 구형/신형 씬의 실제 결과 개수와 각 원본142개 복원 차이0을 대조했다. 신형 씬까지 같은 세션85464는 종료0이다. 이어 저장/제작/편집12종을 현재 소스에서 실행 중이다(세션65936, run-final-related.ps1). 최종 수정 후30종과드론9종은 아직 실행 전이며 전체54종 완료로 주장하지 않는다.
저장/제작/편집12종은 모두 실제 종료0이다. audit-final-related-graphics.ps1도 실제 종료0으로 기존 정확한15개 진입점을3단계 기록과 대조해 저장/제작/편집453 + 그래픽2130 PASS/FAIL0, 현재 소스1234개 동일, 과거 결과 출력 복원을 확인했다. 검사2개 변경은 앞의 신규22종820/수명3종405와 구분된 계약 경계로 유지하며 기존 최종15종은 모두 현재 테스트 소스로 실행됐다.

이후 최종 기존 규칙30종과드론9종을 run-final-regression-drone.ps1로 순차 실행하기 시작했다(세션24681). 실제 런타임 공급 정렬 수정 뒤의 회귀이며 수정 전RegressionFinal 원문을 덮어쓰지 않는다. 현재 RegressionAfterSupplyGroup의DamageAggregationPolicyVerification.Run(PID16332)이 실행 중이다. 같은 세션은30종 종료·전량 논리/출력 복원 감사 후드론9종으로 이어진다. C#/meta1234개를 고정하고 검사 중 소스 수정/경쟁Unity 실행을 하지 않는다. 원본1489·과거증거9137/GUID 보존과 완료요건별 감사·5단계 문서는 그 뒤에 남아 있다. 아직4단계 전체 완료는 아니다.
최종 회귀가 실행 중인 동안 목표 문서의 정확한15개 완료 조건을 현재 실제 관련 검사 근거에 연결했다. completion-requirements-progress.json은 신규22종의 개별 진입점/결과 경로/실제 종료/PASS·FAIL을 조건별로 연결하고, 실제 수명405·기존 저장/그래픽2583·검사2개 경계를 함께 명시한다. native 자원106의 원문에서 고정/랜덤/유지 공급·중복/순환 생성·실제 요청 집합 누락0/미사용 별도 요청0을, 공개 봇41의 원문에서 실제ID/이력/난수만 다른 입력 쌍의 관찰·후보·선택·점수 동일과 원본/난수 무변경을 다시 확인했다. 이 매핑을 전체 완료 증거로 대체하지 않으며15개 complete는 모두false로 유지한다. 현재 최종39종 종료·전량 비교·1489/9137/GUID 보존·완료요건별 감사·5단계 문서가 남아 있다. 같은 실행 세션24681과 소스 고정을 유지한다.
수정 후 현재 최종 회귀의 aggregation이75264 PASS/FAIL0·실제 종료0으로 끝났다. RegressionAfterSupplyGroup/partial-audit.json에 실제 종료/기존 기대 개수/결과 PASS·FAIL/원본 출력 복원 해시 차이0을 기록했다. 현재 고정 C#/meta1234개 해시 변경0도 대조했다. 같은 세션24681은RemovalMissionProfileVerification.Run(PID100884)으로 이어지며, 수정 전30종의 완료 증거를 현재 최종30종으로 섞지 않는다. 현재 최종 회귀 완료는1종뿐이고 전체39종 종료/전량 비교/1489·9137/GUID 보존/완료감사/5단계 문서는 아직 남아 있다.
수정 후 현재 최종 회귀의 removal-mission도43431 PASS/FAIL0·실제 종료0으로 끝났다. 누적2종118695 PASS이며 새 결과의 실제 개수/기존 기대 개수/종료 영수증/원본 출력 복원 해시 차이0을 partial-audit.json에 대조 기록했다. 같은 세션24681은DurableMagnetPolicyVerification.Run(PID66980)으로 계속 진행 중이다. 현재2종을 전체30종/39종/4단계 완료로 확대하지 않는다. 소스 고정과 수정 전/후 증거 분리를 유지한다.
수정 후 현재 최종 회귀의 durable-magnet도54307 PASS/FAIL0·실제 종료0으로 끝났다. partial-audit.json에서 누적3종173002 PASS의 실제 종료·기존 기대 개수·새 결과 개수·원본 출력 복원 차이0을 확인했다. 같은 세션24681을 직접 폴링해 실행 중임을 확인했으며 현재 CapsuleMagnetPolicyVerification.Run으로 이어진다. C#/meta 고정과 이전/현재 증거 분리를 유지한다. 현재3종을 전체30종/39종 완료로 확대하지 않는다.
현재 최종 회귀의 capsule-magnet은49166 PASS/FAIL0·실제 종료0으로 끝났다. 누적4종222168 PASS의 종료 원문·기존 기대 개수·새 결과의 PASS/FAIL·보관된 원본 해시·출력 복원 해시 차이0을 대조해 partial-audit.json을 갱신했다. 같은 세션24681은CapsuleAdjacentPolicyVerification.Run(PID82900)으로 계속 실행 중이며 직접 폴링으로 확인했다. 현재4종을 전체30종/39종 완료로 확대하지 않는다.
완료감사를 준비하며 현재 제작 값/불변 시각 정의·조회·리소스 계획·공개 봇 관찰·HUD Presenter/읽기 전용 상태의 실제 소스9개와 관련 검사 원문을 읽었다. completion-source-review-progress.json에 소스/원문 해시와 검토 범위를 기록했다. 상태 선택은 미등록 수치를 자르지 않고 오류로 거절하고, 리소스 집합은 공급/생성 방문 집합·주소 중복 제거를 사용한다. native 요청 집합 누락0/미사용 별도 요청0, 공개 동일 쌍의 관찰·후보·선택·점수 동일 및 원본/난수 무변경, HUD 표시 진행값·Dispose/기존 프리팹 검사 원문을 대조했다. 이 검토는 보강 근거이며 최종 회귀/전량 비교/전체 보존과 완료요건별 최종 감사를 대신하지 않는다.
현재 최종 회귀의 capsule-adjacent가48386 PASS/FAIL0·실제 종료0으로 끝났다. 누적5종270554 PASS이며 종료 원문·기대/새 결과 개수·보관 원본 및 출력 복원 해시 차이0을 감사했다. 같은 세션24681은InitialMissionSupplyVerification.Run(PID19896)으로 계속 실행 중이다. 공통 슬롯/효과 반환 및 Reset 소스4개와 native 반복/취소/Unload 검사 원문도 추가 대조해 completion-source-review-progress.json의 현재 고정 소스13개 일치와 미완료 상태를 유지했다. 최종30종/드론9종·전량 비교·전체 보존/완료감사·5단계 문서는 아직 남아 있다.
자원 소유권 완료감사를 준비하며 BoardSpriteAtlas/PuzzleArtwork 및 세션 다시하기/전환 소스4개와 native 자원 수명21개의 결과 원문을 추가 대조했다. 현재 고정 소스와 대조된 검토 범위는17개이며 completion-source-review-progress.json에 해시와 경계를 기록했다. pending 완료 후 반환·독립 클론/공유 소유자·후보 성공 후 교체와 실패/취소 후보 finally 반환을 확인했다. 지연 콜백 검사는 OnAtlasRequested 테스트 직접 호출과 실제 native 완료를 사용하며 엔진 이벤트 발생까지 입증했다고 확대하지 않는다. 세션24681 직접 폴링과 현재 Unity PID19896 Responding=True/CPU144.47 확인으로 같은 초기 미션·공급 검사가 살아 있음을 확인했고 재시작하지 않았다. 완료 회귀는5종270554 PASS이며 전체 완료 판단은 남은 검증 후 진행한다.
현재 최종 회귀의 initial-supply가47201 PASS/FAIL0·실제 종료0으로 끝났다. 누적6종317755 PASS이며 종료 원문·기대/새 결과 개수·원본 보관 및 출력 복원 해시 차이0을 partial-audit.json에 감사했다. 같은 세션24681은DamageRecordPolicyVerification.Run(PID85208)으로 이어진다. 이후 실제 드론9종의 정확한 진입점/651개·실제 종료·결과 원문 해시·과거 출력 복원·고정 소스를 대조할 audit-final-drone.ps1을 준비하고 PowerShell 파싱 오류0을 확인했다. 스크립트는 아직 실행하지 않았으며 검사 통과로 집계하지 않는다. 최종30종/드론9종·전량 비교·전체 보존과 완료감사·5단계 문서는 여전히 남아 있다.
현재 최종 회귀의 record가42279 PASS/FAIL0·실제 종료0으로 끝났다. 누적7종360034 PASS이며 종료 원문·기대/새 결과 개수·보관 원본 및 출력 복원 해시 차이0을 partial-audit.json에 감사했다. 같은 세션24681을 직접 폴링해 실행 중임을 확인했다. C#/meta 고정과 원본 증거 보존을 유지하며, 나머지 최종 회귀/전량 비교/보존 감사/완료감사 및5단계 문서를 이어 진행한다.
현재 최종 회귀의 reserved도7793 PASS/FAIL0·실제 종료0으로 끝났다. 누적8종367827 PASS이며 실제 종료·기존 기대/새 결과 개수·원본 보관 및 출력 복원 해시 차이0을 감사했다. 같은 세션24681은 계속 실행 중이며 전량 비교/보존/완료 감사와5단계 문서는 아직 남아 있다.
현재 최종 회귀의 color-match가20861 PASS/FAIL0·실제 종료0으로 끝났다. 누적9종388688 PASS이며 종료 원문·기대/새 결과 개수·원본 보관 및 출력 복원 해시 차이0을 감사했다. 검사 완료 직전의PID75740은 종료됐고 실제 종료 영수증으로 확인했다. 같은 세션24681을 유지하며 종료된 개별PID만을 전체 회귀 종료로 취급하지 않는다. 최종30종/드론9종과 전량 비교·보존/완료감사·5단계 문서는 아직 남아 있다.
현재 최종 회귀의 generator-reaction24489 및appliance-damage5085가각 PASS/FAIL0·실제 종료0으로 끝났다. 누적11종418262 PASS이며 각 종료 원문·기대/새 결과 개수·원본 보관 및 출력 복원 해시 차이0을 감사했다. 같은 세션24681을 유지하며 현재 소스 고정과 수정 전/후 증거 분리를 유지한다. 전체30종/드론9종·전량 비교·보존/완료감사·5단계 문서는 아직 남아 있다.
후속color-lock-damage4452와capsule-damage592도각 PASS/FAIL0·실제 종료0으로 끝났다. 현재13종423306 PASS의 실제 결과/정확한30종 기대 개수/종료 원문/보관 원본 및 출력 복원 해시 차이0을 partial-audit.json에 추가 감사했다. 같은 세션24681은ScrapDamagePolicyVerification.Run(PID90256)으로 이어지며 소스 수정이나 재시작 없이 유지한다. 현재13종을 전체 최종 회귀 완료로 확대하지 않는다.
현재 최종 회귀는 빠르게 이어져 이번 영수증 스냅샷 기준21종427123 PASS/FAIL0·각 실제 종료0까지 진행했다. 고철/상자 피해와 발전기/내구도형/상자 배치·카탈로그·ID·고정 장애물 등의 새 결과를 정확한30종 기대 목록·종료 원문·보관 원본 및 출력 복원 해시와 대조해 partial-audit.json을 갱신했다. live-source-freeze-audit.json에서 현재 고정C#/meta1234개 해시 차이0도 확인했다. 같은 세션24681은GeneratorVerification.Data(PID91148)으로 이어지며 전체30종/드론9종·전량 비교·전체 보존 및 완료감사·5단계 문서는 아직 남아 있다.
후속 검사 영수증까지 현재25종427683 PASS/FAIL0·각 실제 종료0을 감사했다. 새 결과의 개수와 정확한 기대 목록·종료 원문·보관 원본 및 출력 복원 해시 차이0을 대조했다. 같은 세션24681은 계속 실행 중이며 전량 비교·드론9종·보존/완료감사와5단계 문서는 아직 남아 있다.
현재 고철 유지 공급scrap-maintain-durability가50950 PASS/FAIL0·실제 종료0으로 끝났다. 누적26종478633 PASS이며 종료 원문·정확한 기대/새 결과 개수·원본 보관 및 출력 복원 해시 차이0을 감사했다. 같은 세션24681은 계속 실행 중이며 현재 결과를 이전RegressionFinal 증거와 혼합하지 않는다. 최종30종/드론9종·전량 비교·보존/완료감사와5단계 문서는 아직 남아 있다.
현재 세션24681은디스크 공간 부족으로 실제 종료1이다. scrap-supply-mission Unity PID19236은Stage36/after-values.jsonl 기록에서Win32 IO112로 종료1, 원본 결과 복원도공간 부족으로 실패했다. 같은 실행의 관찰 시간 만료로 추정한 종료가 아니다. 실패 terminal/log와불완전한82MB출력 및 당시source/통과26영수증을RegressionAfterSupplyGroup/disk-full-scrap-supply-mission-20261006-184020에 보존했다. Windows compact 확인은0파일 압축 보고였고 이후 실제 여유 공간8GB를 확인했으며 공간 확보 원인을 압축 성공으로 단정하지 않는다. 과거 보호manifest와 보관 원본을 먼저 비교한뒤Stage36원본4개를 복원해해시차이0을disk-full-recovery.json에 기록했다. 고정소스1234개를 그대로 대조하고resume-after-disk-full.ps1로새 세션15772를 시작했다. Resume는통과26종478633 PASS를 유지하고 공간 부족 실패1종과남은3종만 실행한뒤전량감사/드론9종으로 이어진다. 실패 원문을 통과 증거로 덮어쓰거나 전체검사를 처음부터 반복하지 않는다. 전체보존/완료감사와5단계문서는아직남아있다.
공간 부족 복원 후 scrap-supply-mission 재검사가51155 PASS/FAIL0·실제 종료0으로 끝났다. 현재 최종27종529788 PASS이며 정확한 기대/새 결과 개수·종료 원문·보관 원본과 실제 복원 경로의 해시 차이0을 감사했다. 원래 공간 부족 종료1과불완전한 출력은별도 실패 디렉터리에 그대로 보존하고이번 통과근거와구분한다. 같은 재개 세션15772는ElementDurabilityApplyPolicyVerification.Run(PID93536)으로 이어지며실제 여유공간약7GB를확인했다. 통과26종은반복하지 않았다. 나머지회귀/드론9종·전량 비교·전체 보존 및완료감사·5단계 문서는아직남아있다.
현재 내구도 적용durability-apply-policy가50875 PASS/FAIL0·실제 종료0으로 끝났다. 누적28종580663 PASS이며 종료 원문·정확한 기대/새 결과 개수·보관 원본 및 실제 출력 복원 해시 차이0을 감사했다. 같은 재개 세션15772를 직접 폴링해 실행 중임을 확인했다. 남은 기존 회귀2종/드론9종·전량 비교·전체 보존 및 완료감사·5단계 문서를 계속 진행한다.
현재 반응 적용reaction-apply가53295 PASS/FAIL0·실제 종료0으로 끝났다. 누적29종633958 PASS이며 종료 원문·정확한 기대/새 결과 개수·보관 원본과 실제 출력 복원 해시 차이0을 감사했다. 같은 재개 세션15772는마지막기존 회귀 반응 조회로이어진다. 30종 종료 뒤의전량감사/드론9종·전체보존 및완료감사·5단계문서는아직남아있다.

2026-10-07 추가 확인: 현재 최종 소스로 기존30종의 실제 종료0·687011 PASS/0 FAIL을 확인했다. RegressionAfterSupplyGroup/final-regression-audit.json은 전체18182행·188팩·225상태 논리 차이0, 소스1234개 동결, 기존 출력76개 복원, work/시작 HEAD 보존을 확인한다. 디스크 부족 실패 원문과 복구 근거는 유지했다. 같은 재개 세션15772가 드론9종을 이어서 검사 중이며, 전체 완료·보존 감사·5단계 인계는 아직 미완료다.

드론 Motion 실제 종료1: 표시 클립 검사에서 art 입력이 누락된 NullReferenceException이 발생했다. 실제 Begin은 art를 설정한 뒤 BuildClips를 호출하지만 기존 리플렉션 fixture는 이를 생략했다. 원문/종료/출력 복원은 FinalNew-20261006-190119/failed-motion-fixture에 보존했다. DroneFrameworkVerification.Motion.cs 한 검사 파일에 PuzzleArtwork 입력과 Dispose를 추가했으며 모든 비행 판정은 유지했다. drone-fixture-source-audit.json으로 나머지1233개 소스 동일·런타임 변경0을 확인했다. 통과한4종은 유지하고 Motion 및 미실행4종만 재개한다. 이전 최종30/신규22/관련15/수명3의 런타임 경계는 변하지 않았으며, 검사 파일 변경을 현재 소스 동일로 혼동하지 않는다.

Motion 입력 보정 후 실제 종료0·78 PASS/0 FAIL, 나머지4종까지 모두 완료했다. FinalNew-20261006-190119의 실제 실행9종은651 PASS/0 FAIL·모두 종료0이다. final-drone-audit.json은 정확한9개 진입점, 결과 원문 해시, 기존15개 출력 복원, 소스1234개를 확인한다. UTC/한국 시각을 섞었던 감사 스크립트의 비교를 양쪽 UTC로 정규화했으며 테스트 결과나 시간을 바꾸지 않았다. 기존30/687011 + 드론9/651 + 관련12/453 + 그래픽3/2130, 신규22/820 + 추가수명3/405의 실행 근거는 확보했다. 전체15개 완료 조건별 최종 감사·원본 보존 감사·5단계 문서는 남아 있으므로 아직 전체 완료로 표시하지 않는다.

## 4단계 최종 완료 보고 — 2026-10-07

A~D를 완료했다. 정의별 enum/풀을 늘리지 않고 등록 데이터로 편집·시험·월드 보드와 효과/공급 표현을 연결했다. 제작 원본은 값 DTO로 복사하며 게임 진입에 당시 시각 스냅샷을 전달한다. 미등록 ID/상태/필수 효과는 준비 전 오류로 거절한다. 규칙 팩2/지문은 아트와 분리했다.

필요 집합은 초기 배치·고정/랜덤/유지 공급·미션·기본 매칭/부스터/파워 조합·행동 생성까지 방문하고 순환/중복을 처리한다. native Addressables 요청 집합과 대조해 필요한 주소 누락0·미사용 별도 종류 주소 요청0을 확인했다. 공유 아틀라스의 동반 이미지는 별도 주소 요청으로 세지 않는다.

공통 슬롯은 반환/다음 사용에서 프레임/Sprite·색·위치/크기/회전·order·마스크·효과를 초기화한다. Playback/세션 Reset으로 클립·콜백·예약·취소 상태를 정리하고, 아트 소유권을 슬롯 반환과 분리했다. 기존 실제 씬의 반복/취소/다시하기/전환/Destroy/메모리 씬 Unload에서 잔상/참조 잔류0·준비 용량 안 추가 생성0을 확인했다. 선택 블록은 현재 이웃 최고 order보다 앞에 표시되고 손을 놓으면 복원된다.

HUD는 기존 uGUI 프리팹을 유지한 Presenter/읽기 전용 상태이며 DisplayedMissionProgress로 수집 도착 전/후 숫자를 표시한다. 봇에는 공개 원시 행동 특성만 복사한다. 공개 동일 입력의 관찰·후보·전체 평가·선택, 원본/난수 무변경과 곰팡이 아래 Unknown을 확인했다.

### 최종 실제 실행 근거

| 묶음 | 진입점 수 | PASS | 근거 |
| --- | ---: | ---: | --- |
| 기존 규칙 회귀 | 30 | 687011 | Phase04/RegressionAfterSupplyGroup/final-regression-audit.json |
| 드론 정책/비행 | 9 | 651 | Phase04/final-drone-audit.json, FinalNew-20261006-190119 |
| 3단계 제작/저장/편집 | 12 | 453 | Phase04/final-related-graphics-audit.json |
| 기존 그래픽 | 3 | 2130 | 같은 감사의 별도 graphics3 |
| 신규 표현/자원/풀/HUD/공개 관찰 | 22 | 820 | Phase04/final-new-audit.json |
| 추가 실제 세션/전환 수명 | 3 | 405 | Phase04/final-native-lifetime-audit.json |
| 합계 | 79 | 691470 | 각 실제 종료0·FAIL0 |

원문은 Logs/ElementFramework/Phase04 아래 각 실행의 종료 receipt·결과·복원 manifest에 보존했다. 실패/부분 실행을 합계에 넣지 않았다. 이전 RegressionFinal의 통과30종은 공급 parent SortingGroup 런타임 수정 전 증거이며 현재 최종30종으로 대신 쓰지 않았다. 실제 수정 후 RegressionAfterSupplyGroup에서 최종 검증했다. 이후 변경은 검사 입력/계약뿐이며 런타임은 동일하다.

전체18182행·188팩·225상태의 논리 차이0. raw 동일16919행과 승인된 표시 이력 메타데이터1263행을 구분하고, 3단계 기본 제작 메타데이터4381행도 별도 비교했다. 현재 raw SHA256은 DB73F87E3CA2F4347104E72E8912D3842CCEA8C6C01542F0E31EBF94B54685DC, 정규화 값은 E799F5800A6FD8029C3BEF833D38E1D4A89B5B54A3D1E0479E5820BBC5F4C898이다. 규칙 난수·피해·미션·공급·연쇄 차이를 표시 차이로 숨기지 않았다.

final-preservation-audit.json의 실제 감사 종료0: 원본1489개·과거 증거9137개·보호 비소스6843개·현재 소스1234개, 예상 밖 차이0. 기존 소스/meta를 삭제하지 않았고 새 C#/meta22쌍의 GUID 충돌0을 확인했다. work/HEAD b03ac0539e0286bd67763c88fcbd16f8316ac452와1~3단계 완료분을 보존했다. 시작 WIP는 없었으며 이후 현재 미커밋 작업을 그대로 유지한다.

### 완료 조건별 검토

completion-requirements-progress.json의15개 목표와 completion-source-review-audit.json을 확인했다. 사용자 지시에 따라 하위 에이전트 없이 작성자가 별도 자체 검토했다. 독립 리뷰를 받았다는 의미는 아니다.

| 목표 번호 | 직접 근거와 검증 범위 |
| --- | --- |
| 1~2 | visual29/boards11/legacy79: 제작 원본 변경·파기, DTO/카탈로그 독립, 서로 다른 두 실제 ID·공유 별칭·명시적 오류 |
| 3~4 | preparation13/metadata26/supply166/boards11/legacy16/flights59/ordering11 및 graphics3: 상태 축·프레임·피벗/크기/order, 실제 Sprite/renderer/UI 부모 합성 |
| 5~6 | resources106/effects4와 ResourcePlan 소스: 공급/생성/미션/기본 파워·순환/중복과 실제 native 주소 집합 대조 |
| 7 | native lifetime21 + session221/transition152/boundary32: pending 완료/콜백/공유 클론·후보 취소·다시하기·전환·파기 |
| 8~9 | world37/supply166/effect69와 실제 씬 수명: 고의 오염/반환·같은 인스턴스 반복·잔상0·선택 order/복원·Unload |
| 10 | hud12/session221: 기존 프리팹·표시 진행값·반복 슬롯·구독 해제·pause/입력·아이템 잠금 |
| 11 | bot41/layers42와 실제 소비자: 공개 특성 복사·Unknown·동일 관찰/후보/평가/선택·원본/난수 무변경 |
| 12 | 현재30/드론9/저장12와 boards11: 규칙/낙하/난수·스키마5/팩2·구형4/1·50구간·9×9·아트/룰 지문 독립 |
| 13~14 | 위 정확한79종·전량 논리 감사·보존 감사·검사 계약 경계와 원문. 빌드/팩 재생성/이미지 수정/커밋/푸시/하위 에이전트/사용자 Editor 종료/임의 씬 저장 없음 |
| 15 | 현재 완료 보고 및 다음5단계 계획/목표/전체 복사용 실행문. 구현 미착수 |

### 검사 계약 보정과 실패 원문

- 편집 아트 공통 부모 변경에 맞춰 두 기존 검사에서 CellAt 자식 조회를 ArtworkAt/AnnotationAt/LargeBodies로 바꿨다. 실제 그림/크기/order 기대는 유지했고 이전 검사 원문을 editor-query-contract-20261007에 보존했다.
- 빈칸 반환은 잔여 Sprite/축척/색을 유지할 대상이 아니므로 기존 settlement 검사 기대를 반환 상태로 바꿨다. 고의 오염 실패→최소 슬롯 수정→native 반복/반환 검증을 함께 보존했다.
- match 효과는 등록 공통 이름과 실제5색 native Sprite 프레임으로 검사했다. 이미 준비된 효과의 중복 Dictionary.Add와 준비 대기 fixture만 바로잡았다. 이전 검사2파일/실패 원문·강화된477 PASS는 match-effect-test-contract-boundary.json에 연결했다. 당시 나머지1232개 파일/런타임은 동일하다.
- 드론 Motion은 직접 BuildClips를 호출하면서 실제 Begin이 전달하는 art를 빠뜨렸다. 입력/Dispose3줄만 추가하고 기존 비행 판정78개를 유지했다. 실패 종료1→통과 종료0, 이전 소스와 원문은 drone-motion-test-boundary.json/failed-motion-fixture에 보존했다. 최신 경계는 검사1파일만 다르고 나머지1233개가 동일하다. 통과한 나머지 검사를 무의미하게 반복하지 않았다.
- 최종30종 도중 실제 디스크 부족으로 종료1이 발생했다. 부분 결과는 실패로 보존하고 기존Stage36 출력4개를 원본 해시로 복원한 뒤 미완료 검사만 재개했다. compact는0개 압축을 보고했으므로 확보된 여유 공간을 압축 효과라고 주장하지 않는다. disk-full-recovery.json과 실패 원문은 보존했다.
- 드론 감사 스크립트의 UTC/한국 시각 비교를 양쪽 UTC로 맞췄다. 실제 실행 시간/결과는 수정하지 않았다.

### 제약과 별도 기존 실패

검증은 Unity Editor/실제 메모리 Play Mode·기존 native Addressables 자원·renderer/UI 트리/클립/객체 수명 범위다. 모바일 실기기·플레이어 빌드·새 콘텐츠 번들·프레임/메모리 성능을 검증했다고 확대하지 않는다. 지연 콜백 검사는 OnAtlasRequested 진입을 직접 호출하고 native 로드/완료를 확인했으며 엔진 SpriteAtlasManager 이벤트 자체를 유도한 검사는 아니다. 등록 order 검사는 합성 트리/renderer를 확인했으며 전 픽셀 캡처 동등성은 아니다.

기존 추가 BoardArtwork 종합 검사는 Edit Mode의 Android BundledAssetProvider 번들에 드론 시트가 없는 항목에서474 PASS/1 FAIL이었다. 추가 PlanningBoundaries의10×10 fallback 기대도9×9 기준에서 기존HEAD 동일 실패를 확인했다. 둘 다 필수 최종54종/신규25종의 통과 결과에 포함하지 않았다. 기존 번들 빌드/로딩 정책 또는 봇 전략을 이번 범위에서 변경하지 않았고 원문을 보존했다. 후속 해당 영역 작업에서 따로 다룬다.

자체 최종 검토에서 이번 목표에 대한 추가 미해결 결함은 없었다. 수백 개 제작 콘텐츠와 새 행동의 실제 확장 비용은5단계에서 검증하며4단계 결과로 미리 보증하지 않는다.

### 다음 구간 인계

[5단계 계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/phase-05-extension-validation-plan.md) · [목표](../../../Goals/MoonRabbitJunkyard/ElementFramework/phase-05-extension-validation-goal.md) · [전체 복사용 실행문](../../../Commands/MoonRabbitJunkyard/ElementFramework/phase-05-command.md)을 작성했다. A 데이터 신규3개, B 제작500개/측정, C 새 행동/필요 정책, D 통합 회귀/검증된 중복 정리/추가 안내의 네 묶음이다. phase05-document-audit.json에서 문서3개와 상대 링크를 확인했다. 5단계 구현은 사용자 실행 지시 전 시작하지 않는다.

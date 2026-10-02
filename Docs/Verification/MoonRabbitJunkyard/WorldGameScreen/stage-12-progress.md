# 12단계 실행 기록 — 실제 게임 화면·연출 품질 검수

상태: 2026-10-02 구현·Editor 검수 완료. 독립 리뷰 한 번의 Important1/Minor1을 처리했고 최종 조건10개를 감사했다. 실제 청취·실기기 검증은 미수행이다.

[계획](../../../Planning/MoonRabbitJunkyard/WorldGameScreen/stage-12-presentation-plan.md) · [목표·완료 조건](../../../Goals/MoonRabbitJunkyard/WorldGameScreen/stage-12-presentation-goal.md) · [사용 안내](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-12-presentation-usage.md) · [조건별 상세 감사](../../../../Logs/Stage12/requirement-audit.md)

## 완료 조건별 근거

| 조건 | 판정 | 근거 |
| --- | --- | --- |
| 1 착수 감사 | 충족 | 11단계 완료/현재 결과음 수정·dirty/HEAD·83파일 기준 기록 |
| 2 Asset/MemoryPack 선택 맵 | 충족 | 실제 씬 선택 사본·원본 불변·입력 준비. 기본 촬영에서 두 소스를 교대 사용 |
| 3 기본16장 | 충족 | 네 해상도×play/pause/won/lost 실제 PNG·픽셀·정확한 manifest. 목업3장과 직접 비교 |
| 4 inset4·UI/팝업/입력 | 충족 | 정확한 Rect(24,36,w-48,h-72) 플레이4장. 추가 InsetUI67PASS·32경계·실제 pause4장·글자 mesh/좌표 왕복 |
| 5 식별성 | 충족 | 4색·4파워·6장애물·캡슐 열린 상태·기둥1/9·덮개·미션·아이콘·선택 실제 렌더. 목업의 의도된 차이 기록 |
| 6 실제 움직임 | 충족 | 실제 Update18사례·4파워/10조합/교환/복귀/라스트팡/2×2·시간순렌더와93199좌표·전체 직접 상태 동등 |
| 7 입력/회전/복귀 | 충족 | 실제 UI/pointer·중복 행동·order·수집 원본 소멸 후 회전·pause/Retry·잔류0. background 승패×pause4경계 복귀음1/추가0 |
| 8 청취/기기 구분 | 충족 | 청취0회·실기기0회·GIF재생 미수행을 명시. 자동 재생/예약을 음질 확인으로 세지 않음 |
| 9 최소 수정 | 충족 | 제품 런타임/이미지/프리팹 변경0. Editor 복원 결함 RED Exit1→GREEN3PASS/Exit0 및 cleanup 실패 전파 Exit1 |
| 10 회귀/보존/리뷰/안내 | 충족 | 최종17개 모두 PASS>0/FAIL0/실제Exit0, 세 finally 복원PASS, final-gate 실제Exit0, 83불변·7meta 및 단일 리뷰 조치 감사 |

## 실제 화면과 움직임

리뷰 후 촬영은 `Logs/Stage12/review-fix-capture-results.txt` 238PASS/0FAIL/실제Exit0이다. 기본16장과 inset-play4장은 이전 검수20장과 SHA256이 모두 같아 목업 비교 관찰이 그대로 유효하다. `post-fix-capture-comparison.json`과 `capture-file-audit.json`으로 확인했다. 촬영 요청만으로 통과시키지 않았다.

리뷰 후 Motion은3512PASS/0FAIL/실제Exit0, Interaction은158PASS/0FAIL/실제Exit0이다. 18사례는 모두 전체State/Phase/Outcome/Turn이 직접 실행기와 일치한다. 실제 렌더1110장 중1027개가 서로 다른 내용이며 좌표93199건·36개 사례/라벨 묶음이 있다. 숫자는 반복 프레임/픽셀/보존 단언을 포함하며 고유 테스트 수가 아니다.

최신18개 시간순서 시트도 모두 직접 열어 로켓·폭탄·드론·자석, 10조합, 교환/무효복귀, 수집·동시낙하·공급·라스트팡·2×2의 중간/종료 표시를 확인했다. 실제 TimeScale1 Update와 시각/좌표 분석을 함께 사용한다. 정지1장·수동Tick·GIF재생으로 실제 움직임 관찰을 치환하지 않는다. 드론 표적 소멸은 이미 실행된 논리 결과의 빈 칸과 시각 비행 사이의 경계이며 새 실시간 재표적 규칙을 만들지 않았다.

추가 inset UI 검사는67PASS/0FAIL/실제Exit0이다. 네 해상도에서 HUD·아이템·pause·안내·보드·두 팝업Panel과 안내 글자 mesh의 실제 경계32개를 검사했다. pause PNG4장을 디코딩하고 모두 직접 열었다. 결과Panel은 비활성 상태의 실제 RectTransform 경계를 확인했으며 승패의 활성 화면은 기본16장에 있다. 팝업의 의도된 보드 덮임은 결함으로 세지 않는다.

600×800 inset의 HUD/보드 맞닿음은 행렬 오차0.000061px였다. 안내 컨테이너의 빈 여백3.68px와 실제 글자를 구분했다. CanvasRenderer의 실제 글자 mesh는 보드와10.31px 떨어졌고 raycastTarget=false였다. 검사 전제를 보정했으며 게임 배치/문자/속도를 바꾸지 않았다. API 초안의 CS1501과 이전 실패는 별도 로그로 보존했다.

## 독립 리뷰와 수정

[최종 리뷰](../../../../Logs/Stage12/final-review.md)는 한 번 수행했다. 리뷰 당시 Critical0/Important1/Minor1이었다. 두 번째 리뷰는 요청하지 않고 실행자가 조치 결과를 감사했다.

Important1: `PuzzleUIRenderVerification.RestoreSize`는 상대 인덱스를 삭제 함수에 전달하고 Stage04 이름 전체를 지우려 했다. 새 회귀가 실제Exit1로 실패했다. 현재는 실행 전 전체 목록/선택을 저장하고 실제 추가 항목만 추적해 기본 항목 포함 인덱스로 제거한다. 기존 같은 이름의 항목을 보존하며 복원 동등성을 검사한다. Stage12 세 finally는 cleanup 뒤 결과를 기록하고 실패를 FAIL/Exit1로 전파한다. GREEN3PASS와 기대 목록만 훼손한 negative cleanup 실제Exit1을 확인했다.

Minor1: 재실행 사용 안내에 각 메서드의 필수 -Result 경로를 명시했다. 사용자 문서 요청 범위에서 처리했으며 미뤄둔 Minor는 없다.

기존 helper 수정의 전후 소스/해시는 `game-view-helper-before.cs`, `game-view-helper-before-hash.json`, `game-view-helper-after-hash.json`으로 공개했다. 기준83파일을 새 기준으로 덮어쓰지 않았다. 리뷰 전 이미지/manifest는 `pre-review-fix/`에 보존했다.

## 최종 회귀

`Logs/Stage12/final-suite-results.txt`의 실제 새 실행이다. 각 결과와 종료를 확인했다.

- game-view: PASS=3 FAIL=0 Exit=0
- presentation-data: PASS=146 FAIL=0 Exit=0
- stability-data: PASS=54 FAIL=0 Exit=0
- stability-scene: PASS=2170 FAIL=0 Exit=0
- stability-observe: PASS=74 FAIL=0 Exit=0
- ui-layout: PASS=18 FAIL=0 Exit=0
- ui-interaction: PASS=97 FAIL=0 Exit=0
- audio-data: PASS=26 FAIL=0 Exit=0
- audio-scene: PASS=1591 FAIL=0 Exit=0
- progress-data: PASS=168 FAIL=0 Exit=0
- progress-scene: PASS=181 FAIL=0 Exit=0
- swap: PASS=251 FAIL=0 Exit=0
- swap-scene: PASS=26 FAIL=0 Exit=0
- settlement: PASS=282 FAIL=0 Exit=0
- settlement-scene: PASS=106 FAIL=0 Exit=0
- power-scene: PASS=852 FAIL=0 Exit=0
- editor-entry: PASS=9 FAIL=0 Exit=0

추가 InsetUI는 별도67PASS/0FAIL/Exit0이며 위17개 목록과 구분한다. 실제 준비 래퍼를 포함한 낙하 본문은106PASS였다. 마지막 준비1.252초/1753프레임이다. 최초 낙하 실패의 시간 자료가 없으므로 원인을 확정하지 않는다. 래퍼는 동일 기존 본문과 HasFailed/30초 실패 경계를 유지한다.

## 보존과 한계

기준83파일 SHA256은 모두 그대로다. 신규 Editor partial6개·InsetUI 검사1개와 meta7개의 GUID가 유효하고 서로 다르다. 기존 Editor 복원 helper만 별도 변경했다. 런타임·프리팹·원본Level01/씬·아이콘/속도/결과음 기준을 보존했다. 83개를 전체 저장소 파일 수로 과장하지 않는다.

실제 청취0회·실기기0회다. 음질·기기 음량/FPS/입력 지연·물리notch·주관적 실시간 체감은 미검증이다. GIF는 브라우저 정책으로 재생하지 않았고 우회하지 않았다. 이전9~11단계 전체 알고리즘을 새로 재설계/승인한 것도 아니다.

빌드·커밋·푸시·사용자 Unity 강제 종료·자동 씬 저장·13단계 구현/목표 착수는 하지 않았다. 문서로 작성한13단계는 별도 실행 대상이다. 작업 트리와 ledger·증거를 유지한다.

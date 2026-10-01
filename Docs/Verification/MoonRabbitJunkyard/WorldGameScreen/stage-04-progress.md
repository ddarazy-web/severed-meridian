# 4단계 목업 UI 구현·검증 기록

계획: `Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/stage-04-mockup-ui-plan.md`

상태: 4단계 구현·검증 완료 (2026-10-01). 2026-09-30 시작. 아래 중간 기록의 대기/차단 상태는 당시 이력이며 최종 판정은 문서 끝의 완료 감사에 따른다. 이전 단계 결과는 stage-03-progress.md 참조.

## 실행 판단

- Ruling: 사용자가 지정한 현재 ServeredMeridian/work 체크아웃에서 진행한다. 별도 worktree는 만들지 않는다 — 현재 씬/에셋과 1~3단계 기반을 그대로 검증하기 위함 — 다른 동시 변경과 섞일 위험은 diff로 확인한다.
- Ruling: 자동 커밋과 셸 전용 실행 스킬의 커밋 기반 장부 대신 이 기록과 Logs/PuzzleUIVerification을 사용한다 — 사용자 자동 커밋 금지 및 Windows 환경을 따름 — 각 작업 상태와 검증 증거를 명시한다.
- 현재 프로젝트 Editor가 실행 중이지 않음을 프로세스 목록으로 확인했다. 다른 프로젝트 Editor는 조작하지 않는다.

## 사전 인터페이스 확인

| 생산 작업 → 소비 작업 | 계약 | 확인 |
| --- | --- | --- |
| 1 → 3/4 | pause/retry/item 세션 API | 현 코드에 없으므로 추가 필요 |
| 2 → 3 | 보드 viewport와 screen 좌표 입력 | 기존 입력은 camera.pixelRect를 사용, 종료 지점 UI 차단 추가 필요 |
| 1/2 → 4 | Changed와 종료 상태 표시 | 기존 Changed 재사용, 결과는 Stopped 이후 |

## 중간 작업 상태 (최종 결과는 아래 완료 감사 참조)

1. 세션 제어: 기본 구현, 24개 동작 검사 통과. 추가 수명 검사는 대기.
2. UI 배치: 구현·네 비율 렌더 확보. 재시작 직후 배치 수정 재검증 대기.
3. 아이템 입력: 구현, 통합 검사 진행 중.
4. 팝업: 구현, 일시정지 일부 통과. 승리/실패 시나리오 실행 대기.
5. 통합 검증: 진행 중. Editor 종료 승인 대기; 최종 리뷰·회귀·인계 미완료.

계약 존재 검사는 기능 검증을 대신하지 않는다. pause·재시작·아이템의 행동 검사를 추가하고 실제 Editor 화면 검증까지 완료해야 한다.

## 중간 증거 (2026-09-30)

- 계약 검사 RED: 새 세션 API 7개 부재. 구현 후 GREEN: 7 PASS.
- 실제 Unity Play 세션 검사: 에셋 사본/MemoryPack 각각 망치 → 연쇄 중 정지 8프레임 → 재개 → 기준 실행기 상태 일치 → 5회 스냅샷 재시작 → 원본 보존, 총 24 PASS (`Logs/PuzzleUIVerification/playback-results.txt`). 추가 실패/취소·입력 검사는 아직 남아 있다.
- 배치 검사 RED: 가로 목업 보드 위치 불일치. 세로 하단 여백 제한을 가로에도 적용한 계산을 수정. 현재 18 PASS (`layout-results.txt`). 실제 렌더링과 UI 요소 간 겹침 검사는 아직 남아 있다.
- 기능별 UI 프리팹 6개 생성. 목업 SVG 4개를 128px PNG로 변환하고 64px 9-slice UI 마스크를 추가했다. AI 이미지 생성은 수행하지 않았다.
- Runtime UI/입력/팝업 연결 구현 중. 아직 시각 품질/최종 회귀 검증 전이며 완료로 취급하지 않는다.
- ServeredMeridian Editor를 새로 열었으며 Windows 잠금 화면으로 직접 클릭이 현재 제한된다. 사용자에게 잠금 해제를 요청하고 열린 Editor 내부 자동 검증을 계속한다.
- 임시 `StageFourLiveRunner.cs`는 `Logs/PuzzleUIVerification/live-request.txt`의 제한된 명령을 처리한다. 인계 전에 스크립트와 meta를 제거해야 한다.

## 다음 검증 지점

- 세션 계약 7 PASS, 세션 동작 24 PASS, 계산 배치 18 PASS까지 확인. 최종 통합 완료 수치는 아니다.
- `PuzzleUIRenderVerification.Run`이 실제 Game View를 1280×720, 450×800, 450×975, 600×800으로 바꾸어 `screen-*.png`를 생성했다. 가로/세로를 열어 확인했다. 배경 카메라·목업 배경 변환·미션 트레이 높이·선택 강조·글꼴 개선을 이어 적용 중이다.
- 기존 목업의 배경 캔버스를 2048×1024 / 1024×2048로 변환했다. 새 그림 생성이 아니다. 목업 SVG 아이콘과 마스크까지 모든 추가 래스터 크기는 2의 거듭제곱이다.
- NotoSansKR-Mockup.ttf의 가변축 기본값이 wght=100임을 확인했다. Unity 가독성을 위해 기존 OFL 글꼴을 wght=600으로 고정하고 MoonRabbit UI로 이름을 바꾼 `Assets/Fonts/MoonRabbitUI-Regular.ttf`를 생성했다. 변환 도구 fonttools는 무시되는 Logs/PuzzleUIVerification/font-tools에만 설치했으며 Unity 패키지 변경은 없다.
- 재생성 검증에서 배경 카메라 추가로 기존 Single Camera 검색이 실패했다. PuzzleScreenView 하위 카메라를 제외해 보드 카메라를 선택하도록 수정했다. 이후 generate 성공을 확인했다.
- 남은 작업: 새 글꼴/배경 포함 최종 4비율 렌더 확인, 모든 미션 수 대응, 미션 설명/선택 강조, UI 시작·종료 차단, 세 아이템의 실패/취소/연타, 라스트팡·승리·실패 팝업 실제 행동 검사, 재시작 취소/오류/중복 수명, 두 Editor 입력 모드 통합 및 기존 회귀, 프리팹 재생성 멱등성, 사용 가이드/목표 체크, 최종 독립 리뷰.
- 검사 도구 `PuzzleUIRenderVerification.SetSize`는 Stage04 이름의 Game View custom size를 현재 추가한다. 반복 추가를 재사용으로 바꾸고 검사 후 이 작업에서 추가한 size와 선택 인덱스를 정리할 것. 기존 사용자 항목을 지우지 않는다.
- 마지막 Play는 stop 요청으로 종료했다. 열린 Editor는 `Logs/stage04-interactive.log`, 임시 runner는 live-request의 refresh/generate/layout/contract/playback/play/stop/gameplay/scene/render를 처리한다. 코드를 바꾼 뒤 refresh로 컴파일하고 그 다음 generate/render를 순차 요청한다. 같은 요청 파일을 처리 전에 덮어쓰지 않는다.
- Windows 잠금 해제 요청은 아직 답이 없다. Editor 내부 렌더링은 가능하며 실제 OS 클릭 캡처는 아직 검증하지 않았다.

## 입력 통합 RED → 수정 기록

- 미션 설명 표시 직후 Raycast 갱신 전 보드 입력이 통과하는 RED를 재현. 팝업 수명 동안 `PuzzleBoardInput.SetUIBlocked`로 즉시 차단하도록 수정했다. 후속 실행에서 설명 열기/차단/닫기와 망치·pause·회전·재개·다시하기까지 PASS.
- 재시작 직후 미션 Target의 오래된 RectTransform이 보드 좌표의 Raycast에 잡혔다. 좌표 변환/대상 판정은 정상이며 hits=Target임을 기록했다. 미션 배치를 즉시 재계산하도록 수정하는 과정에서 ApplyLayout → CancelItemSelection → Refresh 재진입이 생겼다.
- 재귀 오류 로그를 `Logs/PuzzleUIVerification/recursive-layout-red.log`에 보존했다. lastSafe/lastSize를 선택 취소 알림보다 먼저 기록하도록 수정. 기존 Editor PID 종료를 확인한 뒤 ServeredMeridian만 다시 열었다. 복구 후 재검증 대기이며 아직 이 문제를 완료로 기록하지 않는다.
- 미션 수는 기존 LevelMissionRules에서 1~4개로 제한되어 있다. 4개 초과용 새 UI/규칙은 추가하지 않는다.

## 검증 환경 중단과 남은 승인

- Get-Process가 실제 호스트 Unity를 누락하여 종료된 것으로 잘못 판단했다. 이후 CIM으로 원래 ServeredMeridian PID 395488과 프로젝트 잠금에서 대기 중인 재실행 PID 413760이 함께 남아 있음을 확인했다. 앞으로 이 환경의 종료 여부는 CIM 결과와 Editor 로그를 함께 확인한다.
- 두 검증용 프로세스의 경로/PID를 제한한 강제 종료가 자동 승인 검토에서 거절됐다. 이유: 미저장 씬·Editor 상태 손실 가능성. 사용자에게 두 PID 종료 승인을 요청했다. 승인 전 종료 우회나 추가 Editor 실행을 하지 않는다.
- 대기 live-request 파일을 제거했다. 재귀 수정 이후 동작 검증은 아직 실행되지 않았다. 사용자 승인 또는 직접 종료 후 프로세스 상태를 확인하고 검증을 재개해야 한다.
- 재귀 수정 후 CancelItemSelection은 실제 선택/메시지 변화가 있을 때만 알림을 보낸다. 잘못된 아이템 대상의 안내와 원자성 검사를 추가했다. 이 추가분은 컴파일·실행 미검증 상태다.

## Editor 외부 컴파일 확인

- Unity가 생성한 현재 응답 파일을 Logs/PuzzleUIVerification/compile로 복사하고 out/refout과 Editor의 런타임 참조만 해당 격리 경로로 바꿨다. Unity Library/실행 중인 프로세스에 DLL을 주입하거나 덮어쓰지 않았다.
- Unity 번들 Roslyn csc.dll로 최신 런타임과 Editor 코드를 순차 컴파일: 모두 성공. runtime.txt 오류/경고 없음, editor.txt에는 기존 InstanceIDToObject 사용 중단 경고 등이 남아 있다. 실행/렌더링 검증을 대체하는 결과는 아니다.
- 최신 검사 보강: 미션 1~4개 × 네 화면 비율의 실제 RectTransform 범위 검사, 잘못된 두 번째 아이템 칸의 원자성/안내, 이전 아틀라스 반환과 구독 수 보존. 이 검사들은 Editor 복구 후 실행해야 한다.
- 미션 이미지 경로 매핑은 UI 뷰에서 PuzzleArtworkPaths로 이동해 월드/세션이 UI 타입에 의존하지 않도록 했다.

## 완료 조건 감사 — 승인 대기 2회차

직전 목표 턴은 코드 수정·격리 컴파일·문서 갱신으로 진행이 있었다. 이번 확인에서도 CIM상 ServeredMeridian 주 프로세스 395488 및 잠금 대기 413760이 남아 있고, 실제 조작 검사 결과는 23:41:33 이후 갱신되지 않았다. 종료 승인은 아직 없다. 같은 종료 승인 대기 조건의 두 번째 목표 턴으로 기록한다.

| 목표 완료 조건 | 현재 증거 | 판정/남은 작업 |
| --- | --- | --- |
| 프리팹·씬·컴파일·GUID | UI 6종 생성, 최신 격리 Roslyn 컴파일 성공 | Unity 재컴파일·최종 재생성 멱등성 미확인 |
| HUD·이미지·한국어·IMGUI 대체 | 실제 HUD 숫자/미션 검사와 pause 렌더 확인 | 최신 배치 수정 포함 재검증 필요 |
| 네 비율·안전 영역·회전 | 네 비율 PNG, 계산 배치 18 PASS, pause 중 회전 상태 보존 | 미션 1~4개 실제 범위 검사 추가분 미실행 |
| UI 입력 경계·마우스/터치 | 설명 팝업 즉시 차단 수정 후 해당 시나리오 PASS | 최신 입력 검사·다중 포인터 회귀 미실행 |
| 세 아이템의 성공/실패/취소/연타 | 망치·취소 PASS, Swap 재시작 직후 실패 원인 조사 | 수정 후 Swap/Shuffle 및 실패/연타 전체 검사 미완료 |
| 연쇄 일시정지/재개 | 24개 세션 검사에서 기준 실행기 일치, UI pause 10프레임 동결 | 이후 변경을 포함한 최종 재실행 필요 |
| 승리·실패·라스트팡 | 검사 코드 작성 | 앞선 실패로 해당 구간까지 실행되지 않음 |
| 두 입력 모드 재시작·수명 | 초기 세션 검사에서 각각 5회 동일 보드 재현 | 아틀라스 반환·구독 수·취소 검사 추가분 미실행 |
| 이전 단계 회귀·에디터 복귀 | 3단계 과거 증거만 존재 | 4단계 변경을 포함한 회귀 실행 필요 |
| 화면·사용법·검증 기록 | 가로/세로/선택/pause 이미지, 사용 안내 작성 | 승리/실패·최종 화면, 독립 리뷰, 임시 runner 제거 미완료 |

읽기 전용 점검에서 Assets/Data, Packages, ProjectSettings의 diff는 없으며 Level_01 GUID의 씬/프리팹 직접 참조도 검색되지 않았다. 이는 전체 Unity 의존성/플레이 검증의 대체 증거가 아니다. 4단계 완료는 입증되지 않았고 목표를 완료 처리하지 않는다.

## 승인 대기 3회차 — 진행 불가 전환

세 번째 연속 목표 턴에서도 CIM으로 주 프로세스 395488/413760이 유지됨을 확인했다. 조작 검사 파일은 2026-09-30 23:41:33 이후 갱신되지 않았다. 사용자 종료 승인은 도착하지 않았다. 독립적으로 가능한 코드 수정·격리 컴파일·문서 감사는 끝났고 남은 조건은 Editor 실행 검증과 그 결과에 따른 수정이다. 자동 승인 거절을 우회하지 않고 목표를 blocked로 전환한다. 완료 처리가 아니다.

재개 조건: 사용자가 두 검증용 프로세스의 종료를 명시적으로 승인하거나 직접 ServeredMeridian Editor를 종료한다. 그 후 CIM으로 실제 종료를 확인하고 한 인스턴스만 열어 최신 코드 컴파일 → UI 생성 → 조작 통합 → 기존 회귀 → 최종 독립 리뷰 순으로 이어간다. 남아 있는 임시 runner는 최종 검증 후 제거한다.

재개 후 승인 대기 1회차: 목표 상태 active 확인. CIM에서 잠금 대기 PID 413760은 사라졌고 원래 PID 395488만 남았다. UI 창 활성화를 시도했으나 해당 Editor 화면/응답은 확보되지 않았다. 강제 종료 승인은 여전히 없으며 종료를 재시도하지 않았다.

재개 후 2·3회차에서도 CIM상 PID 395488 유지, 종료 승인 없음. 같은 차단 조건이 세 번 연속 확인되어 목표를 다시 blocked로 전환한다. 남은 실행 검증과 최종 리뷰는 아직 미완료이며 코드/검사/문서는 보존한다.

2026-10-01 재개: CIM에서 기존 ServeredMeridian 주 프로세스가 없음을 확인했다. 강제 종료 없이 단일 Editor PID 17832를 실행했다. 이전 씬 복구 사본 보존을 선택했으며 원본 씬을 덮어쓰지 않는다. 최신 소스의 Editor 컴파일·재생성·조작 검사 재개 중.

## 복구 후 실행 검증 — 2026-10-01

- 기존 차단 조건 해소: CIM에서 원래 프로세스가 없음을 확인하고 단일 Editor를 실행했다. 복구 씬은 Assets/_Recovery/0.unity 사본으로 보존했다. 강제 종료는 수행하지 않았다.
- 재귀 오류 수정 후 실제 조작 검사 실행. 태블릿 600×800에서 HUD와 보드의 겹침이 추가로 재현됐다(RED). 세로 레이아웃의 최소 상단 여백을 확보하도록 보드 크기/위치를 수정해 네 비율 × 미션 1~4개의 실제 RectTransform 비중첩 검사와 전체 조작 검사가 통과했다(GREEN).
- 최신 컴파일은 실제 Unity Editor에서 오류 없이 완료. 단일 Editor의 임시 명령 runner로 아래 정식 검증 진입점을 호출했다. 입력 검사는 Input System 가상 장치이며 실기기 터치 검증이 아니다.

| 검증 진입점 | PASS | FAIL | 증거 |
| --- | ---: | ---: | --- |
| PuzzleUIInteractionVerification.Run | 68 | 0 | Logs/PuzzleUIVerification/interaction-results.txt |
| PuzzleUIPlaybackVerification.Run | 48 | 0 | Logs/PuzzleUIVerification/playback-results.txt |
| PuzzleUILayoutVerification.Run | 18 | 0 | Logs/PuzzleUIVerification/layout-results.txt |
| PuzzleUIStateVerification.Run | 7 | 0 | Logs/PuzzleUIVerification/contract-results.txt |
| PuzzleGameplayVerification.Run | 29 | 0 | Logs/PuzzleGameplayVerification/results.txt |
| 위 검사에 포함된 PuzzleBoardInputVerification | 23 | 0 | Logs/PuzzleBoardInputVerification/results.txt |
| PuzzleGameSceneVerification.Run | 16 | 0 | Logs/PuzzleGameplayVerification/scene-results.txt |
| PuzzleEditorLaunchVerification.RunData | 9 | 0 | Logs/PuzzleEditorLaunchVerification/data-results.txt |
| PuzzleEditorLaunchLifecycleVerification.Run | 137 | 0 | Logs/PuzzleEditorLaunchVerification/lifecycle-results.txt |
| BoardArtworkLifecycleVerification.Run | 5 | 0 | Logs/BoardArtworkLifecycleVerification/results.txt |

360개 PASS. 이 수에는 계약 존재 검사와 반복 비율/미션 검사도 포함되며 360개의 서로 다른 사용자 기능을 뜻하지 않는다. 원본/팩의 변경 이후에도 요청 스냅샷이 고정되는 데이터 검사, 두 입력 모드 각 5회 재시작, 이전 아틀라스 반환, 구독 수 보존, 연타, 취소 후 복구, 재시작 중 소유자 종료가 포함된다. 실제 에디터 실행 버튼과 편집 복귀는 domain reload 양쪽 모드에서 검사했다.

프리팹 생성기를 연속 두 번 실행했고 UI/Game 프리팹 meta SHA256 전후 비교가 동일했다. 구체적인 씬 단일 구성/직접 레벨 참조 의존성 검사는 별도 기록한다.

최신 화면은 Logs/PuzzleUIVerification의 screen-1280x720.png, screen-450x800.png, screen-450x975.png, screen-600x800.png(00:18:59~00:19:00), item-selected.png, swap-selected.png, pause.png, won.png, lost.png(00:13:35~38)다. 네 비율과 승리/실패 이미지를 직접 열어 한국어 글꼴, 버튼, 미션, 보드가 잘리지 않음을 확인했다. 캡처 후 Game View 설정을 복구하고 Play를 종료했다.

executing-plans가 요구하는 최종 독립 리뷰 1회를 진행 중이다. 리뷰와 최종 정리가 끝나기 전에는 완료 처리하지 않는다.

Final review: 독립 리뷰에서 Critical/Important 없음, Minor 1건(섞기 실패 이유가 무제한 문구에 가려짐)을 받았다. 실제 사용자는 눌렀는데 반응이 없는 이유를 알 수 없으므로 완료 조건의 잘못된 아이템 사용 안내에 해당하는 Important로 재평가했다. 기존 불가능 섞기 fixture를 실제 UI에 넣어 실패를 재현했고 Logs/PuzzleUIVerification/shuffle-feedback-red.txt에 보존했다. 실패 메시지를 선택 유무와 무관하게 표시하고 다음 아이템/보드 조작에서 지우는 수정 1회를 적용했다. 최종 GREEN 및 회귀 검증 대기. 재리뷰는 요청하지 않는다.

Final: fixed 섞기 실패 안내 누락 — 실제 불가능 섞기 UI fixture RED→GREEN, 조작 통합 74/74 PASS. 실제 UI 5회 재시작 때 최초 상태·구독·레벨 사본 수·단일 화면도 추가 확인했다. 단일 UI/세션/EventSystem/InputModule, 임시 IMGUI 부재, 모든 UI/Game 프리팹 및 게임 씬의 전이 의존성에 LevelDefinition 없음의 3개 에셋 감사도 PASS. 프리팹 meta 전후 SHA256 동일, 새 에셋 meta 누락 없음. 입력 수정 이후 전체 회귀 재실행 중이며 완료 조건 갱신은 그 뒤 수행한다.

## 최종 완료 감사 — 2026-10-01

최종 조작 검사에 네 비율 × 비대칭 안전 영역에서 실제 HUD/아이템/일시정지/안내/보드 RectTransform 범위 20개를 추가했다. **조작 94, 세션 48, 배치 18, 계약 7, 게임 29, 입력 23, 씬 16, 입력 데이터 9, 편집 왕복 137, 편집 이미지 5 = 386 PASS / 0 FAIL**. 별도 에셋 구성 감사 3 PASS. 최종 리뷰 수정 이후 영향받는 전체 검사 진입점을 재실행했다. 승리/실패/선택 캡처도 최신 조작 검사에서 다시 생성했다.

| 원래 완료 조건 | 최종 근거와 판정 |
| --- | --- |
| 컴파일·기능별 프리팹·GUID | 실제 Editor 컴파일, UI 6종/Game 기존 역할 유지, 생성기 두 번 실행 후 meta SHA256 동일, 에셋 감사 단일 화면/세션/입력 시스템 PASS |
| 실제 HUD·확정 이미지·한국어·IMGUI 제거 | 조작 검사 실제 이동 수/미션 수, 직접 본 4비율 PNG 및 팝업 PNG, 에셋 감사에서 DebugView 없음 |
| 네 비율·안전 영역·회전 | 94개 조작 검사 중 미션 1~4 배치, 실제 UI 20개 비대칭 안전 영역 검사, pause 상태/보드/난수 보존 및 18개 계산 검사 PASS |
| UI 입력 경계·마우스·터치·다중 포인터 | 입력 검사 23 PASS. UIStartIgnored/UIEndIgnored, 합성 마우스 중복, 두 번째 포인터, 빠른 터치 포함 |
| 아이템·선택·취소·실패·연타 | 실제 버튼/보드 조작 검사, 기존 실행기와 망치 연쇄 일치, Swap/Shuffle 이동 수 보존, 잘못된 대상 원자성, 섞기 불가능 안내, 준비 중 명령 차단 PASS |
| 연쇄 중 pause/resume | 실제 pause 버튼 10프레임 동결, 세션 기준 실행기와 결과 일치, 회전 후 pause 상태 유지 PASS |
| 승리/실패·라스트팡 | 승리 확정과 Stopped 사이 팝업 금지, 라스트팡 종료 후 표시, MovesExhausted 표시, won.png/lost.png |
| 에셋/팩 독립 재시작·수명 | 두 입력 소스 각 5회 최초 보드 재현·아틀라스 반환, 재시작 연타·취소·소유자 종료, 실제 UI 5회 구독/사본/화면 수 보존. 데이터 9개 검사에서 원본/팩 변경 후 요청 스냅샷 고정 |
| 기존 실행/편집 복귀·이미지 | 게임 29/씬 16/편집 왕복 137/편집 이미지 5 PASS, 실제 편집창 게임 플레이 버튼 실행, domain reload 양쪽, 원본 파일/dirty 상태 보존 |
| 화면 증거·사용법·제외 범위 | Logs/PuzzleUIVerification PNG 9종과 본 기록, stage-04-mockup-ui-usage.md, 계획/목표/로드맵 갱신 |

임시 StageFourLiveRunner 스크립트와 meta는 제거했다. 검사 fixture 파일은 정리됐으며 복구 씬 사본은 보존했다. Assets/Data, Packages, ProjectSettings에는 변경이 없고 기존 분할 Addressables/50레벨 MemoryPack을 유지한다. 레벨 에셋은 씬/프리팹의 전이 의존성에도 포함되지 않는다. 자동 커밋·푸시·다른 프로젝트 조작·5단계 작업을 하지 않았다.

최종 리뷰 미해결 사항: 없음. 리뷰의 섞기 안내 1건은 재현 후 수정·회귀 검증했다. 별도 새 이미지 생성은 없으며 목업의 배경/아이콘 변환과 기존 글꼴 고정만 사용했다.

판단 기록: 지정된 work 체크아웃을 유지했다(동시 변경 혼재 위험은 diff로 확인). 자동 커밋 금지에 따라 커밋 기반 장부 대신 본 문서와 Logs를 유지했다(증거 보존 책임이 파일에 남음). finishing-a-development-branch의 통합 선택은 사용자의 자동 커밋 금지와 현재 경로 유지 지시를 따라 현 상태 보존으로 처리했다.

미검증/범위 밖: 플레이어 빌드, Android/iOS 실기기 입력·노치·회전, 광고/결제/보상, 다음 레벨, 상세 애니메이션, 5단계 전체 빌드 통합 검사. Editor 모의 터치·안전 영역을 실기기 검증으로 주장하지 않는다.

최종 정리 확인: 임시 runner 제거 후 Editor가 2026-10-01 00:29:46 Editor 어셈블리를 재컴파일했다. error CS 없음. 씬 외부 변경 알림에서 검증한 파일을 다시 불러왔다. git diff --check 통과, 문서 151개/로컬 링크 925개 검사 실패 0, 기존 문서 134개 보존. 최종 검사 요약은 Logs/PuzzleUIVerification/final-results-summary.json에 저장했다.

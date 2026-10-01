# 6단계 스와이프·교환 연출 — 진행 기록

상태: 구현·Editor 검증 완료 (2026-10-01). 플레이어·Addressables 콘텐츠 빌드 미실행.

계획: Docs/Planning/MoonRabbitJunkyard/WorldGameScreen/stage-06-swipe-swap-plan.md

## 실행 결정

- Ruling: 현재 work 브랜치와 기존 미커밋 작업을 그대로 사용한다 — 승인된 계획과 앞 단계 자산이 이 작업 폴더에 있으며 별도 기본 브랜치 checkout에는 없다 — 기존 변경을 기준 해시와 비교하고 자동 커밋하지 않는다.
- 실행 시점 CIM 조회에서 Unity.exe 없음. 설치된 6000.3.10f1 Editor로 빌드 없는 검증을 실행한다. 실행 중 PID를 확인하고 중복 Editor는 실행하지 않는다.
- 기준: Logs/PuzzleSwipeSwapVerification/baseline-hashes.json. 소스·레벨·팩·Addressables·ProjectSettings 해시를 보존했다.

## 작업 기록

작업 1 시작: 기존 스와이프는 Ended에만 반응하고 TrySwap은 매칭 후 Draw를 즉시 호출한다. 계산 전 그림 유지와 중간 이동을 검사하는 RED부터 확보한다.

작업 1·2: red-results.txt에서 계산 전 스프라이트 보존 실패를 재현했다. 점유자 조회와 별도 재생 객체를 추가하고 즉시 Draw 대신 재생 완료 시 Draw/다음 연쇄를 연결했다. green-editor.log의 동일 검사 통과. 이동 가능한 고철 본체는 정의상 시작 좌표가 아니라 현재 점유 칸에 표시하도록 수정했다.

작업 3: 화면→로컬 좌표 왕복에서 정확히 0.25칸이 0.2499998로 변환됨을 threshold-diagnostic-results.txt로 확인했다. Ruling: 임계값 비교에 0.00001칸 오차 허용 — 좌표 변환 반올림에 한정하며 0.24칸은 여전히 미확정 — 경계 검사로 확인한다. contents-retry-editor.log의 확대 검사와 기존 입력 검사가 통과했다.

작업 4 진행: 실제 게임 화면의 가로·세로 캡처, HUD 지연 반영, 회귀 검증과 독립 최종 리뷰가 남아 있다. 테스트 전용 코드에서 internal 복사 생성자 호출은 Editor 어셈블리 접근 제한으로 컴파일 실패하여 공개 실행기의 사본을 사용하는 fixture로 수정했다. 런타임 공개 계약은 늘리지 않았다.

## 실행 환경·검증 경로

Unity 6000.3.10f1 Editor, Windows, 기존 프로젝트/기존 아틀라스와 팩을 사용했다. 새 플레이어 또는 Addressables 콘텐츠 빌드는 실행하지 않았다. 각 실행은 앞 Unity 프로세스의 종료를 확인한 뒤 순차 실행했다. Editor의 C# 스크립트 컴파일만 수행했다.

실행 형식: 설치된 Unity.exe에 `-batchmode -projectPath C:/Projects/Git/ServeredMeridian -executeMethod <검사> -logFile <로그>`를 전달한다. 비동기 Play Mode 검사 완료 시 검사기가 batch 전용 Exit를 실행하므로 `-quit`을 추가하지 않는다. 열린 사용자 Editor에서는 두 번째 Unity를 실행하지 않는다.

| 검사 | 진입점 | 증거 |
| --- | --- | --- |
| 스와이프·교환·콘텐츠·수명 | GameScreen.Editor.PuzzleSwapAnimationVerification.Run | results.txt, final-swap-editor.log |
| 기존 입력 회귀 | 위 검사에 포함된 PuzzleBoardInputVerification.VerifyAsync | input-results.txt |
| 기존 게임플레이 | GameScreen.Editor.PuzzleGameplayVerification.Run | gameplay-results.txt, gameplay-regression.log |
| 에셋/MemoryPack UI 세션·취소·재시작 | GameScreen.Editor.PuzzleUIPlaybackVerification.Run | playback-results.txt, playback-regression.log |
| 실제 게임 씬·HUD·가로/세로·씬 재진입 | GameScreen.Editor.PuzzleSwapAnimationVerification.RunScene | scene-results.txt, final-ui-state-editor.log |

표의 파일은 모두 Logs/PuzzleSwipeSwapVerification 아래다. 최종 검사 건수와 시각은 final-summary.json으로 확인한다. PASS 수에는 같은 동작의 서로 다른 콘텐츠 조합이 포함되며 서로 다른 기능 개수라는 뜻은 아니다.

## 독립 리뷰와 보완

executing-plans 스킬의 최종 리뷰로 gpt-6-astra 에이전트가 현재 구현과 증거를 읽기 전용 검토했다(final-review.md). Critical/Important 런타임 결함은 발견하지 못했다. Important 검증 공백 1개(실제 게임 UI의 무효 교환 가로/세로 증거), Minor 1개(검사 시작 분기 중복)를 보고했다.

실제 게임 씬에 NoNewMatch 입력과 왕복 캡처·상태 불변 검사를 추가했다. 중복 분기는 제거했다. 재검사에서 실제 씬 중간 교환 종료·재진입도 확인했다. 리뷰를 반복 호출하지 않고 해당 증거와 회귀 결과로 보완했다.

## 완료 감사의 근거

| 요구 | 증거 |
| --- | --- |
| 4방향/0.24·0.25 경계/동률/손을 떼기 전/Ended-only | results.txt의 입력 검사, threshold-diagnostic-results.txt의 RED 및 수정 후 PASS |
| 기존 탭·파워·합성 마우스·UI·포커스 취소 | input-results.txt의 23개 회귀, results.txt의 실제 held/Moved 이벤트 |
| 원래 그림·중간 위치·레이어 고정·반환·거절 상태 불변 | results.txt 및 success-*/return-* 연속 캡처 |
| 파워/조합/고철/회수/자석 제한/벽/덮개/2×2 | results.txt의 종류별 이미지·중간 위치·최종 실행기 결과 비교 |
| 재생 중 입력/아이템/연쇄 잠금·HUD 반영 시점 | results.txt, scene-results.txt |
| pause/resume·회전·5회 다시하기·종료 | results.txt, playback-results.txt, 실제 씬 재진입은 scene-results.txt |
| 에셋/팩 기존 결과와 동일한 규칙·시드·이동 수 | gameplay-results.txt, playback-results.txt의 두 소스 검사 |
| 실제 가로/세로 유효/무효 이동 | scene-1280-* 및 scene-450-*의 start/preview/mid/end, invalid-start/far/return/end |
| 원본·팩·Addressables·ProjectSettings·기존 meta | baseline-hashes.json과 final-files-audit.json의 149개 기준 파일 비교 |
| 빌드 금지·새 패키지/이미지/데이터 포맷 없음 | 실행 진입점과 로그, 변경 파일 목록. 빌드나 콘텐츠 생성 진입점은 실행하지 않음 |

실제 화면을 열어 교환 중 위치와 왕복 후 원래 위치, 이동 수 유지/감소, 가로·세로 UI 잘림 여부를 확인했다. 시간은 검사에서 제어해 중간 프레임을 고정 캡처했다. Editor의 모의 입력·화면 검증이며 Android/iOS 실기기 터치나 성능 검증으로 주장하지 않는다.

## 남은 범위

7단계 제거·낙하·채움, 8단계 파워 상세 효과, 9단계 소리·진동은 이번 구현에서 제외했다. 교환 완료 후 기존 즉시 결과 표시·연쇄가 이어지는 것은 계획된 단계 경계다. 실제 모바일 기기는 미검증이다. 자동 커밋·푸시·업로드·다른 프로젝트 조작은 하지 않았다.

## 최종 결과

- 교환/콘텐츠/수명 190, 기존 입력 23, 기존 게임플레이 29, 두 소스 UI 세션 48, 실제 게임 화면/HUD/씬 재진입 22: **총 312 PASS, FAIL 0**. 마지막 화면 검사는 2026-10-01 02:08:41(KST)에 종료했다. final-summary.json에 항목별 시각을 기록했다.
- 실제 가로/세로에서 유효 이동과 무효 왕복, 교환 도중 정지, HUD 지연 반영, 아이템 버튼 잠금/복원을 확인했다. 최신 PNG를 열어 위치 변화와 복귀, 화면 가독성을 확인했다.
- 독립 리뷰의 Important 증거 공백과 Minor 중복 분기를 보완했다. 미해결 리뷰 사항 없음. 임시 시험 객체·모의 입력 장치·아트 소유권은 각 검사에서 정리하고 Unity 검사 프로세스도 종료했다.
- 기준 149개 파일 비교에서 원본 레벨·팩·Addressables·ProjectSettings·기존 meta 변경 없음. 새 스크립트에는 Unity가 생성한 meta가 모두 있다. runtime 변경은 입력·점유자 표시·재생 장벽·HUD에 한정했다. 프리팹과 데이터 포맷을 변경하지 않았다.
- 완료 조건은 위 감사 표의 증거로 충족했다. [사용 안내](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-06-swipe-swap-usage.md). 기존 work 브랜치와 미커밋 작업을 유지한다.

# 네이티브 레벨툴 UX 2단계 보고서

상태: 2026-10-22 구현 및 Editor 범위 검증 완료. 제품 빌드·실기기 검증을 의미하지 않는다.

## 변경 내용

- 상단은 현재 레벨·저장 상태·저장/시험·Undo/Redo 중심으로 정리했다. 기존 메뉴·작업 탭·시험 설정은 ‘메뉴’에 유지한다. 현재 레벨 제목은 클릭 또는 Enter/Space로 목록을 연다.
- Windows 좌우 패널 표시/숨김, 보드 집중/복원, 휴대폰의 목록·소재·속성·더보기 패널을 연결했다. 작은 가로 화면은 여백을 줄이되 48 UI 단위 터치 영역을 유지한다.
- 소재 분류·검색·최근 사용 20개, 실제 Sprite·이름·현재 층을 표시한다. 소재 선택 시 해당 층과 같은 층 교체를 활성화하고 다른 층은 보존한다. 휴대폰은 선택 후 소재 패널을 접는다. 소재 최근 사용은 실행 세션 범위이며 프로젝트별 최근 편집 레벨 목록의 기존 영속화와 구분한다.
- 현재 층 지우개와 확인을 거치는 모든 층 비우기를 구분했다. 우클릭/롱터치 공통 메뉴에서 층별 이름·수량·삭제, 복사/붙여넣기·선택 속성을 제공한다.
- 보드 전용 Ctrl+휠·핀치와 배율 표시·화면 맞춤을 연결했다. Windows 이동 도구의 마우스 드래그를 보완했다. 메뉴/단축키 도움말과 기존 창 정책은 유지한다.
- 패널을 숨길 때 미확정 입력을 보류하고 저장/Undo 시 다시 표시·검증한다. 집중 모드에서 직접 패널을 열 수 있게 했으며 공유 튜토리얼 초안의 지우개가 부모 레벨을 수정하지 못하도록 막았다.
- 저장 형식·ZIP·메타데이터 목록·가상화·dirty 정책은 바꾸지 않았다. 반복 편집 자동 저장이나 전체 레벨 JSON 읽기를 추가하지 않았다. 씬/프리팹을 편집하는 생성 스크립트도 추가하지 않았다.

## 검증 근거

아래 로그는 `Logs/TestHarness/20261022-<실행 번호>-Run.log`이며 각각 별도 Unity Editor 검사다. 전체 프로젝트 검사 통과를 뜻하지 않는다.

| 범위 | 실행 번호 / 결과 |
|---|---|
| 최종 보드 UI, 전체 소재 Sprite, 검색·최근·자동 층, 층별 삭제·전체 삭제 취소·Undo, 숨긴 입력 저장, 공유 초안 보호, 제목/패널 열기, Windows 이동, 5개 화면, Ctrl+휠/보드 밖 휠 | `193529-217`, exit 0 |
| 모바일 7개 크기, Safe Area, 패널·회전·키보드·Undo/Redo·저장 재열기 | `190124-623`, exit 0. 이 실행의 depth 0 캡처는 시각 증거에서 제외 |
| 숨긴 입력 Save/Undo, 잘못된 숫자 거절, 이후 정상 입력 | `191035-738`, exit 0 |
| 흐름·튜토리얼·시험·기록 진입, 속성 스크롤, 회전·패널 닫기·81칸 복귀 | `192058-474`, exit 0 |
| 실제 Panel scale 1.5/2, 논리 영역, 48단위 버튼, 키보드 팝업·확인/취소·입력 보존 | `192305-069`, exit 0 |
| 1천 목록, 선택 전 본문 미생성, 편집/목록 갱신의 초안 보존 | `190651-266`, exit 0 |
| 1만 가상 목록 데이터 소스/스크롤 유지, 행 드래그와 편집 버튼 구분 | `184717-527`, exit 0 |
| 본문 없는 1만 목록 표시와 선택 레벨만 읽기 | `183944-483`, exit 0 |
| 미저장 레벨 전환·저장/버리기/계속 편집 | `190858-363`, exit 0 |
| 공통 메뉴·단축키·모달·창 명령 경계 | `190932-669`, exit 0 |
| UI 재사용·문서 버전 캐시·추가 읽기·1만 메타데이터·목록 갱신 초안 보존 | `192420-439`, exit 0 |
| 2×2를 1×1로 실제 메뉴 교체, 원래 본체 ID와 단일 Undo 복원 | `192907-452`, exit 0 |
| 층이 다른 붙여넣기 거절, 같은 층 메뉴 붙여넣기·Undo | `192948-890`, exit 0 |
| 탭·취소·롱터치·핀치 후 메뉴 크기 유지·이동·영역·회전·마우스·교체·메뉴·저장 재열기 | `193637-361`, exit 0 |

[최종 터치 결과](NativeLevelToolStage02/touch-verification.txt) · [2×2 교체 결과](NativeLevelToolStage02/touch-body-replace-verification.txt) · [층별 클립보드 결과](NativeLevelToolStage02/touch-layer-clipboard-verification.txt)

## 화면 증거

실제 UI Toolkit 씬을 스텐실이 있는 RenderTexture, 논리 배율 1로 캡처했다. 모바일은 Editor에서 플랫폼 레이아웃을 호출한 시뮬레이션이다.

- [변경 전 Windows](NativeLevelToolStage02/baseline-1280x800.png), [변경 후 Windows](NativeLevelToolStage02/final-1280x800.png), [소재 이미지·검색](NativeLevelToolStage02/final-materials-1280x800.png)
- [1440×900](NativeLevelToolStage02/final-1440x900.png), [390×844](NativeLevelToolStage02/final-390x844.png), [844×390](NativeLevelToolStage02/final-844x390.png), [820×1180](NativeLevelToolStage02/final-820x1180.png)
- [가로 고급 속성 스크롤](NativeLevelToolStage02/advanced-landscape.png), [2배 배율·키보드 가용 영역에서 취소 접근](NativeLevelToolStage02/scaled-keyboard.png)

기본 보드 가용 높이는 각각 1440×900에서 712, 휴대폰 세로 547, 가로 185, 태블릿 883 UI 단위다. 작은 가로 780×360/Safe Area 744×318에서도 viewport 113을 확보했다. 가로에서는 보드를 스크롤해 편집하며 전체 9×9를 억지로 축소하지 않는다. 캡처의 ‘숨긴 입력 저장 검사’ 이름과 빈 칸은 격리 fixture다.

## 성능 비교와 한계

동일 fixture·배율·반복 수로 1단계 HEAD와 수정 소스를 별도 Editor 실행에서 비교했다. 전 측정은 `191505-631`, 후 측정은 `191945-260` 실행을 대표값으로 보존했다. 현재 수정 파일 13개를 먼저 백업하고 baseline 실행 후 원래 바이트 해시로 복원했다. 통계적 벤치마크나 제품 성능이 아니다.

| 100회 호출 합계 | 변경 전 | 변경 후 |
|---|---:|---:|
| 변경 없는 Refresh | 725.926ms | 867.016ms |
| 셀 선택 + Refresh | 7,864.049ms | 8,495.436ms |
| 목록/소재 패널 전환 | 61.264ms | 71.798ms |
| 추가 표시용 문서 읽기 | 0 | 0 |

[전 Refresh](NativeLevelToolStage02/baseline-refresh.txt) · [후 Refresh](NativeLevelToolStage02/final-refresh.txt) · [전 선택/패널](NativeLevelToolStage02/baseline-interaction.txt) · [후 선택/패널](NativeLevelToolStage02/final-interaction.txt)

선택 평균은 약 78.6→85.0ms이며 비용이 증가했다. 속도 개선으로 주장하지 않는다. 새 소재/상태 UI가 포함된 결과이며 필요하면 별도 프로파일링으로 후속 개선할 수 있다. 문서 읽기와 전체 파일 로드의 증가 여부는 별도 검사로 확인했다. `GetAllocatedBytesForCurrentThread`의 0은 지원이 검증되지 않아 실제 무할당 근거로 사용하지 않는다.

## 검증 과정의 수정과 제한

- 한 차례 독립 리뷰의 숨긴 입력 저장·공유 초안 지우기·패널 접근 지적을 재현/회귀 검사로 보완했다. 두 번째 리뷰는 수행하지 않았다.
- 검사 코드의 잘못된 namespace, RenderTexture 스텐실 누락, 변경 전 fixture가 실제 레벨 메타데이터를 덮어쓴 문제를 수정했다. 휴대폰 크기 변경은 고정 시간 대신 실제 root 크기 반영 후 적용하도록 바꿨다. 2×2 조작 전에 대상 칸을 표시 영역으로 스크롤한다.
- 고급 메뉴 검사 한 번은 검사 도중 소스 변경에 따른 강제 리로드로 콜백이 끊겼다. 정확히 해당 배치 프로세스만 종료하고 소스를 고정해 재검사했다. 사용자 Editor나 다른 프로젝트 프로세스는 종료하지 않았다.
- Android 실기기/OS 키보드·IME·물리 dp·실제 터치/뒤로가기, Windows 제품 창 동작은 미검증이다. 제품 빌드·설치가 금지된 범위와 구분한다.
- 실제 제작 원본 전체의 전후 해시를 새로 인증한 것은 아니다. 격리 프로젝트·recovery 경로를 사용하며 검사 증거와 실제 작업 데이터는 보존한다.
- 마지막 검사 후 테스트 연결 해제를 확인했다. 남은 소유 GUID 검사 사본 16곳의 Project/Recovery 파일 87개를 실제 제작 원본·링크·파일 종류·잠금 검사 후 제거했다. [정리 기록](NativeLevelToolStage02/cleanup.json). 성능 비교용 임시 소스 백업과 실행 스크립트도 정리했다. 검증 결과/이미지와 기존 사용자 데이터는 보존했다.
- 최종 변경 범위 `git diff --check` 통과, 보고서/다음 단계 문서의 로컬 링크 33개 누락 없음. 자동 줄바꿈 안내는 오류와 구분한다.

## 다음 단계

3단계는 제작 체크리스트, 쉬운 미션 요약, 목적→보드 선택→필수 설정→단계·공급→시험→완료의 조립 UX다. 신규/기존 튜토리얼 작업 전체 취소와 이전 dirty·다른 레벨·공유 데이터 보존을 핵심 회귀로 둔다. 완료와 파일 저장은 구분한다. 3단계 구현은 시작하지 않았다.

[2단계 목표](../../Goals/MoonRabbitJunkyard/LevelToolNativeUx/2026-10-22-stage02-goal.md) · [3단계 계획](../../Planning/MoonRabbitJunkyard/LevelToolNativeUx/2026-10-22-stage03-plan.md) · [3단계 목표](../../Goals/MoonRabbitJunkyard/LevelToolNativeUx/2026-10-22-stage03-goal.md) · [복사용 실행문](../../Commands/MoonRabbitJunkyard/LevelToolNativeUx/2026-10-22-stage03-command.md)

제품 빌드·설치, 커밋·푸시, HTML 매뉴얼 갱신은 하지 않았다.

## 복사용 커밋 메시지

```text
feat(level-tool): 보드 중심 반응형 UX와 소재·층별 편집 개선

- 패널·입력 보존, 보드 전용 줌과 공통 작업 메뉴 개선
- 모바일·대량 목록 회귀 검증 및 3단계 계획 정리
```

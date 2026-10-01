# 9×9 보드 전환 검증

기준일: 2026-10-01. [설계 결정](../../../Decisions/MoonRabbitJunkyard/2026-10-01-nine-by-nine-board.md).

## 적용 범위

- 신규 보드·편집 보드·플레이 테스트·월드 게임·등록 모양을 9×9, 81칸으로 통일했다.
- 편집 셀 44px, 플레이 테스트 셀 48px. 월드 보드는 화면 영역을 유지하고 카메라 크기 4.8로 조정했다.
- 기존 Level_01, 50레벨 구간 MemoryPack 파일, 등록 모양 2개를 변환했다. 왼쪽 위 9×9의 좌표와 기존 GUID를 보존했다.
- 셀 입력 0~8, 전선 꼭짓점 0~9, 흐름·통로·월드 위치 변환을 함께 조정했다.
- 기획·매뉴얼·목업을 갱신했다. 과거 검증 수치는 당시 10×10 조건으로 보존하고 새 기준을 연결했다.

## 실행한 검사

| 검사 | 결과 | 증거 |
| --- | --- | --- |
| 구형 데이터 변환·MemoryPack 왕복·편집 경계·등록 모양·실제 레벨 | 23 PASS, 실패 0 | `Logs/BoardNineVerification/data-results.txt` |
| 퍼즐 규칙·파워·낙하·장애물·회수·판 종료 | 15개 묶음, 2,655 PASS | `Logs/BoardNineVerification/core-results.txt` |
| 교환 애니메이션·취소·재시작·객체 해제 | 190 PASS | `Logs/BoardNineVerification/swipe-results.txt` |
| 입력·회전·화면 비율·오른쪽/아래 경계 | 26 PASS | `Logs/BoardNineVerification/input-results.txt` |
| 실제 게임 씬 가로·세로 화면, 정상/무효 교환, HUD, 씬 재진입 | 22 PASS | `Logs/BoardNineVerification/scene-results.txt` |
| 편집기 아틀라스·블록·장애물·레이어·로켓 생성 표시 | 474 PASS | `Logs/BoardNineVerification/artwork-results.txt` |
| 일반 Editor 재시작 후 실제 9×9 이미지 보드 | 이미지 표시 및 캡처 확인 | `Logs/BoardNineVerification/editor.png` |

검사는 Unity 6000.3.10f1 Editor에서 실행했다. 최초 실패한 과거 10열 인덱스·배치 fixture는 9열 기준으로 수정하고 다시 통과했다. 기존 게임 규칙의 판정 조건을 완화하지 않았다.

## 편집 이미지 로딩 보완

일반 Editor에서 창을 복원할 때 아틀라스 요청이 미완료 상태로 남아 색 블록만 표시되는 현상도 재현했다. 독립 아틀라스의 스프라이트 5개는 정상 로드되는 것을 확인했으며, 단순 재그리기·갱신 요청만으로 해결되지 않았다.

편집 모드의 로컬 아틀라스 미리보기는 다음 Editor 갱신에서 로드를 시작하고 최초 요청의 완료를 확인한 뒤 표시한다. 이때 한 번의 로컬 로딩 대기가 생길 수 있다. Play Mode·플레이어는 기존 비동기 경로를 유지한다. 개별 PNG를 다시 로드하는 방식이나 콘텐츠 빌드로 우회하지 않았다. 수정 뒤 사전 로드 없는 편집 보드와 이미지·레이어 회귀 검사 474개가 통과했다.

## 검증 한계

플레이어·Addressables 콘텐츠 빌드는 하지 않았다. MemoryPack 파일 직렬화와 Editor 스크립트 컴파일만 수행했다. 실제 모바일 기기 확인과 축소된 판의 난이도 재조정은 수행하지 않았다. 미션·이동 수는 기존 값을 유지한다.

`Docs/Contents/`는 기존 `.gitignore` 규칙의 제외 대상이다. 해당 기획 파일도 로컬에서 갱신했으며, Git 추적 정책은 변경하지 않았다.

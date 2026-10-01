# 신규 공급 대각선 채움 검증

검증일: 2026-10-01. [결정 문서](../../Decisions/MoonRabbitJunkyard/2026-10-01-fresh-supply-diagonal.md).

## 결과

ServeredMeridian 사용자 Editor 종료 후 Unity 6000.3.10f1의 별도 검사 Editor에서 실행했다. **772 PASS, 0 FAIL**. 플레이어·Addressables 콘텐츠 빌드, 사용자 씬·레벨 저장, 커밋은 하지 않았다.

| 진입점 | PASS | 결과 파일 (`Logs/` 기준) |
| --- | ---: | --- |
| `Levels.Editor.SettlementVerification.FreshSupply` | 170 | `FreshDiagonalVerification/manual-results.txt` |
| `Levels.Editor.SettlementVerification.Supplemental` | 13 | `SettlementVerification/supplemental-results.txt` |
| `Levels.Editor.ScrapVerification.Supplemental` | 102 | `ScrapVerification/supplemental-results.txt` |
| `Levels.Editor.RecoveryVerification.Edges` | 53 | `RecoveryVerification/edge-results.txt` |
| `Levels.Editor.CascadeVerification.Data` | 65 | `CascadeVerification/data-results.txt` |
| `Levels.Editor.RoundEndVerification.Data` | 22 | `RoundEndVerification/data-results.txt` |
| `GameScreen.Editor.PuzzleSettlementAnimationVerification.Run` | 347 | `PuzzleSettlementAnimationVerification/results.txt` |

새 회귀 검사를 이전 정착 규칙에 실행했을 때 `기존 블록은 대각선 빈칸으로 이동하지 않음`에서 실패했고, 변경 후 통과했다. 실패 증거는 `Logs/FreshDiagonalVerification/red-results.txt`다. 기존 대각선 경쟁·벽·순환·고철·회수·애니메이션 검사는 새 공급을 사용하는 조건으로 갱신하여 해당 제약을 계속 확인한다.

## 확인 범위

- 기존 블록 대각선 이동 금지와 원본·난수 보존.
- 신규 블록의 여러 이동 묶음에 걸친 대각선 이동, 다음 정착에서 신규 자격 초기화.
- 멀리 있는 상단 공급 및 뒤이어 생성될 다음 블록의 우선권, 상단 공급 소진 시 대각선 대체.
- 공급·벽·합류·경로·반복 감지·최장 9×9 경로와 같은 시드 재현.
- 신규 고철·회수 부품의 대각선 경쟁과 회수 중복 방지, 연쇄·라운드 종료 회귀.
- Play Mode에서 기존 블록의 대각선 정착 기록 없음, 신규 공급 후 대각선 채움, 입력 잠금·일시정지·표시 정리 및 직접 규칙 실행과 최종 상태 일치.

자동 검사 결과이며 실제 모바일 기기에서의 체감·성능 검수는 포함하지 않는다. 콘텐츠 문서와 사용 안내는 최신 규칙으로 갱신했다. 과거 단계 검증 수치는 당시 규칙의 기록으로 보존한다.

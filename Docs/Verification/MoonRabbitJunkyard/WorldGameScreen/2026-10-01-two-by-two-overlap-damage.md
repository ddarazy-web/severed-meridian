# 2×2 장애물의 공격 범위 겹침 피해

2026-10-01. 내구도를 공유하는 금속기둥 상자(Runtime Appliance)의 기존 칸별 피해 규칙을 요청 사례로 재검증했다. 발전기는 내구도가 없는 충전 장치다.

| 공격 | 겹친 칸 | 내구도 9 기준 결과 |
| --- | ---: | ---: |
| 가로/세로 로켓 | 2 | 7 |
| 단독 폭탄 | 0 / 1 / 2 | 9 / 8 / 7 |
| 로켓+로켓 | 2 | 7 |
| 로켓+폭탄 확대 범위 | 2 / 4 | 7 / 5 |
| 폭탄+폭탄 확대 범위 | 1 / 2 / 4 | 8 / 7 / 5 |
| 자석+자석 전체 범위 | 4 | 5 |

한 타격의 동일 점유 칸은 한 번만 세며 별도 후속 공격은 다시 피해를 준다. 남은 내구도가 3이면 자석+자석으로 0이 되고 네 칸이 함께 제거된다. 본체 미션은 한 개로 집계한다.

- 기존 `ObstacleDamageRules`가 이미 이 규칙을 계산하므로 런타임 규칙을 변경하지 않았다. 새 검사로 실제 실행 결과와 단계 기록·중복 방지·원본 보존·전체 제거를 확인했다.
- `Levels.Editor.FixedObstacleVerification.OverlapData`: 13개 사례, 78 PASS, FAIL 0. `Logs/FixedObstacleVerification/overlap-results.txt`, `overlap.log`, Editor 종료 코드 0.
- `PuzzlePowerAnimationVerification.Run`에 단독 폭탄 2피해, 폭탄+폭탄 4피해, 자석+자석 4피해의 실제 표시 검사를 추가했다. 각 유효 타격 시 표시용 상태의 내구도를 실제 기록과 대조했다. 기존 본체 이미지 단계 검사도 함께 실행했다. 최종 216 PASS, FAIL 0, Editor 종료 코드 0. 결과는 `Logs/Stage08/render-results.txt`, 실행 로그는 `Logs/FixedObstacleVerification/overlap-play.log`다.
- 기획 문서에 확대 범위의 동일 칸 중복 제외와 내구도 하한을 명시했다. 플레이어·Addressables 콘텐츠 빌드는 실행하지 않았다.

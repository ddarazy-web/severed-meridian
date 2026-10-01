# 드론 임의 공중 이동·다양한 선회·표적 예고 제거

2026-10-01. 출발점 근처에서 같은 작은 원만 도는 연출을 변경했다. 이전 선회 구현에 대한 후속 변경이다.

- 출발점에서 약 1.4~2.6칸 떨어진 임의 위치로 휘어 이동한다. 가장자리는 보드 안쪽으로 보정하고 최소 1.2칸 이동을 확보한다. 여러 드론으로 혼잡하면 배치 범위를 넓혀 빈 궤도를 탐색한다.
- 공중 이동은 0.32초, 이동과 선회를 합친 최소 대기 시간은 0.72초다. 선행 공격이나 재탐색 때문에 필요한 추가 대기는 계속 선회한다.
- 개별 반경 0.18~0.45칸, 주기 0.65~1.05초, 시작 각도, 시계/반시계 방향을 표시 전용 난수로 선택한다. 대기 위치와 선회 선택에는 표적 좌표를 사용하지 않는다. 게임 규칙 난수와 Unity 전역 난수도 소비하지 않는다.
- 개별 반경이 달라도 선회 중 본체가 겹치지 않도록 궤도 중심 사이에 두 반경의 합과 0.96칸의 간격을 확보한다. 같은 생성점에서 펼쳐지는 이동 구간은 대기 선회와 구분한다.
- 마지막 선회 위치에서 빠르게 가속하는 곡선 돌진을 이어간다. 표적 예고 이미지는 돌진 전·중 모두 재생하지 않는다. 착탄 효과는 유지한다.

## 검증 결과

- 수정 전 출발점과 대기 위치 사이 최소 거리 검사가 실패했다(`drone-wide-red.log`).
- `PuzzlePowerAnimationVerification.Run`: 320 PASS, FAIL 0. 임의 대기 위치의 최소 거리, 공중 이동 후 선회 시간, 표적 예고 이미지 부재, 다중 드론 선회 분리와 기존 표시 검사를 통과했다. `drone-wide-green.log`, `render-results.txt`.
- `PuzzlePowerAnimationVerification.Data`: 102 PASS, FAIL 0. 실제 표적 소실과 재탐색의 추가 대기 시간, 기존 공격 기록과 피해 시간을 확인했다. `drone-wide-data.log`, `data-results.txt`.
- `PuzzlePowerAnimationVerification.RunScene`: 893 PASS, FAIL 0. 실제 게임 씬의 4종 파워·10개 조합, 기존 실행기와 최종 상태·난수 동등성, 일시정지·취소·재시작을 확인했다. `drone-wide-scene.log`, `scene-results.txt`.
- 세 실행 모두 Editor 종료 코드 0. `drone-frame-0.png`에서 출발점과 떨어진 공중 위치, 표적 표시 부재를 확인했다. `git diff --check` 통과.

플레이어·Addressables 콘텐츠 빌드와 실기기 검증은 실행하지 않았다. 결과와 로그는 `Logs/Stage08`에 남겼다. 현재 값과 동작은 [8단계 사용 안내](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-08-power-effects-usage.md)를 따른다.

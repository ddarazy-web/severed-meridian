# 선택·스와이프 블록 표시 순서

2026-10-01 구현·Editor Play Mode 검증 완료. 빌드 미실행.

- 이동 가능한 블록을 누르면 즉시 sortingOrder 50으로 표시한다. 주변 보드 블록·장식보다 앞이며, 교환 상대는 49로 표시한다.
- 손을 놓으면 교환 애니메이션이 진행 중이어도 저장한 원래 순서를 복원한다. 미확정 드래그 취소·포커스 상실·입력 컴포넌트 비활성화도 같은 복원 경계를 사용한다.
- 교환 스냅샷과 재생기에 높아진 선택 순서가 원래 순서로 저장되지 않도록 인계 순서를 수정했다. 교환 위치·규칙·프리팹은 변경하지 않았다.
- RED: `Logs/PuzzleSwipeSwapVerification/order-red.log`에서 ‘선택 즉시 주변 블록보다 앞에 표시’ 실패.
- GREEN: `GameScreen.Editor.PuzzleSwapAnimationVerification.Run`, `order-green.log` 종료 코드 0. `results.txt` 251 PASS, FAIL 0. 네 방향 선택·드래그·확정 후 손 놓기, 미확정 취소/손 놓기와 기존 마우스·터치·콘텐츠 교환·다시하기·종료 검사를 포함한다.
- diff 공백 검사 통과. 기존 사용자 변경은 유지하고 커밋하지 않았다.

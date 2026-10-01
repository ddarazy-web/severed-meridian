# 동시 낙하·공급 대기열 검증

검증일: 2026-10-01. Unity 6000.3.10f1, ServeredMeridian.

## 변경

정착 규칙의 Batch를 하나씩 기다리던 표시를 블록별 이동 시간표로 바꿨다. 앞 블록이 출발하면 뒤 블록도 출발하며, 같은 블록은 다음 기록 경로로 연속 이동한다. 신규 공급 블록은 처음부터 공급구 바깥 대기열에 준비하여 함께 내려오며 대상 칸 마스크로 이웃 칸 누출을 막는다. 통로 입구는 페이드아웃을 기다리고 회수 이미지는 자신의 도착 시간에 숨긴다.

규칙·공급 순서·난수·대각선 자격·최종 상태는 변경하지 않는다. 전체 이동이 끝난 뒤 착지하고 자동 매칭을 이어간다. 프리팹·씬·레벨·MemoryPack 형식은 변경하지 않았다.

## 실행 결과

**367 PASS, 0 FAIL**.

| 검사 | PASS | 결과 |
| --- | ---: | --- |
| `GameScreen.Editor.PuzzleSettlementAnimationVerification.Run` | 280 | `Logs/PuzzleSettlementAnimationVerification/results.txt` |
| `GameScreen.Editor.PuzzleSettlementAnimationVerification.RunScene` | 87 | `Logs/PuzzleSettlementAnimationVerification/scene-results.txt` |

- 기존 구현에서 새 동시 낙하 검사가 `위·중간·아래 기존 블록이 첫 프레임부터 함께 낙하`에 실패함을 먼저 확인했다. 증거: `Logs/simultaneous-fall-red-results.txt`.
- 수정 후 세 기존 블록의 첫 프레임 동시 이동·같은 속도, 두 신규 공급 블록의 시작 시 대기열 준비를 확인했다.
- 프레임 검사는 과거 Batch별 정지 시간을 가정하지 않고 기록된 실제 경로·포털 양끝·회수 숨김·착지 축척을 확인하도록 갱신했다.
- 공급 마스크는 대기열 그림의 현재 위치가 아닌 공급구 위치로 식별하고, 경계에 검사 그림을 놓아 내부 표시와 이웃 누출 방지를 확인했다.
- 실제 씬의 가로·세로 화면, 재생 중 회전·일시정지·다시하기·씬 재진입, 객체 누적 없음, 직접 규칙 실행과 전체 연쇄 최종 결과 일치를 확인했다.
- `scene-450-fall-mid.png` 등 실제 씬 검사 캡처를 확인했다. 캡처는 위 결과 폴더에 있다.
- `git diff --check` 통과. 플레이어·Addressables 콘텐츠 빌드, 사용자 씬 저장, 커밋은 하지 않았다. 모바일 기기 체감·성능 검수는 별도다.

사용 안내: [제거·낙하·채움](../../../Guides/MoonRabbitJunkyard/WorldGameScreen/stage-07-settlement-usage.md).

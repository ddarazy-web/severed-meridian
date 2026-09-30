# 청소 로켓 발사 이미지 v1

내장 image_gen으로 기존 가로·세로 로켓을 편집했다. 각 방향에 대기와 추진 단계 네 컷을 만들고 최종 크기로 보정했다.

- 기존 `cleaning-rocket-vertical-v1.png`, `cleaning-rocket-horizontal-v1.png`는 불꽃 없는 대기 이미지(256×256)로 교체했다.
- 시트: `Assets/Textures/PowerBlocks/cleaning-rocket-{vertical,horizontal}-launch-4frames-v1-512.png`, 512×512, 2×2 배치.
- 개별 컷: 같은 폴더의 `cleaning-rocket-{vertical,horizontal}-launch-frame-{1,2,3,4}-v1-256.png`, 각 256×256.
- 순서: 좌상 1 대기(불꽃 없음), 우상 2 점화, 좌하 3 짧은 추진, 우하 4 긴 추진.
- 사용 제안: 대기 중에는 1번 고정. 발사 시 2→3→4, 비행 중에는 3↔4. 실제 이동은 이미지가 아닌 게임 이동 연출에서 처리한다.
- PNG 크기 및 모서리 투명도 확인, 두 시트의 대기 불꽃 없음과 추진 불꽃 변화를 시각 검수했다.
- 생성 이미지이므로 몸체 위치와 윤곽은 컷 사이에 차이가 있다. Unity 적용 시 정렬 및 실제 재생 검수가 필요하다. AnimationClip 연결과 게임 발사 동작 구현은 이번 작업에 포함하지 않았다.

## 프롬프트

### vertical

```text
Use case: precise-object-edit. Create ONE 2x2 four-frame launch animation sprite sheet for the exact orange cleaning rocket in the reference. nose points UP, exhaust points DOWN. Preserve orange hull, ivory chevron pointing toward nose, ivory middle band, navy circular vent with two bars, navy fins and nozzle, thick navy cartoon outlines and simple shading. Genuine transparent PNG, square canvas. Four equal square cells, reading order top-left top-right bottom-left bottom-right, no drawn grid or labels.
All four cells show the exact same WHOLE rocket body at identical local pixel position, size, perspective, lighting and shape. Reserve space behind nozzle for the longest flame; body occupies about 65% of cell length, total rocket plus maximum flame fits in 88% of cell. Align BODY across frames, not bounding box of body plus flame. No translation or squash of body.
FRAME 1 upper-left: IDLE, engine off. Absolutely NO flame, NO glow, NO smoke, NO sparks. The navy nozzle ends cleanly with transparent empty space behind it.
FRAME 2 upper-right: ignition, small short orange pointed flame attached to nozzle, yellow inner core, approx 6% cell length.
FRAME 3 lower-left: launch thrust, medium broad orange outer flame and ivory-yellow inner core, 13% cell length, two asymmetric flame tips.
FRAME 4 lower-right: full thrust, longer narrower flame 21% cell length, orange outer edge bright yellow and ivory inner core, three flame tips. Distinct flame silhouette from frame3.
Flames emerge from nozzle only. No flame in first frame is essential. No detached particles, background, smoke clouds, external ground shadows, speed lines, faces or text. Exactly four rockets. Requested final sheet dimensions512x512, 256x256 per frame.
```

### horizontal

```text
Use case: precise-object-edit. Create ONE 2x2 four-frame launch animation sprite sheet for the exact orange cleaning rocket in the reference. nose points RIGHT, exhaust points LEFT. Preserve orange hull, ivory chevron pointing toward nose, ivory middle band, navy circular vent with two bars, navy fins and nozzle, thick navy cartoon outlines and simple shading. Genuine transparent PNG, square canvas. Four equal square cells, reading order top-left top-right bottom-left bottom-right, no drawn grid or labels.
All four cells show the exact same WHOLE rocket body at identical local pixel position, size, perspective, lighting and shape. Reserve space behind nozzle for the longest flame; body occupies about 65% of cell length, total rocket plus maximum flame fits in 88% of cell. Align BODY across frames, not bounding box of body plus flame. No translation or squash of body.
FRAME 1 upper-left: IDLE, engine off. Absolutely NO flame, NO glow, NO smoke, NO sparks. The navy nozzle ends cleanly with transparent empty space behind it.
FRAME 2 upper-right: ignition, small short orange pointed flame attached to nozzle, yellow inner core, approx 6% cell length.
FRAME 3 lower-left: launch thrust, medium broad orange outer flame and ivory-yellow inner core, 13% cell length, two asymmetric flame tips.
FRAME 4 lower-right: full thrust, longer narrower flame 21% cell length, orange outer edge bright yellow and ivory inner core, three flame tips. Distinct flame silhouette from frame3.
Flames emerge from nozzle only. No flame in first frame is essential. No detached particles, background, smoke clouds, external ground shadows, speed lines, faces or text. Exactly four rockets. Requested final sheet dimensions512x512, 256x256 per frame.
```


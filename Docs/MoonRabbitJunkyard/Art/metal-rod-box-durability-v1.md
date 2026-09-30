# 금속기둥 상자 색상·내구도별 이미지 v1

세탁기 외형을 대체하기 위한 아트. 네모 상자 안에 세워 넣은 금속기둥의 둥근 단면을 위에서 보는 디자인이다.

- 색상: pink, yellow, blue, green, purple.
- 내구도: 1~9. 금속기둥 수가 남은 내구도다.
- 내부 배치: 3×3. 우하단부터 역순으로 제거하며 남은 기둥은 원래 격자 위치를 유지한다.
- 상자 테두리는 중립적인 아이보리 금속, 바닥은 어두운 회색, 기둥 단면과 측면에 해당 색을 적용한다.
- 파일: `Assets/Textures/Obstacles/MetalRodBox/metal-rod-box-{color}-durability-{1..9}-v1-256.png`.
- 최종 45장 모두 256×256px 투명 RGBA PNG. 네 모서리 알파 0 및 크기 검사 통과.
- 내장 image_gen으로 파란색 내구도 시트를 생성하고 색상별 편집했다. 색상별 시트의 9~1 개수 및 배치를 시각 검수했다. 시트를 3×3 균등 분할하여 각 컷을 Bicubic으로 256×256px에 맞췄다.
- 생성 이미지 특성상 단계 간 윤곽과 위치는 픽셀 단위로 완전히 동일하지 않다.
- 기존 세탁기 파일은 유지했다. 게임 연결, 코드 및 피해 조건 변경은 하지 않았다. 색상별 이미지를 만든 것이며 색 매칭 피해 규칙의 추가를 뜻하지 않는다.
- 기존 대형 폐가전은 2×2 장애물이다. 이번 파일 해상도는 블록 이미지 256 기준이며, 보드 점유 크기는 변경하지 않았다.

## 생성 프롬프트

### 파란색 기준 시트

```text
Use case: stylized-concept. Create a coherent game obstacle durability SPRITE SHEET. Reference is STYLE ONLY: thick smooth dark navy outlines, friendly simple cartoon shapes, broad flat colors, very minimal upper-left highlight.
Square canvas, exactly 3 by 3 equally sized cells with NO grid lines, labels or numbers. Exactly nine separate square boxes. Background outside boxes is genuinely transparent. Each sprite occupies 82% of its own cell and is centered identically.
Subject: an OPEN SQUARE INDUSTRIAL BOX viewed STRICTLY DIRECTLY OVERHEAD, orthographic TOP DOWN. Four low ivory/silver rim walls outline a square with slightly rounded corners. Plain dark slate interior floor. Inside, cylindrical upright metal bars are seen as large ROUND BLUE CIRCULAR END FACES with dark navy outline, a darker blue rim and ONE small crescent highlight (not holes, not gems, not balls, no spirals). Each circle occupies one fixed position in a 3x3 internal array. Box is a storage crate NOT a device, no buttons or feet or writing. Minimal styling, color dominates rod end faces.
Crucial durability sequence: reading order of the NINE sheet cells, top-left to bottom-right, shows EXACTLY 9,8,7,6,5,4,3,2,1 metal bars. There are NEVER sockets or ghost circles in vacant positions, only plain flat dark floor.
Internal positions within EACH box numbered mentally row-major:
A B C
D E F
G H I
Cell1 (9 bars): A B C D E F G H I.
Cell2 (8 bars): A B C D E F G H.
Cell3 (7 bars): A B C D E F G.
Cell4 (6 bars): A B C D E F.
Cell5 (5 bars): A B C D E.
Cell6 (4 bars): A B C D.
Cell7 (3 bars): A B C.
Cell8 (2 bars): A B.
Cell9 (1 bar): A.
Rods NEVER recenter or grow when others disappear. All boxes identical registration and scale. Each bar remains same size and color, each fixed gap identical. All metal bar ends saturated sky blue #409FE8. Rim ivory silver, floor dark gray navy. Clean clear shapes readable as small puzzle sprites. No loose scraps, no perspective tilted sides, no characters, no shadow outside box. Nine correct counts are essential. Transparent PNG.
```

### pink

```text
Use case: precise-object-edit. Recolor ONLY the blue metal rod ends and blue cylinder sidewalls to pink #EF78AF, darker rose pink cylinder sides. Preserve their small pale highlight. This is a game durability SPRITE SHEET, EXACT SAME 3x3 sheet layout and nine separate boxes with counts 9,8,7,6,5,4,3,2,1 reading order. Preserve EVERY rod count, positions, spacing, size, outlines, each box's registration, overhead view, ivory frame and dark gray interior, and transparent exterior background exactly. No recoloring of frame or floor, no new objects, no numbers or labels. Do not add any rods in empty spaces. The bottom-right sprite has ONE rod in upper-left interior position, never recenter it. Output the same square transparent PNG sprite sheet. Single global color substitution only, no redesign.
```

### yellow

```text
Use case: precise-object-edit. Recolor ONLY the blue metal rod ends and blue cylinder sidewalls to golden yellow #F6CC45, darker ochre gold cylinder sides. Preserve their small pale highlight. This is a game durability SPRITE SHEET, EXACT SAME 3x3 sheet layout and nine separate boxes with counts 9,8,7,6,5,4,3,2,1 reading order. Preserve EVERY rod count, positions, spacing, size, outlines, each box's registration, overhead view, ivory frame and dark gray interior, and transparent exterior background exactly. No recoloring of frame or floor, no new objects, no numbers or labels. Do not add any rods in empty spaces. The bottom-right sprite has ONE rod in upper-left interior position, never recenter it. Output the same square transparent PNG sprite sheet. Single global color substitution only, no redesign.
```

### green

```text
Use case: precise-object-edit. Recolor ONLY the blue metal rod ends and blue cylinder sidewalls to green #6CCB72, darker emerald green cylinder sides. Preserve their small pale highlight. This is a game durability SPRITE SHEET, EXACT SAME 3x3 sheet layout and nine separate boxes with counts 9,8,7,6,5,4,3,2,1 reading order. Preserve EVERY rod count, positions, spacing, size, outlines, each box's registration, overhead view, ivory frame and dark gray interior, and transparent exterior background exactly. No recoloring of frame or floor, no new objects, no numbers or labels. Do not add any rods in empty spaces. The bottom-right sprite has ONE rod in upper-left interior position, never recenter it. Output the same square transparent PNG sprite sheet. Single global color substitution only, no redesign.
```

### purple

```text
Use case: precise-object-edit. Recolor ONLY the blue metal rod ends and blue cylinder sidewalls to purple #AA79D9, darker violet cylinder sides. Preserve their small pale highlight. This is a game durability SPRITE SHEET, EXACT SAME 3x3 sheet layout and nine separate boxes with counts 9,8,7,6,5,4,3,2,1 reading order. Preserve EVERY rod count, positions, spacing, size, outlines, each box's registration, overhead view, ivory frame and dark gray interior, and transparent exterior background exactly. No recoloring of frame or floor, no new objects, no numbers or labels. Do not add any rods in empty spaces. The bottom-right sprite has ONE rod in upper-left interior position, never recenter it. Output the same square transparent PNG sprite sheet. Single global color substitution only, no redesign.
```


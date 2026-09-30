# 고물 회수 캡슐 내구도별 이미지 v1

기존 금고를 대체할 아트 후보. 낮고 넓은 원통형이며 내구도 1은 열린 상태다.
내장 image_gen으로 생성·편집하고, 최종 파일은 고품질 Bicubic으로 256×256px에 맞췄다.

| 내구도 | 구분 |
|---|---|
| 5 | 파란 전체 외피 + 주황 잠금띠와 걸쇠 |
| 4 | 잠금띠 제거, 파란 외피 중앙 틈으로 아이보리 용기 노출 |
| 3 | 상부 외피 제거, 하부 파란 외피만 남음 |
| 2 | 외피 제거, 닫힌 아이보리 용기 |
| 1 | 뚜껑 열린 용기, 내부 고물 노출 |

파일: `Assets/Textures/Obstacles/RecoveryCapsule/recovery-capsule-durability-{1,2,3,4,5}-v1-256.png`

5장 모두 실제 256×256px, RGBA, 네 모서리 알파 0을 확인하고 최종 크기로 시각 검수했다.
독립 생성된 전체 이미지이므로 단계 간 받침대 위치와 윤곽은 픽셀 단위로 일치하지 않는다.

이미지 제작만 완료했다. 기존 금고 에셋은 보존했으며 게임 연결·코드·기획 본문의 명칭은 아직 변경하지 않았다.
기존 금고의 내구도 1~5와 제거 규칙을 유지하는 외형이다. 파워 직접 타격 또는 망치에 손상되고 한 수당 최대 1피해이며, 연결 발전기 완충으로 즉시 제거할 수 있다.
내부 고물은 장식이며 별도 보상이나 블록을 생성하지 않는다.

## 생성 프롬프트

### 내구도 5

```text
Use case: stylized-concept. Create ONE standalone salvage recovery capsule obstacle sprite for a cute moon rabbit match-3 game. Reference image is STYLE ONLY: match its thick smooth dark navy outlines, simple large flat color planes, restrained upper-left highlights, friendly rounded shapes and readability at 40 screen pixels. Do NOT make a safe.
Subject: a LOW WIDE CYLINDRICAL salvage capsule, width about 1.25 times height, viewed from slightly above from front, symmetrical centered. Durability 5, fully armored state. Broad squat cylindrical ivory container hidden inside smooth steel-blue outer shell; large muted blue domed top and wraparound blue armored body, a substantial continuous orange locking band runs VERTICALLY over the center of the dome and down the front like a strapped parcel, locked by ONE simple large dark navy clasp. Wide turquoise bottom pedestal rim remains visible, with two tiny dark feet. Shell and band should dominate the image with very few details. The same teal pedestal and underlying ivory container will persist when armor is stripped in later versions. No handles or face, no dial, no rivets or small triangle corner plates, no small decorations, no lettering, no numerals, no coins, no glows or particles, no ground shadow, no background or tile backing. Genuine transparent PNG background. Exactly one whole object in square canvas with generous 12% margins, keep room above for an open hinged lid in a later state. Target output 256x256.
```

### 내구도 4

```text
Use case: precise-object-edit. Input is edit target: salvage capsule game sprite. Create exactly ONE durability variant, not a sheet. Durability 4: REMOVE the entire orange locking strap AND central clasp. The blue armored shell remains but is split along its FRONT CENTER by one large vertical open seam about 15 percent of body width, revealing a broad ivory strip of inner container. Continue this clearly visible separation across the blue dome, like two separated outer shell halves. Keep the silhouette closed, no floating parts. No orange anywhere. Preserve the reference's teal pedestal, two feet, navy outlines, light from upper left, camera perspective, centered framing, and broad clean cartoon shapes. Keep same body registration and scale. Make a legible 256x256 transparent PNG game icon. No labels, numbers, background, ground shadow, loose debris, faces, tiny rivets or small triangle plates. Actual transparent alpha, square canvas.
```

### 내구도 3

```text
Use case: precise-object-edit. Input is edit target: salvage capsule game sprite. Create exactly ONE durability variant, not a sheet. Durability 3: REMOVE the entire orange strap and clasp AND REMOVE ALL blue upper dome armor. Expose a smooth IVORY closed domed lid and upper half of ivory inner container. ONLY a thick blue armor sleeve around the LOWER HALF of the cylindrical body remains, sitting immediately above the unchanged teal pedestal rim. Clear two-tone design: ivory upper half, blue lower half. No loose pieces, no orange. Preserve the reference's teal pedestal, two feet, navy outlines, light from upper left, camera perspective, centered framing, and broad clean cartoon shapes. Keep same body registration and scale. Make a legible 256x256 transparent PNG game icon. No labels, numbers, background, ground shadow, loose debris, faces, tiny rivets or small triangle plates. Actual transparent alpha, square canvas.
```

### 내구도 2

```text
Use case: precise-object-edit. Input is edit target: salvage capsule game sprite. Create exactly ONE durability variant, not a sheet. Durability 2: REMOVE the entire orange strap and clasp AND ALL blue armor shell, top AND sides. Expose just the IVORY cylindrical inner container with a simple firmly CLOSED shallow ivory domed lid, seated on the unchanged teal pedestal rim and feet. The entire body and closed lid are ivory, no blue panels or armor remaining, only navy outlines. Same low wide cylinder and same width, original position and bottom baseline. No orange, no loose parts. Preserve the reference's teal pedestal, two feet, navy outlines, light from upper left, camera perspective, centered framing, and broad clean cartoon shapes. Keep same body registration and scale. Make a legible 256x256 transparent PNG game icon. No labels, numbers, background, ground shadow, loose debris, faces, tiny rivets or small triangle plates. Actual transparent alpha, square canvas.
```

### 내구도 1

```text
Use case: precise-object-edit. Input is edit target: salvage capsule game sprite. Create exactly ONE durability variant, not a sheet. Durability 1: REMOVE the entire orange strap and clasp AND ALL blue armor. Expose an IVORY cylinder on the unchanged teal pedestal rim. OPEN the ivory lid fully, hinged at the BACK, tilted upright behind the container so its inside is clearly visible. Dark interior opening is large and readable; inside are THREE simple oversized scrap pieces: one gray gear, one bent copper tube, one teal metal chunk, not overflowing. Keep the original container width, bottom rim exact position, same bottom baseline; the open lid extends into the available TOP margin without cropping. Distinct unmistakably open container silhouette. No blue armor, no orange band, no coins, no treasure glow. Preserve the reference's teal pedestal, two feet, navy outlines, light from upper left, camera perspective, centered framing, and broad clean cartoon shapes. Keep same body registration and scale. Make a legible 256x256 transparent PNG game icon. No labels, numbers, background, ground shadow, loose debris, faces, tiny rivets or small triangle plates. Actual transparent alpha, square canvas.
```


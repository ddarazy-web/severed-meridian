# 거미줄 내구도별 이미지 v1

현재 CoverKind.Web의 최대 내구도 3에 맞춘 256×256px 투명 PNG 3장이다.

| 내구도 | 표현 |
|---|---|
| 1 | 중하단에 처진 절반 거미줄과 위쪽 양편에 조금 남은 찢어진 조각 |
| 2 | 바깥 연결 줄이 사라지고 내부 줄 일부가 끊어진 모습 |
| 3 | 기존 온전한 거미줄 |

저장: `Assets/Textures/Obstacles/Web/web-durability-{1,2,3}-v1-256.png`

1·2단계는 built-in image_gen으로 생성하여 알파를 유지해 축소했다. 3단계는 기존 기본 파일을 그대로 복사했다.
모두 256×256 크기와 네 모서리 투명도를 확인했다. 현재 1단계는 중앙 빈 공간의 투명도를 검사하고, 중하단의 처진 거미줄과 위쪽의 찢어진 조각 두 개를 256px에서 눈으로 확인했다. 2단계 중앙 160×160 영역의 완전 투명 픽셀 비율은 64.5%다.
기존 3단계는 고정 검사 지점이 줄 위에 걸렸고 중앙 영역 투명 비율도 임의로 잡은 50% 기준에 미달했다. 기존 원본을 그대로 보존했으며, 실제 보드에서 가독성을 확인하여 필요시 줄 두께를 조정해야 한다.
단계별 독립 이미지로 정확히 정렬된 애니메이션 레이어는 아니다. 게임 보드 위에서의 가독성과 피격 연출은 아직 검증하지 않았다.
이미지 제작까지이며 에디터 연결이나 규칙 변경은 포함하지 않는다.

## 프롬프트

### 1단계 상단 조각 추가본 (현재 사용)

사용자 요청으로 위쪽에도 찢어진 조각을 조금 남겼다. built-in image_gen으로 수정하여 기존 PNG 경로에 저장했다.

Edit this damaged cobweb game overlay sprite. KEEP THE EXISTING LOWER HALF WEB EXACTLY as it is: its sagging curved mesh, size, position, ivory silk, navy outline, thickness and torn upper edge. User wants a LITTLE torn web remaining ABOVE too. Add only TWO SMALL broken web remnants near the upper-left and upper-right original web anchor areas, roughly x=20-35%,y=18-35% and x=67-80%,y=20-33%. Each remnant is a short curved silk arc with one small triangular mesh corner and 1-2 short ragged dangling ends, hanging slightly downward. Asymmetric sizes, upper-left slightly larger. They must look like torn fragments of the same spiderweb, not floating stars, dots or straight sticks. These upper fragments must remain DISCONNECTED from the lower web, with a large open transparent gap in the center. Do not reconstruct the top half or fill the blank space; the dominant feature is still the existing sagging lower half, only a little silk remains above. Same simple cartoon style, no spider, no labels, no opaque film, no background. Single square transparent PNG, same safe margins.

### 1단계 절반 잔여 수정본 (교체됨)

사용자 요청에 따라 남은 양을 절반 정도로 늘리고, 중하단 위치와 처진 느낌을 유지했다. built-in image_gen 사용, 기존 PNG 경로에 저장.

Edit a cobweb game sprite. Image 1 is the current damaged version: its sagging feel is good BUT it has far too little web. Image 2 is the original intact web, use it for scale, silk style and mesh structure. Create a replacement damaged web with approximately HALF of the original web remaining, concentrated across the MIDDLE AND LOWER HALF of the square. It must be substantially larger and fuller than image 1. Span roughly x=12%-88%, y=40%-88%; leave upper third entirely transparent. Show a broad connected half-web, around 3 scalloped curved silk rows and 5 short radial connecting strands, giving several clearly visible web mesh cells. Upper edge irregularly ripped with a few short broken ends. The whole half-web hangs slightly downward, gently sagging like loose fabric, not a tiny U-shaped thread and not a full circular web. Preserve ivory silk and navy outlines, simple flat cartoon match3 overlay style of image 2. Fine readable strands, transparent holes, no opaque sheet, no spider, no labels, no detached upper fragments, no backdrop. One square transparent sprite, full-tile framing, safe margins.

### 1단계 소량 잔여 시안 (교체됨)

사용자 요청: 대부분 찢어져 중하단부만 남고 약간 처진 느낌. built-in image_gen으로 수정하고 기존 PNG 경로에 저장했다.

Edit this torn cobweb game overlay sprite. User correction: Almost the ENTIRE web is torn away; ONLY ONE small connected remnant remains in the MIDDLE-LOWER portion of the square. Upper 55 percent of canvas must be completely EMPTY transparent, with NO silk at all, no top anchors, no detached fragments. Remove all upper and side web patches. Remaining remnant confined roughly to x=25%-78%, y=57%-84%: a sparse little torn web hammock consisting of 2 or 3 curved connected silk arcs with a few short broken radial threads, clearly recognizable as spiderweb mesh. It hangs loosely and slightly SAGS downward under gravity; middle of curves droops lower than their ends, no taut straight lines, no star shape. Thin ivory silk with dark navy outlines matching reference, simple flat cartoon game sprite, a few short ragged broken ends. Leave ample transparent space around AND between threads. Keep original square canvas and full-tile scale: do NOT enlarge or recenter the remnant to fill the image. Transparent PNG, no spider, no block, no lettering, no background or shadow, one sprite only.

### 1단계 첫 수정본 (교체됨)

사용자 요청에 따라 직선 몇 가닥으로 된 시안을 많이 찢어진 거미줄로 교체했다. 기존 1단계 PNG 경로를 유지했으며 .meta는 수정하지 않았다.

Edit the reference INTACT COBWEB sprite into a heavily torn cobweb for durability 1. Must unmistakably read as a ripped SPIDER WEB, NOT a star, asterisk, snowflake, spokes or straight crossed sticks. Preserve ivory silk and dark navy outline cartoon style, square footprint and center placement. Show several irregular remnants of CURVED scalloped web mesh: a small connected torn mesh patch in upper-left and another in lower-right, with a few slack curved radial silk strands and short dangling broken ends. Make large irregular holes where most mesh was torn away. Break the central hub and break long straight diameters so there is no uninterrupted cross or asterisk. Around 65-75 percent of the original silk is missing, but enough curved interconnected web arcs remain to identify it as damaged cobweb. Thin readable strands and simple shapes suitable for 256px match3 overlay. All spaces between strands completely transparent; no film, no shadow backdrop, no spider, no block, no letters. Single centered square transparent sprite with safe margins. It must look more damaged than a partially broken web, but not merely a set of straight lines.

### 1단계 최초 시안 (교체됨)

Edit target: this single cobweb game overlay sprite. Create durability 1 of 3, a heavily weakened cobweb. Keep the original ivory silk and dark navy outline style, exact centered position and square footprint. Retain ONLY a thin vertical strand, thin horizontal strand and thin diagonal from top-left to bottom-right, all meeting at center, with a couple short broken curved silk ends near outer tips. Remove ALL continuous concentric curved web rings and remove the other diagonal. The three remaining strands are about HALF the original strand thickness, thin but clearly readable at 256px, smooth simple cartoon lines. Most of the image must be fully transparent so a colorful game block behind is visible. Actual transparent background and holes; no translucent film, no spider, no block, no letters, no extra objects, no sheet. Same safe margin.

### 2단계

Edit target: this single cobweb game overlay sprite. Create durability 2 of 3, a partially broken cobweb. Preserve original ivory silk with dark navy outline, centered position, eight spoke directions and square footprint. Make the silk strands slightly thinner than original. Remove the entire outer curved ring between the eight spoke tips, leaving all spoke tips free. Keep the small INNER curved ring around the central intersection but break TWO separated segments of it, one upper-right and one lower-left, leaving obvious gaps. Keep all eight radial spokes connected at center. The result must visibly be less dense and weaker than original but stronger than a few bare strands. Most of image is transparent so a colorful game block behind remains visible. Actual transparent background and holes, no translucent film, no spider, no block, no letters, no sheet. Same safe margin.

## 원본

원본 폴더: `C:/Users/ddara/.codex/generated_images/01a0c2e2-6b1d-7a12-afac-7de1402fe58d/`

- 1단계 최초 시안 (교체됨): `exec-9b6181e5-c0aa-468f-b12f-e7e7bf77a658.png`
- 1단계 첫 수정본 (교체됨): `exec-0fca76e0-5457-4f27-b333-9cbfd45ec473.png`
- 1단계 소량 잔여 시안 (교체됨): `exec-e66cbd31-9cfb-4e4b-8b03-1bdaddb946ee.png`
- 1단계 절반 잔여 수정본 (교체됨): `exec-9b8599d9-d4c6-44be-83d8-2486ca86a065.png`
- 1단계 상단 조각 추가본 (현재 사용): `exec-5da6dd80-e008-4d01-b191-2eb9b62a9b1c.png`
- 2단계: `exec-f342ea32-41e9-4df9-8266-297f37fcb223.png`


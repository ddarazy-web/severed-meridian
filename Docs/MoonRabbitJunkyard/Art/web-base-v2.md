# 거미줄 기본 이미지 시안 v2

- 제작일: 2026-09-30
- 사용자 요청: 기존 거미줄을 조금 색다르게 다시 제작.
- 내장 image_gen으로 v1을 참고해 비대칭 거미줄을 생성했다. 중심을 왼쪽 위로 옮기고, 네 모서리에 걸린 처진 곡선과 크기가 다른 빈 공간으로 표현했다.
- 크림색 실과 남색 외곽선을 유지하고 옅은 청색 음영을 사용했다. 실제 투명 배경과 구멍을 갖는 덮개용 이미지다.
- [PNG](../../../Assets/Textures/Obstacles/Web/web-base-v2.png): 1254×1254px, 32비트 알파. 원본과 복사본의 해시 일치, 좌상단·중앙 투명도를 확인했다.
- 기존 v1과 내구도별 이미지는 보존했다. 이번 산출물은 새 기본 시안 한 장이며, 내구도 변형·256px 축소·게임 연결·실제 보드 가독성 검수는 포함하지 않는다.

## 생성 프롬프트

Use case: precise-object-edit / stylized-concept. Reference image is the OLD intact spiderweb overlay for a cute moon-rabbit match-3 puzzle. REDESIGN it with a noticeably different but compatible silhouette: an asymmetrical cobweb stretched across the square from four corner anchor tips, with the web hub off-center toward the upper LEFT (around 35% x, 35% y). About five gently bowed radial silk strands and only TWO irregular scalloped connecting rows; larger open gaps toward lower right. Silky threads slightly sag, organically uneven spacing, softly tapered anchor ends. It should instantly read as an intact sticky SPIDERWEB catching a tile, not a symmetric eight-point badge, snowflake, asterisk or straight crossed sticks. Preserve friendly clean cartoon treatment, warm ivory silk, dark navy #28364F outlines, restrained pale cool-blue underside shading. Slightly varied chunky smooth strands, but overall AIRY OPEN MESH: at least 70% of the tile should remain fully transparent so the colored rabbit underneath remains recognizable. All mesh holes and backdrop genuinely transparent, absolutely no opaque film. One web only centered in square canvas, 8% safe margin, no cropping. No rabbit, spider, eyes, dewdrops, glow, stars, particle decorations, backing tile, text, shadow or extra objects. Game-ready overlay concept, readable at small sizes, simple limited shading not realistic.


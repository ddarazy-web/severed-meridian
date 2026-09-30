# 발전기 충전 단계 이미지 v1

발전기는 내구도 대신 필요 충전량 3~5회를 사용한다. 기존 작동 전·후 2장에 빠진 단계별 충전 표시를 추가했다.

| 필요 충전량 | 이미지 단계 | 장수 |
|---|---|---|
| 3회 | 0/3, 1/3, 2/3, 3/3 | 4 |
| 4회 | 0/4, 1/4, 2/4, 3/4, 4/4 | 5 |
| 5회 | 0/5, 1/5, 2/5, 3/5, 4/5, 5/5 | 6 |

총 15장, 모두 512×512px 투명 PNG.
저장 폴더: `Assets/Textures/Obstacles/Generator/`
파일명: `generator-charge-{현재충전}-of-{필요충전}-v1-512.png`

## 표현

본체 상단의 별도 표시등이 왼쪽부터 하나씩 켜진다. 필요 충전량에 따라 표시등 수 자체가 3·4·5개다.
미충전 등과 중앙 번개는 회색이며, 완충 시 모든 충전등과 중앙 번개가 노란색으로 켜진다.
하단 단자 3개는 충전등과 별개이며 이번 이미지에서는 모두 꺼진 상태를 유지한다. 개별 연결 단자 점등은 포함하지 않는다.

완충 이미지는 발동 순간의 표현용이다. 기존 규칙대로 완충 시 연결 대상을 제거하고 발전기도 사라지며, 완충 후 지속 상태를 새로 추가한 것이 아니다.

## 검수 및 범위

built-in image_gen으로 기본형 3장과 점등 변형 12장을 만들고 알파를 유지해 축소 저장했다.
15장 생성 결과의 표시등 개수와 왼쪽부터 점등 여부를 눈으로 확인했다. 모든 최종 파일의 크기와 네 모서리 투명도를 검사했다. 축소된 2/4와 5/5도 확인했다.
독립 생성된 이미지여서 윤곽·색·표시등 위치가 픽셀 단위로 완전히 일치하지 않는다.
기존 이미지는 보존했고, 게임·에디터 연결 및 실제 보드에서의 전환 검증은 아직 하지 않았다.

## 파일 및 원본

원본 폴더: `C:/Users/ddara/.codex/generated_images/01a0c2e2-6b1d-7a12-afac-7de1402fe58d/`

| 파일 | 생성 원본 |
|---|---|
| `generator-charge-0-of-3-v1-512.png` | `exec-fc32870b-dd75-4e2e-8d24-1078dd668866.png` |
| `generator-charge-1-of-3-v1-512.png` | `exec-deddde40-edfd-420b-9466-e009b3f72dc9.png` |
| `generator-charge-2-of-3-v1-512.png` | `exec-628787ad-3edc-45ea-b2d8-4c414136c562.png` |
| `generator-charge-3-of-3-v1-512.png` | `exec-cf6f8b54-1dd3-43a7-a2cf-b7ce76e1ce49.png` |
| `generator-charge-0-of-4-v1-512.png` | `exec-df2eba20-f6e5-4096-8b17-36f9bf2ab612.png` |
| `generator-charge-1-of-4-v1-512.png` | `exec-6a146351-b2ab-4afc-94a1-075235c903ae.png` |
| `generator-charge-2-of-4-v1-512.png` | `exec-1184886a-8be2-4dba-b287-2523c62760d0.png` |
| `generator-charge-3-of-4-v1-512.png` | `exec-f0ba55c6-131c-4a88-bad1-f0e9f147770c.png` |
| `generator-charge-4-of-4-v1-512.png` | `exec-394d1fc9-63a3-47e7-a387-7382e714515a.png` |
| `generator-charge-0-of-5-v1-512.png` | `exec-8d920493-e509-4316-8bc2-e6bbecb4da52.png` |
| `generator-charge-1-of-5-v1-512.png` | `exec-e5aa990f-3e20-442d-88cb-784ca89b3b28.png` |
| `generator-charge-2-of-5-v1-512.png` | `exec-737522af-08b1-4489-a484-a6551dd3314f.png` |
| `generator-charge-3-of-5-v1-512.png` | `exec-66ebcf78-bb7d-42f6-865a-b34719d80b7f.png` |
| `generator-charge-4-of-5-v1-512.png` | `exec-4e0bb6bc-ea9d-472e-804e-6cf63276c186.png` |
| `generator-charge-5-of-5-v1-512.png` | `exec-203bbea1-d68e-44f1-98d2-b7117986e3c8.png` |

## 프롬프트

### 0/3

Edit reference generator sprite. Preserve its exact body, handle, feet, center gray lightning bolt, gauge, vents, three bottom sockets, navy outlines, colors, front view and square framing. Add a compact horizontal row of exactly 3 large round charge indicator lamps on the upper blue body just below handle and ABOVE bolt/vents/gauge. All lamps unlit dark slate blue with navy borders. Row fits in the empty upper body strip without covering existing features. These lamps are separate from the THREE bottom electrical sockets which remain unlit and unchanged. Do not add any other lamps or markings. Simple pastel cartoon match3 art. One generator sprite, actual transparent background and handle hole, no text or numerals, no sheet.

### 1/3

Edit this generator: there are 3 small charge lamps in TOP row. Illuminate exactly the LEFTMOST 1 of these top lamps warm yellow with ivory highlight. Keep remaining 2 top lamps unlit dark slate. Keep all lamp count, positions and sizes identical. Keep central lightning bolt gray and unlit. The THREE LARGE BOTTOM SOCKETS remain gray and UNLIT regardless of top lights. Preserve all other pixels/geometry, body colors, handle, feet, gauge, vents, scale and framing. Simple cartoon transparent PNG, no text, no extra objects or symbols.

### 2/3

Edit this generator: there are 3 small charge lamps in TOP row. Illuminate exactly the LEFTMOST 2 of these top lamps warm yellow with ivory highlight. Keep remaining 1 top lamps unlit dark slate. Keep all lamp count, positions and sizes identical. Keep central lightning bolt gray and unlit. The THREE LARGE BOTTOM SOCKETS remain gray and UNLIT regardless of top lights. Preserve all other pixels/geometry, body colors, handle, feet, gauge, vents, scale and framing. Simple cartoon transparent PNG, no text, no extra objects or symbols.

### 3/3

Edit this generator: there are 3 small charge lamps in TOP row. Illuminate exactly the LEFTMOST 3 of these top lamps warm yellow with ivory highlight. Keep remaining 0 top lamps unlit dark slate. Keep all lamp count, positions and sizes identical. All top lamps now lit; also change central lightning bolt to luminous yellow, subtle glow. The THREE LARGE BOTTOM SOCKETS remain gray and UNLIT regardless of top lights. Preserve all other pixels/geometry, body colors, handle, feet, gauge, vents, scale and framing. Simple cartoon transparent PNG, no text, no extra objects or symbols.

### 0/4

Edit reference generator sprite. Preserve its exact body, handle, feet, center gray lightning bolt, gauge, vents, three bottom sockets, navy outlines, colors, front view and square framing. Add a compact horizontal row of exactly 4 large round charge indicator lamps on the upper blue body just below handle and ABOVE bolt/vents/gauge. All lamps unlit dark slate blue with navy borders. Row fits in the empty upper body strip without covering existing features. These lamps are separate from the THREE bottom electrical sockets which remain unlit and unchanged. Do not add any other lamps or markings. Simple pastel cartoon match3 art. One generator sprite, actual transparent background and handle hole, no text or numerals, no sheet.

### 1/4

Edit this generator: there are 4 small charge lamps in TOP row. Illuminate exactly the LEFTMOST 1 of these top lamps warm yellow with ivory highlight. Keep remaining 3 top lamps unlit dark slate. Keep all lamp count, positions and sizes identical. Keep central lightning bolt gray and unlit. The THREE LARGE BOTTOM SOCKETS remain gray and UNLIT regardless of top lights. Preserve all other pixels/geometry, body colors, handle, feet, gauge, vents, scale and framing. Simple cartoon transparent PNG, no text, no extra objects or symbols.

### 2/4

Edit this generator: there are 4 small charge lamps in TOP row. Illuminate exactly the LEFTMOST 2 of these top lamps warm yellow with ivory highlight. Keep remaining 2 top lamps unlit dark slate. Keep all lamp count, positions and sizes identical. Keep central lightning bolt gray and unlit. The THREE LARGE BOTTOM SOCKETS remain gray and UNLIT regardless of top lights. Preserve all other geometry, body colors, handle, feet, gauge, vents, scale and framing. Simple cartoon transparent PNG, no text, no extra objects or symbols.

### 3/4

Edit this generator: there are 4 small charge lamps in TOP row. Illuminate exactly the LEFTMOST 3 of these top lamps warm yellow with ivory highlight. Keep remaining 1 top lamps unlit dark slate. Keep all lamp count, positions and sizes identical. Keep central lightning bolt gray and unlit. The THREE LARGE BOTTOM SOCKETS remain gray and UNLIT regardless of top lights. Preserve all other geometry, body colors, handle, feet, gauge, vents, scale and framing. Simple cartoon transparent PNG, no text, no extra objects or symbols.

### 4/4

Edit this generator: there are 4 small charge lamps in TOP row. Illuminate exactly the LEFTMOST 4 of these top lamps warm yellow with ivory highlight. Keep remaining 0 top lamps unlit dark slate. Keep all lamp count, positions and sizes identical. All top lamps now lit; also change central lightning bolt to luminous yellow, subtle glow. The THREE LARGE BOTTOM SOCKETS remain gray and UNLIT regardless of top lights. Preserve all other geometry, body colors, handle, feet, gauge, vents, scale and framing. Simple cartoon transparent PNG, no text, no extra objects or symbols.

### 0/5

Edit reference generator sprite. Preserve its exact body, handle, feet, center gray lightning bolt, gauge, vents, three bottom sockets, navy outlines, colors, front view and square framing. Add a compact horizontal row of exactly 5 large round charge indicator lamps on the upper blue body just below handle and ABOVE bolt/vents/gauge. All lamps unlit dark slate blue with navy borders. Row fits in the empty upper body strip without covering existing features. These lamps are separate from the THREE bottom electrical sockets which remain unlit and unchanged. Do not add any other lamps or markings. Simple pastel cartoon match3 art. One generator sprite, actual transparent background and handle hole, no text or numerals, no sheet.

### 1/5

Edit this generator: there are 5 small charge lamps in TOP row. Illuminate exactly the LEFTMOST 1 of these top lamps warm yellow with ivory highlight. Keep remaining 4 top lamps unlit dark slate. Keep all lamp count, positions and sizes identical. Keep central lightning bolt gray and unlit. The THREE LARGE BOTTOM SOCKETS remain gray and UNLIT regardless of top lights. Preserve all other geometry, body colors, handle, feet, gauge, vents, scale and framing. Simple cartoon transparent PNG, no text, no extra objects or symbols.

### 2/5

Edit this generator: there are FIVE small charge lamps in TOP row. Illuminate exactly the LEFTMOST 2 top lamps warm yellow with ivory highlight. Keep remaining 3 top lamps unlit dark slate. Keep all FIVE lamp positions and sizes identical. Keep central lightning bolt gray and unlit. The THREE LARGE BOTTOM SOCKETS remain gray and UNLIT regardless of top lights. Preserve all other geometry, body colors, handle, feet, gauge, vents, scale and framing. Simple cartoon transparent PNG, no text, no extra objects or symbols.

### 3/5

Edit this generator: there are FIVE small charge lamps in TOP row. Illuminate exactly the LEFTMOST 3 top lamps warm yellow with ivory highlight. Keep remaining 2 top lamps unlit dark slate. Keep all FIVE lamp positions and sizes identical. Keep central lightning bolt gray and unlit. The THREE LARGE BOTTOM SOCKETS remain gray and UNLIT regardless of top lights. Preserve all other geometry, body colors, handle, feet, gauge, vents, scale and framing. Simple cartoon transparent PNG, no text, no extra objects or symbols.

### 4/5

Edit this generator: there are FIVE small charge lamps in TOP row. Illuminate exactly the LEFTMOST 4 top lamps warm yellow with ivory highlight. Keep remaining 1 top lamps unlit dark slate. Keep all FIVE lamp positions and sizes identical. Keep central lightning bolt gray and unlit. The THREE LARGE BOTTOM SOCKETS remain gray and UNLIT regardless of top lights. Preserve all other geometry, body colors, handle, feet, gauge, vents, scale and framing. Simple cartoon transparent PNG, no text, no extra objects or symbols.

### 5/5

Edit this generator: there are FIVE small charge lamps in TOP row. Illuminate exactly the LEFTMOST 5 top lamps warm yellow with ivory highlight. Keep remaining 0 top lamps unlit dark slate. Keep all FIVE lamp positions and sizes identical. All five top lamps now lit; also change central lightning bolt to luminous yellow with subtle glow. The THREE LARGE BOTTOM SOCKETS remain gray and UNLIT regardless of top lights. Preserve all other geometry, body colors, handle, feet, gauge, vents, scale and framing. Simple cartoon transparent PNG, no text, no extra objects or symbols.


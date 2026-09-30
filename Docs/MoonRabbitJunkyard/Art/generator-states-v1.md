# 발전기 작동 상태 이미지 v1

후속: 충전량 3~5회별 중간 단계 15장은 [발전기 충전 단계 이미지](generator-charge-v1.md)에 추가했다. 아래 2장은 초기 상태 비교용이며 충전 단계 전체를 나타내지 않는다.

2×2칸 발전기의 작동 전·후 기준 이미지 2장이다. 모두 512×512px 투명 PNG.

| 상태 | 표현 | 파일 |
|---|---|---|
| 꺼짐 | 회색 번개, 불 꺼진 단자 3개 | `Assets/Textures/Obstacles/Generator/generator-off-v1-512.png` |
| 켜짐 | 노란 번개와 노란 불빛의 단자 3개 | `Assets/Textures/Obstacles/Generator/generator-on-v1-512.png` |

켜짐 이미지는 출력 단자 3개가 모두 켜진 기준 모습이다. 연결된 단자만 개별 점등하는 조합별 이미지나 분리된 불빛 레이어는 이번 2장에 포함하지 않는다. 실제 게임에서 개별 연결 상태를 나타내려면 별도 표시 처리가 필요하다.
기존 기본 이미지는 보존했다. 게임 규칙과 에디터 이미지 연결은 변경하지 않았다.

built-in image_gen으로 생성한 뒤 알파를 유지해 축소 저장했다. 두 파일의 크기, 모서리 및 손잡이 안쪽 투명도를 검사하고 512px에서 불빛 차이와 단자 3개를 눈으로 확인했다.
독립 생성된 스프라이트이므로 윤곽과 위치가 픽셀 단위로 완전히 일치하지 않는다. 실제 게임 보드에서의 상태 전환은 아직 검증하지 않았다.

## 프롬프트

### 꺼짐

Edit target: the reference cute portable generator game sprite. Preserve EXACT same steel-blue rounded body, yellow handle, two feet, two left ventilation slots, right gauge, central lightning-bolt shape and THREE bottom circular electrical sockets with two dark holes each. Preserve front view, proportions, object position, thick navy outlines, simple pastel cartoon shading and square safe margin. Single transparent sprite, no text, no background, no extra objects, no cables. Create POWER OFF state. Recolor ONLY central lightning bolt to muted slate gray with a subtle pale gray highlight, clearly unlit. All three socket rings and interiors remain muted gray-blue, no illumination anywhere. Keep the handle yellow. Do not add damage.

### 켜짐

Edit target: the reference cute portable generator game sprite. Preserve EXACT same steel-blue rounded body, yellow handle, two feet, two left ventilation slots, right gauge, central lightning-bolt shape and THREE bottom circular electrical sockets with two dark holes each. Preserve front view, proportions, object position, thick navy outlines, simple pastel cartoon shading and square safe margin. Single transparent sprite, no text, no background, no extra objects, no cables. Create POWER ON state with all three outputs connected. Central lightning bolt is bright warm yellow with an ivory highlight and a subtle narrow golden glow around it. Illuminate the inner rim of EACH of the three bottom socket rings with a bright warm yellow light, keeping the navy outer outlines and both dark plug holes sharply visible. Keep glow contained close to the markings, not a big halo covering the body, and no light outside the generator silhouette. Leave body, handle, gauge and other geometry unchanged. Clear simple lights, no sparks or rays.

## 생성 원본

폴더: `C:/Users/ddara/.codex/generated_images/01a0c2e2-6b1d-7a12-afac-7de1402fe58d/`

- 꺼짐: `exec-6d41a6ca-400b-48dd-987c-affe7e2a3673.png`
- 켜짐: `exec-6dfe23f1-e162-42a7-8bcc-dc3a7dcb2389.png`


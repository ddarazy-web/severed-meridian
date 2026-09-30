# 수집 드론 프로펠러 4프레임 v1

## 최종 편집 프롬프트 (내장 image_gen)

Use case: precise-object-edit. Edit the supplied drone animation sheet. Preserve exactly the 2x2 layout, transparent background, teal guards, navy hubs and arms, ivory body, turquoise intake and orange notch, claws, shading, body size and center positions. Change ONLY the ivory two-blade propellers. This is a four-frame looping animation, reading order top-left, top-right, bottom-left, bottom-right. Within EACH drone, ALL FOUR propellers must have DISTINCT angles, not synchronized. Each propeller advances 45 degrees clockwise per successive frame. Blade axes are measured clockwise from screen horizontal, modulo 180 (two opposed blades). Use this exact orientation table, columns being rotor positions within each drone [upper-left, upper-right, lower-left, lower-right]:
Frame1 upper-left cell: [0,45,90,135] degrees.
Frame2 upper-right cell: [45,90,135,0] degrees.
Frame3 lower-left cell: [90,135,0,45] degrees.
Frame4 lower-right cell: [135,0,45,90] degrees.
0=horizontal,45=backslash diagonal,90=vertical,135=forward-slash diagonal.
Every rotor retains exactly two opposed cream paddles and navy center hub. All rings and hubs stationary across frames. No body bobbing or rotating, no motion blur, no extra blades, no captions or grid lines. Genuine transparent alpha including rotor holes. Request actual output 512x512 pixels, 256x256 per equal cell. Match the input clean outlined cartoon style and all non-blade details.

## 출력 정보

- 생성 방식: 내장 image_gen 편집
- 참조: Assets/Textures/PowerBlocks/collection-drone-v1.png
- 결과: Assets/Textures/PowerBlocks/collection-drone-rotor-4frames-v1.png
- 크기: 512 × 512 PNG, RGBA
- 분할: 2 × 2, 셀당 256 × 256
- 재생 순서: 좌상 → 우상 → 좌하 → 우하 → 반복
- 각 컷 안에서 네 프로펠러의 시작 각도를 서로 다르게 배치하고, 다음 컷마다 각각 45°씩 회전한다.
- 로터 순서 [좌상, 우상, 좌하, 우하] 기준: 1컷 [0°,45°,90°,135°], 2컷 [45°,90°,135°,0°], 3컷 [90°,135°,0°,45°], 4컷 [135°,0°,45°,90°].
- 권장 시작 설정: 12fps, Loop, 각 셀 피벗 중앙
- 크기 보정: 기존 시트를 고품질 Bicubic으로 512 × 512로 축소. 그림과 프레임 순서를 유지했다.
- 확인: 저장된 PNG의 실제 크기 512 × 512, RGBA, 모서리와 시트 중앙 알파 0 확인.
- 아직 Unity 스프라이트 분할 및 AnimationClip 연결은 하지 않았다. 생성 이미지이므로 몸체의 픽셀 단위 동일성 및 실제 반복 재생 품질은 Unity 적용 시 확인해야 한다.

## 초기 생성 프롬프트 (아래 최종 편집으로 각도 변경)

```text
Use case: precise-object-edit.
Input image 1 is the exact drone design to animate.
Create ONE production sprite sheet of exactly FOUR animation frames, a square canvas divided into a precisely equal 2 by 2 grid. Transparent background, transparent empty holes between propeller blades, no visible grid, no captions or frame numbers.
Reading order upper left, upper right, lower left, lower right.
Each quadrant contains one entire identical drone from reference, uniformly scaled to 85% of its square cell and centered at exactly the same local coordinates. Preserve the exact ivory body, turquoise intake, orange notch, two bottom claws, four navy diagonal arms, four turquoise circular rotor guards, shading, perspective and thick navy outlines. Body, arms, rotor guards, hubs, highlights, camera, object size, silhouette and placement MUST remain identical across all four frames. No bobbing, no body rotation, no perspective changes, no added parts.
Animate ONLY the FOUR ivory TWO-BLADE PROPELLERS within their fixed guard rings. In each frame all four propellers have the same rotation phase in their rotor plane:
frame 1 top left: blade axes horizontal, 0 degrees;
frame 2 top right: blade axes diagonal down-right/up-left, 45 degrees;
frame 3 bottom left: blade axes vertical, 90 degrees;
frame 4 bottom right: blade axes diagonal up-right/down-left, 135 degrees.
Use foreshortening appropriate to the original slightly tilted top view. Each propeller is two opposed cream paddles joined at a stationary navy central hub. Every frame must have a visibly DIFFERENT propeller orientation from the other three. Two-blade 180-degree symmetry makes frame4 to frame1 seamless. Exactly four drones, each with exactly four rotors. Prioritize extremely consistent registration so these four cells can be sliced into a looping game animation. Crisp readable game sprite style matching reference. No blur, motion lines, text, shadows on ground, background colors, checkerboard, or extra objects.
```

# 나무상자 내구도 이미지

내구도 1~6의 개별 이미지다. 기존 상자를 참고해 image_gen으로 생성하고 투명 배경을 유지해 각각 256×256px로 저장했다. 원래 기본 이미지는 보존했다. 화면 연결은 아직 적용하지 않았다.

저장 위치: `Assets/Textures/Obstacles/Crate/`

| 내구도 | 모양 | 파일 |
|---|---|---|
| 1 | 가로판으로 된 본체, 보강판 없음 | `crate-durability-1-v1-256.png` |
| 2 | 세로 보강판 1개 | `crate-durability-2-v1-256.png` |
| 3 | 세로 보강판 2개 | `crate-durability-3-v1-256.png` |
| 4 | 중앙 가로 보강판 추가 · H 모양 | `crate-durability-4-v1-256.png` |
| 5 | 오른쪽 위로 향하는 대각선 보강판 추가 | `crate-durability-5-v1-256.png` |
| 6 | 반대 대각선 보강판 추가 · X 모양 | `crate-durability-6-v1-256.png` |

피격 시 6→5→4→3→2→1 순서로 교체하며, 내구도 0에서는 상자를 제거한다. 한 단계 내려갈 때 보강판 하나가 줄어드는 표현이다. 이미지는 개별 생성본이므로 판 단위로 분리된 애니메이션 레이어가 아니다.

생성 원본 위치: `C:/Users/ddara/.codex/generated_images/01a0c2e2-6b1d-7a12-afac-7de1402fe58d/`

1. `exec-a2727944-7113-40a1-a68f-8b4fa967632f.png`
2. `exec-73b31066-ffd7-476e-8afa-e42406de682b.png`
3. `exec-24ee2094-a3b0-41ab-99e2-513e9e6aae4d.png`
4. `exec-a3bd85d2-cc21-4085-a08a-33415c83fa00.png`
5. `exec-c445b934-8ce9-4b1d-943d-bc61d15997cb.png`
6. `exec-d3930be6-84f9-4806-bee5-fb9362ef322b.png`

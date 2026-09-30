# 고철 뭉치 내구도 이미지

내구도 1~5의 256×256px 투명 PNG다. 1~4단계는 기존 고철 기본 이미지를 참고해 image_gen으로 생성했다. 5단계는 기존 기본 이미지의 복사본이다. 원래 파일은 보존했으며 에디터 및 게임 화면 연결은 아직 적용하지 않았다.

저장 위치: `Assets/Textures/Obstacles/Scrap/`

| 내구도 | 남아 있는 구성 | 파일 |
|---|---|---|
| 1 | 철판 | `scrap-durability-1-v1-256.png` |
| 2 | 철판 + 톱니바퀴 | `scrap-durability-2-v1-256.png` |
| 3 | 철판 + 톱니바퀴 + 파이프 | `scrap-durability-3-v1-256.png` |
| 4 | 3단계 + 육각 볼트 | `scrap-durability-4-v1-256.png` |
| 5 | 4단계 + 버클이 달린 고정띠 | `scrap-durability-5-v1-256.png` |

피격 시 5→4→3→2→1 순서로 구성물이 줄고, 내구도 0에서 남은 철판을 제거한다. 개별 이미지 교체용이며 부품이 분리된 애니메이션 레이어는 아니다. 원본 참조로 그림체를 맞췄으나 단계 간 부품의 위치와 윤곽이 픽셀 단위로 동일한 것은 아니다.

생성 원본 위치: `C:/Users/ddara/.codex/generated_images/01a0c2e2-6b1d-7a12-afac-7de1402fe58d/`

1. `exec-6beac405-bf77-4aba-9331-f8678aa482a5.png`
2. `exec-39633dee-180c-4358-aa52-5ba5128deb69.png`
3. `exec-cd6e49fb-9954-4820-9e6d-b447a627d399.png`
4. `exec-3dc77950-77ec-4ee1-a10f-e011a87fcfc8.png`
5. 기존 `scrap-base-v1-256.png` 사용

최종 파일 5장의 크기와 모서리 투명도를 확인했다.

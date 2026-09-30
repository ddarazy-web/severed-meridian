# 자물쇠 내구도별 이미지 v1

공통 자물쇠와 분홍·노랑·파랑·초록·보라 자물쇠를 각각 내구도 1~3으로 구성했다. 총 18장이며 모두 256×256px 투명 PNG다.

| 내구도 | 표현 |
|---|---|
| 1 | 기존 기본형, 보강 띠 없음 |
| 2 | 몸체 아래쪽 보강 띠 1개 |
| 3 | 몸체 위아래 보강 띠 2개 |

색상과 열쇠 구멍을 유지하며 보강 띠 개수로 내구도를 구분한다. 내구도 1은 기존 기본 이미지를 그대로 복사했고, 2·3은 image_gen으로 생성한 뒤 알파를 유지하여 축소했다.

## 저장 위치

- 공통: `Assets/Textures/Obstacles/Lock/common-lock-durability-{1,2,3}-v1-256.png`
- 색상별: `Assets/Textures/Obstacles/ColorLock/color-lock-{pink,yellow,blue,green,purple}-durability-{1,2,3}-v1-256.png`

색상 대응: Type1 분홍 / Type2 노랑 / Type3 파랑 / Type4 초록 / Type5 보라.

## 검수 및 적용 범위

18장 모두 크기와 모서리·고리 내부의 투명도를 확인했다. 축소된 공통 2단계 및 보라 3단계 이미지도 눈으로 확인했다.
각 이미지는 독립 생성되어 윤곽·위치가 픽셀 단위로 일치하는 애니메이션 레이어는 아니다.
이번 작업은 이미지 파일 추가이며 에디터·게임의 내구도별 이미지 연결이나 공통 자물쇠 게임 규칙 추가는 포함하지 않는다.

## 생성 원본

원본 폴더: `C:/Users/ddara/.codex/generated_images/01a0c2e2-6b1d-7a12-afac-7de1402fe58d/`

| 종류 | 2단계 원본 | 3단계 원본 |
|---|---|---|
| common | `exec-d6a6dc37-52c8-4d00-a2bd-a5d2802c3dca.png` | `exec-3a7c2d36-4cd6-4069-8eca-31addfb3931b.png` |
| pink | `exec-264429de-c0d2-4a5f-8f4c-3ad03b703069.png` | `exec-8437c923-9cde-4c21-8bd6-ff24a28fbe73.png` |
| yellow | `exec-1dcae616-fb4b-447f-b7e2-ce8375f4bbe0.png` | `exec-e49b0900-33ab-4fff-ae07-cbc18d94aec5.png` |
| blue | `exec-f9fc8704-c357-4b6f-a175-bd2c5484b6e0.png` | `exec-0d500f61-7197-4404-8d3f-2a7b6199ef06.png` |
| green | `exec-48782a68-922b-412e-830e-d4dd68d0165a.png` | `exec-a3c6501f-dc4f-4d99-9d0b-29c6255d2bee.png` |
| purple | `exec-f7303395-7722-49e9-8349-e1165eedbd35.png` | `exec-2b1642d1-cf53-4c92-8885-487ed91273e3.png` |


# 대형 폐가전 내구도별 이미지 v1

> 이전 제작 기록. 세탁기 PNG와 메타 파일은 삭제했으며, 현재 이미지는 `MetalRodBox` 폴더의 금속기둥 상자로 대체했다. 아래 경로와 프롬프트는 과거 기록이다.

2×2칸 대형 폐가전(세탁기)의 내구도 1~9단계. 모두 512×512px 투명 PNG.
내구도 1은 본체만 남고 내구도 2~9는 나사 1~8개로 구분한다.

저장 위치: `Assets/Textures/Obstacles/Appliance/appliance-durability-{1..9}-v1-512.png`

## 나사 배치

양옆 나사 자리를 각각 위에서부터 L1~L4, R1~R4로 정의한다.

| 내구도 | 나사 수 | 남아 있는 자리 |
|---|---|---|
| 1 | 0 | 없음 |
| 2 | 1 | L1 |
| 3 | 2 | L1, R4 |
| 4 | 3 | L1, R4, R1 |
| 5 | 4 | L1, R4, R1, L4 |
| 6 | 5 | L1, R4, R1, L4, L2 |
| 7 | 6 | L1, R4, R1, L4, L2, R3 |
| 8 | 7 | L1, R4, R1, L4, L2, R3, R2 |
| 9 | 8 | 모든 자리 |

피격 시 높은 단계에서 낮은 단계로 전환한다. 여러 피해를 한 번에 받으면 해당 내구도 단계로 바로 내려간다. 본체 제거와 나사가 빠지는 연출은 구현하지 않았다.

## 제작 및 검수

built-in image_gen으로 기존 기본 이미지에서 나사를 제거한 1~8단계를 생성했다. 9단계는 기존 기본 파일을 그대로 복사했다. 기본 파일과 생성 원본은 보존했다.
모든 결과에서 나사 개수를 눈으로 확인하고, 최종 9개 파일의 512×512 크기와 네 모서리 투명도를 검사했다. 축소된 3·8단계도 확인했다.
독립 생성된 전체 스프라이트이므로 외형이 픽셀 단위로 완전히 일치하는 분리 레이어는 아니다.
이번 작업은 이미지 추가까지이며 에디터·게임 이미지 연결은 포함하지 않는다.

## 프롬프트

공통 지시:

Edit target: reference washing machine sprite for a casual match-3 game. Preserve the cream rounded body, coral control panel, blue circular glass door and navy ring, small right door handle, feet, scratches, thick navy outline, front view, scale and position EXACTLY. Only change the number of the eight large navy slotted screws on the outer body. Remove unwanted screws completely, including outline and hole; restore plain cream body beneath. Do not add any new detail or screws. Transparent background, square PNG with same margin. One appliance only, no text, no grid. 

각 단계는 다음 자리만 남기고 다른 나사와 구멍을 모두 제거하도록 지시했다. 1단계는 나사 전부 제거.

- 순서 1: uppermost LEFT screw
- 순서 2: lowermost RIGHT screw
- 순서 3: uppermost RIGHT screw
- 순서 4: lowermost LEFT screw
- 순서 5: second-from-top LEFT screw
- 순서 6: second-from-bottom RIGHT screw
- 순서 7: second-from-top RIGHT screw
- 순서 8: second-from-bottom LEFT screw

## 생성 원본

원본 폴더: `C:/Users/ddara/.codex/generated_images/01a0c2e2-6b1d-7a12-afac-7de1402fe58d/`

- 1단계: `exec-b21ca56d-3043-4ffa-82bb-d793a66ccb5d.png`
- 2단계: `exec-1beb6586-1ce9-402e-b231-2164240c818d.png`
- 3단계: `exec-5eb85083-f7a7-4065-9402-4ffed2034f74.png`
- 4단계: `exec-f7e7cd05-4999-4ebc-b603-3810798885f6.png`
- 5단계: `exec-53d84f23-bbd9-437f-8c63-d306c0e84d33.png`
- 6단계: `exec-f39d44ec-d5ec-4d09-badf-2099fa588fc4.png`
- 7단계: `exec-b914c14a-1726-46f2-b35a-2ec805070264.png`
- 8단계: `exec-c7a5a9c3-a611-4e0d-951b-6e10fe6ae94e.png`

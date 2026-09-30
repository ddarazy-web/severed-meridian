# 금고 내구도별 이미지 v1

> 이전 제작 기록. 금고 PNG와 메타 파일은 삭제했으며, 현재 이미지는 `RecoveryCapsule` 폴더의 고물 회수 캡슐로 대체했다. 아래 경로와 프롬프트는 과거 기록이다.

총 5장, 모두 256×256px 투명 PNG. 기존 민트색 금고와 노란 다이얼을 유지하고 문 모서리의 보강판 개수로 구분한다.

| 내구도 | 보강판 위치 |
|---|---|
| 1 | 없음, 기존 기본 이미지 복사 |
| 2 | 왼쪽 위 |
| 3 | 왼쪽 위 + 오른쪽 아래 |
| 4 | 왼쪽 위 + 오른쪽 아래 + 오른쪽 위 |
| 5 | 네 모서리 모두 |

저장: `Assets/Textures/Obstacles/Safe/safe-durability-{1,2,3,4,5}-v1-256.png`

내구도가 줄면 5→4에서 왼쪽 아래, 4→3에서 오른쪽 위, 3→2에서 오른쪽 아래, 2→1에서 왼쪽 위 보강판이 사라진다.
이미지는 독립 생성된 전체 스프라이트라 윤곽과 색이 픽셀 단위로 일치하는 분리 레이어는 아니다.

기본 파일은 유지했다. 이미지 제작만 완료했으며 에디터·게임 연결과 피격 연출은 이번 작업에 포함하지 않았다.
5장 모두 크기와 네 모서리 투명도를 검사했고, 생성 결과의 보강판 수 및 256px 최종 5단계의 가독성을 확인했다.

## 생성 방식과 프롬프트

built-in image_gen 사용. 5단계를 만든 뒤 보강판을 제거하여 2~4단계를 생성했다. 원본은 Codex 생성 폴더에 보존하고 프로젝트에 256px로 축소 저장했다.

### 내구도 5

Edit the reference safe sprite for a casual match-3 game. Preserve the teal rounded square safe, pale mint door, large yellow combination dial, two right-side hinges, small feet, navy thick outlines, flat simple shading, exact front view and composition. Add exactly FOUR small steel-blue triangular corner reinforcement plates with one silver rivet each, one at EACH corner of the door (top-left, top-right, bottom-left, bottom-right). Plates clearly readable but do not cover the dial or hinges. These are removable armor pieces for durability 5. Keep the original art style, no added details elsewhere. Single centered sprite, square transparent PNG, safe margin, no text or background.

### 내구도 2

Use case: precise-object-edit. Edit the single safe sprite. Keep ONLY the TOP-LEFT triangular plate. Remove the top-right, bottom-left and bottom-right plates and their rivets. Exactly ONE plate remains. Fill removed plate areas with the same plain pale mint door surface. Preserve EVERYTHING else: large yellow combination dial, teal body, navy outlines, hinges, feet, scale, position, lighting and silhouette. No new marks or details. Transparent background, square PNG, same margin. One safe only, no text, no sheet.

### 내구도 3

Use case: precise-object-edit. Edit the single safe sprite. Keep ONLY the TOP-LEFT and BOTTOM-RIGHT triangular plates. Remove the top-right and bottom-left plates and their rivets. Exactly TWO plates remain. Fill removed plate areas with the same plain pale mint door surface. Preserve EVERYTHING else: large yellow combination dial, teal body, navy outlines, hinges, feet, scale, position, lighting and silhouette. No new marks or details. Transparent background, square PNG, same margin. One safe only, no text, no sheet.

### 내구도 4

Use case: precise-object-edit. Edit the single safe sprite. Remove ONLY the BOTTOM-LEFT triangular plate and its rivet. Keep top-left, top-right, bottom-right plates. Exactly THREE plates remain. Fill removed plate areas with the same plain pale mint door surface. Preserve EVERYTHING else: large yellow combination dial, teal body, navy outlines, hinges, feet, scale, position, lighting and silhouette. No new marks or details. Transparent background, square PNG, same margin. One safe only, no text, no sheet.

## 생성 원본

폴더: `C:/Users/ddara/.codex/generated_images/01a0c2e2-6b1d-7a12-afac-7de1402fe58d/`

- 2: `exec-310b0612-dc0b-409f-b32f-c1aa0b93b766.png`
- 3: `exec-76906406-f775-4cb0-ae21-b1f652960070.png`
- 4: `exec-33b2b1ae-e086-476f-85ee-719e61a4ffc9.png`
- 5: `exec-478cdefc-5bee-45c4-92e2-3a2a1a29ed22.png`

# 탐색 캐릭터 프로덕션 원본

## `chr_protagonist_male_stage_01`

- 상태: `PRODUCTION / REWORK — DO NOT USE`
- 용도: 1~20레벨 불입류 남성 주인공 유청운의 필드 탐색 대기·이동
- 콘셉트 기준: `ArtSource/Concepts/Characters/chr_protagonist_herbalist_concept_01.png`
- 논리 몸체: 64×96px
- 프레임 셀: 128×128px
- 피벗: 셀 좌상단 기준 `(64, 116)`, 발밑 중앙
- 방향 행: `down → left → right → up`
- 대기: 방향당 2프레임, 4fps, 반복
- 걷기: 방향당 6프레임, 8fps, 반복
- Aseprite 레이어: `shadow`, `equipment_back`, `body`, `clothes`, `head_hair`, `equipment_front`
- 팔레트: `servered-meridian-master-32.gpl`에 포함된 색만 사용하며 그림자만 부분 투명을 허용
- 고유 표식: 청회색 머리끈, 약초 등짐, 장부집, 주홍 수선 끈이 감긴 부러진 약초칼
- 측면 방향은 소품의 앞뒤 관계와 머리끈 위치를 별도로 그려 단순 반전하지 않는다.
- 첫 출력은 기준 원본보다 외곽선과 형태가 각지고 복고풍 8비트 캐릭터에 가까워 사용자 검수에서 반려됐다.
- 현재 PNG와 Aseprite 파일은 폐기 전 기술 구조 확인용으로만 남겨 둔다. 승인된 비도트 기준으로 대체되기 전까지 런타임에서 사용하지 않는다.
- 최신 승인 기준: `ArtSource/Concepts/Characters/chr_protagonist_male_stage_01_walk_down_concept_01.png` (256×256 RGBA 비도트 동양 채색화)

### 파일

- 편집 원본: `chr_protagonist_male_stage_01.aseprite`
- 구조 명세: `chr_protagonist_male_stage_01_animation.json`
- 대기 미리보기: `Preview/chr_protagonist_male_stage_01_idle_preview.gif`
- 걷기 미리보기: `Preview/chr_protagonist_male_stage_01_walk_preview.gif`
- Unity 대기 시트: `Assets/Art/Production/Characters/chr_protagonist_male_stage_01_idle.png`
- Unity 걷기 시트: `Assets/Art/Production/Characters/chr_protagonist_male_stage_01_walk.png`
- 재생성 도구: `Tools/Art/generate_protagonist_male_stage_01.lua`

### 남은 확인

- Unity 6000.3.10f1 배치 검증에서 대기 8개, 걷기 24개의 128×128 스프라이트 슬라이스와 Point 필터, 압축 없음, 64 PPU 설정을 확인했다.
- 실제 타일맵 위에서 1배 크기 판독성, 이동 중 발 미끄러짐과 정수 픽셀 스냅을 확인한다.
- 승인된 256×256 비도트 정면 보행 기준으로 프레임 규격과 월드 표시 크기를 먼저 확정한 뒤 네 방향 전 프레임을 새로 제작한다.

### 비도트 정면 걷기 수동 조립 키포즈

- 키포즈 스트립: `Candidates/ManualAssembly/chr_protagonist_male_stage_01_walk_down_keyposes.png`
- 개별 키포즈: `Candidates/ManualAssembly/Frames/`
- 조립 안내: `Candidates/ManualAssembly/README.md`
- 상태: `MANUAL ASSEMBLY KEYPOSES / BUILD EXCLUDED`
- 후보 01과 02는 무릎 수선천 좌우 교환, 같은 앞발 이미지 반복, 균일한 보폭과 불분명한 발 교대 때문에 모두 사용하지 않는다.
- 새 묶음은 무릎 수선천을 제거하고 접지·하강·통과를 왼발과 오른발로 명확히 나눈 256×256 독립 프레임 6장이다.
- Aseprite 수동 조립과 동작 승인 전에는 기존 Unity 출력 파일을 교체하지 않는다.

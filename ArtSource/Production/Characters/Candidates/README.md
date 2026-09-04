# 탐색 캐릭터 프로덕션 후보

## `ManualAssembly/chr_protagonist_male_stage_01_walk_down_keyposes`

- 상태: `MANUAL ASSEMBLY KEYPOSES / BUILD EXCLUDED`
- 사용자 검수 결과에 따라 무릎 수선천을 모두 제거했다.
- 좌우 발이 같은 보폭과 같은 앞발 모양으로 반복되지 않도록 접지·하강·통과 동작을 여섯 개의 독립 PNG로 분리했다.
- 기본 순서: 왼발 접지 → 왼발 하강 → 오른발 통과 → 오른발 접지 → 오른발 하강 → 왼발 통과
- 프레임 규격: 256×256px RGBA, 동일 발 기준선
- 개별 키포즈: `ManualAssembly/Frames/`
- 조립용 스트립: `ManualAssembly/chr_protagonist_male_stage_01_walk_down_keyposes.png`
- 조립 안내: `ManualAssembly/README.md`
- GIF는 추천 순서의 확인용일 뿐 완성 애니메이션으로 승인하지 않는다.

## `chr_protagonist_male_stage_01_walk_candidate_02`

- 상태: `REJECTED / DO NOT USE / BUILD EXCLUDED`
- 수정 기준: 사용자 검수에서 확인된 정면 수선천 좌우 교환과 부자연스러운 보행 주기
- 정면 수선천 고정: 화면 왼쪽 바짓단은 청회색, 화면 오른쪽 바짓단은 황토색
- 정면 보행 순서: 왼발 접지·하강·통과 → 오른발 접지·하강·통과
- 시트: `chr_protagonist_male_stage_01_walk_candidate_02.png`
- 미리보기: `Preview/chr_protagonist_male_stage_01_walk_candidate_02.gif`
- 메타데이터: `chr_protagonist_male_stage_01_walk_candidate_02.json`
- 규격, 방향 행, 프레임 수, 피벗과 필터는 후보 01과 동일하다.
- 후보 02에서는 정면 행만 교체했으며 좌·우·상 방향은 후보 01의 프레임을 유지한다.

## `chr_protagonist_male_stage_01_walk_candidate_01`

- 상태: `REJECTED / DO NOT USE / BUILD EXCLUDED`
- 기준: `ArtSource/Concepts/Characters/chr_protagonist_male_stage_01_walk_down_concept_01.png`
- 용도: 1~20레벨 남성 주인공의 필드 걷기 애니메이션 검토
- 형식: 1536×1024px RGBA PNG 스프라이트 시트
- 셀: 256×256px
- 방향 행: `down → left → right → up`
- 프레임: 방향당 6프레임, 총 24프레임
- 재생: 8fps, 반복
- 피벗 후보: 정규화 `(0.5, 0.105469)`, 발밑 중앙
- 필터 후보: Bilinear
- 접지 그림자: 포함하지 않음
- 미리보기: `Preview/chr_protagonist_male_stage_01_walk_candidate_01.gif`
- 메타데이터: `chr_protagonist_male_stage_01_walk_candidate_01.json`
- 생성 방향 원본: `Source/`의 방향별 3×2 그리드

### 검수 상태

- 24개 셀의 크기, 행 순서, 실제 알파, 발 기준선과 루프 프레임 수를 자동 검사한다.
- 후보 01은 정면 수선천 색이 프레임에서 좌우로 바뀌고 보행의 좌우 교대가 불분명해 반려됐다.
- 후보 02도 사용자 동작 검수에서 같은 앞발 이미지의 연속, 균일한 보폭, 좌우 순서 오류가 확인되어 반려됐다.
- 다음 검수는 `ManualAssembly/`의 독립 키포즈를 Aseprite에서 수동 조립한 결과를 기준으로 한다.

이 폴더는 Unity `Assets` 밖에 있으며 현재 빌드에 포함되지 않는다.

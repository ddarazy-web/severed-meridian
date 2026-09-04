# 정면 걷기 수동 조립용 키포즈

## 용도

- 상태: `MANUAL ASSEMBLY KEYPOSES / BUILD EXCLUDED`
- 기준 캐릭터: `ArtSource/Concepts/Characters/chr_protagonist_male_stage_01_walk_down_concept_01.png`
- 규격: 프레임당 256×256px RGBA, 투명 배경
- 피벗 후보: 정규화 `(0.5, 0.105469)`, 발밑 중앙
- 무릎 수선천: 사용하지 않음
- 주의: 자동 생성된 완성 루프가 아니라 Aseprite에서 순서와 타이밍을 직접 조립하기 위한 독립 키포즈다.

## 프레임

1. `01_left_contact`: 왼발 전방 접지
2. `02_left_recoil`: 왼발 접지 뒤 하강, 보폭 축소
3. `03_right_passing`: 오른발이 몸 아래를 통과하며 무릎 상승
4. `04_right_contact`: 오른발 전방 접지
5. `05_right_recoil`: 오른발 접지 뒤 하강, 보폭 축소
6. `06_left_passing`: 왼발이 몸 아래를 통과하며 무릎 상승

개별 PNG는 `Frames/`에 있으며, `chr_protagonist_male_stage_01_walk_down_keyposes.png`는 같은 순서를 한 줄로 배치한 1536×256px 스트립이다. 반대 발 프레임은 허리 장비와 상체를 고정하고 하체만 반대 동작으로 구성했다.

## Aseprite 조립

1. 스트립을 스프라이트 시트로 불러올 때 셀 크기를 256×256px, 행 우선 순서로 지정한다.
2. 기본 배열은 `01 → 02 → 03 → 04 → 05 → 06`이다.
3. 시작은 약 8fps 또는 프레임당 125ms를 권장하지만, 접지 프레임은 조금 길게 하고 통과 프레임은 짧게 조정할 수 있다.
4. `Preview/chr_protagonist_male_stage_01_walk_down_suggested_order.gif`는 순서 확인용이며 최종 승인 애니메이션이 아니다.
5. 조립 후 발 미끄러짐, 머리 높이 변화, 등짐과 칼집의 흔들림을 별도로 보정한다.

이 디렉터리는 Unity `Assets` 밖에 있어 빌드에 포함되지 않는다.

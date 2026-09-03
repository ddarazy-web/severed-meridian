# 명 중기 중국 스토리 참고 지도

## 목적

- Servered Meridian의 장거리 이동, 세력권, 사건 배치와 스토리 동선을 검토하기 위한 중국 전역 개략도다.
- 기준 시점은 명 중기 중에서도 가정 연간 전반, 대략 1530년경으로 잡았다.
- 현대 성(省) 구분이 아니라 명대의 양경십삼성 체계를 우선한다.
- 요동도사는 일반 성과 구분되는 북동 변경 군정 구역으로 별도 표기한다.

## 파일

- `01-ming-mid-china-story-reference-map.svg`: 수정·확장의 기준 원본
- `01-ming-mid-china-story-reference-map.png`: 2400×1600 열람용 렌더
- `tools/generate-ming-reference-map.js`: SVG 재생성 스크립트

## 범례와 사용 규칙

- 수도: 북경 순천부, 남경 응천부
- 성도: 각 포정사·성급 행정 구역의 중심 도시. 귀주는 당시 표현을 고려해 `귀주선위사`로 표기했다.
- 도시: 대도시 / 중소도시 / 소도시·관문으로 시각적 크기를 구분한다.
- 산세: 큰산 / 중간산 / 작은산 / 산맥을 서로 다른 크기와 연속 기호로 구분한다.
- 물길: 황하, 장강, 회수, 서강과 대운하는 도시 이동 경로를 읽기 위한 보조 정보다.
- 경계·해안선·산줄기·도시 위치는 정밀 역사 GIS가 아니라 스토리 기획용 근사다. 실제 사건의 이동 거리나 행정 관할이 중요해지는 시점에는 해당 지역의 연도별 사료를 별도로 확인한다.

## 행정 구획 기준

양경십삼성: 북직례, 남직례, 산동, 산서, 하남, 섬서, 절강, 강서, 호광, 사천, 복건, 광동, 광서, 운남, 귀주.

## 참고 근거

- Library of Congress, *Da Ming yu di tu*: 가정 연간에 편찬·간행된 명대 지도책으로 두 직례와 13개 성의 지도를 수록한다. https://www.loc.gov/item/2002626776/
- Academia Sinica Digital Atlas, *Da Ming yu di tu*: 명대 행정 구획, 산악, 도성과 도시 기호의 표현 방식을 설명한다. https://digitalatlas.asdc.sinica.edu.tw/digitalatlasen/map_detail.jsp?id=A103000001
- China Historical GIS: 역사 지명과 행정 단위 교차 확인용 데이터베이스. https://chgis.fas.harvard.edu/

## 상태 및 빌드 제외

- 상태: `CONCEPT REFERENCE / APPROVED BASELINE`
- 이 폴더는 `ArtSource/Concepts/` 아래에 있으며 Unity `Assets/`에 포함하지 않는다.
- 지도 안의 모든 문자는 참고 문서용이다. 런타임 지도 UI로 전환할 때는 텍스트를 이미지에 굽지 않고 현지화 가능한 UI 레이어로 분리한다.

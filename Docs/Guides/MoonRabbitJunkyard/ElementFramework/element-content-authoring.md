# 요소 제작 원본과 MemoryPack 사용

## 원본 위치

- `Assets/Data/Elements/Definitions/`: 종류별 ElementDefinitionAsset 18개. 이름·고유 ID·크기·내구도/충전·피해·미션·공급·행동과 기획 출처를 관리한다.
- `Assets/Data/Elements/DefaultElementCatalog.asset`: 정의 목록과 표현 원본을 연결한다.
- `Assets/Data/Elements/DefaultElementVisuals.asset`: 상태별 이미지 주소·크기·위치·회전·정렬·시트 프레임·효과 프레임을 관리한다.

규칙 구현 자체, 레벨의 배치와 개별 실행 상태는 정의 SO와 구분한다. 이미지 픽셀은 기존 아틀라스 리소스로 유지하며 MemoryPack에는 주소와 표현 설정만 넣는다.

## 메뉴와 내보내기

1. `MATCH > 데이터 > 확정 요소 제작 원본 생성`: 기존18개를 초기 제작 에셋으로 생성한다. 이미 있는 정의·표현의 편집값은 덮어쓰지 않는다. 미연결 제작 레벨에는 기본 카탈로그 참조만 연결한다.
2. 원본 Inspector나 기존 카탈로그 편집 도구에서 데이터를 수정한다. 실제 기획 문서 경로를 Planning Document에, 근거 항목을 Planning Section에 기록한다.
3. `MATCH > 데이터 > 요소 및 레벨 MemoryPack 갱신`: 데이터 파일만 갱신한다. 게임 빌드 및 이미지 번들 빌드는 실행하지 않는다.

출력은 `Assets/Data/ElementPacks/default-content.bytes`와 기존 `Assets/Data/LevelPacks/levels-*.bytes`다. 런타임 Addressables 주소는 `Elements/default-content`와 `Levels/levels-<구간 시작 번호>`다. 레벨 팩은 기존50레벨 구간을 유지하고, 해당 구간에서 사용하는/생성 가능한 정의를 포함한다.

콘텐츠 팩에는 정의·표현 메타데이터를 담는다. 미사용 장애물의 이미지·프리팹까지 로드하지 않는다. 실제 아틀라스·프리팹의 준비는 기존 레벨별 리소스 계획과 풀을 사용한다.

레벨 에디터의 에셋 입력은 현재 SO 편집값을 사용한다. MemoryPack 입력은 규칙과 이미지 설정 모두 마지막으로 내보낸 파일을 사용한다. 수정 후 두 모드가 다르게 보이면 MemoryPack 갱신 메뉴로 내보낸다.

표현 SO의 효과 프레임에는 다른 효과를 중첩하지 않는다. Unity 원본은 유한한 제작 구조이고, 실행에는 기존 표현 DTO로 변환하여 전달한다.

## 빌드 연결

기존 LevelPackBuild의 플레이어 빌드 전 변환과 LevelPackAddressablesBuilder의 Addressables 빌드 전 변환이 두 데이터 팩을 갱신한다. 실제 빌드를 수행할 때 별도의 수동 변환은 필요 없다. 빌드 시점에 올바른 제작 원본은 존재해야 한다.

LevelDefinition, ElementDefinitionAsset, ElementCatalogAsset, ElementVisualCatalogAsset 원본은 Addressables 직접 등록에서 제외한다. 씬·프리팹·Resources·Addressables 폴더 의존성에 원본이 남으면 변환/빌드를 거절한다. 게임 화면은 콘텐츠 팩의 표현과 레벨 팩의 규칙 값을 사용한다. 구형 레벨 팩을 읽는 호환 경로도 유지한다.

여러 출시 레벨이 각자 카탈로그를 참조하면 기본 카탈로그와 함께 콘텐츠 팩으로 합친다. 같은 정의 ID의 설정, 시각 키의 내용 또는 ID의 표현 연결이 서로 다르면 거절한다. 값이 다른 새 종류는 고유 ID와 필요 시 고유 시각 키로 분리한다.

## 신규 블록·장애물 절차

1. [기획 기준](../../../Contents/MoonRabbitJunkyard/14_퍼즐요소데이터.md)에 따라 기획을 먼저 확정한다.
2. `Assets > Create > MATCH > 요소 정의`로 제작 원본을 생성하고 Definitions 아래에서 관리한다. 고유 ID를 지정하고 필요한 프로필만 활성화한다. 기획 문서 경로는 실제 존재하는 `Docs/Contents/.../*.md`여야 한다.
3. 카탈로그의 Definitions 목록에 원본을 등록한다. 표현 카탈로그에 ID 별칭과 상태별 그림·프레임을 등록한다. 이미지 공유는 명시적인 시각 키 연결로 지정한다.
4. 기존 행동에 해당하지 않는 기능은 새 행동 구현·등록·검증을 먼저 완료한다. 이름만 추가하고 다른 기존 행동으로 대신하지 않는다.
5. 레벨 에디터에서 배치와 상태별 표현을 확인하고 MemoryPack을 갱신한다. 배치된 레벨뿐 아니라 공급이나 매칭으로 생성될 종류도 확인한다.

출처 누락·잘못된 프로필·중복 ID·정의/표현 누락·충돌은 오류로 표시한다. 문서의 의미와 수치를 자동 해석하지 않으므로 실제 기획 대조는 제작자의 책임이다. 초기 생성 도구는 미확정 신규 콘텐츠를 생성하지 않는다.

`Docs/Contents`는 기존 Git ignore 대상이다. 다른 작업 환경에서도 변환하려면 출처 문서를 함께 제공해야 한다. 문서의 Git 관리 방식은 이번 변경에서 유지했다.

## 검사

프로젝트 Editor를 닫은 뒤 저장소 루트에서 실행한다.

```powershell
./Tools/Testing/ProjectTests.ps1 -Action Run -Method Elements.Editor.ElementContentAuthoringVerification.Run
./Tools/Testing/ProjectTests.ps1 -Action Run -Method Elements.Editor.ElementContentAuthoringVerification.RunLoad
```

MemoryPack ECPK 버전1은 새 콘텐츠 팩 계약이다. 기존 레벨 팩2/구형 팩1 계약은 변경하지 않았다. DTO의 필드 추가·순서 변경은 콘텐츠 팩 버전과 호환 정책 검토가 필요하다.

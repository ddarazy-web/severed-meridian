# JSON 제작 데이터 전환 — 5단계 계획

> 후속 통합 계획: [게임·Windows 제작 도구 분리 — 6단계](2026-10-08-game-and-authoring-products-plan.md). 앞으로의 실행 순서는 통합 계획을 따르며, 이 문서의 JSON 저장 계약은 참고 기준으로 유지한다. 두 계획의 단계 수를 합산하지 않는다.

> 상태: 2026-10-08에 작성한 초기 데이터 계약 계획이다. 실행은 위 6단계 통합 계획으로 대체되었으며 2026-10-09 해당 구조·데이터 검증을 완료했다. 아래 단계/제외 범위는 초기 제안의 기록이며 최신 완료 범위는 통합 계획과 6단계 검증 기록을 따른다.
> 실행 담당: 단계별 상세 계획 확정 후 superpowers:executing-plans로 직접 진행한다. 이번 요청은 계획 작성이며 소스·데이터 변환·빌드·커밋·푸시를 수행하지 않는다.

**Goal:** 레벨·요소·튜토리얼·모양 프리셋의 제작 원본을 Unity와 독립적인 JSON으로 저장하고, Unity에서 읽어 기존 MemoryPack 배포·플레이 흐름을 유지한다.

**Architecture:** JSON 문서와 ID 참조를 공통 계약으로 삼는다. Unity 에디터는 편집 세션 어댑터를 통해 문서를 다루고, 검증한 문서를 기존 실행 데이터와 MemoryPack으로 변환한다. 현재 화면 전체를 지금 이식하거나 모든 ScriptableObject 타입을 한 번에 없애지는 않는다.

**Tech Stack:** 현재 Unity/C#, JSON, 기존 MemoryPack 및 Addressables. JSON 라이브러리의 실제 설치·독립 .NET 사용 가능 여부는 1단계에서 확인한다. 신규 패키지나 어셈블리 경계를 이번 계획만으로 확정하지 않는다.

**Spec:** 사용자 요청 — JSON 제작 원본, 향후 독립 Windows 편집기, Unity 재입력과 MemoryPack 변환. 아래 범위·계약·완료 기준을 단계별 상세 계획의 공통 기준으로 사용한다.

## 1. 결론과 범위

권장안은 **JSON을 유일한 제작 원본으로 사용하고, 배포에는 MemoryPack을 유지**하는 것이다.

단순 JSON 내보내기만 추가하면 실제 편집 원본은 여전히 .asset에 남는다. 반대로 Windows 앱 이식까지 동시에 하면 UI·입력·Undo·에셋 미리보기·게임 시뮬레이션을 함께 바꾸게 된다. 이번에는 저장 계약과 파일 작업을 먼저 독립시킨다.

| 대상 | 이번 전환 방향 |
|---|---|
| 레벨 | 보드, 배치, 연결, 공급, 미션, 튜토리얼을 JSON 원본으로 전환 |
| 일반 블록·파워·장애물·덮개·바닥·장치 정의 | 기존 요소 ID와 조합 가능한 프로필을 유지하여 JSON 전환 |
| 요소 카탈로그 | 정의 에셋 참조를 정의 ID 목록으로 전환. 카탈로그별 포함 관계 보존 |
| 표현 카탈로그 | 상태·프레임·피벗·크기·효과 및 리소스 연결 정보를 JSON 전환 |
| 공통 튜토리얼·내 샘플 | 독립 JSON 문서와 ID 참조로 전환. 공유/복사 의미 유지 |
| 맵 모양 프리셋 | 활성 칸·원본 정보·장애물 사용 기록을 JSON 전환 |
| 이미지·아틀라스·프리팹·씬·오디오 | 기존 리소스로 유지. JSON에는 연결 ID 기록 |
| PopupCatalog 등 Unity 프리팹 조립 설정 | 이번 콘텐츠 전환에서 제외. 직접 프리팹 연결 설정은 별도 범위 |
| 플레이어 진행·완료 기록·아이템 재고, 시험 결과 로그 | 이번 대상 아님. 제작 데이터와 사용자 저장을 혼합하지 않음 |
| 외부 플러그인·프로젝트 설정의 ScriptableObject | 수정하지 않음 |

기획 출처가 필요한 요소는 JSON에서도 출처 문서와 절을 보존한다. 미확정 장애물이나 새로운 규칙을 임의로 생성하지 않는다.

## 2. 확인한 현재 구조

- LevelDefinition은 ScriptableObject이며 기존/신형 레벨 스키마와 ElementCatalogAsset 참조를 가진다.
- LevelAssetOperations는 Assets/Data/Levels에 .asset을 생성한다. 이름 변경·복제·선택·목록도 연결되어 있다.
- 배치/공급/튜토리얼 편집은 SerializedObject, SerializedProperty, Undo를 활용한다. 파일 저장과 함께 일괄 폐기하면 위험이 크다.
- ElementDefinitionAsset은 PackedElementDefinition과 기획 출처를 보유한다. 기존 프로필과 검증을 재사용할 수 있다.
- ElementCatalogAsset은 정의 에셋 목록과 시각 카탈로그를 직접 참조한다.
- ElementVisualCatalogAsset은 깊은 재귀 직렬화 문제를 피하도록 유한한 제작 구조로 분리되어 있다.
- LevelTutorialDefinition은 공통 flow 에셋, 레벨별 bindings, completionId, 이전 완료 이관 정보를 가진다. 실행 팩에서는 공유 구성이 해석되므로 **실행 팩을 JSON으로 풀기만 해서는 제작 의도가 복구되지 않는다.**
- TutorialFlowDefinition/TutorialUserSampleDefinition 및 LevelShapePreset도 ScriptableObject다.
- LevelPackBuild.Generate는 AssetDatabase에서 레벨을 검색한다. ElementContentPackBuild도 에셋 카탈로그와 레벨을 조회한다.
- 현재 레벨 팩은 **번호 구간별 50레벨**이며 튜토리얼 확장 포맷과 이전 버전 읽기가 존재한다.
- 팩 로드도 메모리 안에서 LevelDefinition을 만든다. “SO 파일 원본 제거”와 “실행 중 SO 인스턴스 완전 제거”는 별개다.

주요 조사 파일:

- Assets/Scripts/Features/Levels/Data/LevelDefinition.cs
- Assets/Scripts/Features/Levels/Editor/Persistence/LevelAssetOperations.cs
- Assets/Scripts/Features/Levels/Editor/Persistence/LevelPackBuild.cs
- Assets/Scripts/Features/Levels/Data/LevelPackCodec*.cs
- Assets/Scripts/Features/Elements/Data/ElementDefinitionAsset.cs
- Assets/Scripts/Features/Elements/Data/ElementCatalogAsset.cs
- Assets/Scripts/Features/Elements/Data/ElementVisualCatalogAsset.cs
- Assets/Scripts/Features/Elements/Editor/ElementContentPackBuild.cs
- Assets/Scripts/Features/Tutorial/Data/LevelTutorialDefinition.cs
- Assets/Scripts/Features/Tutorial/Data/TutorialFlowDefinition.cs
- Assets/Scripts/Features/Tutorial/Editor/LevelTutorialEditorPanel*.cs
- Assets/Scripts/Features/ShapeCatalog/Editor/Data/LevelShapePreset.cs
- Assets/Scripts/Systems/Popup/Runtime/PopupCatalog.cs

이는 관련 타입 조사이며 모든 데이터 파일의 전수 이관 목록은 아니다. 실제 파일 수와 사용자 제작 자료는 1단계에서 목록으로 확정한다.

## 3. 목표 구조와 저장 계약

    현재 Unity 편집기              향후 Windows 편집기
            └──── 같은 JSON 문서 계약과 파일 저장 ────┘
                                ↓
                       구조·ID·참조 검증
                                ↓
                      Unity 입력 어댑터
                                ↓
                 공유 구성 해석·실행용 값 변환
                                ↓
                  MemoryPack → Addressables → 게임

Windows 앱의 UI 기술은 지금 결정하지 않는다. 문서 모델·저장·검증은 공유할 수 있게 만들지만 현재 EditorWindow, AssetDatabase, Unity Undo를 외부 앱에서 그대로 실행할 수 있다는 뜻은 아니다. 게임 미리보기의 규칙·시뮬레이션 의존성도 추후 별도 조사한다.

### 제작 파일 배치안

Assets 밖 프로젝트 루트의 ContentData를 권장한다. 게임에 제작 JSON이 직접 포함되는 것을 피하고 폴더를 외부 앱에 전달하기 쉽다.

    ContentData/
      project.json
      levels/<문서이름>.json
      elements/<요소ID>.json
      catalogs/<카탈로그ID>.json
      visuals/<표현카탈로그ID>.json
      tutorials/flows/<공통구성ID>.json
      tutorials/samples/<샘플ID>.json
      shapes/<모양ID>.json

    Assets/Data/LevelPacks/*.bytes       기존 배포 산출물
    Assets/Data/ElementPacks/*.bytes     기존 배포 산출물

- 레벨 1개/정의 1개/흐름 1개당 파일 1개. 상태 프레임은 관련 표현 정의에 묶는다.
- 50레벨 묶음은 MemoryPack 배포 규칙이며 JSON 편집 파일을 50개씩 묶지 않는다.
- project.json은 형식·프로젝트 식별·기본 카탈로그 등을 정의한다. 파일 목록을 불필요하게 중복 관리하지 않는다.
- 위 경로는 제안이며 1단계 확정 전 폴더·파일을 미리 만들지 않는다.

### 필수 계약

1. 문서 종류, schemaVersion, 안정적인 id를 둔다. 표시 이름·파일명·레벨 번호와 참조 ID를 구분한다.
2. 기존 요소 ID, 배치 instanceId, 단계/조건 authoringId, 파라미터 key, 생성 결과 이름, 학습 completionId는 각각 원래 의미대로 보존한다.
3. ID가 없는 공통 구성·프리셋에는 최초 이관 때 한 번만 ID를 부여한다. 옛 에셋 GUID→문서 ID 대응표로 재실행 시 중복 발급을 막는다. .meta를 외부 앱의 필수 계약으로 삼지 않는다.
4. 복제한 문서 ID는 새로 만들되 학습 완료 ID의 유지 여부는 별도 정책이다. 완료 기록을 임의 초기화하지 않는다.
5. 좌표는 기존 저장 0 기준, 화면 1~9 표시를 유지한다. 색·방향·프로필은 명시적 키로 표현한다.
6. enum 숫자 순서와 C# 타입명을 외부 저장 계약으로 삼지 않는다. 명시적인 문자열 키 변환표를 둔다.
7. 누락/null/빈 목록/알 수 없는 버전·필드 정책을 정의한다. 상위 버전의 모르는 데이터를 버리고 덮어쓰지 않도록 저장을 차단한다.
8. 공통 튜토리얼은 flowId와 레벨별 binding을 보존한다. 제작 JSON에서 매번 펼쳐 복사하지 않고 팩 생성 때 해석한다.
9. 리소스 논리 ID와 Unity 측 경로/주소 매핑을 분리한다. 현재 시각 경로는 이행 중 보존할 수 있지만 외부 앱이 Unity GUID를 해석해야 문서를 저장할 필요는 없게 한다. Windows 미리보기 이미지 패키지는 후속 범위다.
10. JSON용 문서 모델은 UnityEngine.Object/UnityEditor에 의존하지 않는다. 기존 런타임 DTO가 Unity 타입·속성에 의존하면 경계에서 명시적으로 변환한다.
11. 기존 MemoryPack DTO와 필드 순서를 JSON 계약으로 그대로 공개하지 않는다. 내부 값 재사용과 외부 계약은 구분한다.
12. UTF-8, 일정한 들여쓰기·속성 순서·줄바꿈을 사용한다. 공급·튜토리얼처럼 순서가 중요한 목록은 임의 정렬하지 않는다.
13. 같은 폴더의 임시 파일에 저장·검증한 뒤 교체하고 이전 정상 파일을 복구할 수 있게 한다. 외부 수정은 읽은 내용의 해시로 감지하고 조용히 덮어쓰지 않는다.

### 이행 중 SO 사용

JSON을 **저장하지 않는 임시 ScriptableObject 편집 세션**으로 투영하는 것은 허용한다. 현재 Undo·SerializedProperty·보드 편집 기능을 유지하기 위한 Unity 전용 어댑터다.

- 저장은 편집 값을 JSON 문서로 돌려 기록한다.
- JSON을 읽을 때마다 영구 .asset을 만들거나 자동 동기화하지 않는다.
- 프로젝트 단위로 제작 원본 모드를 명확히 선택한다. 같은 문서를 JSON과 SO 양쪽에서 동시에 수정하지 않는다.
- 임시 객체·도메인 재로드 복구 내용은 캐시일 뿐 원본이 아니다.
- Windows 앱은 순수 문서 모델과 저장 서비스를 사용한다. Unity 임시 SO 어댑터는 가져가지 않는다.

## 4. 권장 5단계

5개는 큰 완료 구간이다. 단계 안의 작업 묶음은 순차 진행하되 매번 별도 단계 번호를 늘리지 않는다. 상세 파일·테스트 계획은 해당 단계 시작 시 작성한다.

### 1단계 — 저장 규격·참조 규칙·이관 기반

- [ ] 실제 에셋과 참조 그래프, 구형 스키마, 기획 출처를 조사하고 목록을 확정한다.
- [ ] JSON 모델·ID·참조·버전·경로·오류 정책을 확정한다. 실제 설치된 JSON 도구와 독립 .NET 사용 가능성을 검증한다.
- [ ] 기존 원본을 건드리지 않는 내보내기/읽기/변환을 만든다. 공유 정보를 잃는 실행 팩 경유 내보내기는 금지한다.
- [ ] 레벨·각 요소 프로필·표현 프레임·공통 튜토리얼·샘플·프리셋 표본을 왕복 검사한다.
- [ ] Unity 없는 검증 경로에서 JSON 모델·파싱·참조 검사가 실행되는지 확인한다.

**완료:** SO→JSON→문서→기존 입력 변환의 의미가 같고 ID·공유 정보·시각 상태·기획 출처가 보존된다. 현재 편집기의 기본 저장 방식은 아직 바꾸지 않는다.

### 2단계 — 블록·장애물·표현·카탈로그 JSON 편집

- [ ] 정의·표현·카탈로그 JSON 저장소와 편집 세션을 연결한다.
- [ ] 등록/수정/삭제/검색·기획 출처·중복 ID·미해결 참조 검사를 연결한다.
- [ ] 현재 요소 프로필과 시각 상태를 JSON에서 읽어 보드 미리보기와 게임 입력에 반영한다.
- [ ] 기존 SO와 JSON 경로는 명시적 이행 모드로 구분한다. 자동 최신 파일 선택·양방향 동기화는 만들지 않는다.

**완료:** JSON의 내구도·표현·공급 설정을 저장·재시작해도 유지하고 기존 규칙과 같은 결과를 낸다. 없는 정의/리소스/기획 근거는 구체적으로 안내된다.

### 3단계 — 레벨·튜토리얼·모양의 편집 저장 전환

같은 단계 안에서 A/B 두 작업 묶음으로 진행한다.

**A. 레벨 파일 작업**
- [ ] 목록/열기/새로 만들기/저장/다른 이름 저장/복제/이름 변경/삭제/최근 항목을 JSON 기준으로 연결한다.
- [ ] 에셋 ObjectField 대신 데이터 목록·파일 열기를 사용할 수 있게 한다.
- [ ] 미저장 표시·창 닫기·Undo/Redo·도메인 재로드·외부 수정 충돌을 처리한다.

**B. 공유 데이터와 게임 테스트**
- [ ] 공통 튜토리얼·레벨별 값·독립 복사·내 샘플·모양 등록을 JSON에 연결한다.
- [ ] 참조 ID·완료 ID·생성 연결·고정 공급 순서를 보존한다.
- [ ] 현재 편집 값의 게임 플레이·논리 검사·배치 시험을 연결한다.

**완료:** 기존 에디터에서 제작→저장→닫기→재열기→튜토리얼 연결→게임 테스트를 할 수 있고 원본은 JSON이다. 영구 SO를 생성하지 않는다.

### 4단계 — JSON → MemoryPack 배포 경로

- [ ] LevelPackBuild/ElementContentPackBuild의 입력 탐색을 선택한 제작 저장소로 통일한다.
- [ ] 공통 흐름·ID 참조를 해석하고 기획/데이터 검증 뒤 기존 실행 DTO로 변환한다.
- [ ] 레벨 번호 1~50, 51~100 등 기존 구간·주소 체계·이전 팩 읽기를 유지한다.
- [ ] 레벨과 요소 팩을 동일한 콘텐츠 스냅샷에서 만든다. 생성 중 원본 변경 시 중단한다.
- [ ] 모든 산출물 선검증 후 교체하고 중간 실패 시 이전 정상 팩으로 복구한다.
- [ ] JSON/옛 SO/임시 객체가 런타임 리소스 의존성에 포함되지 않도록 검사한다.
- [ ] 에디터 게임 입력을 JSON 편집 데이터 / MemoryPack으로 구분하고 오래된 팩을 표시한다.

**완료:** 기존/JSON 입력의 실행 결과가 동등하다. 50/51·100/101 경계, 결번, 중복 번호, 정의 충돌, 튜토리얼을 검증한다. 플레이어/번들 빌드 없이 팩 변환·등록·Editor 로딩까지 검증한다.

**중간 단계 제한:** 4단계 전에는 JSON 모드의 배포 변환을 차단하고 현재 편집 데이터 테스트만 허용한다. 옛 SO를 몰래 읽어 JSON 수정이 빠진 팩을 만드는 일이 없어야 한다. 기존 SO 모드의 기존 변환 경로는 유지한다.

### 5단계 — 전체 이관·기본값 전환·잔여 경로 정리

- [ ] 원본 목록·백업·GUID↔문서 ID 매핑·참조 보고서를 확보한다.
- [ ] 전체 제작 데이터를 임시 출력에 변환하고 수량·ID·참조·대표 시뮬레이션 결과를 비교한다.
- [ ] 검사가 통과한 스냅샷을 JSON 원본으로 채택하고 기본 편집/변환 입력을 전환한다.
- [ ] 일반 제작 메뉴의 SO 생성·저장을 제거하고 명시적 구형 가져오기만 남긴다. 기존 SO는 검증·보관 뒤 활성 원본에서 제외한다.
- [ ] 씬·프리팹·최근 항목·직접 참조를 확인한다. 엔진용 SO와 런타임 임시 SO를 잘못 삭제하지 않는다.
- [ ] 재시작·다른 폴더 복사·외부 편집 후 Unity 재읽기를 확인한다.
- [ ] Windows 앱에서 재사용할 모듈과 여전히 Unity 전용인 UI·입력·시뮬레이션 경계를 인계한다.

**완료:** 제작 원본이 JSON으로 일원화되고 옛 SO가 몰래 우선되는 경로가 없다. 기존 게임·튜토리얼·팩 로딩을 유지하며, 재이관 시 중복 생성이 없고 실패/취소 시 복구 가능하다.

## 5. 위험과 검증

| 위험 | 담당 단계 / 필수 검사 |
|---|---|
| 공유 튜토리얼·기획 출처 소실 | 1·3: 제작 정보 왕복, 공유 수정/독립 복사 의미 비교 |
| enum/GUID/절대경로 의존 | 1: 명시적 키 변환, Unity 없는 로딩, 폴더 이동 |
| 저장 실패·외부 수정 덮어쓰기 | 1·3: 실패 주입, 정상 파일 보존, 해시 충돌 거절 |
| 레벨과 요소 팩 버전 불일치 | 4: 동일 스냅샷·전체 선검증·교체 복구 |
| 기본값 처리로 동작 변화 | 1·2·5: 명시/누락/null 구분, 기본 카탈로그 보충 정책 비교 |
| Undo/공통 흐름 수정 유실 | 3: 재로드·Undo/Redo·미저장·공유 사용자 영향 |
| 상위 JSON 버전을 구버전이 덮어씀 | 1: 미지원 상위 버전 저장 차단 |
| 파일명 변경이 참조·완료 기록 변경 | 3·5: 문서 ID/완료 ID/배치 ID 별도 유지 |
| 정의 수 증가 시 ID 충돌·조회 비용 | 2·4: 시작 시 ID 인덱스, 중복 검사. 미래 콘텐츠는 임의 생성하지 않음 |

## 6. 전체 완료 조건과 제외 항목

- 실제 제작 JSON을 Unity 밖의 도구가 읽고 다시 저장할 수 있다.
- Unity에서 재입력하여 레벨·요소·튜토리얼을 편집하고 같은 규칙으로 시험할 수 있다.
- JSON→MemoryPack 후 기존 로더와 같은 결과를 낸다.
- 기획 출처·공통 구성·리소스 연결·학습 완료 기록의 의미가 보존된다.
- 저장 충돌/손상/버전 불일치를 숨기지 않고 정상 파일을 보존한다.
- 게임에는 필요한 팩과 리소스만 포함시키는 기존 정책을 유지한다.

이번에 하지 않는 것:
- Windows 독립 앱의 화면 구현·설치 프로그램·프레임워크 선정.
- 게임 규칙 전면 재작성, MVVM 의무 도입, 모든 asmdef 분리.
- 프로젝트의 모든 ScriptableObject 제거.
- 모바일에서 제작 JSON을 매번 직접 파싱하는 방식으로 변경.
- 요청 없는 HTML 매뉴얼 갱신.
- 플레이어 빌드·번들 빌드·커밋·푸시.

## 7. 진행 운영

- 먼저 이 5단계 방향을 검토한다. 이번 문서는 구현 완료를 의미하지 않는다.
- 착수할 때 1단계 상세 계획서와 목표/완료 조건을 별도로 작성한다. 모든 후속 단계의 세부 구현 문서를 미리 만들지 않는다.
- 한 단계 종료 시 변경·검증·제약을 보고하고 다음 단계 계획·목표·복사 가능한 실행문을 제시한다.
- 3단계가 가장 크므로 A/B 작업 묶음으로 진행하되 하나의 단계로 관리한다.
- 원본 전환과 삭제를 구분한다. 검증되지 않은 이관 결과를 기존 원본에 덮어쓰지 않는다.
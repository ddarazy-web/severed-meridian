# 레벨 MemoryPack 배포와 테스트

## 결정

레벨 `.asset`은 편집 원본으로 유지하고, 배포 시에는 MemoryPack 1.21.4로 변환한 바이너리만 Addressables에 포함한다. 사용자 결정에 따라 레벨 번호 구간은 50개로 고정한다. `1~50`, `51~100`, `101~150` 순이며 번호가 비어 있어도 뒤 구간으로 밀지 않는다.

JSON 문자열을 바이너리 안에 감싸지 않는다. 보드·블록·장애물·덮개·먼지·중력/경로·발전기 연결·공급·회수 부품·미션의 저장 필드를 직접 직렬화한다. Unity 원본의 필드명과 `.meta`/GUID는 유지한다.

## 파일과 로딩

| 용도 | 위치/주소 |
|---|---|
| 배포용 원본 | `Assets/Data/Levels/` |
| 생성 파일 | `Assets/Data/LevelPacks/levels-000001.bytes` 등 |
| Addressables 주소 | `Levels/levels-000001`, `Levels/levels-000051` 등 |
| 그룹 | `Level Packs` · Pack Separately |

다른 폴더의 작업용 레벨과 검사 fixture는 자동 배포하지 않는다. 배포할 원본은 위 원본 폴더에 둔다. 현재 로컬 번들 방식이며 원격 호스팅은 별도 구성이다. `Resources` 폴더에는 넣지 않는다.

런타임에서는 `await LevelPackLoader.LoadAsync(levelNumber)`를 사용한다. 필요한 번호 구간의 TextAsset을 Addressables로 읽고 메모리의 `LevelDefinition`으로 복원한다. 기존 게임 로직은 이 메모리 객체를 사용한다. 원본 ScriptableObject 에셋 파일을 배포하는 것은 아니다. 로더는 번들 핸들을 해제하며, 반환한 메모리 객체는 호출자가 사용 후 `Destroy`해야 한다. 현재는 요청마다 읽는 방식이며 장기 캐시는 두지 않았다.

## 에디터 사용

1. 플레이 테스트/초기 보드 화면의 **레벨 입력**에서 **에셋** 또는 **MemoryPack**을 선택한다.
2. **에셋**은 편집 중인 현재 데이터를 사용한다.
3. **MemoryPack**은 해당 레벨 번호가 들어 있는 마지막 생성 `.bytes` 파일을 읽는다. 원본 수정은 자동으로 반영하지 않는다.
4. 수정한 내용을 반영하려면 **MemoryPack 갱신**을 누른 뒤 시험을 다시 구성한다.
5. 여러 레벨 시험 화면에도 **레벨 입력**과 **MemoryPack 갱신**이 있다.

에디터의 MemoryPack 선택은 생성된 바이너리 파일 자체를 검사한다. Addressables 번들까지 다시 만드는 버튼은 아니다. 실제 번들 로드는 런타임 로더와 별도 검증으로 확인한다. 팩이나 레벨이 없거나 파일이 손상되면 오류를 표시하며 에셋 입력으로 몰래 대체하지 않는다. 갱신은 현재 메모리에 로드된 원본 데이터를 변환하되 원본 에셋을 자동 저장하지 않는다. 원본 저장은 기존 저장 버튼을 사용한다.

## 빌드

- 플레이어 빌드 전 `LevelPackBuild`가 변환하고 기존 `BoardAtlasContentBuild` 단계에서 Addressables 콘텐츠를 빌드한다.
- Addressables의 활성 빌더는 `Level MemoryPack + AssetBundles`다. 이 빌더를 통한 Addressables 단독 빌드도 먼저 MemoryPack을 갱신한다.
- 배치 실행은 `-executeMethod Levels.Editor.LevelPackBuild.Prepare`를 사용한다. 생성·콘텐츠 빌드 후 Unity 프로세스를 종료하므로 작업 중인 에디터에서 직접 호출하지 않는다.
- 원본 `LevelDefinition`의 직접 Addressables 등록은 제거한다. 씬·프리팹·Resources·Preloaded Assets·Addressables 폴더의 간접 참조로 원본이 들어오면 경로를 표시하고 빌드를 중단한다. 해당 참조는 레벨 번호 기반 로드로 바꿔야 한다.
- 중복 번호·다른 구간 혼합·지원하지 않는 레벨 스키마는 변환을 거절한다. 원본이 없어진 구간의 오래된 생성 파일과 Addressables 항목은 제거한다.

## 포맷 유지보수와 검증

`LevelPackCodec.FormatVersion`은 바이너리 포맷 버전이다. 저장 필드의 타입·순서·개수 변경 시 갱신하고 팩을 다시 생성한다. `[MemoryPackOrder]` 순서는 배포 계약이며 런타임 계산용 속성은 `[MemoryPackIgnore]`로 제외한다. 현재는 구형 바이너리 자동 마이그레이션 대신 불일치 오류로 처리한다.

검증: `Levels.Editor.LevelPackVerification.Run`, 결과: `Logs/LevelPackVerification/results.txt`. 구간 경계, 원본 전체 필드 왕복, 누락·중복·손상·버전 오류, 실제 BundledAssetProvider 로드, 에디터 두 입력 경로, 원본 보존 및 빌드 포함 방지를 검사한다. Android 대상 Addressables 콘텐츠를 빌드했다. 실제 Android 플레이어/IL2CPP 실행은 별도 검증 대상이다.

2026-09-30 검증 결과: MemoryPack 검사 32개, 기존 에셋·플레이 표시 회귀 검사 22개 통과. 현재 1개 레벨을 담은 파일은 2,467바이트다.

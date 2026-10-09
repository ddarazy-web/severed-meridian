# 6단계 JSON 이관 목록 및 참조

기본 원본: `ContentData/Candidates/main`. 26개 SO 제작 원본과 프로젝트 문서 1개를 보존했다. 기존 SO 및 메타는 삭제하지 않았다.

원본·메타 SHA-256와 GUID↔문서 ID는 `migration-report.json`, 필드별 문서 참조와 기획 출처는 `migration-references.json`에서 확인한다. 채택 후 JSON 수정은 원래 SO와 자동 동기화되지 않는다.

| 종류 | 원본 | 문서 ID |
|---|---|---|
| element | `Assets/Data/Elements/Definitions/obstacle.recovery-capsule.asset` | `element-1c095c23ee284aa9a7edef97055de51d` |
| level | `Assets/Data/Levels/Level_02.asset` | `level-60bc319684094c67bfee34e35d48a8d8` |
| element | `Assets/Data/Elements/Definitions/supply.normal.fixed.asset` | `element-750e8ab379c24e36af76571cefbb0923` |
| element | `Assets/Data/Elements/Definitions/obstacle.metal-rod-box.asset` | `element-180a89acacce40b0b47e5cb698e6b4fe` |
| element | `Assets/Data/Elements/Definitions/obstacle.generator.asset` | `element-39abc33d5bd046ed9e205e8b35e7c448` |
| level | `Assets/Data/Levels/Level_03.asset` | `level-88f4664956ec4c2bb3700f68a7cac518` |
| element | `Assets/Data/Elements/Definitions/supply.power.random.asset` | `element-a743ce838c2e48c1b056977fdddd8057` |
| element | `Assets/Data/Elements/Definitions/power.bomb.asset` | `element-7bb42b50a3394be98efa6fd4cd6713b8` |
| element | `Assets/Data/Elements/Definitions/obstacle.color-lock.asset` | `element-7b45bb5da5fe401e8b647ff3387adb53` |
| element | `Assets/Data/Elements/Definitions/power.magnet.asset` | `element-5c946f6e4c6f466cb1e5645a578a70dc` |
| element | `Assets/Data/Elements/Definitions/cover.web.asset` | `element-b914266c7529487e97723076fac4a5c3` |
| element | `Assets/Data/Elements/Definitions/power.drone.asset` | `element-938967dba657416ebf9bbc9685f04faa` |
| element | `Assets/Data/Elements/Definitions/supply.scrap.asset` | `element-a8db92745fe5401180f50807c7a75546` |
| level | `Assets/Data/Levels/Level_04.asset` | `level-9476d3b3a91c4563b712bfb753f46c98` |
| element | `Assets/Data/Elements/Definitions/supply.normal.random.asset` | `element-f91baa94af4f4a1a8f739045361221d1` |
| element | `Assets/Data/Elements/Definitions/floor.dust.asset` | `element-faac530173aa422e89c3b12b868a6105` |
| element | `Assets/Data/Elements/Definitions/supply.recovery.asset` | `element-190847babf4f4d45abac1136766295f2` |
| element | `Assets/Data/Elements/Definitions/obstacle.scrap.asset` | `element-43c5dd84c6f0476b84b5b140bd71964f` |
| level | `Assets/Data/Levels/Level_01.asset` | `level-60f267ee0c5a453bb09291a997bd660a` |
| element | `Assets/Data/Elements/Definitions/cover.mold.asset` | `element-26cc4f57499845659071b5a07f8e9798` |
| visual | `Assets/Data/Elements/DefaultElementVisuals.asset` | `visual-39d32a17fab04590841af320d59c56c7` |
| catalog | `Assets/Data/Elements/DefaultElementCatalog.asset` | `catalog-3dca08e6adb246c09c7ef6724e5d1dc9` |
| element | `Assets/Data/Elements/Definitions/power.rocket.asset` | `element-c656ce4b36ce49cfaeee9e191e6efbcd` |
| shape | `Assets/Editor/LevelShapes/001_Square.asset` | `shape-5d73912df91d4a32b90ade639c9cb0f3` |
| element | `Assets/Data/Elements/Definitions/obstacle.crate.wood.asset` | `element-cb5a4b10ab3b40bdbf0b74a41028d486` |
| shape | `Assets/Editor/LevelShapes/002_Round_Box.asset` | `shape-74d53b3c3a8c4628a226f8ff5eed25e8` |

정식 원본에는 현재 별도 공통 튜토리얼/사용자 샘플 SO가 없다. 공통 흐름·레벨별 바인딩·학습 완료 정보 보존은 별도 대표 검사 자료로 검증하며 정식 콘텐츠를 시험 자료로 늘리지 않는다.

전환 전 팩·등록 설정 복구본: `ContentData/Backups/game-authoring-stage-06/before-adoption.json`. 복구 절차와 코드/산출물 세대 제약은 같은 폴더의 README를 따른다.

## 현재 제작 흐름

1. Unity의 `MATCH > 레벨툴 씬 실행`을 연다. 복구하거나 편집 중인 작업이 없다면 채택한 JSON 작업 폴더를 연다. 기존 작업이 있으면 이를 덮어쓰지 않고 기본 작업 열기 버튼을 제공한다.
2. 레벨툴에서 JSON을 편집·저장한다. 게임 시험의 입력을 현재 편집본 JSON 또는 생성된 MemoryPack으로 선택한다. 팩 상태 확인으로 저장된 팩에 수정이 반영됐는지 확인할 수 있다.
3. 배포 입력을 갱신할 때 `MATCH > 데이터 > 요소 및 레벨 MemoryPack 갱신`을 실행한다. 이는 데이터 파일 생성·검증·주소 등록이며 Player/번들 빌드를 시작하지 않는다.
4. 구형 SO는 보관된 읽기 전용 원본이다. 새 후보 이관이나 명시적 가져오기를 사용한다. 채택한 폴더와 편집된 후보에는 재이관할 수 없다.

현재 사용하지 않는 옛 팩의 주소는 새 공개 세트에서 제외한다. 이전 물리 파일을 자동 삭제하지 않으며 게임은 현재 세대 목록과 주소만 사용한다.

# 2단계 제작 원본 → JSON 필드 대응 조사

> 2026-10-08 · 소스 및 Unity YAML 읽기 전용 조사. 이 문서는 변환·왕복·실행 검증 통과를 뜻하지 않는다.

[계획](../../Planning/project-wide/game-authoring-stage-02-plan.md) · [목표](../../Goals/project-wide/game-authoring-stage-02-goal.md)

## 조사 범위와 계약 원칙

기준 경로는 `Assets/Scripts/Features/`다. 아래 목록은 getter/실행 DTO가 아니라 실제 Unity 직렬화 필드 이름을 기준으로 한다. 공통 envelope의 `schemaVersion: 1`과 레벨 내부 기존 `schemaVersion: 1~5`는 별개다. 후자는 `data.schemaVersion` 같은 명시된 payload 위치에서 보존한다. 원본 에셋 `m_Name`은 별도 표시명으로 보존하며 문서 ID로 사용하지 않는다. 프로젝트 문서는 기존 SO가 없는 신규 aggregate다.

각 종류의 payload는 아래 대응 필드를 모두 가진다. Unity 객체 참조만 안정 문서 ID로 바꾸며, enum은 명시 문자열 키로 매핑한다. DTO를 경유하여 값을 정규화하거나 기본값을 덧붙이지 않는다. null, 빈 배열, 비활성 설정의 남은 값도 서로 구별한다. 순서 있는 목록은 원본 순서다. `MemoryPackIgnore`는 Unity 제작 저장 제외를 뜻하지 않는다.

## 실제 에셋 목록

스크립트 `.meta` GUID를 읽고 `Assets/**/*.asset`의 참조를 검색했다. 조사 시점 결과이며 이후 생성 자료는 별도 인벤토리에 기록해야 한다.

| 종류 | 실제 파일/개수 |
|---|---|
| project | 기존 SO 없음. 시험 스냅샷 프로젝트 문서 1개를 새로 구성 |
| level | `Assets/Data/Levels/Level_01.asset`, `Level_02.asset`, `Level_03.asset`, `Level_04.asset` (4개) |
| element | `Assets/Data/Elements/Definitions/` 아래 18개 (아래 목록) |
| catalog | `Assets/Data/Elements/DefaultElementCatalog.asset` (1개) |
| visual | `Assets/Data/Elements/DefaultElementVisuals.asset` (1개) |
| tutorialFlow | `TutorialFlowDefinition` GUID 참조 SO 0개 |
| tutorialSample | `TutorialUserSampleDefinition` GUID 참조 SO 0개 |
| shape | `Assets/Editor/LevelShapes/001_Square.asset`, `002_Round_Box.asset` (2개) |

요소 파일명(확장자 `.asset`): `cover.mold`, `cover.web`, `floor.dust`, `obstacle.color-lock`, `obstacle.crate.wood`, `obstacle.generator`, `obstacle.metal-rod-box`, `obstacle.recovery-capsule`, `obstacle.scrap`, `power.bomb`, `power.drone`, `power.magnet`, `power.rocket`, `supply.normal.fixed`, `supply.normal.random`, `supply.power.random`, `supply.recovery`, `supply.scrap`.

8종류 왕복 및 두 레벨의 공유 flow 검사는 영구 콘텐츠를 새로 만들지 않는 임시 테스트 fixture가 필요하다. 코드에 정의된 기본 요소/표현은 SO 인벤토리와 별도다. 명시 원본과 실행 기본값을 같은 작성 목록에 합치지 않는다.

## project

신규 문서: `kind`, envelope `schemaVersion`, 안정 `id`, 프로젝트 표시명/버전, 기본 catalog 문서 ID, 포함 문서 ID 목록, 리소스 논리 ID 매핑. 매핑은 논리 ID와 기존 Unity asset path/address를 분리한다. 원본 GUID→문서 ID 대응은 재내보내기 시 재사용하고 외부 읽기에서 `.meta`를 요구하지 않는다. project 포함 관계와 catalog 정의 목록을 서로 대체하지 않는다.

## level

원본 `Levels/Data/LevelDefinition.cs`의 모든 필드:

| 필드 | JSON 보존/참조 |
|---|---|
| schemaVersion, levelNumber, moveCount | 기존 정수 값 그대로; 문서 버전과 별개 |
| colors | RabbitColor 문자열 키의 순서 목록 |
| board | BoardDefinition |
| initialBlocks, obstacles, covers, dust | 레거시 배치 각각 보존; elements로 덮어쓰지 않음 |
| flow, connections | 보드 흐름 및 연결의 원본 값 |
| supply, recoveryParts, missions | 레거시 공급, 좌표 목록, 미션 |
| elements | ElementPlacementDefinition 목록 |
| elementCatalog | catalog 문서 ID 또는 null |
| elementSupply | ElementLevelSupplyDefinition |
| embeddedDefinitions | 숨겨진 직렬화 PackedElementDefinition[]; 팩 기반 메모리 사본용. 있으면 보존하고 임의로 제작 catalog에 흡수하지 않음 |
| tutorial | LevelTutorialDefinition; 아래 공유 참조·bindings까지 보존 |

`runtimeCatalog`는 NonSerialized 실행 캐시다. `PackedVisuals`는 실행 속성이며 제작 필드가 아니다. `CreateElementCatalog()`/`ToElementPacked()`는 기본값 보충·구형→신형 투영이므로 제작 내보내기의 기준으로 사용하지 않는다.

| 중첩 타입 (원본 경로) | 실제 직렬화 필드 |
|---|---|
| BoardDefinition (`Board/Data`) | rows, columns, cells |
| CellDefinition (`Board/Data`) | isActive |
| BoardCoordinate (`Board/Data`) | row, column |
| BoardEdge (`Board/Data`) | a, b |
| InitialBlockDefinition (`Levels/Data`) | coordinate, kind, fixedColor, rocketDirection |
| ObstaclePlacementDefinition (`Obstacles/Data`) | id, coordinate, kind, durability, color, requiredCharge |
| CoverPlacementDefinition (`Obstacles/Data`) | coordinate, kind, durability |
| DustPlacementDefinition (`Obstacles/Data`) | coordinate, durability |
| ElementPlacementDefinition (`Levels/Data`) | definitionId, instanceId, layer, coordinate, durability, hasColor, color, requiredCharge, rocketDirection |
| LevelConnectionDefinition (`BoardFlow/Data`) | generatorId, targetId, vertices |
| LevelFlowDefinition (`BoardFlow/Data`) | gravity, paths, merges, walls, portals, arrivals |
| GravityCell (`BoardFlow/Data/LevelFlowDefinition.cs`) | coordinate, direction |
| FlowPathCell (동일 파일) | coordinate, isEnd, next |
| FlowMerge (동일 파일) | coordinate, sources |
| FlowPortal (동일 파일) | entrance, hasExit, exit |
| LevelSupplyDefinition (`BlockSupply/Data`) | sources, scrapTarget, scrapLimit, scrapDurability, recoveryTarget |
| SupplySourceDefinition (동일 파일) | coordinate, mode, exhaustion, items |
| SupplyItem (동일 파일) | kind, count, color, direction, durability |
| ElementLevelSupplyDefinition (`Levels/Data`) | sources, scrapTarget, scrapLimit, scrapDurability, recoveryTarget, scrapDefinitionId, recoveryDefinitionId |
| ElementSupplySourceDefinition (동일 파일) | coordinate, mode, exhaustion, items, randomDefinitionId |
| ElementSupplyItemDefinition (동일 파일) | definitionId, count, color, direction, durability |
| LevelMissionDefinition (`Missions/Data`) | kind, color, count |

좌표는 0 기준이며 보드는 9×9 row-major다. 벽/전선 꼭짓점은 칸 좌표와 검증 범위가 다를 수 있으므로 모두 무조건 0..8로 제한하지 않는다. 연결 generatorId/targetId는 레벨 내 본체 ID다. 공급 items[0]이 먼저 나온다. `InitialBlockDefinition.FixedColor` getter는 RandomNormal 등에 남은 fixedColor를 숨긴다. 반드시 private 직렬화 필드를 읽는다. isEnd=true일 때 next, hasExit=false일 때 exit 등 비활성 보조 값도 삭제하지 않는다.

## element

원본 `Elements/Data/ElementDefinitionAsset.cs`: `definition`, `planningDocument`, `planningSection`. `definition`은 `PackedElementDefinition.cs`의 다음 전체 구조다. 실행 `ElementDefinition`의 프로필로 왕복하면 disabled 설정 및 null 차이가 소실된다.

| 타입 | 실제 필드 |
|---|---|
| PackedElementDefinition | id, displayName, placement, charge, damage, color, aggregation, removal, reaction, layer, turn, supply |
| PackedElementPlacement | size, maxDurability, enabled |
| PackedElementCharge | size, minRequired, maxRequired, perHit, enabled |
| PackedElementDamage | adjacentMatch, power, magnetAdjacent, hammer, enabled |
| PackedElementColor | requiresMatchingColor, enabled |
| PackedElementAggregation | perHitCell, enabled |
| PackedElementRemoval | kind, enabled |
| PackedElementLayer | behavior, mission, damage, enabled |
| PackedElementTurn | behavior, initialDurability, enabled |
| PackedElementSupply | behavior, content, hasObstacle, obstacle, bodyIdPrefix, choices, enabled, obstacleDefinitionId, choiceDefinitionIds |

definition.id는 게임 요소 ID다. envelope id와 별개이며 supply의 obstacleDefinitionId/choiceDefinitionIds는 요소 ID다. disabled 객체의 기본 behavior=0, reaction=0의 없음, tutorial missionKind=-1 같은 실제 sentinel은 enum 키 표에서 명시적으로 표현해야 한다. enum 이름 목록만 기계적으로 허용하면 정상 비활성 데이터가 거절된다.

## catalog

원본 `Elements/Data/ElementCatalogAsset.cs`: `definitions`는 element 문서 ID의 원본 순서 목록, `visuals`는 visual 문서 ID 또는 null. 포함 정의의 게임 ID와 문서 ID를 구별한다. 카탈로그 안에 정의 전체 복사본을 저장하지 않는다. 실제 기본 카탈로그에는 정의 18개 및 시각 카탈로그 참조 1개가 있다.

## visual

원본 `Elements/Data/ElementVisualCatalogAsset.cs`: private `catalog`, `planningDocument`.

| 실제 제작 타입 | 실제 필드 |
|---|---|
| VisualAuthoringCatalog | definitions, bindings |
| VisualAuthoringDefinition | key, states, generates |
| VisualAuthoringScalarFrame | color, durability, charge, requiredCharge, direction, frame, logicalSize, path, pivotX, pivotY, size, offsetX, offsetY, angle, order, sheetColumns, sheetRows, sheetFrame |
| VisualAuthoringFrame : VisualAuthoringScalarFrame | 상속 scalar 전체 + effects, effectAnimations |
| VisualAuthoringEffect | key, frames (scalar frame 배열) |
| ElementVisualBindingDto (`ElementVisualCatalogDto.cs`) | id, visualKey |

`path`는 리소스 논리 ID로 변환하고 기존 path를 project 리소스 매핑에 보존한다. states/effectAnimations 순서는 보존한다. 효과 프레임은 scalar이므로 effects/effectAnimations를 재귀적으로 담는 실행 DTO와 다르다. color/direction의 -1은 wildcard다. 명시 문자열 키 계약에서는 wildcard를 별도 키로 두거나, 숫자 selector라는 별도 스키마로 검증한다. generates는 요소 표현 바인딩 ID, effect key와 visualKey는 표현 내부 키이며 임의로 문서 ID로 재발급하지 않는다. effects는 이미지 경로 배열이므로 JSON의 effectResourceIds로 변환하고 project.resources에 경로를 보존한다. path는 resourceId로 저장한다.

`ElementVisualCatalogAsset.ToDto()`는 `CreateCatalog()`→`LegacyElementVisuals.WithOverrides()`를 거쳐 기본 표현을 추가한다. 이는 원본 내보내기 API가 아니다. private 제작 구조를 직접 읽고 실행 시에만 동일 기본값을 적용한다.

## tutorialFlow / tutorialSample / level.tutorial

| 원본 파일 (`Tutorial/Data/`)·타입 | 실제 필드 |
|---|---|
| LevelTutorialDefinition | flow, bindings, completionId, previousLevelNumbers, seed, steps, supply |
| TutorialFlowDefinition | steps, parameters |
| TutorialFlowParameter | key, label, help, stepId, conditionId, field |
| TutorialFlowBinding | key, field, number, definitionId, coordinate, cells, target, color |
| TutorialUserSampleDefinition | description, steps; 샘플 이름은 SO.name |
| TutorialStepDefinition | authoringId, kind, instructions, highlights, hasFirst, first, hasSecond, second, item, actionDefinitionId, results, conditions, combination, automaticHighlights, freeItemCount, actionArea, firstBinding, secondBinding |
| TutorialResultDefinition | kind, definitionId, hasCoordinate, coordinate, count |
| TutorialConditionDefinition | authoringId, kind, requiredCount, matchSize, sizeComparison, origin, anyColor, color, target, aggregation, allowedOrigins, powerDefinitionId, anyDirection, rocketDirection, bindGeneratedAs, item, missionIndex, missionKind, missionColor |
| TutorialTargetDefinition | kind, layer, coordinate, definitionId, cells, binding |

flow는 tutorialFlow 문서 ID인 flowId로 대체한다. shared steps를 각 레벨 제작 steps에 펼쳐 저장하지 않는다. 레벨의 기존 steps도 함께 원본대로 보존한다. 공급은 위 ElementLevelSupplyDefinition이다. previousLevelNumbers는 기존 선행 레벨 번호 의미를 유지하며 completionId는 영구 학습 완료 키다. authoringId는 단계/조건 ID, parameter.key는 binding 대응 키, stepId/conditionId는 공유 흐름 내 ID, firstBinding/secondBinding/target.binding/bindGeneratedAs는 생성 결과 명명 체계다. 이들을 하나의 전역 ID 집합으로 합치면 안 된다.

튜토리얼 조건의 missionKind 기본값 -1은 미선택을 나타낸다. 조건 종류에 따라 사용하지 않는 필드도 왕복한다. 단계·조건·결과·하이라이트·영역·출처 목록 및 파라미터 순서를 보존한다.

## shape

원본 `ShapeCatalog/Editor/Data/LevelShapePreset.cs`: `cells` (bool[]), `sourceName`, `sourceLevelNumber`, `obstacleHistory` (string[]). SO.name도 표시명으로 보존한다. sourceName/sourceLevelNumber는 캡처 당시 기록이지 살아 있는 level 참조가 아니다. 원본 레벨 삭제 후에도 남아야 하므로 필수 참조 검증 대상으로 삼지 않는다.

## 명시 enum 계약에 필요한 전체 타입

문자열 키는 아래 CLR 이름을 고정 계약으로 선언하거나 별도 안정 키 표로 매핑한다. InspectorName 한국어 문구를 저장 키로 쓰지 않는다. 숫자를 무조건 Enum.ToString으로 변환하지 않는다.

| 타입 | 정의 값 |
|---|---|
| RabbitColor | Type1, Type2, Type3, Type4, Type5 |
| InitialBlockKind | RandomNormal, FixedNormal, Rocket, Bomb, Drone, Magnet |
| RocketDirection | Horizontal, Vertical |
| ObstacleKind | Crate, Scrap, Safe, ColorLock, Appliance, Generator |
| CoverKind | Web, Mold |
| PlacementLayer | Block, Obstacle, Cover, Dust |
| GravityDirection | Down, Up, Left, Right |
| SupplyMode | Random, Fixed, MaintainScrap, MaintainRecovery |
| SupplyExhaustion | Stop, Random |
| SupplyKind | RandomNormal, FixedNormal, Rocket, Bomb, Drone, Magnet, Scrap, Recovery, RandomPower |
| MissionKind | Color, Crate, Web, Scrap, Dust, Safe, ColorLock, Appliance, Mold, Recovery; 조건 미선택 -1 별도 |
| ElementReactionBehavior | Durability=1, GeneratorCharge=2, EvenTurnDurability=3; reaction 0 없음 |
| ElementLayerBehavior | CoverDurability=1, CoverRemoval=2, NormalConsumption=3; disabled 기본 0 |
| ElementTurnBehavior | AdjacentCoverSpread=1; disabled 기본 0 |
| ElementSupplyBehavior | RandomNormal=1, FixedNormal=2, Power=3, Recovery=4, Obstacle=5, RandomPower=6; disabled 기본 0 |
| RuntimeContent | Empty, Normal, Rocket, Bomb, Drone, Magnet, Obstacle, Recovery |
| TutorialFlowField | First, Second, ActionArea, Highlights, FreeItemCount, RequiredCount, MatchSize, Target, PowerDefinitionId, Color, MissionIndex, ActionDefinitionId |
| TutorialStepKind | Description, Swap, PowerSwap, Item |
| TutorialResultKind | Generated, Activated, Removed |
| TutorialConditionKind | SuccessfulSwap, Match, DurabilityDecrease, RemainingDurability, Removed, Generated, Activated, Combined, ItemUsed, MissionProgress |
| TutorialDamageAggregation | Total, Each |
| TutorialConditionCombination | All, Any |
| TutorialMatchSizeComparison | Exactly, AtLeast |
| TutorialMatchOrigin | DirectSwap, IncludingCascade |
| TutorialTargetKind | Board, Entity, Definition, Area, Generated |
| TutorialTargetLayer | Content, Cover, Floor |
| BoardItem | Hammer, Swap, Shuffle |
| EffectOrigin | Unknown, AdjacentMatch, Rocket, Bomb, Drone, Magnet, Hammer, RocketRocket, RocketBomb, RocketDrone, BombBomb, BombDrone, DroneDrone, MagnetRocket, MagnetBomb, MagnetDrone, MagnetMagnet |

추가 enum 원본 위치: `Obstacles/Rules/LevelPlacementRules.cs`, `Elements/Data/Element*Profile.cs`, `Elements/Data/ElementReactionBehavior.cs`, `PuzzlePlay/Runtime/State/LevelRuntimeState.cs`, `PuzzlePlay/Runtime/State/EffectOrigin.cs`, `PuzzlePlay/Runtime/Actions/BoardActionExecutor.Items.cs`, `Tutorial/Runtime/TutorialTargetQuery.cs`. 나머지는 위 각 데이터 타입 파일에 정의되어 있다.

## 참조 그래프와 손실 검증

- project → 포함 문서 및 기본 catalog, 리소스 매핑.
- level → catalog, tutorialFlow. catalog → element 목록, visual.
- element 공급 프로필 및 level/tutorial 각 definitionId → 게임 요소 ID. 기본 코드 정의를 허용하려면 이를 명시된 실행 기본 정의 집합으로 검사한다.
- visual binding.id/generates → 게임 요소 표현 바인딩 ID; binding.visualKey/effect key → 표현 키; frame.path/effects → resourceId/effectResourceIds와 프로젝트 리소스 매핑.
- level 연결 → 레벨 내부 id/instanceId. tutorial parameter → step/condition authoringId. bindings → parameter.key. generated bindings → 생성 결과 이름.
- shape 출처는 역사적 스냅샷이며 강한 외래 참조가 아니다.

필수 비교는 원본 직렬화 트리 대 내보낸 제작 트리다. 실행 결과만 일치한다고 비활성 설정·기획 출처·원본 공유 관계 보존을 입증할 수 없다. LevelDefinition과 LevelShapePreset에는 10×10 데이터를 9×9로 자르는 OnAfterDeserialize가 있다. 원본 파일 해시 불변과 로드된 제작 값의 동등성을 분리 기록하며 구형 파일 원문과 로드 후 값이 같다고 가정하지 않는다.

## 제외 항목

PopupCatalog (`Assets/Scripts/Systems/Popup/Runtime/PopupCatalog.cs`, `Assets/Data/UI/PuzzlePopupCatalog.asset`), 외부 SDK SO, URP/품질/빌드 프로필, Addressables 설정, Scene/Prefab UI, 플레이어 진행·학습 저장은 제작 JSON 변환 대상이 아니다. 팩은 산출물이며 역변환 원본이 아니다. `.meta` GUID는 최초 대응 근거와 원본 보존 검사에만 쓰고 외부 도구의 필수 의존으로 만들지 않는다. Unity 런타임 캐시, dirty/hideFlags/instanceID 및 Unity 엔진 기반 참조 메타데이터는 문서 payload에 복제하지 않는다.

## 구현된 v1 저장 계약

- project.data: name, contentVersion, defaultCatalogId, resources[{id,path}], documents[{id,path}], sourceIds[{sourceGuid,documentId}]. 새 원본 GUID 대응은 새 스냅샷의 project와 함께 공개한다.
- 모든 스키마 필드는 필수다. null 허용 여부는 AuthoringSchema에 고정되어 있으며 null과 빈 목록은 자동 치환하지 않는다. 사용에 필수인 목록/객체는 의미 검사에서도 확인한다. 미지원 속성·enum 키·envelope 버전을 거절하고 level.data.schemaVersion은 기존 실행기 범위인 1~5만 허용한다.
- 단일 파일의 Hash는 실제 UTF-8 바이트 SHA256이다. StoredContentSnapshot.Hash는 project 파일과 포함된 모든 하위 파일 해시를 합친 revision이다. 단일 파일 Save에 스냅샷 Hash를 넘기지 않는다.
- Publish는 시작과 공개 직전에 기존 스냅샷 revision을 확인한다. 새 세대 문서를 기록·재읽기·참조 검증한 뒤 project.json을 교체한다. 실패 세대는 공개 목록에 없으므로 재열기에서 제외한다. project.json.previous가 이전 세대를 가리킨다.
- 외부 프로세스까지 잠그는 다중 파일 트랜잭션은 아니다. 교체 직전 확인 이후의 비협조적 외부 수정 경쟁과 외부에서 이미 삭제/손상시킨 과거 세대의 복원을 보장하지 않는다.
- 공유 흐름의 단계/조건/파라미터 식별자는 원본에서 검사한다. 바인딩 대상 값의 생성 이름/횟수 의미는 레벨별 값을 적용한 뒤 검사한다.
- 모든 카탈로그의 공급 참조는 자체 정의 목록에서 먼저 확인한다. 기본 ID 보충은 레벨의 요소 조회 범위에만 적용한다. 참조 검증은 전체 게임 규칙·클리어 가능성 검증을 대신하지 않는다.
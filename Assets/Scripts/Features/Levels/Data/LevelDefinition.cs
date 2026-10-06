using System.Collections.Generic;
using Board;
using System.Linq;
using UnityEngine;

namespace Levels
{
    public sealed class LevelDefinition : ScriptableObject, ISerializationCallbackReceiver
    {
        public const int LegacySchemaVersion = 4;
        public const int CurrentSchemaVersion = 5;

        // 기존 생성 경로는 편집기의 신형 생성 연결 전까지 v4를 유지한다.
        [SerializeField] private int schemaVersion = LegacySchemaVersion;
        [SerializeField] private int levelNumber = 1;
        [SerializeField] private int moveCount = 20;
        [SerializeField] private List<RabbitColor> colors = new List<RabbitColor>
        {
            RabbitColor.Type1, RabbitColor.Type2, RabbitColor.Type3,
            RabbitColor.Type4, RabbitColor.Type5
        };
        [SerializeField] private BoardDefinition board = BoardDefinition.CreateDefault();
        [SerializeField] private List<InitialBlockDefinition> initialBlocks = new List<InitialBlockDefinition>();
        [SerializeField] private List<ObstaclePlacementDefinition> obstacles = new List<ObstaclePlacementDefinition>();
        [SerializeField] private List<CoverPlacementDefinition> covers = new List<CoverPlacementDefinition>();
        [SerializeField] private List<DustPlacementDefinition> dust = new List<DustPlacementDefinition>();
        [SerializeField] private LevelFlowDefinition flow = new LevelFlowDefinition();
        [SerializeField] private List<LevelConnectionDefinition> connections = new List<LevelConnectionDefinition>();
        [SerializeField] private LevelSupplyDefinition supply = new LevelSupplyDefinition();
        [SerializeField] private List<BoardCoordinate> recoveryParts = new List<BoardCoordinate>();
        [SerializeField] private List<LevelMissionDefinition> missions = new List<LevelMissionDefinition>();
        [SerializeField] private List<ElementPlacementDefinition> elements = new List<ElementPlacementDefinition>();
        [SerializeField] private Elements.ElementCatalogAsset elementCatalog;
        [SerializeField] private ElementLevelSupplyDefinition elementSupply = new ElementLevelSupplyDefinition();
        // 팩에서 만든 메모리 레벨의 JSON 시험 사본용 값이다. 제작 에셋에는 자동으로 쓰지 않는다.
        [SerializeField, HideInInspector] private Elements.PackedElementDefinition[] embeddedDefinitions;

        public int SchemaVersion => schemaVersion;
        public int LevelNumber => levelNumber;
        public int MoveCount => moveCount;
        public IReadOnlyList<RabbitColor> Colors => colors;
        public BoardDefinition Board => board;
        public IReadOnlyList<InitialBlockDefinition> InitialBlocks => initialBlocks;
        public IReadOnlyList<ObstaclePlacementDefinition> Obstacles => obstacles;
        public IReadOnlyList<CoverPlacementDefinition> Covers => covers;
        public IReadOnlyList<DustPlacementDefinition> Dust => dust;
        public LevelFlowDefinition Flow => flow;
        public IReadOnlyList<LevelConnectionDefinition> Connections => connections;
        public LevelSupplyDefinition Supply => supply;
        public IReadOnlyList<BoardCoordinate> RecoveryParts => recoveryParts;
        public IReadOnlyList<LevelMissionDefinition> Missions => missions;
        public IReadOnlyList<ElementPlacementDefinition> Elements => elements;
        public Elements.ElementCatalogAsset ElementCatalog => elementCatalog;
        public ElementLevelSupplyDefinition ElementSupply => elementSupply;

        [System.NonSerialized] private Elements.ElementCatalog runtimeCatalog;
        public Elements.ElementCatalog CreateElementCatalog()
        {
            if (runtimeCatalog != null) return runtimeCatalog;
            if (elementCatalog == null) return embeddedDefinitions == null || embeddedDefinitions.Length == 0
                ? global::Elements.LegacyElementDefinitions.DefaultCatalog
                : new Elements.ElementCatalog(embeddedDefinitions.Select(value => value.ToDefinition()));
            Elements.ElementCatalog authored = elementCatalog.CreateCatalog();
            // 제작 카탈로그에 없는 기본 블록만 보충한다. 같은 ID의 제작 정의는 그대로 유지한다.
            HashSet<Elements.ElementId> ids = new HashSet<Elements.ElementId>(authored.Definitions.Select(value => value.Id));
            return new Elements.ElementCatalog(authored.Definitions.Concat(global::Elements.LegacyElementDefinitions.DefaultCatalog.Definitions
                .Where(value => !ids.Contains(value.Id))));
        }

        public PackedLevel ToPacked()
        {
            if (schemaVersion >= CurrentSchemaVersion) throw new System.InvalidOperationException("신형 ID 레벨은 구형 DTO로 저장할 수 없습니다. 팩2를 사용하세요.");
            return LegacyProjection();
        }

        internal PackedLevel LegacyProjection() => new PackedLevel
        {
            SchemaVersion = schemaVersion,
            LevelNumber = levelNumber,
            MoveCount = moveCount,
            Colors = colors,
            Board = board,
            InitialBlocks = initialBlocks,
            Obstacles = obstacles,
            Covers = covers,
            Dust = dust,
            Flow = flow,
            Connections = connections,
            Supply = supply,
            RecoveryParts = recoveryParts,
            Missions = missions
        };

        internal PackedElementLevel ToElementPacked() => new PackedElementLevel
        {
            SchemaVersion = CurrentSchemaVersion, LevelNumber = levelNumber, MoveCount = moveCount,
            Colors = colors, Board = board, Flow = flow, Connections = connections, Missions = missions,
            Elements = (schemaVersion == LegacySchemaVersion ? LegacyElementLevelAdapter.Preview(this) : elements?.ToArray())?
                .Select(item => item == null ? null : new ElementPlacementDefinition
                {
                    definitionId = item.definitionId, instanceId = string.IsNullOrEmpty(item.instanceId) ? null : item.instanceId,
                    layer = item.layer, coordinate = item.coordinate, durability = item.durability, hasColor = item.hasColor,
                    color = item.color, requiredCharge = item.requiredCharge, rocketDirection = item.rocketDirection
                }).ToList(),
            Supply = schemaVersion == LegacySchemaVersion ? ElementLevelSupplyDefinition.FromLegacy(supply) : elementSupply
        };

        internal static LevelDefinition FromElementPacked(PackedElementLevel data, Elements.ElementCatalog catalog)
        {
            LevelDefinition level = CreateInstance<LevelDefinition>();
            level.schemaVersion = data.SchemaVersion; level.levelNumber = data.LevelNumber; level.moveCount = data.MoveCount;
            level.colors = data.Colors; level.board = data.Board; level.flow = data.Flow; level.connections = data.Connections;
            level.missions = data.Missions; level.elements = data.Elements; level.elementSupply = data.Supply;
            level.runtimeCatalog = catalog; level.name = "Level_" + data.LevelNumber; level.hideFlags = HideFlags.DontSave;
            level.embeddedDefinitions = catalog.Definitions.OrderBy(value => value.Id.Value, System.StringComparer.Ordinal)
                .Select(global::Elements.PackedElementDefinition.FromDefinition).ToArray();
            return level;
        }

        public static LevelDefinition FromPacked(PackedLevel data)
        {
            LevelBoardSizeMigration.Crop(data);
            LevelDefinition level = CreateInstance<LevelDefinition>();
            level.schemaVersion = data.SchemaVersion;
            level.levelNumber = data.LevelNumber;
            level.moveCount = data.MoveCount;
            level.colors = data.Colors;
            level.board = data.Board;
            level.initialBlocks = data.InitialBlocks;
            level.obstacles = data.Obstacles;
            level.covers = data.Covers;
            level.dust = data.Dust;
            level.flow = data.Flow;
            level.connections = data.Connections;
            level.supply = data.Supply;
            level.recoveryParts = data.RecoveryParts;
            level.missions = data.Missions;
            level.name = "Level_" + data.LevelNumber;
            level.hideFlags = HideFlags.DontSave;
            return level;
        }

        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize()
        {
            runtimeCatalog = null;
            if (schemaVersion <= LegacySchemaVersion) LevelBoardSizeMigration.Crop(LegacyProjection());
        }

        // OnEnable/OnValidate에서 초기화하지 않는다. 완전한 구형 10×10만 잘라내고 오류 데이터는 보존한다.
    }
}

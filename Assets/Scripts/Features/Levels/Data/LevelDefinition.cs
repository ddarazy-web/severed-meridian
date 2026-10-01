using System.Collections.Generic;
using Board;
using UnityEngine;

namespace Levels
{
    public sealed class LevelDefinition : ScriptableObject, ISerializationCallbackReceiver
    {
        public const int CurrentSchemaVersion = 4;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
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

        public PackedLevel ToPacked() => new PackedLevel
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
        public void OnAfterDeserialize() => LevelBoardSizeMigration.Crop(ToPacked());

        // OnEnable/OnValidate에서 초기화하지 않는다. 완전한 구형 10×10만 잘라내고 오류 데이터는 보존한다.
    }
}

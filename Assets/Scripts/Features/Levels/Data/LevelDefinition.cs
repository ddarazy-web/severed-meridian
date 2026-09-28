using System.Collections.Generic;
using Board;
using UnityEngine;

namespace Levels
{
    public sealed class LevelDefinition : ScriptableObject
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

        // OnEnable/OnValidate에서 초기화하지 않는다. 오류가 있는 작업 중 에셋도 그대로 읽는다.
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    // 초기 고정 여부는 이동 제한이 아니다. 덮개·장애물의 실제 이동 규칙은 후속 실행부에서 판단한다.
    public enum RuntimeContent { Empty, Normal, Rocket, Bomb, Drone, Magnet, Obstacle, Recovery }

    public sealed class RuntimeCell
    {
        public BoardCoordinate Coordinate { get; }
        public bool IsActive { get; }
        public RuntimeContent Content { get; internal set; }
        public RabbitColor? Color { get; internal set; }
        public RocketDirection? RocketDirection { get; internal set; }
        public int? ObstacleIndex { get; internal set; }
        public CoverKind? Cover { get; internal set; }
        public int CoverDurability { get; internal set; }
        public int DustDurability { get; internal set; }
        public GravityDirection Gravity { get; internal set; } = GravityDirection.Down;
        internal RuntimeCell(BoardCoordinate coordinate, bool active) { Coordinate = coordinate; IsActive = active; }
        internal RuntimeCell Copy() => (RuntimeCell)MemberwiseClone();
    }

    public sealed class RuntimeObstacle
    {
        public ObstaclePlacementDefinition Definition { get; }
        public int Durability { get; internal set; }
        public int Charge { get; internal set; }
        internal RuntimeObstacle(ObstaclePlacementDefinition definition) { Definition = definition; Durability = definition.Durability; }
        internal RuntimeObstacle Copy() => (RuntimeObstacle)MemberwiseClone();
    }

    public sealed class RuntimeMission
    {
        public LevelMissionDefinition Definition { get; }
        public int Target { get; internal set; }
        public int Progress { get; internal set; }
        public int Remaining => Math.Max(0, Target - Progress);
        internal RuntimeMission(LevelMissionDefinition definition) { Definition = definition; Target = definition.Count; }
        internal RuntimeMission Copy() => (RuntimeMission)MemberwiseClone();
    }

    public sealed class RuntimeMerge
    {
        public BoardCoordinate Coordinate { get; }
        public ReadOnlyCollection<BoardCoordinate> Sources { get; }
        internal RuntimeMerge(FlowMerge merge) { Coordinate = merge.Coordinate; Sources = Array.AsReadOnly(merge.Sources.ToArray()); }
    }

    public sealed class RuntimeConnection
    {
        public string GeneratorId { get; }
        public string TargetId { get; }
        public ReadOnlyCollection<BoardCoordinate> Vertices { get; }
        internal RuntimeConnection(LevelConnectionDefinition connection)
        { GeneratorId = connection.GeneratorId; TargetId = connection.TargetId; Vertices = Array.AsReadOnly(connection.Vertices.ToArray()); }
        // 공개 연결의 양 끝만 복원할 때 경로 장식은 실행 규칙에 관여하지 않는다.
        internal RuntimeConnection(string generatorId, string targetId)
        { GeneratorId = generatorId; TargetId = targetId; Vertices = Array.AsReadOnly(Array.Empty<BoardCoordinate>()); }
    }

    public sealed class RuntimeSource
    {
        public BoardCoordinate Coordinate { get; }
        public SupplyMode Mode { get; }
        public SupplyExhaustion Exhaustion { get; }
        public ReadOnlyCollection<SupplyItem> Items { get; }
        // 저장 목록을 직접 줄이지 않는다. 인덱스 0이 첫 공급이며 초기 구성에서는 소모하지 않는다.
        public int ItemIndex { get; internal set; }
        public int ItemConsumed { get; internal set; }
        internal RuntimeSource(SupplySourceDefinition source)
        {
            Coordinate = source.Coordinate; Mode = source.Mode; Exhaustion = source.Exhaustion;
            Items = Array.AsReadOnly(source.Items.ToArray());
        }
        internal RuntimeSource Copy() => (RuntimeSource)MemberwiseClone();
        internal RuntimeSource(BoardCoordinate coordinate)
        { Coordinate = coordinate; Mode = SupplyMode.Random; Exhaustion = SupplyExhaustion.Random; Items = Array.AsReadOnly(Array.Empty<SupplyItem>()); }
    }

    public sealed class RuntimeFlow
    {
        public ReadOnlyCollection<GravityCell> Gravity { get; }
        public ReadOnlyCollection<FlowPathCell> Paths { get; }
        public ReadOnlyCollection<RuntimeMerge> Merges { get; }
        public ReadOnlyCollection<BoardEdge> Walls { get; }
        public ReadOnlyCollection<FlowPortal> Portals { get; }
        public ReadOnlyCollection<BoardCoordinate> Arrivals { get; }
        internal RuntimeFlow(LevelFlowDefinition flow)
        {
            Gravity = Array.AsReadOnly(flow.Gravity.ToArray()); Paths = Array.AsReadOnly(flow.Paths.ToArray());
            Merges = Array.AsReadOnly(flow.Merges.Select(merge => new RuntimeMerge(merge)).ToArray());
            Walls = Array.AsReadOnly(flow.Walls.ToArray()); Portals = Array.AsReadOnly(flow.Portals.ToArray());
            Arrivals = Array.AsReadOnly(flow.Arrivals.ToArray());
        }
        // 제작 전용 경로/합류 설정 없이 공개된 지형만 새 배열로 구성한다.
        internal RuntimeFlow(IEnumerable<BoardEdge> walls, IEnumerable<FlowPortal> portals, IEnumerable<BoardCoordinate> arrivals)
        {
            Gravity = Array.AsReadOnly(Array.Empty<GravityCell>()); Paths = Array.AsReadOnly(Array.Empty<FlowPathCell>());
            Merges = Array.AsReadOnly(Array.Empty<RuntimeMerge>());
            Walls = Array.AsReadOnly(walls.ToArray()); Portals = Array.AsReadOnly(portals.ToArray()); Arrivals = Array.AsReadOnly(arrivals.ToArray());
        }
    }

    public sealed class RuntimeSupply
    {
        public ReadOnlyCollection<RuntimeSource> Sources { get; }
        public int ScrapTarget { get; }
        public int ScrapLimit { get; }
        public int ScrapDurability { get; }
        public int RecoveryTarget { get; }
        public int ScrapGenerated { get; internal set; }
        public int ScrapRemaining => Math.Max(0, ScrapLimit - ScrapGenerated);
        internal RuntimeSupply(LevelSupplyDefinition supply)
        {
            Sources = Array.AsReadOnly(supply.Sources.Select(source => new RuntimeSource(source)).ToArray());
            ScrapTarget = supply.ScrapTarget; ScrapLimit = supply.ScrapLimit;
            ScrapDurability = supply.ScrapDurability; RecoveryTarget = supply.RecoveryTarget;
        }
        internal RuntimeSupply(RuntimeSupply source)
        {
            Sources = Array.AsReadOnly(source.Sources.Select(item => item.Copy()).ToArray());
            ScrapTarget = source.ScrapTarget; ScrapLimit = source.ScrapLimit; ScrapDurability = source.ScrapDurability;
            RecoveryTarget = source.RecoveryTarget; ScrapGenerated = source.ScrapGenerated;
        }
        internal RuntimeSupply(IEnumerable<BoardCoordinate> randomSources)
        { Sources = Array.AsReadOnly(randomSources.Select(at => new RuntimeSource(at)).ToArray()); ScrapDurability = 1; }
    }

    public sealed class LevelRuntimeState
    {
        private readonly List<RuntimeObstacle> obstacleBodies;
        public int SchemaVersion { get; }
        public int LevelNumber { get; }
        public int InitialMoves { get; }
        public int MovesRemaining { get; internal set; }
        public string DefinitionFingerprint { get; }
        public int Rows { get; }
        public int Columns { get; }
        public ReadOnlyCollection<RabbitColor> Colors { get; }
        public ReadOnlyCollection<InitialBlockDefinition> InitialBlocks { get; }
        public ReadOnlyCollection<RuntimeCell> Cells { get; }
        public ReadOnlyCollection<RuntimeObstacle> Obstacles { get; }
        public ReadOnlyCollection<RuntimeMission> Missions { get; }
        public ReadOnlyCollection<RuntimeConnection> Connections { get; }
        public RuntimeFlow Flow { get; }
        public RuntimeSupply Supply { get; }
        public SimulationRandom Random { get; }
        private readonly List<RecoveryRecord> recoveries = new List<RecoveryRecord>();
        private readonly List<MissionProgressRecord> missionProgressRecords = new List<MissionProgressRecord>();
        public ReadOnlyCollection<MissionProgressRecord> MissionProgressRecords => missionProgressRecords.AsReadOnly();
        internal void RecordMissionProgress(MissionProgressRecord record) => missionProgressRecords.Add(record);
        public ReadOnlyCollection<RecoveryRecord> Recoveries => recoveries.AsReadOnly();
        internal void RecordRecovery(RecoveryRecord record) => recoveries.Add(record);

        internal LevelRuntimeState(LevelDefinition level, int seed, string fingerprint)
        {
            SchemaVersion = level.SchemaVersion; LevelNumber = level.LevelNumber;
            InitialMoves = MovesRemaining = level.MoveCount; DefinitionFingerprint = fingerprint;
            Rows = level.Board.Rows; Columns = level.Board.Columns;
            Colors = Array.AsReadOnly(level.Colors.ToArray()); InitialBlocks = Array.AsReadOnly(level.InitialBlocks.ToArray());
            Cells = Array.AsReadOnly(Enumerable.Range(0, Rows * Columns)
                .Select(index => new RuntimeCell(new BoardCoordinate(index / Columns, index % Columns), level.Board.Cells[index].IsActive)).ToArray());
            obstacleBodies = level.Obstacles.Select(definition => new RuntimeObstacle(definition)).ToList();
            Obstacles = obstacleBodies.AsReadOnly();
            Missions = Array.AsReadOnly(level.Missions.Select(definition => new RuntimeMission(definition)).ToArray());
            foreach (RuntimeMission mission in Missions)
                if (mission.Definition.Kind == MissionKind.Mold) mission.Target = level.Covers.Count(cover => cover.Kind == CoverKind.Mold);
            Connections = Array.AsReadOnly(level.Connections.Select(connection => new RuntimeConnection(connection)).ToArray());
            Flow = new RuntimeFlow(level.Flow); Supply = new RuntimeSupply(level.Supply); Random = new SimulationRandom(seed);
        }

        // 불변 정의/흐름은 공유하고, 변경 가능한 셀·본체·미션·공급 커서·난수는 독립 복사한다.
        internal LevelRuntimeState(LevelRuntimeState source)
        {
            SchemaVersion = source.SchemaVersion; LevelNumber = source.LevelNumber;
            InitialMoves = source.InitialMoves; MovesRemaining = source.MovesRemaining; DefinitionFingerprint = source.DefinitionFingerprint;
            Rows = source.Rows; Columns = source.Columns; Colors = source.Colors; InitialBlocks = source.InitialBlocks;
            Cells = Array.AsReadOnly(source.Cells.Select(cell => cell.Copy()).ToArray());
            obstacleBodies = source.Obstacles.Select(obstacle => obstacle.Copy()).ToList();
            Obstacles = obstacleBodies.AsReadOnly();
            Missions = Array.AsReadOnly(source.Missions.Select(mission => mission.Copy()).ToArray());
            Connections = source.Connections; Flow = source.Flow; Supply = new RuntimeSupply(source.Supply); Random = source.Random.Copy();
            recoveries.AddRange(source.recoveries);
            missionProgressRecords.AddRange(source.missionProgressRecords);
        }

        /// <summary>
        /// 원본 에셋이나 실행 상태를 받지 않는 값 기반 현재 상태 구성 경로다.
        /// 초기 배치를 다시 채우지 않으며 빈칸·현재 내구도·남은 미션을 그대로 보존한다.
        /// 전달한 변경 가능 값도 복사하므로 호출자의 작업 배열과 판이 상태를 공유하지 않는다.
        /// </summary>
        /// <param name="rows">행 수.</param><param name="columns">열 수.</param><param name="moves">남은 이동.</param>
        /// <param name="cells">현재 점유 값.</param><param name="bodies">현재 본체 값.</param><param name="missions">현재 목표.</param>
        /// <param name="flow">값으로 구성한 흐름.</param><param name="supply">값으로 구성한 공급.</param>
        /// <param name="connections">값으로 구성한 연결.</param><param name="seed">이 새 상태만의 난수 시드.</param>
        internal LevelRuntimeState(int rows, int columns, int moves, IEnumerable<RuntimeCell> cells,
            IEnumerable<RuntimeObstacle> bodies, IEnumerable<RuntimeMission> missions, RuntimeFlow flow,
            RuntimeSupply supply, IEnumerable<RuntimeConnection> connections, int seed)
        {
            SchemaVersion = LevelDefinition.CurrentSchemaVersion; LevelNumber = 1;
            Rows = rows; Columns = columns; InitialMoves = MovesRemaining = moves;
            DefinitionFingerprint = "value-snapshot";
            Colors = Array.AsReadOnly((RabbitColor[])Enum.GetValues(typeof(RabbitColor)));
            InitialBlocks = Array.AsReadOnly(Array.Empty<InitialBlockDefinition>());
            Cells = Array.AsReadOnly(cells.Select(cell => cell.Copy()).ToArray());
            obstacleBodies = bodies.Select(body => body.Copy()).ToList(); Obstacles = obstacleBodies.AsReadOnly();
            Missions = Array.AsReadOnly(missions.Select(mission => mission.Copy()).ToArray());
            Flow = flow; Supply = new RuntimeSupply(supply);
            Connections = Array.AsReadOnly(connections.ToArray()); Random = new SimulationRandom(seed);
        }

        // 제거된 본체도 남겨 같은 턴의 피해 기록과 새 공급 본체가 충돌하지 않게 한다.
        internal void SupplyScrap(RuntimeCell cell, int durability)
        {
            cell.Content = RuntimeContent.Obstacle; cell.Color = null; cell.RocketDirection = null;
            cell.ObstacleIndex = obstacleBodies.Count;
            obstacleBodies.Add(new RuntimeObstacle(new ObstaclePlacementDefinition(
                "supply-scrap-" + cell.ObstacleIndex.Value, cell.Coordinate, ObstacleKind.Scrap, durability)));
        }

        public int LiveScrapCount => Cells.Count(cell => cell.Content == RuntimeContent.Obstacle && cell.ObstacleIndex.HasValue &&
            Obstacles[cell.ObstacleIndex.Value].Definition.Kind == ObstacleKind.Scrap && Obstacles[cell.ObstacleIndex.Value].Durability > 0);

        public RuntimeCell CellAt(BoardCoordinate coordinate)
        {
            if (coordinate.Row < 0 || coordinate.Row >= Rows || coordinate.Column < 0 || coordinate.Column >= Columns)
                throw new ArgumentOutOfRangeException(nameof(coordinate));
            return Cells[coordinate.Row * Columns + coordinate.Column];
        }
    }
}

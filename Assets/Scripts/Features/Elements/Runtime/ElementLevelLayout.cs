using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;
using Simulation;
using UnityEngine;

namespace Elements
{
    /// <summary>신형 배치를 기존 흐름 입력에 연결하되 실제 선택 정의를 별도로 보존한다.</summary>
    internal sealed class ElementLevelLayout : IDisposable
    {
        internal LevelDefinition Level { get; }
        internal Dictionary<string, ElementDefinition> Bodies { get; } = new Dictionary<string, ElementDefinition>(StringComparer.Ordinal);
        internal Dictionary<BoardCoordinate, ElementDefinition> Covers { get; } = new Dictionary<BoardCoordinate, ElementDefinition>();
        internal Dictionary<BoardCoordinate, ElementDefinition> Floors { get; } = new Dictionary<BoardCoordinate, ElementDefinition>();
        internal Dictionary<BoardCoordinate, ElementDefinition> Contents { get; } = new Dictionary<BoardCoordinate, ElementDefinition>();
        internal List<LevelValidationIssue> Issues { get; } = new List<LevelValidationIssue>();
        internal ElementLevelSupplyLayout Supply { get; }

        internal ElementLevelLayout(LevelDefinition source, ElementCatalog catalog)
        {
            // 구형 목록은 신형 목록이 비어 있어도 재사용하지 않는다. 다른 레벨 값은 읽기만 한다.
            PackedLevel data = source.LegacyProjection(); data.SchemaVersion = 4;
            data.InitialBlocks = new List<InitialBlockDefinition>(); data.Obstacles = new List<ObstaclePlacementDefinition>();
            data.Covers = new List<CoverPlacementDefinition>(); data.Dust = new List<DustPlacementDefinition>();
            data.RecoveryParts = new List<BoardCoordinate>();
            Dictionary<PlacementLayer, HashSet<BoardCoordinate>> occupied = Enum.GetValues(typeof(PlacementLayer)).Cast<PlacementLayer>()
                .ToDictionary(layer => layer, layer => new HashSet<BoardCoordinate>());
            if (source.Elements == null) Issues.Add(new LevelValidationIssue(LevelValidationCode.MissingPlacementList, "ID 배치 목록이 없습니다.", "elements"));
            else for (int i = 0; i < source.Elements.Count; i++)
            {
                ElementPlacementDefinition item = source.Elements[i]; string path = $"elements.Array.data[{i}]";
                try
                {
                    if (item == null) throw new ArgumentException("ID 배치가 null입니다.");
                    ElementDefinition definition = catalog.Get(new ElementId(item.definitionId));
                    PackedElementDefinition.ValidateDefinition(definition);
                    if (!occupied.ContainsKey(item.layer)) throw new ArgumentException("등록하지 않은 배치 층입니다.");
                    int size = item.layer == PlacementLayer.Obstacle ? definition.ChargePlacement?.Size ?? definition.RequirePlacement().Size : 1;
                    if (size > BoardDefinition.DefaultRows) throw new ArgumentException("요소 크기가 보드보다 큽니다.");
                    if (item.layer == PlacementLayer.Obstacle && source.Flow?.Walls != null &&
                        source.Flow.Walls.Any(wall => LevelFlowRules.InternalWall(wall, item.coordinate, size)))
                        throw new ArgumentException("본체 내부에 고철 벽이 있습니다.");
                    foreach (BoardCoordinate cell in LevelPlacementRules.Footprint(item.coordinate, size))
                    {
                        string error = LevelPlacementRules.CellError(source, cell);
                        if (error != null) throw new ArgumentException(error);
                        if (!occupied[item.layer].Add(cell)) throw new ArgumentException("같은 층의 점유가 중복됩니다.");
                        if (item.layer == PlacementLayer.Block && occupied[PlacementLayer.Obstacle].Contains(cell) ||
                            item.layer == PlacementLayer.Obstacle && occupied[PlacementLayer.Block].Contains(cell))
                            throw new ArgumentException("일반/파워 블록과 장애물이 같은 칸을 점유합니다.");
                    }
                    if (item.hasColor && (!Enum.IsDefined(typeof(RabbitColor), item.color) || source.Colors == null || !source.Colors.Contains(item.color)))
                        throw new ArgumentException("사용하지 않는 배치 색입니다.");
                    if (definition.ColorMatchPolicy?.RequiresMatchingColor == true && !item.hasColor)
                        throw new ArgumentException("일치 색 정책의 배치 색이 없습니다.");
                    if (item.layer == PlacementLayer.Obstacle)
                    {
                        ObstacleKind kind = ViewKind(definition);
                        if (string.IsNullOrWhiteSpace(item.instanceId) || Bodies.ContainsKey(item.instanceId)) throw new ArgumentException("본체 ID가 비어 있거나 중복됩니다.");
                        if (definition.ReactionBehavior == ElementReactionBehavior.GeneratorCharge)
                        {
                            ElementChargePlacementProfile charge = definition.RequireChargePlacement();
                            if (item.requiredCharge < charge.MinRequiredCharge || item.requiredCharge > charge.MaxRequiredCharge)
                                throw new ArgumentException("필요 충전량이 정의 범위를 벗어났습니다.");
                        }
                        else if (item.durability < 1 || item.durability > definition.RequirePlacement().MaxDurability)
                            throw new ArgumentException("내구도가 정의 범위를 벗어났습니다.");
                        Bodies.Add(item.instanceId, definition);
                        data.Obstacles.Add(new ObstaclePlacementDefinition(item.instanceId, item.coordinate, kind, item.durability, item.color, item.requiredCharge));
                    }
                    else if (item.layer == PlacementLayer.Cover || item.layer == PlacementLayer.Dust)
                    {
                        ElementLayerProfile layer = definition.RequireLayer();
                        if (item.durability < 1 || item.durability > definition.RequirePlacement().MaxDurability)
                            throw new ArgumentException("층 내구도가 정의 범위를 벗어났습니다.");
                        if (item.layer == PlacementLayer.Dust)
                        {
                            if (layer.Behavior != ElementLayerBehavior.NormalConsumption) throw new ArgumentException("바닥 층 행동이 아닙니다.");
                            Floors.Add(item.coordinate, definition);
                            data.Dust.Add(new DustPlacementDefinition(item.coordinate, item.durability));
                        }
                        else
                        {
                            if (layer.Behavior == ElementLayerBehavior.NormalConsumption) throw new ArgumentException("덮개 층 행동이 아닙니다.");
                            Covers.Add(item.coordinate, definition);
                            data.Covers.Add(new CoverPlacementDefinition(item.coordinate,
                                layer.Behavior == ElementLayerBehavior.CoverRemoval ? CoverKind.Mold : CoverKind.Web, item.durability));
                        }
                    }
                    else
                    {
                        ElementSupplyProfile supply = definition.RequireSupply();
                        Contents.Add(item.coordinate, definition);
                        if (supply.Behavior == ElementSupplyBehavior.Recovery) data.RecoveryParts.Add(item.coordinate);
                        else
                        {
                            InitialBlockKind kind = supply.Behavior switch
                            {
                                ElementSupplyBehavior.RandomNormal => InitialBlockKind.RandomNormal,
                                ElementSupplyBehavior.FixedNormal => InitialBlockKind.FixedNormal,
                                ElementSupplyBehavior.Power => supply.Content switch
                                {
                                    RuntimeContent.Rocket => InitialBlockKind.Rocket, RuntimeContent.Bomb => InitialBlockKind.Bomb,
                                    RuntimeContent.Drone => InitialBlockKind.Drone, RuntimeContent.Magnet => InitialBlockKind.Magnet,
                                    _ => throw new ArgumentException("초기 파워 생성 설정이 잘못됐습니다.")
                                },
                                _ => throw new ArgumentException("초기 블록에 사용할 수 없는 공급 행동입니다.")
                            };
                            if (kind == InitialBlockKind.FixedNormal && !item.hasColor) throw new ArgumentException("고정 일반 블록의 색이 없습니다.");
                            if (kind == InitialBlockKind.Rocket && !Enum.IsDefined(typeof(RocketDirection), item.rocketDirection))
                                throw new ArgumentException("등록하지 않은 로켓 방향입니다.");
                            data.InitialBlocks.Add(new InitialBlockDefinition(item.coordinate, kind, item.color, item.rocketDirection));
                        }
                    }
                }
                catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is KeyNotFoundException)
                { Issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidPlacementValue, $"정의 ID '{item?.definitionId ?? "<null>"}': {error.Message}", path, item?.coordinate)); }
            }
            Supply = new ElementLevelSupplyLayout(source.ElementSupply, catalog, Issues);
            data.Supply = Supply.Value;
            Level = LevelDefinition.FromPacked(data);
        }

        // 기존 표시/공개 기록의 작은 의미 집합만 연결한다. 실제 피해·미션·드론 조회는 Bodies의 선택 정의를 사용한다.
        internal MissionSupplySummary MissionSupply(LevelMissionDefinition mission)
        {
            MissionSupplySummary legacy = LevelMissionRules.Supply(Level, mission);
            if (mission.Kind == MissionKind.Color) return legacy;
            ElementDefinition[] layers = Covers.Values.Concat(Floors.Values).ToArray();
            long initial = layers.LongCount(definition => definition.RequireLayer().Mission == mission.Kind) +
                Bodies.Values.LongCount(definition => definition.RemovalMissionProfile?.Kind == mission.Kind);
            if (mission.Kind == MissionKind.Recovery)
                initial += Contents.Values.LongCount(definition => definition.RequireSupply().Behavior == ElementSupplyBehavior.Recovery);
            bool dynamic = mission.Kind == MissionKind.Mold || layers.Any(definition =>
                definition.Turn != null && definition.RequireLayer().Mission == mission.Kind);
            long fixedCount = Supply.Fixed(mission.Kind);
            bool recovery = mission.Kind == MissionKind.Recovery && Supply.Recovery != null;
            long maintained = mission.Kind == MissionKind.Scrap && Supply.Scrap != null ? Math.Max(0, Supply.Value.ScrapLimit) :
                recovery ? Math.Max(0, (long)mission.Count - initial - fixedCount) : 0;
            return new MissionSupplySummary(initial, fixedCount, maintained, dynamic, recovery);
        }

        internal static ObstacleKind ViewKind(ElementDefinition definition) => definition.RequireReactionBehavior() switch
        {
            ElementReactionBehavior.GeneratorCharge => ObstacleKind.Generator,
            ElementReactionBehavior.Durability => definition.RequireRemovalMissionProfile().Kind switch
            {
                MissionKind.Crate => ObstacleKind.Crate, MissionKind.Scrap => ObstacleKind.Scrap, MissionKind.Safe => ObstacleKind.Safe,
                MissionKind.ColorLock => ObstacleKind.ColorLock, MissionKind.Appliance => ObstacleKind.Appliance,
                _ => throw new ArgumentException("표시할 수 없는 내구도 미션입니다.")
            },
            _ => throw new ArgumentException("등록하지 않은 본체 행동입니다.")
        };

        public void Dispose()
        { if (Application.isPlaying) UnityEngine.Object.Destroy(Level); else UnityEngine.Object.DestroyImmediate(Level); }
    }
}

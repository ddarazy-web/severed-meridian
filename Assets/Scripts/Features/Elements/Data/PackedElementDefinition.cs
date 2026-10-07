using System;
using System.Linq;
using Levels;
using MemoryPack;
using Simulation;

namespace Elements
{
    /// <summary>제작 원본 참조 없이 배포하는 명시적 프로필 값. 필드 순서는 팩 계약이다.</summary>
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedElementDefinition
    {
        [MemoryPackOrder(0)] public string id;
        [MemoryPackOrder(1)] public string displayName;
        [MemoryPackOrder(2)] public PackedElementPlacement placement;
        [MemoryPackOrder(3)] public PackedElementCharge charge;
        [MemoryPackOrder(4)] public PackedElementDamage damage;
        [MemoryPackOrder(5)] public PackedElementColor color;
        [MemoryPackOrder(6)] public PackedElementAggregation aggregation;
        [MemoryPackOrder(7)] public PackedElementRemoval removal;
        [MemoryPackOrder(8)] public int reaction;
        [MemoryPackOrder(9)] public PackedElementLayer layer;
        [MemoryPackOrder(10)] public PackedElementTurn turn;
        [MemoryPackOrder(11)] public PackedElementSupply supply;

        public ElementDefinition ToDefinition()
        {
            try
            {
                ElementDefinition result = new ElementDefinition(new ElementId(id), displayName,
                    placement == null || !placement.enabled ? null : new ElementPlacementProfile(placement.size, placement.maxDurability),
                    charge == null || !charge.enabled ? null : new ElementChargePlacementProfile(charge.size, charge.minRequired, charge.maxRequired, charge.perHit),
                    damage == null || !damage.enabled ? null : new ElementDamageSourcePolicy(damage.adjacentMatch, damage.power, damage.magnetAdjacent, damage.hammer),
                    color == null || !color.enabled ? null : new ElementColorMatchPolicy(color.requiresMatchingColor),
                    aggregation == null || !aggregation.enabled ? null : new ElementDamageAggregationPolicy(aggregation.perHitCell),
                    removal == null || !removal.enabled ? null : new ElementRemovalMissionProfile(removal.kind),
                    reaction == 0 ? null : (ElementReactionBehavior)reaction,
                    layer == null || !layer.enabled ? null : new ElementLayerProfile(layer.behavior, layer.mission, layer.damage),
                    turn == null || !turn.enabled ? null : new ElementTurnProfile(turn.behavior, turn.initialDurability),
                    supply == null || !supply.enabled ? null : new ElementSupplyProfile(supply.behavior, supply.content,
                        supply.hasObstacle ? supply.obstacle : null, string.IsNullOrEmpty(supply.bodyIdPrefix) ? null : supply.bodyIdPrefix, supply.choices,
                        string.IsNullOrEmpty(supply.obstacleDefinitionId) ? null : new ElementId(supply.obstacleDefinitionId),
                        supply.choiceDefinitionIds?.Select(value => new ElementId(value))));
                ValidateDefinition(result);
                return result;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            {
                throw new ArgumentException($"요소 정의 ID '{id ?? "<null>"}': {error.Message}", nameof(id), error);
            }
        }

        internal static void ValidateDefinition(ElementDefinition definition)
        {
            if (definition.ReactionBehavior.HasValue)
            {
                if (definition.Layer != null || definition.Turn != null || definition.Supply != null)
                    throw new ArgumentException("본체 반응과 층/공급 행동을 함께 지정할 수 없습니다.");
                definition.RequireDamageSourcePolicy();
                switch (definition.RequireReactionBehavior())
                {
                    case ElementReactionBehavior.Durability:
                    case ElementReactionBehavior.EvenTurnDurability:
                        definition.RequirePlacement(); definition.RequireDamageAggregationPolicy(); definition.RequireRemovalMissionProfile();
                        if (definition.ChargePlacement != null) throw new ArgumentException("내구도 행동에 충전 프로필이 있습니다.");
                        break;
                    case ElementReactionBehavior.GeneratorCharge:
                        definition.RequireChargePlacement();
                        if (definition.Placement != null || definition.RemovalMissionProfile != null || definition.DamageAggregationPolicy != null || definition.ColorMatchPolicy != null)
                            throw new ArgumentException("충전 행동에 내구도 전용 프로필이 있습니다.");
                        break;
                    default: throw new ArgumentException("등록하지 않은 반응 행동입니다.");
                }
            }
            else if (definition.Layer != null)
            {
                definition.RequirePlacement();
                if (definition.ChargePlacement != null || definition.DamageSourcePolicy != null || definition.ColorMatchPolicy != null ||
                    definition.DamageAggregationPolicy != null || definition.RemovalMissionProfile != null || definition.Supply != null)
                    throw new ArgumentException("층 행동에 본체/공급 전용 프로필이 있습니다.");
                if (definition.Turn != null && (definition.Layer.Behavior != ElementLayerBehavior.CoverRemoval ||
                    definition.Turn.InitialDurability > definition.Placement.MaxDurability))
                    throw new ArgumentException("번식 행동의 층/초기 내구도 조합이 잘못됐습니다.");
            }
            else if (definition.Supply != null)
            {
                if (definition.Placement != null || definition.ChargePlacement != null || definition.DamageSourcePolicy != null ||
                    definition.ColorMatchPolicy != null || definition.DamageAggregationPolicy != null || definition.RemovalMissionProfile != null || definition.Turn != null)
                    throw new ArgumentException("공급 행동에 본체/층 전용 프로필이 있습니다.");
            }
            else throw new ArgumentException("배치/반응/층/공급 행동 프로필이 없습니다.");
        }

        public static PackedElementDefinition FromDefinition(ElementDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return new PackedElementDefinition
            {
                id = definition.Id.Value, displayName = definition.DisplayName,
                placement = definition.Placement == null ? null : new PackedElementPlacement { enabled = true, size = definition.Placement.Size, maxDurability = definition.Placement.MaxDurability },
                charge = definition.ChargePlacement == null ? null : new PackedElementCharge { enabled = true, size = definition.ChargePlacement.Size, minRequired = definition.ChargePlacement.MinRequiredCharge, maxRequired = definition.ChargePlacement.MaxRequiredCharge, perHit = definition.ChargePlacement.ChargePerHit },
                damage = definition.DamageSourcePolicy == null ? null : new PackedElementDamage { enabled = true, adjacentMatch = definition.DamageSourcePolicy.AdjacentMatch, power = definition.DamageSourcePolicy.Power, magnetAdjacent = definition.DamageSourcePolicy.MagnetAdjacent, hammer = definition.DamageSourcePolicy.Hammer },
                color = definition.ColorMatchPolicy == null ? null : new PackedElementColor { enabled = true, requiresMatchingColor = definition.ColorMatchPolicy.RequiresMatchingColor },
                aggregation = definition.DamageAggregationPolicy == null ? null : new PackedElementAggregation { enabled = true, perHitCell = definition.DamageAggregationPolicy.PerHitCell },
                removal = definition.RemovalMissionProfile == null ? null : new PackedElementRemoval { enabled = true, kind = definition.RemovalMissionProfile.Kind },
                reaction = definition.ReactionBehavior.HasValue ? (int)definition.ReactionBehavior.Value : 0,
                layer = definition.Layer == null ? null : new PackedElementLayer { enabled = true, behavior = definition.Layer.Behavior, mission = definition.Layer.Mission, damage = definition.Layer.Damage },
                turn = definition.Turn == null ? null : new PackedElementTurn { enabled = true, behavior = definition.Turn.Behavior, initialDurability = definition.Turn.InitialDurability },
                supply = definition.Supply == null ? null : new PackedElementSupply { enabled = true, behavior = definition.Supply.Behavior, content = definition.Supply.Content, hasObstacle = definition.Supply.Obstacle.HasValue, obstacle = definition.Supply.Obstacle.GetValueOrDefault(), bodyIdPrefix = definition.Supply.BodyIdPrefix, choices = definition.Supply.Choices.ToArray(), obstacleDefinitionId = definition.Supply.ObstacleDefinitionId?.Value, choiceDefinitionIds = definition.Supply.ChoiceDefinitionIds.Select(id => id.Value).ToArray() }
            };
        }
    }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedElementPlacement
    {
        [MemoryPackOrder(0)] public int size = 1;
        [MemoryPackOrder(1)] public int maxDurability = 1;
        [MemoryPackOrder(2)] public bool enabled;
    }
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedElementCharge
    {
        [MemoryPackOrder(0)] public int size = 2;
        [MemoryPackOrder(1)] public int minRequired = 3;
        [MemoryPackOrder(2)] public int maxRequired = 5;
        [MemoryPackOrder(3)] public int perHit = 1;
        [MemoryPackOrder(4)] public bool enabled;
    }
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedElementDamage
    {
        [MemoryPackOrder(0)] public bool adjacentMatch;
        [MemoryPackOrder(1)] public bool power;
        [MemoryPackOrder(2)] public bool magnetAdjacent;
        [MemoryPackOrder(3)] public bool hammer;
        [MemoryPackOrder(4)] public bool enabled;
    }
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedElementColor { [MemoryPackOrder(0)] public bool requiresMatchingColor; [MemoryPackOrder(1)] public bool enabled; }
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedElementAggregation { [MemoryPackOrder(0)] public bool perHitCell; [MemoryPackOrder(1)] public bool enabled; }
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedElementRemoval { [MemoryPackOrder(0)] public MissionKind kind; [MemoryPackOrder(1)] public bool enabled; }
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedElementLayer
    {
        [MemoryPackOrder(0)] public ElementLayerBehavior behavior;
        [MemoryPackOrder(1)] public MissionKind mission;
        [MemoryPackOrder(2)] public int damage = 1;
        [MemoryPackOrder(3)] public bool enabled;
    }
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedElementTurn
    {
        [MemoryPackOrder(0)] public ElementTurnBehavior behavior;
        [MemoryPackOrder(1)] public int initialDurability = 1;
        [MemoryPackOrder(2)] public bool enabled;
    }
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedElementSupply
    {
        [MemoryPackOrder(0)] public ElementSupplyBehavior behavior;
        [MemoryPackOrder(1)] public RuntimeContent content;
        [MemoryPackOrder(2)] public bool hasObstacle;
        [MemoryPackOrder(3)] public ObstacleKind obstacle;
        [MemoryPackOrder(4)] public string bodyIdPrefix;
        [MemoryPackOrder(5)] public SupplyKind[] choices;
        [MemoryPackOrder(6)] public bool enabled;
        [MemoryPackOrder(7)] public string obstacleDefinitionId;
        [MemoryPackOrder(8)] public string[] choiceDefinitionIds;
    }
}

using Simulation;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class FixedObstacleVerification
    {
        private static LevelDefinition CreateFixture(int pair)
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, pair, RocketDirection.Horizontal, null);
            Obstacle(level, ObstacleKind.Safe, 3, C(9, 0));
            Obstacle(level, ObstacleKind.ColorLock, 3, C(9, 9), RabbitColor.Type3);
            Obstacle(level, ObstacleKind.Appliance, 9, C(7, 7));
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 2 }, new[] { C(7, 7), C(7, 8), C(8, 7), C(8, 8) });
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Safe + ",\"count\":1},{\"kind\":" + (int)MissionKind.ColorLock + ",\"count\":1},{\"kind\":" + (int)MissionKind.Appliance + ",\"count\":1},{\"kind\":" + (int)MissionKind.Dust + ",\"count\":4}]}", level);
            Check(LevelDefinitionValidator.Validate(level).Count == 0 && new StartConditionReport(Build(level)).IsSatisfied, "실제 고정장애물4미션/시작 검증 " + pair);
            return level;
        }
    }
}

using System.Linq;
using Simulation;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class MoldVerification
    {
        private static LevelDefinition CreateFixture(int pair)
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, pair, RocketDirection.Horizontal, null);
            Mold(level, C(9, 0), C(9, 9));
            Check(LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 2 }, new[] { C(4, 7) }).Changed == 1, "실제 거미줄 배치 " + pair);
            Check(LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 2 }, new[] { C(9, 0), C(9, 9), C(4, 7) }).Changed == 3, "실제 먼지 중첩 " + pair);
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":100},{\"kind\":8,\"count\":0},{\"kind\":2,\"count\":1},{\"kind\":4,\"count\":3}]}", level);
            Check(LevelDefinitionValidator.Validate(level).Count == 0 && new StartConditionReport(Build(level)).IsSatisfied, "실제 곰팡이4미션/시작 검증 " + pair);
            return level;
        }
    }
}

using System;
using System.Linq;
using Board;
using Simulation;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class ScrapVerification
    {
        private static LevelDefinition CreateFixture(int pair)
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, pair, RocketDirection.Horizontal, null);
            Place(level, C(4, 6), 3);
            Check(LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 2 }, new[] { C(4, 7) }).Changed == 1, "실제 편집 거미줄 배치 " + pair);
            Check(LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 2 }, new[] { C(4, 6), C(4, 7), C(4, 8) }).Changed == 3, "실제 편집 먼지 중첩 배치 " + pair);
            if (pair == 1)
            {
                Fixed(level, C(0, 4), new SupplyItem(SupplyKind.Scrap, 2, durability: 2), new SupplyItem(SupplyKind.Bomb));
                Fixed(level, C(0, 5), new SupplyItem(SupplyKind.FixedNormal), new SupplyItem(SupplyKind.Scrap, durability: 5));
            }
            else Maintain(level, new[] { C(0, 4), C(0, 5) }, 3, 6, 4);
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":100},{\"kind\":" + (int)MissionKind.Scrap + ",\"count\":3},{\"kind\":2,\"count\":1},{\"kind\":4,\"count\":3}]}", level);
            Check(LevelDefinitionValidator.Validate(level).Count == 0 && new StartConditionReport(Build(level)).IsSatisfied, "유효 4미션/고철 공급 및 시작 검증 " + pair);
            return level;
        }
    }
}

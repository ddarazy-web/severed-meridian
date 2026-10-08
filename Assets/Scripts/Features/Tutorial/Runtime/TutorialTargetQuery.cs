using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Elements;
using Levels;
using Simulation;

namespace Tutorial
{
    public enum TutorialTargetLayer { Content, Cover, Floor }

    /// <summary>한 시점의 실제 개체. 2×2 점유 칸은 하나의 본체에 속한다.</summary>
    public sealed class TutorialTargetEntity
    {
        public long Occurrence { get; }
        public ElementDefinition Definition { get; }
        public TutorialTargetLayer Layer { get; }
        public ReadOnlyCollection<BoardCoordinate> Cells { get; }
        public int? Durability { get; }
        public RocketDirection? RocketDirection { get; }
        internal TutorialTargetEntity(long occurrence, ElementDefinition definition, TutorialTargetLayer layer,
            IEnumerable<BoardCoordinate> cells, int? durability, RocketDirection? direction = null)
        {
            Occurrence = occurrence; Definition = definition; Layer = layer;
            Cells = Array.AsReadOnly(cells.ToArray()); Durability = durability; RocketDirection = direction;
        }
    }

    /// <summary>판정/안내가 같은 실제 식별자를 사용하도록 보드 상태를 읽는다. 보드나 난수를 변경하지 않는다.</summary>
    public static class TutorialTargetQuery
    {
        public static IReadOnlyList<TutorialTargetEntity> Capture(LevelRuntimeState state)
        {
            List<TutorialTargetEntity> result = new List<TutorialTargetEntity>();
            HashSet<long> bodies = new HashSet<long>();
            foreach (RuntimeCell cell in state.Cells)
            {
                if (!cell.IsActive) continue;
                if (cell.Content == RuntimeContent.Obstacle && cell.ObstacleIndex.HasValue)
                {
                    RuntimeObstacle body = state.Obstacles[cell.ObstacleIndex.Value];
                    if (bodies.Add(body.Occurrence))
                        result.Add(new TutorialTargetEntity(body.Occurrence, body.Element, TutorialTargetLayer.Content,
                            state.Cells.Where(value => value.IsActive && value.Content == RuntimeContent.Obstacle && value.ObstacleIndex == cell.ObstacleIndex).Select(value => value.Coordinate),
                            body.Element.Placement != null ? body.Durability : (int?)null));
                }
                else if (cell.Content != RuntimeContent.Empty)
                    result.Add(new TutorialTargetEntity(cell.ContentOccurrence,
                        cell.ContentElement ?? LegacyElementDefinitions.GetContent(cell.Content, state.ElementCatalog),
                        TutorialTargetLayer.Content, new[] { cell.Coordinate }, null, cell.RocketDirection));
                if (cell.Cover.HasValue)
                    result.Add(new TutorialTargetEntity(cell.CoverOccurrence, cell.CoverElement ?? LegacyElementDefinitions.Get(cell.Cover.Value),
                        TutorialTargetLayer.Cover, new[] { cell.Coordinate }, cell.CoverDurability));
                if (cell.DustDurability > 0)
                    result.Add(new TutorialTargetEntity(cell.DustOccurrence, cell.DustElement ?? LegacyElementDefinitions.GetDust(),
                        TutorialTargetLayer.Floor, new[] { cell.Coordinate }, cell.DustDurability));
            }
            return result.AsReadOnly();
        }
    }
}

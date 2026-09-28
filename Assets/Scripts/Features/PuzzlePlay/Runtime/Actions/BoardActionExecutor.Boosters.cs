using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    public enum StartBooster { Rocket, Bomb, Magnet }

    public sealed class BoosterPlacement
    {
        public StartBooster Booster { get; }
        public BoardCoordinate Coordinate { get; }
        public RocketDirection? Direction { get; }
        public int Turn { get; }
        public int RandomBefore { get; }
        public int RandomAfter { get; }
        internal BoosterPlacement(StartBooster booster, RuntimeCell cell, int turn, int before, int after)
        { Booster = booster; Coordinate = cell.Coordinate; Direction = cell.RocketDirection; Turn = turn; RandomBefore = before; RandomAfter = after; }
    }

    public sealed partial class BoardActionExecutor
    {
        private readonly List<StartBooster> pendingBoosters = new List<StartBooster>();
        private readonly List<BoosterPlacement> boosterPlacements = new List<BoosterPlacement>();
        public ReadOnlyCollection<StartBooster> PendingBoosters => pendingBoosters.AsReadOnly();
        public ReadOnlyCollection<BoosterPlacement> BoosterPlacements => boosterPlacements.AsReadOnly();

        private void InitializeBoosters(IEnumerable<StartBooster> boosters)
        {
            if (boosters == null) throw new ArgumentNullException(nameof(boosters));
            StartBooster[] selected = boosters.ToArray();
            if (selected.Any(b => b < StartBooster.Rocket || b > StartBooster.Magnet) || selected.Distinct().Count() != selected.Length)
                throw new ArgumentException("시작 부스터는 지원하는 세 종류에서 각각 한 번만 선택하세요.", nameof(boosters));
            pendingBoosters.AddRange(selected.OrderBy(b => b));
        }

        private void PlacePendingBoosters()
        {
            if (Outcome != null || pendingBoosters.Count == 0) return;
            List<RuntimeCell> candidates = State.Cells.Where(c => c.IsActive && c.Content == RuntimeContent.Normal && !c.Cover.HasValue).ToList();
            while (pendingBoosters.Count > 0 && candidates.Count > 0)
            {
                int before = State.Random.DrawCount;
                int index = candidates.Count == 1 ? 0 : State.Random.Next(candidates.Count);
                RuntimeCell cell = candidates[index]; candidates.RemoveAt(index);
                StartBooster booster = pendingBoosters[0]; pendingBoosters.RemoveAt(0);
                cell.Content = booster switch { StartBooster.Rocket => RuntimeContent.Rocket, StartBooster.Bomb => RuntimeContent.Bomb, _ => RuntimeContent.Magnet };
                cell.Color = null;
                cell.RocketDirection = booster == StartBooster.Rocket ? (State.Random.Next(2) == 0 ? RocketDirection.Horizontal : RocketDirection.Vertical) : (RocketDirection?)null;
                boosterPlacements.Add(new BoosterPlacement(booster, cell, Turn, before, State.Random.DrawCount));
            }
        }
    }
}

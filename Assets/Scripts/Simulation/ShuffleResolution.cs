using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulation
{
    public enum ShuffleReason { Applied, Impossible, LimitReached }

    public sealed class ShuffleResult
    {
        public ShuffleReason Reason { get; }
        public int Attempts { get; }
        public int RandomBefore { get; }
        public int RandomAfter { get; }
        public LevelRuntimeState State { get; }
        internal ShuffleResult(ShuffleReason reason, int attempts, LevelRuntimeState original, LevelRuntimeState work)
        { Reason = reason; Attempts = attempts; RandomBefore = original.Random.DrawCount; RandomAfter = work.Random.DrawCount; State = reason == ShuffleReason.Applied ? work : null; }
    }

    public static class ShuffleResolution
    {
        public const string Version = "shuffle-v1";
        public const int RandomLimit = 256;
        public const int ExactLimit = 4096;
        public static ShuffleResult Resolve(LevelRuntimeState original)
        {
            LevelRuntimeState work = new LevelRuntimeState(original);
            RuntimeCell[] cells = work.Cells.Where(c => c.IsActive && !c.Cover.HasValue && c.Content >= RuntimeContent.Normal && c.Content <= RuntimeContent.Magnet).ToArray();
            RuntimeCell[] contents = cells.Select(c => c.Copy()).ToArray();
            int attempts = 0;
            bool Valid() => MatchQuery.Find(work).Count == 0 && ActionQuery.Find(work).Count > 0;
            void Assign(int destination, int source)
            {
                cells[destination].Content = contents[source].Content;
                cells[destination].Color = contents[source].Color;
                cells[destination].RocketDirection = contents[source].RocketDirection;
            }
            string Key(RuntimeCell cell) => cell.Content + ":" + cell.Color + ":" + cell.RocketDirection;
            if (cells.Length < 2 || contents.Select(Key).Distinct().Count() <= 1)
                return new ShuffleResult(Valid() ? ShuffleReason.Applied : ShuffleReason.Impossible, 1, original, work);
            int[] order = Enumerable.Range(0, cells.Length).ToArray();
            for (int attempt = 0; attempt < RandomLimit; attempt++)
            {
                for (int i = order.Length - 1; i > 0; i--) { int other = work.Random.Next(i + 1); (order[i], order[other]) = (order[other], order[i]); }
                for (int i = 0; i < cells.Length; i++) Assign(i, order[i]);
                attempts++;
                if (Valid()) return new ShuffleResult(ShuffleReason.Applied, attempts, original, work);
            }
            // 작은 배치만 중복 없는 순열로 불가능을 증명한다. 큰 보드는 한도를 불가능으로 오인하지 않는다.
            if (cells.Length > 8) return new ShuffleResult(ShuffleReason.LimitReached, attempts, original, work);
            bool[] used = new bool[cells.Length]; bool limited = false; int exact = 0;
            bool Search(int depth)
            {
                if (depth == cells.Length)
                {
                    if (exact >= ExactLimit) { limited = true; return false; }
                    exact++; attempts++; return Valid();
                }
                HashSet<string> seen = new HashSet<string>();
                for (int i = 0; i < cells.Length; i++)
                {
                    if (used[i] || !seen.Add(Key(contents[i]))) continue;
                    used[i] = true; Assign(depth, i);
                    if (Search(depth + 1)) return true;
                    used[i] = false; if (limited) return false;
                }
                return false;
            }
            bool found = Search(0);
            return new ShuffleResult(found ? ShuffleReason.Applied : limited ? ShuffleReason.LimitReached : ShuffleReason.Impossible, attempts, original, work);
        }
    }
}

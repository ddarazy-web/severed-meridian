using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    public enum MoldSpreadReason { NoTurn, MissionsComplete, RemovedThisTurn, NoMold, NoCandidate, Spread }

    public sealed class MoldSpreadRecord
    {
        public MoldSpreadReason Reason { get; }
        public BoardCoordinate? Target { get; }
        public int CandidateCount { get; }
        public int RandomBefore { get; }
        public int RandomAfter { get; }
        public string Message => Reason switch
        {
            MoldSpreadReason.NoTurn => "이동 수 소비 없음 · 확산 생략",
            MoldSpreadReason.MissionsComplete => "모든 미션 달성 · 확산 생략",
            MoldSpreadReason.RemovedThisTurn => "이번 수 곰팡이 제거 · 확산 생략",
            MoldSpreadReason.NoMold => "남은 곰팡이 없음",
            MoldSpreadReason.NoCandidate => "확산 가능한 칸 없음",
            _ => "곰팡이 확산 " + Target
        };
        internal MoldSpreadRecord(MoldSpreadReason reason, int before, int after, int count = 0, BoardCoordinate? target = null)
        { Reason = reason; RandomBefore = before; RandomAfter = after; CandidateCount = count; Target = target; }
    }

    internal static class MoldRules
    {
        internal static void Remove(LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context)
        {
            cell.Cover = null; cell.CoverDurability = 0; context.RemovedMold = true;
            MissionProgressRules.Complete(state, MissionKind.Mold, cell.Coordinate);
        }

        // 종료 단계의 작업 사본에서만 호출한다. 같은 턴 기록이 있으면 난수도 다시 쓰지 않는다.
        internal static MoldSpreadRecord FinishTurn(LevelRuntimeState state, TurnEffectContext context)
        {
            if (context.MoldSpread != null) return context.MoldSpread;
            int before = state.Random.DrawCount;
            MoldSpreadRecord Record(MoldSpreadReason reason, int count = 0, BoardCoordinate? target = null)
                => context.MoldSpread = new MoldSpreadRecord(reason, before, state.Random.DrawCount, count, target);
            if (!context.ConsumesMove) return Record(MoldSpreadReason.NoTurn);
            if (state.Missions.Count > 0 && state.Missions.All(m => m.Remaining == 0)) return Record(MoldSpreadReason.MissionsComplete);
            if (context.RemovedMold) return Record(MoldSpreadReason.RemovedThisTurn);
            RuntimeCell[] molds = state.Cells.Where(c => c.IsActive && c.Cover == CoverKind.Mold).ToArray();
            if (molds.Length == 0) return Record(MoldSpreadReason.NoMold);
            RuntimeCell[] candidates = state.Cells.Where(c => c.IsActive && !c.Cover.HasValue &&
                c.Content >= RuntimeContent.Normal && c.Content <= RuntimeContent.Magnet &&
                molds.Any(m => new BoardEdge(m.Coordinate, c.Coordinate).IsAdjacent &&
                    !state.Flow.Walls.Contains(new BoardEdge(m.Coordinate, c.Coordinate))))
                .OrderBy(c => c.Coordinate.Row).ThenBy(c => c.Coordinate.Column).ToArray();
            if (candidates.Length == 0) return Record(MoldSpreadReason.NoCandidate);
            RuntimeCell selected = candidates[candidates.Length == 1 ? 0 : state.Random.Next(candidates.Length)];
            selected.Cover = CoverKind.Mold; selected.CoverDurability = 1;
            foreach (RuntimeMission mission in state.Missions)
                if (mission.Definition.Kind == MissionKind.Mold) mission.Target++;
            return Record(MoldSpreadReason.Spread, candidates.Length, selected.Coordinate);
        }
    }
}

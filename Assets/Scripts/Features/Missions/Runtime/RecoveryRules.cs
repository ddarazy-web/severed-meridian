using System;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    public sealed class RecoveryRecord
    {
        public BoardCoordinate Coordinate { get; }
        public int Turn { get; }
        public int Batch { get; }
        internal RecoveryRecord(BoardCoordinate coordinate, int turn, int batch)
        { Coordinate = coordinate; Turn = turn; Batch = batch; }
    }

    public static class RecoveryRules
    {
        public const string Version = "recovery-v1";
        public static int Remaining(LevelRuntimeState state) => state.Missions.Where(m => m.Definition.Kind == MissionKind.Recovery)
            .Select(m => m.Remaining).DefaultIfEmpty(0).Max();
        public static int OnBoard(LevelRuntimeState state) => state.Cells.Count(c => c.Content == RuntimeContent.Recovery);
        public static int Needed(LevelRuntimeState state) => Math.Max(0, Math.Min(state.Supply.RecoveryTarget, Remaining(state)) - OnBoard(state));

        // 다음 이동의 방해 요소만 예측한다. 길을 열어도 회수 완료를 보장하지 않는다.
        internal static MissionContribution Query(LevelRuntimeState state, BoardCoordinate target, DamageReaction reaction, int mission)
        {
            if (reaction.Response != DamageResponse.Remove && reaction.Response != DamageResponse.Damage && reaction.Response != DamageResponse.CoverDamage && reaction.Response != DamageResponse.Charge)
                return null;
            MovementCandidate[] blocked = MovementQuery.Find(state).Concat(MovementQuery.Find(state, true))
                .Where(m => m.Reason == MovementReason.Occupied && m.Target.Equals(target) &&
                    state.CellAt(m.Source).Content == RuntimeContent.Recovery).ToArray();
            if (blocked.Length == 0) return null;
            LevelRuntimeState opened = new LevelRuntimeState(state);
            RuntimeCell cell = opened.CellAt(target);
            cell.Content = RuntimeContent.Empty; cell.Color = null; cell.RocketDirection = null;
            cell.ObstacleIndex = null; cell.Cover = null; cell.CoverDurability = 0;
            MovementCandidate[] primary = MovementQuery.Find(opened).Where(m => m.IsAllowed).ToArray();
            foreach (MovementCandidate candidate in blocked)
            {
                MovementCandidate allowed = (candidate.Kind == MovementKind.Diagonal ?
                    primary.Length == 0 ? MovementQuery.Find(opened, true).Where(m => m.IsAllowed) : Enumerable.Empty<MovementCandidate>() : primary)
                    .FirstOrDefault(m => m.Source.Equals(candidate.Source) && m.Target.Equals(target));
                if (allowed == null) continue;
                RuntimeMerge merge = opened.Flow.Merges.FirstOrDefault(m => m.Coordinate.Equals(target));
                if (allowed.Kind != MovementKind.Diagonal && merge != null && primary.Any(m => m.Target.Equals(target) &&
                    merge.Sources.IndexOf(m.Source) < merge.Sources.IndexOf(allowed.Source))) continue;
                return new MissionContribution(mission, 0, reaction.Response == DamageResponse.Charge ? 0 : Math.Max(1, reaction.Amount),
                    bodyIndex: state.CellAt(target).ObstacleIndex, charge: reaction.Response == DamageResponse.Charge ? 1 : 0);
            }
            return null;
        }

        // 점유 해제와 실제 미션 집계는 같은 작업 사본 안에서만 수행한다.
        internal static int Collect(LevelRuntimeState state, int turn, int batch)
        {
            int count = 0;
            foreach (BoardCoordinate coordinate in state.Flow.Arrivals.Distinct())
            {
                if (!MovementQuery.Active(state, coordinate)) continue;
                RuntimeCell cell = state.CellAt(coordinate);
                if (cell.Content != RuntimeContent.Recovery) continue;
                cell.Content = RuntimeContent.Empty; cell.Color = null; cell.RocketDirection = null; cell.ObstacleIndex = null;
                MissionProgressRules.Complete(state, MissionKind.Recovery, coordinate);
                state.RecordRecovery(new RecoveryRecord(coordinate, turn, batch)); count++;
            }
            return count;
        }
    }
}

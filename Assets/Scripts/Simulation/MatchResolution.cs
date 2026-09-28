using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    public sealed class MatchDecision
    {
        public MatchPattern Selected { get; }
        public ReadOnlyCollection<MatchPattern> Excluded { get; }
        public BoardCoordinate? Spawn { get; }
        public string Reason { get; }
        internal MatchDecision(MatchPattern selected, IEnumerable<MatchPattern> excluded, BoardCoordinate? spawn, string reason)
        { Selected = selected; Excluded = Array.AsReadOnly(excluded.ToArray()); Spawn = spawn; Reason = reason; }
    }

    public static class MatchResolution
    {
        public const string Version = "match-resolution-hit-groups-v4";

        // 조회는 이 함수를 호출하지 않는다. 실행 작업 사본의 난수로 동률을 한 번만 해소한다.
        internal static ReadOnlyCollection<MatchDecision> Select(IEnumerable<MatchPattern> patterns, BoardCoordinate first, BoardCoordinate second, SimulationRandom random)
            => SelectCore(patterns, random, selected =>
            {
                if (selected.Cells.Contains(second)) return (second, "교환 도착 칸");
                if (selected.Cells.Contains(first)) return (first, "교환 도착 칸");
                throw new InvalidOperationException("선택 패턴에 교환 도착 칸이 없습니다.");
            });

        internal static ReadOnlyCollection<MatchDecision> SelectAutomatic(IEnumerable<MatchPattern> patterns, TurnEffectContext context, SimulationRandom random)
            => SelectCore(patterns, random, selected =>
            {
                long latest = selected.Cells.Max(context.LastArrival);
                BoardCoordinate[] candidates = selected.Cells.Where(c => context.LastArrival(c) == latest).OrderBy(c => c.Row).ThenBy(c => c.Column).ToArray();
                BoardCoordinate spawn = candidates[candidates.Length == 1 ? 0 : random.Next(candidates.Length)];
                return (spawn, "최종 도착 " + (latest >> 32) + ":" + (uint)latest + " / 동시 후보 " + candidates.Length);
            });

        private static ReadOnlyCollection<MatchDecision> SelectCore(IEnumerable<MatchPattern> patterns, SimulationRandom random,
            Func<MatchPattern, (BoardCoordinate coordinate, string reason)> chooseSpawn)
        {
            List<MatchPattern> pending = patterns.GroupBy(pattern => pattern.Key).Select(group => group.First())
                .OrderBy(pattern => pattern.Cells[0].Row).ThenBy(pattern => pattern.Cells[0].Column).ThenBy(pattern => pattern.Key, StringComparer.Ordinal).ToList();
            List<MatchDecision> decisions = new List<MatchDecision>();
            while (pending.Count > 0)
            {
                List<MatchPattern> group = new List<MatchPattern> { pending[0] }; pending.RemoveAt(0);
                for (int index = 0; index < group.Count; index++)
                    for (int i = 0; i < pending.Count;)
                    {
                        if (pending[i].Cells.Any(group[index].Cells.Contains)) { group.Add(pending[i]); pending.RemoveAt(i); }
                        else i++;
                    }
                MatchKind priority = group.Max(pattern => pattern.Kind);
                int count = group.Where(pattern => pattern.Kind == priority).Max(pattern => pattern.Cells.Count);
                MatchPattern[] tied = group.Where(pattern => pattern.Kind == priority && pattern.Cells.Count == count).OrderBy(pattern => pattern.Key, StringComparer.Ordinal).ToArray();
                MatchPattern selected = tied[tied.Length == 1 ? 0 : random.Next(tied.Length)];
                BoardCoordinate? spawn = null;
                string spawnReason = "";
                if (selected.Kind != MatchKind.Three)
                {
                    var choice = chooseSpawn(selected); spawn = choice.coordinate; spawnReason = " / " + choice.reason;
                }
                string reason = tied.Length > 1 ? "동일 우선순위·블록 수 후보 " + tied.Length + "개 중 무작위 선택" : group.Count == 1 ? "독립 패턴" : "파워 우선순위와 블록 수로 선택";
                decisions.Add(new MatchDecision(selected, group.Where(pattern => pattern != selected), spawn, reason + spawnReason));
            }
            return decisions.AsReadOnly();
        }

        // 선택을 끝낸 패턴만 작업 사본에 적용한다. 수 차감과 낙하 대기 전환은 실행기가 담당한다.
        internal static ReadOnlyCollection<MatchedBlockChange> Apply(LevelRuntimeState work, IEnumerable<MatchDecision> decisions, int turn)
            => ApplyLayers(work, ResolveCoveredSpawn(work, decisions), turn, new TurnEffectContext(turn, Array.Empty<MatchedBlockChange>()));

        internal static ReadOnlyCollection<MatchDecision> ResolveCoveredSpawn(LevelRuntimeState work, IEnumerable<MatchDecision> decisions)
        {
            List<MatchDecision> resolved = new List<MatchDecision>();
            foreach (MatchDecision decision in decisions)
            {
                if (!decision.Spawn.HasValue || work.CellAt(decision.Spawn.Value).Cover != CoverKind.Web) { resolved.Add(decision); continue; }
                BoardCoordinate origin = decision.Spawn.Value;
                BoardCoordinate[] open = decision.Selected.Cells.Where(c => !work.CellAt(c).Cover.HasValue).ToArray();
                if (open.Length == 0)
                { resolved.Add(new MatchDecision(decision.Selected, decision.Excluded, null, decision.Reason + " / 노출 칸 없음 · 생성 생략")); continue; }
                int distance = open.Min(c => Math.Abs(c.Row - origin.Row) + Math.Abs(c.Column - origin.Column));
                BoardCoordinate[] nearest = open.Where(c => Math.Abs(c.Row - origin.Row) + Math.Abs(c.Column - origin.Column) == distance).ToArray();
                resolved.Add(new MatchDecision(decision.Selected, decision.Excluded, nearest[nearest.Length == 1 ? 0 : work.Random.Next(nearest.Length)],
                    decision.Reason + " / 거미줄 회피 최단거리 " + distance));
            }
            return resolved.AsReadOnly();
        }

        internal static ReadOnlyCollection<MatchedBlockChange> ApplyLayers(LevelRuntimeState work, IEnumerable<MatchDecision> decisions, int turn, TurnEffectContext context)
        {
            List<MatchedBlockChange> changes = new List<MatchedBlockChange>();
            foreach (MatchDecision decision in decisions)
            {
                int hit = context.NextHit();
                foreach (BoardCoordinate coordinate in decision.Selected.Cells)
                {
                    RuntimeCell cell = work.CellAt(coordinate);
                    if (cell.Cover == CoverKind.Web)
                    {
                        int before = cell.CoverDurability;
                        WebRules.Apply(work, cell, context);
                        changes.Add(new MatchedBlockChange(coordinate, cell.Color.Value, cell.Content, null, turn, false, before, cell.CoverDurability) { HitGroup = hit });
                        continue;
                    }
                    bool spawn = decision.Spawn.HasValue && decision.Spawn.Value.Equals(coordinate);
                    RuntimeContent content = !spawn ? RuntimeContent.Empty : decision.Selected.Kind switch
                    { MatchKind.Drone => RuntimeContent.Drone, MatchKind.Rocket => RuntimeContent.Rocket, MatchKind.Bomb => RuntimeContent.Bomb, _ => RuntimeContent.Magnet };
                    RocketDirection? direction = spawn ? decision.Selected.RocketDirection : null;
                    changes.Add(new MatchedBlockChange(coordinate, cell.Color.Value, content, direction, turn) { HitGroup = hit });
                    DustRules.ConsumeNormal(work, cell, context);
                    cell.Content = content; cell.Color = null; cell.RocketDirection = direction; cell.ObstacleIndex = null;
                }
            }
            return changes.AsReadOnly();
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    public enum DamageCause { AdjacentMatch, Power, MagnetAdjacent, Hammer }
    public enum DamageResponse { None, Remove, Activate, Damage, Protected, AlreadyDamaged, Wall, Unsupported, CoverDamage, Charge }

    // 한 턴의 효과 처리 기록이다. 낙하 대기에서는 유지하며 조회만으로 기록을 추가하지 않는다.
    public sealed class TurnEffectContext
    {
        private readonly HashSet<BoardCoordinate> protectedPowers = new HashSet<BoardCoordinate>();
        private readonly HashSet<BoardCoordinate> firedPowers = new HashSet<BoardCoordinate>();
        private readonly HashSet<int> damagedObstacles = new HashSet<int>();
        private readonly HashSet<int> chargedGenerators = new HashSet<int>();
        private readonly List<GeneratorRecord> generators = new List<GeneratorRecord>();
        public System.Collections.ObjectModel.ReadOnlyCollection<GeneratorRecord> Generators => generators.AsReadOnly();
        public bool HasCharged(int index) => chargedGenerators.Contains(index);
        internal void RegisterCharge(int index) => chargedGenerators.Add(index);
        internal void RecordGenerator(GeneratorRecord record) => generators.Add(record);
        private readonly HashSet<BoardCoordinate> damagedWebs = new HashSet<BoardCoordinate>();
        private readonly HashSet<BoardCoordinate> damagedDust = new HashSet<BoardCoordinate>();
        private readonly HashSet<string> processedPatterns = new HashSet<string>();
        public System.Collections.ObjectModel.ReadOnlyCollection<string> ProcessedPatterns => processedPatterns.OrderBy(p => p, System.StringComparer.Ordinal).ToList().AsReadOnly();
        private readonly Dictionary<BoardCoordinate, long> arrivals = new Dictionary<BoardCoordinate, long>();
        private readonly List<TargetingRecord> targeting = new List<TargetingRecord>();
        public System.Collections.ObjectModel.ReadOnlyCollection<TargetingRecord> Targeting => targeting.AsReadOnly();
        public PowerCombination Combination { get; internal set; }
        public PowerPresentationTrace PowerTrace { get; internal set; }
        private int targetRequest;
        private int lastHit;
        private readonly HashSet<(int hit, BoardCoordinate cell)> hitCells = new HashSet<(int, BoardCoordinate)>();
        public int LastHit => lastHit;
        internal int NextHit() => ++lastHit;
        internal bool HasHit(int hit, BoardCoordinate cell) => hit > 0 && hitCells.Contains((hit, cell));
        internal void RegisterHit(int hit, BoardCoordinate cell) => hitCells.Add((hit, cell));
        internal int NextTargetRequest() => ++targetRequest;
        internal void RecordTargeting(TargetingRecord record) => targeting.Add(record);
        public int Turn { get; }
        public bool ConsumesMove { get; internal set; }
        public bool RemovedMold { get; internal set; }
        public MoldSpreadRecord MoldSpread { get; internal set; }
        public int SettlementCount { get; private set; }
        public long LastArrival(BoardCoordinate coordinate) => arrivals.TryGetValue(coordinate, out long stamp) ? stamp : 0;
        internal TurnEffectContext(int turn, IEnumerable<MatchedBlockChange> changes)
        {
            Turn = turn; ConsumesMove = turn > 0;
            Protect(changes);
        }
        public bool IsProtected(BoardCoordinate coordinate) => protectedPowers.Contains(coordinate);
        public bool HasFired(BoardCoordinate coordinate) => firedPowers.Contains(coordinate);
        public bool HasDamaged(int obstacleIndex) => damagedObstacles.Contains(obstacleIndex);
        public bool HasDamagedWeb(BoardCoordinate coordinate) => damagedWebs.Contains(coordinate);
        public bool HasDamagedDust(BoardCoordinate coordinate) => damagedDust.Contains(coordinate);
        internal void RegisterWeb(BoardCoordinate coordinate) => damagedWebs.Add(coordinate);
        internal void RegisterDust(BoardCoordinate coordinate) => damagedDust.Add(coordinate);
        private string PatternStamp(MatchPattern pattern) => pattern.Key + "|" + string.Join(",", pattern.Cells.Select(LastArrival));
        internal bool WasProcessed(MatchPattern pattern) => processedPatterns.Contains(PatternStamp(pattern));
        internal void RecordPatterns(IEnumerable<MatchDecision> decisions)
        {
            foreach (MatchDecision decision in decisions)
                foreach (MatchPattern pattern in decision.Excluded.Concat(new[] { decision.Selected })) processedPatterns.Add(PatternStamp(pattern));
        }
        internal TurnEffectContext NextTurn(int turn)
        {
            // 피해·파워 보호는 새 턴에 초기화하되, 그대로 남은 매칭은 다음 수에도 새 매칭이 아니다.
            TurnEffectContext next = new TurnEffectContext(turn, System.Array.Empty<MatchedBlockChange>());
            next.SettlementCount = SettlementCount;
            next.processedPatterns.UnionWith(processedPatterns);
            foreach (KeyValuePair<BoardCoordinate, long> arrival in arrivals) next.arrivals.Add(arrival.Key, arrival.Value);
            return next;
        }
        internal void RecordSwap(LevelRuntimeState state, BoardCoordinate first, BoardCoordinate second)
        {
            SettlementCount++;
            arrivals.Remove(first); arrivals.Remove(second);
            foreach (BoardCoordinate coordinate in new[] { first, second })
                if (state.CellAt(coordinate).Content == RuntimeContent.Normal) arrivals[coordinate] = (long)SettlementCount << 32;
        }
        internal void RegisterFire(BoardCoordinate coordinate) => firedPowers.Add(coordinate);
        internal void ReleaseLastPangPowers() { protectedPowers.Clear(); firedPowers.Clear(); }
        internal void ResetAfterShuffle() { protectedPowers.Clear(); firedPowers.Clear(); arrivals.Clear(); processedPatterns.Clear(); }
        internal void RegisterDamage(int obstacleIndex) => damagedObstacles.Add(obstacleIndex);
        internal void Protect(IEnumerable<MatchedBlockChange> changes)
        {
            foreach (MatchedBlockChange change in changes)
                if (change.IsTransformation) protectedPowers.Add(change.Coordinate);
        }
        internal TurnEffectContext Copy()
        {
            TurnEffectContext copy = new TurnEffectContext(Turn, System.Array.Empty<MatchedBlockChange>());
            copy.ConsumesMove = ConsumesMove;
            copy.protectedPowers.UnionWith(protectedPowers); copy.damagedObstacles.UnionWith(damagedObstacles);
            copy.chargedGenerators.UnionWith(chargedGenerators); copy.generators.AddRange(generators);
            copy.firedPowers.UnionWith(firedPowers); copy.SettlementCount = SettlementCount;
            copy.damagedWebs.UnionWith(damagedWebs); copy.damagedDust.UnionWith(damagedDust);
            copy.processedPatterns.UnionWith(processedPatterns);
            copy.targeting.AddRange(targeting);
            copy.targetRequest = targetRequest;
            copy.lastHit = lastHit; copy.hitCells.UnionWith(hitCells);
            copy.Combination = Combination;
            copy.PowerTrace = PowerTrace;
            copy.RemovedMold = RemovedMold; copy.MoldSpread = MoldSpread;
            foreach (KeyValuePair<BoardCoordinate, long> arrival in arrivals) copy.arrivals.Add(arrival.Key, arrival.Value);
            return copy;
        }
        internal TurnEffectContext CopyForFall()
        {
            TurnEffectContext copy = Copy(); copy.SettlementCount++;
            // 발동한 파워는 이미 소모됐다. 그 좌표로 들어올 다른 파워까지 발동 금지하지 않는다.
            copy.firedPowers.Clear();
            return copy;
        }
        internal void RecordArrival(BoardCoordinate source, BoardCoordinate target, int batch, bool normal)
        {
            arrivals.Remove(source); arrivals.Remove(target);
            if (normal) arrivals[target] = ((long)SettlementCount << 32) | (uint)batch;
        }
        internal void ForgetRemoved(LevelRuntimeState state)
        {
            List<BoardCoordinate> removed = new List<BoardCoordinate>();
            foreach (BoardCoordinate coordinate in arrivals.Keys)
                if (state.CellAt(coordinate).Content != RuntimeContent.Normal) removed.Add(coordinate);
            foreach (BoardCoordinate coordinate in removed) arrivals.Remove(coordinate);
        }
        internal void MoveProtection(IEnumerable<MovementCandidate> moves)
        {
            Dictionary<BoardCoordinate, BoardCoordinate> destinations = new Dictionary<BoardCoordinate, BoardCoordinate>();
            foreach (MovementCandidate move in moves) destinations.Add(move.Source, move.Target);
            HashSet<BoardCoordinate> updated = new HashSet<BoardCoordinate>();
            foreach (BoardCoordinate coordinate in protectedPowers)
                updated.Add(destinations.TryGetValue(coordinate, out BoardCoordinate next) ? next : coordinate);
            protectedPowers.Clear(); protectedPowers.UnionWith(updated);
        }
    }

    public sealed class DamageReaction
    {
        public DamageResponse Response { get; }
        public int Amount { get; }
        public string Message { get; }
        internal DamageReaction(DamageResponse response, string message, int amount = 0)
        { Response = response; Message = message; Amount = amount; }

        public static DamageReaction Evaluate(LevelRuntimeState state, BoardCoordinate target, DamageCause cause,
            BoardCoordinate source, TurnEffectContext context = null, RabbitColor? sourceColor = null, int hit = 0)
        {
            BoardQueryView view = new BoardQueryView(state);
            if (!view.Contains(target) || !state.CellAt(target).IsActive) return new DamageReaction(DamageResponse.None, "보드 밖 또는 비활성 칸");
            RuntimeCell cell = state.CellAt(target);
            if (cause == DamageCause.AdjacentMatch || cause == DamageCause.MagnetAdjacent)
            {
                if (!new BoardEdge(source, target).IsAdjacent) return new DamageReaction(DamageResponse.None, "인접하지 않음");
                if (view.Wall(source, target)) return new DamageReaction(DamageResponse.Wall, "벽이 인접 매칭 피해를 차단");
                if (cell.Content != RuntimeContent.Obstacle && cell.Cover != CoverKind.Mold) return new DamageReaction(DamageResponse.None, "인접 매칭 피해 대상이 아님");
            }
            if (cause == DamageCause.MagnetAdjacent && (cell.Content != RuntimeContent.Obstacle ||
                (state.SchemaVersion == LevelDefinition.LegacySchemaVersion
                    ? state.Obstacles[cell.ObstacleIndex.Value].Definition.Kind != ObstacleKind.ColorLock &&
                      ((state.Obstacles[cell.ObstacleIndex.Value].Definition.Kind < ObstacleKind.Crate || state.Obstacles[cell.ObstacleIndex.Value].Definition.Kind > ObstacleKind.Appliance) ||
                       !state.Obstacles[cell.ObstacleIndex.Value].Element.RequireDamageSourcePolicy().MagnetAdjacent)
                    : state.Obstacles[cell.ObstacleIndex.Value].Element.ReactionBehavior != Elements.ElementReactionBehavior.Durability ||
                      !state.Obstacles[cell.ObstacleIndex.Value].Element.RequireDamageSourcePolicy().MagnetAdjacent)))
                return new DamageReaction(DamageResponse.None, "자석 인접 예외는 색깔 자물쇠만 적용");
            if (cell.Content == RuntimeContent.Recovery)
                return new DamageReaction(DamageResponse.None, "회수 부품은 파괴되지 않음");
            if (cell.Cover.HasValue) return Elements.ElementLayerBehaviorRegistry.Query(cell.CoverElement ?? Elements.LegacyElementDefinitions.Get(cell.Cover.Value), cell, context);
            if (context?.IsProtected(target) == true) return new DamageReaction(DamageResponse.Protected, "이번 턴에 생성된 파워 보호");
            if (cell.Content == RuntimeContent.Obstacle) return ObstacleDamageRules.Query(state, cell, cause,
                sourceColor ?? state.CellAt(source).Color, context, hit);
            if (cell.Content == RuntimeContent.Normal) return new DamageReaction(DamageResponse.Remove, "일반 블록 제거", 1);
            if (cell.Content == RuntimeContent.Rocket || cell.Content == RuntimeContent.Bomb || cell.Content == RuntimeContent.Drone || cell.Content == RuntimeContent.Magnet)
                return context?.HasFired(target) == true ? new DamageReaction(DamageResponse.None, "이미 발동한 파워") :
                    new DamageReaction(DamageResponse.Activate, "파워 단독 발동");
            return new DamageReaction(DamageResponse.None, "빈칸");
        }

    }
}

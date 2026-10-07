using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;

namespace Simulation
{
    public enum BoardItem { Hammer, Swap, Shuffle }

    public sealed class ItemUseResult
    {
        public BoardItem Item { get; }
        public bool IsApplied { get; }
        public string Message { get; }
        public BoardCoordinate? First { get; }
        public BoardCoordinate? Second { get; }
        public int Turn { get; }
        public int MovesRemaining { get; }
        public int RandomBefore { get; }
        public int RandomAfter { get; }
        public ReadOnlyCollection<MatchedBlockChange> Changes { get; }
        public ReadOnlyCollection<EffectRecord> Effects { get; }
        public PowerPresentationTrace PowerTrace { get; }
        public ShuffleResult Shuffle { get; }
        internal ItemUseResult(BoardItem item, bool applied, string message, BoardCoordinate? first, BoardCoordinate? second, int turn,
            int moves, int before, int after, IEnumerable<MatchedBlockChange> changes = null, IEnumerable<EffectRecord> effects = null, ShuffleResult shuffle = null, PowerPresentationTrace powerTrace = null)
        {
            Item = item; IsApplied = applied; Message = message; First = first; Second = second; Turn = turn; MovesRemaining = moves;
            RandomBefore = before; RandomAfter = after; Changes = Array.AsReadOnly(changes?.ToArray() ?? Array.Empty<MatchedBlockChange>());
            Effects = Array.AsReadOnly(effects?.ToArray() ?? Array.Empty<EffectRecord>()); Shuffle = shuffle; PowerTrace = powerTrace;
        }
    }

    public sealed partial class BoardActionExecutor
    {
        private readonly List<ItemUseResult> itemUses = new List<ItemUseResult>();
        public ReadOnlyCollection<ItemUseResult> ItemUses => itemUses.AsReadOnly();
        public bool CanUseItems => CanUseItem(false);
        private bool CanUseItem(bool approvedFree) => Outcome == null && Phase == BoardActionPhase.Ready &&
            (State.MovesRemaining > 0 || approvedFree && endingDeferred);

        // 선택 화면과 실행이 같은 대상 조건을 사용한다. 조회는 턴·난수를 변경하지 않는다.
        public bool CanSelectItemTarget(BoardItem item, BoardCoordinate coordinate)
            => CanSelectItemTarget(item, coordinate, false);

        internal bool CanSelectApprovedItemTarget(BoardItem item, BoardCoordinate coordinate)
            => CanSelectItemTarget(item, coordinate, true);

        private bool CanSelectItemTarget(BoardItem item, BoardCoordinate coordinate, bool approvedFree)
        {
            if (!CanUseItem(approvedFree) || !new BoardQueryView(State).Contains(coordinate)) return false;
            RuntimeCell cell = State.CellAt(coordinate);
            if (!cell.IsActive) return false;
            if (item == BoardItem.Swap) return ActionQuery.Movable(State, cell);
            if (item != BoardItem.Hammer) return false;
            DamageResponse response = DamageReaction.Evaluate(State, coordinate, DamageCause.Hammer, coordinate).Response;
            return response == DamageResponse.Remove || response == DamageResponse.Activate || response == DamageResponse.Damage ||
                response == DamageResponse.CoverDamage || response == DamageResponse.Charge;
        }

        public bool CanSwapItemTargets(BoardCoordinate first, BoardCoordinate second) =>
            CanSelectItemTarget(BoardItem.Swap, first) && CanSelectItemTarget(BoardItem.Swap, second) &&
            new BoardEdge(first, second).IsAdjacent && !new BoardQueryView(State).Wall(first, second);

        public ItemUseResult UseItem(BoardItem item, BoardCoordinate? first = null, BoardCoordinate? second = null)
            => ExecuteItem(item, first, second, false);

        // 게임 연결부가 지정 무료 체험을 승인한 경우에만 사용한다. 일반 아이템의 0회 제한은 유지한다.
        internal ItemUseResult UseApprovedFreeItem(BoardItem item, BoardCoordinate? first = null, BoardCoordinate? second = null)
            => ExecuteItem(item, first, second, true);

        private ItemUseResult ExecuteItem(BoardItem item, BoardCoordinate? first, BoardCoordinate? second, bool approvedFree)
        {
            int before = State.Random.DrawCount;
            ItemUseResult Reject(string message, ShuffleResult shuffle = null) =>
                new ItemUseResult(item, false, message, first, second, Turn, State.MovesRemaining, before, before, shuffle: shuffle);
            if (!CanUseItem(approvedFree)) return Reject("안정된 진행 중 보드에서만 아이템을 사용할 수 있습니다.");
            if (item != BoardItem.Hammer && item != BoardItem.Swap && item != BoardItem.Shuffle) return Reject("지원하지 않는 아이템입니다.");
            if (item != BoardItem.Shuffle && (!first.HasValue || !CanSelectItemTarget(item, first.Value, approvedFree))) return Reject("사용 가능한 대상 칸을 선택하세요.");
            if (item == BoardItem.Swap && (!second.HasValue || !CanSelectItemTarget(item, second.Value, approvedFree) ||
                !new BoardEdge(first.Value, second.Value).IsAdjacent || new BoardQueryView(State).Wall(first.Value, second.Value))) return Reject("벽으로 막히지 않은 인접 블록을 선택하세요.");

            LevelRuntimeState work = new LevelRuntimeState(State);
            TurnEffectContext context = TurnEffects?.NextTurn(Turn + 1) ?? new TurnEffectContext(Turn + 1, Array.Empty<MatchedBlockChange>());
            context.ConsumesMove = false;
            ReadOnlyCollection<MatchedBlockChange> changes = Array.AsReadOnly(Array.Empty<MatchedBlockChange>());
            List<EffectRecord> effects = new List<EffectRecord>();
            ShuffleResult shuffled = null;
            if (item == BoardItem.Shuffle)
            {
                shuffled = ShuffleResolution.Resolve(State);
                if (shuffled.Reason != ShuffleReason.Applied) return Reject(shuffled.Reason == ShuffleReason.Impossible ?
                    "지금은 섞을 수 없어요." : "섞기 탐색 한도 · 사용하지 않았습니다.", shuffled);
                work = shuffled.State; context.ResetAfterShuffle();
            }
            else if (item == BoardItem.Hammer)
            {
                if (!PowerEffectResolution.ApplyHammer(work, first.Value, context, effects, out string error)) return Reject(error);
            }
            else
            {
                HashSet<string> previous = new HashSet<string>(MatchQuery.Find(State).Select(pattern => pattern.Key));
                RuntimeCell left = work.CellAt(first.Value), right = work.CellAt(second.Value);
                Elements.ElementDefinition leftElement = left.ContentElement, rightElement = right.ContentElement;
                long leftOccurrence = left.ContentOccurrence, rightOccurrence = right.ContentOccurrence;
                (left.Content, right.Content) = (right.Content, left.Content);
                left.ContentElement = rightElement; right.ContentElement = leftElement;
                left.ContentOccurrence = rightOccurrence; right.ContentOccurrence = leftOccurrence;
                (left.Color, right.Color) = (right.Color, left.Color);
                (left.RocketDirection, right.RocketDirection) = (right.RocketDirection, left.RocketDirection);
                (left.ObstacleIndex, right.ObstacleIndex) = (right.ObstacleIndex, left.ObstacleIndex);
                context.RecordSwap(work, first.Value, second.Value);
                RecoveryRules.Collect(work, Turn + 1, 0);
                IEnumerable<MatchPattern> patterns = MatchQuery.Find(work).Where(pattern => !previous.Contains(pattern.Key) &&
                    (pattern.Cells.Contains(first.Value) || pattern.Cells.Contains(second.Value)));
                ReadOnlyCollection<MatchDecision> decisions = MatchResolution.Select(patterns, first.Value, second.Value, work.Random);
                decisions = MatchResolution.ResolveCoveredSpawn(work, decisions); context.RecordPatterns(decisions);
                changes = MatchResolution.ApplyLayers(work, decisions, Turn + 1, context); context.Protect(changes);
                if (!PowerEffectResolution.Apply(work, changes, null, context, effects, out string error)) return Reject(error);
            }
            ItemUseResult result = new ItemUseResult(item, true, "아이템 사용 완료 · 후속 처리 대기", first, second, Turn + 1,
                work.MovesRemaining, before, work.Random.DrawCount, changes, effects, shuffled, context.PowerTrace);
            hasDeferredEnding = false;
            State = work; TurnEffects = context; Turn++; itemUses.Add(result);
            Phase = item == BoardItem.Shuffle ? BoardActionPhase.WaitingForAutomaticMatch : BoardActionPhase.WaitingForFall;
            CascadeRounds = 0; LastApplied = null; LastSettlement = null; LastCascadeStep = null;
            cascadeHistory.Clear(); seenCascadeStates.Clear();
            return result;
        }
    }
}

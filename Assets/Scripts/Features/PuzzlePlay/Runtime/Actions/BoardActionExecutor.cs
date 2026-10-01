using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    public enum BoardActionPhase { Ready, WaitingForFall, WaitingForAutomaticMatch, Stopped, WaitingForLastPang }
    public enum BoardActionReason { Applied, StartNotSatisfied, UnsupportedBoard, UnsupportedAction, NoMoves, WaitingForFall, InvalidSwap, InvalidPattern, WaitingForAutomaticMatch, Stopped }

    public sealed class MatchedBlockChange
    {
        public BoardCoordinate Coordinate { get; }
        public RabbitColor OriginalColor { get; }
        public RuntimeContent ResultContent { get; }
        public RocketDirection? RocketDirection { get; }
        public bool IsConsumed { get; }
        public int CoverBefore { get; }
        public int CoverAfter { get; }
        public bool IsTransformation => IsConsumed && ResultContent != RuntimeContent.Empty;
        public int CreatedOnTurn { get; }
        public int HitGroup { get; internal set; }
        internal MatchedBlockChange(BoardCoordinate coordinate, RabbitColor originalColor, RuntimeContent content, RocketDirection? direction, int turn,
            bool consumed = true, int coverBefore = 0, int coverAfter = 0)
        { Coordinate = coordinate; OriginalColor = originalColor; ResultContent = content; RocketDirection = direction; IsConsumed = consumed;
            CoverBefore = coverBefore; CoverAfter = coverAfter; CreatedOnTurn = IsTransformation ? turn : 0; }
    }

    public sealed class BoardActionResult
    {
        public BoardActionReason Reason { get; }
        public bool IsApplied => Reason == BoardActionReason.Applied;
        public string Message { get; }
        public BoardCoordinate First { get; }
        public BoardCoordinate Second { get; }
        public int MovesBefore { get; }
        public int MovesAfter { get; }
        public int RandomBefore { get; }
        public int RandomAfter { get; }
        public int Turn { get; }
        public ReadOnlyCollection<MatchDecision> Decisions { get; }
        public ReadOnlyCollection<MatchedBlockChange> Changes { get; }
        public bool IsActivation { get; }
        public ReadOnlyCollection<EffectRecord> Effects { get; }
        public PowerPresentationTrace PowerTrace { get; }
        internal BoardActionResult(BoardActionReason reason, string message, BoardCoordinate first, BoardCoordinate second,
            int before, int after, int randomBefore, int randomAfter, int turn, IEnumerable<MatchDecision> decisions = null, IEnumerable<MatchedBlockChange> changes = null,
            IEnumerable<EffectRecord> effects = null, bool isActivation = false, PowerPresentationTrace powerTrace = null)
        {
            Reason = reason; Message = message; First = first; Second = second; MovesBefore = before; MovesAfter = after;
            RandomBefore = randomBefore; RandomAfter = randomAfter; Turn = turn;
            Decisions = Array.AsReadOnly(decisions?.ToArray() ?? Array.Empty<MatchDecision>());
            Changes = Array.AsReadOnly(changes?.ToArray() ?? Array.Empty<MatchedBlockChange>());
            Effects = Array.AsReadOnly(effects?.ToArray() ?? Array.Empty<EffectRecord>()); IsActivation = isActivation; PowerTrace = powerTrace;
        }
    }

    // 사용자 행동과 단계별 연쇄를 소유한다. 조회와 Editor 표시에는 상태 변경을 맡기지 않는다.
    public sealed partial class BoardActionExecutor
    {
        public const string Version = "board-action-fresh-diagonal-v15";
        public LevelRuntimeState State { get; private set; }
        public BoardActionPhase Phase { get; private set; }
        public int Turn { get; private set; }
        public BoardActionResult LastApplied { get; private set; }
        public TurnEffectContext TurnEffects { get; private set; }
        public SettlementResult LastSettlement { get; private set; }
        public BoardActionExecutor(LevelRuntimeState startingState) : this(startingState, Array.Empty<StartBooster>()) { }
        public BoardActionExecutor(LevelRuntimeState startingState, IEnumerable<StartBooster> boosters)
        {
            State = new LevelRuntimeState(startingState ?? throw new ArgumentNullException(nameof(startingState)));
            InitializeBoosters(boosters);
            if (State.Cells.Any(c => c.Content == RuntimeContent.Recovery && State.Flow.Arrivals.Contains(c.Coordinate)))
            {
                TurnEffects = new TurnEffectContext(0, Array.Empty<MatchedBlockChange>());
                Phase = BoardActionPhase.WaitingForFall;
                Settle();
            }
            else if (State.Missions.Count == 0 || State.Missions.All(m => m.Remaining == 0) || State.MovesRemaining == 0)
            {
                TurnEffects = new TurnEffectContext(0, Array.Empty<MatchedBlockChange>());
                Phase = BoardActionPhase.WaitingForAutomaticMatch;
            }
            else PlacePendingBoosters();
        }

        public BoardActionResult Swap(BoardCoordinate first, BoardCoordinate second)
            => Execute(first, second, false);

        public BoardActionResult Activate(BoardCoordinate coordinate) => Execute(coordinate, coordinate, true);

        public SettlementResult Settle()
        {
            if (Phase != BoardActionPhase.WaitingForFall)
                return new SettlementResult(SettlementReason.WrongPhase, "낙하 대기에서만 정착을 실행할 수 있습니다.", State);
            SettlementResult result = SettlementResolution.Resolve(State, TurnEffects, IsLastPang);
            if (!result.IsApplied) { AbortExecution(result.Message); return result; }
            State = result.State; TurnEffects = result.TurnEffects; LastSettlement = result;
            Phase = BoardActionPhase.WaitingForAutomaticMatch;
            RecordStep(new CascadeStepResult(CascadeStepReason.Settled, result.Message, Turn, CascadeRounds,
                result.RandomBefore, result.RandomAfter, settlement: result));
            return result;
        }

        private BoardActionResult Execute(BoardCoordinate first, BoardCoordinate second, bool activation)
        {
            BoardActionResult Reject(BoardActionReason reason, string message) => new BoardActionResult(reason, message, first, second,
                State.MovesRemaining, State.MovesRemaining, State.Random.DrawCount, State.Random.DrawCount, Turn, isActivation: activation);
            if (Outcome != null) return Reject(BoardActionReason.Stopped, Outcome.Message);
            if (Phase == BoardActionPhase.WaitingForFall) return Reject(BoardActionReason.WaitingForFall, "낙하 대기 중입니다. 정착을 실행하거나 같은 시드로 초기화하세요.");
            if (Phase == BoardActionPhase.WaitingForAutomaticMatch) return Reject(BoardActionReason.WaitingForAutomaticMatch, "자동 매칭 대기 중입니다. 다음 연쇄 단계를 실행하세요.");
            if (Phase == BoardActionPhase.Stopped) return Reject(BoardActionReason.Stopped, LastCascadeStep?.Message ?? "시험 정지 상태입니다.");
            if (State.Obstacles.Any(obstacle => !ObstacleDamageRules.Supports(obstacle.Definition.Kind)) ||
                State.Cells.Any(cell => cell.Content == RuntimeContent.Obstacle && (!cell.ObstacleIndex.HasValue || cell.ObstacleIndex < 0 || cell.ObstacleIndex >= State.Obstacles.Count)))
            {
                AbortExecution("미지원 장애물 또는 잘못된 본체 데이터");
                return Reject(BoardActionReason.UnsupportedBoard, "미지원 장애물 실행 미지원");
            }
            if (State.MovesRemaining <= 0) return Reject(BoardActionReason.NoMoves, "남은 이동 횟수가 없습니다.");
            if (State.Missions.Any(m => !MissionProgressRules.Supports(m.Definition.Kind)))
            {
                AbortExecution("미지원 미션 포함");
                return Reject(BoardActionReason.UnsupportedBoard, "미지원 미션 포함");
            }
            ActionCandidate action = activation ? ActionQuery.Activate(State, first) : ActionQuery.Swap(State, first, second);
            if (!action.IsAllowed) return Reject(BoardActionReason.InvalidSwap, action.Message);
            if (Turn == 0 && !new StartConditionReport(State).IsSatisfied) return Reject(BoardActionReason.StartNotSatisfied, "시작 조건을 만족한 보드에서 실행하세요.");

            LevelRuntimeState work = new LevelRuntimeState(State);
            RabbitColor? exchangeColor = !activation && action.Kind == QueryActionKind.SwapPower &&
                (action.FirstContent == RuntimeContent.Magnet || action.SecondContent == RuntimeContent.Magnet) ?
                (action.FirstContent == RuntimeContent.Normal ? State.CellAt(first).Color : State.CellAt(second).Color) : null;
            BoardCoordinate? power = activation ? first : action.Kind == QueryActionKind.SwapPower ?
                (action.FirstContent >= RuntimeContent.Rocket && action.FirstContent <= RuntimeContent.Magnet ? second : first) : (BoardCoordinate?)null;
            if (!activation)
            {
                RuntimeCell a = work.CellAt(first), b = work.CellAt(second);
                // 점유자만 이동하고 바닥/중력과 좌표는 유지한다. 조합 중심은 두 번째 입력 칸이다.
                (a.Content, b.Content) = (b.Content, a.Content);
                (a.Color, b.Color) = (b.Color, a.Color);
                (a.RocketDirection, b.RocketDirection) = (b.RocketDirection, a.RocketDirection);
                (a.ObstacleIndex, b.ObstacleIndex) = (b.ObstacleIndex, a.ObstacleIndex);
            }
            RecoveryRules.Collect(work, Turn + 1, 0);
            ReadOnlyCollection<MatchDecision> decisions;
            IEnumerable<MatchPattern> patterns = activation || action.Kind == QueryActionKind.SwapCombination ? Array.Empty<MatchPattern>() : action.Kind == QueryActionKind.SwapMatch ? action.Matches :
                MatchQuery.Find(work).Where(match => match.Cells.Contains(first) || match.Cells.Contains(second));
            try { decisions = MatchResolution.Select(patterns, first, second, work.Random); }
            catch (InvalidOperationException error) { return Reject(BoardActionReason.InvalidPattern, error.Message); }
            TurnEffectContext context = TurnEffects?.NextTurn(Turn + 1) ?? new TurnEffectContext(Turn + 1, Array.Empty<MatchedBlockChange>());
            if (!activation && TurnEffects != null) context.RecordSwap(work, first, second);
            decisions = MatchResolution.ResolveCoveredSpawn(work, decisions);
            context.RecordPatterns(decisions);
            ReadOnlyCollection<MatchedBlockChange> changes = MatchResolution.ApplyLayers(work, decisions, Turn + 1, context);
            context.Protect(changes);
            List<EffectRecord> effects = new List<EffectRecord>();
            string errorMessage;
            bool applied = action.Kind == QueryActionKind.SwapCombination ?
                PowerEffectResolution.ApplyCombination(work, first, second, context, effects, out errorMessage) :
                PowerEffectResolution.ApplyWithColor(work, changes, power, context, effects, out errorMessage, exchangeColor);
            if (!applied)
            {
                AbortExecution(errorMessage);
                return Reject(BoardActionReason.UnsupportedAction, errorMessage);
            }
            work.MovesRemaining--;
            LastApplied = new BoardActionResult(BoardActionReason.Applied, "매칭·효과 처리 완료 · 낙하 대기", first, second,
                State.MovesRemaining, work.MovesRemaining, State.Random.DrawCount, work.Random.DrawCount, Turn + 1, decisions, changes, effects, activation, context.PowerTrace);
            State = work; TurnEffects = context; Turn++; Phase = BoardActionPhase.WaitingForFall;
            CascadeRounds = 0; LastSettlement = null; LastCascadeStep = null; cascadeHistory.Clear(); seenCascadeStates.Clear();
            return LastApplied;
        }
    }
}

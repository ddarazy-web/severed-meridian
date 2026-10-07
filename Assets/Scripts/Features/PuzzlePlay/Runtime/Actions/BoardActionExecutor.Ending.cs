using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    public sealed partial class BoardActionExecutor
    {
        public BoardOutcome Outcome { get; private set; }
        public ShuffleResult LastShuffle { get; private set; }
        public TurnEffectContext WinningTurnEffects { get; private set; }
        public int LastPangConversions { get; private set; }
        public int LastPangWaves { get; private set; }
        public string LastPangMessage { get; private set; }
        public bool IsLastPang => Outcome?.Kind == BoardOutcomeKind.Won && Phase != BoardActionPhase.Stopped;
        private bool endingDeferred;
        private bool hasDeferredEnding;

        /// <summary>외부 진행 흐름이 끝날 때까지 승패와 자동 재배치의 판단만 보류한다.</summary>
        /// <param name="deferred">종료 판단을 보류할지 여부.</param>
        public void SetEndingDeferred(bool deferred) => endingDeferred = deferred;

        /// <summary>이미 처리한 턴 효과를 반복하지 않고 보류했던 종료 판단을 한 번 재개한다.</summary>
        /// <returns>재개한 종료 기록. 아직 보류 중이거나 재개할 판단이 없으면 null.</returns>
        public CascadeStepResult ResumeDeferredEnding()
        {
            if (endingDeferred || !hasDeferredEnding || Outcome != null || Phase != BoardActionPhase.Ready) return null;
            CascadeStepResult result = DecideStableEnding(State, TurnEffects, "종료 보류 해제", State.Random.DrawCount);
            hasDeferredEnding = false;
            return result;
        }

        private void AbortExecution(string message)
        {
            if (Outcome == null) Outcome = new BoardOutcome(BoardOutcomeKind.Aborted, message, State, Turn);
            else if (Outcome.Kind == BoardOutcomeKind.Won) LastPangMessage = "라스트팡 중단 · 성공 유지 · " + message;
            Phase = BoardActionPhase.Stopped;
        }

        private CascadeStepResult EndStableTurn()
        {
            int before = State.Random.DrawCount;
            if (IsLastPang)
            {
                Phase = BoardActionPhase.WaitingForLastPang;
                return RecordEnding(CascadeStepReason.LastPang, "라스트팡 정착 완료 · 다음 파워 발동", before);
            }
            if (State.Missions.Count == 0 || State.Missions.Any(m => !MissionProgressRules.Supports(m.Definition.Kind)))
            {
                AbortExecution("유효한 미션이 없습니다.");
                return RecordEnding(CascadeStepReason.Aborted, Outcome.Message, before);
            }
            LevelRuntimeState previousState = State; TurnEffectContext previousContext = TurnEffects;
            LevelRuntimeState work = new LevelRuntimeState(State);
            TurnEffectContext context = TurnEffects.Copy();
            MoldSpreadRecord spread = MoldRules.FinishTurn(work, context);
            State = work; TurnEffects = context;
            if (endingDeferred)
            {
                hasDeferredEnding = true;
                Phase = BoardActionPhase.Ready;
                return RecordEnding(CascadeStepReason.Stable, "연쇄 완료 · 종료 판단 보류 · " + spread.Message, before);
            }
            return DecideStableEnding(previousState, previousContext, spread.Message, before);
        }

        private CascadeStepResult DecideStableEnding(LevelRuntimeState previousState, TurnEffectContext previousContext, string turnMessage, int before)
        {
            if (State.Missions.All(m => m.Remaining == 0))
            {
                Outcome = new BoardOutcome(BoardOutcomeKind.Won, "목표 달성 · 성공", State, Turn);
                WinningTurnEffects = TurnEffects.Copy();
                BeginLastPang();
                return RecordEnding(CascadeStepReason.Won, "성공 확정 · 라스트팡 대기", before);
            }
            if (State.MovesRemaining == 0)
            {
                Outcome = new BoardOutcome(BoardOutcomeKind.MovesExhausted, "이동 수 소진 · 목표 미달성", State, Turn);
                Phase = BoardActionPhase.Stopped;
                return RecordEnding(CascadeStepReason.MovesExhausted, Outcome.Message + " · " + turnMessage, before);
            }
            StartBooster[] previousBoosters = pendingBoosters.ToArray();
            int previousPlacementCount = boosterPlacements.Count;
            BoardActionPhase previousPhase = Phase; BoardOutcome previousOutcome = Outcome;
            ShuffleResult previousShuffle = LastShuffle;
            try
            {
                PlacePendingBoosters();
                if (ActionQuery.Find(State).Count > 0)
                {
                    Phase = BoardActionPhase.Ready;
                    return RecordEnding(CascadeStepReason.Stable, "연쇄 완료 · 다음 행동 대기 · " + turnMessage, before);
                }
                LastShuffle = ShuffleResolution.Resolve(State);
                if (LastShuffle.Reason == ShuffleReason.Applied)
                {
                    State = LastShuffle.State;
                    TurnEffects.ResetAfterShuffle();
                    Phase = BoardActionPhase.Ready;
                    return RecordEnding(CascadeStepReason.Shuffled, "자동 재배치 완료 · 시도 " + LastShuffle.Attempts, before);
                }
                if (LastShuffle.Reason == ShuffleReason.Impossible)
                    Outcome = new BoardOutcome(BoardOutcomeKind.Blocked, "진행 불가 · 무료 재도전", State, Turn);
                else AbortExecution("재배치 탐색 한도 · 패배 아님 · 시도 " + LastShuffle.Attempts);
                Phase = BoardActionPhase.Stopped;
                return RecordEnding(Outcome.Kind == BoardOutcomeKind.Blocked ? CascadeStepReason.Blocked : CascadeStepReason.Aborted, Outcome.Message, before);
            }
            catch
            {
                // 다음 행동 조회/재배치 실패는 번식·난수와 대기 부스터까지 원복한다.
                State = previousState; TurnEffects = previousContext;
                Phase = previousPhase; Outcome = previousOutcome; LastShuffle = previousShuffle;
                pendingBoosters.Clear(); pendingBoosters.AddRange(previousBoosters);
                boosterPlacements.RemoveRange(previousPlacementCount, boosterPlacements.Count - previousPlacementCount);
                throw;
            }
        }

        private CascadeStepResult RecordEnding(CascadeStepReason reason, string message, int before, IEnumerable<EffectRecord> effects = null)
        {
            CascadeStepResult result = new CascadeStepResult(reason, message, Turn, CascadeRounds, before, State.Random.DrawCount, effects: effects,
                powerTrace: effects != null ? TurnEffects.PowerTrace : null);
            RecordStep(result); return result;
        }

        private void BeginLastPang()
        {
            LevelRuntimeState work = new LevelRuntimeState(State);
            List<RuntimeCell> candidates = work.Cells.Where(c => c.IsActive && c.Content == RuntimeContent.Normal && !c.Cover.HasValue).ToList();
            int count = Math.Min(Outcome.MovesRemaining, candidates.Count);
            LastPangConversions = count;
            for (int i = 0; i < count; i++)
            {
                int index = candidates.Count == 1 ? 0 : work.Random.Next(candidates.Count);
                RuntimeCell cell = candidates[index]; candidates.RemoveAt(index);
                cell.Content = RuntimeContent.Rocket; cell.Color = null;
                cell.ContentElement = Elements.LegacyElementDefinitions.GetContent(cell.Content, work.ElementCatalog);
                cell.RocketDirection = work.Random.Next(2) == 0 ? RocketDirection.Horizontal : RocketDirection.Vertical;
            }
            State = work; TurnEffects = new TurnEffectContext(0, Array.Empty<MatchedBlockChange>());
            CascadeRounds = 0; seenCascadeStates.Clear(); Phase = BoardActionPhase.WaitingForLastPang;
            LastPangMessage = "남은 이동 " + Outcome.MovesRemaining + " · 로켓 변환 " + count;
        }

        public CascadeStepResult SkipLastPang()
        {
            if (!IsLastPang) return RejectStep(CascadeStepReason.WrongPhase, "라스트팡에서만 건너뛸 수 있습니다.");
            Phase = BoardActionPhase.Stopped; LastPangMessage = "라스트팡 건너뛰기 · 성공 유지";
            return RecordEnding(CascadeStepReason.LastPangComplete, LastPangMessage, State.Random.DrawCount);
        }

        private CascadeStepResult AdvanceLastPang()
        {
            RuntimeCell[] powers = State.Cells.Where(c => c.IsActive && !c.Cover.HasValue && c.Content >= RuntimeContent.Rocket && c.Content <= RuntimeContent.Magnet).ToArray();
            if (powers.Length == 0)
            {
                Phase = BoardActionPhase.Stopped; LastPangMessage = "라스트팡 완료 · 성공";
                return RecordEnding(CascadeStepReason.LastPangComplete, LastPangMessage, State.Random.DrawCount);
            }
            if (LastPangWaves >= CascadeLimit) return RejectStep(CascadeStepReason.LimitReached, "라스트팡 발동 한도");
            LevelRuntimeState work = new LevelRuntimeState(State);
            TurnEffectContext context = TurnEffects.Copy(); context.ReleaseLastPangPowers();
            List<EffectRecord> effects = new List<EffectRecord>();
            foreach (RuntimeCell power in powers)
            {
                RuntimeCell current = work.CellAt(power.Coordinate);
                if (current.Cover.HasValue || current.Content < RuntimeContent.Rocket || current.Content > RuntimeContent.Magnet) continue;
                if (!PowerEffectResolution.Apply(work, Array.Empty<MatchedBlockChange>(), power.Coordinate, context, effects, out string error))
                    return RejectStep(CascadeStepReason.Unsupported, error);
            }
            int before = State.Random.DrawCount;
            State = work; TurnEffects = context; LastPangWaves++; Phase = BoardActionPhase.WaitingForFall;
            LastPangMessage = "라스트팡 파워 발동 " + LastPangWaves;
            return RecordEnding(CascadeStepReason.LastPang, LastPangMessage, before, effects);
        }
    }
}

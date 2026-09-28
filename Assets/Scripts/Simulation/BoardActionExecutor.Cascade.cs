using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Simulation
{
    public enum CascadeStepReason { Settled, Matched, Stable, MovesExhausted, NeedsShuffle, UnsupportedOnly, WrongPhase, Unsupported, SettlementFailed, Repeating, LimitReached, Won, Shuffled, Blocked, Aborted, LastPang, LastPangComplete }

    public sealed class CascadeStepResult
    {
        public CascadeStepReason Reason { get; }
        public bool IsApplied => Reason <= CascadeStepReason.UnsupportedOnly || Reason >= CascadeStepReason.Won;
        public bool IsStable => Reason >= CascadeStepReason.Stable && Reason <= CascadeStepReason.UnsupportedOnly || Reason == CascadeStepReason.Shuffled || Reason == CascadeStepReason.Blocked || Reason == CascadeStepReason.Aborted || Reason == CascadeStepReason.LastPangComplete;
        public string Message { get; }
        public int Turn { get; }
        public int Round { get; }
        public int RandomBefore { get; }
        public int RandomAfter { get; }
        public ReadOnlyCollection<MatchDecision> Decisions { get; }
        public ReadOnlyCollection<MatchedBlockChange> Changes { get; }
        public ReadOnlyCollection<EffectRecord> Effects { get; }
        public SettlementResult Settlement { get; }
        internal CascadeStepResult(CascadeStepReason reason, string message, int turn, int round, int randomBefore, int randomAfter,
            IEnumerable<MatchDecision> decisions = null, IEnumerable<MatchedBlockChange> changes = null, IEnumerable<EffectRecord> effects = null, SettlementResult settlement = null)
        {
            Reason = reason; Message = message; Turn = turn; Round = round; RandomBefore = randomBefore; RandomAfter = randomAfter;
            Decisions = Array.AsReadOnly(decisions?.ToArray() ?? Array.Empty<MatchDecision>());
            Changes = Array.AsReadOnly(changes?.ToArray() ?? Array.Empty<MatchedBlockChange>());
            Effects = Array.AsReadOnly(effects?.ToArray() ?? Array.Empty<EffectRecord>()); Settlement = settlement;
        }
    }

    public sealed partial class BoardActionExecutor
    {
        private readonly List<CascadeStepResult> cascadeHistory = new List<CascadeStepResult>();
        private readonly Dictionary<string, int> seenCascadeStates = new Dictionary<string, int>();
        public int CascadeRounds { get; private set; }
        public int CascadeLimit => Math.Max(128, State.Cells.Count * 4);
        public CascadeStepResult LastCascadeStep { get; private set; }
        public ReadOnlyCollection<CascadeStepResult> CascadeHistory => cascadeHistory.AsReadOnly();
        public bool HasPendingCascade => Phase == BoardActionPhase.WaitingForFall || Phase == BoardActionPhase.WaitingForAutomaticMatch || Phase == BoardActionPhase.WaitingForLastPang;

        private void RecordStep(CascadeStepResult result) { LastCascadeStep = result; cascadeHistory.Add(result); }
        private CascadeStepResult RejectStep(CascadeStepReason reason, string message)
        {
            if (reason != CascadeStepReason.WrongPhase) AbortExecution(message);
            return new CascadeStepResult(reason, message, Turn, CascadeRounds, State.Random.DrawCount, State.Random.DrawCount);
        }

        // Editor는 이 명령을 틱마다 한 번 호출한다. 단계 실행과 끝까지 실행이 같은 경로를 사용한다.
        public CascadeStepResult AdvanceCascade()
        {
            if (Phase == BoardActionPhase.WaitingForLastPang) return AdvanceLastPang();
            if (Phase == BoardActionPhase.WaitingForFall)
            {
                SettlementResult settled = Settle();
                return settled.IsApplied ? LastCascadeStep : RejectStep(CascadeStepReason.SettlementFailed, settled.Message);
            }
            return ResolveAutomaticMatch();
        }

        public CascadeStepResult ResolveAutomaticMatch()
        {
            if (Phase != BoardActionPhase.WaitingForAutomaticMatch)
                return RejectStep(CascadeStepReason.WrongPhase, "정착 후 자동 매칭 대기에서만 실행할 수 있습니다.");
            ReadOnlyCollection<MatchPattern> patterns = MatchQuery.Find(State).Where(p => !TurnEffects.WasProcessed(p)).ToList().AsReadOnly();
            if (patterns.Count == 0)
                return EndStableTurn();
            if (CascadeRounds >= CascadeLimit)
                return RejectStep(CascadeStepReason.LimitReached, "자동 연쇄 " + CascadeLimit + "회 한도 초과 · 마지막 성공 단계 보존");
            string key = CascadeKey();
            if (seenCascadeStates.TryGetValue(key, out int draws) && draws == State.Random.DrawCount)
                return RejectStep(CascadeStepReason.Repeating, "진행 없는 자동 매칭 상태 반복 · 마지막 성공 단계 보존");

            LevelRuntimeState work = new LevelRuntimeState(State);
            TurnEffectContext context = TurnEffects.Copy();
            ReadOnlyCollection<MatchDecision> decisions = MatchResolution.SelectAutomatic(patterns, context, work.Random);
            decisions = MatchResolution.ResolveCoveredSpawn(work, decisions);
            context.RecordPatterns(decisions);
            ReadOnlyCollection<MatchedBlockChange> changes = MatchResolution.ApplyLayers(work, decisions, Turn, context);
            context.Protect(changes);
            List<EffectRecord> effects = new List<EffectRecord>();
            if (!PowerEffectResolution.Apply(work, changes, null, context, effects, out string error))
                return RejectStep(CascadeStepReason.Unsupported, error);
            context.ForgetRemoved(work);
            CascadeStepResult result = new CascadeStepResult(CascadeStepReason.Matched, "자동 매칭·효과 완료 · 낙하 대기", Turn, CascadeRounds + 1,
                State.Random.DrawCount, work.Random.DrawCount, decisions, changes, effects);
            seenCascadeStates[key] = State.Random.DrawCount;
            State = work; TurnEffects = context; CascadeRounds++; Phase = BoardActionPhase.WaitingForFall;
            RecordStep(result); return result;
        }

        private string CascadeKey()
        {
            // 절대 회차 대신 상대 도착 순위를 비교한다. 회차만 증가한 무진행 반복도 찾는다.
            long[] times = State.Cells.Where(c => c.Content == RuntimeContent.Normal).Select(c => TurnEffects.LastArrival(c.Coordinate)).Distinct().OrderBy(t => t).ToArray();
            return string.Join(";", State.Cells.Select(c => c.Content + "," + c.Color + "," + c.RocketDirection + "," + c.ObstacleIndex + "," +
                TurnEffects.IsProtected(c.Coordinate) + "," + c.Cover + "," + c.CoverDurability + "," + c.DustDurability + "," +
                TurnEffects.HasDamagedWeb(c.Coordinate) + "," + TurnEffects.HasDamagedDust(c.Coordinate) + "," +
                (c.Content == RuntimeContent.Normal ? Array.IndexOf(times, TurnEffects.LastArrival(c.Coordinate)) : -1))) + "|" +
                string.Join(";", State.Obstacles.Select((o, i) => o.Durability + "," + TurnEffects.HasDamaged(i) + "," + o.Charge + "," + TurnEffects.HasCharged(i))) + "|" +
                State.Supply.ScrapGenerated + "|" + State.Recoveries.Count + "|" + string.Join(";", State.Missions.Select(m => m.Progress)) + "|" + string.Join(";", State.Supply.Sources.Select(s => s.ItemIndex + "," + s.ItemConsumed));
        }
    }
}

using System;
using System.Linq;
using Board;
using Levels;

namespace AutoPlay
{
    public enum BotBatchStatus { Running, Paused, Completed, Stopped, Error, Interrupted }

    /// <summary>묶음의 저장용 값. 게임 상태나 전략 객체를 담지 않으며 전략 입력으로 사용하지 않는다.</summary>
    [Serializable]
    public sealed class BotBatchRecord
    {
        public int formatVersion = 1;
        public string id, definitionJson, fingerprint, startedUtc, endedUtc, message;
        // Editor가 제공하는 이력 연결 정보다. 구기록은 비어 있어도 읽을 수 있으며,
        // 실행·재생 근거는 항상 definitionJson/fingerprint다. 봇에는 전달하지 않는다.
        public string sourceGuid, sourceName;
        public string startingVersion;
        public string basicVersion, planningVersion, observationVersion, assumptionVersion, sessionVersion, engineVersion;
        public int[] seeds;
        public BotBatchStatus status;
        public int finished, won, exhausted, blocked, errors, stopped;
        public int basicFinished, planningFinished;
        public int Total => seeds.Length * 2;
        public int Recorded => finished + errors + stopped;
        public int Unrun => Total - Recorded;
    }

    /// <summary>확정한 한 판의 값. 행동 기록은 디스크에 저장한 후 다음 판으로 넘기지 않는다.</summary>
    [Serializable]
    public sealed class BotBatchGame
    {
        public string batchId, message;
        public int ordinal, seed, usedMoves, remainingMoves;
        public BotStrategyKind strategy;
        public BotSessionStatus outcome;
        public BotBatchMission[] missions;
        public BotBatchAction[] actions;

        /// <param name="batch">현재 묶음의 값.</param><param name="session">기록할 한 판.</param>
        /// <param name="outcome">실제 종료 종류 또는 사용자 중단.</param><param name="message">종료·중단 이유.</param>
        internal BotBatchGame(BotBatchRecord batch, BotPlaySession session, BotSessionStatus outcome, string message)
        {
            batchId = batch.id; ordinal = batch.Recorded; seed = session.Seed; strategy = session.Strategy;
            this.outcome = outcome; this.message = message;
            remainingMoves = session.State?.MovesRemaining ?? -1;
            usedMoves = session.State == null ? 0 : session.State.InitialMoves - remainingMoves;
            missions = session.State == null ? Array.Empty<BotBatchMission>() : session.State.Missions.Select(m =>
                new BotBatchMission { kind = m.Definition.Kind, color = m.Definition.Color, remaining = m.Remaining }).ToArray();
            actions = session.Records.Select(r => new BotBatchAction {
                turn = r.Turn, kind = r.Choice.Action.Kind, first = r.Choice.Action.First,
                hasSecond = r.Choice.Action.Second.HasValue, second = r.Choice.Action.Second.GetValueOrDefault(),
                movesBefore = r.MovesBefore, movesAfter = r.MovesAfter, randomBefore = r.RandomBefore,
                randomAfter = r.RandomAfter, reason = r.Choice.Reason }).ToArray();
        }
    }

    [Serializable]
    public sealed class BotBatchMission
    {
        public MissionKind kind;
        public RabbitColor color;
        public int remaining;
    }

    [Serializable]
    public sealed class BotBatchAction
    {
        public int turn, movesBefore, movesAfter, randomBefore, randomAfter;
        public BotActionKind kind;
        public BoardCoordinate first, second;
        public bool hasSecond;
        public string reason;
    }
}

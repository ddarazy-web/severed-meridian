using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class MoldVerification
    {
        private const string BaselineEvidence = "Logs/ElementFramework/Stage05";
        private static readonly List<string> Observations = new List<string>();

        public static void Rollback()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(BaselineEvidence); Results.Clear();
            try
            {
                foreach (bool booster in new[] { true, false })
                {
                    LevelDefinition level = Make();
                    try
                    {
                        Mold(level, C(4, 4)); BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                        Set(executor, "Phase", BoardActionPhase.WaitingForAutomaticMatch); Set(executor, "TurnEffects", Context()); Set(executor, "Turn", 1);
                        if (booster) ((List<StartBooster>)typeof(BoardActionExecutor).GetField("pendingBoosters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                            .GetValue(executor)).Add(StartBooster.Bomb);
                        RuntimeCell cell = executor.State.CellAt(C(0, 1)); Set(cell, "Content", RuntimeContent.Obstacle); Set(cell, "Color", null); Set(cell, "ObstacleIndex", 999);
                        string Raw() => Snapshot(executor.State.Cells) + Snapshot(executor.State.Obstacles) + Snapshot(executor.State.Missions)
                            + Snapshot(executor.State.Random) + ContextSnapshot(executor) + Snapshot(executor.PendingBoosters) + Snapshot(executor.BoosterPlacements);
                        LevelRuntimeState original = executor.State; TurnEffectContext context = executor.TurnEffects;
                        string before = Raw(); bool rejected = false;
                        try { executor.ResolveAutomaticMatch(); } catch (ArgumentOutOfRangeException) { rejected = true; }
                        File.WriteAllText(BaselineEvidence + "/rollback-" + booster + "-observation.txt", "before=" + before + "\nafter=" + Raw());
                        Check(rejected && Raw() == before && ReferenceEquals(original, executor.State) && ReferenceEquals(context, executor.TurnEffects) &&
                            executor.Phase == BoardActionPhase.WaitingForAutomaticMatch && executor.Outcome == null && executor.LastShuffle == null && executor.CascadeHistory.Count == 0,
                            "턴 종료 예외 상태·난수·문맥·부스터 원복 " + booster);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
                File.WriteAllLines(BaselineEvidence + "/rollback-results.txt", Results); EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error); File.WriteAllLines(BaselineEvidence + "/rollback-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1);
            }
        }

        public static void DiagnoseEdges()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(BaselineEvidence);
            LevelDefinition level = Make();
            try
            {
                Mold(level, C(4, 4)); BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                Set(executor, "Phase", BoardActionPhase.WaitingForAutomaticMatch); Set(executor, "TurnEffects", Context()); Set(executor, "Turn", 1);
                RuntimeCell cell = executor.State.CellAt(C(0, 1)); Set(cell, "Content", RuntimeContent.Obstacle); Set(cell, "Color", null); Set(cell, "ObstacleIndex", 999);
                string Raw() => Snapshot(executor.State.Cells) + Snapshot(executor.State.Obstacles) + Snapshot(executor.State.Missions) + Snapshot(executor.State.Supply) + Snapshot(executor.State.Random) + ContextSnapshot(executor);
                string before = Raw(); string error = "none", result = "none";
                try { result = Snapshot(executor.ResolveAutomaticMatch()); } catch (Exception exception) { error = exception.ToString(); }
                File.WriteAllText(BaselineEvidence + "/edge-diagnostic.txt", "exception=" + error + "\nresult=" + result + "\nunchanged=" + (Raw() == before)
                    + "\nphase=" + executor.Phase + "\nBEFORE properties=" + before + "\nAFTER properties=" + Raw());
                EditorApplication.Exit(0);
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        public static void Baseline()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(BaselineEvidence); Results.Clear(); Observations.Clear();
            try
            {
                foreach (int seed in new[] { 7, 12345 }) ObserveProgress(seed);
                foreach (string name in new[] { "single", "no-candidate", "no-turn", "no-mold", "complete", "removed" }) ObserveReason(name);
                foreach (bool complete in new[] { false, true }) ObserveEnding(complete);
                RemainingEdges();
                File.WriteAllLines(BaselineEvidence + "/baseline-results.txt", Results);
                File.WriteAllLines(BaselineEvidence + "/mold-observations.jsonl", Observations);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error); File.WriteAllLines(BaselineEvidence + "/baseline-results.txt", Results);
                File.WriteAllLines(BaselineEvidence + "/mold-observations.jsonl", Observations);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }

        private static void ObserveProgress(int seed)
        {
            LevelDefinition level = Make();
            try
            {
                Missions(level); Mold(level, C(4, 4), C(4, 6)); LevelRuntimeState state = Build(level, seed);
                Set(state.Missions[0], "Progress", 3); Set(state.Missions[1], "Progress", 1);
                TurnEffectContext context = Context(); string before = Snapshot(state);
                MoldSpreadRecord first = End(state, context);
                Record("progress", "set color progress=3, mold progress=1; FinishTurn", seed, level, before, state, context, first);
                Check(first.CandidateCount == 7 && first.RandomAfter == first.RandomBefore + 1 && state.Missions[1].Target == 3 &&
                    state.Missions[1].Progress == 1 && state.Missions[0].Progress == 3, "번식 목표 증가/기존 진행 보존 " + seed);
                string after = Snapshot(state);
                Check(ReferenceEquals(first, End(state, context)) && Snapshot(state) == after, "관찰 턴 종료 반복 무변경 " + seed);
                LevelRuntimeState replay = Build(level, seed); Set(replay.Missions[0], "Progress", 3); Set(replay.Missions[1], "Progress", 1);
                Check(Snapshot(End(replay, Context())) == Snapshot(first) && Snapshot(replay) == after, "관찰 같은 시드 전체 재현 " + seed);
                TurnEffectContext next = (TurnEffectContext)Invoke(typeof(TurnEffectContext), "NextTurn", context, 2);
                Check(next.MoldSpread == null && !next.RemovedMold, "관찰 다음 턴 이력 초기화 " + seed);
                before = Snapshot(state); MoldSpreadRecord second = End(state, next);
                Record("next-turn", "NextTurn(2); FinishTurn", seed, level, before, state, next, second);
                Check(second.Reason == MoldSpreadReason.Spread && state.Missions[1].Target == 4 && state.Missions[1].Progress == 1,
                    "다음 턴 새 번식/누적 목표·진행 보존 " + seed);
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void RemainingEdges()
        {
            // 기존 Edges가 예외 원복 실패로 중단되므로 뒤의 독립 사례를 따로 실행한다.
            LevelDefinition isolated = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null,
                new Dictionary<BoardCoordinate, int> { [C(0, 0)] = 0, [C(0, 1)] = 1 }, 20);
            try
            {
                Mold(isolated, C(0, 0)); BoardActionExecutor executor = new BoardActionExecutor(Build(isolated));
                Set(executor, "Phase", BoardActionPhase.WaitingForAutomaticMatch); Set(executor, "TurnEffects", Context()); Set(executor, "Turn", 1);
                CascadeStepResult result = executor.ResolveAutomaticMatch();
                Record("no-actions", "ResolveAutomaticMatch; actual=" + result.Reason, 12345, isolated, "", executor.State, executor.TurnEffects, executor.TurnEffects.MoldSpread);
                Check(result.Reason == CascadeStepReason.Blocked && executor.Outcome.Kind == BoardOutcomeKind.Blocked &&
                    executor.TurnEffects.MoldSpread.Reason == MoldSpreadReason.Spread && executor.State.Cells.Count(c => c.Cover == CoverKind.Mold) == 2,
                    "독립 경계 번식 후 자동 재배치 불가/진행 불가 종료");
            }
            finally { UnityEngine.Object.DestroyImmediate(isolated); }
            LevelDefinition level = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null,
                new Dictionary<BoardCoordinate, int> { [C(0, 0)] = 2, [C(1, 0)] = 1, [C(2, 0)] = 0 }, 20);
            try
            {
                Mold(level, C(0, 0)); LevelRuntimeState state = Build(level);
                foreach (RuntimeCell cell in state.Cells.Where(c => c.IsActive && c.Coordinate.Row > 0)) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
                Check(SettlementResolution.Resolve(state).State.CellAt(C(0, 0)).Cover == CoverKind.Mold, "독립 경계 낙하 중 덮개 고정");
                Hit(state, C(0, 0), Context()); SettlementResult result = SettlementResolution.Resolve(state);
                Check(result.IsApplied && result.State.CellAt(C(2, 0)).Color == RabbitColor.Type3, "독립 경계 덮개 제거 후 실제 낙하/색 보존");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void ObserveReason(string name)
        {
            LevelDefinition level = Make();
            try
            {
                Missions(level); if (name != "no-mold") Mold(level, C(4, 4));
                LevelRuntimeState state = Build(level); TurnEffectContext context = Context(name == "no-turn" ? 0 : 1);
                if (name == "single" || name == "no-candidate")
                {
                    foreach (RuntimeCell cell in state.Cells.Where(c => !c.Cover.HasValue)) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
                    if (name == "single")
                    {
                        RuntimeCell cell = state.CellAt(C(4, 5)); Set(cell, "Content", RuntimeContent.Rocket);
                        Set(cell, "RocketDirection", RocketDirection.Vertical); Set(cell, "DustDurability", 3);
                    }
                }
                if (name == "complete") foreach (RuntimeMission mission in state.Missions) Set(mission, "Progress", mission.Target);
                string input = "setup " + name + "; FinishTurn";
                if (name == "removed")
                {
                    string covered = Snapshot(state); List<EffectRecord> effects = Hit(state, C(4, 4), context);
                    Observations.Add(JsonUtility.ToJson(new MoldObservation { name = "remove-hit", input = "Hit (4,4)", seed = 12345,
                        beforeProperties = covered, afterProperties = Snapshot(state), effectProperties = Snapshot(effects) }));
                    input = "Hit (4,4); FinishTurn";
                }
                string before = Snapshot(state); MoldSpreadRecord record = End(state, context);
                Record(name, input, 12345, level, before, state, context, record);
                MoldSpreadReason expected = name == "single" ? MoldSpreadReason.Spread : name == "no-candidate" ? MoldSpreadReason.NoCandidate :
                    name == "no-turn" ? MoldSpreadReason.NoTurn : name == "no-mold" ? MoldSpreadReason.NoMold :
                    name == "complete" ? MoldSpreadReason.MissionsComplete : MoldSpreadReason.RemovedThisTurn;
                Check(record.Reason == expected && record.RandomBefore == record.RandomAfter, "번식 사유/0·1후보 무난수 " + name);
                if (name == "single") Check(state.CellAt(C(4, 5)).Content == RuntimeContent.Rocket && state.CellAt(C(4, 5)).DustDurability == 3,
                    "단일 후보 번식 내용물/먼지 보존");
                else Check(Snapshot(state) == before, "억제 사유 상태 무변경 " + name);
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void ObserveEnding(bool complete)
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, 1, RocketDirection.Horizontal, null);
            try
            {
                Mold(level, C(8, 0)); JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", level);
                BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                Check(executor.Swap(C(4, 4), C(4, 5)).IsApplied, "기록 실제 마지막 수 조합 " + complete);
                if (complete) foreach (RuntimeMission mission in executor.State.Missions) Set(mission, "Progress", mission.Target);
                string before = Snapshot(executor.State); Finish(executor);
                TurnEffectContext context = complete ? executor.WinningTurnEffects : executor.TurnEffects;
                Record("ending-" + complete, "moveCount=1; Swap (4,4)/(4,5); complete=" + complete + "; AdvanceCascade to finish", 12345,
                    level, before, executor.State, context, context.MoldSpread);
                Check(context.MoldSpread.Reason == (complete ? MoldSpreadReason.MissionsComplete : MoldSpreadReason.Spread) &&
                    executor.Outcome.Kind == (complete ? BoardOutcomeKind.Won : BoardOutcomeKind.MovesExhausted), "실제 턴 종료 사유/승패 " + complete);
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void Record(string name, string input, int seed, LevelDefinition level, string before, LevelRuntimeState state,
            TurnEffectContext context, MoldSpreadRecord record)
        {
            Observations.Add(JsonUtility.ToJson(new MoldObservation { name = name, input = input, seed = seed, turn = context.Turn,
                level = JsonUtility.ToJson(level), beforeProperties = before, afterProperties = Snapshot(state), contextProperties = Snapshot(context),
                reason = record.Reason.ToString(), candidates = record.CandidateCount, target = record.Target?.ToString(),
                randomBefore = record.RandomBefore, randomAfter = record.RandomAfter,
                missionTargets = state.Missions.Select(m => m.Target).ToArray(), missionProgress = state.Missions.Select(m => m.Progress).ToArray() }));
        }

        [Serializable]
        private sealed class MoldObservation
        {
            public string name, input, level, beforeProperties, afterProperties, contextProperties, effectProperties, reason, target;
            public int seed, turn, candidates, randomBefore, randomAfter;
            public int[] missionTargets, missionProgress;
        }
    }
}

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
    public static partial class GeneratorVerification
    {
        private const string BaselineEvidence = "Logs/ElementFramework/Stage04";
        private static readonly List<string> Observations = new List<string>();

        public static void Baseline()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(BaselineEvidence); Results.Clear(); Observations.Clear();
            try
            {
                foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance })
                    for (int required = 3; required <= 5; required++) ObserveCharge(kind, required);
                ObserveConnections();
                ObserveWire();
                File.WriteAllLines(BaselineEvidence + "/baseline-results.txt", Results);
                File.WriteAllLines(BaselineEvidence + "/generator-observations.jsonl", Observations);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error); File.WriteAllLines(BaselineEvidence + "/baseline-results.txt", Results);
                File.WriteAllLines(BaselineEvidence + "/generator-observations.jsonl", Observations);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }

        private static void ObserveCharge(ObstacleKind kind, int required)
        {
            LevelDefinition level = Make(kind, required);
            try
            {
                LevelRuntimeState state = Build(level); TurnEffectContext context = Context();
                string name = kind + "-" + required;
                Observe(name, "setup", level, state, context, 0, Array.Empty<EffectRecord>());
                for (int turn = 1; turn <= required; turn++)
                {
                    context = Next(context, turn);
                    List<EffectRecord> effects = Hit(state, C(4, 4), context);
                    Observe(name, "NextTurn(" + turn + "); Hit (4,4)", level, state, context, turn, effects);
                    List<EffectRecord> repeats = new List<EffectRecord>();
                    foreach (BoardCoordinate cell in new[] { C(4, 4), C(4, 5), C(5, 4), C(5, 5) }) repeats.AddRange(Hit(state, cell, context));
                    Observe(name, "same turn Hit (4,4),(4,5),(5,4),(5,5)", level, state, context, turn, repeats);
                }
                Check(state.Obstacles[0].Charge == required && GeneratorRules.ActiveConnections(state).Count == 0 &&
                    !state.Cells.Any(c => c.ObstacleIndex.HasValue) && state.Missions[0].Progress == 1,
                    "실측 완충/연결 전체 제거/미션 " + name);
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void ObserveConnections()
        {
            LevelDefinition level = Multiple();
            try
            {
                LevelRuntimeState state = Build(level); TurnEffectContext context = Context();
                foreach (RuntimeCell cell in state.Cells.Where(c => c.ObstacleIndex == 1)) Set(cell, "DustDurability", 2);
                string neighbor = Snapshot(state.CellAt(C(3, 8)));
                Observe("connections", "setup 3 connections; Appliance floor=2", level, state, context, 0, Array.Empty<EffectRecord>());
                Observe("connections", "Hit crate (1,4)", level, state, context, 1, Hit(state, C(1, 4), context));
                Check(GeneratorRules.ActiveConnections(state).Count == 2 && state.Obstacles[0].Charge == 0,
                    "실측 직접 제거는 연결 하나만 해제");
                for (int turn = 1; turn <= 3; turn++)
                {
                    context = Next(context, turn);
                    Observe("connections", "NextTurn(" + turn + "); Hit generator (4,4)", level, state, context, turn, Hit(state, C(4, 4), context));
                }
                Check(state.Missions.All(m => m.Remaining == 0) && state.CellAt(C(4, 7)).DustDurability == 2 && Snapshot(state.CellAt(C(3, 8))) == neighbor,
                    "실측 실제 세 턴 완충/잔여 연결 제거·먼지·주변 보존");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }

            level = Make(ObstacleKind.Appliance, 3);
            try
            {
                LevelRuntimeState state = Build(level); TurnEffectContext context = Context();
                Observe("retire", "setup one connection; Appliance durability=9", level, state, context, 0, Array.Empty<EffectRecord>());
                for (int hit = 1; hit <= 9; hit++)
                    Observe("retire", "same turn Hit (4,7) #" + hit, level, state, context, 1, Hit(state, C(4, 7), context));
                Check(state.Obstacles[0].Charge == 0 && GeneratorRules.ActiveConnections(state).Count == 0 &&
                    context.Generators.Count(g => g.Event == GeneratorEvent.Retired) == 1 && state.Missions[0].Progress == 1,
                    "실측 마지막 연결 제거/무충전 자동 철거 단일 기록");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void ObserveWire()
        {
            LevelDefinition level = Make(ObstacleKind.Crate, 3);
            try
            {
                LevelRuntimeState state = Build(level); TurnEffectContext context = Context();
                Observe("wire", "setup wire (4,6),(4,7)", level, state, context, 0, Array.Empty<EffectRecord>());
                Observe("wire", "Hit wire-only Normal (4,6)", level, state, context, 1, Hit(state, C(4, 6), context));
                Check(state.Obstacles[0].Charge == 0 && GeneratorRules.ActiveConnections(state).Count == 1 && context.Generators.Count == 0,
                    "전선 표시 칸 타격은 발전기를 충전/단선하지 않음");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void Observe(string name, string input, LevelDefinition level, LevelRuntimeState state,
            TurnEffectContext context, int turn, IEnumerable<EffectRecord> effects)
        {
            Observations.Add(JsonUtility.ToJson(new GeneratorObservation
            {
                name = name, input = input, seed = 12345, turn = turn, level = JsonUtility.ToJson(level),
                charges = state.Obstacles.Select(o => o.Charge).ToArray(),
                occupiedBodies = state.Cells.Where(c => c.ObstacleIndex.HasValue).Select(c => c.ObstacleIndex.Value).Distinct().OrderBy(i => i).ToArray(),
                activeConnections = GeneratorRules.ActiveConnections(state).Count,
                missionProgress = state.Missions.Select(m => m.Progress).ToArray(),
                stateProperties = Snapshot(state), contextProperties = Snapshot(context), effectProperties = Snapshot(effects.ToArray())
            }));
        }

        [Serializable]
        private sealed class GeneratorObservation
        {
            public string name, input, level, stateProperties, contextProperties, effectProperties;
            public int seed, turn, activeConnections;
            public int[] charges, occupiedBodies, missionProgress;
        }
    }
}

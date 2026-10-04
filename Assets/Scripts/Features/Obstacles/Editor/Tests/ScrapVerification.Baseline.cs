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
    public static partial class ScrapVerification
    {
        private const string BaselineEvidence = "Logs/ElementFramework/Stage06";
        private static readonly List<string> Observations = new List<string>();

        public static void Baseline()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(BaselineEvidence); Results.Clear(); Observations.Clear();
            try
            {
                for (int durability = 1; durability <= 5; durability++) ObserveDamage(durability);
                ObserveFixed(); ObserveMaintain(); ObserveFailure();
                File.WriteAllLines(BaselineEvidence + "/baseline-results.txt", Results);
                File.WriteAllLines(BaselineEvidence + "/scrap-observations.jsonl", Observations);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error); File.WriteAllLines(BaselineEvidence + "/baseline-results.txt", Results);
                File.WriteAllLines(BaselineEvidence + "/scrap-observations.jsonl", Observations); Debug.LogException(error); EditorApplication.Exit(1);
            }
        }

        private static void ObserveDamage(int durability)
        {
            LevelDefinition level = Make();
            try
            {
                Place(level, C(4, 4), durability); Mission(level, 1); LevelRuntimeState state = Build(level); TurnEffectContext context = Context();
                Set(state.CellAt(C(4, 4)), "DustDurability", 3); string before = Snapshot(state);
                List<EffectRecord> effects = Hit(state, C(4, 4), context);
                Record("damage-" + durability, "Hit (4,4)", level, before, state, context, effects);
                Check(state.Obstacles[0].Durability == durability - 1 && state.Missions[0].Progress == (durability == 1 ? 1 : 0), "기록 실제 고철 피해/미션 " + durability);
                if (durability == 1) return;
                Empty(state, state.Cells.Where(c => c.Content == RuntimeContent.Normal).Select(c => c.Coordinate).ToArray());
                before = Snapshot(state); SettlementResult result = SettlementResolution.Resolve(state, context);
                Check(result.IsApplied, "기록 고철 정착 성공 " + durability);
                Record("fall-" + durability, "clear normals; Resolve", level, before, result.State, result.TurnEffects, result.Records);
                RuntimeCell moved = result.State.Cells.Single(c => c.ObstacleIndex == 0);
                Check(moved.Coordinate.Equals(C(8, 4)) && result.State.Obstacles[0].Definition.Coordinate.Equals(C(4, 4)) &&
                    result.State.CellAt(C(4, 4)).DustDurability == 3, "점유 좌표 이동/정의 초기 좌표·바닥 보존 " + durability);
                Occupancy(result.State, "실측 낙하" + durability);
                before = Snapshot(result.State); effects = Hit(result.State, moved.Coordinate, result.TurnEffects);
                Record("repeat-" + durability, "same turn Hit (8,4)", level, before, result.State, result.TurnEffects, effects);
                Check(result.State.Obstacles[0].Durability == durability - 1, "낙하 후 동일 턴 피해 제한 " + durability);
                TurnEffectContext next = (TurnEffectContext)Invoke(typeof(TurnEffectContext), "NextTurn", result.TurnEffects, 2);
                before = Snapshot(result.State); effects = Hit(result.State, moved.Coordinate, next);
                Record("next-" + durability, "NextTurn(2); Hit (8,4)", level, before, result.State, next, effects);
                Check(result.State.Obstacles[0].Durability == durability - 2 && state.Obstacles[0].Durability == durability - 1,
                    "실제 다음 턴 피해/원본 사본 독립 " + durability);
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void ObserveFixed()
        {
            LevelDefinition level = Make();
            try
            {
                Fixed(level, C(0, 0), new SupplyItem(SupplyKind.Scrap, 2, durability: 2), new SupplyItem(SupplyKind.Bomb));
                LevelRuntimeState state = Build(level); TurnEffectContext context = Context(); int turn = 1;
                for (int n = 0; n < 3; n++)
                {
                    Empty(state, new[] { C(0, 0) }); string before = Snapshot(state);
                    SettlementResult result = SettlementResolution.Resolve(state, context); Check(result.IsApplied, "기록 고정 공급 " + n);
                    state = result.State; context = result.TurnEffects;
                    Record("fixed", "clear (0,0); Resolve #" + n, level, before, state, context, result.Records);
                    if (n == 2) { Check(state.CellAt(C(0, 0)).Content == RuntimeContent.Bomb, "고정 목록 고철2개 후 폭탄"); continue; }
                    before = Snapshot(state); List<EffectRecord> effects = Hit(state, C(0, 0), context);
                    Record("fixed-new-body", "Hit new body #" + n, level, before, state, context, effects);
                    Check(state.CellAt(C(0, 0)).ObstacleIndex == n && state.Obstacles[n].Durability == 1 && state.Supply.ScrapGenerated == 0,
                        "새 본체 키/기존 피해 미상속/유지 카운터 분리 " + n);
                    context = (TurnEffectContext)Invoke(typeof(TurnEffectContext), "NextTurn", context, ++turn);
                    before = Snapshot(state); effects = Hit(state, C(0, 0), context);
                    Record("fixed-remove", "NextTurn(" + turn + "); Hit (0,0)", level, before, state, context, effects);
                }
                Empty(state, new[] { C(0, 0) }); string last = Snapshot(state); SettlementResult stop = SettlementResolution.Resolve(state, context);
                Record("fixed-stop", "clear Bomb fixture; Resolve exhausted Stop", level, last, stop.State, stop.TurnEffects, stop.Records);
                Check(stop.IsApplied && stop.State.CellAt(C(0, 0)).Content == RuntimeContent.Empty && stop.State.Supply.Sources[0].ItemIndex == 2,
                    "기록 고정 소진 Stop/커서 끝");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void ObserveMaintain()
        {
            LevelDefinition level = Make();
            try
            {
                Maintain(level, new[] { C(0, 0), C(0, 2) }, 2, 3, 1); LevelRuntimeState state = Build(level); TurnEffectContext context = Context();
                for (int round = 0; round < 3; round++)
                {
                    Empty(state, new[] { C(0, 0), C(0, 2) }); string before = Snapshot(state); SettlementResult result = SettlementResolution.Resolve(state, context);
                    Check(result.IsApplied, "기록 유지 공급 성공 " + round); state = result.State; context = result.TurnEffects;
                    Record("maintain", "target=2 limit=3 durability=1; clear sources; Resolve #" + round, level, before, state, context, result.Records);
                    Check(state.LiveScrapCount == (round == 0 ? 2 : round == 1 ? 1 : 0) && state.Supply.ScrapGenerated == (round == 0 ? 2 : 3),
                        "기록 부족량/누적 한도 " + round);
                    List<EffectRecord> hits = new List<EffectRecord>(); before = Snapshot(state);
                    foreach (RuntimeCell cell in state.Cells.Where(c => c.Content == RuntimeContent.Obstacle).ToArray()) hits.AddRange(Hit(state, cell.Coordinate, context));
                    Record("maintain-remove", "same turn remove supplied bodies #" + round, level, before, state, context, hits);
                }
                Check(state.Obstacles.Count == 3 && state.Supply.ScrapRemaining == 0 && state.LiveScrapCount == 0, "기록 한도 소진/키 비재사용");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void ObserveFailure()
        {
            foreach (bool maintain in new[] { false, true })
            {
                LevelDefinition level = Sparse(C(0, 0), C(1, 1), C(2, 1));
                try
                {
                    LevelFlowEditing.SetPortal(level, C(2, 1), C(0, 0));
                    if (maintain) Maintain(level, new[] { C(0, 0) }, 1, 3, 2); else Fixed(level, C(0, 0), new SupplyItem(SupplyKind.Scrap, durability: 2));
                    LevelRuntimeState state = Build(level); Empty(state, state.Cells.Where(c => c.IsActive).Select(c => c.Coordinate).ToArray()); string before = Snapshot(state);
                    SettlementResult result = SettlementResolution.Resolve(state);
                    Record("failure-" + maintain, "portal (2,1)->(0,0); clear all; Resolve; reason=" + result.Reason, level, before, state, null, result);
                    Check(result.Reason == SettlementReason.Repeating && result.State == null && Snapshot(state) == before, "기록 공급 실패 원본/커서/카운터/난수 보존 " + maintain);
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
        }

        private static void Record(string name, string input, LevelDefinition level, string before, LevelRuntimeState state, TurnEffectContext context, object records)
        {
            Observations.Add(JsonUtility.ToJson(new ScrapObservation { name = name, input = input, seed = 12345, level = JsonUtility.ToJson(level),
                beforeProperties = before, afterProperties = Snapshot(state), contextProperties = context == null ? null : Snapshot(context), recordsProperties = Snapshot(records),
                bodyDurabilities = state.Obstacles.Select(o => o.Durability).ToArray(), occupiedCoordinates = state.Cells.Where(c => c.ObstacleIndex.HasValue).Select(c => c.ObstacleIndex + "@" + c.Coordinate).ToArray(),
                missionProgress = state.Missions.Select(m => m.Progress).ToArray(), sourceIndices = state.Supply.Sources.Select(s => s.ItemIndex).ToArray(),
                sourceConsumed = state.Supply.Sources.Select(s => s.ItemConsumed).ToArray(), generated = state.Supply.ScrapGenerated, remaining = state.Supply.ScrapRemaining,
                live = state.LiveScrapCount, randomDraws = state.Random.DrawCount }));
        }

        [Serializable]
        private sealed class ScrapObservation
        {
            public string name, input, level, beforeProperties, afterProperties, contextProperties, recordsProperties;
            public string[] occupiedCoordinates;
            public int seed, generated, remaining, live, randomDraws;
            public int[] bodyDurabilities, missionProgress, sourceIndices, sourceConsumed;
        }
    }
}

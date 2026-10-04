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
    public static partial class LayerVerification
    {
        private const string BaselineEvidence = "Logs/ElementFramework/Stage03";
        private static readonly List<string> Observations = new List<string>();

        public static void Baseline()
        {
            Directory.CreateDirectory(BaselineEvidence); Results.Clear(); Observations.Clear();
            try
            {
                for (int durability = 1; durability <= 3; durability++) ObserveTurns(durability);
                ObserveTransformation();
                ObserveFloorMovement();
                File.WriteAllLines(BaselineEvidence + "/baseline-results.txt", Results);
                File.WriteAllLines(BaselineEvidence + "/observations.txt", Observations);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error); File.WriteAllLines(BaselineEvidence + "/baseline-results.txt", Results);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }

        private static void ObserveTurns(int durability)
        {
            LevelDefinition level = Make();
            try
            {
                Missions(level); LevelRuntimeState state = Build(level, 12345);
                RuntimeCell cell = state.CellAt(C(4, 4)); Web(cell, durability); Set(cell, "DustDurability", durability);
                Set(cell, "Color", RabbitColor.Type1); TurnEffectContext context = Context();
                RecordLayer("web" + durability, "seed=12345; setup (4,4) Normal Type1; hit 1", state, cell, context, Hit(state, C(4, 4), context));
                Check(cell.CoverDurability == durability - 1 && cell.Content == RuntimeContent.Normal && cell.DustDurability == durability,
                    "기록 사례 덮개 우선/내용물·바닥 보존 " + durability);
                if (durability > 1)
                {
                    Hit(state, C(4, 4), context);
                    Check(cell.CoverDurability == durability - 1, "동일 문맥 거미줄 제한 " + durability);
                    TurnEffectContext next = (TurnEffectContext)Invoke(typeof(TurnEffectContext), "NextTurn", context, 2);
                    RecordLayer("web-next" + durability, "NextTurn(2); hit 3", state, cell, next, Hit(state, C(4, 4), next));
                    Check(cell.CoverDurability == durability - 2 && cell.DustDurability == durability, "실제 다음 턴 거미줄 재피해 " + durability);
                }
                Set(cell, "Cover", null); Set(cell, "CoverDurability", 0);
                context = Context();
                RecordLayer("dust" + durability, "clear cover fixture; hit normal", state, cell, context, Hit(state, C(4, 4), context));
                Check(cell.DustDurability == durability - 1 && cell.Content == RuntimeContent.Empty && state.Missions[3].Progress == (durability == 1 ? 1 : 0),
                    "먼지 실제 소비/제거 미션 " + durability);
                Set(cell, "Content", RuntimeContent.Normal); Set(cell, "Color", RabbitColor.Type1); Hit(state, C(4, 4), context);
                Check(cell.DustDurability == durability - 1, "동일 턴 재유입 먼지 제한 " + durability);
                Set(cell, "Content", RuntimeContent.Normal); Set(cell, "Color", RabbitColor.Type1);
                TurnEffectContext dustNext = (TurnEffectContext)Invoke(typeof(TurnEffectContext), "NextTurn", context, 2);
                RecordLayer("dust-next" + durability, "new normal; NextTurn(2); consume", state, cell, dustNext, Hit(state, C(4, 4), dustNext));
                Check(cell.DustDurability == Math.Max(0, durability - 2), "실제 다음 턴 먼지 재피해/0 유지 " + durability);
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void ObserveFloorMovement()
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null,
                new Dictionary<BoardCoordinate, int> { [C(0, 0)] = 0, [C(1, 0)] = 1 }, 20);
            try
            {
                LevelRuntimeState state = Build(level); Set(state.CellAt(C(1, 0)), "Content", RuntimeContent.Empty);
                Set(state.CellAt(C(1, 0)), "Color", null); Set(state.CellAt(C(0, 0)), "DustDurability", 2);
                Set(state.CellAt(C(1, 0)), "DustDurability", 3); string before = Snapshot(state);
                SettlementResult result = SettlementResolution.Resolve(state, Context());
                Check(result.IsApplied && result.Records.Count > 0 && result.State.CellAt(C(1, 0)).Content == RuntimeContent.Normal,
                    "일반 블록 실제 수직 낙하");
                Check(result.State.CellAt(C(0, 0)).DustDurability == 2 && result.State.CellAt(C(1, 0)).DustDurability == 3 && Snapshot(state) == before,
                    "출발/도착 먼지 좌표 잔류·원본 보존");
                Observations.Add("movement seed=12345; clear (1,0); Resolve; records=" + result.Records.Count + "\nBEFORE properties: " + before + "\nAFTER properties: " + Snapshot(result.State));
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void ObserveTransformation()
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, 6, RocketDirection.Horizontal, null);
            try
            {
                Missions(level); LevelRuntimeState state = Build(level);
                foreach (RuntimeCell cell in state.Cells.Where(c => c.Content == RuntimeContent.Normal)) Set(cell, "DustDurability", 2);
                BoardActionExecutor executor = new BoardActionExecutor(state);
                BoardActionResult result = executor.Swap(C(4, 4), C(4, 5));
                Check(result.IsApplied && executor.TurnEffects.Combination.Transformations.Count > 0 &&
                    executor.TurnEffects.Combination.Transformations.All(t => executor.State.CellAt(t.Coordinate).DustDurability == 1),
                    "기록 사례 자석·로켓 변환/후속 효과 먼지 단일 피해");
                Observations.Add("transform seed=12345; pair=6 Magnet+Rocket Horizontal; all Normal dust=2; Swap (4,4)/(4,5)\nSTATE properties: "
                    + Snapshot(executor.State) + "\nCONTEXT properties: " + Snapshot(executor.TurnEffects));
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void RecordLayer(string name, string input, LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context, List<EffectRecord> effects)
        {
            Observations.Add(name + "; " + input + "; content=" + cell.Content + "; web=" + cell.CoverDurability + "; dust=" + cell.DustDurability
                + "; missions=" + string.Join(",", state.Missions.Select(m => m.Progress)) + "; effects=" + effects.Count
                + "\nCONTEXT properties: " + Snapshot(context) + "\nEFFECTS properties: " + Snapshot(effects));
        }
    }
}

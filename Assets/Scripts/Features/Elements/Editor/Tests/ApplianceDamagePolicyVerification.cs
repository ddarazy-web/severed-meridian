using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    /// <summary>저장한 동일 메모리 입력으로 피해 경계를 비교한다.</summary>
    public static class ApplianceDamagePolicyVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage22";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static readonly Type Rules = typeof(DamageReaction).Assembly.GetType("Simulation.ObstacleDamageRules");
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static void Check(bool pass, string name)
        { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static object Invoke(Type type, string method, params object[] args) =>
            type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        // 비공개 턴 기록을 빠뜨리지 않도록 튜플 필드도 기록한다.
        private static string Deep(object value)
        {
            if (value == null) return "null";
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string) return Snapshot(value);
            if (value is IEnumerable items) return "[" + string.Join("|", items.Cast<object>().Select(Deep)) + "]";
            return Snapshot(value) + string.Join("|", type.GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(field => field.Name).Select(field => field.Name + "=" + Deep(field.GetValue(value))));
        }
        private static string Context(TurnEffectContext context) => context == null ? "null" :
            string.Join("|", typeof(TurnEffectContext).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).OrderBy(field => field.Name).Select(field => field.Name + "=" + Deep(field.GetValue(context))));
        private static TurnEffectContext Fresh() => (TurnEffectContext)Invoke(typeof(FixedObstacleVerification), "Context");
        private static LevelRuntimeState Build(LevelDefinition level) => (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
        [Serializable] private sealed class Row { public string name, input, output; }
        private static void Record(string name, string input, string output) => Values.Add(JsonUtility.ToJson(new Row { name = name, input = input, output = output }));
        private static LevelDefinition Input(string name, bool before, Action<LevelDefinition> prepare)
        {
            LevelDefinition level = before ? (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make") : ScriptableObject.CreateInstance<LevelDefinition>();
            string path = Evidence + "/input-" + name + ".json";
            if (before) { prepare(level); File.WriteAllText(path, JsonUtility.ToJson(level)); }
            else JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level);
            return level;
        }
        private static void Set(object owner, string property, object value) => owner.GetType().GetProperty(property).GetSetMethod(true).Invoke(owner, new[] { value });
        private static void Query(LevelRuntimeState state, BoardCoordinate target, BoardCoordinate source, DamageCause cause, TurnEffectContext context, string name, DamageResponse? expected = null, int hit = 7, RabbitColor? sourceColor = RabbitColor.Type1)
        {
            string initial = Snapshot(state), turn = Context(context), global = JsonUtility.ToJson(UnityEngine.Random.state);
            DamageReaction outer = DamageReaction.Evaluate(state, target, cause, source, context, sourceColor, hit);
            DamageReaction inner = (DamageReaction)Invoke(Rules, "Query", state, state.CellAt(target), cause, sourceColor, context, hit);
            object mission = MissionProgressRules.Query(state, target, context);
            int[] reserved = new[] { 0, 1, 2, 4 }.Select(cells => (int)Invoke(Rules, "ReservedDamage", state.Obstacles[state.CellAt(target).ObstacleIndex.Value], cells)).ToArray();
            Check(initial == Snapshot(state) && turn == Context(context) && global == JsonUtility.ToJson(UnityEngine.Random.state), "상태/비공개 문맥/규칙·전역 난수 무변경 " + name);
            if (expected.HasValue) Check(outer.Response == expected.Value, "외부 경계 실제 응답 " + name);
            Record("query", name + "/" + (int)cause + "/source=" + source + "/target=" + target + "/hit=" + hit + "/sourceColor=" + sourceColor, "state=" + initial + ";context=" + turn + ";outer=" + Snapshot(outer) + ";inner=" + Snapshot(inner) + ";mission=" + Snapshot(mission) + ";reserved=" + string.Join(",", reserved));
        }
        public static void Before() => Execute(true);
        public static void Run() => Execute(false);
        private static void Execute(bool before)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear(); int exit = 0;
            try
            {
                QueryChecks(before); ApplyChecks(before); AdjacentChecks(before); HitChecks(before); ReservationChecks(before); OverlapChecks(before); OtherChecks(before); GeneratorChecks(before);
                Check(LevelPackCodec.LegacyFormatVersion == 1 && LevelPackCodec.LevelsPerPack == 50, "팩 버전1/50구간 유지");
                if (before) File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values);
                else { Check(RecordedLogicComparison.Equal(File.ReadAllLines(Evidence + "/baseline-values.jsonl"), Values), "기존 논리/입력/팩 비교 · 추가 비행 표시 이력 별도"); PolicyChecks(); }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                string mode = before ? "before" : "after";
                File.WriteAllLines(Evidence + "/" + mode + "-results.txt", Results); File.WriteAllLines(Evidence + "/" + mode + "-values.jsonl", Values);
            }
            EditorApplication.Exit(exit);
        }
        private static void QueryChecks(bool before)
        {
            BoardCoordinate[] cells = LevelPlacementRules.Footprint(C(4, 4), 2).ToArray();
            for (int durability = 1; durability <= 9; durability++)
            {
                LevelDefinition level = Input("query-" + durability, before, item =>
                {
                    Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.Appliance, durability, C(4, 4), RabbitColor.Type1);
                    JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Appliance + ",\"count\":1}]}", item);
                });
                try
                {
                    for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                    {
                        BoardCoordinate target = cells[cellIndex], source = C(target.Row == 4 ? 3 : 6, target.Column);
                        foreach (DamageCause cause in new[] { DamageCause.AdjacentMatch, DamageCause.Power, DamageCause.MagnetAdjacent, DamageCause.Hammer, (DamageCause)(-1), (DamageCause)4 })
                            foreach (string mode in new[] { "fresh", "null-context", "hit0", "same-hit", "other-cell", "other-hit", "next-turn", "removed" })
                            {
                                LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh(); int hit = mode == "hit0" ? 0 : mode == "other-hit" ? 8 : 7;
                                if (mode == "null-context") context = null;
                                if (mode == "hit0" || mode == "same-hit" || mode == "other-cell" || mode == "other-hit" || mode == "next-turn")
                                    typeof(TurnEffectContext).GetMethod("RegisterHit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(context, new object[] { mode == "hit0" ? 0 : 7, mode == "other-cell" ? cells[(cellIndex + 1) % 4] : target });
                                if (mode == "next-turn") context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, 2);
                                if (mode == "removed") Set(state.Obstacles[0], "Durability", 0);
                                DamageResponse expected = cause == DamageCause.MagnetAdjacent || mode == "removed" ? DamageResponse.None : mode == "same-hit" ? DamageResponse.AlreadyDamaged : DamageResponse.Damage;
                                Query(state, target, source, cause, context, "d" + durability + "/cell" + cellIndex + "/" + mode, expected, hit, mode == "null-context" ? (RabbitColor?)null : RabbitColor.Type1);
                            }
                        foreach (string boundary in new[] { "distance", "wall", "inactive", "protected" })
                        {
                            LevelDefinition wall = null;
                            try
                            {
                                LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
                                if (boundary == "inactive") typeof(RuntimeCell).GetField("<IsActive>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state.CellAt(target), false);
                                if (boundary == "protected") ((HashSet<BoardCoordinate>)typeof(TurnEffectContext).GetField("protectedPowers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context)).Add(target);
                                if (boundary == "wall")
                                {
                                    wall = Input("wall-" + durability + "-" + cellIndex, before, item => { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(level), item); Check(LevelFlowEditing.SetWalls(item, new[] { new BoardEdge(source, target) }, false) == null, "금속기둥 인접 벽 준비"); });
                                    state = Build(wall);
                                }
                                foreach (DamageCause cause in new[] { DamageCause.AdjacentMatch, DamageCause.Power, DamageCause.MagnetAdjacent, DamageCause.Hammer })
                                    Query(state, target, boundary == "distance" ? C(0, 0) : source, cause, context, "d" + durability + "/cell" + cellIndex + "/" + boundary);
                            }
                            finally { if (wall != null) UnityEngine.Object.DestroyImmediate(wall); }
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
        }
        private static void ApplyChecks(bool before)
        {
            for (int durability = 1; durability <= 9; durability++)
                foreach (bool hammer in new[] { false, true })
                {
                    LevelDefinition level = Input("apply-" + durability + "-" + hammer, before, item =>
                    {
                        Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.Appliance, durability, C(4, 4), RabbitColor.Type1);
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Appliance + ",\"count\":1}]}", item);
                    });
                    try
                    {
                        LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh(); string original = JsonUtility.ToJson(level), global = JsonUtility.ToJson(UnityEngine.Random.state); byte[] packed = LevelPackCodec.Encode(new[] { level }); int draws = state.Random.DrawCount;
                        for (int step = 0; step < durability + 2; step++)
                        {
                            if (step % 2 == 0) context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, step / 2 + 1);
                            List<EffectRecord> effects = new List<EffectRecord>();
                            if (hammer) { object[] args = { state, C(4, 4), context, effects, null }; Check((bool)Invoke(typeof(PowerEffectResolution), "ApplyHammer", args), "실제 망치 실행 " + durability + "/" + step); }
                            else effects = (List<EffectRecord>)Invoke(typeof(FixedObstacleVerification), "Hit", state, C(4, 4), context);
                            int remaining = Math.Max(0, durability - step - 1);
                            Check(state.Obstacles[0].Durability == remaining && state.Missions[0].Progress == (remaining == 0 ? 1 : 0), "실제 다른hit 반복/다음턴/제거 미션 " + durability + "/" + hammer + "/" + step);
                            Record("apply", durability + "/hammer=" + hammer + "/step=" + step, Snapshot(state) + ";effects=" + Snapshot(effects) + ";context=" + Context(context));
                        }
                        Check(!state.Cells.Any(cell => cell.ObstacleIndex == 0) && state.Random.DrawCount == draws && JsonUtility.ToJson(UnityEngine.Random.state) == global && JsonUtility.ToJson(level) == original && packed.SequenceEqual(LevelPackCodec.Encode(new[] { level })), "실제 4칸 제거/난수/원본 ID/바이트 유지 " + durability + hammer);
                        Record("pack", original, Convert.ToBase64String(packed));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static void AdjacentChecks(bool before)
        {
            for (int durability = 1; durability <= 9; durability++)
                foreach (bool wall in new[] { false, true })
                {
                    LevelDefinition level = Input("adjacent-" + durability + "-" + wall, before, item =>
                    {
                        foreach (BoardCoordinate cell in new[] { C(3, 2), C(3, 4), C(2, 3) }) Invoke(typeof(PowerEffectVerification), "Place", item, cell, InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type1);
                        Invoke(typeof(PowerEffectVerification), "Place", item, C(3, 3), InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type2);
                        Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.Appliance, durability, C(4, 3), RabbitColor.Type1);
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Appliance + ",\"count\":1}]}", item);
                        if (wall) Check(LevelFlowEditing.SetWalls(item, new[] { new BoardEdge(C(3, 3), C(4, 3)) }, false) == null, "실제 매칭 인접 벽 준비");
                    });
                    try
                    {
                        BoardActionExecutor executor = new BoardActionExecutor(Build(level)); BoardActionResult result = executor.Swap(C(2, 3), C(3, 3));
                        Check(result.IsApplied && executor.State.Obstacles[0].Durability == Math.Max(0, durability - (wall ? 1 : 2)) && executor.State.Missions[0].Progress == (durability <= (wall ? 1 : 2) ? 1 : 0), "실제 일반 매칭 피해/벽/미션 " + durability + wall);
                        Record("adjacent", JsonUtility.ToJson(level), Snapshot(executor.State) + ";result=" + Snapshot(result) + ";context=" + Context(executor.TurnEffects));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static void HitChecks(bool before)
        {
            for (int durability = 1; durability <= 9; durability++)
                foreach (int firstHit in new[] { 0, 7 })
                {
                    LevelDefinition level = Input("hits-" + durability + "-" + firstHit, before, item =>
                    {
                        Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.Appliance, durability, C(4, 4), RabbitColor.Type1);
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Appliance + ",\"count\":1}]}", item);
                    });
                    try
                    {
                        LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh(); int applied = 0;
                        BoardCoordinate[] cells = LevelPlacementRules.Footprint(C(4, 4), 2).ToArray();
                        foreach (var request in new (BoardCoordinate cell, int hit)[] { (cells[0], firstHit), (cells[0], firstHit), (cells[1], firstHit), (cells[0], firstHit + 1), (cells[2], firstHit + 1), (cells[3], firstHit + 1) })
                        {
                            DamageReaction reaction = DamageReaction.Evaluate(state, request.cell, DamageCause.Power, request.cell, context, null, request.hit);
                            if (reaction.Response == DamageResponse.Damage) { Invoke(Rules, "Apply", state, state.CellAt(request.cell), context, request.hit); applied++; }
                            Check(state.Obstacles[0].Durability == Math.Max(0, durability - applied), "실제 칸/hit 적용 내구도 " + durability + "/" + firstHit + "/" + applied);
                            Record("hit-apply", durability + "/firstHit=" + firstHit + "/cell=" + request.cell + "/hit=" + request.hit, Snapshot(reaction) + ";state=" + Snapshot(state) + ";context=" + Context(context));
                        }
                        int expected = Math.Min(durability, firstHit == 0 ? 6 : 5);
                        Check(applied == expected && state.Missions[0].Progress == (applied == durability ? 1 : 0), "hit0/같은칸 중복/다른칸/다른hit/미션 " + durability + "/" + firstHit);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static void ReservationChecks(bool before)
        {
            for (int durability = 1; durability <= 9; durability++)
            {
                LevelDefinition level = Input("reservation-" + durability, before, item =>
                {
                    Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.Appliance, durability, C(4, 4), RabbitColor.Type1);
                    JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Appliance + ",\"count\":1}]}", item);
                });
                try
                {
                    LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh(); string initial = Snapshot(state), turn = Context(context), global = JsonUtility.ToJson(UnityEngine.Random.state);
                    foreach (int count in new[] { 0, 1, 2, 4 })
                    {
                        DroneImpact[] impacts = LevelPlacementRules.Footprint(C(4, 4), 2).Take(count).Select(cell => (DroneImpact)Activator.CreateInstance(typeof(DroneImpact), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { state.CellAt(cell), MissionProgressRules.Query(state, cell, context) }, null)).ToArray();
                        object projection = Invoke(typeof(MissionProgressRules), "Project", state, impacts.Concat(impacts).ToArray());
                        int damage = (int)projection.GetType().GetField("Item2").GetValue(projection);
                        Check(damage == Math.Min(durability, count) && initial == Snapshot(state) && turn == Context(context) && global == JsonUtility.ToJson(UnityEngine.Random.state), "실제 중복 예약4칸/내구도 상한/조회 무변경 " + durability + "/" + count);
                        Record("reservation", durability + "/cells=" + count + "/duplicates=true", Deep(projection) + ";state=" + initial + ";context=" + turn);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
        }
        private static void OverlapChecks(bool before)
        {
            var cases = new (string name, int pair, BoardCoordinate power, BoardCoordinate body, int hits)[] {
                ("horizontal", -1, C(4, 0), C(4, 4), 2),
                ("vertical", -2, C(0, 4), C(4, 4), 2),
                ("bomb-one", -3, C(4, 4), C(2, 2), 1),
                ("bomb-two", -3, C(4, 4), C(2, 3), 2),
                ("bomb-area-four", 3, C(4, 4), C(2, 3), 4),
                ("bomb-zero", -3, C(4, 4), C(1, 1), 0),
                ("rocket-rocket", 0, C(4, 4), C(3, 6), 2),
                ("rocket-bomb-four", 1, C(4, 4), C(3, 6), 4),
                ("rocket-bomb-two", 1, C(4, 4), C(2, 7), 2),
                ("rocket-bomb-three", 1, C(4, 4), C(2, 3), 3),
                ("bomb-bomb-one", 3, C(4, 4), C(1, 2), 1),
                ("bomb-bomb-two", 3, C(4, 4), C(1, 3), 2),
                ("bomb-bomb-four", 3, C(4, 4), C(2, 3), 4),
                ("magnet-magnet", 9, C(4, 4), C(0, 0), 4)
            };
            foreach (var test in cases)
                for (int durability = 1; durability <= 9; durability++)
                {
                    LevelDefinition level = Input("overlap-" + test.name + "-" + durability, before, item =>
                    {
                        if (test.pair >= 0)
                        {
                            LevelDefinition fixture = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", test.pair, RocketDirection.Horizontal, null);
                            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture), item); UnityEngine.Object.DestroyImmediate(fixture);
                        }
                        Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.Appliance, durability, test.body, RabbitColor.Type1);
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Appliance + ",\"count\":1}]}", item);
                        if (test.pair < 0) Invoke(typeof(PowerEffectVerification), "Place", item, test.power, test.pair == -3 ? InitialBlockKind.Bomb : InitialBlockKind.Rocket, test.pair == -2 ? RocketDirection.Vertical : RocketDirection.Horizontal, RabbitColor.Type1);
                    });
                    try
                    {
                        LevelRuntimeState source = Build(level); string initial = Snapshot(source), original = JsonUtility.ToJson(level), global = JsonUtility.ToJson(UnityEngine.Random.state); byte[] packed = LevelPackCodec.Encode(new[] { level });
                        BoardActionExecutor executor = new BoardActionExecutor(source); BoardActionResult result = test.pair < 0 ? executor.Activate(test.power) : executor.Swap(C(4, 4), C(4, 5));
                        int body = source.CellAt(test.body).ObstacleIndex.Value, expected = Math.Min(durability, test.hits);
                        EffectRecord[] hits = result.Effects.Where(effect => effect.Response == DamageResponse.Damage && source.CellAt(effect.Target).ObstacleIndex == body).ToArray();
                        Check(result.IsApplied && executor.State.Obstacles[body].Durability == durability - expected && hits.Length == expected, "실제 범위 겹침/내구도 하한 " + test.name + "/" + durability);
                        Check(hits.Select(hit => (hit.HitGroup, hit.Target)).Distinct().Count() == hits.Length && hits.Select((hit, index) => hit.DurabilityBefore == durability - index && hit.DurabilityAfter == durability - index - 1).All(value => value), "칸/hit 중복 없음/단계 효과 " + test.name + "/" + durability);
                        Check(executor.State.Cells.Count(cell => cell.ObstacleIndex == body) == (expected == durability ? 0 : 4) && executor.State.Missions[0].Progress == (expected == durability ? 1 : 0), "4칸 점유/본체 미션1 " + test.name + "/" + durability);
                        Check(initial == Snapshot(source) && original == JsonUtility.ToJson(level) && global == JsonUtility.ToJson(UnityEngine.Random.state) && packed.SequenceEqual(LevelPackCodec.Encode(new[] { level })), "범위 원본/ID/전역 난수/바이트 유지 " + test.name + "/" + durability);
                        Record("overlap", test.name + "/d=" + durability + "/input=" + original, Snapshot(executor.State) + ";result=" + Snapshot(result) + ";context=" + Context(executor.TurnEffects));
                        Record("overlap-pack", original, Convert.ToBase64String(packed));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static void OtherChecks(bool before)
        {
            foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.ColorLock, ObstacleKind.Safe, ObstacleKind.Generator, (ObstacleKind)(-1), (ObstacleKind)6 })
            {
                LevelDefinition level = Input("other-" + (int)kind, before, item =>
                {
                    if (kind == ObstacleKind.Generator)
                    {
                        LevelDefinition fixture = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.Crate, 3);
                        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture), item); UnityEngine.Object.DestroyImmediate(fixture);
                    }
                    else
                    {
                        ObstacleKind placed = (int)kind < 0 || (int)kind > 5 ? ObstacleKind.Crate : kind;
                        Invoke(typeof(FixedObstacleVerification), "Obstacle", item, placed, LevelPlacementRules.MaxDurability(placed), C(4, 7), RabbitColor.Type1);
                    }
                });
                try
                {
                    LevelRuntimeState state = Build(level); BoardCoordinate target = kind == ObstacleKind.Generator ? C(4, 4) : C(4, 7);
                    if ((int)kind < 0 || (int)kind > 5)
                    {
                        RuntimeObstacle body = state.Obstacles[state.CellAt(target).ObstacleIndex.Value];
                        ObstaclePlacementDefinition invalid = (ObstaclePlacementDefinition)Activator.CreateInstance(typeof(ObstaclePlacementDefinition), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { body.Definition.Id, target, kind, 6, RabbitColor.Type1, 0 }, null);
                        typeof(RuntimeObstacle).GetField("<Definition>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(body, invalid);
                    }
                    foreach (DamageCause cause in new[] { DamageCause.AdjacentMatch, DamageCause.Power, DamageCause.MagnetAdjacent, DamageCause.Hammer, (DamageCause)(-1), (DamageCause)4 })
                        foreach (RabbitColor color in new[] { RabbitColor.Type1, RabbitColor.Type2 })
                        {
                            // 색 조건도 기존 내부 조회에서 유지한다.
                            Query(state, target, C(target.Row - 1, target.Column), cause, Fresh(), "other" + (int)kind + "/" + (int)cause + "/" + color, null, 7, color);
                            Record("other-color", kind + "/" + cause + "/" + color, Snapshot(Invoke(Rules, "Query", state, state.CellAt(target), cause, (RabbitColor?)color, Fresh(), 7)));
                        }
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
        }
        private static void GeneratorChecks(bool before)
        {
            LevelDefinition level = Input("generator-retirement", before, item =>
            {
                LevelDefinition fixture = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.Appliance, 3);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture), item); UnityEngine.Object.DestroyImmediate(fixture);
            });
            try
            {
                LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh(); string original = JsonUtility.ToJson(level), global = JsonUtility.ToJson(UnityEngine.Random.state); byte[] packed = LevelPackCodec.Encode(new[] { level }); int draws = state.Random.DrawCount;
                for (int turn = 1; turn <= 9; turn++)
                {
                    context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, turn);
                    object effects = Invoke(typeof(FixedObstacleVerification), "Hit", state, C(4, 7), context);
                    Record("generator-target", turn.ToString(), Snapshot(state) + ";effects=" + Snapshot(effects) + ";context=" + Context(context));
                }
                Check(!state.Cells.Any(cell => cell.ObstacleIndex.HasValue) && state.Obstacles[0].Charge == 0 && GeneratorRules.ActiveConnections(state).Count == 0 && state.Missions[0].Progress == 1, "금속기둥 직접 파괴/발전기 무충전 철거/미션");
                Check(draws == state.Random.DrawCount && global == JsonUtility.ToJson(UnityEngine.Random.state) && original == JsonUtility.ToJson(level) && packed.SequenceEqual(LevelPackCodec.Encode(new[] { level })), "연결 철거 난수/원본/ID/바이트 유지");
                Record("generator-pack", original, Convert.ToBase64String(packed));
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
        private static void PolicyChecks()
        {
            Type policy = typeof(ElementId).Assembly.GetType("Elements.ElementDamageSourcePolicy");
            Check(policy != null, "불변 피해 원인 정책 존재");
            Check(policy.IsSealed && policy.GetProperties().All(property => property.SetMethod == null) && policy.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).All(field => field.IsInitOnly && field.FieldType == typeof(bool)), "정책 불변 bool 값만 보유");
            MethodInfo require = typeof(ElementDefinition).GetMethod("RequireDamageSourcePolicy"), allows = policy.GetMethod("Allows");
            ElementDefinition crate = LegacyElementDefinitions.Get(ObstacleKind.Appliance); object prepared = require.Invoke(crate, null);
            Check(ReferenceEquals(prepared, require.Invoke(LegacyElementDefinitions.Get(ObstacleKind.Appliance), null)), "금속기둥 정책 한 번 준비");
            foreach (DamageCause cause in Enum.GetValues(typeof(DamageCause))) Check((bool)allows.Invoke(prepared, new object[] { cause }) == (cause != DamageCause.MagnetAdjacent), "금속기둥 정책 실제 허용 " + cause);
            for (int mask = 0; mask < 16; mask++)
            {
                bool[] flags = Enumerable.Range(0, 4).Select(bit => (mask & (1 << bit)) != 0).ToArray();
                object profile = Activator.CreateInstance(policy, flags.Cast<object>().ToArray());
                ElementId id = new ElementId("element.policy." + mask);
                ElementDefinition definition = (ElementDefinition)Activator.CreateInstance(typeof(ElementDefinition), new object[] { id, "정책", null, null, profile });
                ElementCatalog catalog = new ElementCatalog(new[] { definition });
                foreach (DamageCause cause in Enum.GetValues(typeof(DamageCause))) Check((bool)allows.Invoke(require.Invoke(catalog.Get(id), null), new object[] { cause }) == flags[(int)cause], "같은 카탈로그 다른 메모리 정책 " + mask + "/" + cause);
            }
            foreach (ElementDefinition missing in new[] { new ElementDefinition(new ElementId("element.missing.2"), "누락"), new ElementDefinition(new ElementId("element.missing.3"), "누락", new ElementPlacementProfile(1, 6)), new ElementDefinition(new ElementId("element.missing.4"), "누락", null, new ElementChargePlacementProfile(2, 3, 5)) })
            {
                Exception error = null; try { require.Invoke(new ElementCatalog(new[] { missing }).Get(missing.Id), null); } catch (TargetInvocationException found) { error = found.InnerException; }
                Check(error is InvalidOperationException && error.Message.Contains(missing.Id.Value), "기존 생성 계약/정책 누락 ID 오류 " + missing.Id);
            }
        }
    }
}

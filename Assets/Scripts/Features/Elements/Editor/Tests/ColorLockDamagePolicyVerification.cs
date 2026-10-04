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
    public static class ColorLockDamagePolicyVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage21";
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
        private static void Query(LevelRuntimeState state, BoardCoordinate target, BoardCoordinate source, DamageCause cause, TurnEffectContext context, string name, DamageResponse? expected = null, RabbitColor? sourceColor = RabbitColor.Type1)
        {
            string initial = Snapshot(state), turn = Context(context), global = JsonUtility.ToJson(UnityEngine.Random.state);
            DamageReaction outer = DamageReaction.Evaluate(state, target, cause, source, context, sourceColor, 7);
            DamageReaction inner = (DamageReaction)Invoke(Rules, "Query", state, state.CellAt(target), cause, sourceColor, context, 7);
            object mission = MissionProgressRules.Query(state, target, context);
            int[] reserved = new[] { 0, 1, 2, 4 }.Select(cells => (int)Invoke(Rules, "ReservedDamage", state.Obstacles[state.CellAt(target).ObstacleIndex.Value], cells)).ToArray();
            Check(initial == Snapshot(state) && turn == Context(context) && global == JsonUtility.ToJson(UnityEngine.Random.state), "상태/비공개 문맥/규칙·전역 난수 무변경 " + name);
            if (expected.HasValue) Check(outer.Response == expected.Value, "외부 경계 실제 응답 " + name);
            Record("query", name + "/" + (int)cause + "/source=" + source + "/target=" + target + "/sourceColor=" + (sourceColor.HasValue ? sourceColor.ToString() : "null") + "/cellColor=" + state.CellAt(source).Color, "state=" + initial + ";context=" + turn + ";outer=" + Snapshot(outer) + ";inner=" + Snapshot(inner) + ";mission=" + Snapshot(mission) + ";reserved=" + string.Join(",", reserved));
        }
        public static void Before() => Execute(true);
        public static void Run() => Execute(false);
        private static void Execute(bool before)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear(); int exit = 0;
            try
            {
                QueryChecks(before); ApplyChecks(before); AdjacentChecks(before); MagnetChecks(before); OtherChecks(before); GeneratorChecks(before);
                Check(LevelPackCodec.FormatVersion == 1 && LevelPackCodec.LevelsPerPack == 50, "팩 버전1/50구간 유지");
                if (before) File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values);
                else { Check(File.ReadAllLines(Evidence + "/baseline-values.jsonl").SequenceEqual(Values), "동일 입력/시드 실제 조회·피해·미션·철거·효과·예약·바이트 전후 일치"); PolicyChecks(); }
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
            for (int durability = 1; durability <= 3; durability++)
                foreach (RabbitColor color in Enum.GetValues(typeof(RabbitColor)))
                {
                    string id = durability + "-" + color;
                    LevelDefinition level = Input("query-" + id, before, item => Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.ColorLock, durability, C(4, 4), color));
                    try
                    {
                        foreach (RabbitColor? sourceColor in new RabbitColor?[] { color, (RabbitColor)(((int)color + 1) % 5), null })
                            foreach (DamageCause cause in new[] { DamageCause.AdjacentMatch, DamageCause.Power, DamageCause.MagnetAdjacent, DamageCause.Hammer, (DamageCause)(-1), (DamageCause)4 })
                                foreach (string mode in new[] { "null-context", "fresh", "same-turn", "next-turn", "removed", "source-null" })
                                {
                                    LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
                                    // 외부 null 대체와 내부 null을 같은 입력에서 구별한다.
                                    Set(state.CellAt(C(3, 4)), "Color", mode == "source-null" ? (RabbitColor?)null : color);
                                    if (mode == "null-context") context = null;
                                    if (mode == "same-turn" || mode == "next-turn")
                                        typeof(TurnEffectContext).GetMethod("RegisterDamage", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(context, new object[] { 0 });
                                    if (mode == "next-turn") context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, 2);
                                    if (mode == "removed") Set(state.Obstacles[0], "Durability", 0);
                                    bool allowed = cause == DamageCause.Power || cause == DamageCause.Hammer || (sourceColor ?? state.CellAt(C(3, 4)).Color) == color;
                                    DamageResponse expected = !allowed || mode == "removed" ? DamageResponse.None : mode == "same-turn" ? DamageResponse.AlreadyDamaged : DamageResponse.Damage;
                                    Query(state, C(4, 4), C(3, 4), cause, context, id + "/" + mode, expected, sourceColor);
                                }
                        foreach (string boundary in new[] { "distance", "wall", "inactive", "protected" })
                        {
                            LevelDefinition wall = null;
                            try
                            {
                                LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
                                if (boundary == "inactive") typeof(RuntimeCell).GetField("<IsActive>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state.CellAt(C(4, 4)), false);
                                if (boundary == "protected") ((HashSet<BoardCoordinate>)typeof(TurnEffectContext).GetField("protectedPowers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context)).Add(C(4, 4));
                                if (boundary == "wall")
                                {
                                    wall = Input("wall-" + id, before, item => { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(level), item); Check(LevelFlowEditing.SetWalls(item, new[] { new BoardEdge(C(3, 4), C(4, 4)) }, false) == null, "색 자물쇠 인접 벽 준비"); });
                                    state = Build(wall);
                                }
                                foreach (RabbitColor? sourceColor in new RabbitColor?[] { color, (RabbitColor)(((int)color + 1) % 5), null })
                                    foreach (DamageCause cause in new[] { DamageCause.AdjacentMatch, DamageCause.Power, DamageCause.MagnetAdjacent, DamageCause.Hammer })
                                        Query(state, C(4, 4), boundary == "distance" ? C(0, 0) : C(3, 4), cause, context, id + "/" + boundary, null, sourceColor);
                            }
                            finally { if (wall != null) UnityEngine.Object.DestroyImmediate(wall); }
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static void ApplyChecks(bool before)
        {
            for (int durability = 1; durability <= 3; durability++)
                foreach (RabbitColor color in Enum.GetValues(typeof(RabbitColor)))
                foreach (bool hammer in new[] { false, true })
                {
                    LevelDefinition level = Input("apply-" + durability + "-" + color + "-" + hammer, before, item =>
                    {
                        Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.ColorLock, durability, C(4, 4), color);
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.ColorLock + ",\"count\":1}]}", item);
                    });
                    try
                    {
                        LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh(); string original = JsonUtility.ToJson(level), global = JsonUtility.ToJson(UnityEngine.Random.state); byte[] packed = LevelPackCodec.Encode(new[] { level }); int draws = state.Random.DrawCount;
                        for (int turn = 1; turn <= durability; turn++)
                        {
                            context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, turn);
                            for (int repeat = 0; repeat < 2; repeat++)
                            {
                                List<EffectRecord> effects = new List<EffectRecord>();
                                if (hammer)
                                { object[] args = { state, C(4, 4), context, effects, null }; Check((bool)Invoke(typeof(PowerEffectResolution), "ApplyHammer", args), "실제 망치 실행 " + durability + "/" + turn + "/" + repeat); }
                                else effects = (List<EffectRecord>)Invoke(typeof(FixedObstacleVerification), "Hit", state, C(4, 4), context);
                                Check(state.Obstacles[0].Durability == durability - turn && state.Missions[0].Progress == (turn == durability ? 1 : 0), "실제 피해/동일턴 반복/제거 미션 " + durability + "/" + hammer + "/" + turn + "/" + repeat);
                                Record("apply", durability + "/hammer=" + hammer + "/turn=" + turn + "/repeat=" + repeat, Snapshot(state) + ";effects=" + Snapshot(effects) + ";context=" + Context(context));
                            }
                        }
                        Check(state.CellAt(C(4, 4)).Content == RuntimeContent.Empty && !state.CellAt(C(4, 4)).ObstacleIndex.HasValue && state.Random.DrawCount == draws && JsonUtility.ToJson(UnityEngine.Random.state) == global && JsonUtility.ToJson(level) == original && packed.SequenceEqual(LevelPackCodec.Encode(new[] { level })), "실제 제거/난수/원본 ID/바이트 유지 " + durability + hammer);
                        Record("pack", original, Convert.ToBase64String(packed));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static void AdjacentChecks(bool before)
        {
            for (int durability = 1; durability <= 3; durability++)
                foreach (RabbitColor color in Enum.GetValues(typeof(RabbitColor)))
                    foreach (bool matching in new[] { false, true })
                        foreach (bool wall in new[] { false, true })
                        {
                            RabbitColor selected = matching ? color : (RabbitColor)(((int)color + 1) % 5);
                            LevelDefinition level = Input("adjacent-" + durability + color + matching + wall, before, item =>
                            {
                                foreach (BoardCoordinate cell in new[] { C(3, 2), C(3, 4), C(2, 3) }) Invoke(typeof(PowerEffectVerification), "Place", item, cell, InitialBlockKind.FixedNormal, RocketDirection.Horizontal, selected);
                                Invoke(typeof(PowerEffectVerification), "Place", item, C(3, 3), InitialBlockKind.FixedNormal, RocketDirection.Horizontal, (RabbitColor)(((int)selected + 1) % 5));
                                Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.ColorLock, durability, C(4, 3), color);
                                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.ColorLock + ",\"count\":1}]}", item);
                                if (wall) Check(LevelFlowEditing.SetWalls(item, new[] { new BoardEdge(C(3, 3), C(4, 3)) }, false) == null, "실제 매칭 인접 벽 준비");
                            });
                            try
                            {
                                BoardActionExecutor executor = new BoardActionExecutor(Build(level)); BoardActionResult result = executor.Swap(C(2, 3), C(3, 3));
                                int remaining = matching && !wall ? durability - 1 : durability;
                                Check(result.IsApplied && executor.State.Obstacles[0].Durability == remaining && executor.State.Missions[0].Progress == (remaining == 0 ? 1 : 0), "실제 일반 매칭/색/벽/미션 " + durability + color + matching + wall);
                                Record("adjacent", JsonUtility.ToJson(level), Snapshot(executor.State) + ";result=" + Snapshot(result) + ";context=" + Context(executor.TurnEffects));
                            }
                            finally { UnityEngine.Object.DestroyImmediate(level); }
                        }
        }
        private static void MagnetChecks(bool before)
        {
            for (int durability = 1; durability <= 3; durability++)
                foreach (RabbitColor color in Enum.GetValues(typeof(RabbitColor)))
                    foreach (bool matching in new[] { false, true })
                        foreach (bool wall in new[] { false, true })
                        {
                            RabbitColor selected = matching ? color : (RabbitColor)(((int)color + 1) % 5);
                            LevelDefinition level = Input("magnet-" + durability + color + matching + wall, before, item =>
                            {
                                Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.ColorLock, durability, C(4, 4), color);
                                Invoke(typeof(PowerEffectVerification), "Place", item, C(4, 3), InitialBlockKind.FixedNormal, RocketDirection.Horizontal, selected);
                                Invoke(typeof(PowerEffectVerification), "Place", item, C(0, 0), InitialBlockKind.Magnet, RocketDirection.Horizontal, selected);
                                Invoke(typeof(PowerEffectVerification), "Place", item, C(0, 1), InitialBlockKind.FixedNormal, RocketDirection.Horizontal, selected);
                                // 벽 반대편의 다른 일반 블록까지 같은 색으로 소모되지 않도록 선택 노출을 한정한다.
                                BoardCoordinate[] hidden = item.InitialBlocks.Where(block => block.Kind == InitialBlockKind.FixedNormal && !block.Coordinate.Equals(C(0, 1)) && !block.Coordinate.Equals(C(4, 3))).Select(block => block.Coordinate).ToArray();
                                LevelObstacleEditing.Apply(item, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Mold, Durability = 1 }, hidden);
                                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.ColorLock + ",\"count\":1}]}", item);
                                if (wall) Check(LevelFlowEditing.SetWalls(item, new[] { new BoardEdge(C(4, 3), C(4, 4)) }, false) == null, "실제 자석 인접 벽 준비");
                            });
                            try
                            {
                                BoardActionExecutor executor = new BoardActionExecutor(Build(level)); BoardActionResult result = executor.Swap(C(0, 0), C(0, 1));
                                int remaining = matching && !wall ? durability - 1 : durability;
                                Check(result.IsApplied && executor.State.Obstacles[0].Durability == remaining && executor.State.Missions[0].Progress == (remaining == 0 ? 1 : 0), "실제 자석 교환/인접/색/벽/미션 " + durability + color + matching + wall + "/result=" + Snapshot(result) + "/durability=" + executor.State.Obstacles[0].Durability);
                                Record("magnet", JsonUtility.ToJson(level), Snapshot(executor.State) + ";result=" + Snapshot(result) + ";context=" + Context(executor.TurnEffects));
                            }
                            finally { UnityEngine.Object.DestroyImmediate(level); }
                        }
        }
        private static void OtherChecks(bool before)
        {
            foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.Appliance, ObstacleKind.Generator, (ObstacleKind)(-1), (ObstacleKind)6 })
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
                            Query(state, target, C(target.Row - 1, target.Column), cause, Fresh(), "other" + (int)kind + "/" + (int)cause + "/" + color, null, color);
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
                LevelDefinition fixture = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.ColorLock, 3);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture), item); UnityEngine.Object.DestroyImmediate(fixture);
            });
            try
            {
                LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh(); string original = JsonUtility.ToJson(level), global = JsonUtility.ToJson(UnityEngine.Random.state); byte[] packed = LevelPackCodec.Encode(new[] { level }); int draws = state.Random.DrawCount;
                for (int turn = 1; turn <= 3; turn++)
                {
                    context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, turn);
                    object effects = Invoke(typeof(FixedObstacleVerification), "Hit", state, C(4, 7), context);
                    Record("generator-target", turn.ToString(), Snapshot(state) + ";effects=" + Snapshot(effects) + ";context=" + Context(context));
                }
                Check(!state.Cells.Any(cell => cell.ObstacleIndex.HasValue) && state.Obstacles[0].Charge == 0 && GeneratorRules.ActiveConnections(state).Count == 0 && state.Missions[0].Progress == 1, "색 자물쇠 직접 파괴/발전기 무충전 철거/미션");
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
            ElementDefinition crate = LegacyElementDefinitions.Get(ObstacleKind.ColorLock); object prepared = require.Invoke(crate, null);
            Check(ReferenceEquals(prepared, require.Invoke(LegacyElementDefinitions.Get(ObstacleKind.ColorLock), null)), "색 자물쇠 정책 한 번 준비");
            foreach (DamageCause cause in Enum.GetValues(typeof(DamageCause))) Check((bool)allows.Invoke(prepared, new object[] { cause }) == true, "색 자물쇠 정책 실제 허용 " + cause);
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

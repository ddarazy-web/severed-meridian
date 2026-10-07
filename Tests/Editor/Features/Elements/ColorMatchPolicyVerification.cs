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
    public static class ColorMatchPolicyVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage24";
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
            string ruleBefore = string.Join("|", Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>().Select(kind => Snapshot(LegacyElementDefinitions.Get(kind))));
            DamageReaction outer = DamageReaction.Evaluate(state, target, cause, source, context, sourceColor, 7);
            DamageReaction inner = (DamageReaction)Invoke(Rules, "Query", state, state.CellAt(target), cause, sourceColor, context, 7);
            object mission = MissionProgressRules.Query(state, target, context);
            int[] reserved = new[] { 0, 1, 2, 4 }.Select(cells => (int)Invoke(Rules, "ReservedDamage", state.Obstacles[state.CellAt(target).ObstacleIndex.Value], cells)).ToArray();
            Check(initial == Snapshot(state) && turn == Context(context) && global == JsonUtility.ToJson(UnityEngine.Random.state) && ruleBefore == string.Join("|", Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>().Select(kind => Snapshot(LegacyElementDefinitions.Get(kind)))), "상태/비공개 문맥/규칙·전역 난수 무변경 " + name);
            if (state.Obstacles[state.CellAt(target).ObstacleIndex.Value].Definition.Kind == ObstacleKind.ColorLock)
            {
                RuntimeObstacle body = state.Obstacles[state.CellAt(target).ObstacleIndex.Value];
                bool match = cause == DamageCause.Power || cause == DamageCause.Hammer || sourceColor == body.Definition.Color;
                DamageResponse response = !match || body.Durability <= 0 ? DamageResponse.None : context?.HasDamaged(state.CellAt(target).ObstacleIndex.Value) == true ? DamageResponse.AlreadyDamaged : DamageResponse.Damage;
                string message = !match ? "자물쇠 지정 색 불일치" : body.Durability <= 0 ? "제거된 본체" : response == DamageResponse.AlreadyDamaged ? "본체별 턴당 최대 1 피해" : "본체 내구도 감소";
                Check(inner.Response == response && inner.Amount == (response == DamageResponse.Damage ? 1 : 0) && inner.Message == message, "내부 색/null/원인/내구도/문맥 반응·양·메시지 " + name);
            }
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
                QueryChecks(before); ApplyChecks(before); AdjacentChecks(before); MagnetChecks(before); OtherChecks(before); GeneratorChecks(before); ReservationChecks(before); DeletedChecks(before); PackChecks(before);
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
            for (int durability = 1; durability <= 3; durability++)
                foreach (RabbitColor color in Enum.GetValues(typeof(RabbitColor)))
                {
                    string id = durability + "-" + color;
                    LevelDefinition level = Input("query-" + id, before, item => Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.ColorLock, durability, C(4, 4), color));
                    try
                    {
                        foreach (RabbitColor? sourceColor in Enum.GetValues(typeof(RabbitColor)).Cast<RabbitColor?>().Concat(new RabbitColor?[] { null }))
                            foreach (DamageCause cause in new[] { DamageCause.AdjacentMatch, DamageCause.Power, DamageCause.MagnetAdjacent, DamageCause.Hammer, (DamageCause)(-1), (DamageCause)4 })
                                foreach (string mode in new[] { "null-context", "fresh", "same-turn", "next-turn", "removed", "removed-same-turn", "source-null", "source-other" })
                                {
                                    LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
                                    // 외부 null 대체와 내부 null을 같은 입력에서 구별한다.
                                    Set(state.CellAt(C(3, 4)), "Color", mode == "source-null" ? (RabbitColor?)null : mode == "source-other" ? (RabbitColor)(((int)color + 1) % 5) : color);
                                    if (mode == "null-context") context = null;
                                    if (mode == "same-turn" || mode == "next-turn" || mode == "removed-same-turn")
                                        typeof(TurnEffectContext).GetMethod("RegisterDamage", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(context, new object[] { 0 });
                                    if (mode == "next-turn") context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, 2);
                                    if (mode == "removed" || mode == "removed-same-turn") Set(state.Obstacles[0], "Durability", 0);
                                    bool allowed = cause == DamageCause.Power || cause == DamageCause.Hammer || (sourceColor ?? state.CellAt(C(3, 4)).Color) == color;
                                    DamageResponse expected = !allowed || mode == "removed" || mode == "removed-same-turn" ? DamageResponse.None : mode == "same-turn" ? DamageResponse.AlreadyDamaged : DamageResponse.Damage;
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
                                foreach (RabbitColor? sourceColor in Enum.GetValues(typeof(RabbitColor)).Cast<RabbitColor?>().Concat(new RabbitColor?[] { null }))
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
        private static void ReservationChecks(bool before)
        {
            for (int durability = 1; durability <= 3; durability++)
                foreach (RabbitColor color in Enum.GetValues(typeof(RabbitColor)))
                {
                    LevelDefinition level = Input("reservation-" + durability + "-" + color, before, item =>
                    {
                        Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.ColorLock, durability, C(4, 4), color);
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.ColorLock + ",\"count\":1}]}", item);
                    });
                    try
                    {
                        LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh(); string initial = Snapshot(state), turn = Context(context), global = JsonUtility.ToJson(UnityEngine.Random.state);
                        foreach (int count in new[] { 0, 1, 2, 4 })
                        {
                            DroneImpact[] impacts = Enumerable.Range(0, count).Select(index => (DroneImpact)Activator.CreateInstance(typeof(DroneImpact), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { state.CellAt(C(4, 4)), MissionProgressRules.Query(state, C(4, 4), context) }, null)).ToArray();
                            object projection = Invoke(typeof(MissionProgressRules), "Project", state, impacts.Concat(impacts).ToArray());
                            Check((int)projection.GetType().GetField("Item2").GetValue(projection) == (count == 0 ? 0 : 1) && (int)projection.GetType().GetField("Item3").GetValue(projection) == 0, "실제 중복 예약 본체1피해/충전0 " + durability + color + count);
                            Check(initial == Snapshot(state) && turn == Context(context) && global == JsonUtility.ToJson(UnityEngine.Random.state), "예약투영 상태/문맥/난수 무변경 " + durability + color + count);
                            Record("reservation-projection", durability + "/" + color + "/duplicates=" + count, Deep(projection) + ";state=" + initial + ";context=" + turn);
                        }
                        DroneTargetManager manager = (DroneTargetManager)Invoke(typeof(GeneratorVerification), "Manager", state, context);
                        Dictionary<int, DroneTarget> reservations = (Dictionary<int, DroneTarget>)typeof(DroneTargetManager).GetField("reservations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                        DroneTarget target = manager.Query().First(t => t.ObstacleIndex == 0); reservations.Add(1, target);
                        Check(manager.ExpectedDamage == 1 && manager.ExpectedCharge == 0 && manager.ExpectedComplete == (durability == 1 ? 1 : 0) && manager.Query().All(t => t.ObstacleIndex != 0), "실제 예약/미션/점유 후보 제외 " + durability + color);
                        Check(initial == Snapshot(state) && turn == Context(context) && global == JsonUtility.ToJson(UnityEngine.Random.state), "실제 예약 조회 상태/문맥/난수 무변경 " + durability + color);
                        Record("reservation-manager", durability + "/" + color, "damage=" + manager.ExpectedDamage + ";charge=" + manager.ExpectedCharge + ";complete=" + manager.ExpectedComplete + ";reservations=" + Deep(reservations));
                        reservations.Clear(); Check(manager.ExpectedDamage == 0 && manager.ExpectedComplete == 0 && manager.Query().Any(t => t.ObstacleIndex == 0), "예약취소 복원 " + durability + color);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static void DeletedChecks(bool before)
        {
            for (int durability = 1; durability <= 3; durability++)
                foreach (RabbitColor color in Enum.GetValues(typeof(RabbitColor)))
                {
                    LevelDefinition level = Input("delete-" + durability + "-" + color, before, item => Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.ColorLock, durability, C(4, 4), color));
                    try
                    {
                        foreach (RabbitColor? sourceColor in Enum.GetValues(typeof(RabbitColor)).Cast<RabbitColor?>().Concat(new RabbitColor?[] { null }))
                            foreach (DamageCause cause in new[] { DamageCause.AdjacentMatch, DamageCause.Power, DamageCause.MagnetAdjacent, DamageCause.Hammer, (DamageCause)(-1), (DamageCause)4 })
                                foreach (bool damaged in new[] { false, true })
                                {
                                    LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh(); Set(state.CellAt(C(3, 4)), "Color", color);
                                    if (damaged) typeof(TurnEffectContext).GetMethod("RegisterDamage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(context, new object[] { 0 });
                                    Invoke(Rules, "Remove", state, 0); string initial = Snapshot(state), turn = Context(context), global = JsonUtility.ToJson(UnityEngine.Random.state);
                                    DamageReaction outer = DamageReaction.Evaluate(state, C(4, 4), cause, C(3, 4), context, sourceColor);
                                    Check(outer.Response == DamageResponse.None && state.CellAt(C(4, 4)).Content == RuntimeContent.Empty && initial == Snapshot(state) && turn == Context(context) && global == JsonUtility.ToJson(UnityEngine.Random.state), "실제 삭제 후 외부Empty/조회 무변경 " + durability + color + sourceColor + cause + damaged);
                                    Record("deleted-outer", durability + "/" + color + "/sourceColor=" + sourceColor + "/cause=" + (int)cause + "/damaged=" + damaged, Snapshot(outer) + ";state=" + initial + ";context=" + turn);
                                    // 실제 빈칸과 내부 조회의 점유 전제를 구분하는 메모리 전용 경계다.
                                    Set(state.CellAt(C(4, 4)), "Content", RuntimeContent.Obstacle); Set(state.CellAt(C(4, 4)), "ObstacleIndex", (int?)0);
                                    Query(state, C(4, 4), C(3, 4), cause, context, durability + "-" + color + "/retained/damaged=" + damaged, DamageResponse.None, sourceColor);
                                }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static void PackChecks(bool before)
        {
            foreach (int number in new[] { 1, 50, 51, 100, 101 })
            {
                LevelDefinition level = Input("pack-boundary-" + number, before, item =>
                {
                    Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.ColorLock, 2, C(4, 4), RabbitColor.Type2);
                    JsonUtility.FromJsonOverwrite("{\"levelNumber\":" + number + ",\"missions\":[{\"kind\":" + (int)MissionKind.ColorLock + ",\"count\":1}]}", item);
                });
                LevelDefinition copy = null;
                try
                {
                    byte[] bytes = LevelPackCodec.Encode(new[] { level }); copy = LevelPackCodec.ReadLevel(bytes, number);
                    int first = number <= 50 ? 1 : number <= 100 ? 51 : 101;
                    Check(LevelPackCodec.Decode(bytes).FormatVersion == 1 && LevelPackCodec.Decode(bytes).FirstLevel == first && LevelPackCodec.FirstLevel(number) == first && LevelPackCodec.Address(number) == "Levels/levels-" + first.ToString("D6"), "실제 버전1/50구간 경계 " + number);
                    Check(JsonUtility.ToJson(level) == JsonUtility.ToJson(copy) && bytes.SequenceEqual(LevelPackCodec.Encode(new[] { copy })), "실제 전체 필드/본체ID/바이트 왕복 " + number);
                    Record("boundary-pack", JsonUtility.ToJson(level), Convert.ToBase64String(bytes));
                    LevelRuntimeState original = Build(level), decoded = Build(copy); TurnEffectContext a = Fresh(), b = Fresh();
                    object effectsA = Invoke(typeof(FixedObstacleVerification), "Hit", original, C(4, 4), a), effectsB = Invoke(typeof(FixedObstacleVerification), "Hit", decoded, C(4, 4), b);
                    Check(Snapshot(original) == Snapshot(decoded) && Context(a) == Context(b) && Snapshot(effectsA) == Snapshot(effectsB), "원본/MemoryPack 같은시드 피해 동일 " + number);
                    Record("boundary-pack-state", number.ToString(), Snapshot(decoded) + ";effects=" + Snapshot(effectsB) + ";context=" + Context(b));
                }
                finally { if (copy != null) UnityEngine.Object.DestroyImmediate(copy); UnityEngine.Object.DestroyImmediate(level); }
            }
        }
        private static void PolicyChecks()
        {
            Type policy = typeof(ElementId).Assembly.GetType("Elements.ElementColorMatchPolicy");
            Check(policy != null, "불변 색 일치 정책 존재");
            Check(policy.IsSealed && policy.GetProperties().Select(p => p.Name).SequenceEqual(new[] { "RequiresMatchingColor" }) && policy.GetProperties().All(p => p.SetMethod == null) && policy.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).All(f => f.IsInitOnly && f.FieldType == typeof(bool)), "색 정책 불변 bool만 보유");
            MethodInfo require = typeof(ElementDefinition).GetMethod("RequireColorMatchPolicy"), allows = policy.GetMethod("Allows");
            Check(require != null && allows != null && typeof(ElementDefinition).GetProperty("ColorMatchPolicy").SetMethod == null, "필수 조회/읽기전용 색 정책 계약");
            ElementDefinition definition = LegacyElementDefinitions.Get(ObstacleKind.ColorLock); object prepared = require.Invoke(definition, null);
            Check(ReferenceEquals(prepared, require.Invoke(LegacyElementDefinitions.Get(ObstacleKind.ColorLock), null)) && (bool)policy.GetProperty("RequiresMatchingColor").GetValue(prepared), "색 자물쇠 true 정책 한 번 등록");
            foreach (bool matching in new[] { false, true })
            {
                object profile = Activator.CreateInstance(policy, new object[] { matching }); ElementId id = new ElementId("element.color." + matching);
                ElementDefinition entry = (ElementDefinition)Activator.CreateInstance(typeof(ElementDefinition), new object[] { id, "색 조건", null, null, null, profile }); ElementCatalog catalog = new ElementCatalog(new[] { entry });
                foreach (RabbitColor target in Enum.GetValues(typeof(RabbitColor)))
                    foreach (RabbitColor? source in Enum.GetValues(typeof(RabbitColor)).Cast<RabbitColor?>().Concat(new RabbitColor?[] { null }))
                    {
                        bool actual = (bool)allows.Invoke(require.Invoke(catalog.Get(id), null), new object[] { source, target });
                        Check(actual == (!matching || source == target), "같은 카탈로그 true/false 색 일치 조회 " + matching + "/" + source + "/" + target);
                        if (matching) Check(actual == (bool)allows.Invoke(prepared, new object[] { source, target }), "등록된 색 자물쇠 true 조회 " + source + "/" + target);
                    }
            }
            foreach (ElementDefinition missing in new[] {
                new ElementDefinition(new ElementId("element.missing.2"), "누락"),
                new ElementDefinition(new ElementId("element.missing.3"), "누락", new ElementPlacementProfile(1, 3)),
                new ElementDefinition(new ElementId("element.missing.4"), "누락", null, new ElementChargePlacementProfile(2, 3, 5)),
                new ElementDefinition(new ElementId("element.missing.5"), "누락", new ElementPlacementProfile(1, 3), null, new ElementDamageSourcePolicy(true, true, true, true)) })
            {
                Exception error = null; try { require.Invoke(new ElementCatalog(new[] { missing }).Get(missing.Id), null); } catch (TargetInvocationException found) { error = found.InnerException; }
                Check(error is InvalidOperationException && error.Message.Contains(missing.Id.Value), "기존2/3/4/5 생성 계약/누락 ID 오류 " + missing.Id);
            }
            foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.Appliance, ObstacleKind.Generator })
                Check(typeof(ElementDefinition).GetProperty("ColorMatchPolicy").GetValue(LegacyElementDefinitions.Get(kind)) == null, "다른5종 색 프로필 강제 없음 " + kind);
        }
    }
}

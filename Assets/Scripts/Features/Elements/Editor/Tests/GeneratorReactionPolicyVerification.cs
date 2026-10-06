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
    public static class GeneratorReactionPolicyVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage23";
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
        private static void Query(LevelRuntimeState state, BoardCoordinate target, BoardCoordinate source, DamageCause cause, TurnEffectContext context, string name, RabbitColor? color = RabbitColor.Type1, DamageResponse? expected = null)
        {
            string initial = Snapshot(state), turn = Context(context), global = JsonUtility.ToJson(UnityEngine.Random.state);
            string rules = string.Join("|", Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>().Select(kind => Snapshot(LegacyElementDefinitions.Get(kind))));
            DamageReaction outer = DamageReaction.Evaluate(state, target, cause, source, context, color, 7);
            DamageReaction inner = (DamageReaction)Invoke(Rules, "Query", state, state.CellAt(target), cause, color, context, 7);
            object mission = MissionProgressRules.Query(state, target, context);
            Check(initial == Snapshot(state) && turn == Context(context) && global == JsonUtility.ToJson(UnityEngine.Random.state) && rules == string.Join("|", Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>().Select(kind => Snapshot(LegacyElementDefinitions.Get(kind)))), "상태/비공개 문맥/규칙·전역 난수 무변경 " + name);
            if (state.Obstacles[state.CellAt(target).ObstacleIndex.Value].Definition.Kind == ObstacleKind.Generator)
            {
                bool charged = context?.HasCharged(state.CellAt(target).ObstacleIndex.Value) == true;
                Check(inner.Response == (charged ? DamageResponse.AlreadyDamaged : DamageResponse.Charge) && inner.Amount == (charged ? 0 : 1) && inner.Message == (charged ? "발전기 본체별 수당 최대 1 충전" : "발전기 충전"), "내부 원인/문맥/0내구도 반응·양·메시지 " + name);
            }
            if (expected.HasValue) Check(outer.Response == expected.Value, "외부 경계 실제 응답 " + name);
            Record("query", name + "/cause=" + (int)cause + "/source=" + source + "/target=" + target + "/color=" + color, "state=" + initial + ";context=" + turn + ";outer=" + Snapshot(outer) + ";inner=" + Snapshot(inner) + ";mission=" + Snapshot(mission));
        }
        public static void Before() => Execute(true);
        public static void Run() => Execute(false);
        private static void Execute(bool before)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear(); int exit = 0;
            try
            {
                QueryChecks(before); ApplyChecks(before); AdjacentChecks(before); ConnectionChecks(before); ReservationChecks(before); OtherChecks(before); PackChecks(before);
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
            for (int required = 3; required <= 5; required++)
            {
                LevelDefinition level = Input("query-" + required, before, item =>
                {
                    LevelDefinition fixture = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.Appliance, required);
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture), item); UnityEngine.Object.DestroyImmediate(fixture);
                });
                try
                {
                    for (int charge = 0; charge < required; charge++)
                        for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                        {
                            BoardCoordinate target = cells[cellIndex], source = C(target.Row == 4 ? 3 : 6, target.Column);
                            foreach (DamageCause cause in new[] { DamageCause.AdjacentMatch, DamageCause.Power, DamageCause.MagnetAdjacent, DamageCause.Hammer, (DamageCause)(-1), (DamageCause)4 })
                                foreach (string mode in new[] { "fresh", "null", "same-turn", "next-turn" })
                                    foreach (RabbitColor? color in new RabbitColor?[] { RabbitColor.Type1, RabbitColor.Type2, null })
                                    foreach (int bodyDurability in new[] { 0, 1 })
                                    {
                                        LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh(); Set(state.Obstacles[0], "Charge", charge);
                                        Set(state.Obstacles[0], "Durability", bodyDurability);
                                        if (mode == "null") context = null;
                                        if (mode == "same-turn" || mode == "next-turn") typeof(TurnEffectContext).GetMethod("RegisterCharge", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(context, new object[] { 0 });
                                        if (mode == "next-turn") context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, 2);
                                        Query(state, target, source, cause, context, "r" + required + "/q" + charge + "/cell" + cellIndex + "/" + mode + "/dur" + bodyDurability, color,
                                            cause == DamageCause.MagnetAdjacent ? DamageResponse.None : mode == "same-turn" ? DamageResponse.AlreadyDamaged : DamageResponse.Charge);
                                    }
                            foreach (string boundary in new[] { "distance", "wall", "inactive", "protected" })
                            {
                                LevelDefinition wall = null;
                                try
                                {
                                    LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh(); Set(state.Obstacles[0], "Charge", charge);
                                    if (boundary == "inactive") typeof(RuntimeCell).GetField("<IsActive>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state.CellAt(target), false);
                                    if (boundary == "protected") ((HashSet<BoardCoordinate>)typeof(TurnEffectContext).GetField("protectedPowers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context)).Add(target);
                                    if (boundary == "wall")
                                    {
                                        wall = Input("wall-" + required + "-" + charge + "-" + cellIndex, before, item => { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(level), item); Check(LevelFlowEditing.SetWalls(item, new[] { new BoardEdge(source, target) }, false) == null, "인접 벽 메모리 준비"); });
                                        state = Build(wall); Set(state.Obstacles[0], "Charge", charge);
                                    }
                                    foreach (DamageCause cause in Enum.GetValues(typeof(DamageCause)))
                                    {
                                        DamageResponse expected = boundary == "inactive" ? DamageResponse.None :
                                            boundary == "protected" ? (cause == DamageCause.MagnetAdjacent ? DamageResponse.None : DamageResponse.Protected) :
                                            cause == DamageCause.AdjacentMatch || cause == DamageCause.MagnetAdjacent ? (boundary == "wall" ? DamageResponse.Wall : DamageResponse.None) : DamageResponse.Charge;
                                        Query(state, target, boundary == "distance" ? C(0, 0) : source, cause, context, "r" + required + "/q" + charge + "/cell" + cellIndex + "/" + boundary, null, expected);
                                    }
                                }
                                finally { if (wall != null) UnityEngine.Object.DestroyImmediate(wall); }
                            }
                        }
                    foreach (bool charged in new[] { false, true })
                        foreach (BoardCoordinate target in cells)
                            foreach (DamageCause cause in new[] { DamageCause.AdjacentMatch, DamageCause.Power, DamageCause.MagnetAdjacent, DamageCause.Hammer, (DamageCause)(-1), (DamageCause)4 })
                            {
                                LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
                                if (charged) typeof(TurnEffectContext).GetMethod("RegisterCharge", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(context, new object[] { 0 });
                                Invoke(Rules, "Remove", state, 0);
                                DamageReaction empty = DamageReaction.Evaluate(state, target, cause, C(target.Row == 4 ? 3 : 6, target.Column), context);
                                Check(empty.Response == DamageResponse.None && state.CellAt(target).Content == RuntimeContent.Empty, "실제 삭제 후 외부Empty " + required + charged + target + cause);
                                Record("deleted-outer", required + "/charged=" + charged + "/" + target + "/" + cause, Snapshot(empty) + ";state=" + Snapshot(state) + ";context=" + Context(context));
                                // 삭제 상태와 내부 조회의 점유 전제를 구분하는 메모리 전용 경계다.
                                Set(state.CellAt(target), "Content", RuntimeContent.Obstacle); Set(state.CellAt(target), "ObstacleIndex", (int?)0);
                                Query(state, target, C(target.Row == 4 ? 3 : 6, target.Column), cause, context, "r" + required + "/retained-occupancy/charged=" + charged);
                            }
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
        }
        private static void ApplyChecks(bool before)
        {
            foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance })
                for (int required = 3; required <= 5; required++)
                    foreach (bool hammer in new[] { false, true })
                    {
                        LevelDefinition level = Input("apply-" + kind + "-" + required + "-" + hammer, before, item =>
                        {
                            LevelDefinition fixture = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", kind, required);
                            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture), item); UnityEngine.Object.DestroyImmediate(fixture);
                        });
                        try
                        {
                            LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh(); string original = JsonUtility.ToJson(level), global = JsonUtility.ToJson(UnityEngine.Random.state); byte[] packed = LevelPackCodec.Encode(new[] { level }); int draws = state.Random.DrawCount;
                            for (int turn = 1; turn <= required; turn++)
                            {
                                context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, turn);
                                foreach (BoardCoordinate cell in LevelPlacementRules.Footprint(C(4, 4), 2))
                                {
                                    List<EffectRecord> effects = new List<EffectRecord>();
                                    if (hammer) { object[] args = { state, cell, context, effects, null }; Check((bool)Invoke(typeof(PowerEffectResolution), "ApplyHammer", args), "실제 망치 실행 " + kind + required + turn + cell); }
                                    else effects = (List<EffectRecord>)Invoke(typeof(FixedObstacleVerification), "Hit", state, cell, context);
                                    Check(state.Obstacles[0].Charge == turn && state.Missions[0].Progress == (turn == required ? 1 : 0) && context.Generators.Count(g => g.Event == GeneratorEvent.Charged) == 1, "실제4칸 본체 턴당1충전/미션 " + kind + required + hammer + turn + cell);
                                    Record("apply", kind + "/required=" + required + "/hammer=" + hammer + "/turn=" + turn + "/cell=" + cell, Snapshot(state) + ";effects=" + Snapshot(effects) + ";context=" + Context(context));
                                }
                            }
                            Check(!state.Cells.Any(cell => cell.ObstacleIndex.HasValue) && GeneratorRules.ActiveConnections(state).Count == 0 && context.Generators.Count(g => g.Event == GeneratorEvent.Activated) == 1 && context.Generators.All(g => g.Event != GeneratorEvent.Retired), "완충 작동/전체제거/연결해제 " + kind + required + hammer);
                            Check(draws == state.Random.DrawCount && global == JsonUtility.ToJson(UnityEngine.Random.state) && original == JsonUtility.ToJson(level) && packed.SequenceEqual(LevelPackCodec.Encode(new[] { level })), "실제 충전 원본/ID/바이트/난수 보존 " + kind + required + hammer);
                            Record("pack", original, Convert.ToBase64String(packed));
                            state = Build(level); context = Fresh();
                            for (int hit = 1; hit <= LevelPlacementRules.MaxDurability(kind); hit++)
                            {
                                context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, hit);
                                object effects = Invoke(typeof(FixedObstacleVerification), "Hit", state, C(4, 7), context);
                                Record("direct-target", kind + "/r" + required + "/" + hammer + "/hit" + hit, Snapshot(state) + ";effects=" + Snapshot(effects) + ";context=" + Context(context));
                            }
                            Check(!state.Cells.Any(cell => cell.ObstacleIndex.HasValue) && state.Obstacles[0].Charge == 0 && state.Missions[0].Progress == 1 && context.Generators.Count(g => g.Event == GeneratorEvent.Retired) == 1, "대상 직접제거/무충전 철거 " + kind + required + hammer);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(level); }
                    }
        }
        private static void AdjacentChecks(bool before)
        {
            for (int required = 3; required <= 5; required++)
                foreach (bool wall in new[] { false, true })
                {
                    LevelDefinition level = Input("adjacent-" + required + "-" + wall, before, item =>
                    {
                        LevelDefinition fixture = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.Appliance, required);
                        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture), item); UnityEngine.Object.DestroyImmediate(fixture);
                        foreach (BoardCoordinate cell in new[] { C(3, 3), C(3, 4), C(3, 5) }) Invoke(typeof(PowerEffectVerification), "Place", item, cell, InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type1);
                        if (wall) Check(LevelFlowEditing.SetWalls(item, new[] { new BoardEdge(C(3, 4), C(4, 4)), new BoardEdge(C(3, 5), C(4, 5)) }, false) == null, "일반 인접 전체 접촉 벽 준비");
                    });
                    try
                    {
                        LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
                        object matchEffects = Invoke(typeof(FixedObstacleVerification), "Matches", state, context, new[] { C(3, 3), C(3, 4), C(3, 5) }, C(3, 4));
                        Check(state.Obstacles[0].Charge == (wall ? 0 : 1) && state.Missions[0].Progress == 0, "실제 일반 인접 매칭/여러 접촉1충전/벽 " + required + wall);
                        Record("adjacent", JsonUtility.ToJson(level), Snapshot(state) + ";effects=" + Snapshot(matchEffects) + ";context=" + Context(context));
                        object effects = Invoke(typeof(FixedObstacleVerification), "Hit", state, C(5, 5), context);
                        Check(state.Obstacles[0].Charge == 1, "매칭 후 파워 같은턴1충전/벽 관통 " + required + wall);
                        Record("adjacent-power", required + "/" + wall, Snapshot(state) + ";effects=" + Snapshot(effects) + ";context=" + Context(context));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static void ConnectionChecks(bool before)
        {
            foreach (string mode in new[] { "multiple", "twice", "wire-wall" })
            {
                LevelDefinition level = Input("connections-" + mode, before, item =>
                {
                    LevelDefinition fixture = (LevelDefinition)Invoke(typeof(GeneratorVerification), mode == "multiple" ? "Multiple" : "Make", mode == "multiple" ? Array.Empty<object>() : new object[] { ObstacleKind.Appliance, 3 });
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture), item); UnityEngine.Object.DestroyImmediate(fixture);
                    if (mode == "twice")
                    {
                        foreach (BoardCoordinate origin in new[] { C(0, 0), C(0, 3) }) LevelObstacleEditing.Apply(item, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, LevelPlacementRules.Footprint(origin, 2));
                        LevelObstacleEditing.Apply(item, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Generator, RequiredCharge = 5 }, new[] { C(0, 0) });
                        LevelObstacleEditing.Apply(item, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 1 }, new[] { C(0, 3) });
                        Check(LevelConnectionEditing.Add(item, item.Obstacles[2].Id, item.Obstacles[3].Id) == null && LevelConnectionEditing.SetWire(item, 1, new[] { C(0, 2), C(0, 3) }) == null, "기존 복수 발전기 메모리 사례 준비");
                    }
                    if (mode == "wire-wall") Check(LevelFlowEditing.SetWalls(item, new[] { new BoardEdge(C(4, 3), C(4, 4)), new BoardEdge(C(4, 6), C(4, 7)) }, false) == null, "와이어 경로 벽 메모리 준비");
                });
                try
                {
                    LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh(); byte[] pack = LevelPackCodec.Encode(new[] { level }); string original = JsonUtility.ToJson(level);
                    if (mode == "multiple")
                    {
                        object effects = Invoke(typeof(FixedObstacleVerification), "Hit", state, C(1, 4), context);
                        Check(GeneratorRules.ActiveConnections(state).Count == 2 && state.Obstacles[0].Charge == 0, "여러 연결 일부제거/무충전 유지");
                        Record("multiple-direct", original, Snapshot(state) + ";effects=" + Snapshot(effects) + ";context=" + Context(context));
                    }
                    if (mode == "twice")
                    {
                        Invoke(typeof(FixedObstacleVerification), "Hit", state, C(4, 4), context); Invoke(typeof(FixedObstacleVerification), "Hit", state, C(0, 0), context); Invoke(typeof(FixedObstacleVerification), "Hit", state, C(1, 1), context);
                        Check(state.Obstacles[0].Charge == 1 && state.Obstacles[2].Charge == 1, "복수 발전기 독립 턴당1충전");
                        object effects = Invoke(typeof(FixedObstacleVerification), "Hit", state, C(0, 3), context);
                        Check(GeneratorRules.ActiveConnections(state).Count == 1 && state.Cells.Count(cell => cell.ObstacleIndex == 0) == 4 && state.Cells.All(cell => cell.ObstacleIndex != 2), "다른 발전기만 철거");
                        Record("twice", original, Snapshot(state) + ";effects=" + Snapshot(effects) + ";context=" + Context(context));
                    }
                    else
                        for (int turn = 1; turn <= 3; turn++)
                        {
                            context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, turn);
                            object effects = Invoke(typeof(FixedObstacleVerification), "Hit", state, C(4, 4), context);
                            Record(mode, original + "/turn=" + turn, Snapshot(state) + ";effects=" + Snapshot(effects) + ";context=" + Context(context));
                        }
                    if (mode != "twice") Check(!state.Cells.Any(cell => cell.ObstacleIndex.HasValue) && state.Missions.All(m => m.Progress == m.Target) && GeneratorRules.ActiveConnections(state).Count == 0, "복수연결/벽/와이어 완충 전체제거 " + mode);
                    Check(original == JsonUtility.ToJson(level) && pack.SequenceEqual(LevelPackCodec.Encode(new[] { level })), "복수연결 입력/ID/바이트 보존 " + mode);
                    Record("connection-pack", original, Convert.ToBase64String(pack));
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
        }
        private static void ReservationChecks(bool before)
        {
            for (int required = 3; required <= 5; required++)
            {
                LevelDefinition level = Input("reservation-" + required, before, item =>
                {
                    LevelDefinition fixture = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.Appliance, required);
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture), item); UnityEngine.Object.DestroyImmediate(fixture);
                });
                try
                {
                    for (int charge = 0; charge < required; charge++)
                    {
                        LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh(); Set(state.Obstacles[0], "Charge", charge);
                        string initial = Snapshot(state), turn = Context(context), global = JsonUtility.ToJson(UnityEngine.Random.state);
                        foreach (int count in new[] { 0, 1, 2, 4 })
                        {
                            DroneImpact[] impacts = LevelPlacementRules.Footprint(C(4, 4), 2).Take(count).Select(cell => (DroneImpact)Activator.CreateInstance(typeof(DroneImpact), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { state.CellAt(cell), MissionProgressRules.Query(state, cell, context) }, null)).ToArray();
                            object projection = Invoke(typeof(MissionProgressRules), "Project", state, impacts.Concat(impacts).ToArray());
                            Check((int)projection.GetType().GetField("Item2").GetValue(projection) == 0 && (int)projection.GetType().GetField("Item3").GetValue(projection) == (count == 0 ? 0 : 1), "4칸 중복 예약 본체1충전/피해0 " + required + charge + count);
                            Check(initial == Snapshot(state) && turn == Context(context) && global == JsonUtility.ToJson(UnityEngine.Random.state), "예약투영 상태/문맥/난수 무변경 " + required + charge + count);
                            Record("reservation-projection", required + "/q" + charge + "/cells=" + count, Deep(projection) + ";state=" + initial + ";context=" + turn);
                        }
                        DroneTargetManager manager = (DroneTargetManager)Invoke(typeof(GeneratorVerification), "Manager", state, context);
                        Dictionary<int, DroneTarget> reservations = (Dictionary<int, DroneTarget>)typeof(DroneTargetManager).GetField("reservations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                        DroneTarget target = manager.QueryArea(PowerArea.Horizontal).First(t => t.ObstacleIndex == 0); reservations.Add(1, target);
                        Record("reservation-observed", required + "/q" + charge, "charge=" + manager.ExpectedCharge + ";damage=" + manager.ExpectedDamage + ";complete=" + manager.ExpectedComplete + ";targets=" + Snapshot(manager.QueryArea(PowerArea.Horizontal)));
                        Check(manager.ExpectedCharge == 1 && manager.ExpectedDamage == 2 && manager.ExpectedComplete == (charge + 1 == required ? 1 : 0) && manager.QueryArea(PowerArea.Horizontal).All(t => t.ObstacleIndex != 0), "실제 범위 예약/미션/후보 제외 " + required + charge);
                        Check(initial == Snapshot(state) && turn == Context(context) && global == JsonUtility.ToJson(UnityEngine.Random.state), "실제 예약 조회 상태/문맥/난수 무변경 " + required + charge);
                        Record("reservation-manager", required + "/q" + charge, Snapshot(manager.QueryArea(PowerArea.Horizontal)) + ";charge=" + manager.ExpectedCharge + ";damage=" + manager.ExpectedDamage + ";complete=" + manager.ExpectedComplete + ";reservations=" + Deep(reservations));
                        reservations.Clear(); Check(manager.ExpectedCharge == 0 && manager.ExpectedComplete == 0 && manager.Query().Any(t => t.ObstacleIndex == 0), "예약 취소 복원 " + required + charge);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
        }
        private static void OtherChecks(bool before)
        {
            foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance, (ObstacleKind)(-1), (ObstacleKind)6 })
            {
                LevelDefinition level = Input("other-" + (int)kind, before, item => Invoke(typeof(FixedObstacleVerification), "Obstacle", item, (int)kind < 0 || (int)kind > 5 ? ObstacleKind.Crate : kind, LevelPlacementRules.MaxDurability((int)kind < 0 || (int)kind > 5 ? ObstacleKind.Crate : kind), C(4, 7), RabbitColor.Type1));
                try
                {
                    LevelRuntimeState state = Build(level); BoardCoordinate target = C(4, 7);
                    if ((int)kind < 0 || (int)kind > 5)
                    {
                        RuntimeObstacle body = state.Obstacles[0];
                        ObstaclePlacementDefinition invalid = (ObstaclePlacementDefinition)Activator.CreateInstance(typeof(ObstaclePlacementDefinition), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { body.Definition.Id, target, kind, 6, RabbitColor.Type1, 0 }, null);
                        typeof(RuntimeObstacle).GetField("<Definition>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(body, invalid);
                    }
                    foreach (DamageCause cause in new[] { DamageCause.AdjacentMatch, DamageCause.Power, DamageCause.MagnetAdjacent, DamageCause.Hammer, (DamageCause)(-1), (DamageCause)4 })
                        foreach (RabbitColor? color in new RabbitColor?[] { RabbitColor.Type1, RabbitColor.Type2, null }) Query(state, target, C(3, 7), cause, Fresh(), "other" + (int)kind, color);
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
                    LevelDefinition fixture = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.Appliance, 3);
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture), item); UnityEngine.Object.DestroyImmediate(fixture);
                    JsonUtility.FromJsonOverwrite("{\"levelNumber\":" + number + "}", item);
                });
                LevelDefinition copy = null;
                try
                {
                    byte[] bytes = LevelPackCodec.Encode(new[] { level }); copy = LevelPackCodec.ReadLevel(bytes, number);
                    int first = number <= 50 ? 1 : number <= 100 ? 51 : 101;
                    Check(LevelPackCodec.Decode(bytes).FormatVersion == 1 && LevelPackCodec.Decode(bytes).FirstLevel == first && LevelPackCodec.FirstLevel(number) == first && LevelPackCodec.Address(number) == "Levels/levels-" + first.ToString("D6"), "실제 버전1/50구간 경계 " + number);
                    Check(JsonUtility.ToJson(level) == JsonUtility.ToJson(copy) && bytes.SequenceEqual(LevelPackCodec.Encode(new[] { copy })), "실제 전체 필드/본체ID/연결ID/바이트 왕복 " + number);
                    Record("boundary-pack", JsonUtility.ToJson(level), Convert.ToBase64String(bytes));
                    LevelRuntimeState original = Build(level), decoded = Build(copy); TurnEffectContext a = Fresh(), b = Fresh();
                    object effectsA = Invoke(typeof(FixedObstacleVerification), "Hit", original, C(4, 4), a), effectsB = Invoke(typeof(FixedObstacleVerification), "Hit", decoded, C(4, 4), b);
                    Check(Snapshot(original) == Snapshot(decoded) && Context(a) == Context(b) && Snapshot(effectsA) == Snapshot(effectsB), "원본/MemoryPack 같은시드 충전 동일 " + number);
                    Record("boundary-pack-state", number.ToString(), Snapshot(decoded) + ";effects=" + Snapshot(effectsB) + ";context=" + Context(b));
                }
                finally { if (copy != null) UnityEngine.Object.DestroyImmediate(copy); UnityEngine.Object.DestroyImmediate(level); }
            }
        }
        private static void PolicyChecks()
        {
            Type policy = typeof(ElementId).Assembly.GetType("Elements.ElementDamageSourcePolicy");
            Check(policy != null, "불변 피해 원인 정책 존재");
            Check(policy.IsSealed && policy.GetProperties().All(property => property.SetMethod == null) && policy.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).All(field => field.IsInitOnly && field.FieldType == typeof(bool)), "정책 불변 bool 값만 보유");
            MethodInfo require = typeof(ElementDefinition).GetMethod("RequireDamageSourcePolicy"), allows = policy.GetMethod("Allows");
            ElementDefinition crate = LegacyElementDefinitions.Get(ObstacleKind.Generator); object prepared = require.Invoke(crate, null);
            Check(ReferenceEquals(prepared, require.Invoke(LegacyElementDefinitions.Get(ObstacleKind.Generator), null)), "발전기 정책 한 번 준비");
            foreach (DamageCause cause in Enum.GetValues(typeof(DamageCause))) Check((bool)allows.Invoke(prepared, new object[] { cause }) == true, "발전기 정책 실제 허용 " + cause);
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

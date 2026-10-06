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
    /// <summary>예약 피해량의 실제 입력과 공통 집계 연결을 별도로 검증한다.</summary>
    public static class ReservedDamagePolicyVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage26";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static readonly Type Rules = typeof(DamageReaction).Assembly.GetType("Simulation.ObstacleDamageRules");
        private static readonly ObstacleKind[] Kinds = { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance };
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static string Context(TurnEffectContext value) => (string)Invoke(typeof(DamageAggregationPolicyVerification), "Context", value);
        private static LevelRuntimeState Build(LevelDefinition level) => (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
        private static TurnEffectContext Fresh() => (TurnEffectContext)Invoke(typeof(FixedObstacleVerification), "Context");
        private static int Maximum(ObstacleKind kind) => kind == ObstacleKind.Crate ? 6 : kind == ObstacleKind.ColorLock ? 3 : kind == ObstacleKind.Appliance ? 9 : 5;
        private static MissionKind Mission(ObstacleKind kind) => kind == ObstacleKind.Scrap ? MissionKind.Scrap : kind == ObstacleKind.Safe ? MissionKind.Safe : kind == ObstacleKind.ColorLock ? MissionKind.ColorLock : kind == ObstacleKind.Appliance ? MissionKind.Appliance : MissionKind.Crate;
        private static int Reserved(RuntimeObstacle body, int cells) => (int)Invoke(Rules, "ReservedDamage", body, cells);
        private static void Durability(RuntimeObstacle body, int value) => typeof(RuntimeObstacle).GetProperty("Durability").GetSetMethod(true).Invoke(body, new object[] { value });
        [Serializable] private sealed class Row { public string name, input, output; }
        private static void Record(string name, string input, string output) => Values.Add(JsonUtility.ToJson(new Row { name = name, input = input, output = output }));
        private static LevelDefinition Input(string key, bool before, Action<LevelDefinition> prepare)
        {
            LevelDefinition level = before ? (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make") : ScriptableObject.CreateInstance<LevelDefinition>();
            string path = Evidence + "/input-" + key + ".json";
            if (before && !File.Exists(path)) { prepare(level); File.WriteAllText(path, JsonUtility.ToJson(level)); }
            else JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level);
            return level;
        }
        private static LevelDefinition Fixture(ObstacleKind kind, int durability, bool before) => Input(kind + "-" + durability, before, level =>
        {
            Invoke(typeof(FixedObstacleVerification), "Obstacle", level, kind, durability, C(4, 4), RabbitColor.Type1);
            Invoke(typeof(FixedObstacleVerification), "Obstacle", level, kind, durability, C(1, 1), RabbitColor.Type1);
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)Mission(kind) + ",\"count\":2}]}", level);
        });
        public static void Before() => Execute(true);
        public static void Run() => Execute(false);
        private static void Execute(bool before)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear(); int exit = 0;
            try
            {
                if (!before) ConnectionChecks();
                DirectChecks(before); ProjectionChecks(before); ManagerChecks(before); OtherChecks(before); ExistingExecutionChecks();
                if (before) File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values);
                else Check(RecordedLogicComparison.Equal(File.ReadAllLines(Evidence + "/baseline-values.jsonl"), Values), "기존 논리/입력/팩 비교 · 추가 비행 표시 이력 별도");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                string mode = before ? "before" : "after";
                File.WriteAllLines(Evidence + "/" + mode + "-results.txt", Results); File.WriteAllLines(Evidence + "/" + mode + "-values.jsonl", Values);
            }
            EditorApplication.Exit(exit);
        }
        private static void DirectChecks(bool before)
        {
            foreach (ObstacleKind kind in Kinds)
                for (int initial = 1; initial <= Maximum(kind); initial++)
                {
                    LevelDefinition level = Fixture(kind, initial, before);
                    try
                    {
                        foreach (string mode in new[] { "fresh", "null", "same-body", "other-body", "same-hit", "other-cell", "other-hit", "next-turn", "zero", "deleted" })
                        {
                            LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh(); RuntimeObstacle body = state.Obstacles[0];
                            if (mode == "null") context = null;
                            if (mode == "same-body" || mode == "next-turn") typeof(TurnEffectContext).GetMethod("RegisterDamage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(context, new object[] { 0 });
                            if (mode == "other-body") typeof(TurnEffectContext).GetMethod("RegisterDamage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(context, new object[] { 1 });
                            if (mode == "same-hit" || mode == "other-cell" || mode == "other-hit") typeof(TurnEffectContext).GetMethod("RegisterHit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(context, new object[] { mode == "other-hit" ? 8 : 7, mode == "other-cell" ? C(4, 5) : C(4, 4) });
                            if (mode == "next-turn") context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, 2);
                            if (mode == "zero") Durability(body, 0);
                            if (mode == "deleted") Invoke(Rules, "Remove", state, 0);
                            foreach (int cells in new[] { -1, 0, 1, 2, 4, 9, 81 })
                            {
                                string snapshot = Snapshot(state), turn = Context(context), global = JsonUtility.ToJson(UnityEngine.Random.state), definitions = Snapshot(LegacyElementDefinitions.Get(kind));
                                int actual = Reserved(body, cells);
                                int expected = cells < 0 ? -1 : Math.Min(body.Durability, kind == ObstacleKind.Appliance ? cells : cells == 0 ? 0 : 1);
                                Check(actual == expected, "전체 내구도/0/삭제/칸수·음수 예약량 " + kind + initial + mode + cells);
                                Check(snapshot == Snapshot(state) && turn == Context(context) && global == JsonUtility.ToJson(UnityEngine.Random.state) && definitions == Snapshot(LegacyElementDefinitions.Get(kind)), "직접 조회 상태/문맥/정의/난수 무변경");
                                Record("direct", kind + "/initial=" + initial + "/mode=" + mode + "/cells=" + cells, "amount=" + actual + ";state=" + snapshot + ";context=" + turn);
                            }
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static DroneImpact Impact(LevelRuntimeState state, BoardCoordinate cell, TurnEffectContext context) => (DroneImpact)Activator.CreateInstance(typeof(DroneImpact), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { state.CellAt(cell), MissionProgressRules.Query(state, cell, context) }, null);
        private static void ProjectionChecks(bool before)
        {
            foreach (ObstacleKind kind in Kinds)
                for (int durability = 1; durability <= Maximum(kind); durability++)
                {
                    LevelDefinition level = Fixture(kind, durability, before);
                    try
                    {
                        foreach (int count in new[] { 0, 1, 2, 4 })
                            foreach (bool second in new[] { false, true })
                                foreach (bool duplicate in new[] { false, true })
                                {
                                    LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
                                    BoardCoordinate[] footprint = LevelPlacementRules.Footprint(C(4, 4), kind == ObstacleKind.Appliance ? 2 : 1).ToArray();
                                    DroneImpact[] impacts = footprint.Take(count).Select(cell => Impact(state, cell, context)).Concat(second ? new[] { Impact(state, C(1, 1), context) } : Array.Empty<DroneImpact>()).ToArray();
                                    if (duplicate) impacts = impacts.Concat(impacts).Concat(impacts.Take(1)).ToArray();
                                    string snapshot = Snapshot(state), turn = Context(context), global = JsonUtility.ToJson(UnityEngine.Random.state);
                                    object projected = Invoke(typeof(MissionProgressRules), "Project", state, impacts);
                                    int actual = (int)projected.GetType().GetField("Item2").GetValue(projected), charge = (int)projected.GetType().GetField("Item3").GetValue(projected);
                                    MissionContribution[] contributions = ((IEnumerable)projected.GetType().GetField("Item1").GetValue(projected)).Cast<MissionContribution>().ToArray();
                                    int amount = Math.Min(durability, kind == ObstacleKind.Appliance ? count : count == 0 ? 0 : 1);
                                    int expected = amount + (second ? 1 : 0), completed = (count > 0 && amount >= durability ? 1 : 0) + (second && durability == 1 ? 1 : 0);
                                    int[] pending = ((IEnumerable<int>)Invoke(typeof(MissionProgressRules), "PendingBodyRemovals", state, impacts)).ToArray();
                                    Check(actual == expected && charge == 0 && contributions.Sum(c => c.Damage) == expected && contributions.Sum(c => c.ExpectedComplete) == completed && pending.Length == completed, "중복제거/다른칸·본체/상한·기여·완료예상 " + kind + durability + count + second + duplicate);
                                    Check(contributions.Select(c => c.BodyIndex).Distinct().Count() == contributions.Length && snapshot == Snapshot(state) && turn == Context(context) && global == JsonUtility.ToJson(UnityEngine.Random.state), "본체 기여 중복 없음/투영 무변경");
                                    Record("project", kind + "/d=" + durability + "/count=" + count + "/other=" + second + "/duplicate=" + duplicate + "/impacts=" + Snapshot(impacts), "damage=" + actual + ";charge=" + charge + ";contributions=" + Snapshot(contributions) + ";pending=" + Snapshot(pending) + ";state=" + snapshot + ";context=" + turn);
                                }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static void ManagerChecks(bool before)
        {
            foreach (ObstacleKind kind in Kinds)
                for (int durability = 1; durability <= Maximum(kind); durability++)
                {
                    LevelDefinition level = Fixture(kind, durability, before);
                    try
                    {
                        LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
                        DroneTargetManager manager = (DroneTargetManager)Invoke(typeof(GeneratorVerification), "Manager", state, context);
                        int request = (int)typeof(DroneTargetManager).GetMethod("Request", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { C(8, 8) });
                        Dictionary<int, DroneTarget> reservations = (Dictionary<int, DroneTarget>)typeof(DroneTargetManager).GetField("reservations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                        Check(manager.ReservationCount == 1 && manager.ExpectedDamage == 1 && manager.ExpectedComplete == (durability == 1 ? 1 : 0), "실제 요청 예약/예상량");
                        Record("manager-reserved", kind + "/d=" + durability, Snapshot(manager.Query()) + ";state=" + Snapshot(state) + ";context=" + Context(context));
                        DroneTarget target = reservations[request];
                        typeof(DroneTargetManager).GetMethod("Release", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { request, C(8, 8), "검사 취소" });
                        Check(manager.ReservationCount == 0 && manager.ExpectedDamage == 0 && manager.ExpectedComplete == 0, "실제 Release 예약취소");
                        int next = (int)typeof(DroneTargetManager).GetMethod("Request", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { C(8, 8) });
                        target = reservations[next]; int index = target.ObstacleIndex.Value;
                        Invoke(Rules, "Remove", state, index);
                        Check(manager.ExpectedDamage == 0 && manager.ExpectedComplete == 0 && manager.ReservationCount == 1, "삭제된 예약 조회 제외/착탄까지 보유");
                        typeof(DroneTargetManager).GetMethod("Invalidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
                        DroneTarget landed = (DroneTarget)typeof(DroneTargetManager).GetMethod("Land", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { next, C(8, 8) });
                        Check(landed != null && landed.ObstacleIndex.HasValue && landed.ObstacleIndex != index && manager.ReservationCount == 0, "무효화/다른본체 재선정/착탄 예약반환");
                        Record("manager-retarget", kind + "/d=" + durability, Snapshot(landed) + ";state=" + Snapshot(state) + ";context=" + Context(context));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static void OtherChecks(bool before)
        {
            LevelDefinition level = Input("generator", before, item =>
            {
                LevelDefinition source = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.Crate, 3);
                try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), item); }
                finally { UnityEngine.Object.DestroyImmediate(source); }
            });
            try
            {
                foreach (int kind in new[] { 5, -1, 6, 99 })
                    foreach (int durability in new[] { 0, 1, 3 })
                        foreach (int cells in new[] { -1, 0, 1, 2, 4, 9, 81 })
                        {
                            LevelRuntimeState state = Build(level); RuntimeObstacle body = state.Obstacles[0];
                            ObstaclePlacementDefinition changed = (ObstaclePlacementDefinition)Activator.CreateInstance(typeof(ObstaclePlacementDefinition), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { body.Definition.Id, body.Definition.Coordinate, (ObstacleKind)kind, durability, RabbitColor.Type1, body.Definition.RequiredCharge }, null);
                            typeof(RuntimeObstacle).GetField("<Definition>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(body, changed);
                            Durability(body, durability); string snapshot = Snapshot(state), global = JsonUtility.ToJson(UnityEngine.Random.state);
                            int actual = Reserved(body, cells), expected = cells == -1 ? -1 : cells == 0 ? 0 : durability == 0 ? 0 : 1;
                            Check(actual == expected && snapshot == Snapshot(state) && global == JsonUtility.ToJson(UnityEngine.Random.state), "발전기/미지원 정책 강제 없음/경계 보존 " + kind + durability + cells);
                            Record("unsupported", "kind=" + kind + "/d=" + durability + "/cells=" + cells, "amount=" + actual + ";state=" + snapshot);
                        }
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
        // 기존 실제 실행 검사는 메모리 입력만 재사용하고 과거 출력 파일은 쓰지 않는다.
        private static void ExistingExecutionChecks()
        {
            Type previous = typeof(DamageAggregationPolicyVerification);
            List<string> results = (List<string>)previous.GetField("Results", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            List<string> values = (List<string>)previous.GetField("Values", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            results.Clear(); values.Clear();
            foreach (string method in new[] { "ApplyChecks", "AdjacentChecks", "HitChecks", "ReservationChecks", "OverlapChecks", "GeneratorChecks", "BodyApplyChecks", "DeletedChecks", "PackChecks", "ManagerChecks", "BodyHitChecks" }) Invoke(previous, method, false);
            Results.AddRange(results); Values.AddRange(values);
        }
        private static void ConnectionChecks()
        {
            ElementCatalog catalog = (ElementCatalog)typeof(LegacyElementDefinitions).GetField("Catalog", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Dictionary<ElementId, ElementDefinition> definitions = (Dictionary<ElementId, ElementDefinition>)typeof(ElementCatalog).GetField("definitions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(catalog);
            foreach (ObstacleKind kind in Kinds)
            {
                ElementDefinition original = LegacyElementDefinitions.Get(kind); LevelDefinition level = Fixture(kind, Maximum(kind), false);
                try
                {
                    RuntimeObstacle body = Build(level).Obstacles[0];
                    foreach (bool perCell in new[] { true, false })
                    {
                        definitions[original.Id] = new ElementDefinition(original.Id, original.DisplayName, original.Placement, original.ChargePlacement, original.DamageSourcePolicy, original.ColorMatchPolicy, new ElementDamageAggregationPolicy(perCell), null, original.ReactionBehavior);
                        Check(ReferenceEquals(LegacyElementDefinitions.Get(kind), definitions[original.Id]), "같은 ID 메모리 등록 실제 조회");
                        foreach (var test in new[] { (cells: -1, want: -1), (cells: 0, want: 0), (cells: 1, want: 1), (cells: 2, want: perCell ? 2 : 1) })
                        {
                            int actual = Reserved(body, test.cells);
                            Check(actual == test.want, "ReservedDamage가 종류 조건 대신 등록 집계를 따름 " + kind + "/perCell=" + perCell + "/cells=" + test.cells + "/actual=" + actual + "/want=" + test.want);
                        }
                    }
                    definitions[original.Id] = new ElementDefinition(original.Id, original.DisplayName, original.Placement, original.ChargePlacement, original.DamageSourcePolicy, original.ColorMatchPolicy, null, null, original.ReactionBehavior);
                    Exception error = null; try { Reserved(body, 0); } catch (TargetInvocationException found) { error = found.InnerException; }
                    Check(error is InvalidOperationException && error.Message.Contains(original.Id.Value), "유효 내구도형 누락은 ID 오류/0칸도 기본값 대체 없음 " + kind);
                }
                finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(kind), original), "finally 원래 등록 참조 복원 " + kind); }
            }
        }
    }
}

using System;
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
    /// <summary>실제 타격 후 턴 기록과 후속 조회가 같은 집계 정책을 사용하는지 검증한다.</summary>
    public static class DamageRecordPolicyVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage27";
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
        private static TurnEffectContext Next(TurnEffectContext context) => (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Next", context, context.Turn + 1);
        private static int Maximum(ObstacleKind kind) => kind == ObstacleKind.Crate ? 6 : kind == ObstacleKind.ColorLock ? 3 : kind == ObstacleKind.Appliance ? 9 : 5;
        private static MissionKind Mission(ObstacleKind kind) => kind == ObstacleKind.Scrap ? MissionKind.Scrap : kind == ObstacleKind.Safe ? MissionKind.Safe : kind == ObstacleKind.ColorLock ? MissionKind.ColorLock : kind == ObstacleKind.Appliance ? MissionKind.Appliance : MissionKind.Crate;
        private static void Durability(RuntimeObstacle body, int value) => typeof(RuntimeObstacle).GetProperty("Durability").GetSetMethod(true).Invoke(body, new object[] { value });
        private static HashSet<int> Bodies(TurnEffectContext context) => (HashSet<int>)typeof(TurnEffectContext).GetField("damagedObstacles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context);
        private static HashSet<(int, BoardCoordinate)> Hits(TurnEffectContext context) => (HashSet<(int, BoardCoordinate)>)typeof(TurnEffectContext).GetField("hitCells", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context);
        [Serializable] private sealed class Row { public string name, input, output; }
        private static void Record(string name, string input, string output) => Values.Add(JsonUtility.ToJson(new Row { name = name, input = input, output = output }));
        private static LevelDefinition Input(string key, bool before, Action<LevelDefinition> prepare)
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>(); string path = Evidence + "/input-" + key + ".json";
            if (before && !File.Exists(path))
            {
                LevelDefinition source = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
                try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), level); }
                finally { UnityEngine.Object.DestroyImmediate(source); }
                prepare(level); File.WriteAllText(path, JsonUtility.ToJson(level));
            }
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
                SequenceChecks(before); DirectChecks(before); ExistingChecks();
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
        private static void SequenceChecks(bool before)
        {
            foreach (ObstacleKind kind in Kinds)
                for (int durability = 1; durability <= Maximum(kind); durability++)
                {
                    LevelDefinition level = Fixture(kind, durability, before);
                    try
                    {
                        foreach (int firstHit in new[] { -1, 0, 7, 8 })
                            foreach (string mode in new[] { "fresh", "same-body", "other-body", "same-hit", "other-cell", "other-hit", "next-turn", "zero", "deleted" })
                            {
                                LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
                                if (mode == "same-body" || mode == "next-turn") Bodies(context).Add(0);
                                if (mode == "other-body") Bodies(context).Add(1);
                                if (mode == "same-hit" || mode == "other-cell" || mode == "other-hit") Hits(context).Add((mode == "other-hit" ? firstHit + 1 : firstHit, mode == "other-cell" ? C(4, 5) : C(4, 4)));
                                if (mode == "next-turn") context = Next(context);
                                if (mode == "zero") Durability(state.Obstacles[0], 0);
                                if (mode == "deleted") Invoke(Rules, "Remove", state, 0);
                                var requests = new (BoardCoordinate cell, int hit)[] {
                                    (C(4, 4), firstHit), (C(4, 4), firstHit),
                                    (kind == ObstacleKind.Appliance ? C(4, 5) : C(4, 4), firstHit),
                                    (C(4, 4), firstHit + 1), (C(1, 1), firstHit),
                                    (C(4, 4), firstHit + 2), (C(1, 1), firstHit + 1), (C(4, 4), firstHit)
                                };
                                for (int step = 0; step < requests.Length; step++)
                                {
                                    if (step == 7) context = Next(context);
                                    var request = requests[step]; RuntimeCell cell = state.CellAt(request.cell);
                                    string initial = Snapshot(state), turn = Context(context), global = JsonUtility.ToJson(UnityEngine.Random.state), definitions = Snapshot(LegacyElementDefinitions.Get(kind));
                                    DamageReaction reaction = DamageReaction.Evaluate(state, request.cell, DamageCause.Power, request.cell, context, null, request.hit);
                                    Check(initial == Snapshot(state) && turn == Context(context), "타격 전 조회 상태/문맥 무변경");
                                    int? remaining = null;
                                    if (reaction.Response == DamageResponse.Damage)
                                    {
                                        int body = cell.ObstacleIndex.Value, previous = state.Obstacles[body].Durability;
                                        HashSet<int> bodies = new HashSet<int>(Bodies(context)); HashSet<(int, BoardCoordinate)> hits = new HashSet<(int, BoardCoordinate)>(Hits(context));
                                        if (kind == ObstacleKind.Appliance) hits.Add((request.hit, request.cell)); else bodies.Add(body);
                                        remaining = (int)Invoke(Rules, "Apply", state, cell, context, request.hit);
                                        Check(remaining == previous - 1 && state.Obstacles[body].Durability == previous - 1 && Bodies(context).SetEquals(bodies) && Hits(context).SetEquals(hits), "실제 기록/내구도 감소 " + kind + durability + mode + firstHit + step);
                                        DamageReaction follow = DamageReaction.Evaluate(state, request.cell, DamageCause.Power, request.cell, context, null, request.hit);
                                        DamageResponse expected = remaining == 0 ? DamageResponse.None : kind == ObstacleKind.Appliance && request.hit <= 0 ? DamageResponse.Damage : DamageResponse.AlreadyDamaged;
                                        Check(follow.Response == expected, "실제 기록 후 동일 타격 재조회 " + kind + mode + request.hit);
                                        Check(state.Cells.Count(c => c.ObstacleIndex == body) == (remaining == 0 ? 0 : kind == ObstacleKind.Appliance ? 4 : 1), "기록 뒤 감소/본체 점유 제거");
                                    }
                                    int complete = state.Obstacles.Count(body => body.Durability == 0);
                                    // 점유를 유지한 인위적 내구도0은 실제 제거 미션에 포함하지 않는다.
                                    if (mode == "zero") complete--;
                                    Check(state.Missions[0].Progress == complete, "실제 제거/미션 집계 순서");
                                    Check(global == JsonUtility.ToJson(UnityEngine.Random.state) && definitions == Snapshot(LegacyElementDefinitions.Get(kind)), "실제 타격 규칙/전역 난수 무변경");
                                    Record("sequence", kind + "/d=" + durability + "/mode=" + mode + "/firstHit=" + firstHit + "/step=" + step + "/target=" + request.cell + "/hit=" + request.hit,
                                        "reaction=" + Snapshot(reaction) + ";remaining=" + remaining + ";state=" + Snapshot(state) + ";context=" + Context(context));
                                }
                            }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
        private static void DirectChecks(bool before)
        {
            LevelDefinition level = Fixture(ObstacleKind.Crate, 1, before);
            try
            {
                foreach (int kind in new[] { -1, 0, 1, 2, 3, 4, 5, 6, 99 })
                    foreach (int durability in new[] { -1, 0, 1 })
                        foreach (int hit in new[] { -1, 0, 7 })
                            foreach (string mode in new[] { "fresh", "null-context", "null-cell", "null-state", "deleted" })
                            {
                                LevelRuntimeState state = Build(level); RuntimeObstacle body = state.Obstacles[0]; TurnEffectContext context = Fresh(); RuntimeCell cell = state.CellAt(C(4, 4));
                                ObstaclePlacementDefinition changed = (ObstaclePlacementDefinition)Activator.CreateInstance(typeof(ObstaclePlacementDefinition), BindingFlags.Instance | BindingFlags.NonPublic, null,
                                    new object[] { body.Definition.Id, body.Definition.Coordinate, (ObstacleKind)kind, durability, RabbitColor.Type1, 0 }, null);
                                typeof(RuntimeObstacle).GetField("<Definition>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(body, changed); Durability(body, durability);
                                if (mode == "deleted") Invoke(Rules, "Remove", state, 0);
                                if (mode == "null-context") context = null;
                                string initial = Snapshot(state), turn = Context(context), global = JsonUtility.ToJson(UnityEngine.Random.state);
                                Exception error = null; int? actual = null;
                                try { actual = (int)Invoke(Rules, "Apply", mode == "null-state" ? null : state, mode == "null-cell" ? null : cell, context, hit); }
                                catch (TargetInvocationException found) { error = found.InnerException; }
                                if (mode != "fresh")
                                    Check((mode == "deleted" ? error is InvalidOperationException : error is NullReferenceException) && initial == Snapshot(state) && turn == Context(context), "직접 삭제/null 실패와 무변경 " + kind + durability + hit + mode);
                                else
                                {
                                    Check(error == null && actual == durability - 1 && body.Durability == durability - 1, "직접0/음수/발전기/미지원 내구도 경계 " + kind + durability + hit);
                                    Check(kind == 4 ? Hits(context).Contains((hit, C(4, 4))) && Bodies(context).Count == 0 : Bodies(context).SetEquals(new[] { 0 }) && Hits(context).Count == 0, "직접 칸/본체 기록 선택 경계");
                                }
                                Check(global == JsonUtility.ToJson(UnityEngine.Random.state), "직접 적용 전역 난수 무변경");
                                Record("direct-apply", "kind=" + kind + "/d=" + durability + "/hit=" + hit + "/mode=" + mode, "return=" + actual + ";error=" + error?.GetType().Name + ";before=" + initial + ";state=" + Snapshot(state) + ";beforeContext=" + turn + ";context=" + Context(context));
                            }
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
        // 이전 입력·실제 실행 검사를 재사용하되 과거 출력 파일을 덮어쓰지 않는다.
        private static void ExistingChecks()
        {
            Type previous = typeof(ReservedDamagePolicyVerification);
            List<string> results = (List<string>)previous.GetField("Results", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            List<string> values = (List<string>)previous.GetField("Values", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            results.Clear(); values.Clear();
            foreach (string method in new[] { "DirectChecks", "ProjectionChecks", "ManagerChecks", "OtherChecks" }) Invoke(previous, method, false);
            Invoke(previous, "ExistingExecutionChecks"); Results.AddRange(results); Values.AddRange(values);
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
                    foreach (bool perCell in new[] { true, false })
                    {
                        definitions[original.Id] = new ElementDefinition(original.Id, original.DisplayName, original.Placement, original.ChargePlacement, original.DamageSourcePolicy, original.ColorMatchPolicy, new ElementDamageAggregationPolicy(perCell), original.RemovalMissionProfile, original.ReactionBehavior);
                        Check(ReferenceEquals(LegacyElementDefinitions.Get(kind), definitions[original.Id]), "같은 ID 등록 실제 조회 " + kind + perCell);
                        LevelRuntimeState state = Build(level); TurnEffectContext context = Fresh();
                        BoardCoordinate[] cells = { C(4, 4), C(4, 4), C(4, 4), C(1, 1), kind == ObstacleKind.Appliance ? C(4, 5) : C(4, 4), C(1, 1) };
                        int[] groups = { 7, 7, 8, 7, 7, 8 }; bool[] allowed = perCell ? new[] { true, false, true, true, kind == ObstacleKind.Appliance, true } : new[] { true, false, false, true, false, false };
                        int[] applied = { 0, 0 };
                        for (int step = 0; step < cells.Length; step++)
                        {
                            DamageReaction reaction = DamageReaction.Evaluate(state, cells[step], DamageCause.Power, cells[step], context, null, groups[step]);
                            DamageResponse want = allowed[step] ? DamageResponse.Damage : DamageResponse.AlreadyDamaged;
                            Check(reaction.Response == want, "실제 등록 집계 Query→Apply→후속조회 " + kind + "/perCell=" + perCell + "/step=" + step + "/actual=" + reaction.Response + "/want=" + want);
                            if (reaction.Response == DamageResponse.Damage)
                            {
                                int body = state.CellAt(cells[step]).ObstacleIndex.Value;
                                Invoke(Rules, "Apply", state, state.CellAt(cells[step]), context, groups[step]); applied[body]++;
                                Check(state.Obstacles[body].Durability == Maximum(kind) - applied[body], "정책이 실제 피해량에 반영됨 " + kind + perCell + step);
                            }
                        }
                        Check(perCell ? Bodies(context).Count == 0 && Hits(context).Count == (kind == ObstacleKind.Appliance ? 5 : 4) : Bodies(context).SetEquals(new[] { 0, 1 }) && Hits(context).Count == 0, "실제 비공개 기록 선택 " + kind + perCell);
                    }
                    definitions[original.Id] = new ElementDefinition(original.Id, original.DisplayName, original.Placement, original.ChargePlacement, original.DamageSourcePolicy, original.ColorMatchPolicy, null, original.RemovalMissionProfile, original.ReactionBehavior);
                    LevelRuntimeState missing = Build(level); TurnEffectContext turn = Fresh(); string initial = Snapshot(missing), originalContext = Context(turn); Exception error = null;
                    try { Invoke(Rules, "Apply", missing, missing.CellAt(C(4, 4)), turn, 0); } catch (TargetInvocationException found) { error = found.InnerException; }
                    Check(error is InvalidOperationException && error.Message.Contains(original.Id.Value) && initial == Snapshot(missing) && originalContext == Context(turn), "누락 ID 오류/타격 기록·내구도 무변경 " + kind);
                }
                finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(kind), original), "finally 원래 등록 참조 복원 " + kind); }
            }
        }
    }
}

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
    /// <summary>최초 본체 수량과 검증/실행 구성의 정의 연결을 동일 입력으로 검사한다.</summary>
    public static class InitialMissionSupplyVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage29";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static readonly List<string> Connections = new List<string>();
        private static readonly ObstacleKind[] Kinds = { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance };
        private static readonly MissionKind[] Missions = { MissionKind.Crate, MissionKind.Scrap, MissionKind.Safe, MissionKind.ColorLock, MissionKind.Appliance };
        private static readonly int[] Limits = { 6, 5, 5, 3, 9 };
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        [Serializable] private sealed class Row { public string name, input, output; }
        private static string RowText(string name, string input, string output) => JsonUtility.ToJson(new Row { name = name, input = input, output = output });
        private static string Summary(MissionSupplySummary value) => value.Initial + "|" + value.Fixed + "|" + value.Maintained + "|" + value.Dynamic + "|" + value.GoalBased + "|" + value.Maximum + "|" + value;
        private static Dictionary<ElementId, ElementDefinition> Definitions()
        {
            object catalog = typeof(LegacyElementDefinitions).GetField("Catalog", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            return (Dictionary<ElementId, ElementDefinition>)typeof(ElementCatalog).GetField("definitions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(catalog);
        }
        private static LevelDefinition Fixture(int index, int durability, int bodies, bool mixed)
        {
            string path = Evidence + "/input-" + index + "-" + durability + "-" + bodies + "-" + mixed + ".json";
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            if (!File.Exists(path))
            {
                LevelDefinition source = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
                try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), level); }
                finally { UnityEngine.Object.DestroyImmediate(source); }
                for (int body = 0; body < bodies; body++)
                    Invoke(typeof(FixedObstacleVerification), "Obstacle", level, Kinds[index], durability, new BoardCoordinate(body == 0 ? 4 : 1, body == 0 ? 4 : 1), RabbitColor.Type1);
                int other = index == 0 ? 1 : 0;
                if (mixed) Invoke(typeof(FixedObstacleVerification), "Obstacle", level, Kinds[other], 1, new BoardCoordinate(7, 1), RabbitColor.Type1);
                SetMissions(level, index, 1, mixed);
                File.WriteAllText(path, JsonUtility.ToJson(level));
            }
            else JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level);
            return level;
        }
        private static void SetMissions(LevelDefinition level, int index, int count, bool mixed)
        {
            int other = index == 0 ? 1 : 0;
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)Missions[index] + ",\"count\":" + count + "}" + (mixed ? ",{\"kind\":" + (int)Missions[other] + ",\"count\":1}" : "") + "]}", level);
        }
        public static void Before() => Execute("before");
        public static void Red() => Execute("red");
        public static void FixtureProbe() => Execute("fixture-probe");
        public static void Run() => Execute("after");
        private static void Execute(string mode)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear(); Connections.Clear(); int exit = 0;
            try
            {
                if (mode == "fixture-probe") ProbeChecks();
                else if (mode != "before") { ConnectionChecks(); MissingChecks(); }
                if (mode != "red" && mode != "fixture-probe")
                {
                    DefaultChecks(); SupplyChecks(); BoundaryChecks(); ExistingChecks();
                    if (mode == "before") File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values);
                    else Check(RecordedLogicComparison.Equal(File.ReadAllLines(Evidence + "/baseline-values.jsonl"), Values), "기존 논리/입력/팩 비교 · 추가 비행 표시 이력 별도");
                    File.WriteAllText(Evidence + "/definitions-" + mode + ".json", Snapshot(Kinds.Select(LegacyElementDefinitions.Get).ToArray()));
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                File.WriteAllLines(Evidence + "/" + mode + "-results.txt", Results);
                File.WriteAllLines(Evidence + "/" + mode + "-values.jsonl", Values);
                File.WriteAllLines(Evidence + "/" + mode + "-connection-values.jsonl", Connections);
            }
            EditorApplication.Exit(exit);
        }
        private static void DefaultChecks()
        {
            for (int index = 0; index < Kinds.Length; index++)
                for (int durability = 1; durability <= Limits[index]; durability++)
                    foreach (int bodies in new[] { 0, 1, 2 })
                        foreach (bool mixed in new[] { false, true })
                        {
                            LevelDefinition level = Fixture(index, durability, bodies, mixed);
                            try
                            {
                                foreach (int goal in new[] { 0, 1, 2, 3 })
                                {
                                    SetMissions(level, index, goal, mixed);
                                    string input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state);
                                    bool dirty = EditorUtility.IsDirty(level);
                                    MissionSupplySummary quantity = LevelMissionRules.Supply(level, level.Missions[0]);
                                    List<LevelValidationIssue> missions = new List<LevelValidationIssue>(); LevelMissionRules.Validate(level, missions);
                                    List<LevelValidationIssue> all = LevelDefinitionValidator.Validate(level);
                                    LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                                    Check(quantity.Initial == bodies && quantity.Fixed == 0 && quantity.Maintained == 0 && !quantity.Dynamic && !quantity.GoalBased && quantity.Maximum == bodies, "내구도/점유칸 아닌 최초 본체 수 " + index + "/" + durability + "/" + bodies + "/" + mixed + "/" + goal);
                                    Check(missions.Count == (goal == 0 || goal > bodies ? 1 : 0) && (missions.Count == 0 || missions[0].Code == (goal == 0 ? LevelValidationCode.InvalidMission : LevelValidationCode.InsufficientSupply)), "목표0/1/상한 실제 미션 검증");
                                    Check(Snapshot(all) == Snapshot(built.Issues) && built.IsBuilt == (all.Count == 0), "전체 검증과 실행 구성 응답 연결");
                                    Check(input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state) && dirty == EditorUtility.IsDirty(level), "조회/구성 입력·전역 난수·dirty 보존");
                                    Values.Add(RowText("initial-supply", index + "/d=" + durability + "/n=" + bodies + "/mixed=" + mixed + "/goal=" + goal, "input=" + input + ";summary=" + Summary(quantity) + ";missions=" + Snapshot(missions) + ";all=" + Snapshot(all) + ";built=" + Snapshot(built)));
                                }
                            }
                            finally { UnityEngine.Object.DestroyImmediate(level); }
                        }
        }
        private static void ConnectionChecks()
        {
            Dictionary<ElementId, ElementDefinition> definitions = Definitions();
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Crate);
            LevelDefinition level = Fixture(0, 1, 1, true);
            try
            {
                Check(LevelDefinitionValidator.Validate(level).Count == 0, "Crate/Scrap 본체와 양쪽 미션의 유효 저장 입력");
                definitions[original.Id] = new ElementDefinition(original.Id, original.DisplayName, original.Placement, original.ChargePlacement, original.DamageSourcePolicy, original.ColorMatchPolicy, original.DamageAggregationPolicy, new ElementRemovalMissionProfile(MissionKind.Scrap), original.ReactionBehavior);
                MissionSupplySummary crate = LevelMissionRules.Supply(level, level.Missions[0]);
                MissionSupplySummary scrap = LevelMissionRules.Supply(level, level.Missions[1]);
                List<LevelValidationIssue> issues = new List<LevelValidationIssue>(); LevelMissionRules.Validate(level, issues);
                Connections.Add(RowText("same-id-initial", JsonUtility.ToJson(level), "id=" + original.Id.Value + ";crate=" + Summary(crate) + ";scrap=" + Summary(scrap) + ";wantInitial=0/2;actualValidate=" + Snapshot(issues)));
                Check(crate.Initial == 0 && scrap.Initial == 2 && issues.Count == 1 && issues[0].Code == LevelValidationCode.InsufficientSupply && issues[0].PropertyPath == "missions.Array.data[0]", "같은 ID 프로필 교체 actual=" + crate.Initial + "/" + scrap.Initial + " want=0/2 및 실제 부족 공급 판단");
            }
            finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Crate), original), "finally 원래 등록 참조 정확한 복원"); }
        }
        private static void ExistingChecks()
        {
            Type previous = typeof(RemovalMissionProfileVerification);
            List<string> results = (List<string>)previous.GetField("Results", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            List<string> values = (List<string>)previous.GetField("Values", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            results.Clear(); values.Clear();
            Invoke(previous, "DefaultChecks", false); Invoke(previous, "ExistingChecks");
            Results.AddRange(results); Values.AddRange(values);
        }
        private static Exception Error(Action action)
        {
            try { action(); return null; }
            catch (Exception error) { while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException; return error; }
        }
        private static void ProbeChecks()
        {
            foreach (Type type in new[] { typeof(DamageRecordPolicyVerification), typeof(RemovalMissionProfileVerification) })
            {
                ElementDefinition[] originals = Kinds.Select(LegacyElementDefinitions.Get).ToArray();
                Exception error = Error(() => Invoke(type, "ConnectionChecks"));
                Connections.Add(RowText("fixture-call", type.FullName + ".ConnectionChecks", error == null ? "no error" : error.ToString()));
                Check(error is InvalidOperationException && error.Message.Contains(originals[0].Id.Value), "실제 fixture Build→Validate→Supply 누락 오류 " + type.Name);
                for (int i = 0; i < Kinds.Length; i++) Check(ReferenceEquals(LegacyElementDefinitions.Get(Kinds[i]), originals[i]), "기존 fixture finally 정확한 복원 " + type.Name + Kinds[i]);
            }
        }
        private static void MissingChecks()
        {
            Dictionary<ElementId, ElementDefinition> definitions = Definitions();
            for (int index = 0; index < Kinds.Length; index++)
            {
                ElementDefinition original = LegacyElementDefinitions.Get(Kinds[index]);
                LevelDefinition level = Fixture(index, 1, 1, true);
                try
                {
                    string input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state);
                    Dictionary<int, string> otherSummaries = new Dictionary<int, string>();
                    foreach (int kind in new[] { -1, 0, 2, 4, 8, 9, 99 })
                    {
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + kind + ",\"count\":1}]}", level);
                        otherSummaries[kind] = Summary(LevelMissionRules.Supply(level, level.Missions[0]));
                    }
                    JsonUtility.FromJsonOverwrite(input, level);
                    definitions[original.Id] = new ElementDefinition(original.Id, original.DisplayName, original.Placement, original.ChargePlacement, original.DamageSourcePolicy, original.ColorMatchPolicy, original.DamageAggregationPolicy, null, original.ReactionBehavior);
                    Exception supply = Error(() => LevelMissionRules.Supply(level, level.Missions[0]));
                    Exception validate = Error(() => LevelDefinitionValidator.Validate(level));
                    Exception build = Error(() => LevelStateBuilder.Build(level, 12345));
                    foreach (Exception error in new[] { supply, validate, build }) Check(error is InvalidOperationException && error.Message.Contains(original.Id.Value), "유효 누락 ID 실제 수량/검증/구성 오류 " + Kinds[index]);
                    Connections.Add(RowText("missing-profile", original.Id.Value, "supply=" + supply.Message + ";validate=" + validate.Message + ";build=" + build.Message));
                    foreach (int kind in new[] { -1, 0, 2, 4, 8, 9, 99 })
                    {
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + kind + ",\"count\":1}]}", level);
                        MissionSupplySummary actual = LevelMissionRules.Supply(level, level.Missions[0]);
                        Connections.Add(RowText("missing-nonremoval", original.Id.Value + "/kind=" + kind, Summary(actual)));
                        Check(Summary(actual) == otherSummaries[kind], "비제거/미지원 미션 필수 조회 없이 전체 요약 유지 " + Kinds[index] + kind);
                    }
                    JsonUtility.FromJsonOverwrite(input, level);
                    Check(input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state), "누락 조회 입력/난수 무변경");
                    typeof(LevelDefinition).GetField("obstacles", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(level, null);
                    Check(LevelMissionRules.Supply(level, level.Missions[0]).Initial == 0, "누락 등록에도 null 목록0 " + Kinds[index]);
                }
                finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(Kinds[index]), original), "finally 누락 등록 참조 복원 " + Kinds[index]); }
            }
        }
        private static LevelDefinition SupplyFixture(int mode)
        {
            string path = Evidence + "/supply-input-" + mode + ".json";
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            if (File.Exists(path)) { JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level); return level; }
            LevelDefinition source = Fixture(0, 1, 1, true);
            try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), level); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            string items = "[{\"kind\":6,\"count\":3,\"durability\":1},{\"kind\":6,\"count\":4,\"durability\":5},{\"kind\":7,\"count\":2},{\"kind\":7,\"count\":1}]";
            if (mode == 6) items = "[{\"kind\":6,\"count\":2147483647,\"durability\":1},{\"kind\":6,\"count\":2147483647,\"durability\":5}]";
            if (mode == 7) items = "[{\"kind\":6,\"count\":-1,\"durability\":1},{\"kind\":6,\"count\":10,\"durability\":0},{\"kind\":6,\"count\":2,\"durability\":1},{\"kind\":7,\"count\":0}]";
            int sourceMode = mode == 1 || mode >= 6 ? 1 : mode == 2 || mode == 4 ? 2 : mode == 3 || mode == 5 ? 3 : 0;
            string sources = "[{\"coordinate\":{\"row\":0,\"column\":1},\"mode\":" + sourceMode + ",\"items\":" + (sourceMode == 1 ? items : "[]") + "}";
            if (sourceMode == 2 || sourceMode == 3) sources += ",{\"coordinate\":{\"row\":0,\"column\":2},\"mode\":" + sourceMode + ",\"items\":[]}";
            sources += "]";
            JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[{\"coordinate\":{\"row\":0,\"column\":0},\"kind\":1,\"fixedColor\":0}],\"covers\":[{\"coordinate\":{\"row\":1,\"column\":3},\"kind\":0,\"durability\":1},{\"coordinate\":{\"row\":2,\"column\":3},\"kind\":1,\"durability\":1}],\"dust\":[{\"coordinate\":{\"row\":3,\"column\":3},\"durability\":1}],\"recoveryParts\":[{\"row\":7,\"column\":7}],\"flow\":{\"arrivals\":[{\"row\":8,\"column\":7}]},\"supply\":{\"sources\":" + sources + ",\"scrapTarget\":" + (mode == 4 ? 0 : 2) + ",\"scrapLimit\":" + (mode == 4 ? -2 : 6) + ",\"scrapDurability\":1,\"recoveryTarget\":" + (mode == 5 ? 0 : 1) + "}}", level);
            File.WriteAllText(path, JsonUtility.ToJson(level)); return level;
        }
        private static void SupplyChecks()
        {
            // 입력은 유효/오류 공급 모두 포함한다. 공급 오류도 전체 목록 그대로 비교한다.
            for (int mode = 0; mode < 8; mode++)
            {
                LevelDefinition level = SupplyFixture(mode);
                try
                {
                    foreach (int kind in new[] { -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 99 })
                        foreach (int goal in new[] { 0, 1, 9, int.MaxValue })
                        {
                            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + kind + ",\"color\":0,\"count\":" + goal + "}]}", level);
                            string input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state);
                            MissionSupplySummary actual = LevelMissionRules.Supply(level, level.Missions[0]);
                            long initial = kind == 0 || kind == 1 || kind == 2 || kind == 3 || kind == 4 || kind == 8 || kind == 9 ? 1 : 0;
                            long fixedCount = kind == 3 ? mode == 1 ? 7 : mode == 6 ? 4294967294L : mode == 7 ? 2 : 0 : kind == 9 && mode == 1 ? 3 : 0;
                            bool goalBased = kind == 9 && mode == 3, dynamic = kind == 0 || kind == 8;
                            long maintained = kind == 3 && mode == 2 ? 6 : goalBased ? Math.Max(0, (long)goal - initial - fixedCount) : 0;
                            MissionSupplySummary expected = new MissionSupplySummary(initial, fixedCount, maintained, dynamic, goalBased);
                            Check(Summary(actual) == Summary(expected), "고정/유지/동적/비제거 전체 요약 " + mode + "/" + kind + "/" + goal);
                            List<LevelValidationIssue> missions = new List<LevelValidationIssue>(); LevelMissionRules.Validate(level, missions);
                            List<LevelValidationIssue> all = LevelDefinitionValidator.Validate(level);
                            LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                            Check(Snapshot(all) == Snapshot(built.Issues) && built.IsBuilt == (all.Count == 0), "공급 전체 오류/실행 구성 일치");
                            Check(input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state), "공급 조회/구성 입력/전역 난수 보존");
                            Values.Add(RowText("supply-summary", mode + "/kind=" + kind + "/goal=" + goal, "input=" + input + ";summary=" + Summary(actual) + ";missions=" + Snapshot(missions) + ";all=" + Snapshot(all) + ";built=" + Snapshot(built)));
                        }
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
        }
        private static void BoundaryChecks()
        {
            foreach (int kind in new[] { -1, 5, 6, 99 })
            {
                LevelDefinition level = Fixture(0, 1, 1, true);
                try
                {
                    JsonUtility.FromJsonOverwrite("{\"obstacles\":[{\"id\":\"boundary-body\",\"coordinate\":{\"row\":4,\"column\":4},\"kind\":" + kind + ",\"durability\":1}]}", level);
                    foreach (MissionKind mission in Missions)
                    {
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)mission + ",\"count\":1}]}", level);
                        MissionSupplySummary actual = LevelMissionRules.Supply(level, level.Missions[0]);
                        Check(actual.Initial == 0, "발전기/미지원 장애물 필수 조회 없이0 " + kind + "/" + mission);
                        Values.Add(RowText("unsupported-obstacle", JsonUtility.ToJson(level), Summary(actual)));
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
            LevelDefinition empty = Fixture(0, 1, 1, true);
            try
            {
                typeof(LevelDefinition).GetField("obstacles", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(empty, null);
                foreach (int kind in new[] { -1, 1, 3, 5, 6, 7, 99 })
                {
                    JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + kind + ",\"count\":1}]}", empty);
                    MissionSupplySummary actual = LevelMissionRules.Supply(empty, empty.Missions[0]);
                    Check(actual.Initial == 0, "null 장애물 목록 기존0 " + kind);
                    Values.Add(RowText("null-obstacles", kind.ToString(), Summary(actual)));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(empty); }
        }
    }
}

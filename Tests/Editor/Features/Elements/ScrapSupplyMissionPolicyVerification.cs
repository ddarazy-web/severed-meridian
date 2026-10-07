using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    /// <summary>고철 공급의 미션 대상과 실제 검증·런타임 구성을 검사한다.</summary>
    public static class ScrapSupplyMissionPolicyVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage36";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static readonly List<string> Connections = new List<string>();
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        [Serializable] private sealed class Row { public string name, input, output; }
        private static void Record(string name, string input, string output) => Connections.Add(JsonUtility.ToJson(new Row {name = name, input = input, output = output}));
        private static void NormalChecks()
        {
            Type previous = typeof(ElementDurabilityApplyPolicyVerification);
            foreach (string field in new[] {"Results", "Values", "Connections"})
                ((List<string>)previous.GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).Clear();
            Invoke(previous, "NormalChecks");
            Results.AddRange((List<string>)previous.GetField("Results", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
            Values.AddRange((List<string>)previous.GetField("Values", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
        }
        private static Exception Error(Action action)
        {
            try { action(); return null; }
            catch (Exception error) { return error; }
        }
        private static void PolicyCase(int mode, bool missing)
        {
            var definitions = (Dictionary<ElementId, ElementDefinition>)Invoke(typeof(CapsuleMagnetPolicyVerification), "Definitions");
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Scrap);
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(Evidence + "/supply-input-1.json"), level);
            try
            {
                string fixedSource = "{\"coordinate\":{\"row\":0,\"column\":1},\"mode\":1,\"items\":[{\"kind\":6,\"count\":3,\"durability\":1},{\"kind\":6,\"count\":4,\"durability\":5},{\"kind\":7,\"count\":2}]}";
                string maintainedSource = "{\"coordinate\":{\"row\":0,\"column\":2},\"mode\":2,\"items\":[]}";
                string mixedSource = "{\"coordinate\":{\"row\":0,\"column\":1},\"mode\":1,\"items\":[{\"kind\":7,\"count\":2},{\"kind\":2,\"count\":1,\"direction\":0}]}";
                string sources = mode == 0 ? fixedSource : mode == 1 ? maintainedSource : mixedSource + "," + maintainedSource;
                JsonUtility.FromJsonOverwrite("{\"covers\":[],\"supply\":{\"sources\":[" + sources + "],\"scrapTarget\":" + (mode == 0 ? 0 : 2) + ",\"scrapLimit\":" + (mode == 0 ? 0 : 6) + ",\"scrapDurability\":1,\"recoveryTarget\":0}}", level);
                Record("fixture-validation", JsonUtility.ToJson(level), Snapshot(LevelDefinitionValidator.Validate(level)));
                Check(LevelDefinitionValidator.Validate(level).Count == 0 && LevelStateBuilder.Build(level, 12345).IsBuilt, "원래 정의 유효 공급/런타임 구성 " + mode + missing);
                LevelRuntimeState state = LevelStateBuilder.Build(level, 12345).State;
                string stateBefore = Snapshot(state), rules = Snapshot(state.Random);
                if (missing) JsonUtility.FromJsonOverwrite("{\"obstacles\":[],\"missions\":[{\"kind\":1,\"count\":1}]}", level);
                else JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":1,\"count\":3}]}", level);
                string input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state);
                bool dirty = EditorUtility.IsDirty(level);
                definitions[original.Id] = new ElementDefinition(original.Id, original.DisplayName, original.Placement, original.ChargePlacement, original.DamageSourcePolicy, original.ColorMatchPolicy, original.DamageAggregationPolicy, missing ? null : new ElementRemovalMissionProfile(MissionKind.Crate), original.ReactionBehavior);
                if (missing)
                {
                    Exception supply = Error(() => LevelMissionRules.Supply(level, level.Missions[0]));
                    Exception validate = Error(() => LevelDefinitionValidator.Validate(level));
                    Exception build = Error(() => LevelStateBuilder.Build(level, 12345));
                    Record("missing-supply-profile", input, "supply=" + supply + ";validate=" + validate + ";build=" + build);
                    Check(new[] {supply, validate, build}.All(error => error is InvalidOperationException && error.Message.Contains(original.Id.Value)), "필요한 공급 프로필 누락 공개 수량/검증/구성 ID 오류 " + mode);
                }
                else
                {
                    MissionSupplySummary actual = LevelMissionRules.Supply(level, level.Missions[0]);
                    JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":3,\"count\":1}]}", level);
                    MissionSupplySummary oldTarget = LevelMissionRules.Supply(level, level.Missions[0]);
                    JsonUtility.FromJsonOverwrite(input, level);
                    List<LevelValidationIssue> issues = new List<LevelValidationIssue>();
                    LevelMissionRules.Validate(level, issues);
                    List<LevelValidationIssue> all = LevelDefinitionValidator.Validate(level);
                    LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                    long expectedFixed = mode == 0 ? 7 : 0, expectedMaintained = mode == 0 ? 0 : 6;
                    Record("same-id-supply", input, "summary=" + Snapshot(actual) + ";oldTarget=" + Snapshot(oldTarget) + ";missions=" + Snapshot(issues) + ";all=" + Snapshot(all) + ";built=" + Snapshot(built));
                    Check(actual.Initial == 2 && actual.Fixed == expectedFixed && actual.Maintained == expectedMaintained && actual.Maximum == 2 + expectedFixed + expectedMaintained && !actual.Dynamic && !actual.GoalBased && oldTarget.Maximum == 0 && issues.Count == 0 && all.Count == 0 && built.IsBuilt, "같은 ID 실제 Fixed/Maintained/Maximum 및 Validate/Build 연결 " + mode + " actual=" + actual);
                }
                Check(input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state) && stateBefore == Snapshot(state) && rules == Snapshot(state.Random) && dirty == EditorUtility.IsDirty(level), "원본/상태/규칙·전역 난수/dirty 무변경 " + mode + missing);
            }
            finally
            {
                definitions[original.Id] = original;
                UnityEngine.Object.DestroyImmediate(level);
                Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Scrap), original), "finally 원래 정의 정확한 참조 복원");
            }
        }
        private static void PolicyChecks(bool red)
        {
            foreach (bool missing in new[] {false, true})
                for (int mode = 0; mode < 3; mode++)
                    try { PolicyCase(mode, missing); }
                    catch (Exception error) { if (!red) throw; Results.Add("FAIL " + mode + missing + " " + error); Debug.LogException(error); }
        }
        private static void BoundaryChecks()
        {
            var definitions = (Dictionary<ElementId, ElementDefinition>)Invoke(typeof(CapsuleMagnetPolicyVerification), "Definitions");
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Scrap);
            string fixedItems = "{\"sources\":[{\"mode\":1,\"items\":[{\"kind\":6,\"count\":0,\"durability\":1},{\"kind\":6,\"count\":3,\"durability\":0},{\"kind\":7,\"count\":2}]}]}";
            string[] supplies = {"null", "{\"sources\":null}", "{\"sources\":[]}", fixedItems,
                "{\"sources\":[{\"mode\":1,\"items\":null}]}",
                "{\"sources\":[{\"mode\":2,\"items\":[]}],\"scrapTarget\":0,\"scrapLimit\":6}",
                "{\"sources\":[{\"mode\":2,\"items\":[]}],\"scrapTarget\":2,\"scrapLimit\":0}",
                "{\"sources\":[{\"mode\":2,\"items\":[]}],\"scrapTarget\":2,\"scrapLimit\":-1}"};
            foreach (string supply in supplies)
                foreach (int kind in new[] {-1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 99})
                {
                    LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(Evidence + "/supply-input-1.json"), level);
                    try
                    {
                        JsonUtility.FromJsonOverwrite("{\"obstacles\":[],\"missions\":[{\"kind\":" + kind + ",\"count\":1}],\"supply\":" + supply + "}", level);
                        if (supply == "null") typeof(LevelDefinition).GetField("supply", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(level, null);
                        else if (supply.Contains("\"sources\":null")) typeof(LevelSupplyDefinition).GetField("sources", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(level.Supply, null);
                        string input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state);
                        string expected = Snapshot(LevelMissionRules.Supply(level, level.Missions[0]));
                        definitions[original.Id] = new ElementDefinition(original.Id, original.DisplayName, original.Placement, original.ChargePlacement, original.DamageSourcePolicy, original.ColorMatchPolicy, original.DamageAggregationPolicy, null, original.ReactionBehavior);
                        Check(expected == Snapshot(LevelMissionRules.Supply(level, level.Missions[0])), "공급 없음/무효·0 수량/비관련 미션 누락 프로필 조회 없음 " + kind + supply);
                        Check(input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state), "경계 원본/전역 난수 무변경");
                    }
                    finally
                    {
                        definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level);
                        Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Scrap), original), "경계 finally 정확한 참조 복원");
                    }
                }
        }
        private static void PlacementOrderChecks()
        {
            var definitions = (Dictionary<ElementId, ElementDefinition>)Invoke(typeof(CapsuleMagnetPolicyVerification), "Definitions");
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Scrap);
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(Evidence + "/supply-input-1.json"), level);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"obstacles\":[],\"missions\":[{\"kind\":1,\"count\":1}]}", level);
                string input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state);
                definitions[original.Id] = new ElementDefinition(original.Id, original.DisplayName, null, original.ChargePlacement, original.DamageSourcePolicy, original.ColorMatchPolicy, original.DamageAggregationPolicy, original.RemovalMissionProfile, original.ReactionBehavior);
                Exception unrelated = Error(() => LevelMissionRules.Supply(level, level.Missions[0]));
                Record("unrelated-placement-order", input, "error=" + unrelated);
                Check(unrelated == null, "기본 고철 미션과 무관한 Crate 조회는 고철 배치 누락 오류를 새로 만들지 않음");
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":3,\"count\":1}]}", level);
                definitions[original.Id] = new ElementDefinition(original.Id, original.DisplayName, null, original.ChargePlacement, original.DamageSourcePolicy, original.ColorMatchPolicy, original.DamageAggregationPolicy, null, original.ReactionBehavior);
                Exception needed = Error(() => LevelMissionRules.Supply(level, level.Missions[0]));
                Check(needed is InvalidOperationException && needed.Message.Contains(original.Id.Value) && needed.Message.Contains("배치 프로필"), "고철 유효 항목은 제거 미션 조회보다 기존 배치 누락 오류가 먼저 발생");
                JsonUtility.FromJsonOverwrite(input, level);
                Check(input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state), "조회 오류 순서 원본/난수 무변경");
            }
            finally
            {
                definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level);
                Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Scrap), original), "배치 오류 finally 정확한 참조 복원");
            }
        }
        public static void Before() => Execute("before");
        public static void Red() => Execute("red");
        public static void Run() => Execute("after");
        public static void Quick() => Execute("quick");
        private static void Execute(string mode)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear(); Connections.Clear(); int exit = 0;
            try
            {
                BoundaryChecks();
                if (mode != "before" && mode != "red") PlacementOrderChecks();
                if (mode == "red" || mode == "quick") PolicyChecks(mode == "red");
                else
                {
                    NormalChecks();
                    if (mode == "before") File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values);
                    else
                    {
                        Check(RecordedLogicComparison.Equal(File.ReadAllLines(Evidence + "/baseline-values.jsonl"), Values), "기존 논리/입력/팩 비교 · 추가 비행 표시 이력 별도");
                        PolicyChecks(false);
                    }
                    File.WriteAllText(Evidence + "/definitions-" + mode + ".json", Snapshot(Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>().Select(LegacyElementDefinitions.Get).ToArray()));
                }
                if (Results.Any(result => result.StartsWith("FAIL "))) exit = 1;
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
    }
}

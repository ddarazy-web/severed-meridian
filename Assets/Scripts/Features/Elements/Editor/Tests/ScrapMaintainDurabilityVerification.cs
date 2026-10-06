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
    /// <summary>유지 공급 내구도의 공개 검증과 배치 정의 연결을 검사한다.</summary>
    public static class ScrapMaintainDurabilityVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage37";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static readonly List<string> Connections = new List<string>();
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        [Serializable] private sealed class Row { public string name, input, output; }
        private static void Record(string name, string input, string output) => Connections.Add(JsonUtility.ToJson(new Row {name = name, input = input, output = output}));
        private static Exception Error(Action action) { try { action(); return null; } catch (Exception error) { return error; } }
        private static ElementDefinition Replace(ElementDefinition original, ElementPlacementProfile placement) =>
            new ElementDefinition(original.Id, original.DisplayName, placement, original.ChargePlacement, original.DamageSourcePolicy,
                original.ColorMatchPolicy, original.DamageAggregationPolicy, original.RemovalMissionProfile, original.ReactionBehavior);
        private static List<LevelValidationIssue> Supply(LevelDefinition level) { List<LevelValidationIssue> issues = new List<LevelValidationIssue>(); LevelSupplyRules.Validate(level, issues); return issues; }
        private static LevelDefinition Fixture()
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(Evidence + "/supply-input-1.json"), level);
            JsonUtility.FromJsonOverwrite("{\"covers\":[],\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":2},\"mode\":2,\"items\":[]}],\"scrapTarget\":2,\"scrapLimit\":6,\"scrapDurability\":1,\"recoveryTarget\":0}}", level);
            return level;
        }
        private static void NormalChecks()
        {
            Type previous = typeof(ScrapSupplyMissionPolicyVerification);
            foreach (string field in new[] {"Results", "Values", "Connections"})
                ((List<string>)previous.GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).Clear();
            Invoke(previous, "NormalChecks");
            Results.AddRange((List<string>)previous.GetField("Results", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
            Values.AddRange((List<string>)previous.GetField("Values", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
        }
        private static void PolicyCase(int maximum, int durability, bool missing, bool unused = false)
        {
            var definitions = (Dictionary<ElementId, ElementDefinition>)Invoke(typeof(CapsuleMagnetPolicyVerification), "Definitions");
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Scrap);
            LevelDefinition level = Fixture();
            try
            {
                Check(Supply(level).Count == 0 && LevelDefinitionValidator.Validate(level).Count == 0 && LevelStateBuilder.Build(level, 12345).IsBuilt, "유효 기반 입력 " + maximum + missing);
                LevelRuntimeState state = LevelStateBuilder.Build(level, 12345).State;
                string before = Snapshot(state), rules = Snapshot(state.Random);
                if (unused) JsonUtility.FromJsonOverwrite("{\"supply\":{\"sources\":[],\"scrapTarget\":0,\"scrapLimit\":0}}", level);
                Check(LevelDefinitionValidator.Validate(level).Count == 0 && LevelStateBuilder.Build(level, 12345).IsBuilt, "활성/미사용 설정의 유효 기반 입력");
                if (missing) JsonUtility.FromJsonOverwrite("{\"obstacles\":[]}", level);
                JsonUtility.FromJsonOverwrite("{\"supply\":{\"scrapDurability\":" + durability + "}}", level);
                string input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state);
                bool dirty = EditorUtility.IsDirty(level);
                definitions[original.Id] = Replace(original, missing ? null : new ElementPlacementProfile(original.Placement.Size, maximum));
                Check(ReferenceEquals(definitions[original.Id].DamageSourcePolicy, original.DamageSourcePolicy) &&
                    ReferenceEquals(definitions[original.Id].RemovalMissionProfile, original.RemovalMissionProfile) &&
                    definitions[original.Id].DisplayName == original.DisplayName &&
                    (missing || definitions[original.Id].Placement.Size == original.Placement.Size), "같은ID/이름/크기/기존 프로필 보존");
                if (missing)
                {
                    Exception supply = Error(() => Supply(level)), all = Error(() => LevelDefinitionValidator.Validate(level)), build = Error(() => LevelStateBuilder.Build(level, 12345));
                    Record("missing-placement", input, "supply=" + supply + ";all=" + all + ";build=" + build);
                    Check(new[] {supply, all, build}.All(error => error is InvalidOperationException && error.Message.Contains(original.Id.Value) && error.Message.Contains("배치 프로필")), "필요한 상한 누락 공개 검증/전체/Build ID 오류");
                }
                else
                {
                    List<LevelValidationIssue> supply = Supply(level), all = LevelDefinitionValidator.Validate(level);
                    LevelStateBuildResult build = LevelStateBuilder.Build(level, 12345);
                    bool allowed = durability <= maximum;
                    Record("same-id-maintain", input, Snapshot(new {maximum, durability, unused, supply, all, build}));
                    Check((supply.Count == 0) == allowed, "공개 유지 내구도 상한 " + maximum + "/" + durability);
                    Check((all.Count == 0) == allowed && build.IsBuilt == allowed, "전체 검증/Build 상한 소비");
                    if (!allowed) Check(supply.Count == 1 && supply[0].Code == LevelValidationCode.InvalidSupply && supply[0].PropertyPath == "supply.scrapTarget" && supply[0].Message == "고철 유지 목표·추가 한도·내구도와 담당 생성구를 확인하세요.", "기존 오류 코드/경로/문구");
                }
                Check(before == Snapshot(state) && rules == Snapshot(state.Random) && input == JsonUtility.ToJson(level) &&
                    random == JsonUtility.ToJson(UnityEngine.Random.state) && dirty == EditorUtility.IsDirty(level), "상태/원본/규칙·전역 난수/dirty 무변경");
            }
            finally
            {
                definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level);
                Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Scrap), original), "finally 정확한 원래 참조 복원");
            }
        }
        private static void PolicyChecks(bool red)
        {
            foreach (bool missing in new[] {false, true})
                foreach (int maximum in new[] {2, 7})
                    try { PolicyCase(maximum, maximum == 2 ? 3 : 6, missing); }
                    catch (Exception error) { if (!red) throw; Results.Add("FAIL " + maximum + missing + " " + error); Debug.LogException(error); }
        }
        private static void UnusedPolicyChecks(bool red)
        {
            foreach (int maximum in new[] {2, 7})
                try { PolicyCase(maximum, maximum == 2 ? 3 : 6, false, true); }
                catch (Exception error) { if (!red) throw; Results.Add("FAIL unused " + maximum + " " + error); Debug.LogException(error); }
        }
        private static void BoundaryChecks()
        {
            var definitions = (Dictionary<ElementId, ElementDefinition>)Invoke(typeof(CapsuleMagnetPolicyVerification), "Definitions");
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Scrap);
            string[] patches = {
                "{\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":2},\"mode\":1,\"items\":null}],\"scrapTarget\":0,\"scrapLimit\":0}}",
                "{\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":2},\"mode\":99,\"exhaustion\":99,\"items\":[{\"kind\":99,\"count\":0}]}],\"scrapTarget\":-1,\"recoveryTarget\":-1}}",
                "{\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":2},\"mode\":1,\"items\":[{\"kind\":6,\"count\":0}]},{\"coordinate\":{\"row\":0,\"column\":2},\"mode\":2,\"items\":[]}],\"recoveryTarget\":-1}}",
                "{\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":2},\"mode\":3,\"items\":[]}],\"scrapTarget\":0,\"scrapLimit\":0,\"recoveryTarget\":1}}",
                "{\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":2},\"mode\":2,\"items\":[{\"kind\":6,\"count\":1,\"durability\":0}]}],\"scrapTarget\":-1,\"scrapDurability\":6,\"recoveryTarget\":-1}}",
                "{\"supply\":null}", "{\"supply\":{\"sources\":null}}", "{\"recoveryParts\":null}",
                "{\"supply\":{\"sources\":[],\"scrapTarget\":0,\"scrapLimit\":0}}",
                "{\"supply\":{\"scrapDurability\":0}}", "{\"supply\":{\"scrapDurability\":-1}}",
                "{\"supply\":{\"scrapTarget\":-1,\"scrapDurability\":6}}", "{\"supply\":{\"scrapLimit\":-1,\"scrapDurability\":6}}",
                "{\"supply\":{\"scrapTarget\":0}}", "{\"supply\":{\"scrapLimit\":0}}",
                "{\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":2},\"mode\":1,\"items\":[{\"kind\":2,\"count\":1,\"direction\":0}]}],\"scrapTarget\":0,\"scrapLimit\":0}}",
                "{\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":2},\"mode\":1,\"items\":[{\"kind\":6,\"count\":0,\"durability\":6}]}],\"scrapTarget\":0,\"scrapLimit\":0}}"
            };
            foreach (string patch in patches)
            {
                LevelDefinition level = Fixture();
                try
                {
                    JsonUtility.FromJsonOverwrite("{\"obstacles\":[]}", level);
                    JsonUtility.FromJsonOverwrite(patch, level);
                    if (patch == "{\"supply\":null}") typeof(LevelDefinition).GetField("supply", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(level, null);
                    if (patch.Contains("\"sources\":null")) typeof(LevelSupplyDefinition).GetField("sources", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(level.Supply, null);
                    if (patch.Contains("\"items\":null"))
                    {
                        List<SupplySourceDefinition> sources = (List<SupplySourceDefinition>)typeof(LevelSupplyDefinition).GetField("sources", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(level.Supply);
                        object source = sources[0];
                        typeof(SupplySourceDefinition).GetField("items", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(source, null);
                        sources[0] = (SupplySourceDefinition)source;
                        Check(level.Supply.Sources[0].Items == null, "실제 null 고정 목록");
                    }
                    if (patch.Contains("\"recoveryParts\":null")) typeof(LevelDefinition).GetField("recoveryParts", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(level, null);
                    string input = JsonUtility.ToJson(level), random = JsonUtility.ToJson(UnityEngine.Random.state), expected = Snapshot(Supply(level));
                    string expectedAll = Snapshot(LevelDefinitionValidator.Validate(level)), expectedBuild = Snapshot(LevelStateBuilder.Build(level, 12345));
                    definitions[original.Id] = Replace(original, null);
                    Check(expected == Snapshot(Supply(level)), "기본/미사용/null/하한/단락 경계 " + patch);
                    Check(expectedAll == Snapshot(LevelDefinitionValidator.Validate(level)) && expectedBuild == Snapshot(LevelStateBuilder.Build(level, 12345)), "전체 검증/Build 불필요 조회 없음");
                    Check(input == JsonUtility.ToJson(level) && random == JsonUtility.ToJson(UnityEngine.Random.state), "경계 원본/전역 난수 무변경");
                    Record("boundary", input, expected);
                }
                finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); Check(ReferenceEquals(LegacyElementDefinitions.Get(ObstacleKind.Scrap), original), "경계 참조 복원"); }
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
                if (mode == "red" || mode == "quick") { PolicyChecks(mode == "red"); UnusedPolicyChecks(mode == "red"); }
                else
                {
                    NormalChecks();
                    if (mode == "before") File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values);
                    else { Check(RecordedLogicComparison.Equal(File.ReadAllLines(Evidence + "/baseline-values.jsonl"), Values), "기존 논리/입력/팩 비교 · 추가 비행 표시 이력 별도"); PolicyChecks(false); UnusedPolicyChecks(false); }
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





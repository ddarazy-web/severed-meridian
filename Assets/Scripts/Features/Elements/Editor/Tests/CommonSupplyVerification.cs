using System;
using System.Collections.Generic;
using System.Collections;
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
    /// <summary>실제 공급 선택·생성·낙하를 동일 입력으로 비교한다.</summary>
    public static class CommonSupplyVerification
    {
        private const string Evidence = "Logs/ElementFramework/Phase01/Supply";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static object Invoke(Type type, string name, params object[] args) =>
            type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static void Check(bool pass, string name) => Results.Add((pass ? "PASS " : "FAIL ") + name);
        public static void Before() => Execute(false);
        public static void Run() => Execute(true);
        private static void Execute(bool connected)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear();
            try
            {
                List<string> results = (List<string>)typeof(SettlementVerification).GetField("Results", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                results.Clear(); Invoke(typeof(SettlementVerification), "DataChecks"); Results.AddRange(results);
                Capture(connected);
                if (!connected) File.WriteAllLines(Evidence + "/baseline-values.txt", Values);
                else
                {
                    Check(File.ReadAllLines(Evidence + "/baseline-values.txt").SequenceEqual(Values), "공급9종 실제 상태·기록·난수 전후 동일");
                    MethodInfo get = typeof(LegacyElementDefinitions).GetMethod("GetSupply", new[] { typeof(SupplyKind) });
                    Check(get != null, "실제 공급 생성 정의 조회 존재");
                    if (get != null)
                        foreach (SupplyKind kind in Enum.GetValues(typeof(SupplyKind)))
                        {
                            ElementDefinition definition = (ElementDefinition)get.Invoke(null, new object[] { kind });
                            Check(typeof(ElementDefinition).GetProperty("Supply")?.GetValue(definition) != null, "공급 생성 행동 정의 " + kind);
                        }
                    RecipeChecks();
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); }
            string mode = connected ? "after" : "before";
            File.WriteAllLines(Evidence + "/" + mode + "-results.txt", Results);
            File.WriteAllLines(Evidence + "/" + mode + "-values.txt", Values);
            EditorApplication.Exit(Results.Any(value => value.StartsWith("FAIL ")) ? 1 : 0);
        }
        private static void Capture(bool connected)
        {
            BoardCoordinate[] column = Enumerable.Range(0, BoardDefinition.DefaultRows).Select(row => new BoardCoordinate(row, 0)).ToArray();
            foreach (SupplyKind kind in Enum.GetValues(typeof(SupplyKind)))
            {
                string path = Evidence + "/input-" + kind + ".json";
                LevelDefinition level;
                if (!connected)
                {
                    level = (LevelDefinition)Invoke(typeof(SettlementVerification), "Make", column);
                    Invoke(typeof(SettlementVerification), "Source", level, column[0], SupplyExhaustion.Stop,
                        new[] { new SupplyItem(kind, column.Length, durability: 2, direction: RocketDirection.Vertical) });
                    File.WriteAllText(path, JsonUtility.ToJson(level));
                }
                else
                {
                    level = ScriptableObject.CreateInstance<LevelDefinition>(); JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level);
                }
                try
                {
                    foreach (int seed in new[] { 1, 42, 12345 })
                    {
                        LevelStateBuildResult built = LevelStateBuilder.Build(level, seed);
                        Check(built.IsBuilt, "고정 공급 입력 검증 " + kind + seed);
                        if (!built.IsBuilt) throw new InvalidOperationException(string.Join("|", built.Issues));
                        Invoke(typeof(SettlementVerification), "Empty", built.State, column);
                        string before = Snapshot(built.State);
                        SettlementResult settled = SettlementResolution.Resolve(built.State);
                        Check(settled.IsApplied && Snapshot(built.State) == before, "실제 공급/원본 무변경 " + kind + seed);
                        Values.Add(kind + ":" + seed + ":" + Snapshot(settled.State) + ":" + Snapshot(settled.Records));
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
        }
        private static void RecipeChecks()
        {
            ElementDefinition original = LegacyElementDefinitions.GetSupply(SupplyKind.Rocket);
            ElementCatalog catalog = (ElementCatalog)typeof(LegacyElementDefinitions).GetField("Catalog", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            IDictionary definitions = (IDictionary)typeof(ElementCatalog).GetField("definitions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(catalog);
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(Evidence + "/input-Rocket.json"), level);
                BoardCoordinate[] column = Enumerable.Range(0, BoardDefinition.DefaultRows).Select(row => new BoardCoordinate(row, 0)).ToArray();
                definitions[original.Id] = ElementDefinition.CreateSupply(original.Id, original.DisplayName,
                    new ElementSupplyProfile(ElementSupplyBehavior.Power, RuntimeContent.Bomb));
                LevelRuntimeState state = LevelStateBuilder.Build(level, 12345).State;
                Invoke(typeof(SettlementVerification), "Empty", state, column);
                SettlementResult supplied = SettlementResolution.Resolve(state);
                Check(supplied.IsApplied && column.All(coordinate => supplied.State.CellAt(coordinate).Content == RuntimeContent.Bomb &&
                    !supplied.State.CellAt(coordinate).RocketDirection.HasValue), "공급 정의 생성 종류 실제 보드/낙하 적용");
                definitions[original.Id] = new ElementDefinition(original.Id, original.DisplayName);
                string before = Snapshot(state);
                Exception failure = null;
                try { SettlementResolution.Resolve(state); } catch (InvalidOperationException error) { failure = error; }
                Check(failure != null && failure.Message.Contains(original.Id.Value) && Snapshot(state) == before,
                    "필요한 공급 프로필 누락 ID 거절/원본 무변경");
            }
            finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}

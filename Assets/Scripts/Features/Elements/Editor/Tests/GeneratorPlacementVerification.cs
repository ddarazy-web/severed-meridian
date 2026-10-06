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
    /// <summary>같은 저장 메모리 입력으로 발전기 배치·충전·연결 결과를 비교한다.</summary>
    public static class GeneratorPlacementVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage17";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static void Check(bool pass, string name)
        { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static object Invoke(Type type, string method, params object[] args) =>
            type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
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
        private static void Reject(Action operation, Type expected, string input, string id = null)
        {
            Exception found = null;
            try { operation(); }
            catch (Exception error) { found = error is TargetInvocationException ? error.InnerException : error; }
            Check(found != null && found.GetType() == expected && (id == null || found.Message.Contains(id)), "명시적 오류 " + input);
            Record("reject", input, found.GetType().Name + ": " + found.Message);
        }
        public static void Before() => Execute(true);
        public static void Run() => Execute(false);
        private static void Execute(bool before)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear(); int exit = 0;
            try
            {
                PlacementChecks(before); RuntimeChecks(before);
                if (before) File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values);
                else { Check(RecordedLogicComparison.Equal(File.ReadAllLines(Evidence + "/baseline-values.jsonl"), Values), "기존 논리/입력/팩 비교 · 추가 비행 표시 이력 별도"); ProfileChecks(); }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                string mode = before ? "before" : "after";
                File.WriteAllLines(Evidence + "/" + mode + "-results.txt", Results); File.WriteAllLines(Evidence + "/" + mode + "-values.jsonl", Values);
            }
            EditorApplication.Exit(exit);
        }
        private static void PlacementChecks(bool before)
        {
            int[] sizes = { 1, 1, 1, 1, 2, 2 }, maxima = { 6, 5, 5, 3, 9, 0 };
            foreach (int number in Enumerable.Range(0, 6).Concat(new[] { -1, 6, 999, int.MinValue, int.MaxValue }))
            {
                ObstacleKind kind = (ObstacleKind)number;
                Check(LevelPlacementRules.Size(kind) == (number >= 0 && number < 6 ? sizes[number] : 1) && LevelPlacementRules.MaxDurability(kind) == (number >= 0 && number < 6 ? maxima[number] : 0), "기존 수치 의미 " + number);
                Record("numeric", number.ToString(), LevelPlacementRules.Size(kind) + "/" + LevelPlacementRules.MaxDurability(kind));
            }
            BoardCoordinate origin = new BoardCoordinate(4, 4);
            for (int charge = 2; charge <= 6; charge++)
            {
                LevelDefinition level = Input("charge-" + charge, before, item => LevelObstacleEditing.Apply(item, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, LevelPlacementRules.Footprint(origin, 2)));
                try
                {
                    PlacementBrush brush = new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Generator, RequiredCharge = charge };
                    string error = LevelObstacleEditing.PlacementError(level, brush, origin);
                    PlacementEditResult edit = LevelObstacleEditing.Apply(level, brush, new[] { origin });
                    Check((error == null) == (charge >= 3 && charge <= 5) && edit.Changed == (charge >= 3 && charge <= 5 ? 1 : 0), "실제 충전량 편집 " + charge);
                    Record("charge", charge.ToString(), "error=" + error + ";changed=" + edit.Changed + ";skipped=" + edit.Skipped);
                    if (level.Obstacles.Count == 0) LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Generator, RequiredCharge = 3 }, new[] { origin });
                    using (SerializedObject data = new SerializedObject(level))
                    { data.FindProperty("obstacles.Array.data[0].requiredCharge").intValue = charge; data.ApplyModifiedPropertiesWithoutUndo(); }
                    LevelValidationIssue[] issues = LevelDefinitionValidator.Validate(level).Where(issue => issue.Code == LevelValidationCode.InvalidPlacementValue).ToArray();
                    Check(issues.Length == (charge >= 3 && charge <= 5 ? 0 : 1), "실제 레벨 충전량 검사 " + charge);
                    Record("charge-validation", charge.ToString(), string.Join("|", issues.Select(issue => issue.Message)));
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
            foreach (BoardCoordinate cell in new[] { new BoardCoordinate(0, 0), new BoardCoordinate(7, 7), new BoardCoordinate(8, 8), new BoardCoordinate(9, 8), new BoardCoordinate(-1, 0) })
            {
                LevelDefinition level = Input("boundary-" + cell.Row + "-" + cell.Column, before, item => LevelObstacleEditing.Apply(item, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, LevelPlacementRules.Footprint(cell, 2)));
                try
                {
                    PlacementBrush brush = new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Generator, RequiredCharge = 3 };
                    string error = LevelObstacleEditing.PlacementError(level, brush, cell);
                    PlacementEditResult edit = LevelObstacleEditing.Apply(level, brush, new[] { cell });
                    bool fits = cell.Row >= 0 && cell.Column >= 0 && cell.Row + 2 <= 9 && cell.Column + 2 <= 9;
                    Check((error == null) == fits && edit.Changed == (fits ? 1 : 0), "실제 발전기2×2 경계 " + cell);
                    Record("boundary", cell.ToString(), "error=" + error + ";changed=" + edit.Changed);
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
            LevelDefinition wall = Input("wall", before, item =>
            {
                LevelObstacleEditing.Apply(item, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, LevelPlacementRules.Footprint(origin, 2));
                Check(LevelFlowEditing.SetWalls(item, new[] { new BoardEdge(origin, new BoardCoordinate(4, 5)) }, false) == null, "실제 내부 벽 준비");
            });
            try
            {
                PlacementBrush brush = new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Generator, RequiredCharge = 3 };
                string error = LevelObstacleEditing.PlacementError(wall, brush, origin);
                Check(error != null && error.Contains("내부") && LevelObstacleEditing.Apply(wall, brush, new[] { origin }).Changed == 0, "발전기 내부 벽 거절");
                Record("wall", origin.ToString(), error);
            }
            finally { UnityEngine.Object.DestroyImmediate(wall); }
            LevelDefinition duplicate = Input("duplicate", before, item =>
            {
                LevelDefinition fixture = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.Appliance, 3);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture), item); UnityEngine.Object.DestroyImmediate(fixture);
            });
            try
            {
                string json = JsonUtility.ToJson(duplicate);
                int start = json.IndexOf("\"obstacles\":[", StringComparison.Ordinal) + "\"obstacles\":[".Length;
                string body = JsonUtility.ToJson(duplicate.Obstacles[0]).Replace(duplicate.Obstacles[0].Id, "duplicate-generator");
                JsonUtility.FromJsonOverwrite(json.Insert(start, body + ","), duplicate);
                foreach (BoardCoordinate cell in LevelPlacementRules.Footprint(origin, 2)) Check(LevelPlacementRules.Find(duplicate, PlacementLayer.Obstacle, cell) == -2, "발전기 중복 점유 " + cell);
                PlacementBrush brush = new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Generator, RequiredCharge = 3 };
                string error = LevelObstacleEditing.PlacementError(duplicate, brush, origin);
                Check(error != null && LevelObstacleEditing.Apply(duplicate, brush, new[] { origin }).Changed == 0 && LevelDefinitionValidator.Validate(duplicate).Count > 0, "중복 발전기 실제 편집/검사 거절");
                Record("duplicate", JsonUtility.ToJson(duplicate), "error=" + error + ";issues=" + Snapshot(LevelDefinitionValidator.Validate(duplicate)));
            }
            finally { UnityEngine.Object.DestroyImmediate(duplicate); }
        }
        private static void RuntimeChecks(bool before)
        {
            foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance })
                for (int required = 3; required <= 5; required++)
                {
                    LevelDefinition level = Input("runtime-" + kind + "-" + required, before, item =>
                    {
                        LevelDefinition fixture = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", kind, required);
                        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture), item); UnityEngine.Object.DestroyImmediate(fixture);
                    });
                    try
                    {
                        string original = JsonUtility.ToJson(level), global = JsonUtility.ToJson(UnityEngine.Random.state); byte[] packed = LevelPackCodec.Encode(new[] { level });
                        LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345); Check(built.IsBuilt && GeneratorRules.ActiveConnections(built.State).Count == 1, "실제 연결 런타임 " + kind + required);
                        string initial = Snapshot(built.State); int draws = built.State.Random.DrawCount; object context = Invoke(typeof(FixedObstacleVerification), "Context");
                        for (int turn = 1; turn <= required; turn++)
                        {
                            context = Invoke(typeof(GeneratorVerification), "Next", context, turn);
                            object effects = Invoke(typeof(FixedObstacleVerification), "Hit", built.State, new BoardCoordinate(4, 4), context);
                            Check(built.State.Obstacles[0].Charge == turn && built.State.Missions[0].Progress == (turn == required ? 1 : 0), "실제 충전/제거 미션 " + kind + required + "/" + turn);
                            Record("charge-runtime", kind + "/" + required + "/" + turn, Snapshot(built.State) + ";effects=" + Snapshot(effects) + ";context=" + Snapshot(context));
                        }
                        Check(!built.State.Cells.Any(cell => cell.ObstacleIndex.HasValue) && GeneratorRules.ActiveConnections(built.State).Count == 0, "완충 전체 제거/연결 해제 " + kind + required);
                        Check(built.State.Random.DrawCount == draws && JsonUtility.ToJson(UnityEngine.Random.state) == global && JsonUtility.ToJson(level) == original && packed.SequenceEqual(LevelPackCodec.Encode(new[] { level })), "충전 난수/원본/배치 ID/바이트 보존 " + kind + required);
                        Record("runtime", original, "before=" + initial + ";after=" + Snapshot(built.State) + ";pack=" + Convert.ToBase64String(packed));
                        LevelRuntimeState direct = LevelStateBuilder.Build(level, 12345).State; object directContext = Invoke(typeof(FixedObstacleVerification), "Context");
                        for (int hit = 1; hit <= LevelPlacementRules.MaxDurability(kind); hit++)
                        {
                            directContext = Invoke(typeof(GeneratorVerification), "Next", directContext, hit);
                            Invoke(typeof(FixedObstacleVerification), "Hit", direct, new BoardCoordinate(4, 7), directContext);
                        }
                        Check(direct.Obstacles[0].Charge == 0 && !direct.Cells.Any(cell => cell.ObstacleIndex.HasValue) && direct.Missions[0].Progress == 1 && GeneratorRules.ActiveConnections(direct).Count == 0, "대상 직접 파괴/발전기 무충전 철거 " + kind + required);
                        Record("direct-target", kind + "/" + required, Snapshot(direct) + ";context=" + Snapshot(directContext));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
            Check(LevelPackCodec.LegacyFormatVersion == 1 && LevelPackCodec.LevelsPerPack == 50, "기존 팩 버전1/50구간");
        }
        private static void ProfileChecks()
        {
            Type profileType = typeof(ElementId).Assembly.GetType("Elements.ElementChargePlacementProfile");
            Check(profileType != null, "불변 충전 배치 프로필 존재");
            Check(profileType.IsSealed && profileType.GetProperties().All(property => property.SetMethod == null) && profileType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).All(field => field.IsInitOnly && field.FieldType == typeof(int)), "충전 프로필 불변 정수 계약");
            MethodInfo require = typeof(ElementDefinition).GetMethod("RequireChargePlacement"); Check(require != null, "필수 충전 프로필 조회");
            ElementDefinition generator = LegacyElementDefinitions.Get(ObstacleKind.Generator); object real = require.Invoke(generator, null);
            Check(generator.Id == new ElementId("obstacle.generator") && ReferenceEquals(generator, LegacyElementDefinitions.Get(ObstacleKind.Generator)) && (int)profileType.GetProperty("Size").GetValue(real) == 2 && (int)profileType.GetProperty("MinRequiredCharge").GetValue(real) == 3 && (int)profileType.GetProperty("MaxRequiredCharge").GetValue(real) == 5, "한 번 준비한 발전기2/3~5");
            foreach (int[] numbers in new[] { new[] { 2, 3, 5 }, new[] { 1, 2, 8 }, new[] { 3, 4, 4 } })
            {
                object profile = Activator.CreateInstance(profileType, new object[] { numbers[0], numbers[1], numbers[2] }); ElementId id = new ElementId("element.charge-fixture");
                ElementDefinition definition = (ElementDefinition)Activator.CreateInstance(typeof(ElementDefinition), new object[] { id, "충전 수치", null, profile });
                object found = require.Invoke(new ElementCatalog(new[] { definition }).Get(id), null);
                Check(ReferenceEquals(profile, found) && (int)profileType.GetProperty("Size").GetValue(found) == numbers[0] && (int)profileType.GetProperty("MinRequiredCharge").GetValue(found) == numbers[1] && (int)profileType.GetProperty("MaxRequiredCharge").GetValue(found) == numbers[2], "같은 카탈로그의 충전 수치 조회 " + string.Join("/", numbers));
                Record("profile", string.Join("/", numbers), Snapshot(found));
            }
            ElementId missing = new ElementId("element.no-charge"); ElementDefinition metadata = new ElementDefinition(missing, "메타데이터");
            Reject(() => require.Invoke(metadata, null), typeof(InvalidOperationException), "충전 프로필 누락", missing.Value);
            Reject(() => new ElementCatalog(Array.Empty<ElementDefinition>()).Get(missing), typeof(KeyNotFoundException), "정의 누락", missing.Value);
            foreach (int[] numbers in new[] { new[] { 0, 3, 5 }, new[] { -1, 3, 5 }, new[] { 2, 0, 5 }, new[] { 2, -1, 5 }, new[] { 2, 5, 4 } })
                Reject(() => Activator.CreateInstance(profileType, new object[] { numbers[0], numbers[1], numbers[2] }), typeof(ArgumentOutOfRangeException), "무효 충전 수치 " + string.Join("/", numbers));
        }
    }
}

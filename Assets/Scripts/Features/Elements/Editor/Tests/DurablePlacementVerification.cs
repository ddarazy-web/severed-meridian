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
    /// <summary>기존 입력을 저장하여 内구도형 배치 조회 전환의 실제 전후 결과를 비교한다.</summary>
    public static class DurablePlacementVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage16";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static readonly ObstacleKind[] Kinds = { ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance };
        private static readonly int[] Sizes = { 1, 1, 1, 2 }, Maxima = { 5, 5, 3, 9 };
        private static void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static object Invoke(Type type, string method, params object[] args) =>
            type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        [Serializable] private sealed class Row { public string name, input, output; }
        private static void Record(string name, string input, string output) =>
            Values.Add(JsonUtility.ToJson(new Row { name = name, input = input, output = output }));
        private static LevelDefinition Input(string name, bool before, Action<LevelDefinition> prepare)
        {
            string path = Evidence + "/input-" + name + ".json";
            LevelDefinition level = before ? (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make") : ScriptableObject.CreateInstance<LevelDefinition>();
            if (before) { prepare(level); File.WriteAllText(path, JsonUtility.ToJson(level)); }
            else JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level);
            return level;
        }
        public static void Before() => Execute(true);
        public static void Run() => Execute(false);
        private static void Execute(bool before)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear(); int exit = 0;
            try
            {
                PlacementChecks(before);
                RuntimeChecks(before);
                if (before) File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values);
                else
                {
                    Check(File.ReadAllLines(Evidence + "/baseline-values.jsonl").SequenceEqual(Values), "배치/오류/피해/미션/난수/바이트 전후 기록 동일");
                    foreach (ObstacleKind kind in Kinds)
                    {
                        ElementDefinition definition = LegacyElementDefinitions.Get(kind);
                        ElementPlacementProfile profile = definition.RequirePlacement();
                        Check(definition.Id == LegacyElementMap.Get(kind) && ReferenceEquals(definition, LegacyElementDefinitions.Get(kind)), "한 번 준비한 기존 ID 정의 " + kind);
                        Check(profile.Size == LevelPlacementRules.Size(kind) && profile.MaxDurability == LevelPlacementRules.MaxDurability(kind), "같은 정의 수치 조회 " + kind);
                        Record("definition", definition.Id.Value, "size=" + profile.Size + ";max=" + profile.MaxDurability);
                    }
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                string mode = before ? "before" : "after";
                File.WriteAllLines(Evidence + "/" + mode + "-results.txt", Results);
                File.WriteAllLines(Evidence + "/" + mode + "-values.jsonl", Values);
            }
            EditorApplication.Exit(exit);
        }
        private static void PlacementChecks(bool before)
        {
            int[] allSizes = { 1, 1, 1, 1, 2, 2 }, allMaxima = { 6, 5, 5, 3, 9, 0 };
            foreach (int number in Enumerable.Range(0, 6).Concat(new[] { -1, 6, 999, int.MinValue, int.MaxValue }))
            {
                ObstacleKind kind = (ObstacleKind)number;
                Check(LevelPlacementRules.Size(kind) == (number >= 0 && number < 6 ? allSizes[number] : 1) && LevelPlacementRules.MaxDurability(kind) == (number >= 0 && number < 6 ? allMaxima[number] : 0), "기존 종류/미지원 수치 " + number);
                Record("numeric", number.ToString(), LevelPlacementRules.Size(kind) + "/" + LevelPlacementRules.MaxDurability(kind));
            }
            for (int i = 0; i < Kinds.Length; i++)
            {
                ObstacleKind kind = Kinds[i]; int size = Sizes[i], maximum = Maxima[i];
                BoardCoordinate corner = new BoardCoordinate(9 - size, 9 - size);
                for (int durability = 0; durability <= maximum + 1; durability++)
                {
                    LevelDefinition level = Input("durability-" + kind + "-" + durability, before, item => LevelObstacleEditing.Apply(item, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, LevelPlacementRules.Footprint(corner, size)));
                    try
                    {
                        PlacementBrush brush = new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)kind, Durability = durability, Color = RabbitColor.Type1 };
                        string error = LevelObstacleEditing.PlacementError(level, brush, corner);
                        PlacementEditResult edit = LevelObstacleEditing.Apply(level, brush, new[] { corner });
                        bool allowed = durability >= 1 && durability <= maximum;
                        Check((error == null) == allowed && edit.Changed == (allowed ? 1 : 0) && edit.Skipped == (allowed ? 0 : 1), "실제 내구도 편집 " + kind + durability);
                        Check(!allowed || LevelDefinitionValidator.Validate(level).Count == 0, "허용 배치 레벨 검사 " + kind + durability);
                        Record("durability", kind + "/" + durability + "/" + corner, "error=" + error + ";changed=" + edit.Changed + ";skipped=" + edit.Skipped + ";values=" + string.Join(",", level.Obstacles.Select(body => body.Durability)));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
                foreach (BoardCoordinate origin in new[] { corner, new BoardCoordinate(8, 8), new BoardCoordinate(9, 8), new BoardCoordinate(-1, 0) }.Distinct())
                {
                    LevelDefinition level = Input("boundary-" + kind + "-" + origin.Row + "-" + origin.Column, before, item => LevelObstacleEditing.Apply(item, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, LevelPlacementRules.Footprint(origin, size)));
                    try
                    {
                        PlacementBrush brush = new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)kind, Durability = 1, Color = RabbitColor.Type1 };
                        string error = LevelObstacleEditing.PlacementError(level, brush, origin);
                        PlacementEditResult edit = LevelObstacleEditing.Apply(level, brush, new[] { origin });
                        bool fits = origin.Row >= 0 && origin.Column >= 0 && origin.Row + size <= 9 && origin.Column + size <= 9;
                        Check((error == null) == fits && edit.Changed == (fits ? 1 : 0), "실제 점유 경계 편집 " + kind + origin);
                        Record("boundary", kind + "/" + origin, "error=" + error + ";changed=" + edit.Changed + ";skipped=" + edit.Skipped);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
                LevelDefinition duplicate = Input("duplicate-" + kind, before, item => Invoke(typeof(FixedObstacleVerification), "Obstacle", item, kind, maximum, corner, RabbitColor.Type1));
                try
                {
                    string json = JsonUtility.ToJson(duplicate);
                    int start = json.IndexOf("\"obstacles\":[", StringComparison.Ordinal) + "\"obstacles\":[".Length;
                    int end = json.IndexOf("}]", start, StringComparison.Ordinal) + 1;
                    JsonUtility.FromJsonOverwrite(json.Insert(end, "," + json.Substring(start, end - start).Replace(duplicate.Obstacles[0].Id, "duplicate-instance")), duplicate);
                    PlacementBrush brush = new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)kind, Durability = 1, Color = RabbitColor.Type1 };
                    foreach (BoardCoordinate cell in LevelPlacementRules.Footprint(corner, size)) Check(LevelPlacementRules.Find(duplicate, PlacementLayer.Obstacle, cell) == -2, "중복 점유 칸 " + kind + cell);
                    string error = LevelObstacleEditing.PlacementError(duplicate, brush, corner);
                    PlacementEditResult edit = LevelObstacleEditing.Apply(duplicate, brush, new[] { corner });
                    Check(error != null && edit.Changed == 0 && LevelDefinitionValidator.Validate(duplicate).Count > 0, "실제 중복 본체 편집/레벨 검사 거절 " + kind);
                    Record("duplicate", kind.ToString(), "error=" + error + ";changed=" + edit.Changed + ";issues=" + Snapshot(LevelDefinitionValidator.Validate(duplicate)));
                }
                finally { UnityEngine.Object.DestroyImmediate(duplicate); }
            }
            SpecialPlacementChecks(before);
        }
        private static void SpecialPlacementChecks(bool before)
        {
            BoardCoordinate origin = new BoardCoordinate(4, 4);
            LevelDefinition wall = Input("wall", before, item =>
            {
                LevelObstacleEditing.Apply(item, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, LevelPlacementRules.Footprint(origin, 2));
                Check(LevelFlowEditing.SetWalls(item, new[] { new BoardEdge(origin, new BoardCoordinate(4, 5)) }, false) == null, "실제 내부 벽 준비");
            });
            try
            {
                PlacementBrush brush = new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Appliance, Durability = 9 };
                string error = LevelObstacleEditing.PlacementError(wall, brush, origin);
                PlacementEditResult edit = LevelObstacleEditing.Apply(wall, brush, new[] { origin });
                Check(error != null && error.Contains("내부") && edit.Changed == 0, "2×2 내부 벽 실제 편집 거절");
                Record("internal-wall", origin.ToString(), "error=" + error + ";changed=" + edit.Changed);
            }
            finally { UnityEngine.Object.DestroyImmediate(wall); }
            LevelDefinition color = Input("color", before, item =>
            {
                LevelObstacleEditing.Apply(item, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { origin });
                JsonUtility.FromJsonOverwrite("{\"colors\":[0,1,2,3]}", item);
            });
            try
            {
                foreach (RabbitColor input in new[] { RabbitColor.Type1, (RabbitColor)4, (RabbitColor)(-1) })
                {
                    PlacementBrush brush = new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.ColorLock, Durability = 3, Color = input };
                    string error = LevelObstacleEditing.PlacementError(color, brush, origin);
                    PlacementEditResult edit = LevelObstacleEditing.Apply(color, brush, new[] { origin });
                    Check((error == null) == (input == RabbitColor.Type1) && edit.Changed == (input == RabbitColor.Type1 ? 1 : 0), "자물쇠 실제 색 조건 " + (int)input);
                    Record("color", ((int)input).ToString(), "error=" + error + ";changed=" + edit.Changed);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(color); }
            for (int charge = 2; charge <= 6; charge++)
            {
                LevelDefinition level = Input("charge-" + charge, before, item => LevelObstacleEditing.Apply(item, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, LevelPlacementRules.Footprint(origin, 2)));
                try
                {
                    PlacementBrush brush = new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Generator, RequiredCharge = charge };
                    string error = LevelObstacleEditing.PlacementError(level, brush, origin);
                    PlacementEditResult edit = LevelObstacleEditing.Apply(level, brush, new[] { origin });
                    Check((error == null) == (charge >= 3 && charge <= 5) && edit.Changed == (charge >= 3 && charge <= 5 ? 1 : 0), "발전기 충전 편집 의미 " + charge);
                    Record("charge", charge.ToString(), "error=" + error + ";changed=" + edit.Changed);
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
            LevelDefinition supply = Input("supply", before, item => { });
            try
            {
                for (int durability = 0; durability <= 6; durability++)
                {
                    string error = LevelSupplyRules.ItemError(supply, new SupplyItem(SupplyKind.Scrap, durability: durability));
                    Check((error == null) == (durability >= 1 && durability <= 5), "고철 공급 항목 내구도 " + durability);
                    Record("supply", durability.ToString(), error);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(supply); }
        }
        private static void RuntimeChecks(bool before)
        {
            for (int i = 0; i < Kinds.Length; i++)
                for (int durability = 1; durability <= Maxima[i]; durability++)
                {
                    ObstacleKind kind = Kinds[i]; BoardCoordinate target = new BoardCoordinate(4, 4);
                    LevelDefinition level = Input("runtime-" + kind + "-" + durability, before, item =>
                    {
                        Invoke(typeof(FixedObstacleVerification), "Obstacle", item, kind, durability, target, RabbitColor.Type1);
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)Enum.Parse(typeof(MissionKind), kind.ToString()) + ",\"count\":1}]}", item);
                    });
                    try
                    {
                        string original = JsonUtility.ToJson(level), global = JsonUtility.ToJson(UnityEngine.Random.state);
                        byte[] packed = LevelPackCodec.Encode(new[] { level });
                        LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345); Check(built.IsBuilt, "런타임 구성 " + kind + durability);
                        string initial = Snapshot(built.State); int draws = built.State.Random.DrawCount;
                        object context = Invoke(typeof(FixedObstacleVerification), "Context");
                        object effects = Invoke(typeof(FixedObstacleVerification), "Hit", built.State, target, context);
                        Check(built.State.Obstacles[0].Durability == durability - 1 && built.State.Missions[0].Progress == (durability == 1 ? 1 : 0), "실제 피해/제거 미션 " + kind + durability);
                        Check(built.State.Random.DrawCount == draws && JsonUtility.ToJson(UnityEngine.Random.state) == global, "피해 난수 무소비 " + kind + durability);
                        Check(JsonUtility.ToJson(level) == original && packed.SequenceEqual(LevelPackCodec.Encode(new[] { level })), "원본/배치 ID/바이트 유지 " + kind + durability);
                        Record("runtime", original, "before=" + initial + ";after=" + Snapshot(built.State) + ";effects=" + Snapshot(effects) + ";pack=" + Convert.ToBase64String(packed));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
            LevelDefinition connected = Input("connected", before, item =>
            {
                LevelDefinition fixture = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.Appliance, 3);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture), item); UnityEngine.Object.DestroyImmediate(fixture);
            });
            try
            {
                string original = JsonUtility.ToJson(connected); byte[] packed = LevelPackCodec.Encode(new[] { connected });
                LevelStateBuildResult built = LevelStateBuilder.Build(connected, 12345);
                Check(built.IsBuilt && GeneratorRules.ActiveConnections(built.State).Count == 1 && connected.Obstacles.Select(body => body.Id).Distinct().Count() == 2, "발전기 본체 ID/활성 연결 유지");
                Check(LevelPackCodec.FormatVersion == 1 && LevelPackCodec.LevelsPerPack == 50 && packed.SequenceEqual(LevelPackCodec.Encode(new[] { connected })), "연결 팩 바이트/버전1/50구간 유지");
                Record("connected", original, Snapshot(built.State) + ";pack=" + Convert.ToBase64String(packed));
            }
            finally { UnityEngine.Object.DestroyImmediate(connected); }
        }
    }
}

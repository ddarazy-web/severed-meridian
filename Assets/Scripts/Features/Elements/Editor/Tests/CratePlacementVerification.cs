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
    /// <summary>전환 전 입력을 재사용해 편집·실행·저장 결과를 비교한다. 원본 에셋은 저장하지 않는다.</summary>
    public static class CratePlacementVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage15";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static void Check(bool pass, string name)
        { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static object Invoke(Type type, string method, params object[] args) =>
            type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static LevelDefinition Input(string name, bool before, Action<LevelDefinition> prepare)
        {
            LevelDefinition level = before ? (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make") : ScriptableObject.CreateInstance<LevelDefinition>();
            string path = Evidence + "/input-" + name + ".json";
            if (before) { prepare(level); File.WriteAllText(path, JsonUtility.ToJson(level)); }
            else JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level);
            return level;
        }
        private static void Reject(Action operation, Type expected, string name, string id = null)
        {
            Exception found = null;
            try { operation(); }
            catch (Exception error) { found = error is TargetInvocationException ? error.InnerException : error; }
            Check(found != null && found.GetType() == expected && (id == null || found.Message.Contains(id)), "명시적 거절 " + name);
            Values.Add(JsonUtility.ToJson(new Row { name = name, output = found.GetType().Name + ": " + found.Message }));
        }
        [Serializable] private sealed class Row { public string name, input, output; }
        private static void Record(string name, string input, string output)
        { Values.Add(JsonUtility.ToJson(new Row { name = name, input = input, output = output })); }
        public static void Before() => Execute(true);
        public static void Run() => Execute(false);
        private static void Execute(bool before)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear(); int exit = 0;
            string mode = before ? "before" : "after";
            try
            {
                CapturePlacement(before);
                CaptureRuntime(before);
                if (before) File.WriteAllLines(Evidence + "/baseline-values.jsonl", Values);
                else
                {
                    Check(File.ReadAllLines(Evidence + "/baseline-values.jsonl").SequenceEqual(Values), "실제 전환 전후 배치/실행/바이트 기록 전체 동일");
                    ProfileChecks();
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally { File.WriteAllLines(Evidence + "/" + mode + "-results.txt", Results); File.WriteAllLines(Evidence + "/" + mode + "-values.jsonl", Values); }
            EditorApplication.Exit(exit);
        }
        private static void CapturePlacement(bool before)
        {
            int[] sizes = { 1, 1, 1, 1, 2, 2 }, maxima = { 6, 5, 5, 3, 9, 0 };
            foreach (int number in Enumerable.Range(0, 6).Concat(new[] { -1, 6, 999, int.MinValue, int.MaxValue }))
            {
                ObstacleKind kind = (ObstacleKind)number;
                int size = LevelPlacementRules.Size(kind), maximum = LevelPlacementRules.MaxDurability(kind);
                Check(size == (number >= 0 && number < 6 ? sizes[number] : 1) && maximum == (number >= 0 && number < 6 ? maxima[number] : 0), "기존 크기/최대 내구도 " + number);
                Record("kind", number.ToString(), "size=" + size + ";max=" + maximum);
            }
            BoardCoordinate edge = new BoardCoordinate(8, 8);
            for (int durability = 0; durability <= 7; durability++)
            {
                LevelDefinition level = Input("durability-" + durability, before, item => LevelObstacleEditing.Apply(item, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { edge }));
                try
                {
                    PlacementBrush brush = new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = durability };
                    string valueError = LevelPlacementRules.ObstacleValueError(level, ObstacleKind.Crate, durability, RabbitColor.Type1, 0);
                    string editError = LevelObstacleEditing.PlacementError(level, brush, edge);
                    PlacementEditResult edit = LevelObstacleEditing.Apply(level, brush, new[] { edge });
                    bool allowed = durability >= 1 && durability <= 6;
                    Check((valueError == null) == allowed && (editError == null) == allowed && edit.Changed == (allowed ? 1 : 0) && edit.Skipped == (allowed ? 0 : 1), "실제 경계 칸 내구도 편집 " + durability);
                    Check(!allowed || (level.Obstacles.Count == 1 && level.Obstacles[0].Durability == durability && LevelDefinitionValidator.Validate(level).Count == 0), "허용 배치 검사 " + durability);
                    Record("durability", durability.ToString(), "value=" + valueError + ";edit=" + editError + ";changed=" + edit.Changed + ";skipped=" + edit.Skipped + ";reasons=" + string.Join("|", edit.Reasons.OrderBy(value => value)));
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
            LevelDefinition space = Input("space", before, item => Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.Crate, 3, edge, RabbitColor.Type1));
            try
            {
                foreach (BoardCoordinate cell in new[] { edge, new BoardCoordinate(9, 8), new BoardCoordinate(-1, 8), new BoardCoordinate(0, 0) })
                {
                    string error = LevelPlacementRules.ObstacleSpaceError(space, cell, ObstacleKind.Crate);
                    Check(error != null, "점유/범위 밖 거절 " + cell);
                    Record("space", cell.ToString(), error);
                }
                Check(LevelPlacementRules.ObstacleSpaceError(space, edge, ObstacleKind.Crate, 0) == null, "자기 점유 재편집 허용");
                Record("self", edge.ToString(), LevelPlacementRules.ObstacleSpaceError(space, edge, ObstacleKind.Crate, 0));
                string json = JsonUtility.ToJson(space);
                int start = json.IndexOf("\"obstacles\":[", StringComparison.Ordinal) + "\"obstacles\":[".Length;
                int end = json.IndexOf("}]", start, StringComparison.Ordinal) + 1;
                string body = json.Substring(start, end - start);
                JsonUtility.FromJsonOverwrite(json.Insert(end, "," + body.Replace(space.Obstacles[0].Id, "duplicate-instance")), space);
                Check(LevelPlacementRules.Find(space, PlacementLayer.Obstacle, edge) == -2 && LevelPlacementRules.ObstacleSpaceError(space, edge, ObstacleKind.Crate, 0) != null && LevelDefinitionValidator.Validate(space).Count > 0, "실제 중복 본체 점유/레벨 검사 거절");
                Record("duplicate", edge.ToString(), "find=" + LevelPlacementRules.Find(space, PlacementLayer.Obstacle, edge) + ";error=" + LevelPlacementRules.ObstacleSpaceError(space, edge, ObstacleKind.Crate, 0));
            }
            finally { UnityEngine.Object.DestroyImmediate(space); }
        }
        private static void CaptureRuntime(bool before)
        {
            for (int durability = 1; durability <= 6; durability++)
            {
                BoardCoordinate target = new BoardCoordinate(4, 4);
                LevelDefinition level = Input("runtime-" + durability, before, item =>
                {
                    Invoke(typeof(FixedObstacleVerification), "Obstacle", item, ObstacleKind.Crate, durability, target, RabbitColor.Type1);
                    JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Crate + ",\"count\":1}]}", item);
                });
                try
                {
                    string original = JsonUtility.ToJson(level), global = JsonUtility.ToJson(UnityEngine.Random.state);
                    byte[] packed = LevelPackCodec.Encode(new[] { level });
                    LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                    Check(built.IsBuilt, "기존 상자 런타임 구성 " + durability);
                    string runtimeBefore = Snapshot(built.State); int randomBefore = built.State.Random.DrawCount;
                    object context = Invoke(typeof(FixedObstacleVerification), "Context");
                    object effects = Invoke(typeof(FixedObstacleVerification), "Hit", built.State, target, context);
                    Check(built.State.Obstacles[0].Durability == durability - 1 && built.State.Missions[0].Progress == (durability == 1 ? 1 : 0), "실제 피해1/제거 미션 " + durability);
                    Check(built.State.Random.DrawCount == randomBefore && JsonUtility.ToJson(UnityEngine.Random.state) == global, "규칙/전역 난수 무소비 " + durability);
                    Check(original == JsonUtility.ToJson(level) && packed.SequenceEqual(LevelPackCodec.Encode(new[] { level })) && LevelPackCodec.FormatVersion == 1 && LevelPackCodec.LevelsPerPack == 50, "입력/배치 ID/저장 바이트/버전 유지 " + durability);
                    Record("runtime", original, "before=" + runtimeBefore + ";after=" + Snapshot(built.State) + ";effects=" + Snapshot(effects) + ";pack=" + Convert.ToBase64String(packed));
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
        }
        private static void ProfileChecks()
        {
            Type profileType = typeof(ElementId).Assembly.GetType("Elements.ElementPlacementProfile");
            Check(profileType != null, "불변 배치 프로필 존재");
            Check(profileType.IsSealed && profileType.GetProperties().All(property => property.SetMethod == null) && profileType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).All(field => field.IsInitOnly && field.FieldType == typeof(int)), "프로필 양수 수치 불변 값");
            Type boundaryType = typeof(ElementId).Assembly.GetType("Elements.LegacyElementDefinitions");
            Check(boundaryType != null, "상자 정의 호환 경계 존재");
            MethodInfo lookup = boundaryType.GetMethod("Get");
            ElementDefinition first = (ElementDefinition)lookup.Invoke(null, new object[] { ObstacleKind.Crate });
            Check(ReferenceEquals(first, lookup.Invoke(null, new object[] { ObstacleKind.Crate })) && first.Id == LegacyElementMap.Get(ObstacleKind.Crate), "한 번 준비한 동일 상자 정의 조회");
            MethodInfo require = typeof(ElementDefinition).GetMethod("RequirePlacement");
            Check(require != null, "필수 프로필 조회 API");
            object real = require.Invoke(first, null);
            Check((int)profileType.GetProperty("Size").GetValue(real) == 1 && (int)profileType.GetProperty("MaxDurability").GetValue(real) == 6, "실제 상자 정의1/6");
            foreach (int[] numbers in new[] { new[] { 1, 6 }, new[] { 2, 11 }, new[] { 3, 2 } })
            {
                object profile = Activator.CreateInstance(profileType, new object[] { numbers[0], numbers[1] });
                ElementId id = new ElementId("element.fixture.placement");
                ElementDefinition definition = (ElementDefinition)Activator.CreateInstance(typeof(ElementDefinition), new object[] { id, "수치 검사", profile });
                ElementCatalog catalog = new ElementCatalog(new[] { definition });
                object actual = require.Invoke(catalog.Get(id), null);
                Check(ReferenceEquals(profile, actual) && (int)profileType.GetProperty("Size").GetValue(actual) == numbers[0] && (int)profileType.GetProperty("MaxDurability").GetValue(actual) == numbers[1], "같은 카탈로그/프로필 데이터 조회 " + numbers[0] + "/" + numbers[1]);
                Record("profile", numbers[0] + "/" + numbers[1], Snapshot(actual));
            }
            ElementId absent = new ElementId("element.no-placement");
            ElementDefinition metadata = new ElementDefinition(absent, "메타데이터만");
            Check(metadata.Id == absent && metadata.DisplayName == "메타데이터만", "기존2인자 계약 유지");
            Reject(() => require.Invoke(metadata, null), typeof(InvalidOperationException), "프로필 누락", absent.Value);
            Reject(() => new ElementCatalog(Array.Empty<ElementDefinition>()).Get(absent), typeof(KeyNotFoundException), "정의 누락", absent.Value);
            Reject(() => new ElementCatalog(Array.Empty<ElementDefinition>()).Get(new ElementId("element.unregistered.fixture")), typeof(KeyNotFoundException), "미등록 정의 누락", "element.unregistered.fixture");
            foreach (int[] numbers in new[] { new[] { 0, 6 }, new[] { -1, 6 }, new[] { 1, 0 }, new[] { 1, -1 } })
                Reject(() => Activator.CreateInstance(profileType, new object[] { numbers[0], numbers[1] }), typeof(ArgumentOutOfRangeException), "무효 수치 " + numbers[0] + "/" + numbers[1]);
        }
    }
}

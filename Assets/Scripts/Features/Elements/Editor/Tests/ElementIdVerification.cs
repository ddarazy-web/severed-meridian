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
    /// <summary>새 정의 ID만 메모리에서 검사하며 원본이나 저장된 팩은 쓰지 않는다.</summary>
    public static class ElementIdVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage13";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static Type idType;
        private static void Check(bool pass, string name)
        { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name.Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n")); }
        private static object Id(string value) => Activator.CreateInstance(idType, new object[] { value });
        private static string Value(object id) => (string)idType.GetProperty("Value").GetValue(id);
        private static void Reject(Action action, Type expected, string input)
        {
            Exception found = null;
            try { action(); }
            catch (Exception error) { found = error is TargetInvocationException ? error.InnerException : error; }
            Check(found != null && found.GetType() == expected, "명시적 거절 " + input);
            Values.Add(JsonUtility.ToJson(new Entry { name = "reject", input = input, error = found.GetType().Name + ": " + found.Message }));
        }
        private static string Snapshot(object value) => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { value });
        [Serializable] private sealed class Entry { public string name, input, output, error; }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); int exit = 0;
            LevelDefinition level = null, changed = null;
            try
            {
                idType = typeof(ObstacleKind).Assembly.GetType("Elements.ElementId");
                Check(idType != null, "ElementId 계약 존재");
                Type mapType = idType.Assembly.GetType("Elements.LegacyElementMap");
                Check(mapType != null, "LegacyElementMap 계약 존재");
                MethodInfo map = mapType.GetMethod("Get", new[] { typeof(ObstacleKind) });
                Check(map != null && map.IsStatic && map.ReturnType == idType, "명시적 장애물 매핑 API");
                Func<ObstacleKind, object> get = kind => map.Invoke(null, new object[] { kind });
                object id = Id("obstacle.scrap"), copy = Id(new string("obstacle.scrap".ToCharArray()));
                Check(Value(id) == "obstacle.scrap" && id.ToString() == "obstacle.scrap", "원문 값/문자열 보존");
                Check(id.Equals(copy) && (bool)idType.GetMethod("Equals", new[] { idType }).Invoke(id, new[] { copy }) && id.GetHashCode() == copy.GetHashCode(), "객체/형식 동등성·동등 해시");
                Values.Add(JsonUtility.ToJson(new Entry { name = "identity-base", input = "obstacle.scrap", output = "Value=" + Value(id) + ";IsValid=true;Hash=" + id.GetHashCode() + ";copyHash=" + copy.GetHashCode() }));
                Check(!id.Equals(null) && !id.Equals("obstacle.scrap"), "다른 형식/null과 다름");
                Check((bool)idType.GetMethod("op_Equality").Invoke(null, new[] { id, copy }) && !(bool)idType.GetMethod("op_Inequality").Invoke(null, new[] { id, copy }), "동등 연산자");
                foreach (string input in new[] { "OBSTACLE.SCRAP", " obstacle.scrap ", "obstacle.scrap.other" })
                {
                    object distinct = Id(input);
                    Check(Value(distinct) == input && !id.Equals(distinct) && !(bool)idType.GetMethod("op_Equality").Invoke(null, new[] { id, distinct }) && (bool)idType.GetMethod("op_Inequality").Invoke(null, new[] { id, distinct }), "정규화 없이 값 구별 " + input);
                    Values.Add(JsonUtility.ToJson(new Entry { name = "identity", input = input, output = Value(distinct) }));
                }
                Check(!Id("element.caf\u00e9").Equals(Id("element.cafe\u0301")), "Ordinal은 유니코드 정규형도 구별");
                Check(new HashSet<object> { id, copy, Id("obstacle.crate.wood") }.Count == 2, "동등 ID 해시 집합 중복 제거");
                Check(idType.IsValueType && idType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).All(field => field.IsInitOnly) && idType.GetProperties().All(property => property.SetMethod == null), "불변 값 형식·쓰기 API 없음");
                foreach (string invalid in new string[] { null, "", " ", "\t\r\n" }) Reject(() => Id(invalid), typeof(ArgumentException), "ID=" + (invalid ?? "<null>"));
                object empty = Activator.CreateInstance(idType);
                Check(!(bool)idType.GetProperty("IsValid").GetValue(empty) && (bool)idType.GetProperty("IsValid").GetValue(id), "default 무효/생성 ID 유효");
                Reject(() => Value(empty), typeof(InvalidOperationException), "default.Value");
                Reject(() => empty.ToString(), typeof(InvalidOperationException), "default.ToString");
                Check(empty.Equals(Activator.CreateInstance(idType)) && !empty.Equals(id) && empty.GetHashCode() == 0, "무효값 비교/해시 안전·유효 ID와 다름");
                Values.Add(JsonUtility.ToJson(new Entry { name = "default", input = "default(ElementId)", output = "IsValid=false;Hash=" + empty.GetHashCode() + ";equalsDefault=true;Value/ToString rejected" }));
                string[] expectedIds = { "obstacle.crate.wood", "obstacle.scrap", "obstacle.recovery-capsule", "obstacle.color-lock", "obstacle.metal-rod-box", "obstacle.generator" };
                ObstacleKind[] kinds = { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance, ObstacleKind.Generator };
                Check(Enum.GetValues(typeof(ObstacleKind)).Length == 6, "기존 enum6종 유지");
                HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < kinds.Length; i++)
                {
                    string actual = Value(get(kinds[i]));
                    Check((int)kinds[i] == i && actual == expectedIds[i] && unique.Add(actual), "정확한 ID/숫자/중복 없음 " + kinds[i]);
                    Values.Add(JsonUtility.ToJson(new Entry { name = "mapping", input = kinds[i] + "=" + (int)kinds[i], output = actual }));
                }
                foreach (int unknown in new[] { -1, 6, 999, int.MinValue, int.MaxValue })
                {
                    Reject(() => get((ObstacleKind)unknown), typeof(ArgumentOutOfRangeException), "ObstacleKind=" + unknown);
                    try { get((ObstacleKind)unknown); }
                    catch (TargetInvocationException error) { Check(error.InnerException.Message.Contains(unknown.ToString()) && ((ArgumentOutOfRangeException)error.InnerException).ActualValue.Equals((ObstacleKind)unknown), "거절에 입력 값 포함 " + unknown); }
                }
                level = (LevelDefinition)typeof(GeneratorVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { ObstacleKind.Crate, 3 });
                level.name = "original-label"; changed = UnityEngine.Object.Instantiate(level); changed.name = "different-label";
                string json = JsonUtility.ToJson(changed);
                for (int i = 0; i < level.Obstacles.Count; i++) json = json.Replace(level.Obstacles[i].Id, "renamed-instance-" + i);
                JsonUtility.FromJsonOverwrite(json, changed);
                using (SerializedObject serialized = new SerializedObject(changed)) { serialized.FindProperty("obstacles").MoveArrayElement(0, 1); serialized.ApplyModifiedPropertiesWithoutUndo(); }
                Check(level.name != changed.name && level.Obstacles[0].Id != changed.Obstacles[0].Id && level.Obstacles[0].Kind != changed.Obstacles[0].Kind, "표시명/본체 Id/배열 순서 실제 차이");
                foreach (LevelDefinition input in new[] { level, changed })
                {
                    LevelStateBuildResult built = LevelStateBuilder.Build(input, 12345); Check(built.IsBuilt, "기존 메모리 입력 구성 " + input.name);
                    string before = Snapshot(built.State), original = JsonUtility.ToJson(input), global = JsonUtility.ToJson(UnityEngine.Random.state);
                    byte[] packed = LevelPackCodec.Encode(new[] { input });
                    string output = string.Join(";", input.Obstacles.OrderBy(body => body.Kind).Select(body => body.Kind + "=" + Value(get(body.Kind))));
                    Check(output == "Crate=obstacle.crate.wood;Generator=obstacle.generator", "인스턴스 이력 독립 매핑 " + input.name);
                    Check(Snapshot(built.State) == before && JsonUtility.ToJson(input) == original && global == JsonUtility.ToJson(UnityEngine.Random.state), "매핑 조회 원본/상태/난수 보존 " + input.name);
                    Check(packed.SequenceEqual(LevelPackCodec.Encode(new[] { input })) && LevelPackCodec.LegacyFormatVersion == 1 && LevelPackCodec.LevelsPerPack == 50, "기존 메모리 팩 바이트/버전/50구간 유지 " + input.name);
                    Check(GeneratorRules.ActiveConnections(built.State).Count == 1 && input.Obstacles.All(body => body.Id != Value(get(body.Kind))), "기존 인스턴스 연결 유지·정의 ID와 분리 " + input.name);
                    Values.Add(JsonUtility.ToJson(new Entry { name = input.name, input = original, output = output + " | runtime=" + before + " | memoryPackBytes=" + packed.Length }));
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                if (changed != null) UnityEngine.Object.DestroyImmediate(changed);
                if (level != null) UnityEngine.Object.DestroyImmediate(level);
                File.WriteAllLines(Evidence + "/id-results.txt", Results); File.WriteAllLines(Evidence + "/id-values.jsonl", Values);
            }
            EditorApplication.Exit(exit);
        }
    }
}

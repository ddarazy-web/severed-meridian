using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Board;
using MemoryPack;
using Simulation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Levels.Editor
{
    public static class LevelStorageBaselineVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage08";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Observations = new List<string>();
        private static readonly List<LevelDefinition> Owned = new List<LevelDefinition>();
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static string Hash(byte[] bytes) { using (SHA256 algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); Results.Add("PASS " + label); }
        private static LevelDefinition New(int number = 1)
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>(); Owned.Add(level);
            JsonUtility.FromJsonOverwrite("{\"levelNumber\":" + number + "}", level); return level;
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Observations.Clear(); Owned.Clear();
            int exit = 0;
            try { Migration(); RoundTrips(); Ranges(); Legacy(); Existing(); }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                foreach (LevelDefinition level in Owned) if (level != null) UnityEngine.Object.DestroyImmediate(level);
                File.WriteAllLines(Evidence + "/data-results.txt", Results);
                File.WriteAllLines(Evidence + "/storage-observations.jsonl", Observations);
            }
            EditorApplication.Exit(exit);
        }

        private static void Migration()
        {
            // 메모리 자르기 검사만 호출한다. 원본/팩을 저장하는 Run은 호출하지 않는다.
            List<string> existing = (List<string>)typeof(BoardNineVerification).GetField("Results", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            existing.Clear(); Invoke(typeof(BoardNineVerification), "Migration"); Results.AddRange(existing);
            Record("migration", null, null, null, "BoardNineVerification.Migration", Snapshot(existing));
            LevelDefinition source = New(); PackedLevel legacy = source.ToPacked();
            string cells = string.Join(",", Enumerable.Range(0, 100).Select(i => "{\"isActive\":" + (i == 18 ? "false" : "true") + "}"));
            string boardJson = "{\"rows\":10,\"columns\":10,\"cells\":[" + cells + "]}";
            legacy.Board = JsonUtility.FromJson<BoardDefinition>(boardJson);
            legacy.RecoveryParts.AddRange(new[] { new BoardCoordinate(8, 1), new BoardCoordinate(9, 1) });
            byte[] bytes = MemoryPackSerializer.Serialize(new LevelPack { FormatVersion = 1, FirstLevel = 1, Levels = new[] { legacy } });
            LevelDefinition cropped = LevelPackCodec.ReadLevel(bytes, 1); Owned.Add(cropped);
            Check(cropped.Board.Cells.Count == 81 && !cropped.Board.Cells[17].IsActive && cropped.RecoveryParts.Count == 1 &&
                cropped.RecoveryParts[0].Equals(new BoardCoordinate(8, 1)), "자르기 실제 값/원본 바이트 기록");
            string before = "{\"schemaVersion\":4,\"board\":" + boardJson + ",\"recoveryParts\":[{\"row\":8,\"column\":1},{\"row\":9,\"column\":1}]}";
            Record("migration-values", cropped, bytes, before, "schema4 complete10x10 decode; source metadata/moves/colors use New defaults", "100->81; inactive18->17; recovery8,1 kept /9,1 removed");
            string snapshot = JsonUtility.ToJson(cropped); cropped.OnAfterDeserialize();
            Check(snapshot == JsonUtility.ToJson(cropped) && legacy.Board.Cells.Count == 100 && legacy.RecoveryParts.Count == 2,
                "자르기 관찰 반복/메모리 입력 보존");
        }

        private static void RoundTrips()
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.Crate, 3); Owned.Add(level);
            BoardCoordinate recovery = new BoardCoordinate(7, 0);
            Invoke(typeof(RecoveryVerification), "Place", level, recovery);
            Check(LevelFlowEditing.SetArrival(level, new BoardCoordinate(8, 0), false) == null, "현재 fixture 출구");
            Check(LevelFlowEditing.SetPath(level, Enumerable.Range(0, 9).Select(r => new BoardCoordinate(r, 0)).ToArray()) == null, "현재 fixture 경로");
            Invoke(typeof(SettlementVerification), "Source", level, new BoardCoordinate(0, 0), SupplyExhaustion.Stop, new[] { new SupplyItem(SupplyKind.Recovery) });
            JsonUtility.FromJsonOverwrite("{\"covers\":[{\"coordinate\":{\"row\":2,\"column\":2},\"kind\":0,\"durability\":2}],\"dust\":[{\"coordinate\":{\"row\":2,\"column\":3},\"durability\":2}],\"missions\":[{\"kind\":" + (int)MissionKind.Crate + ",\"count\":1},{\"kind\":" + (int)MissionKind.Recovery + ",\"count\":1}]}", level);
            Check(level.InitialBlocks.Count > 0 && level.Obstacles.Count > 0 && level.Covers.Count > 0 && level.Dust.Count > 0 &&
                level.Flow.Paths.Count > 0 && level.Connections.Count > 0 && level.Supply.Sources.Count > 0 && level.RecoveryParts.Count > 0 && level.Missions.Count > 0,
                "모든 목록 필드 비어 있지 않은 현재 fixture");
            PackedLevel packed = level.ToPacked();
            Check(ReferenceEquals(packed.Board, level.Board) && ReferenceEquals(packed.Obstacles, level.Obstacles) && ReferenceEquals(packed.Supply, level.Supply), "ToPacked 정의 참조 공유 계약");
            RoundTrip("current-all-fields", level, true);
        }

        private static void RoundTrip(string name, LevelDefinition level, bool requireBuilt)
        {
            string before = JsonUtility.ToJson(level); byte[] bytes = LevelPackCodec.Encode(new[] { level });
            LevelDefinition copy = LevelPackCodec.ReadLevel(bytes, level.LevelNumber); Owned.Add(copy);
            string copiedJson = JsonUtility.ToJson(copy);
            if (level.ElementCatalog != null)
            {
                // 제작 SO 참조는 팩1 값에 포함하지 않는다. 모든 다른 필드는 그대로 비교한다.
                LevelDefinition comparable = LevelPackCodec.Copy(level); Owned.Add(comparable);
                SerializedObject authoring = new SerializedObject(comparable);
                authoring.FindProperty("elementCatalog").objectReferenceValue = null;
                authoring.ApplyModifiedPropertiesWithoutUndo();
                Check(copiedJson == JsonUtility.ToJson(comparable) && JsonUtility.ToJson(level) == before,
                    name + " 제작 참조 제외 전체 JSON 왕복/원본 보존");
            }
            else Check(copiedJson == before && JsonUtility.ToJson(level) == before, name + " 전체 JSON 왕복/원본 보존");
            Check(bytes.SequenceEqual(LevelPackCodec.Encode(new[] { copy })), name + " 재인코딩 바이트 동일");
            LevelStateBuildResult a = LevelStateBuilder.Build(level, 12345), b = LevelStateBuilder.Build(copy, 12345);
            Check(a.IsBuilt == b.IsBuilt && LevelStateBuilder.Fingerprint(level) == LevelStateBuilder.Fingerprint(copy), name + " 같은 시드 결과/fingerprint");
            if (requireBuilt) Check(a.IsBuilt && b.IsBuilt, name + " 유효한 플레이 상태 " + string.Join(" | ", a.Issues));
            if (a.IsBuilt) Check(Snapshot(a.State) == Snapshot(b.State), name + " 전체 초기 상태 동일");
            else Check(Snapshot(a.Issues) == Snapshot(b.Issues), name + " 진단 동일");
            Record(name, level, bytes, before, "roundtrip", Snapshot(a.IsBuilt ? (object)a.State : a.Issues));
        }

        private static void Ranges()
        {
            foreach (int number in new[] { 1, 50, 51, 100, 101 })
            {
                int first = number <= 50 ? 1 : number <= 100 ? 51 : 101;
                Check(LevelPackCodec.FirstLevel(number) == first && LevelPackCodec.Address(number) == "Levels/levels-" + first.ToString("D6"), "50구간/주소 " + number);
                Record("range-" + number, New(number), null, null, "FirstLevel/Address", first + " / " + LevelPackCodec.Address(number));
            }
            LevelDefinition firstLevel = New(), last = New(50), next = New(51);
            byte[] bytes = LevelPackCodec.Encode(new[] { last, firstLevel });
            Check(LevelPackCodec.Decode(bytes).Levels.Select(l => l.LevelNumber).SequenceEqual(new[] { 1, 50 }), "번호 정렬/누락 구간 보존");
            Record("sparse", firstLevel, bytes, JsonUtility.ToJson(firstLevel), "Encode 50,1", "1,50");
            Reject("duplicate", () => LevelPackCodec.Encode(new[] { firstLevel, firstLevel }), firstLevel, null);
            Reject("mixed-range", () => LevelPackCodec.Encode(new[] { last, next }), last, null);
            Reject("missing-level", () => LevelPackCodec.ReadLevel(bytes, 2), firstLevel, bytes);
            Reject("wrong-range", () => LevelPackCodec.ReadLevel(bytes, 51), next, bytes);
            Reject("truncated", () => LevelPackCodec.Decode(bytes.Take(10).ToArray()), firstLevel, bytes.Take(10).ToArray());
            Reject("empty", () => LevelPackCodec.Encode(Array.Empty<LevelDefinition>()), null, null);
            Reject("invalid-number", () => LevelPackCodec.FirstLevel(0), null, null);
            LevelPack wrong = LevelPackCodec.Decode(bytes); wrong.FormatVersion++;
            byte[] invalid = MemoryPackSerializer.Serialize(wrong);
            Reject("unsupported-format-2", () => LevelPackCodec.Decode(invalid), firstLevel, invalid);
            wrong = LevelPackCodec.Decode(bytes); wrong.Levels[0].SchemaVersion = 99;
            invalid = MemoryPackSerializer.Serialize(wrong);
            Reject("unsupported-schema-decode", () => LevelPackCodec.Decode(invalid), null, invalid);
        }

        private static void Legacy()
        {
            foreach (string path in Directory.GetFiles("Tests/Editor/Features/Levels/Fixtures", "*.json").OrderBy(s => s))
            {
                byte[] file = File.ReadAllBytes(path); LevelDefinition level = New(); JsonUtility.FromJsonOverwrite(File.ReadAllText(path), level);
                string before = JsonUtility.ToJson(level);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                Record("legacy-" + Path.GetFileName(path), level, file, before, "raw fixture; no version overwrite", Snapshot(built.Issues));
                Check(!built.IsBuilt && built.Issues.Count > 0, "구형 fixture 플레이 거절/진단 " + Path.GetFileName(path));
                Reject("legacy-pack-" + Path.GetFileName(path), () => LevelPackCodec.Encode(new[] { level }), level, null);
                Check(before == JsonUtility.ToJson(level) && Hash(file) == Hash(File.ReadAllBytes(path)), "구형 fixture 입력/디스크 보존 " + Path.GetFileName(path));
            }
        }

        private static void Existing()
        {
            LevelDefinition[] levels = AssetDatabase.FindAssets("t:LevelDefinition", new[] { "Assets/Data/Levels" }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<LevelDefinition>).OrderBy(l => l.LevelNumber).ToArray();
            Check(levels.Length > 0, "읽기 전용 원본 존재");
            foreach (LevelDefinition level in levels) RoundTrip("asset-" + level.LevelNumber, level, true);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            foreach (string path in Directory.GetFiles("Assets/Data/LevelPacks", "levels-*.bytes").OrderBy(s => s))
            {
                byte[] bytes = File.ReadAllBytes(path);
                bool elements = bytes.Take(4).SequenceEqual(new byte[] { 0x45, 0x46, 0x50, 0x4b });
                int first = elements ? LevelPackCodec.DecodeElements(bytes).FirstLevel : LevelPackCodec.DecodeLegacy(bytes).FirstLevel;
                int[] numbers = elements ? LevelPackCodec.DecodeElements(bytes).Levels.Select(item => item.LevelNumber).ToArray()
                    : LevelPackCodec.DecodeLegacy(bytes).Levels.Select(item => item.LevelNumber).ToArray();
                LevelDefinition[] source = levels.Where(l => LevelPackCodec.FirstLevel(l.LevelNumber) == first).ToArray();
                bool sameBytes = bytes.SequenceEqual(elements ? LevelPackBuild.CreatePackBytes(source).Values.Single() : LevelPackCodec.Encode(source));
                string guid = AssetDatabase.AssetPathToGUID(path);
                var entry = settings == null ? null : settings.FindAssetEntry(guid);
                string address = entry == null ? "<missing>" : entry.address;
                string[] dependencies = AssetDatabase.GetDependencies(path, true);
                List<string> matches = new List<string>();
                foreach (int number in numbers)
                {
                    LevelDefinition loaded = LevelPackCodec.ReadLevel(bytes, number); Owned.Add(loaded);
                    LevelDefinition original = source.SingleOrDefault(l => l.LevelNumber == number);
                    bool same = original != null && JsonUtility.ToJson(loaded) == JsonUtility.ToJson(original);
                    LevelStateBuildResult sourceState = original == null ? null : LevelStateBuilder.Build(original, 12345);
                    LevelStateBuildResult packedState = LevelStateBuilder.Build(loaded, 12345);
                    // 스키마와 저장 지문은 포맷 전환으로 다르다. 실행 값과 다음 난수까지 비교한다.
                    bool logical = sourceState?.IsBuilt == true && packedState.IsBuilt &&
                        Equals(Invoke(typeof(Elements.Editor.ElementPackVerification), "State", sourceState.State),
                            Invoke(typeof(Elements.Editor.ElementPackVerification), "State", packedState.State));
                    Check(logical, "기존 제작 레벨과 디스크 팩의 논리 상태 동일 " + number);
                    matches.Add(number + ":jsonMatch=" + same + ":logicalMatch=" + logical);
                }
                Record("existing-pack-" + first, source.FirstOrDefault(), bytes, null, path,
                    "byteMatch=" + sameBytes + "; " + string.Join(";", matches) + "; address=" + address + "; dependencies=" + string.Join(",", dependencies));
                Check(Hash(bytes) == Hash(File.ReadAllBytes(path)), "기존 팩 디스크 보존 " + first);
                Check(numbers.Length > 0, "기존 팩 디코드/실제 비교 기록 " + first);
                foreach (LevelDefinition original in source)
                {
                    string assetPath = AssetDatabase.GetAssetPath(original);
                    var originalEntry = settings == null ? null : settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(assetPath));
                    Record("addressable-source-" + original.LevelNumber, original, File.ReadAllBytes(assetPath), null, assetPath,
                        "entry=" + (originalEntry == null ? "<excluded>" : originalEntry.address) + "; expectedPack=" + LevelPackCodec.Address(original.LevelNumber));
                }
            }
        }

        private static void Reject(string name, Action action, LevelDefinition level, byte[] bytes)
        {
            string before = level == null ? null : JsonUtility.ToJson(level); Exception rejected = null;
            try { action(); } catch (Exception error) { rejected = error; }
            Check(rejected != null, "거절 " + name);
            if (level != null) Check(JsonUtility.ToJson(level) == before, "거절 입력 보존 " + name);
            Record(name, level, bytes, before, "reject", rejected.GetType().Name + ": " + rejected.Message);
        }

        [Serializable] private sealed class Observation
        {
            public string name, input, beforeJson, afterJson, inputHash, bytesHash, address, detail;
            public int seed, schema, codecFormat, firstLevel, bytesLength;
        }

        private static void Record(string name, LevelDefinition level, byte[] bytes, string before, string input, string detail)
        {
            string json = level == null ? null : JsonUtility.ToJson(level);
            Observations.Add(JsonUtility.ToJson(new Observation
            {
                name = name, input = input, seed = 12345, schema = level == null ? -1 : level.SchemaVersion,
                codecFormat = LevelPackCodec.FormatVersion, firstLevel = level == null || level.LevelNumber < 1 ? -1 : LevelPackCodec.FirstLevel(level.LevelNumber),
                beforeJson = before, afterJson = json, inputHash = json == null ? null : Hash(System.Text.Encoding.UTF8.GetBytes(json)),
                bytesHash = bytes == null ? null : Hash(bytes), bytesLength = bytes == null ? 0 : bytes.Length,
                address = level == null || level.LevelNumber < 1 ? null : LevelPackCodec.Address(level.LevelNumber), detail = detail
            }));
        }
    }
}

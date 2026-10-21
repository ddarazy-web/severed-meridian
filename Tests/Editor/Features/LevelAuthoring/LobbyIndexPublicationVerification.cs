using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelTool;
using Levels;
using UnityEditor;
using UnityEngine;
namespace LevelAuthoring.Editor
{
    public static class LobbyIndexPublicationVerification
    {
        public static void Run()
        {
            string root = Path.GetFullPath("Logs/LobbyStage01/publication-" + Guid.NewGuid().ToString("N"));
            string data = Path.Combine(root, "data"); Directory.CreateDirectory(root);
            int exit = 1;
            try
            {
                var snapshot = new LevelAuthoring.Storage.ContentSnapshotStore(AuthoringSourceSelection.ReadFolder(AuthoringSourceSelection.DefaultConfiguration)).Read().Snapshot;
                var outputs = JsonContentPackBuild.CreateBytes(snapshot);
                const string path = "Assets/Data/LevelPacks/lobby-index.bytes";
                Check(outputs.ContainsKey(path), "Lobby index is missing from atomic publication outputs");
                var generation = ContentPackGenerationCodec.Decode(outputs[JsonContentPackBuild.GenerationPath]);
                ContentPackGenerationCodec.Verify(generation, "Levels/lobby-index", outputs[path]);
                Type codec = typeof(LevelPackCodec).Assembly.GetType("Levels.LevelLobbyIndexCodec");
                Check(codec != null, "Lobby codec missing");
                object index = codec.GetMethod("Decode").Invoke(null, new object[] { outputs[path] });
                Array entries = (Array)index.GetType().GetProperty("Entries").GetValue(index);
                var level = snapshot.Documents.Where(doc => doc.Kind == "level").OrderBy(doc => (int)doc.Data["levelNumber"]).First();
                Check(entries.Length == snapshot.Documents.Count(doc => doc.Kind == "level"), "Wrong entry count"); object row = entries.GetValue(0);
                Check((string)row.GetType().GetProperty("LevelId").GetValue(row) == level.Id, "Stable source ID lost");
                Check((int)row.GetType().GetProperty("LevelNumber").GetValue(row) == (int)level.Data["levelNumber"], "Number lost");
                Check((string)row.GetType().GetProperty("DisplayName").GetValue(row) == (string)level.Data["displayName"], "Name lost");
                LevelLobbyEntry[] decoded = LevelLobbyIndexCodec.Decode(outputs[path]).Entries;
                foreach (var entry in decoded)
                {
                    var info = snapshot.GetDocumentInfo(entry.LevelId);
                    Check(info.Kind == "level" && info.LevelNumber == entry.LevelNumber && info.DisplayName == entry.DisplayName, "Full source mapping mismatch");
                }
                int[] packNumbers = outputs.Where(pair => Path.GetFileName(pair.Key).StartsWith("levels-", StringComparison.Ordinal))
                    .SelectMany(pair => pair.Value[0] == 0x54 ? LevelPackCodec.DecodeTutorial(pair.Value).Tutorials.Select(item => item.LevelNumber)
                        : pair.Value[0] == 0x45 ? LevelPackCodec.DecodeElements(pair.Value).Levels.Select(item => item.LevelNumber)
                        : LevelPackCodec.DecodeLegacy(pair.Value).Levels.Select(item => item.LevelNumber)).OrderBy(number => number).ToArray();
                Check(decoded.Select(entry => entry.LevelNumber).SequenceEqual(packNumbers), "Index/actual pack number set mismatch");
                var removed = new LevelAuthoring.Documents.ContentSnapshot(snapshot.Documents.Where(document => document.Id != decoded[0].LevelId));
                var removedOutputs = JsonContentPackBuild.CreateBytes(removed);
                LevelLobbyEntry[] remaining = LevelLobbyIndexCodec.Decode(removedOutputs[path]).Entries;
                Check(remaining.Length == decoded.Length - 1 && remaining.All(entry => entry.LevelId != decoded[0].LevelId), "Deleted level remains in index");
                var workspace = new AuthoringToolWorkspace(); workspace.Create(Path.Combine(data, "source"), snapshot);
                string target = Path.Combine(data, "outputs");
                JsonPackPublication.Publish(Path.Combine(data, "source"), target, null);
                byte[] original = File.ReadAllBytes(Path.Combine(target, path));
                byte[] originalGeneration = File.ReadAllBytes(Path.Combine(target, JsonContentPackBuild.GenerationPath));
                workspace.Session.EditLevel("rename", body => body["displayName"] = "Changed"); workspace.Save();
                bool rejected = false;
                try { JsonPackPublication.PublishWithRegistration(Path.Combine(data, "source"), target, Array.Empty<string>(), _ =>
                {
                    Check(!File.ReadAllBytes(Path.Combine(target, path)).SequenceEqual(original), "Renamed metadata was not published");
                    throw new IOException("injected registration failure");
                }, null); }
                catch (IOException) { rejected = true; }
                Check(rejected && File.ReadAllBytes(Path.Combine(target, path)).SequenceEqual(original), "Failure did not restore index");
                Check(File.ReadAllBytes(Path.Combine(target, JsonContentPackBuild.GenerationPath)).SequenceEqual(originalGeneration), "Failure did not restore generation");
                workspace.Session.EditLevel("unsupported glyph", body => body["displayName"] = "지원하지 않는 문자 \u0378");
                bool missingGlyphRejected = false;
                try { JsonContentPackBuild.CreateBytes(workspace.Session.CreateSnapshot()); }
                catch (LevelAuthoring.Documents.ContentFormatException error) when (error.Message.Contains("폰트")) { missingGlyphRejected = true; }
                Check(missingGlyphRejected, "Publication accepted a name the lobby font cannot display");
                File.WriteAllText(Path.Combine(root, "result.txt"), "PASS index source mapping, generation hash, atomic publication rollback"); exit = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(root, "result.txt"), "FAIL " + error); Debug.LogException(error); }
            finally { if (Directory.Exists(data)) Directory.Delete(data, true); }
            EditorApplication.Exit(exit);
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    }
}

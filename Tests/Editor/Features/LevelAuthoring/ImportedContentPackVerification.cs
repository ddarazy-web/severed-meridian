using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Elements;
using Elements.Editor;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class ImportedContentPackVerification
    {
        public static void RunUI()
        {
            ContentImportWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<ContentImportWindow>(); window.Show();
                window.CreateGUI();
                var root = window.rootVisualElement;
                Check(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(root, "convert-packs") != null, "conversion action is available without another ZIP import");
                Check(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(root, "pack-status") != null, "current source and generated status shown");
                Check(root.panel != null, "window attached to actual editor panel");
                window.Close(); EditorApplication.Exit(0);
            }
            catch (Exception error) { if (window != null) window.Close(); Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void Run()
        {
            string root = Path.GetFullPath("Logs/LevelDataTransferStage03/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(root);
            try
            {
                var workspace = new AuthoringToolWorkspace();
                workspace.Open(AuthoringSourceSelection.ReadFolder(AuthoringSourceSelection.DefaultConfiguration), "Cancel");
                var session = workspace.Session;
                var snapshot = session.CreateSnapshot();
                Check(snapshot.Documents.Count(value => value.Kind == "level") == 15, "current fifteen levels");
                var outputs = JsonContentPackBuild.CreateBytes(snapshot);
                Check(outputs.Count == 3 && outputs.ContainsKey(LevelPackBuild.FilePath(1)), "1-15 share 1-50 pack plus elements and generation");
                foreach (int number in new[] { 49, 50, 51, 100, 101 })
                    DocumentEditing.DuplicateLevel(session, session.SelectedLevelId, number, "경계 " + number);
                snapshot = session.CreateSnapshot();
                outputs = JsonContentPackBuild.CreateBytes(snapshot);
                Check(outputs.Count == 5, "three intervals plus elements and generation");
                var content = ElementContentPackCodec.Decode(outputs[ElementContentPackBuild.OutputPath]);
                using (var graph = new AuthoringObjectGraph(snapshot.Documents))
                    foreach (var document in snapshot.Documents.Where(value => value.Kind == "level"))
                    {
                        var expected = (LevelDefinition)graph.Resolve(document.Id);
                        var actual = LevelPackCodec.ReadLevel(outputs[LevelPackBuild.FilePath(expected.LevelNumber)], expected.LevelNumber, content);
                        try { Check(LevelPackCodec.Snapshot(expected).SequenceEqual(LevelPackCodec.Snapshot(actual)), "roundtrip " + expected.LevelNumber); }
                        finally { UnityEngine.Object.DestroyImmediate(actual); }
                    }
                string input = Path.Combine(root, "input");
                var store = new ContentSnapshotStore(input); store.Publish(snapshot, null);
                Type state = typeof(JsonContentPackBuild).Assembly.GetType("LevelAuthoring.Editor.ContentPackStatus");
                Check(state != null, "generation status reader exists");
                MethodInfo read = state.GetMethod("Read");
                Func<string> status = () => (string)state.GetProperty("State").GetValue(read.Invoke(null, new object[] { input, root }));
                Check(status() == "Missing", "no generated files");
                JsonPackPublication.Publish(input, root, null);
                Check(status() == "Current", "matching generation and bytes");
                var baseline = outputs.Keys.ToDictionary(path => path, path => File.ReadAllBytes(Path.Combine(root, path)));
                var beforeFailure = store.Read();
                var changed = beforeFailure.Snapshot.Documents;
                var changedLevel = changed.First(value => value.Kind == "level" && (int)value.Data["levelNumber"] <= 50);
                changedLevel.Data["moveCount"] = (int)changedLevel.Data["moveCount"] + 1;
                store.Publish(new LevelAuthoring.Documents.ContentSnapshot(changed), beforeFailure.Hash);
                try { JsonPackPublication.Publish(input, root, index => { if (index == 1) { Check(baseline.Any(pair => !File.ReadAllBytes(Path.Combine(root, pair.Key)).SequenceEqual(pair.Value)), "cancel checkpoint contains changed bytes"); throw new OperationCanceledException(); } }); throw new Exception("cancel ignored"); }
                catch (OperationCanceledException) { }
                Check(baseline.All(pair => File.ReadAllBytes(Path.Combine(root, pair.Key)).SequenceEqual(pair.Value)), "cancellation after first write restores all bytes");
                FileStream locked = null;
                try
                {
                    JsonPackPublication.Publish(input, root, index =>
                    {
                        if (index != 1) return;
                        Check(baseline.Any(pair => !File.ReadAllBytes(Path.Combine(root, pair.Key)).SequenceEqual(pair.Value)), "recovery checkpoint contains changed bytes");
                        locked = new FileStream(Path.Combine(root, outputs.Keys.First()), FileMode.Open, FileAccess.Read, FileShare.None);
                        throw new IOException("injected recovery failure");
                    });
                    throw new Exception("recovery failure ignored");
                }
                catch (AggregateException)
                {
                    Check(File.Exists(Path.Combine(root, "Library/AuthoringPacks/publication.json")), "failed recovery retains journal");
                }
                finally { locked?.Dispose(); }
                JsonPackPublication.Recover(root);
                Check(baseline.All(pair => File.ReadAllBytes(Path.Combine(root, pair.Key)).SequenceEqual(pair.Value)), "retry recovery restores bytes after lock released");
                var saved = store.Read(); var docs = saved.Snapshot.Documents;
                var level = docs.First(value => value.Kind == "level"); level.Data["moveCount"] = (int)level.Data["moveCount"] + 1;
                store.Publish(new LevelAuthoring.Documents.ContentSnapshot(docs), saved.Hash);
                Check(status() == "Outdated", "changed source requires regeneration");
                File.WriteAllText(Path.Combine(root, ElementContentPackBuild.OutputPath), "corrupted");
                Check(status() == "Invalid", "corrupted output is not merely outdated");
                File.WriteAllText(Path.Combine(root, "Library/AuthoringPacks/publication.json"), "pending recovery fixture");
                Check(status() == "RecoveryRequired", "pending recovery is never current or merely outdated");
                File.WriteAllText(Path.Combine(root, "result.txt"), "PASS conversion boundaries and status; FAIL 0");
                Debug.Log("PASS imported packs: " + root); EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(root, "result.txt"), error.ToString()); Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("FAIL " + message);
            Debug.Log("PASS " + message);
        }
    }
}

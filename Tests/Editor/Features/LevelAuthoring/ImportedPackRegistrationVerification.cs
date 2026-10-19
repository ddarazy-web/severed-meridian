using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using Levels;
using Levels.Editor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class ImportedPackRegistrationVerification
    {
        private static string root;
        private static Dictionary<string, byte[]> originals;
        private static ContentImportWindow window;
        private static int passed;
        private static readonly string[] folders = { "Assets/Data/LevelPacks", "Assets/Data/ElementPacks", "Assets/AddressableAssetsData" };

        public static void Run()
        {
            root = Path.GetFullPath("Logs/LevelDataTransferStage03/registration-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(root);
            try
            {
                MethodInfo selected = typeof(JsonPackRegistration).GetMethod("PublishSelected");
                Check(selected != null, "selected source conversion shares transaction guard");
                Check(!File.Exists("Library/AuthoringPacks/publication.json"), "no existing recovery to overwrite");
                string elementFolder = Path.GetDirectoryName(Elements.Editor.ElementContentPackBuild.OutputPath);
                folders[1] = elementFolder;
                originals = Files().ToDictionary(path => path, File.ReadAllBytes);
                File.WriteAllText(Path.Combine(root, "originals.json"), new JObject(originals.Select(pair => new JProperty(pair.Key, Convert.ToBase64String(pair.Value)))).ToString());
                var workspace = new AuthoringToolWorkspace();
                workspace.Open(AuthoringSourceSelection.ReadFolder(AuthoringSourceSelection.DefaultConfiguration), "Cancel");
                string small = Path.Combine(root, "small"), large = Path.Combine(root, "large");
                var snapshot = workspace.Session.CreateSnapshot();
                new ContentSnapshotStore(small).Publish(snapshot, null);
                DocumentEditing.DuplicateLevel(workspace.Session, workspace.Session.SelectedLevelId, 51, "등록 구간 검사");
                new ContentSnapshotStore(large).Publish(workspace.Session.CreateSnapshot(), null);
                JsonPackRegistration.Publish(large);
                Check(Entry(51) != null, "second interval registered by actual Addressables API");
                JsonPackRegistration.Publish(small);
                Check(Entry(51) == null && File.Exists(LevelPackBuild.FilePath(51)), "shrinking removes old registration but preserves old bytes");
                var baseline = Files().ToDictionary(path => path, File.ReadAllBytes);
                try { JsonPackRegistration.Publish(large, () => throw new IOException("injected-registration")); throw new Exception("failure accepted"); }
                catch (IOException error) when (error.Message == "injected-registration") { }
                Check(baseline.All(pair => File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)), "registration failure restores all prior files and metadata");
                Check(Entry(51) == null, "reloaded Addressables state restored");
                string configuration = Path.Combine(root, "selection.json");
                string configText = new JObject { ["version"] = 1, ["folder"] = small, ["projectId"] = snapshot.Project.Id }.ToString();
                File.WriteAllText(configuration, configText);
                try
                {
                    selected.Invoke(null, new object[] { configuration, (Action)(() => File.AppendAllText(configuration, " ")) });
                    throw new Exception("changed selection accepted");
                }
                catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { }
                Check(baseline.All(pair => File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)), "selection change rolls back publication");
                string journal = null;
                Dictionary<string, byte[]> interrupted = null;
                try
                {
                    JsonPackRegistration.Publish(large, () =>
                    {
                        journal = File.ReadAllText("Library/AuthoringPacks/publication.json");
                        interrupted = Files().ToDictionary(path => path, File.ReadAllBytes);
                        throw new IOException("capture interrupted publication");
                    });
                }
                catch (IOException error) when (error.Message == "capture interrupted publication") { }
                Check(journal != null && interrupted.Any(pair => baseline.ContainsKey(pair.Key) && !baseline[pair.Key].SequenceEqual(pair.Value)), "interrupted fixture contains genuinely changed bytes");
                foreach (var pair in interrupted) File.WriteAllBytes(pair.Key, pair.Value);
                File.WriteAllText("Library/AuthoringPacks/publication.json", journal);
                File.WriteAllText(configuration, "{}");
                bool recoveryShown = ContentPackStatus.Read(Path.Combine(root, "missing-source"), Path.GetFullPath(".")).State == "RecoveryRequired";
                window = ScriptableObject.CreateInstance<ContentImportWindow>(); window.ConfigurationPath = configuration; window.Show(); window.CreateGUI();
                bool recoveryInWindow = window.rootVisualElement.Q<Label>("pack-status").text.Contains("복구 필요");
                window.Close(); window = null;
                try { selected.Invoke(null, new object[] { configuration, null }); }
                catch (TargetInvocationException) { }
                Check(recoveryShown, "pending recovery shown even when source cannot be read");
                Check(recoveryInWindow, "window reports pending recovery despite invalid selection");
                Check(!File.Exists("Library/AuthoringPacks/publication.json") && baseline.All(pair => File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)), "invalid source does not prevent prior publication recovery");
                Check(Entry(51) == null, "recovered registration lookup available despite invalid source");
                File.WriteAllText(configuration, configText);
                window = ScriptableObject.CreateInstance<ContentImportWindow>(); window.ConfigurationPath = configuration; window.Show(); window.CreateGUI();
                var button = window.rootVisualElement.Q<Button>("convert-packs");
                typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button.clickable, new object[] { null });
                Check(!button.enabledSelf, "conversion disables duplicate click immediately");
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        Check(window.rootVisualElement.Q<Label>("import-status").text.Contains("등록 완료"), "real window conversion click completes");
                        Check(window.rootVisualElement.Q<Label>("pack-status").text.Contains("최신"), "window reports generated source as current");
                        Check(Entry(1)?.address == LevelPackCodec.Address(1), "expected level address registered");
                        LevelPackBuild.ValidateExclusion(AddressableAssetSettingsDefaultObject.Settings);
                        Finish(null);
                    }
                    catch (Exception error) { Finish(error); }
                };
            }
            catch (Exception error) { Finish(error); }
        }

        private static UnityEditor.AddressableAssets.Settings.AddressableAssetEntry Entry(int number)
            => AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(LevelPackBuild.FilePath(number)));
        private static string[] Files() => folders.Where(Directory.Exists).SelectMany(folder => Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            .Select(path => path.Replace('\\', '/')).Distinct().ToArray();
        private static void Check(bool condition, string text)
        { if (!condition) throw new Exception("FAIL " + text); passed++; Debug.Log("PASS " + text); }
        private static void Finish(Exception failure)
        {
            try
            {
                if (window != null) window.Close();
                if (originals != null)
                {
                    JsonPackPublication.Recover(Path.GetFullPath("."));
                    foreach (string path in Files().Where(path => !originals.ContainsKey(path))) File.Delete(path);
                    foreach (var pair in originals) File.WriteAllBytes(pair.Key, pair.Value);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    foreach (string path in originals.Keys.Where(path => path.EndsWith(".asset"))) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    Check(originals.All(pair => File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)), "original project files preserved after verification");
                }
            }
            catch (Exception restore) { failure = new AggregateException("verification restoration failed", failure ?? restore, restore); }
            File.WriteAllText(Path.Combine(root, "result.txt"), failure == null ? "PASS " + passed + " / FAIL 0" : failure.ToString());
            if (failure != null) Debug.LogException(failure); else Debug.Log("PASS imported registration: " + root);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Storage;
using LevelAuthoring.Documents;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class JsonPackPublicationVerification
    {
        public static void Run()
        {
            try
            {
                Type publisher = typeof(LegacyContentExporter).Assembly.GetType("LevelAuthoring.Editor.JsonPackPublication");
                Check(publisher != null, "pack publication transaction exists");
                var source = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage06/latest-candidate.txt")).Read();
                string root = Path.GetFullPath("Logs/GameAuthoringStage06/publication-" + Guid.NewGuid().ToString("N"));
                string input = Path.Combine(root, "input");
                new ContentSnapshotStore(input).Publish(source.Snapshot, null);
                var outputs = JsonContentPackBuild.CreateBytes(new ContentSnapshotStore(input).Read().Snapshot);
                foreach (string path in outputs.Keys)
                {
                    string destination = Path.Combine(root, path); Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.WriteAllText(destination, "previous generation"); File.WriteAllText(destination + ".meta", "preserved meta");
                }
                var originals = outputs.Keys.SelectMany(path => new[] { path, path + ".meta" }).ToDictionary(path => path, path => File.ReadAllBytes(Path.Combine(root, path)));
                MethodInfo publish = publisher.GetMethod("Publish");
                string journalPath = Path.Combine(root, "Library/AuthoringPacks/publication.json"), interruptedJournal = null;
                try { publish.Invoke(null, new object[] { input, root, (Action<int>)(index => { if (index == 1) { interruptedJournal = File.ReadAllText(journalPath); throw new IOException("교체 실패 주입"); } }) }); throw new Exception("injected failure ignored"); }
                catch (TargetInvocationException error) when (error.InnerException is IOException failure && failure.Message == "교체 실패 주입") { }
                Check(!string.IsNullOrEmpty(interruptedJournal), "failure checkpoint reached after partial replacement");
                foreach (var original in originals) Check(File.ReadAllBytes(Path.Combine(root, original.Key)).SequenceEqual(original.Value), "failed swap restores " + original.Key);
                File.WriteAllText(Path.Combine(root, outputs.Keys.First()), "interrupted replacement");
                File.WriteAllText(journalPath, interruptedJournal);
                publisher.GetMethod("Recover").Invoke(null, new object[] { root });
                foreach (var original in originals) Check(File.ReadAllBytes(Path.Combine(root, original.Key)).SequenceEqual(original.Value), "restart recovery restores " + original.Key);
                publish.Invoke(null, new object[] { input, root, null });
                foreach (var pair in outputs) Check(File.ReadAllBytes(Path.Combine(root, pair.Key)).SequenceEqual(pair.Value), "complete pack set published " + pair.Key);
                foreach (var pair in originals.Where(pair => pair.Key.EndsWith(".meta"))) Check(File.ReadAllBytes(Path.Combine(root, pair.Key)).SequenceEqual(pair.Value), "successful swap preserves meta");
                Check(!File.Exists(Path.Combine(root, "Library/AuthoringPacks/publication.json")), "committed transaction clears journal");
                foreach (int changedAt in new[] { -1, 0 })
                {
                    try
                    {
                        publish.Invoke(null, new object[] { input, root, (Action<int>)(index =>
                        {
                            if (index != changedAt) return;
                            var repository = new ContentSnapshotStore(input); var current = repository.Read();
                            var documents = current.Snapshot.Documents;
                            var level = documents.First(document => document.Kind == "level");
                            level.Data["moveCount"] = (int)level.Data["moveCount"] + 1;
                            repository.Publish(new ContentSnapshot(documents), current.Hash);
                        }) });
                        throw new Exception("changed source published");
                    }
                    catch (TargetInvocationException error) when (error.InnerException is ContentFormatException) { }
                    foreach (var pair in outputs) Check(File.ReadAllBytes(Path.Combine(root, pair.Key)).SequenceEqual(pair.Value), "source change preserves prior complete set");
                }
                MethodInfo registered = publisher.GetMethod("PublishWithRegistration");
                Check(registered != null, "registration participates in pack transaction");
                const string settings = "Assets/AddressableAssetsData/AddressableAssetSettings.asset";
                string settingsPath = Path.Combine(root, settings);
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllText(settingsPath, "old addresses");
                File.WriteAllText(settingsPath + ".meta", "stable settings guid");
                var beforeRegistration = outputs.Keys.ToDictionary(path => path, path => File.ReadAllBytes(Path.Combine(root, path)));
                string registrationJournal = null;
                try
                {
                    registered.Invoke(null, new object[] { input, root, new[] { settings, settings + ".meta" },
                        (Action<IReadOnlyCollection<string>>)(paths =>
                        {
                            File.WriteAllText(settingsPath, "new addresses");
                            File.WriteAllText(settingsPath + ".meta", "changed guid");
                            registrationJournal = File.ReadAllText(journalPath);
                            throw new IOException("등록 실패 주입");
                        }), null });
                    throw new Exception("registration failure ignored");
                }
                catch (TargetInvocationException error) when (error.InnerException is IOException failure && failure.Message == "등록 실패 주입") { }
                Check(registrationJournal != null, "registration failure occurred inside journal boundary");
                Check(File.ReadAllText(settingsPath) == "old addresses" && File.ReadAllText(settingsPath + ".meta") == "stable settings guid", "failed registration restores addresses and GUID");
                foreach (var pair in beforeRegistration) Check(File.ReadAllBytes(Path.Combine(root, pair.Key)).SequenceEqual(pair.Value), "registration failure restores pack bytes");
                File.WriteAllText(settingsPath, "interrupted addresses"); File.WriteAllText(journalPath, registrationJournal);
                JsonPackPublication.Recover(root);
                Check(File.ReadAllText(settingsPath) == "old addresses", "restart restores registration metadata");
                Debug.Log("PASS JSON pack publication"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void Check(bool value, string text) { if (!value) throw new Exception("FAIL " + text); Debug.Log("PASS " + text); }
    }
}


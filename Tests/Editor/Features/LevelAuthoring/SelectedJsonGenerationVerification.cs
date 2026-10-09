using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class SelectedJsonGenerationVerification
    {
        public static void Run()
        {
            var originals = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            string[] roots = { "Assets/AddressableAssetsData", "Assets/Data/LevelPacks", "Assets/Data/ElementPacks" };
            try
            {
                foreach (string path in roots.SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories)))
                    originals[path.Replace('\\', '/')] = File.ReadAllBytes(path);
                foreach (string path in new[] { AuthoringSourceSelection.DefaultConfiguration, AuthoringSourceSelection.DefaultConfiguration + ".previous" })
                    originals[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
                string folder = "ContentData/Candidates/verification-selected-" + Guid.NewGuid().ToString("N");
                LegacyContentExporter.ExportCandidate(folder);
                AuthoringSourceSelection.Adopt(folder, AuthoringSourceSelection.DefaultConfiguration);
                var store = new ContentSnapshotStore(Path.GetFullPath(folder)); var source = store.Read();
                var documents = source.Snapshot.Documents;
                var level = documents.First(doc => doc.Kind == "level");
                level.Data["moveCount"] = (int)level.Data["moveCount"] + 7;
                var changed = store.Publish(new ContentSnapshot(documents), source.Hash);
                var expected = JsonContentPackBuild.CreateBytes(changed.Snapshot);
                LevelPackBuild.Generate();
                foreach (var pair in expected)
                    if (!File.Exists(pair.Key) || !File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value))
                        throw new Exception("FAIL Generate does not publish selected JSON: " + pair.Key);
                var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
                foreach (string path in expected.Keys)
                {
                    string expectedAddress = path == Elements.Editor.ElementContentPackBuild.OutputPath ? Elements.ElementContentPackCodec.Address : "Levels/" + Path.GetFileNameWithoutExtension(path);
                    if (settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path))?.address != expectedAddress)
                        throw new Exception("FAIL registered address " + path);
                }
                Debug.Log("PASS selected JSON edits generate matching packs and registered addresses without SO fallback");
            }
            catch (Exception error) { Debug.LogException(error); Restore(); EditorApplication.Exit(1); return; }
            Restore(); EditorApplication.Exit(0);

            void Restore()
            {
                foreach (string path in roots.SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories)))
                    if (!originals.ContainsKey(path.Replace('\\', '/'))) File.Delete(path);
                foreach (var pair in originals)
                    if (pair.Value == null) { if (File.Exists(pair.Key)) File.Delete(pair.Key); }
                    else File.WriteAllBytes(pair.Key, pair.Value);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                foreach (string path in originals.Keys.Where(path => path.EndsWith(".asset")))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
        }
    }
}

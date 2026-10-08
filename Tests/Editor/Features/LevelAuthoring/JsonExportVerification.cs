using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LevelAuthoring.Storage;
namespace LevelAuthoring.Editor
{
    public static class JsonExportVerification
    {
        public static void Run()
        {
            try
            {
                string root = Path.GetFullPath("ContentData/Trials/game-authoring-stage-02/export-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
                var sources = LegacyContentExporter.Discover();
                var originalFiles = sources.SelectMany(source => new[] { AssetDatabase.GetAssetPath(source.Asset), AssetDatabase.GetAssetPath(source.Asset) + ".meta" })
                    .ToDictionary(path => path, File.ReadAllBytes);
                var originalDirty = sources.ToDictionary(source => source.Asset, source => EditorUtility.IsDirty(source.Asset));
                var first = LegacyContentExporter.Export(root);
                var second = LegacyContentExporter.Export(root);
                var before = first.Snapshot.Project.Data["sourceIds"].ToString();
                if (before != second.Snapshot.Project.Data["sourceIds"].ToString()) throw new Exception("재내보내기 ID 변경");
                if (first.Snapshot.Project.Id != second.Snapshot.Project.Id) throw new Exception("프로젝트 ID 변경");
                if (first.Snapshot.Documents.Length != 27) throw new Exception("원본 문서 수가 다름");
                var repository = new ContentSnapshotStore(root);
                if (repository.ReadBackup().Hash != first.Hash) throw new Exception("전체 이전 정상본 없음");
                var pending = ScriptableObject.CreateInstance<Tutorial.TutorialUserSampleDefinition>();
                var withNewSource = sources.Concat(new[] { new LegacyContentSource("ffffffffffffffffffffffffffffffff", "tutorialSample", pending) }).ToArray();
                try
                {
                    LegacyContentExporter.Export(root, withNewSource, () => { throw new IOException("주입"); });
                    throw new Exception("실패 주입 없음");
                }
                catch (IOException) { }
                finally { UnityEngine.Object.DestroyImmediate(pending); }
                if (repository.Read().Hash != second.Hash) throw new Exception("실패한 재내보내기로 공개본 변경");
                foreach (var pair in originalFiles) if (!File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)) throw new Exception("원본/메타 변경: " + pair.Key);
                foreach (var pair in originalDirty) if (EditorUtility.IsDirty(pair.Key) != pair.Value) throw new Exception("원본 dirty 변경");
                File.WriteAllText("Logs/GameAuthoringStage02/latest-export.txt", root);
                File.WriteAllText("Logs/GameAuthoringStage02/export-results.txt",
                    "PASS 27 published documents\nPASS stable source IDs\nPASS stable project ID\nPASS whole snapshot backup\nPASS failed re-export preserves project and new-source mapping\nPASS source bytes/meta/dirty unchanged\n");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}

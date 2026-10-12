using System;
using System.IO;
using System.Linq;
using System.Threading;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using Levels;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class ProjectPackageTrialVerification
    {
        public static void Run()
        {
            try
            {
                string source = File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt");
                var original = new ContentSnapshotStore(source).Read().Snapshot;
                string directory = Path.GetFullPath("Logs/AndroidLevelToolStage04/trial-" + Guid.NewGuid().ToString("N"));
                using (var stream = new MemoryStream())
                {
                    ProjectPackageStore.Export(source, stream, CancellationToken.None);
                    stream.Position = 0;
                    string imported = ProjectPackageStore.Import(stream, directory, "왕복 검사", CancellationToken.None);
                    var copied = new ContentSnapshotStore(imported).Read().Snapshot;
                    foreach (var level in original.Documents.Where(document => document.Kind == "level"))
                    {
                        var before = JsonPuzzlePlayAdapter.CreateRequest(original, level.Id, 12345).CreateDefinition();
                        var after = JsonPuzzlePlayAdapter.CreateRequest(copied, level.Id, 12345).CreateDefinition();
                        try
                        {
                            if (!LevelPackCodec.Snapshot(before).SequenceEqual(LevelPackCodec.Snapshot(after)))
                                throw new Exception("ZIP 왕복 후 게임용 MemoryPack 입력이 달라졌습니다: " + level.Id);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(before); UnityEngine.Object.DestroyImmediate(after); }
                    }
                }
                Debug.Log("PASS project ZIP roundtrip preserves all fixture level IDs, game definitions and MemoryPack input bytes");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameScreen;
using LevelAuthoring.Storage;
using Levels;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class PackedTrialVerification
    {
        public static async void Run()
        {
            try
            {
                Type adapter = typeof(LevelAuthoring.Runtime.JsonPuzzlePlayAdapter).Assembly.GetType("LevelAuthoring.Runtime.PackedPuzzlePlayAdapter");
                if (adapter == null) throw new Exception("FAIL packed trial adapter missing");
                var snapshot = new ContentSnapshotStore(AuthoringSourceSelection.ReadFolder(AuthoringSourceSelection.DefaultConfiguration)).Read().Snapshot;
                int number = (int)snapshot.Documents.First(doc => doc.Kind == "level").Data["levelNumber"];
                var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
                Func<string, CancellationToken, UniTask<byte[]>> read = (address, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    string path = settings.groups.Where(group => group != null).SelectMany(group => group.entries).Single(entry => entry.address == address).AssetPath;
                    return UniTask.FromResult(AssetDatabase.LoadAssetAtPath<TextAsset>(path).bytes);
                };
                var method = adapter.GetMethod("CreateRequestAsync", BindingFlags.Static | BindingFlags.NonPublic);
                var result = await (UniTask<LevelAuthoring.Runtime.PackedPuzzleTrial>)method.Invoke(null, new object[] { number, 12345, read, CancellationToken.None });
                var request = result.Request;
                string hash = result.SourceHash;
                if (hash != JsonContentPackBuild.SourceHash(snapshot) || request.Seed != 12345) throw new Exception("FAIL packed source identity");
                var expectedRequest = LevelAuthoring.Runtime.JsonPuzzlePlayAdapter.CreateRequest(snapshot, snapshot.Documents.First(doc => doc.Kind == "level").Id, 12345);
                var a = request.CreateDefinition(); var b = expectedRequest.CreateDefinition();
                try { if (!LevelPackCodec.Snapshot(a).SequenceEqual(LevelPackCodec.Snapshot(b))) throw new Exception("FAIL draft and packed inputs differ"); }
                finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
                var docs = snapshot.Documents; docs.First(doc => doc.Kind == "level").Data["moveCount"] = 99;
                if (hash == JsonContentPackBuild.SourceHash(new LevelAuthoring.Documents.ContentSnapshot(docs))) throw new Exception("FAIL stale draft not detected");
                Debug.Log("PASS registered pack trial source hash seed identical request and stale draft detection"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}


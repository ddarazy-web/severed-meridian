using System;
using System.IO;
using System.Linq;
using GameScreen;
using Levels;
using LevelAuthoring.Runtime;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using MemoryPack;
using UnityEditor;
using UnityEngine;
namespace LevelAuthoring.Editor
{
    public static class JsonRequestVerification
    {
        public static void Run()
        {
            try
            {
                var snapshot = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-export.txt")).Read().Snapshot;
                var sources = LegacyContentExporter.Discover();
                int baseline = Count();
                foreach (var source in sources.Where(value => value.Kind == "level"))
                {
                    string id = (string)snapshot.Project.Data["sourceIds"].Single(item => (string)item["sourceGuid"] == source.Guid)["documentId"];
                    var expected = PuzzlePlayRequest.Capture((LevelDefinition)source.Asset, 12345);
                    var actual = JsonPuzzlePlayAdapter.CreateRequest(snapshot, id, 12345);
                    var left = expected.CreateDefinition(); var right = actual.CreateDefinition();
                    try
                    {
                        if (LevelPackCodec.CaptureJson(left) != LevelPackCodec.CaptureJson(right)) throw new Exception("실행 입력 불일치: " + source.Asset.name);
                        if (!MemoryPackSerializer.Serialize(expected.CreateVisuals()).SequenceEqual(MemoryPackSerializer.Serialize(actual.CreateVisuals())))
                            throw new Exception("표현 DTO 불일치");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(left); UnityEngine.Object.DestroyImmediate(right); }
                }
                if (baseline != Count()) throw new Exception("임시 제작 객체 누수");
                var documents = snapshot.Documents;
                var definition = documents.First(doc => doc.Kind == "element" && (string)doc.Data["definition"]["id"] == "obstacle.crate.wood");
                definition.Data["definition"]["placement"]["maxDurability"] = -1;
                var invalid = new ContentSnapshot(documents);
                var levelDocument = documents.First(doc => doc.Kind == "level" && doc.Data["catalogId"].Type == Newtonsoft.Json.Linq.JTokenType.String);
                bool rejected = false;
                try { JsonPuzzlePlayAdapter.CreateRequest(invalid, levelDocument.Id, 73); }
                catch (ArgumentException) { rejected = true; }
                if (!rejected || baseline != Count()) throw new Exception("실행 변환 도중 실패의 임시 객체 해제 오류");
                File.WriteAllText("Logs/GameAuthoringStage02/request-results.txt", "PASS 4 level execution inputs\nPASS 4 visual DTOs\nPASS temporary authoring cleanup\nPASS late conversion failure cleanup\n");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static int Count() => Resources.FindObjectsOfTypeAll<ScriptableObject>().Count(value =>
            value is LevelDefinition || value is Elements.ElementDefinitionAsset || value is Elements.ElementCatalogAsset ||
            value is Elements.ElementVisualCatalogAsset || value is Tutorial.TutorialFlowDefinition);
    }
}

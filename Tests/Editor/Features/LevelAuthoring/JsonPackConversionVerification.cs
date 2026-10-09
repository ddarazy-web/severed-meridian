using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Elements;
using Elements.Editor;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class JsonPackConversionVerification
    {
        public static void Run()
        {
            try
            {
                var isAuthoring = typeof(LevelPackBuild).GetMethod("IsAuthoringType", BindingFlags.Static | BindingFlags.NonPublic);
                foreach (Type type in new[] { typeof(LevelDefinition), typeof(Elements.ElementCatalogAsset), typeof(Elements.ElementDefinitionAsset), typeof(Elements.ElementVisualCatalogAsset), typeof(Tutorial.TutorialFlowDefinition), typeof(Tutorial.TutorialUserSampleDefinition), typeof(LevelShapePreset) })
                    Check((bool)isAuthoring.Invoke(null, new object[] { type }), "all source types excluded from pack resources: " + type.Name);
                Type converter = typeof(LegacyContentExporter).Assembly.GetType("LevelAuthoring.Editor.JsonContentPackBuild");
                Check(converter != null, "single snapshot pack converter exists");
                string folder = File.ReadAllText("Logs/GameAuthoringStage06/latest-candidate.txt");
                var workspace = new AuthoringToolWorkspace(); workspace.Open(folder, "Cancel");
                var session = workspace.Session;
                string source = session.SelectedLevelId;
                foreach (int number in new[] { 50, 51, 100, 101 }) DocumentEditing.DuplicateLevel(session, source, number, "구간 검사 " + number);
                var snapshot = session.CreateSnapshot();
                string contentHash = LevelAuthoring.Documents.ContentSnapshotFingerprint.Compute(snapshot);
                string copyFolder = Path.GetFullPath("ContentData/Candidates/verification-fingerprint-" + Guid.NewGuid().ToString("N"));
                var copyStore = new LevelAuthoring.Storage.ContentSnapshotStore(copyFolder);
                var copy = copyStore.Publish(snapshot, null);
                Check(LevelAuthoring.Documents.ContentSnapshotFingerprint.Compute(copy.Snapshot) == contentHash, "save copy retains content fingerprint");
                var savedAgain = copyStore.Publish(copy.Snapshot, copy.Hash);
                Check(LevelAuthoring.Documents.ContentSnapshotFingerprint.Compute(savedAgain.Snapshot) == contentHash, "unchanged save retains content fingerprint");
                var outputs = (Dictionary<string, byte[]>)converter.GetMethod("CreateBytes").Invoke(null, new object[] { snapshot });
                Check(outputs.ContainsKey(LevelPackBuild.FilePath(1)) && outputs.ContainsKey(LevelPackBuild.FilePath(51)) && outputs.ContainsKey(LevelPackBuild.FilePath(101)),
                    "50 51 100 101 use three existing address ranges");
                ElementContentData content = ElementContentPackCodec.Decode(outputs[ElementContentPackBuild.OutputPath]);
                using (var graph = new AuthoringObjectGraph(snapshot.Documents))
                {
                    foreach (var document in snapshot.Documents.Where(doc => doc.Kind == "level"))
                    {
                        var expected = (LevelDefinition)graph.Resolve(document.Id);
                        var loaded = LevelPackCodec.ReadLevel(outputs[LevelPackBuild.FilePath(expected.LevelNumber)], expected.LevelNumber, content);
                        try { Check(LevelPackCodec.Snapshot(expected).SequenceEqual(LevelPackCodec.Snapshot(loaded)), "existing codec roundtrip level " + expected.LevelNumber); }
                        finally { UnityEngine.Object.DestroyImmediate(loaded); }
                    }
                }
                try { LevelPackCodec.ReadLevel(outputs[LevelPackBuild.FilePath(1)], 49, content); throw new Exception("missing level accepted"); }
                catch (KeyNotFoundException) { }
                string before = session.ExportState();
                converter.GetMethod("CreateBytes").Invoke(null, new object[] { snapshot });
                Check(session.ExportState() == before, "conversion does not mutate authoring session");
                const string generationPath = "Assets/Data/LevelPacks/content-generation.bytes";
                Check(outputs.ContainsKey(generationPath), "pack set includes generation manifest");
                Type manifestCodec = typeof(LevelPackCodec).Assembly.GetType("Levels.ContentPackGenerationCodec");
                Check(manifestCodec != null, "runtime generation verifier exists");
                object manifest = manifestCodec.GetMethod("Decode").Invoke(null, new object[] { outputs[generationPath] });
                manifestCodec.GetMethod("Verify").Invoke(null, new object[] { manifest, ElementContentPackCodec.Address, outputs[ElementContentPackBuild.OutputPath] });
                byte[] mixed = (byte[])outputs[ElementContentPackBuild.OutputPath].Clone(); mixed[mixed.Length - 1] ^= 1;
                try { manifestCodec.GetMethod("Verify").Invoke(null, new object[] { manifest, ElementContentPackCodec.Address, mixed }); throw new Exception("mixed pack accepted"); }
                catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { }
                Check(true, "generation verifier rejects mixed or altered element pack");
                var changedDocuments = snapshot.Documents;
                var changedLevel = changedDocuments.First(document => document.Kind == "level");
                changedLevel.Data["moveCount"] = (int)changedLevel.Data["moveCount"] + 1;
                var changedOutputs = (Dictionary<string, byte[]>)converter.GetMethod("CreateBytes").Invoke(null,
                    new object[] { new LevelAuthoring.Documents.ContentSnapshot(changedDocuments) });
                object nextManifest = manifestCodec.GetMethod("Decode").Invoke(null, new object[] { changedOutputs[generationPath] });
                PropertyInfo sourceHash = manifest.GetType().GetProperty("SourceHash");
                Check((string)sourceHash.GetValue(manifest) != (string)sourceHash.GetValue(nextManifest), "changed JSON has different generation fingerprint");
                int changedNumber = (int)changedLevel.Data["levelNumber"];
                try
                {
                    manifestCodec.GetMethod("Verify").Invoke(null, new object[] { manifest, LevelPackCodec.Address(changedNumber), changedOutputs[LevelPackBuild.FilePath(changedNumber)] });
                    throw new Exception("new generation level accepted by old manifest");
                }
                catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { }
                foreach (string kind in new[] { "level", "element" })
                {
                    var invalid = snapshot.Documents;
                    var pair = invalid.Where(document => document.Kind == kind).Take(2).ToArray();
                    if (kind == "level") pair[1].Data["levelNumber"] = pair[0].Data["levelNumber"].DeepClone();
                    else pair[1].Data["definition"]["id"] = pair[0].Data["definition"]["id"].DeepClone();
                    try
                    {
                        JsonContentPackBuild.CreateBytes(new LevelAuthoring.Documents.ContentSnapshot(invalid));
                        throw new Exception("duplicate content accepted: " + kind);
                    }
                    catch (LevelAuthoring.Documents.ContentFormatException error) when (error.Message.Contains("중복")) { }
                    Check(true, "duplicate " + kind + " rejected before publication");
                }
                var fixture = new LevelAuthoring.Storage.ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt")).Read().Snapshot;
                Check(fixture.Documents.Any(document => document.Kind == "tutorialFlow"), "shared tutorial fixture exists");
                var fixtureOutputs = JsonContentPackBuild.CreateBytes(fixture);
                var fixtureElements = ElementContentPackCodec.Decode(fixtureOutputs[ElementContentPackBuild.OutputPath]);
                using (var graph = new AuthoringObjectGraph(fixture.Documents))
                    foreach (var document in fixture.Documents.Where(value => value.Kind == "level"))
                    {
                        var expected = (LevelDefinition)graph.Resolve(document.Id);
                        var actual = LevelPackCodec.ReadLevel(fixtureOutputs[LevelPackBuild.FilePath(expected.LevelNumber)], expected.LevelNumber, fixtureElements);
                        try
                        {
                            Check(LevelPackCodec.Snapshot(expected).SequenceEqual(LevelPackCodec.Snapshot(actual)), "shared tutorial bindings and completion pack roundtrip " + expected.LevelNumber);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(actual); }
                    }
                Debug.Log("PASS JSON pack conversion"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void Check(bool value, string text) { if (!value) throw new Exception("FAIL " + text); Debug.Log("PASS " + text); }
    }
}



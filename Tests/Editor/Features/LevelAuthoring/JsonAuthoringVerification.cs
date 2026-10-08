using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LevelAuthoring.Editor
{
    public static class JsonAuthoringVerification
    {
        public static void Run()
        {
            var owned = new List<ScriptableObject>();
            var results = new List<string>();
            try
            {
                var roots = new Dictionary<string, Type>
                {
                    { "level", typeof(Levels.LevelDefinition) }, { "element", typeof(Elements.ElementDefinitionAsset) },
                    { "catalog", typeof(Elements.ElementCatalogAsset) }, { "visual", typeof(Elements.ElementVisualCatalogAsset) },
                    { "tutorialFlow", typeof(Tutorial.TutorialFlowDefinition) }, { "tutorialSample", typeof(Tutorial.TutorialUserSampleDefinition) },
                    { "shape", typeof(Levels.Editor.LevelShapePreset) }
                };
                foreach (var root in roots)
                {
                    var sources = AssetDatabase.FindAssets("t:" + root.Value.Name).Select(AssetDatabase.GUIDToAssetPath)
                        .Select(path => AssetDatabase.LoadAssetAtPath(path, root.Value)).OfType<ScriptableObject>().ToList();
                    if (sources.Count == 0)
                    {
                        var source = ScriptableObject.CreateInstance(root.Value); source.name = "임시 " + root.Key;
                        owned.Add(source); sources.Add(source);
                        if (source is Tutorial.TutorialFlowDefinition flow)
                        {
                            flow.steps.Add(new Tutorial.TutorialStepDefinition { authoringId = "step-one", instructions = "교환하세요" });
                            flow.parameters.Add(new Tutorial.TutorialFlowParameter { key = "first", stepId = "step-one", field = Tutorial.TutorialFlowField.First });
                        }
                        if (source is Tutorial.TutorialUserSampleDefinition sample)
                        { sample.description = "재사용 설명"; sample.steps.Add(new Tutorial.TutorialStepDefinition { authoringId = "sample-step" }); }
                    }
                    if (root.Key == "visual")
                    {
                        var visual = ScriptableObject.CreateInstance<Elements.ElementVisualCatalogAsset>();
                        visual.name = "시각 명시 설정"; owned.Add(visual); sources.Add(visual);
                        JsonUtility.FromJsonOverwrite(@"{""catalog"":{""definitions"":[{""key"":""legacy.bomb"",""generates"":[],""states"":[{""color"":-1,""durability"":-1,""charge"":-1,""requiredCharge"":-1,""direction"":-1,""frame"":0,""logicalSize"":-1,""path"":""PowerBlocks/moon-bomb-v1"",""pivotX"":0.5,""pivotY"":0.5,""size"":1,""offsetX"":0,""offsetY"":0,""angle"":0,""order"":10,""sheetColumns"":1,""sheetRows"":1,""sheetFrame"":0,""effects"":[""Effects/BombExplosion/Animations/bomb-explosion-frame-01-v1-256""],""effectAnimations"":[]}]}],""bindings"":[]},""planningDocument"":""Docs/Design/example.md""}", visual);
                    }
                    foreach (var source in sources)
                    {
                        string before = EditorJsonUtility.ToJson(source);
                        bool dirty = EditorUtility.IsDirty(source);
                        string sourcePath = AssetDatabase.GetAssetPath(source);
                        byte[] disk = string.IsNullOrEmpty(sourcePath) ? null : File.ReadAllBytes(sourcePath);
                        var resources = new Dictionary<string, string>();
                        string Resource(string path)
                        { string id = "resource-" + resources.Count; resources[id] = path; return id; }
                        var document = UnityAuthoringCodec.Write(source, root.Key, "document-one", AssetDatabase.GetAssetPath, Resource);
                        if (source.name == "시각 명시 설정")
                        {
                            var effects = document.Data["catalog"]["definitions"][0]["states"][0]["effectResourceIds"];
                            if (effects == null || !resources.ContainsKey((string)effects[0])) throw new Exception("효과 경로의 논리 ID 변환 누락");
                            if (((Elements.ElementVisualCatalogAsset)source).ToDto().definitions.Length <= 1) throw new Exception("명시 설정과 기본 표현 분리 확인 실패");
                        }
                        string json = ContentJson.Write(document);
                        var copy = UnityAuthoringCodec.Read(ContentJson.Read(json), root.Value,
                            path => AssetDatabase.LoadAssetAtPath<ScriptableObject>(path), id => resources[id], owned.Add);
                        if (EditorJsonUtility.ToJson(copy) != before) throw new Exception("제작 필드 왕복 불일치: " + source.name);
                        if (EditorJsonUtility.ToJson(source) != before || EditorUtility.IsDirty(source) != dirty ||
                            (disk != null && !File.ReadAllBytes(sourcePath).SequenceEqual(disk))) throw new Exception("원본 변경: " + source.name);
                        results.Add("PASS authoring roundtrip " + root.Key + " " + source.name);
                    }
                }
                Directory.CreateDirectory("Logs/GameAuthoringStage02");
                File.WriteAllLines("Logs/GameAuthoringStage02/authoring-roundtrip.txt", results);
                Debug.Log("JSON authoring roundtrips: " + results.Count);
                Finish(owned, 0);
            }
            catch (Exception error) { Debug.LogException(error); Finish(owned, 1); }
        }
        private static void Finish(List<ScriptableObject> owned, int code)
        { foreach (var item in owned) if (item != null) Object.DestroyImmediate(item); EditorApplication.Exit(code); }
    }
}

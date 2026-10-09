using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Levels;
using Tutorial;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class TutorialAuthoringRuntimeVerification
    {
        public static void Run()
        {
            var owned = new List<ScriptableObject>();
            try
            {
                Type rules = typeof(TutorialStepDefinition).Assembly.GetType("Tutorial.TutorialAuthoringRules");
                Check(rules != null, "runtime tutorial authoring rules available");
                var level = ScriptableObject.CreateInstance<LevelDefinition>(); owned.Add(level);
                var flow = ScriptableObject.CreateInstance<TutorialFlowDefinition>(); owned.Add(flow);
                flow.steps.Add(new TutorialStepDefinition { authoringId = "step", conditions = new List<TutorialConditionDefinition>
                { new TutorialConditionDefinition { authoringId = "condition", requiredCount = 2 } } });
                flow.parameters.Add(new TutorialFlowParameter { key = "count", stepId = "step", conditionId = "condition", field = TutorialFlowField.RequiredCount });
                void Call(string name, params object[] args) => rules.GetMethod(name).Invoke(null, args);
                Call("Connect", level, flow);
                level.Tutorial.bindings[0].number = 5;
                Call("Synchronize", level);
                Check(level.Tutorial.bindings[0].number == 5 && flow.steps[0].conditions[0].requiredCount == 2, "sync preserves level value without modifying shared flow");
                string before = JsonUtility.ToJson(level);
                flow.parameters.Add(new TutorialFlowParameter { key = "bad", stepId = "missing" });
                try { Call("Connect", level, flow); } catch (TargetInvocationException) { }
                Check(before == JsonUtility.ToJson(level), "invalid connect leaves source intact");
                flow.parameters.RemoveAt(1);
                var source = new List<TutorialStepDefinition>
                {
                    new TutorialStepDefinition { authoringId = "a", conditions = new List<TutorialConditionDefinition>
                    { new TutorialConditionDefinition { authoringId = "c", kind = TutorialConditionKind.Generated, bindGeneratedAs = "rocket" } } },
                    new TutorialStepDefinition { authoringId = "b", firstBinding = "rocket", secondBinding = "rocket",
                        conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition { authoringId = "d",
                            target = new TutorialTargetDefinition { kind = TutorialTargetKind.Generated, binding = "rocket" } } } }
                };
                var copies = (List<TutorialStepDefinition>)rules.GetMethod("CopySample").Invoke(null, new object[] { source, source });
                Check(copies[0].conditions[0].bindGeneratedAs == "rocket_2" && copies[1].firstBinding == "rocket_2" &&
                    copies[1].secondBinding == "rocket_2" && copies[1].conditions[0].target.binding == "rocket_2", "sample renames all generated references together");
                Check(source[0].conditions[0].bindGeneratedAs == "rocket" && copies.Select(step => step.authoringId).Intersect(source.Select(step => step.authoringId)).Count() == 0 &&
                    copies[0].conditions[0].authoringId != source[0].conditions[0].authoringId, "sample copy is independent and allocates stable IDs");
                string sampleBefore = JsonUtility.ToJson(new TutorialSamplePayload { steps = source });
                source[1].conditions[0].bindGeneratedAs = "rocket";
                try { rules.GetMethod("CopySample").Invoke(null, new object[] { source, source }); throw new Exception("duplicate binding accepted"); }
                catch (TargetInvocationException error) { Check(error.InnerException is ArgumentException, "duplicate sample binding rejected"); }
                source[1].conditions[0].bindGeneratedAs = "";
                Check(JsonUtility.ToJson(new TutorialSamplePayload { steps = source }) == sampleBefore, "failed sample copy does not mutate input");
                var preview = rules.GetMethod("PreviewTargetHighlights");
                Check(preview != null, "shared target highlight preview exists");
                var previewLevel = TutorialSampleBoards.All.First(value => value.Id == "remaining").CreateBoard(); owned.Add(previewLevel);
                ((List<ElementPlacementDefinition>)previewLevel.Elements).Clear();
                ((List<ElementPlacementDefinition>)previewLevel.Elements).Add(new ElementPlacementDefinition { definitionId = "obstacle.metal-rod-box", layer = PlacementLayer.Obstacle, coordinate = new Board.BoardCoordinate(3, 3), durability = 3 });
                var previewStep = new TutorialStepDefinition { conditions = new List<TutorialConditionDefinition> {
                    new TutorialConditionDefinition { kind = TutorialConditionKind.DurabilityDecrease, target = new TutorialTargetDefinition {
                        kind = TutorialTargetKind.Entity, coordinate = new Board.BoardCoordinate(4, 4), layer = TutorialTargetLayer.Content } } } };
                Board.BoardCoordinate[] Highlights(bool initial) => ((IEnumerable<Board.BoardCoordinate>)preview.Invoke(null, new object[] { previewLevel, previewStep, initial })).ToArray();
                Check(Highlights(true).Length == 4, "entity inside two by two highlights whole footprint");
                Check(Highlights(false).Length == 1, "later step does not predict moved object footprint");
                previewStep.conditions[0].target.kind = TutorialTargetKind.Board;
                Check(Highlights(true).Length == 0, "whole board target never opens whole spotlight");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
            finally { foreach (var item in owned) UnityEngine.Object.DestroyImmediate(item); }
        }
        [Serializable] private sealed class TutorialSamplePayload { public List<TutorialStepDefinition> steps; }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}

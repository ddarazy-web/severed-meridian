using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Runtime;
using LevelTool;
using Tutorial;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolSharedTutorialExercise
    {
        internal static void Run(LevelToolScreen screen)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var parent = screen.Workspace.Session;
            string levelId = parent.SelectedLevelId;
            TutorialDraftEditing.EditLevel(parent, "prepare shared", level =>
            {
                level.Tutorial.flow = null; level.Tutorial.bindings.Clear(); level.Tutorial.steps.Clear();
                level.Tutorial.steps.Add(new TutorialStepDefinition { authoringId = "step", kind = TutorialStepKind.Swap, instructions = "original", conditions = new List<TutorialConditionDefinition>
                { new TutorialConditionDefinition { authoringId = "condition", kind = TutorialConditionKind.SuccessfulSwap, requiredCount = 2 } } });
            });
            typeof(LevelToolScreen).GetMethod("Refresh", flags).Invoke(screen, null);
            var root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Click(string name)
            {
                var button = root.Q<Button>(name) ?? throw new Exception("Missing shared button " + name);
                typeof(Clickable).GetMethod("Invoke", flags).Invoke(button.clickable, new object[] { null });
            }
            root.Q<DropdownField>("inspector-page").value = "튜토리얼";
            Check(root.Q<TextField>("tutorial-shared-name") != null, "shared authoring controls exist");
            root.Q<TextField>("tutorial-shared-name").value = "shared test"; Click("tutorial-create-flow");
            string flowId = (string)parent.Get(levelId).Data["tutorial"]["flowId"];
            Check(flowId != null && root.Q("tutorial-flow-usage") != null, "shared creation connects and shows usage");
            Click("tutorial-edit-flow");
            root.Q<TextField>("tutorial-instructions").value = "draft text";
            Check((string)parent.Get(flowId).Data["steps"][0]["instructions"] == "original", "shared UI changes stay in draft");
            Click("tutorial-add-parameter");
            var fields = root.Q<DropdownField>("tutorial-parameter-field-0");
            Check(fields.choices.Count == 12, "all twelve shared parameter fields selectable");
            fields.value = "조건 횟수·감소량";
            root.Q<TextField>("tutorial-parameter-key-0").value = "count";
            Click("tutorial-apply-flow"); Click("tutorial-sync");
            Check((string)parent.Get(flowId).Data["steps"][0]["instructions"] == "draft text", "explicit UI apply updates shared source");
            root.Q<IntegerField>("tutorial-binding-number-count").value = 3;
            Check((int)parent.Get(levelId).Data["tutorial"]["bindings"][0]["number"] == 3 &&
                (int)parent.Get(flowId).Data["steps"][0]["conditions"][0]["requiredCount"] == 2, "level parameter does not change shared default");
            Click("tutorial-edit-flow"); root.Q<TextField>("tutorial-instructions").value = "discard me";
            int left = 0; screen.RequestLeave(() => left++); Click("cancel");
            Check(left == 0 && root.Q<TextField>("tutorial-instructions").value == "discard me", "cancel exit keeps shared draft");
            screen.RequestLeave(() => left++); Click("discard-flow-and-leave"); Click("cancel");
            Check(left == 0 && root.Q<Button>("tutorial-edit-flow") != null && root.Q<Button>("tutorial-apply-flow") == null, "cancel parent exit after discarding draft restores parent UI");
            Check((string)parent.Get(flowId).Data["steps"][0]["instructions"] == "draft text", "discarding shared draft preserves source");
            Click("tutorial-detach");
            Check((int)parent.Get(levelId).Data["tutorial"]["steps"][0]["conditions"][0]["requiredCount"] == 3,
                "UI detach expands level-specific value");
            string allFlow = TutorialDraftEditing.CreateFlow(parent, "all fields");
            TutorialDraftEditing.EditFlow(parent, allFlow, "all parameters", flow =>
            {
                foreach (TutorialFlowField field in Enum.GetValues(typeof(TutorialFlowField)))
                    flow.parameters.Add(new TutorialFlowParameter { key = field.ToString(), field = field, stepId = flow.steps[0].authoringId,
                        conditionId = flow.steps[0].conditions[0].authoringId });
            });
            typeof(LevelToolScreen).GetMethod("Refresh", flags).Invoke(screen, null); Click("tutorial-sync");
            string sharedBefore = parent.Get(allFlow).Data.ToString();
            Newtonsoft.Json.Linq.JToken Value(string key) => parent.Get(levelId).Data["tutorial"]["bindings"].Single(value => (string)value["key"] == key);
            foreach (TutorialFlowField field in Enum.GetValues(typeof(TutorialFlowField)))
            {
                string key = field.ToString(); parent.SelectCells(new[] { 42, 43 });
                switch (field)
                {
                    case TutorialFlowField.First:
                    case TutorialFlowField.Second:
                        parent.SelectCells(new[] { 42 }); Click("tutorial-binding-cell-" + key);
                        Check((int)Value(key)["coordinate"]["row"] == 4 && (int)Value(key)["coordinate"]["column"] == 6, "coordinate binding " + key); break;
                    case TutorialFlowField.ActionArea:
                    case TutorialFlowField.Highlights:
                        Click("tutorial-binding-area-" + key); Check(Value(key)["cells"].Count() == 2, "area binding " + key); break;
                    case TutorialFlowField.FreeItemCount:
                    case TutorialFlowField.RequiredCount:
                    case TutorialFlowField.MatchSize:
                        root.Q<IntegerField>("tutorial-binding-number-" + key).value = 4; Check((int)Value(key)["number"] == 4, "number binding " + key); break;
                    case TutorialFlowField.Color:
                        root.Q<DropdownField>("tutorial-binding-color-" + key).value = "Type2"; Check((string)Value(key)["color"] == "Type2", "color binding"); break;
                    case TutorialFlowField.PowerDefinitionId:
                    case TutorialFlowField.ActionDefinitionId:
                        var power = root.Q<DropdownField>("tutorial-binding-power-" + key); power.value = power.choices[1];
                        Check(!string.IsNullOrEmpty((string)Value(key)["definitionId"]), "power binding " + key); break;
                    case TutorialFlowField.MissionIndex:
                        root.Q<DropdownField>("tutorial-binding-mission-" + key).value = "선택하세요";
                        var mission = root.Q<DropdownField>("tutorial-binding-mission-" + key); mission.value = mission.choices[1];
                        Check((int)Value(key)["number"] == 0, "mission binding"); break;
                    case TutorialFlowField.Target:
                        root.Q<DropdownField>("tutorial-target-kind-" + (int)field).value = "선택 영역";
                        Click("tutorial-target-pick-" + (int)field);
                        Check((string)Value(key)["target"]["kind"] == "Area" && Value(key)["target"]["cells"].Count() == 2, "condition target binding"); break;
                }
            }
            Check(parent.Get(allFlow).Data.ToString() == sharedBefore, "all twelve binding edits preserve shared source");
            foreach (string key in new[] { "First", "Second", "ActionArea", "Highlights", "Target" })
            {
                Click("tutorial-binding-pick-" + key);
                typeof(LevelToolScreen).GetMethod("UseTutorialPicker", flags).Invoke(screen, new object[] { 30 });
                Click("tutorial-pick-confirm");
                var value = Value(key);
                Check(key == "First" || key == "Second" ? (int)value["coordinate"]["row"] == 3 :
                    key == "Target" ? value["target"]["cells"].Count() == 1 : value["cells"].Count() == 1, "confirmed binding board selection " + key);
            }
            Check(parent.Get(allFlow).Data.ToString() == sharedBefore, "confirmed level picks preserve shared source");
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}

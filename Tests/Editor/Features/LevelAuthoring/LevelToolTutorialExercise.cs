using System;
using System.Linq;
using System.Reflection;
using LevelTool;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolTutorialExercise
    {
        internal static void Run(LevelToolScreen screen)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            VisualElement root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Click(string name)
            {
                Button value = root.Q<Button>(name) ?? throw new Exception("Missing tutorial button " + name);
                typeof(Clickable).GetMethod("Invoke", flags).Invoke(value.clickable, new object[] { null });
            }
            var session = screen.Workspace.Session;
            string id = session.SelectedLevelId, original = session.Get(id).Data.ToString();
            var page = root.Q<DropdownField>("inspector-page");
            Check(page.choices.Contains("튜토리얼"), "tutorial inspector exists"); page.value = "튜토리얼";
            int previous = session.Get(id).Data["tutorial"]["steps"].Count();
            Click("tutorial-add-step");
            Check(session.Get(id).Data["tutorial"]["steps"].Count() == previous + 1, "step added through runtime UI");
            root.Q<TextField>("tutorial-instructions").value = "블록 두 칸을 바꿔 주세요";
            Check((string)session.Get(id).Data["tutorial"]["steps"].Last["instructions"] == "블록 두 칸을 바꿔 주세요", "step instructions editable");
            root.Q<DropdownField>("tutorial-kind").value = "교환";
            session.SelectCells(new[] { 20, 21 }); typeof(LevelToolScreen).GetMethod("Refresh", flags).Invoke(screen, null);
            Click("tutorial-set-action");
            var step = session.Get(id).Data["tutorial"]["steps"].Last;
            Check((bool)step["hasFirst"] && (bool)step["hasSecond"] && (int)step["first"]["row"] == 2 && (int)step["second"]["column"] == 3, "selected board cells become action coordinates");
            root.Q<DropdownField>("tutorial-new-condition").value = "매칭 조건"; Click("tutorial-add-condition");
            step = session.Get(id).Data["tutorial"]["steps"].Last;
            Check((string)step["conditions"].Last["kind"] == "Match", "registered matching condition added");
            root.Q<IntegerField>("tutorial-condition-count-0").value = 2;
            Check((int)session.Get(id).Data["tutorial"]["steps"].Last["conditions"][0]["requiredCount"] == 2, "condition count editable");
            string stepId = (string)step["authoringId"];
            Click("tutorial-copy-step");
            var steps = session.Get(id).Data["tutorial"]["steps"];
            Check((string)steps.Last["authoringId"] != stepId && steps.Last["conditions"].Count() == 1, "step copied independently");
            Click("tutorial-remove-step");
            string[] conditionLabels = { "교환 횟수", "내구도 줄이기", "남은 내구도", "지정 원인으로 제거", "파워 생성", "직접 단독 발동", "파워 조합", "아이템 사용 성공", "미션 진행 증가" };
            string[] conditionKinds = { "SuccessfulSwap", "DurabilityDecrease", "RemainingDurability", "Removed", "Generated", "Activated", "Combined", "ItemUsed", "MissionProgress" };
            for (int i = 0; i < conditionLabels.Length; i++)
            {
                root.Q<DropdownField>("tutorial-new-condition").value = conditionLabels[i]; Click("tutorial-add-condition");
                Check((string)session.Get(id).Data["tutorial"]["steps"].Last["conditions"].Last["kind"] == conditionKinds[i], "condition UI supports " + conditionKinds[i]);
                if (conditionKinds[i] == "RemainingDurability")
                    Check(root.Q<DropdownField>("tutorial-aggregation-1") == null && root.Q<IntegerField>("tutorial-condition-count-1").label.Contains("정확히"), "remaining durability only exposes exact value");
                if (conditionKinds[i] == "Generated")
                {
                    root.Q<TextField>("tutorial-generated-binding-1").value = "rocket-next";
                    root.Q<DropdownField>("tutorial-target-kind-1").value = "선택 영역";
                    Click("tutorial-target-pick-1");
                    var generated = session.Get(id).Data["tutorial"]["steps"].Last["conditions"].Last;
                    Check((string)generated["bindGeneratedAs"] == "rocket-next" && generated["target"]["cells"].Count() == 2, "generated binding and selected target region edited");
                }
                if (conditionKinds[i] == "MissionProgress")
                {
                    var mission = root.Q<DropdownField>("tutorial-mission-1");
                    Check(mission.value == "선택하세요", "unset mission does not display a false selection");
                    if (mission.choices.Count > 1)
                    {
                        mission.value = mission.choices[1];
                        var condition = session.Get(id).Data["tutorial"]["steps"].Last["conditions"].Last;
                        Check((int)condition["missionIndex"] == 0 && (string)condition["missionKind"] == (string)session.Get(id).Data["missions"][0]["kind"], "mission selection stores index and identity");
                    }
                }
                Click("tutorial-remove-condition-1");
            }
            var preview = root.Q<Toggle>("tutorial-preview");
            Check(preview != null, "tutorial spotlight preview control exists");
            string beforePreview = session.ExportState();
            preview.value = true;
            Check(root.Q("tutorial-preview-overlay") != null && root.Q("tutorial-dim-0") != null, "tutorial preview dims outside action cells");
            var previewStep = session.Get(id).Data["tutorial"]["steps"].Last;
            int firstCell = (int)previewStep["first"]["row"] * 9 + (int)previewStep["first"]["column"];
            Check(root.Q("tutorial-dim-" + firstCell) == null && root.Q("tutorial-preview-overlay").pickingMode == PickingMode.Ignore,
                "highlighted action remains transparent and board input stays available");
            var view = (Newtonsoft.Json.Linq.JObject)typeof(LevelToolScreen).GetMethod("CaptureView", flags).Invoke(screen, null);
            Check((bool)view["tutorialPreview"], "preview setting included in recovery view");
            Check(session.ExportState() == beforePreview, "tutorial preview does not edit source");
            preview = root.Q<Toggle>("tutorial-preview"); preview.value = false;
            Check(root.Q("tutorial-preview-overlay") == null, "tutorial preview can be hidden");
            string beforePicking = session.ExportState();
            Click("tutorial-pick-action");
            Check(root.Q("tutorial-pick-panel") != null && !root.Q<Button>("save").enabledInHierarchy, "picking locks other edits until confirm or cancel");
            var pick = typeof(LevelToolScreen).GetMethod("UseTutorialPicker", flags);
            pick.Invoke(screen, new object[] { 30 }); pick.Invoke(screen, new object[] { 31 });
            Check(session.ExportState() == beforePicking, "pending picks do not change document or undo history");
            Click("tutorial-pick-cancel");
            Check(session.ExportState() == beforePicking && root.Q("tutorial-pick-panel") == null, "cancel preserves original selection and content");
            Click("tutorial-pick-action");
            pick.Invoke(screen, new object[] { 30 }); pick.Invoke(screen, new object[] { 31 });
            view = (Newtonsoft.Json.Linq.JObject)typeof(LevelToolScreen).GetMethod("CaptureView", flags).Invoke(screen, null);
            Check(view["tutorialPick"]["cells"].Count() == 2, "pending selection serialized for reload");
            pick.Invoke(screen, new object[] { 32 });
            Click("tutorial-pick-confirm");
            Check(root.Q("tutorial-pick-panel") != null && session.ExportState() == beforePicking, "invalid three-cell action remains pending without edits");
            pick.Invoke(screen, new object[] { 32 });
            Click("tutorial-pick-confirm");
            var picked = session.Get(id).Data["tutorial"]["steps"].Last;
            Check((int)picked["first"]["row"] == 3 && (int)picked["first"]["column"] == 3 && (int)picked["second"]["column"] == 4,
                "confirmed action preserves click order: " + root.Q<Label>("status").text + " / " + picked["first"] + " / " + picked["second"]);
            Click("undo");
            Check(session.Get(id).Data["tutorial"]["steps"].Last.ToString() == previewStep.ToString(), "picking applies one undo transaction");
            while (session.CanUndo) session.Undo();
            Check(session.Get(id).Data.ToString() == original, "tutorial UI undo restores original document");
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}

using System;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Runtime;
using LevelTool;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolTutorialSamplesExercise
    {
        internal static void Run(LevelToolScreen screen)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var session = screen.Workspace.Session; string id = session.SelectedLevelId;
            string original = session.Get(id).Data.ToString();
            TutorialDraftEditing.EditLevel(session, "prepare sample", level => { level.Tutorial.flow = null; level.Tutorial.bindings.Clear(); level.Tutorial.steps.Clear(); });
            session.SelectCells(new[] { 20, 21 });
            typeof(LevelToolScreen).GetMethod("Refresh", flags).Invoke(screen, null);
            var root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Click(string name)
            {
                if (name == "validate-level" || name == "replay-tutorial")
                {
                    Click("menu-test"); name = name == "validate-level" ? "command-Validate" : "command-ReplayTutorial";
                }
                var button = root.Q<Button>(name) ?? throw new Exception("Missing sample button " + name);
                typeof(Clickable).GetMethod("Invoke", flags).Invoke(button.clickable, new object[] { null });
            }
            root.Q<DropdownField>("inspector-page").value = "튜토리얼";
            Click("tutorial-quick-match");
            var first = session.Get(id).Data["tutorial"]["steps"][0];
            Check((string)first["conditions"][0]["kind"] == "Match" && (int)first["first"]["row"] == 2, "selected cells create reusable three-match step");
            Click("tutorial-quick-swap");
            var sourceStep = root.Q<DropdownField>("tutorial-sample-step"); sourceStep.value = sourceStep.choices[0];
            root.Q<TextField>("tutorial-sample-name").value = "test sample";
            int sampleCount = session.Documents.Count(doc => doc.Kind == "tutorialSample");
            Click("tutorial-save-step-sample");
            Check(session.Documents.Count(doc => doc.Kind == "tutorialSample") == sampleCount + 1, "current step saved as JSON sample");
            string sampleId = session.Documents.Single(doc => doc.Kind == "tutorialSample" && (string)doc.Data["displayName"] == "test sample").Id;
            string source = session.Get(sampleId).Data.ToString();
            Check(session.Get(sampleId).Data["steps"].Count() == 1 && (string)session.Get(sampleId).Data["steps"][0]["conditions"][0]["kind"] == "Match", "sample selector stores chosen step rather than last step");
            Check(root.Q<Label>("tutorial-sample-preview").text.Contains("표시된 두 블록"), "sample preview describes selected sample");
            Click("tutorial-apply-sample");
            var steps = session.Get(id).Data["tutorial"]["steps"];
            Check(steps.Count() == 3 && (string)steps[0]["authoringId"] != (string)steps[2]["authoringId"], "sample button appends independent steps");
            root.Q<TextField>("tutorial-instructions").value = "new text";
            Check(session.Get(sampleId).Data.ToString() == source, "editing applied sample does not mutate library");
            session.SelectCells(new[] { 40 }); typeof(LevelToolScreen).GetMethod("Refresh", flags).Invoke(screen, null);
            Click("tutorial-add-condition-sample");
            var condition = session.Get(id).Data["tutorial"]["steps"].Last["conditions"].Last;
            Check((string)condition["kind"] == "DurabilityDecrease" && (string)condition["target"]["kind"] == "Entity" && (int)condition["target"]["coordinate"]["row"] == 4, "condition sample uses selected entity");
            int levelCount = session.Documents.Count(doc => doc.Kind == "level"); string beforeBoard = session.Get(id).Data.ToString();
            Click("tutorial-add-sample-board");
            Check(session.Documents.Count(doc => doc.Kind == "level") == levelCount + 1 && session.SelectedLevelId != id && session.Get(id).Data.ToString() == beforeBoard, "test board creates a separate editable level");
            string checkBefore = session.ExportState();
            Click("validate-level");
            Check(root.Q<Label>("validation-summary").text.Contains("오류 0"), "sample board structural validation passes");
            Click("replay-tutorial");
            Check(root.Q<Label>("validation-summary").text.Contains("오류 0"), "sample tutorial logical replay passes");
            Check(session.ExportState() == checkBefore, "validation and replay preserve complete edit state");
            TutorialDraftEditing.EditLevel(session, "invalid target", level =>
            {
                level.Tutorial.steps.Add(new Tutorial.TutorialStepDefinition {
                    authoringId = Guid.NewGuid().ToString("N"), kind = Tutorial.TutorialStepKind.Swap,
                    first = new Board.BoardCoordinate(0, 0), second = new Board.BoardCoordinate(8, 8) });
            });
            typeof(LevelToolScreen).GetMethod("Refresh", flags).Invoke(screen, null);
            Check(root.Q<Label>("validation-summary").text.Contains("다시 검사"), "edited results marked stale");
            Click("replay-tutorial");
            Button issueButton = root.Query<Button>().ToList().FirstOrDefault(button => button.name.StartsWith("validation-issue-") && button.tooltip.Contains("steps.Array.data[1]"));
            Check(issueButton != null, "invalid tutorial reports exact step path");
            typeof(Clickable).GetMethod("Invoke", flags).Invoke(issueButton.clickable, new object[] { null });
            Check(root.Q<DropdownField>("inspector-page").value == "튜토리얼" && (int)typeof(LevelToolScreen).GetField("tutorialStep", flags).GetValue(screen) == 1, "issue button selects tutorial step");
            session.Undo();
            foreach (string sample in new[] { "match", "two", "follow" })
            {
                TutorialDraftEditing.AddSampleBoard(session, sample);
                typeof(LevelToolScreen).GetMethod("Refresh", flags).Invoke(screen, null);
                string beforeReplay = session.ExportState();
                Click("replay-tutorial");
                Check(root.Q<Label>("validation-summary").text.Contains("오류 0"), "logical replay through UI " + sample);
                Check(session.ExportState() == beforeReplay, "replay preserves source and history " + sample);
            }
            var location = new Levels.LevelValidationIssue(Levels.LevelValidationCode.InvalidTutorial, "위치 확인", "tutorial.steps.Array.data[0]", new Board.BoardCoordinate(2, 3));
            typeof(LevelToolScreen).GetMethod("FocusValidationIssue", flags).Invoke(screen, new object[] { session.SelectedLevelId, location });
            Check(session.SelectedCells.SequenceEqual(new[] { 21 }), "issue location selects exact board cell");
            while (session.CanUndo) session.Undo();
            Check(session.Get(id).Data.ToString() == original && session.Documents.Count(doc => doc.Kind == "tutorialSample") == sampleCount, "sample and board operations undo without changing source");
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}

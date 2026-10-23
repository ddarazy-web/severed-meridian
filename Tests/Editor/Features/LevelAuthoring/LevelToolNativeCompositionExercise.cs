using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Storage;
using LevelAuthoring.Runtime;
using LevelTool;
using Tutorial;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolNativeCompositionExercise
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static async UniTask Run(LevelToolScreen screen, RenderTexture texture, PanelSettings panel, string isolation)
        {
            VisualElement root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Call(string name, params object[] args) => typeof(LevelToolScreen).GetMethod(name, Flags).Invoke(screen, args);
            void Check(bool value, string message) { if (!value) throw new Exception("FAIL native-composition " + message); Debug.Log("PASS native-composition " + message); }
            void Click(string name)
            {
                Button button = root.Q<Button>(name); Check(button != null && button.enabledInHierarchy, "enabled " + name);
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
            }
            var fixture = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt").Trim()).Read().Snapshot;
            screen.Workspace.Create(Path.Combine(isolation, "CompositionProject"), fixture);
            var session = screen.Workspace.Session;
            Call("Refresh"); await UniTask.DelayFrame(20);
            string Summary(TutorialStepDefinition step) => (string)typeof(LevelToolScreen).GetMethod("TutorialStepSummary", Flags).Invoke(screen, new object[] { step });
            var hammerSummary = Summary(new TutorialStepDefinition { kind = TutorialStepKind.Item, item = Simulation.BoardItem.Hammer, hasFirst = true, first = new Board.BoardCoordinate(0, 0) });
            Check(hammerSummary.Contains("망치") && !hammerSummary.Contains("둘째 칸"), "hammer summary describes one target without a missing second cell");
            var shuffleSummary = Summary(new TutorialStepDefinition { kind = TutorialStepKind.Item, item = Simulation.BoardItem.Shuffle });
            Check(shuffleSummary.Contains("섞기") && !shuffleSummary.Contains("미지정"), "shuffle summary does not require board coordinates");
            var summaryStep = new TutorialStepDefinition { kind = TutorialStepKind.Swap, hasFirst = true, hasSecond = true };
            summaryStep.conditions.Add(new TutorialConditionDefinition { kind = TutorialConditionKind.DurabilityDecrease, aggregation = TutorialDamageAggregation.Each,
                target = new TutorialTargetDefinition { layer = TutorialTargetLayer.Cover } });
            Check(Summary(summaryStep).Contains("각 대상") && Summary(summaryStep).Contains("덮개"), "damage summary exposes aggregation and target layer");
            summaryStep.conditions[0] = new TutorialConditionDefinition { kind = TutorialConditionKind.Match, anyColor = false, color = Levels.RabbitColor.Type1 };
            Check(Summary(summaryStep).Contains("분홍"), "match summary exposes required color");
            summaryStep.conditions[0] = new TutorialConditionDefinition { kind = TutorialConditionKind.Generated, anyDirection = false, rocketDirection = Levels.RocketDirection.Vertical };
            Check(Summary(summaryStep).Contains("세로"), "power summary exposes required direction");
            string before = session.ExportState();
            Click("creation-guide");
            Check(root.Q<Label>("checklist-play-status").text.Contains("필요"), "configuration does not claim actual play");
            Click("checklist-settings"); await UniTask.DelayFrame(2);
            Check(root.Q("level-settings").resolvedStyle.display != DisplayStyle.None, "checklist opens level settings");
            Check(session.ExportState() == before, "checklist navigation preserves documents and history");
            int reads = (int)typeof(LevelToolScreen).GetField("displayDocumentReadCount", Flags).GetValue(screen);
            for (int i = 0; i < 50; i++) Call("Refresh");
            Check((int)typeof(LevelToolScreen).GetField("displayDocumentReadCount", Flags).GetValue(screen) == reads, "checklist refresh does not read level bodies");
            session.EditLevel("검사용 미션", level => level["missions"] = new Newtonsoft.Json.Linq.JArray()); Call("Refresh");
            Click("add-mission"); await UniTask.DelayFrame(30);
            Check(root.Q<Image>("mission-preview").sprite != null, "mission draft uses actual project sprite");
            root.Q<IntegerField>("mission-count").value = 30;
            Click("apply-mission"); await UniTask.DelayFrame(20);
            Check(root.Q<Image>("mission-image-0").sprite != null, "mission summary uses actual project sprite");
            Click("creation-guide");
            Check(root.Q<Label>("checklist-save-status").text.Contains("미저장"), "guide distinguishes unsaved edit");
            Click("checklist-close");
            string tutorialBaseline = session.Get(session.SelectedLevelId).Data.ToString();
            Call("SelectWorkspacePage", "튜토리얼");
            Click("tutorial-begin-work");
            bool settingsBeforeGuard = (bool)typeof(LevelToolScreen).GetField("levelSettingsVisible", Flags).GetValue(screen);
            Click("creation-guide"); Click("checklist-board");
            Check(root.Q("tutorial-continue-work") != null && (bool)typeof(LevelToolScreen).GetField("levelSettingsVisible", Flags).GetValue(screen) == settingsBeforeGuard,
                "creation guide does not change panels before tutorial exit decision");
            Click("tutorial-continue-work");
            session.EditLevel("검사용 튜토리얼 변경", level => level["tutorial"]["completionId"] = "cancel-work"); Call("Refresh");
            Call("SelectWorkspacePage", "시험 기록");
            Check(root.Q("tutorial-continue-work") != null && root.Q("tutorial-discard-work") != null, "leaving tutorial offers continue or whole-work cancel");
            Click("tutorial-continue-work");
            Check((string)session.Get(session.SelectedLevelId).Data["tutorial"]["completionId"] == "cancel-work", "continue retains tutorial work");
            Call("SelectWorkspacePage", "시험 기록"); Click("tutorial-discard-work");
            Check(session.Get(session.SelectedLevelId).Data.ToString() == tutorialBaseline, "whole-work cancellation restores selected level exactly");
            Call("SelectWorkspacePage", "튜토리얼"); Click("tutorial-begin-work");
            Click("save");
            Check(root.Q("tutorial-continue-work") != null, "save requires completing or cancelling tutorial work first");
            Click("tutorial-continue-work");
            TutorialDraftEditing.EditLevel(session, "불완전 단계", level =>
            {
                level.Tutorial.flow = null; level.Tutorial.steps.Clear();
                level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Swap, instructions = "교환 안내" });
            }); Call("Refresh");
            Click("tutorial-work-complete");
            Check(root.Q<Label>("status").text.Contains("확인 필요") && root.Q("tutorial-work-bar").style.display.value != DisplayStyle.None, "invalid required values block completion");
            TutorialDraftEditing.EditLevel(session, "설명 단계", level =>
            {
                level.Tutorial.steps.Clear();
                level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Description, instructions = "블록을 교환해 봐요." });
            }); Call("Refresh"); Click("tutorial-work-complete");
            Check(root.Q("tutorial-work-bar").style.display.value == DisplayStyle.None && session.IsDirty, "valid completion stays in memory without saving");
            Check(root.Q<Label>("status").text.Contains("플레이 확인 필요"), "unplayed completed tutorial remains explicitly unverified");
            Click("tutorial-begin-work");
            int stepsBefore = session.Get(session.SelectedLevelId).Data["tutorial"]["steps"].Count();
            Click("tutorial-purpose-match"); Call("UseTutorialPicker", 0);
            Check(root.Q<Label>("status").text.Contains("고정 블록"), "first action rejects random/empty initial cells with explanation");
            Click("tutorial-work-complete");
            Check(typeof(LevelToolScreen).GetField("tutorialWork", Flags).GetValue(screen) != null, "unfinished purpose selection blocks work completion");
            Call("CancelTutorialPick");
            Check(typeof(LevelToolScreen).GetField("purposeDraft", Flags).GetValue(screen) == null, "cancelled board selection discards the unadded purpose draft");
            var definitions = LevelToolDocuments.Definitions(session);
            string normal = definitions.First(pair => (string)pair.Value["supply"]?["behavior"] == "FixedNormal").Key;
            session.EditLevel("첫 동작 고정 블록", level =>
            {
                LevelAuthoring.Editing.LevelDocumentEditing.Place(level, definitions, normal, 0, 0, true);
                LevelAuthoring.Editing.LevelDocumentEditing.Place(level, definitions, normal, 0, 1, true);
            }); Call("Refresh");
            Click("tutorial-purpose-match");
            Check(root.Q("tutorial-valid-cell-0") != null && root.Q("tutorial-valid-cell-80") == null, "picker outlines only valid first action cells");
            Call("UseTutorialPicker", 0); Call("UseTutorialPicker", 1);
            Check(root.Q<IntegerField>("purpose-count") != null, "two selected cells automatically open required settings");
            Check(session.Get(session.SelectedLevelId).Data["tutorial"]["steps"].Count() == stepsBefore, "purpose wizard is a draft until Apply");
            root.Q<IntegerField>("purpose-count").value = 0;
            Check(!root.Q<Button>("purpose-apply").enabledSelf, "invalid purpose count blocks Apply");
            root.Q<IntegerField>("purpose-count").value = 1;
            Click("purpose-apply");
            var created = session.Get(session.SelectedLevelId).Data["tutorial"]["steps"].Last();
            Check((string)created["conditions"][0]["kind"] == "Match" && (bool)created["hasFirst"] && (bool)created["hasSecond"], "match purpose composes existing action and condition data");
            Click("tutorial-edit-step-1");
            session.EditLevel("안내문 수정", level => level["tutorial"]["steps"][1]["instructions"] = "수정된 매칭 안내"); Call("Refresh");
            Check(root.Q<Label>("tutorial-summary-1").text.Contains("수정된 매칭 안내"), "retained instruction edit refreshes the visible summary");
            session.EditLevel("직접 선택 검사 배치", level => LevelAuthoring.Editing.LevelDocumentEditing.Place(level, definitions, normal, 1, 1, true)); Call("Refresh");
            string directBefore = session.ExportState();
            Call("BeginTutorialPick", "action", null, null, null);
            Check(root.Q("tutorial-valid-cell-0") != null, "direct picker highlights valid action cells");
            Call("UseTutorialPicker", 0); Call("UseTutorialPicker", 10);
            Check(root.Q("direct-pick-apply") == null && root.Q<Label>("status").text.Contains("붙어"), "direct swap rejects nonadjacent second cell");
            Call("UseTutorialPicker", 1);
            Check(root.Q("direct-pick-apply") != null && session.ExportState() == directBefore, "direct fixed selection opens required settings without editing");
            root.Q<TextField>("direct-pick-instructions").value = "직접 선택 초안 유지";
            screen.enabled = false; await UniTask.DelayFrame(2); screen.enabled = true; await UniTask.DelayFrame(10);
            root = screen.GetComponent<UIDocument>().rootVisualElement;
            Check(root.Q<TextField>("direct-pick-instructions")?.value == "직접 선택 초안 유지" && session.ExportState() == directBefore,
                "direct required modal survives screen reactivation without committing");
            root.Q<TextField>("direct-pick-instructions").value = "";
            Check(!root.Q<Button>("direct-pick-apply").enabledSelf, "direct settings require an instruction before apply");
            Click("direct-pick-cancel");
            Check(session.ExportState() == directBefore, "direct modal cancellation preserves document and undo");
            Click("tutorial-purpose-generate"); Call("UseTutorialPicker", 0); Call("UseTutorialPicker", 1);
            root.Q<TextField>("purpose-binding").value = "새 파워"; Click("purpose-apply");
            Click("tutorial-purpose-use");
            Click("purpose-use-generated");
            Call("UseTutorialPicker", 1);
            Check(root.Q("purpose-apply") != null, "generated-result action needs only the remaining second cell");
            Click("purpose-apply");
            var linked = session.Get(session.SelectedLevelId).Data["tutorial"]["steps"].Last();
            Check((string)linked["firstBinding"] == "새 파워" && (string)linked["conditions"][0]["target"]["kind"] == "Generated", "previous result is linked only after explicit choice");
            string linkedBefore = session.Get(session.SelectedLevelId).Data.ToString();
            Call("BeginTutorialPick", "action", null, null, null); Call("UseTutorialPicker", 0);
            Check(root.Q("direct-pick-apply") != null, "direct linked action selects only the remaining fixed cell");
            Click("direct-pick-apply");
            var repicked = session.Get(session.SelectedLevelId).Data["tutorial"]["steps"].Last();
            Check((string)repicked["firstBinding"] == "새 파워" && (int)repicked["second"]["column"] == 0,
                "direct action edit preserves generated binding and changes only the fixed cell");
            Click("undo");
            Check(session.Get(session.SelectedLevelId).Data.ToString() == linkedBefore, "direct settings and selection undo together");
            Call("BeginTutorialPick", "highlights", null, null, null); Call("UseTutorialPicker", 0); Call("UseTutorialPicker", 1);
            Check(root.Q("direct-pick-apply") == null, "free highlight area requires explicit selection completion");
            Call("ConfirmTutorialPick"); Click("direct-pick-cancel");
            Check(session.Get(session.SelectedLevelId).Data.ToString() == linkedBefore, "free selection cancellation preserves data");
            var originalSteps = session.Get(session.SelectedLevelId).Data["tutorial"]["steps"].DeepClone();
            int originalStepIndex = (int)typeof(LevelToolScreen).GetProperty("tutorialStep", Flags).GetValue(screen);
            session.EditLevel("망치 선택 검사", level =>
            {
                var itemStep = level["tutorial"]["steps"].Last();
                itemStep["kind"] = "Item"; itemStep["item"] = "Hammer"; itemStep["firstBinding"] = ""; itemStep["secondBinding"] = "";
                itemStep["hasSecond"] = false;
            }); Call("Refresh");
            Call("BeginTutorialPick", "action", null, null, null); Call("UseTutorialPicker", 0);
            Check(root.Q("direct-pick-apply") != null, "direct hammer opens settings after one cell"); Click("direct-pick-cancel");
            session.EditLevel("섞기 선택 검사", level => { level["tutorial"]["steps"].Last()["item"] = "Shuffle"; level["tutorial"]["steps"].Last()["hasFirst"] = false; }); Call("Refresh");
            Call("BeginTutorialPick", "action", null, null, null);
            Check(typeof(LevelToolScreen).GetField("tutorialPick", Flags).GetValue(screen) == null && root.Q("direct-pick-apply") == null,
                "shuffle does not request an irrelevant board cell");
            session.EditLevel("선택 검사 원복", level => level["tutorial"]["steps"] = originalSteps.DeepClone());
            typeof(LevelToolScreen).GetProperty("tutorialStep", Flags).SetValue(screen, originalStepIndex); Call("Refresh");
            var beforePowerPick = session.Get(session.SelectedLevelId).Data;
            string powerId = definitions.First(pair => (string)pair.Value["supply"]?["behavior"] == "Power").Key;
            session.EditLevel("첫 파워 선택 배치", level => LevelAuthoring.Editing.LevelDocumentEditing.Place(level, definitions, powerId, 1, 0, true));
            TutorialDraftEditing.EditLevel(session, "첫 파워 선택 단계", level =>
            {
                level.Tutorial.steps.Clear(); level.Tutorial.steps.Add(new TutorialStepDefinition
                { kind = TutorialStepKind.PowerSwap, actionDefinitionId = powerId, instructions = "파워를 바꿔 보세요." });
            });
            typeof(LevelToolScreen).GetProperty("tutorialStep", Flags).SetValue(screen, 0); Call("Refresh");
            Call("BeginTutorialPick", "action", null, null, null);
            Check(root.Q("tutorial-valid-cell-9") != null && root.Q("tutorial-valid-cell-0") == null, "first power action highlights the specified power only");
            Call("UseTutorialPicker", 9); Call("UseTutorialPicker", 0); Click("direct-pick-cancel");
            session.EditLevel("첫 파워 검사 원복", level => { foreach (var property in beforePowerPick.Properties()) level[property.Name] = property.Value.DeepClone(); });
            typeof(LevelToolScreen).GetProperty("tutorialStep", Flags).SetValue(screen, originalStepIndex); Call("Refresh");
            MethodInfo through = typeof(JsonPuzzlePlayAdapter).GetMethod("CreateTutorialRequest");
            Check(through != null, "through-step trial has an isolated request builder");
            string beforeTrial = session.Get(session.SelectedLevelId).Data.ToString();
            var request = (GameScreen.PuzzlePlayRequest)through.Invoke(null, new object[] { session.CreateTrialSnapshot(new[] { session.SelectedLevelId }), session.SelectedLevelId, 12345, 1 });
            var trialDefinition = request.CreateDefinition();
            try
            {
                Check(trialDefinition.Tutorial.steps.Count == 2 && trialDefinition.Tutorial.steps[0].instructions == "블록을 교환해 봐요.", "through-step trial keeps the first step rather than jumping to the selected step");
                Check(session.Get(session.SelectedLevelId).Data.ToString() == beforeTrial, "through-step request does not mutate authoring data");
            }
            finally { UnityEngine.Object.Destroy(trialDefinition); }
            Check(root.Q<Button>("tutorial-trial-step-1") != null && root.Q<Label>("tutorial-summary-1").text.Contains("3"), "step summaries expose values and through-step trial action");
            Click("tutorial-purpose-combine"); Call("UseTutorialPicker", 0); Call("UseTutorialPicker", 1);
            Check(root.Q<DropdownField>("purpose-second-cause") != null, "combination wizard supports at most two explicit combinations");
            root.Q<DropdownField>("purpose-second-cause").value = "자석 + 자석"; Click("purpose-apply");
            Check(session.Get(session.SelectedLevelId).Data["tutorial"]["steps"].Last()["conditions"][0]["allowedOrigins"].Count() == 2, "only the two selected combinations are accepted");
            string obstacle = definitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Obstacle" && (int?)pair.Value["placement"]?["maxDurability"] >= 3).Key;
            session.EditLevel("내구도 대상 준비", level => LevelAuthoring.Editing.LevelDocumentEditing.Place(level, definitions, obstacle, 4, 4, true)); Call("Refresh");
            Click("tutorial-purpose-damage"); Call("UseTutorialPicker", 0); Call("UseTutorialPicker", 1);
            root.Q<DropdownField>("purpose-cause").value = "로켓";
            root.Q<DropdownField>("purpose-damage-mode").index = 1;
            root.Q<IntegerField>("purpose-count").value = 2;
            root.Q<TextField>("purpose-instructions").value = "대상 내구도를 정확히 2로 만들어 보세요.";
            Click("purpose-target-board");
            Check(root.Q<IntegerField>("purpose-count").value == 2 && root.Q<TextField>("purpose-instructions").value == "대상 내구도를 정확히 2로 만들어 보세요.",
                "board target change preserves entered count and instructions");
            screen.enabled = false; await UniTask.DelayFrame(2); screen.enabled = true; await UniTask.DelayFrame(10);
            root = screen.GetComponent<UIDocument>().rootVisualElement;
            Check(root.Q<IntegerField>("purpose-count")?.value == 2 && root.Q<TextField>("purpose-instructions")?.value == "대상 내구도를 정확히 2로 만들어 보세요.",
                "reactivation restores unfinished required settings and entered values");
            Click("purpose-pick-target");
            Call("UseTutorialPicker", 0);
            Check(root.Q("purpose-apply") == null && root.Q<Label>("status").text.Contains("내구도"), "normal block cannot be selected as a durability entity");
            Check(root.Q("tutorial-valid-cell-40") != null && root.Q("tutorial-valid-cell-0") == null, "durability picker outlines damageable entities only");
            Call("UseTutorialPicker", 40); Click("purpose-apply");
            var damage = session.Get(session.SelectedLevelId).Data["tutorial"]["steps"].Last()["conditions"][0];
            Check((string)damage["kind"] == "RemainingDurability" && (int)damage["requiredCount"] == 2 && (string)damage["target"]["kind"] == "Entity" && (string)damage["allowedOrigins"][0] == "Rocket", "damage wizard preserves exact N, entity target and specified cause");
            var beforeEntitySteps = session.Get(session.SelectedLevelId).Data["tutorial"]["steps"].DeepClone();
            session.EditLevel("첫 개체 조건 선택", level => level["tutorial"]["steps"] = new Newtonsoft.Json.Linq.JArray(beforeEntitySteps.Last().DeepClone()));
            typeof(LevelToolScreen).GetProperty("tutorialStep", Flags).SetValue(screen, 0); Call("Refresh");
            Call("BeginTutorialPick", "target", (string)damage["authoringId"], null, null);
            Check(root.Q("tutorial-valid-cell-40") != null && root.Q("tutorial-valid-cell-0") == null, "direct initial entity filters incompatible normal blocks");
            Call("UseTutorialPicker", 40);
            Check(root.Q("direct-pick-apply") != null, "direct entity opens required settings after one cell"); Click("direct-pick-cancel");
            session.EditLevel("첫 개체 검사 원복", level => level["tutorial"]["steps"] = beforeEntitySteps.DeepClone());
            typeof(LevelToolScreen).GetProperty("tutorialStep", Flags).SetValue(screen, beforeEntitySteps.Count() - 1); Call("Refresh");
            session.EditLevel("연결 이름 변경", level => level["tutorial"]["steps"].SelectMany(step => step["conditions"]).First(condition => (string)condition["bindGeneratedAs"] == "새 파워")["bindGeneratedAs"] = "바뀐 이름"); Call("Refresh");
            Check(root.Q<Label>("tutorial-link-errors") != null, "broken generated link is visible without silent repair");
            Click("tutorial-work-complete");
            Check(root.Q("tutorial-work-bar").style.display.value != DisplayStyle.None, "broken link blocks completion");
            Check(session.Get(session.SelectedLevelId).Data["tutorial"]["steps"].Any(step => (string)step["firstBinding"] == "새 파워"), "broken consumer binding is not automatically rewritten");
            Click("tutorial-work-cancel"); Click("tutorial-discard-work");
            Check(root.Q<Label>("status").text.Contains("시작 상태로 복원"), "whole cancellation replaces obsolete validation error with restoration result");
            session.EditLevel("화면 검사 교환 준비", level =>
            {
                LevelAuthoring.Editing.LevelDocumentEditing.Place(level, definitions, normal, 0, 0, true);
                LevelAuthoring.Editing.LevelDocumentEditing.Place(level, definitions, normal, 0, 1, true);
            }); Call("Refresh"); Click("tutorial-begin-work");
            string output = Path.GetFullPath("Logs/NativeLevelToolStage03/screens"); Directory.CreateDirectory(output);
            foreach (Vector2Int size in new[] { new Vector2Int(1280,800), new Vector2Int(1440,900), new Vector2Int(390,844), new Vector2Int(844,390), new Vector2Int(820,1180) })
            {
                panel.targetTexture = null; texture.Release(); texture.width = size.x; texture.height = size.y; texture.Create(); panel.targetTexture = texture;
                await UniTask.DelayFrame(5);
                if (size.x < 1000) Call("ApplyMobileViewport", (Vector2)size, new Rect(0,0,size.x,size.y), new Rect());
                await UniTask.DelayFrame(5);
                if (size.x < 1000 && (string)typeof(LevelToolScreen).GetField("mobilePanel", Flags).GetValue(screen) != "properties") Call("SelectMobilePanel", "properties");
                await UniTask.DelayFrame(3);
                Capture(texture, Path.Combine(output, "composition-" + size.x + "x" + size.y + ".png"));
                Click("tutorial-purpose-match"); Call("UseTutorialPicker", 0); Call("UseTutorialPicker", 1);
                await UniTask.DelayFrame(4);
                Capture(texture, Path.Combine(output, "settings-" + size.x + "x" + size.y + ".png"));
                Button apply = root.Q<Button>("purpose-apply");
                ScrollView scroll = apply.GetFirstAncestorOfType<ScrollView>(); scroll?.ScrollTo(apply);
                await UniTask.DelayFrame(3);
                Check(apply.worldBound.Overlaps(new Rect(0,0,size.x,size.y)) && apply.worldBound.height >= 28, "required Apply accessible " + size);
                if (size.x == 390)
                {
                    root.Q<IntegerField>("purpose-count").value = 2;
                    panel.targetTexture = null; texture.Release(); texture.width = 844; texture.height = 390; texture.Create(); panel.targetTexture = texture;
                    await UniTask.DelayFrame(5); Call("ApplyMobileViewport", new Vector2(844,390), new Rect(0,0,844,390), new Rect()); await UniTask.DelayFrame(5);
                    Check(root.Q<IntegerField>("purpose-count").value == 2 && root.Q<Button>("purpose-cancel").enabledInHierarchy, "rotation while modal open preserves draft and cancellation");
                    Capture(texture, Path.Combine(output, "settings-rotated.png"));
                    panel.targetTexture = null; texture.Release(); texture.width = size.x; texture.height = size.y; texture.Create(); panel.targetTexture = texture;
                    await UniTask.DelayFrame(5);
                    Call("ApplyMobileViewport", (Vector2)size, new Rect(0,0,size.x,size.y), new Rect(0,0,size.x,300));
                    await UniTask.DelayFrame(4); scroll.ScrollTo(apply); await UniTask.DelayFrame(3);
                    Check(apply.worldBound.yMax <= size.y - 300 + 2 && root.Q<IntegerField>("purpose-count").value == 2, "keyboard keeps required controls accessible and draft values");
                    Capture(texture, Path.Combine(output, "settings-keyboard.png"));
                    Type commands = typeof(LevelToolScreen).GetNestedType("ToolCommandId", BindingFlags.NonPublic);
                    Call("TryExecuteCommand", Enum.Parse(commands, "Cancel"));
                    Check(root.Q("modal") == null && typeof(LevelToolScreen).GetField("purposeDraft", Flags).GetValue(screen) == null &&
                        typeof(LevelToolScreen).GetField("tutorialWork", Flags).GetValue(screen) != null, "Escape cancels only current purpose draft and keeps tutorial work");
                    Call("ApplyMobileViewport", (Vector2)size, new Rect(0,0,size.x,size.y), new Rect());
                }
                else Click("purpose-cancel");
                Call("BeginTutorialPick", "highlights", null, null, null); Call("UseTutorialPicker", 0); Call("ConfirmTutorialPick");
                await UniTask.DelayFrame(3);
                var directApply = root.Q<Button>("direct-pick-apply");
                directApply.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(directApply); await UniTask.DelayFrame(3);
                Check(directApply.worldBound.Overlaps(new Rect(0,0,size.x,size.y)) && directApply.worldBound.height >= 28, "direct required Apply accessible " + size);
                Capture(texture, Path.Combine(output, "direct-settings-" + size.x + "x" + size.y + ".png")); Click("direct-pick-cancel");
            }
            TutorialDraftEditing.CreateFlow(session, "목록 지연 조회 검사"); Call("Refresh");
            Foldout usage = root.Q<Foldout>("tutorial-flow-usage");
            Check(usage != null && usage.contentContainer.childCount == 0, "shared usage does not enumerate level bodies while collapsed");
            usage.value = true;
            Check(usage.contentContainer.childCount > 0, "shared usage loads on explicit expansion");
            Click("tutorial-work-cancel"); Click("tutorial-discard-work");
            TutorialDraftEditing.AddSampleBoard(session, "swap");
            TutorialDraftEditing.EditLevel(session, "처음부터 시험 검사", level =>
            {
                level.Tutorial.steps.Insert(0, new TutorialStepDefinition { kind = TutorialStepKind.Description, instructions = "처음 안내" });
                level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Description, instructions = "제외할 마지막 안내" });
            });
            Call("Refresh"); Click("tutorial-begin-work");
            string trialBefore = session.ExportState();
            typeof(LevelToolScreen).GetField("packedTrial", Flags).SetValue(screen, true);
            await (UniTask)typeof(LevelToolScreen).GetMethod("BeginPlayCore", Flags).Invoke(screen, new object[] { 1 });
            Check(screen.TestSession != null, "through-step actual game starts");
            await Stable(screen.TestSession);
            Check(screen.TestSession.TutorialState.Instructions == "처음 안내", "through-step game starts at the first description");
            screen.ReturnToEditor(); await UniTask.DelayFrame(2);
            Click("creation-guide");
            Check(root.Q<Label>("checklist-play-status").text.Contains("필요"), "launch without an action does not mark play checked"); Click("checklist-close");
            await (UniTask)typeof(LevelToolScreen).GetMethod("BeginPlayCore", Flags).Invoke(screen, new object[] { 1 });
            await Stable(screen.TestSession);
            Check(screen.TestSession.TryAdvanceTutorial(), "first description advances explicitly");
            var guidance = screen.TestSession.TutorialState;
            Check(guidance.First.HasValue && guidance.Second.HasValue && screen.TestSession.TrySwap(guidance.First.Value, guidance.Second.Value), "selected tutorial swap executes in actual game");
            await Stable(screen.TestSession);
            Check(screen.TestSession.TutorialState.State == TutorialProgressState.Completed, "through-step ends after selected step and excludes later description");
            screen.ReturnToEditor(); await UniTask.DelayFrame(2);
            Check(session.ExportState() == trialBefore, "actual trial preserves authoring documents and history");
            Click("creation-guide");
            Check(root.Q<Label>("checklist-play-status").text.Contains("조작 확인됨"), "completed real swap marks play checked"); Click("checklist-close");
            object checkpointBeforeDisable = typeof(LevelToolScreen).GetField("tutorialWork", Flags).GetValue(screen);
            screen.enabled = false; await UniTask.DelayFrame(2); screen.enabled = true; await UniTask.DelayFrame(10);
            root = screen.GetComponent<UIDocument>().rootVisualElement;
            Check(ReferenceEquals(checkpointBeforeDisable, typeof(LevelToolScreen).GetField("tutorialWork", Flags).GetValue(screen)) && session.ExportState() == trialBefore,
                "temporary screen disable preserves tutorial checkpoint and authoring history");
            Call("SelectWorkspacePage", "시험 기록");
            Check(root.Q("tutorial-continue-work") != null, "resumed screen still guards leaving tutorial work"); Click("tutorial-continue-work");
        }
        private static async UniTask Stable(GameScreen.PuzzleGameSession game)
        {
            using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(35));
            await UniTask.Yield();
            await UniTask.WaitUntil(() => game.HasFailed || game.IsReady && !game.IsPresenting && !game.HasProgressFeedback &&
                (game.Phase == Simulation.BoardActionPhase.Ready || game.Phase == Simulation.BoardActionPhase.Stopped), cancellationToken: timeout.Token);
            if (game.HasFailed) throw new Exception(game.Message);
        }
        private static void Capture(RenderTexture target, string path)
        {
            RenderTexture old = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try { image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG()); }
            finally { RenderTexture.active=old; UnityEngine.Object.Destroy(image); }
        }
    }
}

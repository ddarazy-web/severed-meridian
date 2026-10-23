#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using Board;
using Elements;
using LevelAuthoring.Runtime;
using Levels;
using Newtonsoft.Json.Linq;
using Simulation;
using Tutorial;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private TutorialStepDefinition directPickStep;
        private int directPickCount;
        private bool directPickReady;
        private string[] directPickErrors;
        private string directPickFirstPower;

        private static int DirectActionCount(TutorialStepDefinition step) => step.kind == TutorialStepKind.Description ? 0 :
            step.kind != TutorialStepKind.Item || step.item == BoardItem.Swap ? 2 : step.item == BoardItem.Hammer ? 1 : 0;

        private bool PrepareDirectPick()
        {
            directPickReady = false;
            directPickFirstPower = null;
            directPickCount = -1;
            using var graph = new AuthoringObjectGraph(sharedTutorialDraft?.Session ?? Session);
            var level = (LevelDefinition)graph.Resolve(Session.SelectedLevelId);
            var steps = sharedTutorialDraft == null ? level.Tutorial.steps : ((TutorialFlowDefinition)graph.Resolve(sharedTutorialDraft.FlowId)).steps;
            bool binding = !string.IsNullOrEmpty((string)tutorialPick["binding"]);
            directPickStep = binding ? null : steps[(int)tutorialPick["step"]].Copy();
            TutorialConditionDefinition condition = null;
            TutorialTargetDefinition target = null;
            string mode = (string)tutorialPick["mode"];
            if (binding)
            {
                var field = (TutorialFlowField)Enum.Parse(typeof(TutorialFlowField), (string)tutorialPick["field"]);
                var value = level.Tutorial.bindings.Single(item => item.key == (string)tutorialPick["binding"] && item.field == field);
                if (field == TutorialFlowField.First || field == TutorialFlowField.Second) directPickCount = 1;
                if (field == TutorialFlowField.Target) target = value.target;
            }
            else if (mode == "action")
            {
                directPickCount = DirectActionCount(directPickStep);
                if (directPickCount > 0 && !string.IsNullOrEmpty(directPickStep.firstBinding)) directPickCount--;
                if (DirectActionCount(directPickStep) == 2 && !string.IsNullOrEmpty(directPickStep.secondBinding)) directPickCount--;
            }
            else if (mode == "target")
            {
                condition = directPickStep.conditions.Single(item => item.authoringId == (string)tutorialPick["condition"]);
                target = condition.target;
            }
            if (target != null) directPickCount = target.kind == TutorialTargetKind.Entity ? 1 : target.kind == TutorialTargetKind.Area ? -1 : 0;
            if (directPickCount == 0)
            {
                Show("이 설정에는 보드 칸 선택이 필요하지 않습니다. 생성 연결·보드 전체 설정은 그대로 유지합니다.");
                return false;
            }
            bool initial = !binding && steps.Take((int)tutorialPick["step"]).All(step => step.kind == TutorialStepKind.Description);
            if (initial && mode == "action" && directPickStep.kind == TutorialStepKind.PowerSwap &&
                string.IsNullOrEmpty(directPickStep.firstBinding) && string.IsNullOrEmpty(directPickStep.secondBinding))
                directPickFirstPower = directPickStep.actionDefinitionId;
            directPickErrors = new string[81];
            var catalog = level.CreateElementCatalog();
            for (int cell = 0; cell < 81; cell++)
            {
                var coordinate = new BoardCoordinate(cell / 9, cell % 9);
                string error = LevelPlacementRules.CellError(level, coordinate);
                if (error == null && initial && mode == "action" && string.IsNullOrEmpty(directPickStep.firstBinding) && string.IsNullOrEmpty(directPickStep.secondBinding))
                {
                    var placement = level.Elements.FirstOrDefault(item => item.layer == PlacementLayer.Block && item.coordinate.Equals(coordinate));
                    var definition = placement == null ? null : catalog.Get(new ElementId(placement.definitionId));
                    if (DirectActionCount(directPickStep) == 2)
                    {
                        if (definition?.Supply?.Behavior != ElementSupplyBehavior.FixedNormal && definition?.Supply?.Behavior != ElementSupplyBehavior.Power)
                            error = "첫 교환은 초기 고정 블록 또는 파워를 선택하세요.";
                        else if (level.Elements.Any(item => item.layer == PlacementLayer.Cover && item.coordinate.Equals(coordinate))) error = "덮개가 있는 블록은 교환할 수 없습니다.";
                    }
                    else if (!level.Elements.Any(item => LevelPlacementRules.Footprint(item.coordinate,
                        catalog.Get(new ElementId(item.definitionId)).Placement?.Size ?? catalog.Get(new ElementId(item.definitionId)).ChargePlacement?.Size ?? 1).Contains(coordinate)))
                        error = "망치를 사용할 배치가 있는 칸을 선택하세요.";
                }
                if (error == null && initial && target?.kind == TutorialTargetKind.Entity)
                {
                    var candidate = new TutorialTargetDefinition { kind = TutorialTargetKind.Entity, layer = target.layer, coordinate = coordinate };
                    var definition = TutorialTargetCapabilities.Resolve(level, candidate, true);
                    error = definition == null ? "선택한 층의 개체가 있는 칸을 선택하세요." : TutorialTargetCapabilities.ConditionError(definition, condition.kind);
                    if (error == null && (condition.kind == TutorialConditionKind.DurabilityDecrease || condition.kind == TutorialConditionKind.Removed))
                        error = condition.allowedOrigins.Select(origin => TutorialTargetCapabilities.OriginError(definition, origin)).FirstOrDefault(value => value != null);
                }
                directPickErrors[cell] = error;
            }
            return true;
        }

        private bool DirectPickCellAllowed(int cell, out string reason)
        {
            reason = cell < 0 || cell >= 81 ? "보드 안의 칸을 선택하세요." : directPickErrors?[cell];
            if (reason != null) return false;
            var cells = (JArray)tutorialPick["cells"];
            if (cells.Any(value => (int)value == cell)) return true;
            if (cells.Count == 0 && !string.IsNullOrEmpty(directPickFirstPower) && !displayedLevel["elements"].Any(element =>
                (string)element["layer"] == "Block" && (string)element["definitionId"] == directPickFirstPower &&
                (int)element["coordinate"]["row"] == cell / 9 && (int)element["coordinate"]["column"] == cell % 9))
                reason = "첫 칸에는 조작할 파워 종류로 지정한 파워를 선택하세요.";
            if (directPickCount > 0 && cells.Count >= directPickCount) reason = "필요한 칸을 모두 골랐습니다. 설정을 적용하거나 선택을 취소하세요.";
            if (reason == null && (string)tutorialPick["mode"] == "action" && directPickCount == 2 && cells.Count == 1)
            {
                int first = (int)cells[0];
                if (Math.Abs(first / 9 - cell / 9) + Math.Abs(first % 9 - cell % 9) != 1) reason = "첫 칸과 상하좌우로 붙어 있는 칸을 선택하세요.";
                else if (displayedLevel["flow"]?["walls"] is JArray walls && walls.Any(wall =>
                    ((int)wall["a"]["row"] * 9 + (int)wall["a"]["column"] == first && (int)wall["b"]["row"] * 9 + (int)wall["b"]["column"] == cell) ||
                    ((int)wall["b"]["row"] * 9 + (int)wall["b"]["column"] == first && (int)wall["a"]["row"] * 9 + (int)wall["a"]["column"] == cell))) reason = "벽을 통과하는 교환은 선택할 수 없습니다.";
            }
            return reason == null;
        }

        private void OpenDirectPickSettings()
        {
            if (tutorialPick == null) return;
            directPickReady = true;
            JObject pick = tutorialPick;
            var panel = OpenModal("선택한 칸의 필수 설정 확인");
            panel.Add(new Label("선택 " + pick["cells"].Count() + "칸 · 적용 전까지 원본은 바뀌지 않습니다. 생성 결과 연결은 유지합니다.") { style = { whiteSpace = WhiteSpace.Normal } });
            Action validate = null;
            if (directPickStep != null)
            {
                var instructions = new TextField("플레이어 안내문") { name = "direct-pick-instructions", value = directPickStep.instructions };
                panel.Add(instructions);
                instructions.RegisterValueChangedCallback(evt => { directPickStep.instructions = evt.newValue; validate?.Invoke(); });
                for (int i = 0; i < directPickStep.conditions.Count; i++)
                {
                    var condition = directPickStep.conditions[i];
                    var count = new IntegerField(TutorialAuthoringRules.ConditionLabel(condition.kind) + (condition.kind == TutorialConditionKind.RemainingDurability ? " · 정확히" : " · 횟수/개수"))
                    { name = "direct-pick-count-" + i, value = condition.requiredCount };
                    panel.Add(count); count.RegisterValueChangedCallback(evt => { condition.requiredCount = evt.newValue; validate?.Invoke(); });
                }
            }
            var error = new Label { name = "direct-pick-error", style = { whiteSpace = WhiteSpace.Normal } }; panel.Add(error);
            var apply = Button(panel, "direct-pick-apply", "선택과 설정 적용", () => { if (ReferenceEquals(tutorialPick, pick) && modal?.Contains(panel) == true) ApplyDirectPick(); });
            Button(panel, "direct-pick-cancel", "이번 선택 취소", CancelTutorialPick);
            validate = () =>
            {
                bool valid = directPickStep == null || !string.IsNullOrWhiteSpace(directPickStep.instructions) && directPickStep.conditions.All(condition =>
                    condition.requiredCount >= (condition.kind == TutorialConditionKind.RemainingDurability ? 0 : 1) && (string.IsNullOrEmpty(condition.bindGeneratedAs) || condition.requiredCount == 1));
                apply.SetEnabled(valid); error.text = valid ? "세부 조건과 연결은 기존 값을 유지합니다. 작성 완료 전에 전체 조건을 검사합니다." : "안내문과 수치를 확인하세요. 생성 결과 연결은 1개만 가능합니다.";
            };
            validate();
        }
    }
}
#endif

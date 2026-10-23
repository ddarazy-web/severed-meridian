#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using Board;
using LevelAuthoring.Runtime;
using Levels;
using Newtonsoft.Json.Linq;
using Tutorial;
using Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private JObject tutorialPick;

        private void BeginTutorialPick(string mode, string conditionId = null, string bindingKey = null, string bindingField = null)
        {
            CancelPendingInput(); brush = null;
            string source = sharedTutorialDraft?.FlowId ?? Session.SelectedLevelId;
            tutorialPick = new JObject {
                ["level"] = Session.SelectedLevelId, ["source"] = source, ["flow"] = sharedTutorialDraft?.FlowId,
                ["baseline"] = (sharedTutorialDraft?.Session ?? Session).Get(source).Data,
                ["step"] = tutorialStep, ["mode"] = mode, ["condition"] = conditionId,
                ["binding"] = bindingKey, ["field"] = bindingField, ["cells"] = new JArray()
            };
            if (mode != "purpose" && !PrepareDirectPick()) { tutorialPick = null; Refresh(); return; }
            Refresh(); board.focusable = true; board.Focus();
        }

        private bool UseTutorialPicker(int cell)
        {
            if (tutorialPick == null) return false;
            if ((string)tutorialPick["mode"] == "purpose") return UsePurposePicker(cell);
            if (modal != null) return true;
            if (!DirectPickCellAllowed(cell, out string reason)) { Show(reason); return true; }
            var cells = (JArray)tutorialPick["cells"];
            JToken existing = cells.FirstOrDefault(value => (int)value == cell);
            if (existing == null) cells.Add(cell); else existing.Remove();
            Refresh();
            if (directPickCount > 0 && cells.Count == directPickCount) OpenDirectPickSettings();
            else board.Focus();
            return true;
        }

        private void CancelTutorialPick() { tutorialPick = null; purposeDraft = null; directPickStep = null; directPickReady = false; CloseModal(); Refresh(); }
        private void ConfirmTutorialPick()
        {
            if (tutorialPick == null) return;
            if ((string)tutorialPick["mode"] == "purpose") { Show("필요한 칸을 모두 고르면 설정 창이 열립니다."); return; }
            if (tutorialPick["cells"].Count() == 0 || directPickCount > 0 && tutorialPick["cells"].Count() != directPickCount)
            { Show(directPickCount > 0 ? "필요한 " + directPickCount + "칸을 선택하세요." : "한 칸 이상 선택하세요."); return; }
            OpenDirectPickSettings();
        }
        private void ApplyDirectPick() => Edit(() =>
        {
            if (tutorialPick == null) return;
            if ((string)tutorialPick["mode"] == "purpose") { Show("필요한 칸을 모두 고르면 설정 창이 열립니다."); return; }
            JObject pick = tutorialPick;
            if ((string)pick["level"] != Session.SelectedLevelId || (string)pick["flow"] != sharedTutorialDraft?.FlowId ||
                !JToken.DeepEquals(pick["baseline"], (sharedTutorialDraft?.Session ?? Session).Get((string)pick["source"]).Data))
                throw new InvalidOperationException("선택 중 원본이 바뀌었습니다. 취소한 뒤 다시 선택하세요.");
            var cells = pick["cells"].Values<int>().Select(cell => new BoardCoordinate(cell / 9, cell % 9)).ToList();
            if (cells.Count == 0) throw new ArgumentException("한 칸 이상 선택하세요.");
            if (directPickCount > 0 && cells.Count != directPickCount) throw new ArgumentException("필요한 칸 수를 확인하세요.");
            void SetTarget(TutorialTargetDefinition target)
            {
                if (target.kind == TutorialTargetKind.Entity)
                {
                    if (cells.Count != 1) throw new ArgumentException("특정 개체는 한 칸만 선택하세요.");
                    target.coordinate = cells[0];
                }
                else if (target.kind == TutorialTargetKind.Area) target.cells = cells;
                else throw new ArgumentException("칸 선택을 사용하는 대상 종류가 아닙니다.");
            }
            void Apply(LevelDefinition level)
            {
                if (!string.IsNullOrEmpty((string)pick["binding"]))
                {
                    var field = (TutorialFlowField)Enum.Parse(typeof(TutorialFlowField), (string)pick["field"]);
                    var binding = level.Tutorial.bindings.Single(value => value.key == (string)pick["binding"] && value.field == field);
                    if (field == TutorialFlowField.Target) SetTarget(binding.target);
                    else if (field == TutorialFlowField.First || field == TutorialFlowField.Second)
                    { if (cells.Count != 1) throw new ArgumentException("한 칸만 선택하세요."); binding.coordinate = cells[0]; }
                    else binding.cells = cells;
                    return;
                }
                var step = directPickStep.Copy();
                level.Tutorial.steps[(int)pick["step"]] = step;
                switch ((string)pick["mode"])
                {
                    case "action":
                        int cursor = 0;
                        if (string.IsNullOrEmpty(step.firstBinding)) { step.hasFirst = true; step.first = cells[cursor++]; }
                        if (DirectActionCount(step) == 2 && string.IsNullOrEmpty(step.secondBinding)) { step.hasSecond = true; step.second = cells[cursor++]; }
                        if (DirectActionCount(step) == 1) step.hasSecond = false;
                        step.actionArea.Clear(); break;
                    case "area": step.actionArea = cells; break;
                    case "highlights": step.highlights = cells; step.automaticHighlights = false; break;
                    case "target": SetTarget(step.conditions.Single(value => value.authoringId == (string)pick["condition"]).target); break;
                    default: throw new ArgumentException("알 수 없는 선택 작업입니다.");
                }
            }
            if (sharedTutorialDraft == null) TutorialDraftEditing.EditLevel(Session, "튜토리얼 칸 선택 확정", Apply);
            else TutorialDraftEditing.EditFlowSteps(sharedTutorialDraft.Session, sharedTutorialDraft.FlowId, "튜토리얼 칸 선택 확정", Apply);
            tutorialPick = null; directPickStep = null; directPickReady = false; CloseModal();
            Show("선택한 칸을 적용했습니다. 실행 취소로 되돌릴 수 있습니다.");
        });

        private void DrawTutorialPicking()
        {
            inspector.Q("tutorial-pick-panel")?.RemoveFromHierarchy();
            board.Q("tutorial-pick-overlay")?.RemoveFromHierarchy();
            toolbar.SetEnabled(!busy && tutorialPick == null);
            if (tutorialPick == null) return;
            levelList.SetEnabled(false); palette.SetEnabled(false);
            foreach (string name in new[] { "new-level", "duplicate", "edit-layer", "select" }) root.Q(name)?.SetEnabled(false);
            foreach (var child in inspector.Children().ToArray()) child.SetEnabled(false);
            var panel = new VisualElement { name = "tutorial-pick-panel" }; inspector.Insert(0, panel);
            panel.Add(new Label("보드에서 칸을 차례로 누르세요. 다시 누르면 제외합니다. 선택 " + tutorialPick["cells"].Count() + "칸 · 확정 전에는 원본이 바뀌지 않습니다.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            Button(panel, "tutorial-pick-confirm", "선택 완료 · 설정 확인 (Enter)", ConfirmTutorialPick);
            Button(panel, "tutorial-pick-cancel", "취소 (Esc)", CancelTutorialPick);
            var overlay = new VisualElement { name = "tutorial-pick-overlay", pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute; overlay.style.left = 0; overlay.style.top = 0;
            overlay.style.width = cellSize * 9; overlay.style.height = cellSize * 9; board.Add(overlay);
            bool purpose = (string)tutorialPick["mode"] == "purpose" && purposeDraft != null;
            int[] selectedCells = tutorialPick["cells"].Values<int>().ToArray();
            var visibleCells = Enumerable.Range(0, 81).Where(cell => selectedCells.Contains(cell) || (purpose ?
                PurposeCellAllowed(cell, (bool?)tutorialPick["purposeTarget"] == true, out _) : DirectPickCellAllowed(cell, out _)));
            foreach (int cell in visibleCells)
            {
                bool selected = selectedCells.Contains(cell);
                var mark = new VisualElement { name = "tutorial-valid-cell-" + cell, pickingMode = PickingMode.Ignore };
                mark.style.position = Position.Absolute; mark.style.left = cell % 9 * cellSize; mark.style.top = cell / 9 * cellSize;
                mark.style.width = cellSize; mark.style.height = cellSize;
                mark.style.borderTopWidth = mark.style.borderBottomWidth = mark.style.borderLeftWidth = mark.style.borderRightWidth = selected ? 3 : 1;
                mark.style.borderTopColor = mark.style.borderBottomColor = mark.style.borderLeftColor = mark.style.borderRightColor = selected ? Color.cyan : new Color(0.4f, 0.85f, 0.75f, 0.65f);
                overlay.Add(mark);
            }
        }
    }
}
#endif

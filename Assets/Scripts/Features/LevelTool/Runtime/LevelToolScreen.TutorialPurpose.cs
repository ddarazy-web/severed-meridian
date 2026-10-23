#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Newtonsoft.Json.Linq;
using Simulation;
using Tutorial;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private TutorialStepDefinition purposeDraft;
        private string purposeKind;
        private bool tutorialAdvanced;

        private void DrawTutorialPurposes()
        {
            inspector.Add(new Label("어떤 행동을 알려줄까요?") { style = { whiteSpace = WhiteSpace.Normal } });
            string[] keys = { "match", "generate", "use", "combine", "damage", "direct" };
            string[] labels = { "교환·3매칭", "파워 블록 만들기", "파워 블록 사용", "파워끼리 조합", "내구도 줄이기", "직접 조립" };
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                Button(inspector, "tutorial-purpose-" + key, labels[i], () => BeginTutorialPurpose(key));
            }
            Button(inspector, "tutorial-next-supply", "다음에 내려올 블록 지정", () =>
            {
                CancelPendingInput(); inspectorPage = "튜토리얼 공급"; forceProperties = true; Refresh();
                Show("생성구의 열을 고르고 위에서부터 내려올 순서를 지정하세요. 튜토리얼 시작부터 사용하는 공급입니다.");
            });
        }

        private void BeginTutorialPurpose(string purpose)
        {
            if (tutorialWork == null || sharedTutorialDraft != null || busy || modal != null) return;
            if (purpose == "use" && string.IsNullOrEmpty((string)displayedLevel["tutorial"]?["flowId"]))
            {
                var sources = displayedLevel["tutorial"]["steps"].SelectMany(step => step["conditions"] ?? new JArray())
                    .Where(condition => (string)condition["kind"] == "Generated" && (int?)condition["requiredCount"] == 1 &&
                        !string.IsNullOrWhiteSpace((string)condition["bindGeneratedAs"]))
                    .GroupBy(condition => (string)condition["bindGeneratedAs"]).Where(group => group.Count() == 1).Select(group => group.First()).ToArray();
                if (sources.Length > 0)
                {
                    VisualElement panel = OpenModal("어떤 파워를 사용하나요?");
                    panel.Add(new Label("앞 단계에서 만든 파워를 연결하면 떨어진 뒤의 위치도 따라갑니다. 자동으로 연결하지 않으니 사용할 결과를 선택하세요.")
                        { style = { whiteSpace = WhiteSpace.Normal } });
                    var choice = new DropdownField("앞 단계 생성 결과", sources.Select(source => (string)source["bindGeneratedAs"]).ToList(), 0) { name = "purpose-generated-choice" };
                    panel.Add(choice);
                    Button(panel, "purpose-use-generated", "이 생성 결과 사용", () =>
                    {
                        if (modal == null || !modal.Contains(panel)) return;
                        JToken source = sources[choice.index]; CloseModal();
                        BeginTutorialPurposeCore(purpose, (string)source["bindGeneratedAs"], (string)source["powerDefinitionId"]);
                    });
                    Button(panel, "purpose-use-board", "보드에 있는 파워 선택", () => { CloseModal(); BeginTutorialPurposeCore(purpose); });
                    Button(panel, "cancel", "취소", CloseModal); return;
                }
            }
            BeginTutorialPurposeCore(purpose);
        }

        private void BeginTutorialPurposeCore(string purpose, string binding = null, string powerId = null)
        {
            if (tutorialWork == null || sharedTutorialDraft != null || busy || modal != null) return;
            if (!string.IsNullOrEmpty((string)displayedLevel["tutorial"]?["flowId"]))
            {
                Show("공통 구성의 진행은 직접 조립에서 원본을 편집하거나 ‘현재 값으로 독립 복사’한 뒤 단계를 추가하세요.");
                tutorialAdvanced = true; RefreshProperties(); return;
            }
            if (purpose == "direct")
            {
                TutorialEdit("직접 조립 단계 추가", level =>
                {
                    level.Tutorial.steps.Add(new TutorialStepDefinition { authoringId = Guid.NewGuid().ToString("N"), automaticHighlights = true });
                    tutorialStep = level.Tutorial.steps.Count - 1;
                });
                tutorialAdvanced = true; RefreshProperties(); return;
            }
            purposeKind = purpose;
            purposeDraft = new TutorialStepDefinition { authoringId = Guid.NewGuid().ToString("N"), kind = TutorialStepKind.Swap, automaticHighlights = true,
                instructions = purpose == "match" ? "표시된 두 블록을 바꿔 3개를 맞춰 보세요." : purpose == "generate" ? "표시된 블록을 바꿔 파워 블록을 만들어 보세요." :
                    purpose == "use" ? "파워 블록을 옆 칸과 바꿔 사용해 보세요." : purpose == "combine" ? "두 파워 블록을 서로 바꿔 보세요." : "블록을 바꿔 대상의 내구도를 줄여 보세요." };
            TutorialConditionKind kind = purpose == "match" ? TutorialConditionKind.Match : purpose == "generate" ? TutorialConditionKind.Generated :
                purpose == "use" ? TutorialConditionKind.Activated : purpose == "combine" ? TutorialConditionKind.Combined : TutorialConditionKind.DurabilityDecrease;
            purposeDraft.conditions.Add(new TutorialConditionDefinition
            {
                authoringId = Guid.NewGuid().ToString("N"), kind = kind,
                target = kind == TutorialConditionKind.Match ? null : new TutorialTargetDefinition(),
                allowedOrigins = purpose == "combine" ? new List<EffectOrigin> { EffectOrigin.RocketBomb } : purpose == "damage" ? new List<EffectOrigin> { EffectOrigin.AdjacentMatch } : new List<EffectOrigin>()
            });
            if (binding != null)
            {
                purposeDraft.firstBinding = binding;
                purposeDraft.conditions[0].powerDefinitionId = powerId;
                purposeDraft.conditions[0].target.kind = TutorialTargetKind.Generated;
                purposeDraft.conditions[0].target.binding = binding;
            }
            BeginPurposePick(false);
        }

        private void BeginPurposePick(bool target)
        {
            TutorialStepDefinition draft = purposeDraft;
            BeginTutorialPick("purpose");
            purposeDraft = draft;
            tutorialPick["purposeTarget"] = target;
            Refresh();
            Show(target ? "조건의 대상 개체가 있는 한 칸을 선택하세요." : !string.IsNullOrEmpty(purposeDraft.firstBinding) ?
                "생성된 파워와 교환할 상대 칸만 선택하세요. 파워의 위치는 실행 중 연결 결과를 따릅니다." : "교환할 두 칸을 차례로 선택하세요. 두 칸을 선택하면 필수 설정이 열립니다.");
        }

        private bool PurposeCellAllowed(int cell, bool target, out string reason)
        {
            reason = null;
            if (cell < 0 || cell >= 81 || (bool?)displayedLevel["board"]?["cells"]?[cell]?["isActive"] != true)
                reason = "보드에서 사용하는 칸을 선택하세요.";
            else if (!target && string.IsNullOrEmpty(purposeDraft.firstBinding) && displayedLevel["tutorial"]["steps"].All(step => (string)step["kind"] == "Description") &&
                (!((JArray)displayedLevel["elements"]).Any(element => (string)element["layer"] == "Block" &&
                    (int)element["coordinate"]["row"] == cell / 9 && (int)element["coordinate"]["column"] == cell % 9 &&
                    definitions.TryGetValue((string)element["definitionId"], out JObject definition) &&
                    ((string)definition["supply"]?["behavior"] == "FixedNormal" || (string)definition["supply"]?["behavior"] == "Power")) ||
                 ((JArray)displayedLevel["elements"]).Any(element => (string)element["layer"] == "Cover" &&
                    (int)element["coordinate"]["row"] == cell / 9 && (int)element["coordinate"]["column"] == cell % 9)))
                reason = "첫 교환은 덮개가 없는 초기 고정 블록 또는 파워를 선택하세요. 먼저 보드 배치에서 준비할 수 있습니다.";
            else if (target && !((JArray)displayedLevel["elements"]).Any(element =>
                (string)element["layer"] == (purposeDraft.conditions[0].target.layer == TutorialTargetLayer.Cover ? "Cover" : purposeDraft.conditions[0].target.layer == TutorialTargetLayer.Floor ? "Dust" : "Obstacle") &&
                (int?)element["durability"] > 0 &&
                (int)element["coordinate"]["row"] <= cell / 9 && (int)element["coordinate"]["column"] <= cell % 9 &&
                definitions.TryGetValue((string)element["definitionId"], out JObject definition) &&
                (int)element["coordinate"]["row"] + LevelToolDocuments.Size(definition) > cell / 9 &&
                (int)element["coordinate"]["column"] + LevelToolDocuments.Size(definition) > cell % 9))
                reason = "내구도를 확인할 개체가 있는 칸을 선택하세요. 보드 전체를 대상으로 할 수도 있습니다.";
            if (reason == null && !target && tutorialPick?["cells"] is JArray selected && selected.Count == 1 && (int)selected[0] != cell)
            {
                int first = (int)selected[0];
                if (Math.Abs(first / 9 - cell / 9) + Math.Abs(first % 9 - cell % 9) != 1)
                    reason = "첫 칸과 상하좌우로 붙어 있는 칸을 선택하세요.";
                else if (displayedLevel["flow"]?["walls"] is JArray walls && walls.Any(wall =>
                    ((int)wall["a"]["row"] * 9 + (int)wall["a"]["column"] == first && (int)wall["b"]["row"] * 9 + (int)wall["b"]["column"] == cell) ||
                    ((int)wall["b"]["row"] * 9 + (int)wall["b"]["column"] == first && (int)wall["a"]["row"] * 9 + (int)wall["a"]["column"] == cell)))
                    reason = "두 칸 사이에 벽이 있어 교환할 수 없습니다.";
            }
            return reason == null;
        }

        private bool UsePurposePicker(int cell)
        {
            bool target = (bool?)tutorialPick["purposeTarget"] == true;
            if (!PurposeCellAllowed(cell, target, out string reason)) { Show(reason); return true; }
            JArray cells = (JArray)tutorialPick["cells"];
            JToken old = cells.FirstOrDefault(value => (int)value == cell);
            if (old != null) { old.Remove(); Refresh(); return true; }
            if (!target && cells.Count == 1 && Math.Abs((int)cells[0] / 9 - cell / 9) + Math.Abs((int)cells[0] % 9 - cell % 9) != 1)
            { Show("첫 칸과 상하좌우로 붙어 있는 칸을 선택하세요."); return true; }
            cells.Add(cell);
            if (cells.Count == (target || !string.IsNullOrEmpty(purposeDraft.firstBinding) ? 1 : 2))
            {
                if (target)
                {
                    purposeDraft.conditions[0].target.kind = TutorialTargetKind.Entity;
                    purposeDraft.conditions[0].target.coordinate = new BoardCoordinate(cell / 9, cell % 9);
                }
                else if (!string.IsNullOrEmpty(purposeDraft.firstBinding))
                {
                    purposeDraft.hasSecond = true; purposeDraft.second = new BoardCoordinate(cell / 9, cell % 9);
                }
                else
                {
                    int first = (int)cells[0], second = (int)cells[1];
                    purposeDraft.hasFirst = purposeDraft.hasSecond = true;
                    purposeDraft.first = new BoardCoordinate(first / 9, first % 9); purposeDraft.second = new BoardCoordinate(second / 9, second % 9);
                }
                tutorialPick = null; Refresh(); OpenPurposeSettings();
            }
            else Refresh();
            return true;
        }

        private void OpenPurposeSettings()
        {
            if (purposeDraft == null) return;
            TutorialStepDefinition draft = purposeDraft;
            TutorialConditionDefinition condition = draft.conditions[0];
            VisualElement panel = OpenModal("필수 설정 · 적용 전까지 레벨은 바뀌지 않습니다");
            var instructions = new TextField("플레이어 안내문") { name = "purpose-instructions", value = draft.instructions };
            var count = new IntegerField(condition.kind == TutorialConditionKind.RemainingDurability ? "남길 내구도 (정확히)" : "필요 횟수 / 감소량") { name = "purpose-count", value = condition.requiredCount };
            panel.Add(instructions); panel.Add(count);
            var help = new Label { name = "purpose-error", style = { whiteSpace = WhiteSpace.Normal } };
            panel.Add(help);
            Action validate = null;
            if (purposeKind == "match")
            {
                var size = new IntegerField("한 번에 맞출 블록 수 (정확히)") { name = "purpose-match-size", value = condition.matchSize };
                panel.Add(size); size.RegisterValueChangedCallback(evt => { condition.matchSize = evt.newValue; validate?.Invoke(); });
            }
            if (purposeKind == "generate" || purposeKind == "use")
            {
                var powers = definitions.Where(pair => (string)pair.Value["supply"]?["behavior"] == "Power").ToArray();
                int selected = Array.FindIndex(powers, pair => pair.Key == condition.powerDefinitionId);
                var power = new DropdownField("파워 종류", powers.Select(pair => (string)pair.Value["displayName"]).ToList(), Math.Max(0, selected)) { name = "purpose-power" };
                if (powers.Length > 0) condition.powerDefinitionId = powers[Math.Max(0, selected)].Key;
                panel.Add(power); power.RegisterValueChangedCallback(_ => { condition.powerDefinitionId = powers[power.index].Key; validate?.Invoke(); });
                if (purposeKind == "generate")
                {
                    var binding = new TextField("다음 단계 연결 이름 (선택)") { name = "purpose-binding", value = condition.bindGeneratedAs };
                    binding.tooltip = "생성 1개 조건에 이름을 붙이면 다음 단계에서 이 파워를 대상으로 선택할 수 있습니다.";
                    panel.Add(binding); binding.RegisterValueChangedCallback(evt => { condition.bindGeneratedAs = evt.newValue.Trim(); validate?.Invoke(); });
                }
            }
            if (purposeKind == "combine" || purposeKind == "damage")
            {
                EffectOrigin[] origins = Enum.GetValues(typeof(EffectOrigin)).Cast<EffectOrigin>().Where(origin => purposeKind == "combine" ? origin >= EffectOrigin.RocketRocket : origin != EffectOrigin.Unknown).ToArray();
                var cause = new DropdownField("인정할 원인만", origins.Select(TutorialSampleCatalog.OriginLabel).ToList(), Math.Max(0, Array.IndexOf(origins, condition.allowedOrigins.First()))) { name = "purpose-cause" };
                panel.Add(cause);
                DropdownField secondCause = null;
                if (purposeKind == "combine")
                {
                    var choices = new List<string> { "추가 조합 없음" };
                    choices.AddRange(origins.Select(TutorialSampleCatalog.OriginLabel));
                    int second = condition.allowedOrigins.Count > 1 ? Array.IndexOf(origins, condition.allowedOrigins[1]) + 1 : 0;
                    secondCause = new DropdownField("추가로 인정할 조합 (선택)", choices, Math.Max(0, second)) { name = "purpose-second-cause" };
                    secondCause.tooltip = "선택한 두 조합 중 하나가 발생하면 인정합니다. 같은 조합을 두 번 선택해도 한 종류로 처리합니다.";
                    panel.Add(secondCause);
                }
                Action updateCauses = () =>
                {
                    condition.allowedOrigins = new List<EffectOrigin> { origins[cause.index] };
                    if (secondCause != null && secondCause.index > 0 && !condition.allowedOrigins.Contains(origins[secondCause.index - 1]))
                        condition.allowedOrigins.Add(origins[secondCause.index - 1]);
                    validate?.Invoke();
                };
                cause.RegisterValueChangedCallback(_ => updateCauses());
                secondCause?.RegisterValueChangedCallback(_ => updateCauses());
                if (purposeKind == "damage")
                {
                    var layer = new DropdownField("대상 층", new List<string> { "장애물", "덮개", "바닥" }, (int)condition.target.layer) { name = "purpose-target-layer" };
                    panel.Add(layer);
                    layer.RegisterValueChangedCallback(_ =>
                    {
                        condition.target.layer = (TutorialTargetLayer)layer.index;
                        condition.target.kind = TutorialTargetKind.Board;
                        draft.instructions = instructions.value; condition.requiredCount = count.value;
                        CloseModal(); OpenPurposeSettings();
                    });
                    var mode = new DropdownField("완료 기준", new List<string> { "내구도를 N만큼 줄이기", "남은 내구도 정확히 N" }, condition.kind == TutorialConditionKind.RemainingDurability ? 1 : 0) { name = "purpose-damage-mode" };
                    panel.Add(mode); mode.RegisterValueChangedCallback(_ =>
                    {
                        condition.kind = mode.index == 0 ? TutorialConditionKind.DurabilityDecrease : TutorialConditionKind.RemainingDurability;
                        count.label = mode.index == 0 ? "필요 감소량" : "남길 내구도 (정확히)";
                        validate?.Invoke();
                    });
                    panel.Add(new Label(condition.target.kind == TutorialTargetKind.Entity ? "대상: 선택한 개체 " + condition.target.coordinate : "대상: 보드 전체"));
                    Button(panel, "purpose-pick-target", "보드에서 대상 개체 선택", () => { draft.instructions = instructions.value; condition.requiredCount = count.value; CloseModal(); BeginPurposePick(true); });
                    Button(panel, "purpose-target-board", "대상을 보드 전체로", () => { condition.target.kind = TutorialTargetKind.Board; CloseModal(); OpenPurposeSettings(); });
                }
            }
            Button apply = Button(panel, "purpose-apply", "이 단계 추가", () =>
            {
                if (purposeDraft != draft || tutorialWork == null || modal == null || !modal.Contains(panel)) return;
                draft.instructions = instructions.value.Trim(); condition.requiredCount = count.value;
                CloseModal();
                TutorialEdit("목적별 튜토리얼 단계 추가", level => { level.Tutorial.steps.Add(draft.Copy()); tutorialStep = level.Tutorial.steps.Count - 1; });
                purposeDraft = null; forceProperties = true; Refresh();
            });
            Button(panel, "purpose-cancel", "취소", () => { purposeDraft = null; CloseModal(); });
            validate = () =>
            {
                bool valid = !string.IsNullOrWhiteSpace(instructions.value) && count.value >= (condition.kind == TutorialConditionKind.RemainingDurability ? 0 : 1) &&
                    (purposeKind != "match" || condition.matchSize >= 3) &&
                    (purposeKind != "generate" && purposeKind != "use" || !string.IsNullOrEmpty(condition.powerDefinitionId)) &&
                    (string.IsNullOrEmpty(condition.bindGeneratedAs) || count.value == 1);
                apply.SetEnabled(valid); help.text = valid ? "조작 칸만 자동 강조합니다. 자세한 조건은 직접 조립에서 수정할 수 있습니다." : "안내문과 수치를 확인하세요. 생성 결과 연결은 생성 1개 조건에서만 사용할 수 있습니다.";
            };
            instructions.RegisterValueChangedCallback(evt => { draft.instructions = evt.newValue; validate(); });
            count.RegisterValueChangedCallback(evt => { condition.requiredCount = evt.newValue; validate(); });
            validate();
        }
    }
}
#endif


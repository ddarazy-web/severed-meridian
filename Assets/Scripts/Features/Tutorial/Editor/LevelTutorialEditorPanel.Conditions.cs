using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Elements;
using Levels;
using Simulation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Tutorial.Editor
{
    public sealed partial class LevelTutorialEditorPanel
    {
        private static string ConditionLabel(TutorialConditionKind kind) => TutorialAuthoringRules.ConditionLabel(kind);

        private void AddCondition(TutorialConditionKind kind)
        {
            var condition = TutorialAuthoringRules.CreateCondition(kind, owner.CreateElementCatalog(), owner.Tutorial.steps[selected].item);
            Mutate("튜토리얼 조건 추가", () => { Undo.RecordObject(owner, "튜토리얼 조건 추가"); owner.Tutorial.steps[selected].conditions.Add(condition);
            EditorUtility.SetDirty(owner); }); Rebuild();
        }

        private void ChangeCondition(int index, Action<TutorialConditionDefinition> change)
        {
            Mutate("튜토리얼 조건 편집", () => { Undo.RecordObject(owner, "튜토리얼 조건 편집"); change(owner.Tutorial.steps[selected].conditions[index]);
            EditorUtility.SetDirty(owner); }); Rebuild();
        }

        private void AddExtendedCondition(VisualElement card, SerializedProperty property, int index)
        {
            TutorialConditionDefinition condition = owner.Tutorial.steps[selected].conditions[index];
            card.Add(new Label(ConditionLabel(condition.kind)));
            bool initialAction = owner.Tutorial.steps.Take(selected).All(value => value.kind == TutorialStepKind.Description);
            ElementDefinition targetDefinition = TutorialTargetCapabilities.Resolve(owner, condition.target, initialAction);
            string conditionError = TutorialTargetCapabilities.ConditionError(targetDefinition, condition.kind);
            if (conditionError != null) card.Add(new HelpBox(conditionError, HelpBoxMessageType.Error));
            bool remaining = condition.kind == TutorialConditionKind.RemainingDurability;
            if (remaining) card.Add(new Label("대상의 남은 내구도가 정확히 다음 값이면 완료") { name = "tutorial-exact-durability-" + index });
            card.Add(new PropertyField(property.FindPropertyRelative("requiredCount"), remaining ? "정확히" :
                condition.kind == TutorialConditionKind.DurabilityDecrease ? "줄일 내구도" : condition.kind == TutorialConditionKind.MissionProgress ? "늘릴 진행량" : "필요 횟수 / 개수"));
            if (condition.kind == TutorialConditionKind.MissionProgress)
            {
                var labels = new List<string> { "선택하세요" };
                labels.AddRange(owner.Missions.Select((mission, number) => $"{number + 1}. {LevelMissionRules.Name(mission.Kind)}" +
                    (mission.Kind == MissionKind.Color ? $" · 색상 {(int)mission.Color + 1}" : "") + (mission.Kind == MissionKind.Mold ? " · 전체 제거" : $" · 목표 {mission.Count}")));
                var missionChoice = new PopupField<string>("진행을 셀 미션", labels,
                    condition.missionIndex >= 0 && condition.missionIndex < owner.Missions.Count && owner.Missions[condition.missionIndex].Kind == condition.missionKind &&
                    (condition.missionKind != MissionKind.Color || owner.Missions[condition.missionIndex].Color == condition.missionColor) ? condition.missionIndex + 1 : 0) { name = "tutorial-condition-mission-" + index };
                missionChoice.RegisterValueChangedCallback(_ => ChangeCondition(index, value =>
                {
                    if (missionChoice.index == 0) { value.missionIndex = -1; value.missionKind = (MissionKind)(-1); }
                    else value.SelectMission(missionChoice.index - 1, owner.Missions[missionChoice.index - 1]);
                })); card.Add(missionChoice);
                card.Add(new HelpBox("이 단계에서 실제로 늘어난 진행량만 셉니다. 스테이지 승리 조건은 변경하지 않습니다.", HelpBoxMessageType.Info)); return;
            }
            if (condition.kind == TutorialConditionKind.ItemUsed)
            {
                var item = new PopupField<string>("사용 아이템", new List<string> { "망치", "교환", "섞기" }, (int)condition.item) { name = "tutorial-condition-item-" + index };
                item.RegisterValueChangedCallback(_ => ChangeCondition(index, value => value.item = (BoardItem)item.index)); card.Add(item);
                card.Add(new HelpBox("실제 사용 성공만 집계합니다. 실패·취소는 무료 횟수를 소진하지 않습니다.", HelpBoxMessageType.Info)); return;
            }
            if (condition.kind == TutorialConditionKind.DurabilityDecrease)
            {
                var mode = new PopupField<string>("감소량 계산", new List<string> { "전체 합계", "각 대상마다" }, (int)condition.aggregation);
                mode.RegisterValueChangedCallback(_ => ChangeCondition(index, value => value.aggregation = (TutorialDamageAggregation)mode.index)); card.Add(mode);
            }
            AddConditionTarget(card, index);
            if (condition.kind == TutorialConditionKind.Generated || condition.kind == TutorialConditionKind.Activated)
            {
                ElementDefinition[] powers = owner.CreateElementCatalog().Definitions.Where(value => value.Supply?.Behavior == ElementSupplyBehavior.Power).ToArray();
                var labels = new List<string> { "선택하세요" }; labels.AddRange(powers.Select(value => value.DisplayName + " (" + value.Id.Value + ")"));
                var choice = new PopupField<string>("파워 종류", labels, Array.FindIndex(powers, value => value.Id.Value == condition.powerDefinitionId) + 1) { name = "tutorial-power-kind-" + index };
                choice.RegisterValueChangedCallback(_ => ChangeCondition(index, value => { value.powerDefinitionId = choice.index == 0 ? "" : powers[choice.index - 1].Id.Value; value.anyDirection = true; })); card.Add(choice);
                if (powers.FirstOrDefault(value => value.Id.Value == condition.powerDefinitionId)?.Supply.Content == RuntimeContent.Rocket)
                {
                    var direction = new PopupField<string>("로켓 방향", new List<string> { "방향 무관", "가로", "세로" }, condition.anyDirection ? 0 : (int)condition.rocketDirection + 1);
                    direction.RegisterValueChangedCallback(_ => ChangeCondition(index, value => { value.anyDirection = direction.index == 0; if (!value.anyDirection) value.rocketDirection = (RocketDirection)(direction.index - 1); })); card.Add(direction);
                }
                if (condition.kind == TutorialConditionKind.Generated)
                    card.Add(new PropertyField(property.FindPropertyRelative("bindGeneratedAs"), "생성 결과 연결 이름 (선택)") { name = "tutorial-binding-name-" + index });
                else card.Add(new HelpBox("직접 조작한 단독 파워만 인정합니다. 연쇄 발동과 두 파워 조합은 제외합니다.", HelpBoxMessageType.Info));
            }
            if (condition.kind == TutorialConditionKind.Removed || condition.kind == TutorialConditionKind.DurabilityDecrease || condition.kind == TutorialConditionKind.Combined)
            {
                var origins = new Foldout { text = condition.kind == TutorialConditionKind.DurabilityDecrease ? "허용 원인 · 미선택 시 모든 피해" : "허용 원인 · 하나 이상 선택", value = true };
                if (condition.kind != TutorialConditionKind.Combined && condition.allowedOrigins.Any(value => TutorialTargetCapabilities.OriginError(targetDefinition, value) != null))
                    origins.Add(new Button(() => ChangeCondition(index, value => value.allowedOrigins.RemoveAll(origin => TutorialTargetCapabilities.OriginError(targetDefinition, origin) != null)))
                        { text = "대상 변경으로 사용할 수 없어진 원인 제거", name = "tutorial-origin-clear-invalid-" + index });
                foreach (EffectOrigin origin in Enum.GetValues(typeof(EffectOrigin)))
                {
                    if (origin == EffectOrigin.Unknown || condition.kind == TutorialConditionKind.Combined && origin < EffectOrigin.RocketRocket) continue;
                    EffectOrigin selectedOrigin = origin;
                    var toggle = new Toggle(TutorialSampleCatalog.OriginLabel(origin)) { value = condition.allowedOrigins.Contains(origin), name = "tutorial-origin-" + index + "-" + origin };
                    string originError = condition.kind == TutorialConditionKind.Combined ? null : TutorialTargetCapabilities.OriginError(targetDefinition, origin);
                    if (originError != null) { toggle.SetEnabled(false); toggle.tooltip = originError; }
                    toggle.RegisterValueChangedCallback(evt => ChangeCondition(index, value => { if (evt.newValue) value.allowedOrigins.Add(selectedOrigin); else value.allowedOrigins.Remove(selectedOrigin); })); origins.Add(toggle);
                }
                card.Add(origins);
            }
        }

        private void AddConditionTarget(VisualElement card, int index)
        {
            TutorialConditionDefinition condition = owner.Tutorial.steps[selected].conditions[index];
            if (condition.target == null)
            { card.Add(new Button(() => ChangeCondition(index, value => value.target = new TutorialTargetDefinition())) { text = "조건 대상 지정" }); return; }
            TutorialTargetDefinition target = condition.target;
            var kinds = condition.kind == TutorialConditionKind.Generated ? new List<TutorialTargetKind> { TutorialTargetKind.Board, TutorialTargetKind.Area } :
                Enum.GetValues(typeof(TutorialTargetKind)).Cast<TutorialTargetKind>().ToList();
            string[] labels = { "보드 전체", "특정 개체", "종류", "선택 영역", "이전 생성 결과" };
            var kind = new PopupField<string>("조건 대상", kinds.Select(value => labels[(int)value]).ToList(), Math.Max(0, kinds.IndexOf(target.kind))) { name = "tutorial-target-kind-" + index };
            kind.RegisterValueChangedCallback(_ => ChangeCondition(index, value => value.target.kind = kinds[kind.index])); card.Add(kind);
            if (condition.kind < TutorialConditionKind.Generated)
            {
                var layer = new PopupField<string>("대상 층", new List<string> { "블록 / 장애물", "덮개", "바닥" }, (int)target.layer);
                layer.RegisterValueChangedCallback(_ => ChangeCondition(index, value => value.target.layer = (TutorialTargetLayer)layer.index)); card.Add(layer);
            }
            if (target.kind == TutorialTargetKind.Entity || target.kind == TutorialTargetKind.Area)
            {
                card.Add(new Label(target.kind == TutorialTargetKind.Entity ? $"선택한 개체의 시작 칸: {target.coordinate}" : $"선택 영역: {target.cells.Count}칸"));
                card.Add(new Button(() => BeginConditionTargetPicking(index)) { text = target.kind == TutorialTargetKind.Entity ? "보드에서 개체 선택" : "보드에서 영역 선택", name = "tutorial-target-pick-" + index });
            }
            if (target.kind == TutorialTargetKind.Definition)
            {
                ElementDefinition[] definitions = owner.CreateElementCatalog().Definitions.ToArray();
                var choices = new List<string> { "선택하세요" }; choices.AddRange(definitions.Select(value => value.DisplayName + " (" + value.Id.Value + ")"));
                var definition = new PopupField<string>("대상 종류", choices, Array.FindIndex(definitions, value => value.Id.Value == target.definitionId) + 1);
                definition.RegisterValueChangedCallback(_ => ChangeCondition(index, value => value.target.definitionId = definition.index == 0 ? "" : definitions[definition.index - 1].Id.Value)); card.Add(definition);
            }
            if (target.kind == TutorialTargetKind.Generated)
            {
                List<string> names = owner.Tutorial.steps.Take(selected).SelectMany(value => value.conditions).Select(value => value.bindGeneratedAs).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList();
                names.Insert(0, "선택하세요");
                var binding = new PopupField<string>("생성 연결", names, Math.Max(0, names.IndexOf(target.binding)));
                binding.RegisterValueChangedCallback(_ => ChangeCondition(index, value => value.target.binding = binding.index == 0 ? "" : binding.value)); card.Add(binding);
                if (names.Count == 1) card.Add(new HelpBox("이전 단계의 파워 생성 조건에서 연결 이름을 먼저 지정하세요.", HelpBoxMessageType.Info));
            }
        }

        private void BeginConditionTargetPicking(int index)
        {
            CancelPicking(); board.CancelStroke();
            bool area = owner.Tutorial.steps[selected].conditions[index].target.kind == TutorialTargetKind.Area;
            var cells = new HashSet<BoardCoordinate>(area ? owner.Tutorial.steps[selected].conditions[index].target.cells : new List<BoardCoordinate>());
            picker = coordinate =>
            {
                if (!area) { ChangeCondition(index, value => value.target.coordinate = coordinate); return; }
                if (!cells.Add(coordinate)) cells.Remove(coordinate);
                board.ShowTutorialTargets(cells.ToList(), null, null); pickStatus.text = $"영역 {cells.Count}칸 선택 · 다시 누르면 제외 · 선택 완료로 확정";
            };
            if (area)
                fields.Q("tutorial-condition-" + index).Add(new Button(() => ChangeCondition(index, value => value.target.cells = cells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToList()))
                    { text = "영역 선택 완료", name = "tutorial-target-confirm" });
            board.TutorialTargetPicked = picker; board.Focus(); pickStatus.text = area ? "대상 영역의 칸을 선택하세요. Esc로 취소" : "대상 개체가 있는 칸을 선택하세요. Esc로 취소";
        }

        private void AddConditionSamples()
        {
            List<TutorialConditionSample> samples = TutorialSampleCatalog.ConditionSamples(owner.CreateElementCatalog()).ToList();
            if (owner.Tutorial.steps[selected].kind == TutorialStepKind.Item)
            {
                samples.RemoveAll(value => value.CreateConditions().Any(condition => condition.kind == TutorialConditionKind.SuccessfulSwap));
                samples.Insert(0, TutorialSampleCatalog.ItemSample(owner.Tutorial.steps[selected].item));
            }
            var choice = new PopupField<string>("기본 조건 샘플", samples.Select(value => value.Title).ToList(), 0) { name = "tutorial-condition-sample" };
            var preview = new Label(samples[0].Preview) { name = "tutorial-condition-sample-preview" };
            preview.style.whiteSpace = WhiteSpace.Normal;
            choice.RegisterValueChangedCallback(_ => preview.text = samples[choice.index].Preview);
            fields.Add(choice); fields.Add(preview);
            fields.Add(new Button(() =>
            {
                Mutate("튜토리얼 조건 샘플 적용", () => { Undo.RecordObject(owner, "튜토리얼 조건 샘플 적용");
                owner.Tutorial.steps[selected].conditions.AddRange(samples[choice.index].CreateConditions());
                EditorUtility.SetDirty(owner); }); Rebuild();
            }) { text = "현재 단계에 샘플 조건 추가", name = "tutorial-condition-sample-apply" });
        }

        private bool ShowObstacleSamplePreview(BoardCoordinate coordinate)
        {
            TutorialTargetDefinition target = null;
            ElementDefinition definition = null;
            foreach (TutorialTargetLayer layer in new[] { TutorialTargetLayer.Cover, TutorialTargetLayer.Content, TutorialTargetLayer.Floor })
            {
                var candidate = new TutorialTargetDefinition { kind = TutorialTargetKind.Entity, layer = layer, coordinate = coordinate };
                ElementDefinition found = TutorialTargetCapabilities.Resolve(owner, candidate, true);
                if (found?.Placement == null) continue;
                target = candidate; definition = found; break;
            }
            if (target == null) return false;
            board.TutorialTargetPicked = null; picker = null; samplePreview.Clear();
            TutorialConditionSample[] samples = TutorialSampleCatalog.ConditionSamples(owner.CreateElementCatalog()).Where(value =>
                value.CreateConditions().All(condition => condition.kind == TutorialConditionKind.DurabilityDecrease ||
                    condition.kind == TutorialConditionKind.RemainingDurability || condition.kind == TutorialConditionKind.Removed)).ToArray();
            var choice = new PopupField<string>("선택한 대상: " + definition.DisplayName, samples.Select(value => value.Title).ToList(), 0) { name = "tutorial-obstacle-sample" };
            var preview = new Label(); preview.style.whiteSpace = WhiteSpace.Normal;
            var apply = new Button(() =>
            {
                List<TutorialConditionDefinition> conditions = samples[choice.index].CreateConditions();
                foreach (TutorialConditionDefinition condition in conditions)
                    condition.target = new TutorialTargetDefinition { kind = TutorialTargetKind.Entity, layer = target.layer, coordinate = coordinate };
                Mutate("대상 튜토리얼 샘플 적용", () => { Undo.RecordObject(owner, "대상 튜토리얼 샘플 적용");
                owner.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Swap, instructions = "표시된 대상을 확인하고 블록을 교환해 주세요.",
                    highlights = new List<BoardCoordinate> { coordinate }, conditions = conditions });
                selected = owner.Tutorial.steps.Count - 1; EditorUtility.SetDirty(owner); }); Rebuild();
            }) { text = "새 단계로 적용 · 다음에 조작 셀 지정", name = "tutorial-obstacle-sample-apply" };
            void ShowPreview()
            {
                TutorialConditionSample sample = samples[choice.index];
                string reason = sample.CreateConditions().SelectMany(condition => condition.allowedOrigins)
                    .Select(origin => TutorialTargetCapabilities.OriginError(definition, origin)).FirstOrDefault(value => value != null);
                preview.text = sample.Preview + "\n조건 대상은 선택한 개체입니다. 적용 후 ‘보드에서 교환 두 칸 선택’으로 조작 셀을 지정하세요." + (reason == null ? "" : "\n" + reason);
                apply.SetEnabled(reason == null); apply.tooltip = reason ?? "";
            }
            choice.RegisterValueChangedCallback(_ => ShowPreview());
            samplePreview.Add(choice); samplePreview.Add(preview); samplePreview.Add(apply);
            samplePreview.Add(new Button(CancelPicking) { text = "취소" }); ShowPreview();
            board.ShowTutorialTargets(new[] { coordinate }, null, null);
            pickStatus.text = "대상에 맞는 샘플을 확인하세요. 적용 전에는 단계가 추가되지 않습니다.";
            return true;
        }
    }
}

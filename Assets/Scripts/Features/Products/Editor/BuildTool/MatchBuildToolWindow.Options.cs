using System;
using System.Linq;
using UnityEngine.UIElements;

namespace Products.Editor
{
    public sealed partial class MatchBuildToolWindow
    {
        internal BuildToolOptions OptionRegistry = BuildToolOptions.Default;
        private string optionError;

        private void DrawOptions(ProductDefinition product)
        {
            optionError = null;
            var section = Section(detail, "options-section", "03  빌드 옵션", "이 프로필에 기억할 선택입니다. 실행을 확인할 때 값이 확정되며 원본 프로필 설정은 바꾸지 않습니다.");
            var label = new Label("추가 옵션: 등록된 추가 옵션 없음") { name = "build-options" }; label.AddToClassList("caption"); section.Add(label);
            if (product == null) return;
            try
            {
                var available = OptionRegistry.Available(product);
                var choices = BuildToolPreferences.Choices(SelectionPath, selectedGuid);
                if (available.Count > 0) label.text = "추가 옵션 · 이번 실행에 적용할 값";
                foreach (var definition in available)
                {
                    bool enabled = choices.TryGetValue(definition.Id, out bool saved) ? saved : definition.DefaultEnabled;
                    var card = new UnityEngine.UIElements.VisualElement(); card.AddToClassList("option-card"); card.EnableInClassList("enabled", enabled); section.Add(card);
                    Action<bool> saveChoice = value =>
                    {
                        try { BuildToolPreferences.SaveChoice(SelectionPath, selectedGuid, definition.Id, value); }
                        catch (Exception error) { preferenceNotice = "옵션 저장 실패: " + error.Message; }
                        DrawDetail(); RefreshExecution();
                    };
                    bool selection = definition.DisabledLabel != null && definition.EnabledLabel != null;
                    if (selection)
                    {
                        var choice = new DropdownField(definition.Label, new System.Collections.Generic.List<string> { definition.DisabledLabel, definition.EnabledLabel }, enabled ? 1 : 0)
                        { name = "option-" + definition.Id, tooltip = definition.Tooltip };
                        choice.RegisterValueChangedCallback(change => saveChoice(change.newValue == definition.EnabledLabel)); card.Add(choice);
                    }
                    else
                    {
                        var toggle = new Toggle(definition.Label + " · 기본 " + (definition.DefaultEnabled ? "ON" : "OFF"))
                        { name = "option-" + definition.Id, tooltip = definition.Tooltip, value = enabled };
                        toggle.RegisterValueChangedCallback(change => saveChoice(change.newValue)); card.Add(toggle);
                    }
                    var state = new Label(selection ? (enabled ? definition.EnabledLabel : definition.DisabledLabel) + " · 이번 빌드 형식" :
                        enabled ? "ON · 이번 빌드에 적용" : "OFF · 이번 빌드에서 사용하지 않음"); state.AddToClassList("badge"); state.AddToClassList(enabled ? "good" : "unknown"); card.Add(state);
                    var help = new Label(definition.Tooltip); help.AddToClassList("caption"); card.Add(help);
                }
                string[] ignored = choices.Keys.Except(available.Select(definition => definition.Id)).ToArray();
                if (ignored.Length > 0) section.Add(new HelpBox("현재 제품에서 지원하지 않아 적용하지 않는 선택: " + string.Join(", ", ignored), HelpBoxMessageType.Warning));
                try { OptionRegistry.Resolve(product, choices); }
                catch (Exception error) { optionError = error.Message; section.Add(new HelpBox(optionError, HelpBoxMessageType.Error)); }
            }
            catch (Exception error)
            {
                optionError = error.Message;
                section.Add(new HelpBox("옵션 선택 기록을 확인하세요: " + error.Message, HelpBoxMessageType.Error));
            }
        }
    }
}

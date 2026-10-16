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
            var label = new Label("추가 옵션: 등록된 추가 옵션 없음") { name = "build-options", style = { marginTop = 14 } }; detail.Add(label);
            if (product == null) return;
            try
            {
                var available = OptionRegistry.Available(product);
                var choices = BuildToolPreferences.Choices(SelectionPath, selectedGuid);
                if (available.Count > 0) label.text = "추가 옵션 · 이번 실행에 적용할 값";
                foreach (var definition in available)
                {
                    bool enabled = choices.TryGetValue(definition.Id, out bool saved) ? saved : definition.DefaultEnabled;
                    var toggle = new Toggle(definition.Label + " · 기본 " + (definition.DefaultEnabled ? "ON" : "OFF"))
                    { name = "option-" + definition.Id, tooltip = definition.Tooltip, value = enabled };
                    toggle.RegisterValueChangedCallback(change =>
                    {
                        try { BuildToolPreferences.SaveChoice(SelectionPath, selectedGuid, definition.Id, change.newValue); }
                        catch (Exception error) { preferenceNotice = "옵션 저장 실패: " + error.Message; }
                        DrawDetail(); RefreshExecution();
                    });
                    detail.Add(toggle);
                    detail.Add(new Label(definition.Tooltip) { style = { whiteSpace = WhiteSpace.Normal } });
                }
                string[] ignored = choices.Keys.Except(available.Select(definition => definition.Id)).ToArray();
                if (ignored.Length > 0) detail.Add(new HelpBox("현재 제품에서 지원하지 않아 적용하지 않는 선택: " + string.Join(", ", ignored), HelpBoxMessageType.Warning));
                try { OptionRegistry.Resolve(product, choices); }
                catch (Exception error) { optionError = error.Message; detail.Add(new HelpBox(optionError, HelpBoxMessageType.Error)); }
            }
            catch (Exception error)
            {
                optionError = error.Message;
                detail.Add(new HelpBox("옵션 선택 기록을 확인하세요: " + error.Message, HelpBoxMessageType.Error));
            }
        }
    }
}

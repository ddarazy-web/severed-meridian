#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private sealed class MobilePendingInput
        {
            public Action<bool> Restore;
            public FocusedInputState Focus;
        }
        private readonly Dictionary<VisualElement, MobilePendingInput> mobilePendingInputs = new Dictionary<VisualElement, MobilePendingInput>();

        private void SuspendMobilePanelInput()
        {
            VisualElement input = FindCommandInput(root.focusController?.focusedElement as VisualElement);
            if (input == null || mobilePendingInputs.ContainsKey(input)) return;
            Action<bool> restore = input is TextField text ? SuspendMobileInput(text) :
                input is IntegerField integer ? SuspendMobileInput(integer) :
                input is FloatField single ? SuspendMobileInput(single) :
                input is DoubleField number ? SuspendMobileInput(number) :
                input is LongField wide ? SuspendMobileInput(wide) : null;
            if (restore != null) mobilePendingInputs.Add(input, new MobilePendingInput { Restore = restore, Focus = CaptureFocusedInput() });
        }

        private Action<bool> SuspendMobileInput<T>(TextInputBaseField<T> field)
        {
            if (!field.isDelayed) return null;
            T value = field.value;
            string raw = field.text;
            int cursor = field.cursorIndex, selection = field.selectIndex;
            EventCallback<ChangeEvent<T>> guard = evt =>
            {
                evt.StopImmediatePropagation();
            };
            field.RegisterCallback(guard, TrickleDown.TrickleDown);
            // 숫자 필드는 Blur에서 문자열을 정규화하므로 해당 처리가 끝난 뒤 숨긴 초안을 되돌린다.
            EventCallback<BlurEvent> restoreAfterBlur = _ => root.schedule.Execute(RestoreMobilePanelInputs);
            field.RegisterCallback(restoreAfterBlur, TrickleDown.TrickleDown);
            // 숨김으로 발생하는 Blur가 미확정 입력을 문서에 적용하지 않도록 잠시 확정을 끈다.
            field.isDelayed = false;
            return focus =>
            {
                field.SetValueWithoutNotify(value);
                TextElement text = field.Q(className: "unity-base-field__input").Q<TextElement>();
                ((INotifyValueChanged<string>)text).SetValueWithoutNotify(raw);
                field.SelectRange(cursor, selection);
                if (focus)
                {
                    field.isDelayed = true;
                    field.UnregisterCallback(guard, TrickleDown.TrickleDown);
                    field.UnregisterCallback(restoreAfterBlur, TrickleDown.TrickleDown);
                    field.Focus();
                    field.schedule.Execute(() => field.SelectRange(cursor, selection));
                }
            };
        }

        private void RestoreMobilePanelInputs()
        {
            foreach (VisualElement input in new List<VisualElement>(mobilePendingInputs.Keys))
            {
                if (!root.Contains(input)) { mobilePendingInputs.Remove(input); continue; }
                bool visible = true;
                for (VisualElement ancestor = input; ancestor != root; ancestor = ancestor.parent)
                    if (ancestor.style.display == DisplayStyle.None) { visible = false; break; }
                mobilePendingInputs[input].Restore(visible);
                if (visible) mobilePendingInputs.Remove(input);
            }
        }

        private VisualElement RestoreMobileCommandInput(out FocusedInputState focus)
        {
            focus = null;
            foreach (var pair in mobilePendingInputs.ToArray())
            {
                VisualElement input = pair.Key;
                if (!root.Contains(input)) { mobilePendingInputs.Remove(input); continue; }
                focus = pair.Value.Focus;
                mobilePanel = rightPanel.Contains(input) ? "properties" : leftPanel.Contains(input) ?
                    materialsVisible ? "materials" : "levels" : "more";
                UpdatePanelVisibility();
                // 저장이나 Undo는 숨긴 입력도 기존 검증·확정 처리로 전달한다.
                RestoreMobilePanelInputs();
                return input;
            }
            return null;
        }
    }
}
#endif

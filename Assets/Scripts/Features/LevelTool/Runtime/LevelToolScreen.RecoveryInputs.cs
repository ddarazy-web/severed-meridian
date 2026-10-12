#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        // 문서에 아직 확정하지 않은 문자열은 별도의 화면 초안으로 보관한다.
        private JArray CaptureRecoveryInputs()
        {
            var inputs = new JArray();
            foreach (TextField field in editor.Query<TextField>().ToList()) CaptureRecoveryInput(field, inputs);
            foreach (IntegerField field in editor.Query<IntegerField>().ToList()) CaptureRecoveryInput(field, inputs);
            foreach (LongField field in editor.Query<LongField>().ToList()) CaptureRecoveryInput(field, inputs);
            foreach (FloatField field in editor.Query<FloatField>().ToList()) CaptureRecoveryInput(field, inputs);
            foreach (DoubleField field in editor.Query<DoubleField>().ToList()) CaptureRecoveryInput(field, inputs);
            return inputs;
        }

        private void CaptureRecoveryInput<T>(TextInputBaseField<T> field, JArray inputs)
        {
            bool focused = ReferenceEquals(root.focusController?.focusedElement, field) ||
                root.focusController?.focusedElement is VisualElement currentFocus && field.Contains(currentFocus);
            if (!focused && !mobilePendingInputs.ContainsKey(field) && field.text == Convert.ToString(field.value, CultureInfo.InvariantCulture)) return;
            var path = new List<int>();
            for (VisualElement current = field; current != editor && current.parent != null; current = current.parent)
                path.Insert(0, current.parent.hierarchy.IndexOf(current));
            inputs.Add(new JObject
            {
                ["path"] = new JArray(path), ["name"] = field.name, ["type"] = field.GetType().FullName,
                ["text"] = field.text, ["value"] = field.value == null ? JValue.CreateNull() : JToken.FromObject(field.value),
                ["cursor"] = field.cursorIndex, ["selection"] = field.selectIndex,
                ["focused"] = focused
            });
        }

        private void RestoreRecoveryInputs(JArray inputs)
        {
            if (inputs == null) return;
            foreach (JObject input in inputs)
            {
                VisualElement field = editor;
                foreach (int index in input["path"].Values<int>())
                {
                    if (index < 0 || index >= field.hierarchy.childCount) { field = null; break; }
                    field = field.hierarchy[index];
                }
                if (field == null || field.name != (string)input["name"] || field.GetType().FullName != (string)input["type"]) continue;
                if (field is TextField text) RestoreRecoveryInput(text, input);
                else if (field is IntegerField integer) RestoreRecoveryInput(integer, input);
                else if (field is LongField wide) RestoreRecoveryInput(wide, input);
                else if (field is FloatField single) RestoreRecoveryInput(single, input);
                else if (field is DoubleField number) RestoreRecoveryInput(number, input);
            }
        }

        private void RestoreRecoveryInput<T>(TextInputBaseField<T> field, JObject input)
        {
            TextElement text = field.Q(className: "unity-base-field__input")?.Q<TextElement>();
            if (text == null) return;
            ((INotifyValueChanged<string>)text).SetValueWithoutNotify((string)input["text"] ?? "");
            field.SelectRange(Math.Min((int?)input["cursor"] ?? 0, field.text.Length), Math.Min((int?)input["selection"] ?? 0, field.text.Length));
            if ((bool?)input["focused"] == true) field.Focus();
            // 숨긴 패널의 Blur가 미확정 문자열을 잃지 않도록 기존 입력 보류를 사용한다.
            bool hidden = false;
            for (VisualElement ancestor = field; ancestor != editor && ancestor != null; ancestor = ancestor.parent)
                if (ancestor.style.display == DisplayStyle.None) { hidden = true; break; }
            if (hidden)
            {
                Action<bool> restore = SuspendMobileInput(field);
                if (restore != null) mobilePendingInputs[field] = new MobilePendingInput { Restore = restore };
            }
        }

        private void CheckpointOnInterruption()
        {
            if (busy || root == null || Session == null || draftStore == null) return;
            try { FlushRecovery(); }
            catch (Exception error) { Show("중단 복구 기록 실패: " + error.Message); }
        }
    }
}
#endif

using UnityEditor;
using UnityEngine.UIElements;

namespace Products.Editor
{
    public sealed partial class MatchBuildToolWindow
    {
        private static VisualElement Section(VisualElement parent, string name, string title, string description)
        {
            var card = new VisualElement { name = name }; card.AddToClassList("section"); parent.Add(card);
            var heading = new Label(title); heading.AddToClassList("section-title"); card.Add(heading);
            var caption = new Label(description) { tooltip = description }; caption.AddToClassList("caption"); card.Add(caption);
            return card;
        }

        private static void InfoField(VisualElement parent, string title, string value, string description)
        {
            var field = new VisualElement { tooltip = description }; field.AddToClassList("info-field"); parent.Add(field);
            var label = new Label(title); label.AddToClassList("field-label"); field.Add(label);
            var content = new Label(value) { tooltip = value, selection = { isSelectable = true } }; content.AddToClassList("field-value"); field.Add(content);
        }

        private void ApplyPresentation()
        {
            var root = rootVisualElement; root.AddToClassList("build-tool"); root.EnableInClassList("light", !EditorGUIUtility.isProSkin);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Features/Products/Editor/BuildTool/MatchBuildToolWindow.uss");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
        }
    }
}

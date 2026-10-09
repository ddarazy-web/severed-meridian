using Elements;
using Levels;
using Levels.Editor;
using Tutorial;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LegacyAuthoringInspection
    {
        public static VisualElement Create(SerializedObject data, UnityEditor.Editor owner)
        {
            var root = new VisualElement();
            root.Add(new HelpBox("보관 중인 구형 SO 원본입니다. 기본 제작 데이터는 JSON입니다. 여기서는 내용을 확인하고, 수정은 레벨툴에서 진행하세요.", HelpBoxMessageType.Info));
            root.Add(new Button(LevelTool.Editor.LevelToolLauncher.Launch) { text = "기본 JSON 제작 도구 열기", name = "open-json-tool" });
            if (data.targetObject is LevelDefinition level)
                root.Add(new Button(() => LevelTool.Editor.LevelToolLegacyImport.Open(level)) { text = "구형 레벨을 별도 JSON 사본으로 가져오기", name = "open-legacy-copy" });
            var fields = new VisualElement { name = "legacy-authoring-fields" };
            InspectorElement.FillDefaultInspector(fields, data, owner);
            fields.SetEnabled(false); root.Add(fields);
            return root;
        }
    }

    public abstract class LegacyAuthoringInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI() => LegacyAuthoringInspection.Create(serializedObject, this);
    }
    [CustomEditor(typeof(ElementDefinitionAsset))] public sealed class LegacyElementInspector : LegacyAuthoringInspector { }
    [CustomEditor(typeof(ElementCatalogAsset))] public sealed class LegacyCatalogInspector : LegacyAuthoringInspector { }
    [CustomEditor(typeof(ElementVisualCatalogAsset))] public sealed class LegacyVisualInspector : LegacyAuthoringInspector { }
    [CustomEditor(typeof(TutorialFlowDefinition))] public sealed class LegacyFlowInspector : LegacyAuthoringInspector { }
    [CustomEditor(typeof(TutorialUserSampleDefinition))] public sealed class LegacySampleInspector : LegacyAuthoringInspector { }
    [CustomEditor(typeof(LevelShapePreset))] public sealed class LegacyShapeInspector : LegacyAuthoringInspector { }
}

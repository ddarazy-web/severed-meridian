using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LegacyInspectorVerification
    {
        public static void Run()
        {
            try
            {
                foreach (var source in LegacyContentExporter.Discover().GroupBy(value => value.Kind).Select(group => group.First()))
                {
                    var inspector = UnityEditor.Editor.CreateEditor(source.Asset);
                    try
                    {
                        var root = inspector.CreateInspectorGUI();
                        var fields = root?.Q("legacy-authoring-fields");
                        if (fields == null || fields.enabledSelf) throw new Exception("FAIL saved SO remains directly editable: " + source.Kind);
                        if (root.Q("save-level") != null) throw new Exception("FAIL old SO save entry remains");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(inspector); }
                }
                Debug.Log("PASS stored legacy authoring inspectors are read-only and have no direct save entry");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}

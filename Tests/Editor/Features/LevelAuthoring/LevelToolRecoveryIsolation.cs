using System;
using System.Reflection;
using LevelTool;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolRecoveryIsolation
    {
        public static void Configure(string folder)
        {
            LevelToolScreen screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
            using var serialized = new SerializedObject(screen);
            serialized.FindProperty("recoveryDirectory").stringValue = folder;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
            EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
        }

        public static void Verify(LevelToolScreen screen, string folder)
        {
            string actual = (string)typeof(LevelToolScreen).GetField("recoveryDirectory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(screen);
            if (actual != folder) throw new Exception("Test recovery path was not preserved: " + actual);
        }
    }
}

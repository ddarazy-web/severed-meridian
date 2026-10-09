using System;
using System.Collections.Generic;
using Levels;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class LevelAssetOperations
    {
        public const string DefaultFolder = "Assets/Data/Levels";
        public static void CreateLevelAsset()
        {
            LevelTool.Editor.LevelToolLauncher.Launch();
        }

        public static LevelDefinition CreateNamed(string fileName)
        {
            string error = FileNameError(fileName);
            if (error != null) throw new ArgumentException(error, nameof(fileName));
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
                AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(DefaultFolder))
                AssetDatabase.CreateFolder("Assets/Data", "Levels");

            string path = DefaultFolder + "/" + fileName.Trim() + ".asset";
            LevelDefinition level = CreateElementAtPath(path);
            Selection.activeObject = level;
            EditorGUIUtility.PingObject(level);
            return level;
        }

        // 구형 제작·고정 회귀 입력의 생성 계약은 별도로 유지한다.
        public static LevelDefinition CreateAtPath(string path) => CreateAtPath(path, false);
        public static LevelDefinition CreateElementAtPath(string path) => CreateAtPath(path, true);

        private static LevelDefinition CreateAtPath(string path, bool elements)
        {
            // 명시적으로 지정한 경로의 기존 에셋을 덮어쓰지 않는다.
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/../") || path.Contains("/./") || path.Contains("\\"))
                throw new ArgumentException("Assets 내부의 .asset 경로를 지정해야 합니다.", nameof(path));
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)) || System.IO.File.Exists(path))
                throw new ArgumentException("이미 존재하는 에셋 경로입니다.", nameof(path));

            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                LevelSupplyEditing.AddTopSources(level);
                if (elements)
                {
                    ElementLevelSupplyDefinition supply = ElementLevelSupplyDefinition.FromLegacy(level.Supply);
                    JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"supply\":{\"sources\":[]},\"elementSupply\":" + JsonUtility.ToJson(supply) + "}", level);
                    Elements.ElementCatalogAsset defaults = AssetDatabase.LoadAssetAtPath<Elements.ElementCatalogAsset>(Elements.Editor.ElementContentAuthoring.CatalogPath);
                    if (defaults != null)
                    {
                        SerializedObject input = new SerializedObject(level);
                        input.FindProperty("elementCatalog").objectReferenceValue = defaults;
                        input.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                AssetDatabase.CreateAsset(level, path);
                AssetDatabase.SaveAssetIfDirty(level);
                return level;
            }
            catch
            {
                if (!EditorUtility.IsPersistent(level))
                    UnityEngine.Object.DestroyImmediate(level);
                throw;
            }
        }

        public static List<LevelValidationIssue> FindNumberConflicts(LevelDefinition level)
        {
            List<LevelValidationIssue> issues = new List<LevelValidationIssue>();
            if (level == null)
                return issues;

            foreach (string guid in AssetDatabase.FindAssets("t:LevelDefinition", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                LevelDefinition other = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (other != null && other != level && other.LevelNumber == level.LevelNumber)
                    issues.Add(new LevelValidationIssue(LevelValidationCode.DuplicateLevelNumber,
                        $"레벨 번호 {level.LevelNumber}가 중복됩니다: {path}", "levelNumber"));
            }
            return issues;
        }
    }
}

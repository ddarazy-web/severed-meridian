using System;
using System.IO;
using System.Linq;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    /// <summary>확정된 기존 정의를 제작 원본으로 옮긴다. 재실행으로 편집값을 덮어쓰지 않는다.</summary>
    [InitializeOnLoad]
    public static class ElementContentAuthoring
    {
        public const string Folder = "Assets/Data/Elements";
        public const string CatalogPath = Folder + "/DefaultElementCatalog.asset";
        public const string VisualPath = Folder + "/DefaultElementVisuals.asset";
        public const string RulesDocument = "Docs/Contents/MoonRabbitJunkyard/04_게임규칙.md";
        static ElementContentAuthoring()
        {
            ElementAuthoringDefaults.Configure(
                () => AssetDatabase.LoadAssetAtPath<ElementCatalogAsset>(CatalogPath)?.CreateCatalog(),
                () => AssetDatabase.LoadAssetAtPath<ElementVisualCatalogAsset>(VisualPath)?.CreateCatalog());
        }

        [MenuItem("MATCH/구형 자료 복구/SO 제작 원본 초기화")]
        public static void Initialize()
        {
            if (!File.Exists(RulesDocument)) throw new InvalidOperationException("확정 기획 문서를 찾을 수 없습니다.");
            Directory.CreateDirectory(Folder + "/Definitions");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ElementCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<ElementCatalogAsset>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ElementCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            ElementVisualCatalogAsset visuals = AssetDatabase.LoadAssetAtPath<ElementVisualCatalogAsset>(VisualPath);
            if (visuals == null)
            {
                visuals = ScriptableObject.CreateInstance<ElementVisualCatalogAsset>();
                JsonUtility.FromJsonOverwrite("{\"planningDocument\":\"Docs/Contents/MoonRabbitJunkyard/03_캐릭터와아트.md\",\"catalog\":" +
                    JsonUtility.ToJson(LegacyElementVisuals.Catalog.ToDto()) + "}", visuals);
                AssetDatabase.CreateAsset(visuals, VisualPath);
            }
            SerializedObject input = new SerializedObject(catalog);
            SerializedProperty list = input.FindProperty("definitions");
            string[] registered = catalog.DefinitionAssets.Where(value => value != null).Select(value => value.ToDefinition().Id.Value).ToArray();
            foreach (ElementDefinition definition in LegacyElementDefinitions.DefaultCatalog.Definitions.OrderBy(value => value.Id.Value, StringComparer.Ordinal))
            {
                if (registered.Contains(definition.Id.Value)) continue;
                string path = Folder + "/Definitions/" + definition.Id.Value + ".asset";
                ElementDefinitionAsset asset = AssetDatabase.LoadAssetAtPath<ElementDefinitionAsset>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<ElementDefinitionAsset>();
                    JsonUtility.FromJsonOverwrite("{\"planningDocument\":\"" + RulesDocument + "\",\"planningSection\":\"" +
                        definition.DisplayName + " — 확정 수치/행동과 기존 구현을 대조\",\"definition\":" +
                        JsonUtility.ToJson(PackedElementDefinition.FromDefinition(definition)) + "}", asset);
                    AssetDatabase.CreateAsset(asset, path);
                }
                if (asset.ToDefinition().Id != definition.Id) throw new InvalidOperationException("원본 경로와 정의 ID가 다릅니다: " + path);
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = asset;
            }
            if (input.FindProperty("visuals").objectReferenceValue == null) input.FindProperty("visuals").objectReferenceValue = visuals;
            input.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(visuals);
            // 기존 배치/번호를 변경하지 않고 미연결 제작 레벨에 카탈로그 참조만 추가한다.
            foreach (string guid in AssetDatabase.FindAssets("t:LevelDefinition", new[] { LevelAssetOperations.DefaultFolder }))
            {
                LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (level.ElementCatalog != null) continue;
                SerializedObject levelInput = new SerializedObject(level);
                levelInput.FindProperty("elementCatalog").objectReferenceValue = catalog;
                levelInput.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(level);
            }
            ValidatePlanning(catalog);
            Debug.Log("확정 요소 제작 원본: " + catalog.DefinitionAssets.Count + "개. 기존 편집값은 보존했습니다.");
        }

        public static void ValidatePlanning(ElementCatalogAsset catalog)
        {
            if (catalog == null) throw new ArgumentException("출시 요소 카탈로그가 없습니다. 확정 원본을 먼저 생성하세요.");
            foreach (ElementDefinitionAsset definition in catalog.DefinitionAssets)
            {
                if (definition == null) throw new ArgumentException("출시 정의 원본이 누락되었습니다.");
                string id = definition.ToDefinition().Id.Value;
                ValidateDocument(definition.PlanningDocument, id);
                if (string.IsNullOrWhiteSpace(definition.PlanningSection)) throw new ArgumentException("요소 '" + id + "'의 기획 근거 항목이 없습니다.");
            }
            if (catalog.VisualAsset == null) throw new ArgumentException("출시 표현 제작 원본이 없습니다.");
            ValidateDocument(catalog.VisualAsset.PlanningDocument, "표현 카탈로그");
            _ = catalog.CreateCatalog();
            _ = catalog.CreateVisualCatalog();
        }

        private static void ValidateDocument(string path, string id)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Docs/Contents/", StringComparison.Ordinal) ||
                !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || path.Contains("..") || path.Contains("\\") || !File.Exists(path))
                throw new ArgumentException("요소 '" + id + "'의 유효한 기획 문서가 없습니다: " + path);
        }
    }
}

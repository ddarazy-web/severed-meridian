using UnityEditor;

namespace PopupUI.Editor
{
    public static class PopupCatalogValidation
    {
        [MenuItem("Tools/Popup/등록 검사")]
        public static void ValidateSelection()
        {
            PopupCatalog catalog = Selection.activeObject as PopupCatalog;
            string[] errors = Validate(catalog);
            if (errors.Length == 0) UnityEngine.Debug.Log("팝업 등록 검사 통과: " + catalog.name);
            else UnityEngine.Debug.LogWarning(string.Join("\n", errors), catalog);
        }
        public static string[] Validate(PopupCatalog catalog)
        {
            if (catalog == null) return new[] { "카탈로그가 없습니다." };
            var errors = new System.Collections.Generic.List<string>();
            var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            PopupCatalog.Entry[] entries = catalog.Entries;
            for (int index = 0; index < entries.Length; index++)
            {
                PopupCatalog.Entry entry = entries[index];
                string label = "항목 " + index + " (" + entry?.Id + "): ";
                if (entry == null) { errors.Add(label + "등록 값이 없습니다."); continue; }
                if (string.IsNullOrWhiteSpace(entry.Id)) errors.Add(label + "ID가 비어 있습니다.");
                else if (!ids.Add(entry.Id)) errors.Add(label + "중복 ID입니다.");
                if (entry.Prefab == null) { errors.Add(label + "프리팹이 없습니다."); continue; }
                if (!(entry.Prefab.transform is UnityEngine.RectTransform)) errors.Add(label + "RectTransform이 없습니다.");
                if (entry.Prefab.GetComponent<UnityEngine.CanvasGroup>() == null) errors.Add(label + "CanvasGroup이 없습니다.");
                else if (PrefabUtility.GetPrefabAssetType(entry.Prefab) == PrefabAssetType.Regular && AssetDatabase.IsMainAsset(entry.Prefab.gameObject))
                {
                    // RequireComponent가 로드 시 보완한 누락도 일반 텍스트 프리팹의 원본에서 진단한다.
                    string path = AssetDatabase.GetAssetPath(entry.Prefab);
                    string yaml = System.IO.File.ReadAllText(path);
                    if (yaml.StartsWith("%YAML", System.StringComparison.Ordinal) && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(entry.Prefab.gameObject, out string _, out long localId))
                    {
                        string pattern = @"(?ms)^--- !u!225 &[-0-9]+\r?\nCanvasGroup:\r?\n(?:(?!^--- ).)*?^\s+m_GameObject: \{fileID: " + localId + @"\}";
                        if (!System.Text.RegularExpressions.Regex.IsMatch(yaml, pattern)) errors.Add(label + "저장된 프리팹 루트에 CanvasGroup이 없습니다. 프리팹을 확인하고 저장하세요.");
                    }
                }
            }
            return errors.ToArray();
        }
    }
}

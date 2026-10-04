using System;
using System.IO;
using System.Collections.Generic;
using PopupUI;
using PopupUI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameScreen.Editor
{
    public static class PuzzlePopupAssets
    {
        public const string CatalogPath = "Assets/Data/UI/PuzzlePopupCatalog.asset";
        [MenuItem("Tools/Popup/게임 팝업 연결")]
        public static void Apply() { ApplyToPrefabs(PuzzleUIAssets.Folder, CatalogPath); }
        public static void ApplyToPrefabs(string folder, string catalogPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play Mode에서는 프리팹을 연결하지 않습니다.");
            string pausePath = folder + "/PuzzlePausePopup.prefab", resultPath = folder + "/PuzzleResultPopup.prefab", screenPath = folder + "/PuzzleScreen.prefab", descriptionPath = folder + "/PuzzleDescriptionPopup.prefab";
            foreach (string path in new[] { pausePath, resultPath })
            {
                GameObject popup = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    PopupView view = popup.GetComponent<PopupView>();
                    if (view == null) throw new InvalidOperationException("게임 팝업 View가 없습니다: " + path);
                    if (popup.GetComponent<CanvasGroup>() == null) popup.AddComponent<CanvasGroup>();
                    Button primary = popup.transform.Find("Panel/Primary").GetComponent<Button>();
                    view.SetDefaultSelection(primary.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(popup, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(popup); }
            }
            GameObject root = PrefabUtility.LoadPrefabContents(screenPath);
            GameObject descriptionCopy = null;
            try
            {
                Transform safe = root.transform.Find("SafeArea");
                if (safe == null) throw new InvalidOperationException("기존 SafeArea를 찾을 수 없습니다.");
                Transform inline = safe.Find("MissionDescription");
                if (!File.Exists(descriptionPath))
                {
                    if (inline == null) throw new InvalidOperationException("분리할 기존 설명 UI가 없습니다.");
                    descriptionCopy = UnityEngine.Object.Instantiate(inline.gameObject); descriptionCopy.name = "PuzzleDescriptionPopup";
                    PuzzleDescriptionView descriptionView = descriptionCopy.AddComponent<PuzzleDescriptionView>();
                    descriptionView.Configure(descriptionCopy.transform.Find("Bubble/Description").GetComponent<Text>(), descriptionCopy.GetComponent<Button>());
                    PrefabUtility.SaveAsPrefabAsset(descriptionCopy, descriptionPath);
                }
                PopupCatalog catalog = AssetDatabase.LoadAssetAtPath<PopupCatalog>(catalogPath);
                if (catalog == null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)); AssetDatabase.Refresh();
                    catalog = ScriptableObject.CreateInstance<PopupCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath);
                }
                PopupView pause = AssetDatabase.LoadAssetAtPath<GameObject>(pausePath).GetComponent<PopupView>();
                PopupView result = AssetDatabase.LoadAssetAtPath<GameObject>(resultPath).GetComponent<PopupView>();
                PuzzleDescriptionView description = AssetDatabase.LoadAssetAtPath<GameObject>(descriptionPath).GetComponent<PuzzleDescriptionView>();
                List<PopupCatalog.Entry> merged = new List<PopupCatalog.Entry>(catalog.Entries);
                foreach (PopupCatalog.Entry entry in new[] {
                    new PopupCatalog.Entry { Id = PuzzlePopupBinding.PauseId, Prefab = pause, Restorable = true, PauseGameplay = true, CloseOnCancel = true },
                    new PopupCatalog.Entry { Id = PuzzlePopupBinding.ResultId, Prefab = result, Restorable = true, CloseOnCancel = false },
                    new PopupCatalog.Entry { Id = PuzzlePopupBinding.DescriptionId, Prefab = description, Restorable = true, CloseOnCancel = true }
                })
                {
                    // 이 도구가 소유한 세 ID만 갱신하고 개발자가 추가한 등록은 보존한다.
                    int existing = merged.FindIndex(item => item != null && item.Id == entry.Id);
                    if (existing < 0) merged.Add(entry); else merged[existing] = entry;
                }
                catalog.Configure(merged.ToArray());
                string[] errors = PopupCatalogValidation.Validate(catalog);
                if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors));
                EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
                Transform existingHost = safe.Find("PopupHost");
                GameObject hostRoot = existingHost == null ? new GameObject("PopupHost", typeof(RectTransform), typeof(PopupHost)) : existingHost.gameObject;
                hostRoot.transform.SetParent(safe, false); hostRoot.transform.SetAsLastSibling();
                RectTransform rect = hostRoot.transform as RectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                PuzzlePopupBinding binding = root.GetComponent<PuzzlePopupBinding>();
                if (binding == null) binding = root.AddComponent<PuzzlePopupBinding>();
                binding.ConfigureAssets(catalog, hostRoot.GetComponent<PopupHost>());
                // 기존 필드의 직렬화 계약은 유지하되 실제 표시 인스턴스는 서비스만 만든다.
                SerializedObject screen = new SerializedObject(root.GetComponent<PuzzleScreenView>());
                screen.FindProperty("pause").objectReferenceValue = pause; screen.FindProperty("result").objectReferenceValue = result;
                screen.FindProperty("description").objectReferenceValue = description.GetComponent<Button>();
                screen.FindProperty("descriptionText").objectReferenceValue = description.transform.Find("Bubble/Description").GetComponent<Text>(); screen.ApplyModifiedPropertiesWithoutUndo();
                foreach (string name in new[] { "MissionDescription", "PuzzlePausePopup", "PuzzleResultPopup" })
                { Transform embedded = safe.Find(name); if (embedded != null) UnityEngine.Object.DestroyImmediate(embedded.gameObject); }
                PrefabUtility.SaveAsPrefabAsset(root, screenPath);
            }
            finally
            {
                if (descriptionCopy != null) UnityEngine.Object.DestroyImmediate(descriptionCopy);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

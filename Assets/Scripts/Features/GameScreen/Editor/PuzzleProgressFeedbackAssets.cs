using System;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    // 기존 HUD만 연결한다. 씬 저장·전체 UI 재생성·콘텐츠 빌드를 수행하지 않는다.
    public static class PuzzleProgressFeedbackAssets
    {
        public static void Generate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play 종료 후 연결하세요.");
            string folder = PuzzleUIAssets.Folder + "/Feedback";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(PuzzleUIAssets.Folder, "Feedback");
            string path = folder + "/MissionCollection.prefab";
            GameObject source = new GameObject("MissionCollection", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            try
            {
                UnityEngine.UI.Image image = source.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = false; image.preserveAspect = true;
                image.rectTransform.sizeDelta = new Vector2(40, 40); source.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(source, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            string hudPath = PuzzleUIAssets.Folder + "/PuzzleHUD.prefab";
            GameObject contents = PrefabUtility.LoadPrefabContents(hudPath);
            try
            {
                contents.GetComponent<PuzzleHudView>().ConfigureFeedback(AssetDatabase.LoadAssetAtPath<UnityEngine.UI.Image>(path));
                PrefabUtility.SaveAsPrefabAsset(contents, hudPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}

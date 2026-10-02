using System;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    // 게임용 소리 프리팹과 세션 참조만 연결한다. 전체 씬·UI·콘텐츠는 재생성하지 않는다.
    public static class PuzzleAudioFeedbackAssets
    {
        public static void Generate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play 종료 후 연결하세요.");
            string folder = "Assets/Prefabs/Game/Puzzle/Feedback";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Prefabs/Game/Puzzle", "Feedback");
            string path = folder + "/PuzzleAudioFeedback.prefab";
            GameObject source = new GameObject("PuzzleAudioFeedback", typeof(PuzzleAudioPlayback), typeof(AudioListener));
            try { PrefabUtility.SaveAsPrefabAsset(source, path); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            string sessionPath = "Assets/Prefabs/Game/Puzzle/PuzzleGameSession.prefab";
            GameObject contents = PrefabUtility.LoadPrefabContents(sessionPath);
            try
            {
                contents.GetComponent<PuzzleGameSession>().ConfigureAudio(AssetDatabase.LoadAssetAtPath<PuzzleAudioPlayback>(path));
                PrefabUtility.SaveAsPrefabAsset(contents, sessionPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}

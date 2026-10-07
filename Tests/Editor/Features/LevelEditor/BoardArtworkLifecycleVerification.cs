using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>에디터 창을 유지한 실제 Play Mode 왕복 뒤 이미지가 자동 복구되는지 검사한다.</summary>
    [InitializeOnLoad]
    public static class BoardArtworkLifecycleVerification
    {
        private const string Key = "BoardArtworkLifecycleVerification";
        private const string WindowName = "BoardArtworkLifecycleVerificationWindow";
        private const string Output = "Logs/BoardArtworkLifecycleVerification/results.txt";
        static BoardArtworkLifecycleVerification() => EditorApplication.update += Tick;

        [MenuItem("Tools/Match/편집 보드 이미지 수명 검증")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetInt(Key, 0) != 0)
                throw new InvalidOperationException("편집 모드에서 검사를 시작하세요.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllText(Output, "");
            LevelEditorWindow window = ScriptableObject.CreateInstance<LevelEditorWindow>();
            window.name = WindowName;
            window.ShowUtility();
            window.SetLevel(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset"));
            SessionState.SetString(Key + ".original", JsonUtility.ToJson(window.CurrentLevel));
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 60);
            SessionState.SetInt(Key, 1);
        }

        private static void Tick()
        {
            int stage = SessionState.GetInt(Key, 0);
            if (stage == 0 || EditorApplication.isCompiling) return;
            LevelEditorWindow window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().FirstOrDefault(w => w.name == WindowName);
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
                    throw new TimeoutException("모드 전환 후 편집 보드 이미지 복구 시간 초과: " + stage);
                if (window == null) throw new InvalidOperationException("검사 창이 사라졌습니다.");
                LevelBoardView board = window.rootVisualElement.Q<LevelBoardView>();
                int expected = window.CurrentLevel.InitialBlocks.Count(b => b.Kind == InitialBlockKind.FixedNormal);
                int shown = board?.Query<VisualElement>("board-content-art").ToList().Count(v => v.style.backgroundImage.value.sprite != null) ?? 0;
                if (stage == 1 && expected > 0 && shown == expected)
                {
                    File.AppendAllText(Output, "PASS 편집 모드 최초 이미지 표시\n");
                    SessionState.SetInt(Key, 2);
                    EditorApplication.EnterPlaymode();
                }
                else if (stage == 2 && EditorApplication.isPlaying && expected > 0 && shown == expected)
                {
                    File.AppendAllText(Output, "PASS Play Mode 진입 후 편집 이미지 표시\n");
                    SessionState.SetInt(Key, 3);
                    EditorApplication.ExitPlaymode();
                }
                else if (stage == 3 && !EditorApplication.isPlayingOrWillChangePlaymode && expected > 0 && shown == expected)
                {
                    File.AppendAllText(Output, "PASS Play Mode 종료 후 재컴파일 없이 자동 이미지 복구\n");
                    window.SetLevel(window.CurrentLevel);
                    SessionState.SetInt(Key, 4);
                }
                else if (stage == 4 && expected > 0 && shown == expected)
                {
                    if (JsonUtility.ToJson(window.CurrentLevel) != SessionState.GetString(Key + ".original", ""))
                        throw new InvalidOperationException("원본 레벨 변경");
                    File.AppendAllText(Output, "PASS 같은 레벨 재지정 후 이미지 유지\nPASS 원본 레벨 보존\n");
                    SessionState.SetInt(Key, 0);
                    window.Close();
                }
            }
            catch (Exception error)
            {
                File.AppendAllText(Output, "FAIL " + error + "\n");
                SessionState.SetInt(Key, 0);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                if (window != null) window.Close();
            }
        }
    }
}

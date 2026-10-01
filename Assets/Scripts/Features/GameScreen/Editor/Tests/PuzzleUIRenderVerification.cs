using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static class PuzzleUIRenderVerification
    {
        private const string Key = "StageFour.Render";
        static PuzzleUIRenderVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                { SessionState.SetBool(Key, false); CaptureAsync().Forget(Debug.LogException); }
            };
        }
        [MenuItem("Tools/Match/UI 화면 검증")]
        public static void Run()
        { RememberSize(); SessionState.SetBool(Key, true); PuzzleGameSceneVerification.OpenInteractive(); }

        private static async UniTask CaptureAsync()
        {
            string output = PuzzleUIStateVerification.Output;
            Directory.CreateDirectory(output);
            try
            {
                Application.runInBackground = true;
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                for (int i = 0; i < 1800 && !session.IsReady && !session.HasFailed; i++) await UniTask.Yield();
                if (!session.IsReady) throw new Exception(session.Message);
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(450, 800), new Vector2Int(450, 975), new Vector2Int(600, 800) })
                {
                    SetSize(size.x, size.y);
                    for (int i = 0; i < 20; i++) await UniTask.Yield();
                    ScreenCapture.CaptureScreenshot(output + "screen-" + size.x + "x" + size.y + ".png");
                    await UniTask.Delay(500);
                }
                SetSize(1280, 720);
                File.WriteAllText(output + "render-result.txt", "PASS captures requested, inspect actual image files");
            }
            catch (Exception error) { File.WriteAllText(output + "render-result.txt", "FAIL " + error); Debug.LogException(error); }
            finally { RestoreSize(); }
        }

        internal static void SetSize(int width, int height)
        {
            Assembly assembly = typeof(EditorWindow).Assembly;
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            object sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
            int builtIn = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
            int custom = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
            Type sizeType = assembly.GetType("UnityEditor.GameViewSize"), mode = assembly.GetType("UnityEditor.GameViewSizeType");
            int index = -1;
            for (int i = 0; i < builtIn + custom; i++)
            {
                object existing = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((int)sizeType.GetProperty("width").GetValue(existing) == width && (int)sizeType.GetProperty("height").GetValue(existing) == height)
                { index = i; break; }
            }
            if (index < 0)
            {
                object value = Activator.CreateInstance(sizeType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                    new[] { Enum.ToObject(mode, 1), (object)width, height, "Stage04 " + width + "x" + height }, null);
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { value });
                index = builtIn + custom;
            }
            Type viewType = assembly.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, index);
            view.Show(); view.Repaint();
        }

        internal static void RememberSize()
        {
            Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(type);
            SessionState.SetInt("StageFour.PreviousSize", (int)type.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view));
        }

        internal static void RestoreSize()
        {
            Assembly assembly = typeof(EditorWindow).Assembly;
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            object sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { sizesType.GetProperty("currentGroupType").GetValue(sizes) });
            int builtIn = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
            int custom = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
            int previous = SessionState.GetInt("StageFour.PreviousSize", 0);
            for (int i = custom - 1; i >= 0; i--)
            {
                object value = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { builtIn + i });
                string label = (string)value.GetType().GetProperty("baseText").GetValue(value);
                if (!label.StartsWith("Stage04 ", StringComparison.Ordinal)) continue;
                if (previous == builtIn + i) previous = 0; else if (previous > builtIn + i) previous--;
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { i });
            }
            Type type = assembly.GetType("UnityEditor.GameView");
            type.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(EditorWindow.GetWindow(type), previous);
            SessionState.EraseInt("StageFour.PreviousSize");
        }
    }
}

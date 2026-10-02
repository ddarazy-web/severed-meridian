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
                if (SessionState.GetString("StageFour.SizeSnapshot", "") != "")
                    SessionState.SetString("StageFour.CreatedSizes", SessionState.GetString("StageFour.CreatedSizes", "") + index + "," + width + "," + height + ";");
            }
            Type viewType = assembly.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, index);
            view.Show(); view.Repaint();
        }

        internal static void RememberSize()
        {
            Assembly assembly = typeof(EditorWindow).Assembly;
            Type type = assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(type);
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            object sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
            int count = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null) + (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
            string[] entries = new string[count];
            for (int index = 0; index < count; index++)
            {
                object value = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { index });
                entries[index] = value.GetType().GetProperty("width").GetValue(value) + ":" + value.GetType().GetProperty("height").GetValue(value)
                    + ":" + value.GetType().GetProperty("sizeType").GetValue(value) + ":" + value.GetType().GetProperty("baseText").GetValue(value);
            }
            SessionState.SetString("StageFour.SizeSnapshot", JsonUtility.ToJson(new SizeSnapshot
            {
                Selected = (int)type.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view),
                Group = Convert.ToInt32(groupType), Entries = entries
            }));
            SessionState.SetString("StageFour.CreatedSizes", "");
        }

        [Serializable] private sealed class SizeSnapshot { public int Selected; public int Group; public string[] Entries; }

        internal static void RestoreSize()
        {
            string saved = SessionState.GetString("StageFour.SizeSnapshot", "");
            if (saved == "") return;
            SizeSnapshot snapshot = JsonUtility.FromJson<SizeSnapshot>(saved);
            Assembly assembly = typeof(EditorWindow).Assembly;
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            object sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
            if (Convert.ToInt32(groupType) != snapshot.Group) throw new InvalidOperationException("Game View 크기 그룹이 검사 중 변경되었습니다.");
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
            int builtIn = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
            // 기본 항목을 포함한 인덱스로 이번 실행에서 추가한 크기만 역순 제거한다.
            foreach (string entry in SessionState.GetString("StageFour.CreatedSizes", "").Split(';').Where(entry => entry != "").Reverse())
            {
                int[] fields = entry.Split(',').Select(int.Parse).ToArray();
                int count = builtIn + (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
                if (fields[0] < builtIn || fields[0] >= count) throw new InvalidOperationException("검사 소유 Game View 크기 인덱스가 변경되었습니다.");
                object value = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { fields[0] });
                string label = (string)value.GetType().GetProperty("baseText").GetValue(value);
                if (label != "Stage04 " + fields[1] + "x" + fields[2] || (int)value.GetType().GetProperty("width").GetValue(value) != fields[1]
                    || (int)value.GetType().GetProperty("height").GetValue(value) != fields[2])
                    throw new InvalidOperationException("검사 소유 Game View 크기가 다른 항목으로 바뀌었습니다.");
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { fields[0] });
            }
            Type type = assembly.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(type);
            PropertyInfo selected = type.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            selected.SetValue(view, snapshot.Selected);
            int remaining = builtIn + (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
            string[] restored = new string[remaining];
            for (int index = 0; index < remaining; index++)
            {
                object value = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { index });
                restored[index] = value.GetType().GetProperty("width").GetValue(value) + ":" + value.GetType().GetProperty("height").GetValue(value)
                    + ":" + value.GetType().GetProperty("sizeType").GetValue(value) + ":" + value.GetType().GetProperty("baseText").GetValue(value);
            }
            if ((int)selected.GetValue(view) != snapshot.Selected || !snapshot.Entries.SequenceEqual(restored))
                throw new InvalidOperationException("Game View 선택/크기 목록 복원이 일치하지 않습니다.");
            SessionState.EraseString("StageFour.SizeSnapshot"); SessionState.EraseString("StageFour.CreatedSizes");
        }
    }
}

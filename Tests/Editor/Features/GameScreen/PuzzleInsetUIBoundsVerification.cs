using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static class PuzzleInsetUIBoundsVerification
    {
        private const string Key = "Stage12.InsetUI";
        private const string Output = "Logs/Stage12/";
        private static readonly List<string> results = new List<string>();
        static PuzzleInsetUIBoundsVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                { SessionState.SetBool(Key, false); VerifyAsync().Forget(Debug.LogException); }
            };
        }
        public static void Run()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            Directory.CreateDirectory(Output + "inset-ui"); PuzzleUIRenderVerification.RememberSize();
            SessionState.SetBool(Key, true); EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath); EditorApplication.EnterPlaymode();
        }
        private static async UniTask VerifyAsync()
        {
            results.Clear(); int exit = 0; float previous = Time.timeScale;
            PuzzleScreenLayout layout = null; bool enabled = true;
            List<string> bounds = new List<string> { "width,height,path,xMin,yMin,xMax,yMax,boardOverlapWidth,boardOverlapHeight" };
            try
            {
                Application.runInBackground = true;
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                float deadline = Time.realtimeSinceStartup + 30;
                while (!session.IsReady && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.NextFrame();
                Check(session.IsReady && !session.HasFailed, "실제 세션 준비"); Time.timeScale = 0;
                Check(session.SetPaused(true), "실제 pause 팝업 표시");
                PuzzleScreenView screen = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenView>();
                Transform root = screen.transform.Find("SafeArea"); layout = screen.GetComponent<PuzzleScreenLayout>();
                enabled = layout.enabled; layout.enabled = false;
                MethodInfo measure = typeof(PuzzleUIInteractionVerification).GetMethod("Bounds", BindingFlags.NonPublic | BindingFlags.Static);
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(450, 800), new Vector2Int(450, 975), new Vector2Int(600, 800) })
                {
                    PuzzleUIRenderVerification.SetSize(size.x, size.y);
                    for (int frame = 0; frame < 12; frame++) await UniTask.NextFrame();
                    Check(Screen.width == size.x && Screen.height == size.y, "실제 inset UI 해상도 " + size);
                    Rect safe = new Rect(24, 36, size.x - 48, size.y - 72);
                    layout.ApplyLayout(safe, size); Canvas.ForceUpdateCanvases();
                    foreach (string path in new[] { "PuzzleHUD", "PuzzleItemBar", "Pause", "ItemPrompt", "BoardArea", "PuzzlePausePopup/Panel", "PuzzleResultPopup/Panel" })
                    {
                        Rect rect = (Rect)measure.Invoke(null, new object[] { root.Find(path) as RectTransform });
                        Check(rect.xMin >= safe.xMin - 1 && rect.yMin >= safe.yMin - 1 && rect.xMax <= safe.xMax + 1 && rect.yMax <= safe.yMax + 1,
                            "정확한 inset UI·팝업 경계 " + size + "/" + path);
                        Rect boardRect = session.BoardCamera.pixelRect;
                        float overlapWidth = Mathf.Min(rect.xMax, boardRect.xMax) - Mathf.Max(rect.xMin, boardRect.xMin);
                        float overlapHeight = Mathf.Min(rect.yMax, boardRect.yMax) - Mathf.Max(rect.yMin, boardRect.yMin);
                        bounds.Add(string.Join(",", size.x, size.y, path, rect.xMin, rect.yMin, rect.xMax, rect.yMax, overlapWidth, overlapHeight));
                        // 안내 컨테이너의 빈 여백과 팝업의 의도된 보드 덮임은 표시 겹침으로 세지 않는다.
                        if (!path.Contains("Popup") && path != "ItemPrompt")
                            Check(path == "BoardArea" || overlapWidth <= .1f || overlapHeight <= .1f, "보드 비중첩 " + size + "/" + path);
                    }
                    Text status = root.Find("ItemPrompt/Status").GetComponent<Text>();
                    // CanvasRenderer가 소유한 현재 mesh를 읽기만 하며 반환/파괴하지 않는다.
                    Mesh mesh = status.canvasRenderer.GetMesh();
                    {
                        Check(mesh != null && mesh.vertexCount > 0 && !status.raycastTarget, "실제 안내 글자 mesh·보드 입력 비차단 " + size);
                        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
                        foreach (Vector3 vertex in mesh.vertices)
                        { Vector2 pointOnScreen = status.rectTransform.TransformPoint(vertex); min = Vector2.Min(min, pointOnScreen); max = Vector2.Max(max, pointOnScreen); }
                        Rect boardRect = session.BoardCamera.pixelRect;
                        float overlapWidth = Mathf.Min(max.x, boardRect.xMax) - Mathf.Max(min.x, boardRect.xMin);
                        float overlapHeight = Mathf.Min(max.y, boardRect.yMax) - Mathf.Max(min.y, boardRect.yMin);
                        bounds.Add(string.Join(",", size.x, size.y, "ItemPrompt/StatusMesh", min.x, min.y, max.x, max.y, overlapWidth, overlapHeight));
                        Check(overlapWidth <= .1f || overlapHeight <= .1f, "실제 안내 글자 보드 비중첩 " + size);
                    }
                    BoardCoordinate at = session.State.Cells.First(cell => cell.IsActive).Coordinate;
                    PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
                    Vector2 point = session.BoardCamera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(at)));
                    Check(UnityEngine.Object.FindFirstObjectByType<PuzzleBoardInput>().TryGetCoordinate(point, out BoardCoordinate actual) && actual.Equals(at), "inset 실제 보드 좌표 왕복 " + size);
                    string pathName = Output + "inset-ui/" + size.x + "x" + size.y + "-pause.png";
                    if (File.Exists(pathName)) File.Delete(pathName);
                    float settle = Time.realtimeSinceStartup + .2f;
                    while (Time.realtimeSinceStartup < settle) await UniTask.NextFrame();
                    ScreenCapture.CaptureScreenshot(pathName); deadline = Time.realtimeSinceStartup + 10;
                    while ((!File.Exists(pathName) || new FileInfo(pathName).Length < 1000) && Time.realtimeSinceStartup < deadline) await UniTask.NextFrame();
                    Texture2D image = new Texture2D(2, 2);
                    try { Check(image.LoadImage(File.ReadAllBytes(pathName)) && image.width == size.x && image.height == size.y, "inset pause 실제 PNG " + size); }
                    finally { UnityEngine.Object.DestroyImmediate(image); }
                }
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally
            {
                if (layout != null) layout.enabled = enabled;
                Time.timeScale = previous;
                try { PuzzleUIRenderVerification.RestoreSize(); Check(true, "Game View cleanup 복원 성공"); }
                catch (Exception error) { results.Add("FAIL cleanup " + error); exit = 1; }
                File.WriteAllLines(Output + "inset-ui-bounds.csv", bounds);
                File.WriteAllLines(Output + "inset-ui-results.txt", results); EditorApplication.Exit(exit);
            }
        }
        private static void Check(bool value, string name)
        { if (!value) throw new InvalidOperationException(name); results.Add("PASS " + name); }
    }
}

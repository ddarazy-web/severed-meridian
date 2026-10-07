using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameScreen.Editor
{
    public static partial class PuzzleLevelTransitionVerification
    {
        private static async UniTask CapturePopups(string state, bool inset = false)
        {
            PuzzleScreenLayout layout = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenLayout>();
            bool wasEnabled = layout.enabled;
            if (inset) layout.enabled = false;
            try
            {
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(450, 800), new Vector2Int(450, 975), new Vector2Int(600, 800) })
                {
                    PuzzleUIRenderVerification.SetSize(size.x, size.y);
                    for (int frame = 0; frame < 20; frame++) await UniTask.Yield();
                    Check(Screen.width == size.x && Screen.height == size.y, "실제 Game View 크기 " + state + size);
                    Rect safe = inset ? new Rect(24, 36, size.x - 48, size.y - 72) : Screen.safeArea;
                    if (inset) layout.ApplyLayout(safe, size);
                    Canvas.ForceUpdateCanvases();
                    PuzzleResultView popup = UnityEngine.Object.FindFirstObjectByType<PuzzleResultView>();
                    RectTransform panel = popup.transform.Find("Panel") as RectTransform;
                    Vector3[] corners = new Vector3[4]; panel.GetWorldCorners(corners);
                    Check(corners[0].x >= safe.xMin - 1 && corners[0].y >= safe.yMin - 1 && corners[2].x <= safe.xMax + 1 && corners[2].y <= safe.yMax + 1,
                        "실제 결과 팝업 안전 영역 경계 " + state + size);
                    foreach (UnityEngine.UI.Button button in popup.GetComponentsInChildren<UnityEngine.UI.Button>())
                    {
                        Vector3[] buttonCorners = new Vector3[4]; (button.transform as RectTransform).GetWorldCorners(buttonCorners);
                        Check(buttonCorners[0].x >= corners[0].x && buttonCorners[0].y >= corners[0].y && buttonCorners[2].x <= corners[2].x && buttonCorners[2].y <= corners[2].y,
                            "실제 버튼 팝업 내부 " + button.name + state + size);
                        List<RaycastResult> hits = new List<RaycastResult>();
                        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = (buttonCorners[0] + buttonCorners[2]) * .5f }, hits);
                        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button, "실제 버튼 최상위 raycast " + button.name + state + size);
                    }
                    PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                    PuzzleBoardInput input = session.GetComponent<PuzzleBoardInput>();
                    object before = session.State;
                    Vector2 boardPoint = session.BoardCamera.pixelRect.center;
                    typeof(PuzzleBoardInput).GetMethod("BeginPointer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { -31, boardPoint });
                    typeof(PuzzleBoardInput).GetMethod("EndPointer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { -31, boardPoint });
                    Check(ReferenceEquals(before, session.State) && !input.Selected.HasValue, "실제 결과 보드 포인터 침투0 " + state + size);
                    string path = Output + state + "-" + size.x + "x" + size.y + ".png";
                    if (File.Exists(path)) File.Delete(path);
                    ScreenCapture.CaptureScreenshot(path);
                    float deadline = Time.realtimeSinceStartup + 10;
                    while ((!File.Exists(path) || new FileInfo(path).Length < 1000) && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                    Texture2D image = new Texture2D(2, 2);
                    try
                    {
                        Check(File.Exists(path) && image.LoadImage(File.ReadAllBytes(path)) && image.width == size.x && image.height == size.y,
                            "실제 결과 PNG 저장·디코딩 " + path);
                        int colors = 0; Color32[] pixels = image.GetPixels32(); Color32 first = pixels[0];
                        for (int index = 0; index < pixels.Length; index += 29) if (!pixels[index].Equals(first)) colors++;
                        Check(colors > 100, "빈 화면 거부 " + path);
                    }
                    finally { UnityEngine.Object.Destroy(image); }
                }
            }
            finally { layout.enabled = wasEnabled; }
        }
    }
}

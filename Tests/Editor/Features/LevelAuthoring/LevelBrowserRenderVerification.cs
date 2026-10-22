using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelTool;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelBrowserRenderVerification
    {
        private static string folder, project, first;
        private static LevelToolScreen screen;
        private static PanelSettings panel;
        private static RenderTexture texture, oldTarget;
        private static bool previousEnabled;
        private static EnterPlayModeOptions previousOptions;
        private static int phase;
        private static double next, deadline;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void Run()
        {
            folder = Path.GetFullPath("Logs/LevelBrowserRender/" + Guid.NewGuid().ToString("N"));
            project = Path.Combine(folder, "data"); Directory.CreateDirectory(folder);
            var fixture = new AuthoringToolWorkspace(); fixture.Create(project, LevelToolTestProject.Create());
            first = fixture.Session.SelectedLevelId;
            ContentDocument original = fixture.Session.Get(first);
            string[] ids = Enumerable.Range(2, 999).Select(n => "render-level-" + n).ToArray();
            fixture.Session.ApplyToDocuments("fixture", ids, documents =>
            {
                for (int index = 0; index < ids.Length; index++)
                {
                    var body = original.Data; body["levelNumber"] = index + 2; body["displayName"] = "화면 검사 " + (index + 2);
                    documents.Add(ids[index], new ContentDocument("level", ids[index], body));
                }
            });
            fixture.Save();
            foreach (string path in Directory.GetFiles(Path.Combine(project, ".authoring", "listings"), "*.json")) File.Delete(path);
            previousEnabled = EditorSettings.enterPlayModeOptionsEnabled; previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            LevelToolRecoveryIsolation.Configure(Path.Combine(folder, "recovery"));
            deadline = EditorApplication.timeSinceStartup + 180; phase = 0;
            EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Render verification timeout at " + phase);
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
                screen ??= UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                if (screen == null) return;
                bool busy = (bool)Get("busy");
                if (phase == 0)
                {
                    if (busy) return;
                    panel = screen.GetComponent<UIDocument>().panelSettings; oldTarget = panel.targetTexture;
                    texture = new RenderTexture(1280, 800, 24); texture.Create(); panel.targetTexture = texture;
                    screen.Workspace.OpenListing(project, "Discard"); Call("Refresh"); Advance();
                }
                else if (phase == 1)
                {
                    Capture("01-list.png");
                    Check(screen.Workspace.Session == null && ((ListView)Get("levelBrowser")).itemsSource.Count == 1000, "listing before editing");
                    Call("OpenSelectedLevel", first, null); Advance();
                }
                else if (phase == 2)
                {
                    Capture("02-editor.png");
                    Check(((VisualElement)Get("board")).worldBound.width >= 300, "board visible after edit");
                    screen.Workspace.Session.EditLevel("draft", body => body["displayName"] = "보존할 초안");
                    Call("Refresh"); Call("RequestListingRebuild"); Advance();
                }
                else if (phase == 3)
                {
                    Capture("03-confirm.png"); Submit("confirm-listing-rebuild"); Advance(0.15);
                }
                else if (phase == 4)
                {
                    Check(busy, "real listing scan remains in progress before cancel");
                    Capture("04-progress.png"); Submit("cancel-listing-rebuild"); Advance(0.2);
                }
                else if (phase == 5)
                {
                    if (busy) return;
                    Capture("05-cancelled.png");
                    Check(((Label)Get("status")).text.Contains("취소"), "actual cancel reported");
                    Check(screen.Workspace.Session.IsDirty && screen.Workspace.Directory.MetadataWarning != null, "cancel preserves draft and previous index");
                    screen.Workspace.Session.Undo(); Check(!screen.Workspace.Session.IsDirty, "cancel preserves Undo");
                    Finish(0);
                }
            }
            catch (Exception error) { File.AppendAllText(Path.Combine(folder, "results.txt"), error + "\n"); Debug.LogException(error); Finish(1); }
        }
        private static void Advance(double seconds = 0.7) { phase++; next = EditorApplication.timeSinceStartup + seconds; }
        private static object Get(string name) => typeof(LevelToolScreen).GetField(name, Flags).GetValue(screen);
        private static void Call(string name, params object[] args) => typeof(LevelToolScreen).GetMethod(name, Flags).Invoke(screen, args);
        private static void Submit(string name)
        {
            Button button = ((VisualElement)Get("root")).Q<Button>(name);
            Check(button != null && button.enabledInHierarchy, "button available " + name);
            using var evt = NavigationSubmitEvent.GetPooled(); button.SendEvent(evt);
        }
        private static void Capture(string name)
        {
            RenderTexture prior = RenderTexture.active;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try { RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(folder, name), image.EncodeToPNG()); }
            finally { RenderTexture.active = prior; UnityEngine.Object.DestroyImmediate(image); }
        }
        private static void Check(bool valid, string message)
        {
            File.AppendAllText(Path.Combine(folder, "results.txt"), (valid ? "PASS " : "FAIL ") + message + "\n");
            if (!valid) throw new Exception(message);
        }
        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            if (panel != null) panel.targetTexture = oldTarget;
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            EditorSettings.enterPlayModeOptionsEnabled = previousEnabled; EditorSettings.enterPlayModeOptions = previousOptions;
            foreach (string child in new[] { "data", "recovery" })
            {
                string target = Path.GetFullPath(Path.Combine(folder, child));
                if (!target.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("검사 경로 오류");
                if (Directory.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) == 0) Directory.Delete(target, true);
            }
            EditorApplication.Exit(code);
        }
    }
}

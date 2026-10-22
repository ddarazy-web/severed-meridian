using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Storage;
using LevelTool;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolNativeStartExercise
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static async UniTask Run(LevelToolScreen screen, RenderTexture texture, PanelSettings panel, string isolation)
        {
            VisualElement root = screen.GetComponent<UIDocument>().rootVisualElement;
            string output = Path.GetFullPath("Logs/NativeStartChecks"); Directory.CreateDirectory(output);
            string folder = Path.Combine(isolation, "NewProject");
            void Check(bool condition, string message) { if (!condition) throw new Exception(message); Debug.Log("PASS native-start " + message); }
            void Invoke(string method) => typeof(LevelToolScreen).GetMethod(method, Flags).Invoke(screen, null);
            void Key(KeyCode key)
            {
                VisualElement target = root.focusController.focusedElement as VisualElement ?? root;
                if (target is TextField || target is IntegerField)
                    target = target.Q<TextElement>("unity-text-input") ?? target.Query<TextElement>().ToList().Last();
                var source = new Event { type = EventType.KeyDown, keyCode = key, character = key == KeyCode.Tab ? '\t' : '\0' };
                using (var down = KeyDownEvent.GetPooled(source)) { down.target = target; target.SendEvent(down); }
                source.type = EventType.KeyUp;
                using (var up = KeyUpEvent.GetPooled(source)) { up.target = target; target.SendEvent(up); }
            }
            void Click(string name)
            {
                Button button = root.Q<Button>(name);
                Check(button != null && button.enabledInHierarchy, "enabled " + name);
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
            }
            async UniTask Idle()
            {
                await UniTask.WaitUntil(() => !(bool)typeof(LevelToolScreen).GetField("busy", Flags).GetValue(screen));
                await UniTask.DelayFrame(4);
            }
            await UniTask.DelayFrame(6);
            Invoke("Refresh"); await UniTask.DelayFrame(4);
            Check(root.Q("project-start").resolvedStyle.display == DisplayStyle.Flex, "start screen visible");
            Capture(texture, Path.Combine(output, "start.png"));
            Click("start-recent-projects");
            Check(root.Q("modal").Query<Label>().ToList().Any(label => label.text.Contains("최근에 연 프로젝트가 없습니다")), "empty recent guidance");
            Click("cancel");
            Click("start-new-project");
            await UniTask.DelayFrame(3);
            VisualElement initialFocus = root.focusController.focusedElement as VisualElement;
            Key(KeyCode.Tab); await UniTask.DelayFrame(2);
            Debug.Log("native-start Tab focus: " + initialFocus?.name + " -> " + (root.focusController.focusedElement as VisualElement)?.name);
            Check(root.focusController.focusedElement != initialFocus, "Tab advances project dialog focus");
            Key(KeyCode.Escape); await UniTask.DelayFrame(2);
            Check(root.Q("modal") == null && screen.Workspace.Folder == null, "Escape cancels new project");
            Click("start-new-project");
            root.Q<TextField>("project-name").value = ""; Click("create-project");
            Check(root.Q<Label>("project-name-error").text.Length > 0, "empty name rejected");
            root.Q<TextField>("project-name").value = "시작 UX 검사"; Click("create-project");
            root.Q<TextField>("folder-path").value = folder; Click("choose-folder"); await Idle();
            Check(screen.Workspace.Session != null && screen.Workspace.Session.SelectedLevelId == null, "project has no implicit level");
            Check(root.Q<Label>("start-title").text == "아직 만든 레벨이 없습니다", "empty guidance");
            Check(!root.Q<Button>("play").enabledInHierarchy, "empty project cannot play");
            Capture(texture, Path.Combine(output, "empty.png"));
            // 이전 프로젝트가 시험 탭에 있었던 상태를 재현한다.
            typeof(LevelToolScreen).GetField("inspectorPage", Flags).SetValue(screen, "자동 시험");
            Click("first-level");
            Check(root.Q<IntegerField>("new-level-number").value == 1, "first number suggested");
            Check(root.Q<DropdownField>("new-level-source").choices.Count == 1, "no duplicate without source");
            root.Q<IntegerField>("new-level-number").value = 0; Click("create-level");
            Check(root.Q<Label>("new-level-error").text.Length > 0, "bad number rejected");
            root.Q<IntegerField>("new-level-number").value = 1;
            Button createFirst = root.Q<Button>("create-level"); createFirst.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = createFirst; createFirst.SendEvent(submit); }
            await Idle();
            var session = screen.Workspace.Session;
            string first = session.SelectedLevelId;
            Check(first != null && session.IsDirty, "first level opened dirty");
            Check(root.Q("board").childCount >= 81, "9x9 board visible");
            bool boardVisible = root.Q("board").resolvedStyle.display != DisplayStyle.None;
            using (var disk = new ContentSnapshotStore(folder).ReadDirectory()) Check(disk.Entries.Count == 0, "no automatic level save");
            Capture(texture, Path.Combine(output, "first-level.png"));
            Click("undo"); await Idle();
            Check(session.SelectedLevelId == null && !session.IsDirty, "undo first level shows empty state");
            Click("redo"); await Idle(); Check(session.SelectedLevelId == first, "redo restores first level");
            bool paletteRestored = root.Q("palette").Query<Button>().ToList().Count > 0;
            Debug.Log("native-start review regressions: boardVisible=" + boardVisible + ", paletteRestored=" + paletteRestored);
            Check(boardVisible && paletteRestored, "first level opens board and redo restores palette");
            Click("save"); await Idle(); Check(!session.IsDirty, "explicit save");
            Click("duplicate");
            Check(root.Q<DropdownField>("new-level-source").index == 1, "duplicate source selected");
            Click("create-level"); await Idle();
            Check(session.DocumentIdsOfKind("level").Length == 2 && session.SelectedLevelId != first, "duplicate opened");
            string second = session.SelectedLevelId;
            Check(session.Get(first).Data["elements"].ToString() == session.Get(second).Data["elements"].ToString(), "duplicate preserves board");
            Click("save"); await Idle();
            Invoke("OpenRecentProjects");
            Check(root.Query<Button>("recent-project").ToList().Any(button => button.text == folder && button.enabledInHierarchy), "recent project remembers opened folder");
            Click("cancel");
            var recents = (RecentProjectStore)typeof(LevelToolScreen).GetField("recentProjects", Flags).GetValue(screen);
            recents.RecordOpened(Path.Combine(isolation, "MissingProject"));
            Invoke("OpenRecentProjects");
            Check(root.Query<Button>("recent-project").ToList().Any(button => button.text.Contains("저장본 없음") && !button.enabledInHierarchy) &&
                root.Q<Button>("recent-open-other").enabledInHierarchy, "missing recent folder offers reopen path");
            Click("cancel");
            Click("project-start-menu"); Check(root.Q("menu-open-project") != null, "project start accessible during editing"); Click("cancel");
            foreach (Vector2Int size in new[] { new Vector2Int(1280,800), new Vector2Int(1440,900), new Vector2Int(390,844), new Vector2Int(844,390), new Vector2Int(820,1180) })
            {
                panel.targetTexture = null; texture.Release(); texture.width = size.x; texture.height = size.y; texture.Create(); panel.targetTexture = texture;
                await UniTask.DelayFrame(6);
                if (size.x < 1000)
                    typeof(LevelToolScreen).GetMethod("ApplyMobileViewport", Flags).Invoke(screen,
                        new object[] { (Vector2)size, new Rect(0, 0, size.x, size.y), new Rect() });
                Click("new-level"); await UniTask.DelayFrame(4);
                Button create = root.Q<Button>("create-level");
                Check(create.worldBound.width > 0 && create.worldBound.height > 0 &&
                    root.worldBound.Contains(create.worldBound.min) && root.worldBound.Contains(create.worldBound.max), "dialog visible " + size);
                Capture(texture, Path.Combine(output, "new-level-" + size.x + "x" + size.y + ".png"));
                if (size.x < 1000)
                {
                    typeof(LevelToolScreen).GetMethod("ApplyMobileViewport", Flags).Invoke(screen,
                        new object[] { (Vector2)size, new Rect(0, 0, size.x, size.y), new Rect(0, 0, size.x, size.y * .45f) });
                    await UniTask.DelayFrame(4);
                    root.Q<ScrollView>("mobile-dialog-scroll").ScrollTo(root.Q<Button>("cancel"));
                    await UniTask.DelayFrame(4);
                    Rect available = (Rect)typeof(LevelToolScreen).GetField("mobileVisibleArea", Flags).GetValue(screen);
                    Check(available.Contains(root.Q<Button>("cancel").worldBound.center), "keyboard leaves cancel accessible " + size);
                    Capture(texture, Path.Combine(output, "keyboard-" + size.x + "x" + size.y + ".png"));
                }
                Click("cancel");
            }
            Check(new ContentSnapshotStore(folder).Read().Snapshot.DocumentIds.Count() == 3, "two levels persisted");
            screen.Workspace.Create(Path.Combine(isolation, "EmptyAgain"), LevelToolDocuments.NewProject("빈 화면 검사"), "Discard");
            Invoke("Refresh"); await UniTask.DelayFrame(3);
            Check(root.Q("mobile-navigation").resolvedStyle.display == DisplayStyle.None, "empty project hides editing navigation");
            foreach (Vector2Int size in new[] { new Vector2Int(390,844), new Vector2Int(844,390), new Vector2Int(820,1180) })
            {
                panel.targetTexture = null; texture.Release(); texture.width = size.x; texture.height = size.y; texture.Create(); panel.targetTexture = texture;
                await UniTask.DelayFrame(4);
                typeof(LevelToolScreen).GetMethod("ApplyMobileViewport", Flags).Invoke(screen,
                    new object[] { (Vector2)size, new Rect(0, 0, size.x, size.y), new Rect() });
                Invoke("Refresh"); await UniTask.DelayFrame(3);
                Capture(texture, Path.Combine(output, "empty-" + size.x + "x" + size.y + ".png"));
                Click("first-level"); await UniTask.DelayFrame(3);
                Invoke("HandleAndroidBack"); await UniTask.DelayFrame(2);
                Check(root.Q("modal") == null && screen.Workspace.Session.SelectedLevelId == null, "Android back cancels first level " + size);
            }
        }

        private static void Capture(RenderTexture texture, string path)
        {
            RenderTexture previous = RenderTexture.active; RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try { image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(image); }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using LevelTool;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelBrowserUiVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool previousEnabled;
        private static EnterPlayModeOptions previousOptions;
        private static double deadline;
        private static string folder, aId, savedFrom;
        private static LevelToolScreen screen;
        private static int phase;
        private static bool layoutMode;
        private static bool mobileLayout;
        private static int sizeIndex;
        private static double next;
        private static PanelSettings testPanel;
        private static RenderTexture target;
        private static IntegerField invalidInput;
        private static Vector2 expectedScroll;
        private static double scrollStarted;
        private static int scrollRun;
        private static readonly double[] scrollSamples = new double[3];
        private static Vector2Int[] Sizes = { new Vector2Int(1280, 800), new Vector2Int(1920, 1080) };
        public static void RunLayout() { layoutMode = true; Run(); }
        public static void RunMobileLayout()
        {
            mobileLayout = layoutMode = true;
            Sizes = new[] { new Vector2Int(360, 780), new Vector2Int(800, 1280) }; Run();
        }

        public static void Run()
        {
            folder = Path.GetFullPath("Logs/LevelBrowser/ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            previousEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            LevelToolRecoveryIsolation.Configure(Path.Combine(folder, "recovery"));
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("UI timeout");
                if (EditorApplication.timeSinceStartup < next) return;
                if (!EditorApplication.isPlaying) return;
                screen ??= UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                if (screen == null || (bool)typeof(LevelToolScreen).GetField("busy", Flags).GetValue(screen)) return;
                VisualElement root = screen.GetComponent<UIDocument>().rootVisualElement;
                if (phase == 0)
                {
                    LevelToolRecoveryIsolation.Verify(screen, Path.Combine(folder, "recovery"));
                    ContentSnapshot original = LevelToolTestProject.Create();
                    ContentDocument a = original.Documents.Single(doc => doc.Kind == "level");
                    aId = a.Id;
                    var b = a.Data; b["levelNumber"] = 2; b["displayName"] = "두 번째";
                    ContentSnapshot snapshot = new ContentSnapshot(original.Documents.Concat(new[] { new ContentDocument("level", "level-b", b) }));
                    var stored = (StoredContentSnapshot)Activator.CreateInstance(typeof(StoredContentSnapshot),
                        BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { snapshot, "test", "members" }, null);
                    typeof(AuthoringToolWorkspace).GetProperty("Session").SetValue(screen.Workspace, new AuthoringEditSession(stored, aId));
                    typeof(AuthoringToolWorkspace).GetProperty("Folder").SetValue(screen.Workspace, Path.Combine(folder, "workspace"));
                    typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
                    phase = 1; return;
                }
                ListView list = root.Q<ListView>("levels");
                Check(list != null, "virtual level ListView exists");
                if (layoutMode) { CheckLayout(root, list); return; }
                if (list.Query<Button>(className: "level-edit-button").ToList().Count == 0) return;
                if (phase == 2)
                {
                    Check(screen.Workspace.Session.SelectedLevelId == "level-b", "failed save keeps current editor");
                    Check(screen.Workspace.Session.IsDirty, "failed save retains data");
                    Check(typeof(LevelToolScreen).GetField("manualSaveFailure", Flags).GetValue(screen) != null, "save failure reported");
                    TextField search = root.Q<TextField>("level-search");
                    search.value = "999";
                    using (KeyDownEvent key = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return }))
                    { key.target = search; search.SendEvent(key); }
                    Check(list.itemsSource.Count == 0 && screen.Workspace.Session.SelectedLevelId == "level-b", "empty search preserves editor");
                    search.value = "1";
                    using (KeyDownEvent key = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return }))
                    { key.target = search; search.SendEvent(key); }
                    Check(list.itemsSource.Count == 1 && screen.Workspace.Session.SelectedLevelId == "level-b", "Enter searches but never opens");
                    typeof(Clickable).GetMethod("Invoke", Flags).Invoke(root.Q<Button>("find-current-level").clickable, new object[] { null });
                    Check(search.value == "" && list.itemsSource.Count == 2 && screen.Workspace.Session.SelectedLevelId == "level-b", "find current clears search without opening");
                    invalidInput = new IntegerField("검사 숫자") { name = "browser-pending-input", value = 4, isDelayed = true };
                    root.Q("editor").Add(invalidInput); invalidInput.Focus();
                    phase = 5; next = EditorApplication.timeSinceStartup + .3;
                    return;
                }
                if (phase == 5)
                {
                    TextElement text = invalidInput.Q(className: "unity-base-field__input").Q<TextElement>();
                    ((INotifyValueChanged<string>)text).SetValueWithoutNotify("잘못된 숫자");
                    DropdownField pendingRecent = root.Q<DropdownField>("recent-levels"); pendingRecent.value = pendingRecent.choices[2];
                    Button pendingCancel = root.Q<Button>("cancel-open-level");
                    Check(pendingCancel != null, "pending input opens confirmation");
                    typeof(Clickable).GetMethod("Invoke", Flags).Invoke(pendingCancel.clickable, new object[] { null });
                    phase = 6; next = EditorApplication.timeSinceStartup + .3; return;
                }
                if (phase == 6)
                {
                    Check(invalidInput.text == "잘못된 숫자" && screen.Workspace.Session.SelectedLevelId == "level-b", "cancel preserves raw pending text");
                    DropdownField pendingRecent = root.Q<DropdownField>("recent-levels"); pendingRecent.value = pendingRecent.choices[2];
                    typeof(Clickable).GetMethod("Invoke", Flags).Invoke(root.Q<Button>("save-and-open-level").clickable, new object[] { null });
                    phase = 7; next = EditorApplication.timeSinceStartup + .3; return;
                }
                if (phase == 7)
                {
                    Check(invalidInput.text == "잘못된 숫자" && screen.Workspace.Session.SelectedLevelId == "level-b", "invalid input prevents save/open without loss");
                    invalidInput.RemoveFromHierarchy();
                    typeof(LevelToolScreen).GetMethod("CancelPendingInput", Flags).Invoke(screen, null);
                    TextField search = root.Q<TextField>("level-search");
                    search.SetValueWithoutNotify("2");
                    typeof(LevelToolScreen).GetMethod("ApplyLevelSearch", Flags).Invoke(screen, null);
                    var rowsBefore = list.itemsSource;
                    var index = (LevelBrowserIndex)typeof(LevelToolScreen).GetField("levelIndex", Flags).GetValue(screen);
                    long revision = index.Revision; int sorts = index.SortBuildCount;
                    for (int i = 0; i < 10; i++)
                    {
                        screen.Workspace.Session.SelectCells(new[] { i });
                        typeof(LevelToolScreen).GetField("cellSize", Flags).SetValue(screen, 40f + i);
                        typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
                    }
                    Check(ReferenceEquals(rowsBefore, list.itemsSource) && index.Revision == revision && index.SortBuildCount == sorts,
                        "board selection and zoom cause zero index/list reconstruction");
                    Check(search.value == "2", "same project preserves search");
                    screen.Workspace.Session.EditLevel("최근 이름 변경", data => data["displayName"] = "최근 갱신 확인");
                    typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
                    Check(root.Q<DropdownField>("recent-levels").choices.Any(label => label.Contains("최근 갱신 확인")), "recent labels use current name");
                    screen.Workspace.Session.Apply("다른 레벨 삭제", docs => docs.Remove(aId));
                    typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
                    Check(root.Q<DropdownField>("recent-levels").choices.Count == 2, "deleted recent ID is filtered");
                    screen.Workspace.Session.Undo();
                    typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
                    typeof(AuthoringToolWorkspace).GetProperty("Folder").SetValue(screen.Workspace, Path.Combine(folder, "other-workspace"));
                    typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
                    Check(search.value == "" && list.itemsSource.Count == 2, "different project resets search");
                    screen.Workspace.Session.Discard();
                    typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
                    for (int run = 0; run < 4; run++)
                    {
                        double[] samples = new double[10];
                        for (int i = 0; i < samples.Length; i++)
                        {
                            string targetId = screen.Workspace.Session.SelectedLevelId == aId ? "level-b" : aId;
                            var timer = System.Diagnostics.Stopwatch.StartNew();
                            typeof(LevelToolScreen).GetMethod("RequestOpenLevel", Flags).Invoke(screen, new object[] { targetId, null });
                            samples[i] = timer.Elapsed.TotalMilliseconds;
                            Check(screen.Workspace.Session.SelectedLevelId == targetId, "measured open selects requested level");
                        }
                        Array.Sort(samples);
                        if (run > 0) File.AppendAllText(Path.Combine(folder, "open-performance.txt"),
                            $"run={run} median_ms={samples[5]:F3} p95_max_ms={samples[9]:F3}; two full documents; includes synchronous refresh/recent write, excludes next-frame render\n");
                    }
                    screen.Workspace.SaveCopy(Path.Combine(folder, "saved-workspace"));
                    savedFrom = screen.Workspace.Session.SelectedLevelId;
                    screen.Workspace.Session.EditLevel("저장 전환", data => data["displayName"] = "저장 후 전환 확인");
                    typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
                    typeof(LevelToolScreen).GetMethod("RequestOpenLevel", Flags).Invoke(screen, new object[] { savedFrom == aId ? "level-b" : aId, null });
                    phase = 9;
                    using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
                    { Button successSave = root.Q<Button>("save-and-open-level"); submit.target = successSave; successSave.SendEvent(submit); }
                    next = EditorApplication.timeSinceStartup + .3;
                    return;
                }
                if (phase == 9)
                {
                    Check(screen.Workspace.Session.SelectedLevelId != savedFrom && !screen.Workspace.Session.IsDirty, "successful save opens requested level cleanly");
                    Check((string)new ContentSnapshotStore(Path.Combine(folder, "saved-workspace")).Read().Snapshot.Get(savedFrom).Data["displayName"] == "저장 후 전환 확인", "save reached disk before opening");
                    Finish("PASS virtual list, browse-only selection, confirmation/cancel/discard, other data preserved, recent guard, failed and successful save, Enter search, find current, pending raw input, project search state, board selection/zoom zero rebuild", 0);
                    return;
                }
                list.SetSelection(1);
                Check(screen.Workspace.Session.SelectedLevelId == aId, "row selection must not open editor");
                screen.Workspace.Session.EditLevel("dirty", data => data["displayName"] = "미저장");
                screen.Workspace.Session.ApplyToDocuments("other dirty", new[] { "level-b" }, docs => docs["level-b"].Data["displayName"] = "다른 미저장");
                Button edit = list.Query<Button>(className: "level-edit-button").ToList().Single(button => (string)button.userData == "level-b");
                using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled()) { submit.target = edit; edit.SendEvent(submit); }
                Check(root.Q<Button>("save-and-open-level") != null, "save choice");
                Check(root.Q<Button>("discard-and-open-level") != null, "discard choice");
                Button cancel = root.Q<Button>("cancel-open-level");
                Check(cancel != null, "continue choice");
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(cancel.clickable, new object[] { null });
                Check(screen.Workspace.Session.SelectedLevelId == aId && screen.Workspace.Session.IsDirty, "cancel preserves editor");
                using (PointerDownEvent down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = edit.worldBound.center }))
                { down.target = edit; edit.SendEvent(down); }
                using (PointerUpEvent up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = edit.worldBound.center }))
                { up.target = edit; edit.SendEvent(up); }
                Button discard = root.Q<Button>("discard-and-open-level");
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(discard.clickable, new object[] { null });
                Check(screen.Workspace.Session.SelectedLevelId == "level-b", "discard opens requested level");
                Check((string)screen.Workspace.Session.Get("level-b").Data["displayName"] == "다른 미저장", "other level data retained");
                Check(!screen.Workspace.Session.HasLevelChanges(aId), "only previous level discarded");
                DropdownField recent = root.Q<DropdownField>("recent-levels");
                Check(recent.choices.Count == 3, "recent two opened levels");
                recent.value = recent.choices[2];
                Button save = root.Q<Button>("save-and-open-level");
                Check(save != null, "recent selection uses same unsaved guard");
                phase = 2;
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(save.clickable, new object[] { null });
            }
            catch (Exception error) { Finish("FAIL " + error, 1); }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void CheckLayout(VisualElement root, ListView list)
        {
            if (phase == 1)
            {
                scrollRun = 0;
                UIDocument document = screen.GetComponent<UIDocument>();
                if (testPanel == null) { testPanel = UnityEngine.Object.Instantiate(document.panelSettings); document.panelSettings = testPanel; }
                Vector2Int size = Sizes[sizeIndex];
                target = new RenderTexture(size.x, size.y, 24); target.Create(); testPanel.targetTexture = target;
                if (mobileLayout)
                {
                    testPanel.scaleMode = PanelScaleMode.ConstantPixelSize; testPanel.scale = 1;
                    root.schedule.Execute(() =>
                    {
                        typeof(LevelToolScreen).GetMethod("ApplyMobileViewport", Flags).Invoke(screen,
                            new object[] { new Vector2(size.x, size.y), new Rect(0, 0, size.x, size.y), new Rect() });
                        Button levels = root.Q<Button>("mobile-levels");
                        if (levels != null && (string)typeof(LevelToolScreen).GetField("mobilePanel", Flags).GetValue(screen) != "levels")
                            typeof(Clickable).GetMethod("Invoke", Flags).Invoke(levels.clickable, new object[] { null });
                    }).StartingIn(200);
                }
                var index = new LevelBrowserIndex();
                for (int i = 1; i <= 10000; i++) index.Upsert(i == 1 ? aId : i == 2 ? "level-b" : "level-" + i, i, "아주 긴 레벨 이름 · 로켓과 폭탄 조합 튜토리얼 " + i);
                // 실제 열린 문서의 메타데이터는 유지해야 Refresh의 정상 보정과 가상 목록 재생성을 혼동하지 않는다.
                foreach (string id in new[] { aId, "level-b" })
                {
                    var info = screen.Workspace.Session.GetDocumentInfo(id);
                    index.Upsert(id, info.LevelNumber, info.DisplayName);
                }
                typeof(LevelToolScreen).GetField("levelIndex", Flags).SetValue(screen, index);
                typeof(LevelToolScreen).GetMethod("ApplyLevelSearch", Flags).Invoke(screen, null);
                list.ScrollToItem(0);
                phase = 3; next = EditorApplication.timeSinceStartup + .7; return;
            }
            if (phase == 10)
            {
                if (!list.Query<Button>(className: "level-edit-button").ToList().Any(button => (string)button.userData == aId && button.worldBound.Overlaps(list.worldBound))) return;
                phase = 3; next = EditorApplication.timeSinceStartup + .1; return;
            }
            if (phase == 4 && !list.Query<Button>(className: "level-edit-button").ToList().Any(button => (string)button.userData == "level-10000" && button.worldBound.Overlaps(list.worldBound))) return;
            int rows = list.Query<VisualElement>(className: "level-browser-row").ToList().Count;
            Capture("precheck");
            File.AppendAllText(Path.Combine(folder, "geometry.txt"), "list=" + list.worldBound + " rows=" + rows + "\n");
            Check(rows > 0 && rows <= 40, "bounded realized row count: " + rows);
            Check(root.Q("board-scroll").worldBound.height > (mobileLayout ? 100 : 300), "board height preserved");
            Check(list.worldBound.height > 48 && list.worldBound.xMin >= 0 && list.worldBound.xMax <= root.worldBound.xMax + 1,
                "level list remains visible inside viewport");
            Capture("geometry");
            var visibleButtons = list.Query<Button>(className: "level-edit-button").ToList()
                .Where(button => button.worldBound.Overlaps(list.worldBound) && button.resolvedStyle.display != DisplayStyle.None).ToArray();
            File.AppendAllText(Path.Combine(folder, "geometry.txt"), string.Join("\n", visibleButtons.Select(button => button.userData + ": " + button.worldBound)) + "\n");
            Check(visibleButtons.Length > 0 && visibleButtons.All(button => button.worldBound.width >= 40 && button.worldBound.height >= 36), "edit buttons usable");
            if (phase == 3)
            {
                Capture("top");
                Button dragButton = visibleButtons.Single(button => (string)button.userData == "level-b");
                Vector2 start = dragButton.worldBound.center;
                foreach (EventType eventType in new[] { EventType.MouseDown, EventType.MouseDrag, EventType.MouseUp })
                {
                    Event source = new Event { type = eventType, button = 0, mousePosition = eventType == EventType.MouseDown ? start : start + new Vector2(0, -110) };
                    using EventBase pointer = eventType == EventType.MouseDown ? (EventBase)PointerDownEvent.GetPooled(source) :
                        eventType == EventType.MouseUp ? PointerUpEvent.GetPooled(source) : PointerMoveEvent.GetPooled(source);
                    BindingFlags properties = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    pointer.GetType().GetProperty("pointerType", properties).SetValue(pointer, UnityEngine.UIElements.PointerType.touch);
                    pointer.GetType().GetProperty("pointerId", properties).SetValue(pointer, 1);
                    pointer.GetType().GetProperty("isPrimary", properties).SetValue(pointer, true);
                    pointer.target = dragButton; dragButton.SendEvent(pointer);
                }
                Check(screen.Workspace.Session.SelectedLevelId == aId && root.Q<Button>("save-and-open-level") == null,
                    "touch drag starting on edit button does not open a level");
                scrollStarted = EditorApplication.timeSinceStartup;
                list.ScrollToItem(9999); phase = 4; next = 0; return;
            }
            Check(list.Query<Button>(className: "level-edit-button").ToList().Any(button => (string)button.userData == "level-10000"), "last recycled row bound correctly");
            if (phase == 4)
            {
                if (scrollRun > 0) scrollSamples[scrollRun - 1] = (EditorApplication.timeSinceStartup - scrollStarted) * 1000;
                expectedScroll = list.Q<ScrollView>().scrollOffset;
                var rowsBefore = list.itemsSource;
                typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
                Check(ReferenceEquals(rowsBefore, list.itemsSource), "refresh preserves virtual data source");
                phase = 8; next = EditorApplication.timeSinceStartup + .3; return;
            }
            Check(Vector2.Distance(expectedScroll, list.Q<ScrollView>().scrollOffset) < 1, "same project refresh preserves scroll after layout");
            Capture("bottom");
            scrollRun++;
            if (scrollRun < 4) { list.ScrollToItem(0); phase = 10; next = 0; return; }
            File.AppendAllText(Path.Combine(folder, "scroll-performance.txt"), $"{Sizes[sizeIndex]} next-layout-ready-ms: {string.Join(", ", scrollSamples.Select(value => value.ToString("F3")))}; warmup excluded; Editor update cadence included\n");
            File.AppendAllText(Path.Combine(folder, "layout.txt"), $"{Sizes[sizeIndex]} rows={rows} board_height={root.Q("board-scroll").worldBound.height}\n");
            testPanel.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); target = null;
            sizeIndex++;
            if (sizeIndex == Sizes.Length) { Finish("PASS 10k virtual rows, desktop sizes, long labels, bottom recycling", 0); return; }
            phase = 1;
        }
        private static void Capture(string suffix)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(folder, Sizes[sizeIndex].x + "-" + suffix + ".png"), image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image); RenderTexture.active = previous;
        }
        private static void Finish(string result, int code)
        {
            EditorApplication.update -= Tick;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnabled;
            EditorSettings.enterPlayModeOptions = previousOptions;
            File.WriteAllText(Path.Combine(folder, "result.txt"), result);
            // 사용자 복구 폴더와 분리한 이번 검사 전용 사본만 삭제한다. 화면·결과는 유지한다.
            string testRoot = Path.GetFullPath("Logs/LevelBrowser") + Path.DirectorySeparatorChar;
            string recovery = Path.GetFullPath(Path.Combine(folder, "recovery"));
            if (folder.StartsWith(testRoot, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(folder).StartsWith("ui-", StringComparison.Ordinal) &&
                recovery.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(recovery))
                Directory.Delete(recovery, true);
            string savedFixture = Path.GetFullPath(Path.Combine(folder, "saved-workspace"));
            if (folder.StartsWith(testRoot, StringComparison.OrdinalIgnoreCase) && savedFixture.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(savedFixture))
                Directory.Delete(savedFixture, true);
            EditorApplication.Exit(code);
        }
    }
}

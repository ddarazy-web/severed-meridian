using System;
using System.IO;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;
using LevelTool;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolRefreshPerformanceVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static LevelToolScreen screen;
        private static string folder;
        private static bool previousEnabled;
        private static EnterPlayModeOptions previousOptions;
        private static double deadline;
        private static bool awaitingListing;

        public static void Run()
        {
            folder = Path.GetFullPath("Logs/LevelToolRefreshPerformance/check-" + Guid.NewGuid().ToString("N"));
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
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Refresh verification timeout");
                if (!EditorApplication.isPlaying) return;
                screen ??= UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                if (screen == null || (bool)Get("busy")) return;
                if (awaitingListing)
                {
                    Check(screen.Workspace.Directory.MetadataWarning == null, "explicit listing refresh completed");
                    Check(screen.Workspace.Session.IsDirty && screen.Workspace.Session.GetDocumentInfo(screen.Workspace.Session.SelectedLevelId).DisplayName == "목록 갱신 초안",
                        "listing UI preserved unsaved draft");
                    Check(((LevelBrowserIndex)Get("levelIndex")).Search("2").Count == 1, "listing UI refreshed unknown number");
                    screen.Workspace.Session.Undo(); Check(!screen.Workspace.Session.IsDirty, "listing UI preserved Undo");
                    Check(Get("modal") == null && ((VisualElement)Get("editor")).enabledInHierarchy, "listing progress closed and editor restored");
                    Finish(0); return;
                }
                LevelToolRecoveryIsolation.Verify(screen, Path.Combine(folder, "recovery"));
                screen.Workspace.Create(Path.Combine(folder, "project"), LevelToolTestProject.Create());
                Call("Refresh");
                VisualElement properties = ((ScrollView)Get("properties")).contentContainer;
                VisualElement firstProperty = properties[0];
                VisualElement palette = ((ScrollView)Get("palette")).contentContainer;
                VisualElement firstMaterial = palette[0];
                VisualElement board = (VisualElement)Get("board");
                VisualElement firstBoard = board[0];
                Call("Refresh");
                Check(ReferenceEquals(firstProperty, properties[0]), "unchanged refresh preserves property controls");
                Call("Edit", (Action)(() => screen.Workspace.Session.EditLevel("이름 변경", level => level["displayName"] = "갱신 검사")));
                Check(ReferenceEquals(firstMaterial, palette[0]), "name edit preserves material controls");
                Check(ReferenceEquals(firstBoard, board[0]), "name edit preserves board controls");
                Check(screen.Workspace.Session.IsDirty, "name edit retains dirty state");
                Call("Edit", (Action)(() => screen.Workspace.Session.Undo()));
                Check(!screen.Workspace.Session.IsDirty, "Undo returns to saved state");
                Call("Edit", (Action)(() => screen.Workspace.Session.Redo()));
                Check((string)screen.Workspace.Session.Get(screen.Workspace.Session.SelectedLevelId).Data["displayName"] == "갱신 검사", "Redo restores name");
                object definitions = Get("definitions");
                long version = screen.Workspace.Session.GetDocumentVersion(screen.Workspace.Session.SelectedLevelId);
                screen.Workspace.Session.Get(screen.Workspace.Session.SelectedLevelId).Data["displayName"] = "외부 변경";
                Check(screen.Workspace.Session.GetDocumentVersion(screen.Workspace.Session.SelectedLevelId) == version &&
                    (string)screen.Workspace.Session.Get(screen.Workspace.Session.SelectedLevelId).Data["displayName"] == "갱신 검사", "version lookup preserves public copy isolation");
                Call("Edit", (Action)(() => screen.Workspace.Session.SelectCells(new[] { 40 })));
                Check(ReferenceEquals(definitions, Get("definitions")), "selection reuses definitions");
                Check(ReferenceEquals(firstBoard, board[0]), "selection preserves existing board cells");
                typeof(LevelToolScreen).GetField("brush", Flags).SetValue(screen, "supply.normal.fixed");
                Call("Refresh");
                Check(ReferenceEquals(definitions, Get("definitions")), "brush reuses definitions");
                Check(screen.Workspace.Session.GetDocumentVersion(screen.Workspace.Session.SelectedLevelId) == version, "selection and brush do not edit documents");
                typeof(LevelToolScreen).GetField("checkedPackHash", Flags).SetValue(screen,
                    ContentSnapshotFingerprint.Compute(screen.Workspace.Session.CreateSnapshot()));
                Call("UpdatePackStatus");
                int comparisons = (int)Get("packComparisonCount");
                Call("Refresh");
                Check((int)Get("packComparisonCount") == comparisons, "unchanged refresh reuses pack comparison");
                int reads = (int)Get("displayDocumentReadCount"), builds = (int)Get("definitionBuildCount");
                Call("Refresh");
                Check((int)Get("displayDocumentReadCount") == reads && (int)Get("definitionBuildCount") == builds,
                    "unchanged refresh performs zero display document reads and definition builds");
                Call("ValidateLevel", false);
                Check((bool)typeof(LevelToolScreen).GetMethod("ValidationIsCurrent", Flags).Invoke(screen, null), "validation initially current");
                Call("Edit", (Action)(() => screen.Workspace.Session.EditLevel("셀 변경", level => level["board"]["cells"][40]["isActive"] = false)));
                Check(board.Q("cell-40").ClassListContains("inactive"), "cell edit updates board");
                Check((int)Get("displayDocumentReadCount") == reads + 1 && (int)Get("definitionBuildCount") == builds,
                    "single level edit reads one display document without rebuilding definitions");
                Check(!(bool)typeof(LevelToolScreen).GetMethod("ValidationIsCurrent", Flags).Invoke(screen, null), "changed document invalidates validation");
                Check((int)Get("packComparisonCount") == comparisons + 1, "changed document recomputes pack comparison");
                Call("Edit", (Action)(() => screen.Workspace.Session.Undo()));
                Check(!board.Q("cell-40").ClassListContains("inactive"), "Undo restores board appearance");
                Check((bool)typeof(LevelToolScreen).GetMethod("ValidationIsCurrent", Flags).Invoke(screen, null), "Undo restores validation currency");
                typeof(LevelToolScreen).GetField("cellSize", Flags).SetValue(screen, 64f);
                VisualElement beforeZoom = board.Q("cell-40");
                VisualElement propertiesBeforeZoom = properties[0];
                Call("Refresh");
                Check(ReferenceEquals(beforeZoom, board.Q("cell-40")), "zoom preserves existing board cells");
                Check(ReferenceEquals(propertiesBeforeZoom, properties[0]), "zoom preserves property inputs");
                Check(screen.Workspace.Session.GetDocumentVersion(screen.Workspace.Session.SelectedLevelId) == version, "zoom does not edit document");
                screen.Workspace.Save();
                screen.Workspace.Reload("Discard");
                Call("Refresh");
                Check(!ReferenceEquals(definitions, Get("definitions")), "same ID reload replaces derived definitions");
                Check(((Label)Get("levelTitle")).text.Contains("갱신 검사"), "same ID reload shows saved name");
                Call("BeginTutorialPick", "action", null, null, null);
                Call("CancelTutorialPick");
                Check(properties[0].enabledInHierarchy, "cancel tutorial cell picker restores property input");
                typeof(LevelToolScreen).GetField("inspectorPage", Flags).SetValue(screen, "공급");
                Call("Refresh");
                int previousTarget = properties.Q<IntegerField>("scrapTarget").value;
                Call("Edit", (Action)(() => screen.Workspace.Session.EditLevel("공급 변경", level => level["elementSupply"]["scrapTarget"] = 7)));
                Check(properties.Q<IntegerField>("scrapTarget").value == 7, "supply edit updates visible field");
                Call("Edit", (Action)(() => screen.Workspace.Session.Undo()));
                Check(properties.Q<IntegerField>("scrapTarget").value == previousTarget, "supply Undo updates visible field");
                screen.RequestLeave(() => throw new InvalidOperationException("검사 이탈 실패"));
                Check(((VisualElement)Get("editor")).enabledSelf && !(bool)Get("busy") &&
                    ((Label)Get("status")).text.Contains("검사 이탈 실패"), "failed leave restores enabled screen with error");
                VisualElement beforeLeave = properties[0];
                screen.RequestLeave(() => { });
                Check(ReferenceEquals(beforeLeave, properties[0]), "successful clean leave does not rebuild properties");
                Call("Edit", (Action)(() => screen.Workspace.Session.EditLevel("잘못된 참조", level => level["catalogId"] = "missing-catalog")));
                Check(properties.childCount == 0, "failed display preparation does not leave stale editable controls");
                Call("Edit", (Action)(() => screen.Workspace.Session.Undo()));
                Check(properties.Q<IntegerField>("scrapTarget")?.value == previousTarget, "Undo recovers display after invalid reference");
                string listing = Path.Combine(folder, "listing");
                ContentDocument displayLevel = screen.Workspace.Session.Get(screen.Workspace.Session.SelectedLevelId);
                ContentDocument project = LevelToolTestProject.Create().Project;
                var entries = new JArray();
                for (int index = 0; index < 10000; index++)
                    entries.Add(new JObject { ["id"] = "ui-level-" + index, ["path"] = "levels/" + index + ".json" });
                project.Data["documents"] = entries;
                var files = new JsonContentStore(listing); files.Save(project, "project.json", null);
                screen.Workspace.OpenListing(listing, "Discard"); Call("Refresh");
                var browser = (ListView)Get("levelBrowser");
                Check(screen.Workspace.Session == null && browser.itemsSource.Count == 10000 && browser.enabledInHierarchy,
                    "actual scene displays 10000 entries without selecting a body");
                Check(((TextField)Get("levelSearch")).enabledInHierarchy, "listing-only search enabled");
                files.Save(new ContentDocument("level", "ui-level-0", displayLevel.Data), "levels/0.json", null);
                int priorComparisons = (int)Get("packComparisonCount");
                Call("OpenSelectedLevel", "ui-level-0", null);
                Check(screen.Workspace.Session?.SelectedLevelId == "ui-level-0" && board.Q("cell-40") != null,
                    "explicit edit shows selected board without other bodies");
                Check((int)Get("packComparisonCount") == priorComparisons, "opening selected level avoids automatic whole-project pack comparison");
                Check(Get("validatedDocuments") == null, "old project validation not reused in new directory session");
                string refreshFolder = Path.Combine(folder, "refresh");
                screen.Workspace.Create(refreshFolder, LevelToolTestProject.Create());
                string selectedId = screen.Workspace.Session.SelectedLevelId;
                DocumentEditing.DuplicateLevel(screen.Workspace.Session, selectedId, 2, "목록 테스트"); screen.Workspace.Save();
                foreach (string file in Directory.GetFiles(Path.Combine(refreshFolder, ".authoring", "listings"), "*.json")) File.Delete(file);
                screen.Workspace.OpenListing(refreshFolder, "Discard"); screen.Workspace.OpenLevel(selectedId);
                screen.Workspace.Session.EditLevel("초안", value => value["displayName"] = "목록 갱신 초안"); Call("Refresh");
                var root = (VisualElement)Get("root");
                Check(root.Q<Button>("rebuild-level-list")?.enabledInHierarchy == true, "explicit listing refresh button available");
                Call("RequestListingRebuild");
                Check(root.Q<Button>("confirm-listing-rebuild") != null, "listing rebuild explains full scan before execution");
                Call("CloseModal"); Check(screen.Workspace.Session.IsDirty, "dismissing listing rebuild preserves draft");
                awaitingListing = true; Call("RunListingRebuild");
                Check(root.Q<Label>("listing-progress") != null && root.Q<Button>("cancel-listing-rebuild") != null, "listing progress and cancel controls visible");
            }
            catch (Exception error)
            {
                File.AppendAllText(Path.Combine(folder, "results.txt"), error + "\n");
                Debug.LogException(error); Finish(1);
            }
        }

        private static object Get(string name) => typeof(LevelToolScreen).GetField(name, Flags).GetValue(screen);
        private static void Call(string name, params object[] args) => typeof(LevelToolScreen).GetMethod(name, Flags).Invoke(screen, args);
        private static void Check(bool value, string message)
        {
            File.AppendAllText(Path.Combine(folder, "results.txt"), (value ? "PASS " : "FAIL ") + message + "\n");
            if (!value) throw new Exception(message);
        }
        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnabled;
            EditorSettings.enterPlayModeOptions = previousOptions;
            foreach (string child in new[] { "project", "listing", "refresh", "recovery" })
            {
                string target = Path.GetFullPath(Path.Combine(folder, child));
                if (!target.StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("검사 사본 경로가 소유 범위를 벗어났습니다.");
                if (Directory.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) == 0)
                    Directory.Delete(target, true);
            }
            EditorApplication.Exit(code);
        }
    }
}

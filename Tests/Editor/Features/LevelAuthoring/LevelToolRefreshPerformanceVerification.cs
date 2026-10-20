using System;
using System.IO;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelAuthoring.Documents;
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
                LevelToolRecoveryIsolation.Verify(screen, Path.Combine(folder, "recovery"));
                screen.Workspace.Create(Path.Combine(folder, "project"), LevelToolDocuments.NewProject());
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
                Call("Refresh");
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
                Finish(0);
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
            EditorApplication.Exit(code);
        }
    }
}

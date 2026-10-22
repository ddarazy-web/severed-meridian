using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolBoardViewVerification
    {
        public static void Run()
        {
            int exit = 0;
            IDisposable view = null;
            try
            {
                Type type = typeof(LevelToolScreen).Assembly.GetType("LevelTool.Presentation.LevelToolBoardView");
                Check(type != null, "board presentation has its own lifetime owner");
                Type overlayType = type.Assembly.GetType("LevelTool.Presentation.LevelToolBoardOverlayState");
                Check(overlayType != null, "board overlay inputs are explicit");
                string folder = Path.GetFullPath("Logs/LevelToolRefactorStage03/board-" + Guid.NewGuid().ToString("N"));
                AuthoringToolWorkspace workspace = new AuthoringToolWorkspace();
                workspace.Create(folder, LevelToolTestProject.Create());
                workspace.Session.SelectCells(new[] { 10, 11 });
                string before = workspace.Session.ExportState();
                VisualElement root = new VisualElement();
                view = (IDisposable)Activator.CreateInstance(type, root, (Action<string>)(_ => { }), (Action<int>)(_ => { }), (Func<bool>)(() => false));
                Check(ReferenceEquals(root, type.GetProperty("Root").GetValue(view)), "view uses the root shared with input");
                MethodInfo refresh = type.GetMethod("Refresh");
                object[] values = { workspace.Session, workspace.Folder, LevelToolDocuments.Definitions(workspace.Session), "Block", 52f, Activator.CreateInstance(overlayType) };
                refresh.Invoke(view, values);
                Check(root.Query<Label>().ToList().Count(label => label.name?.StartsWith("cell-") == true) == 81, "view renders the complete nine by nine board");
                Check(root.Q("cell-10").ClassListContains("selected") && !root.Q("cell-9").ClassListContains("selected"), "view reads selection from the session");
                Check(root.style.width.value.value == 468 && root.style.height.value.value == 468, "view preserves cell geometry");
                Check(workspace.Session.ExportState() == before, "rendering does not change document selection or undo history");
                var definitions = (Dictionary<string, JObject>)values[2];
                string large = definitions.First(pair => LevelToolDocuments.Size(pair.Value) == 2).Key;
                workspace.Session.EditLevel("큰 장애물 검사", level =>
                {
                    level["elements"] = new JArray();
                    LevelDocumentEditing.Place(level, definitions, large, 3, 3, true);
                });
                before = workspace.Session.ExportState();
                refresh.Invoke(view, values);
                Image largeImage = root.Query<Image>().ToList().Single();
                float width = largeImage.style.width.value.value, height = largeImage.style.height.value.value;
                Check(Math.Max(width, height) >= 52 * 1.5f && Math.Max(width, height) <= 52 * 3,
                    "large obstacle artwork occupies approximately two cells");
                Check(Math.Abs(largeImage.style.left.value.value + width / 2 - 4 * 52) < 52 &&
                    Math.Abs(largeImage.style.top.value.value + height / 2 - 4 * 52) < 52,
                    "large obstacle artwork remains near its logical footprint center");
                values[4] = 104f;
                refresh.Invoke(view, values);
                Check(ReferenceEquals(largeImage, root.Query<Image>().ToList().Single()) &&
                    Math.Abs(largeImage.style.width.value.value - width * 2) < .01 &&
                    Math.Abs(largeImage.style.height.value.value - height * 2) < .01,
                    "large obstacle zoom reuses artwork and scales both dimensions");
                values[4] = 52f;
                type.GetMethod("Clear").Invoke(view, null);
                Check(root.childCount == 0, "clear removes previous level presentation");
                refresh.Invoke(view, values);
                Check(root.Q("cell-80") != null, "view can render again after clear");
                view.Dispose(); view.Dispose();
                bool rejected = false;
                try { refresh.Invoke(view, values); }
                catch (TargetInvocationException error) when (error.InnerException is ObjectDisposedException) { rejected = true; }
                Check(rejected, "disposed presentation cannot be refreshed by a stale caller");
                Check(workspace.Session.ExportState() == before, "view teardown preserves editing state");
            }
            catch (Exception error) { Debug.LogException(error); exit = 1; }
            finally { view?.Dispose(); }
            EditorApplication.Exit(exit);
        }

        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("FAIL " + message);
            Debug.Log("PASS " + message);
        }
    }
}

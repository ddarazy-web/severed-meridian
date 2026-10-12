using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using Levels;
using Levels.Editor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class JsonWindowSelectionVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Output = "Logs/AndroidLevelToolStage03/json-window-selection-results.txt";
        private static LevelEditorWindow window;
        private static IEnumerator sequence;
        private static readonly List<string> results = new List<string>();
        private static double nextTick;

        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output)); results.Clear();
            sequence = CheckMenus(); EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.2;
            try { if (!sequence.MoveNext()) Finish(0); }
            catch (Exception error) { results.Add("FAIL " + error); Debug.LogException(error); Finish(1); }
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            File.WriteAllLines(Output, results);
            if (window != null)
            {
                typeof(EditorWindow).GetProperty("hasUnsavedChanges").GetSetMethod(true).Invoke(window, new object[] { false });
                UnityEngine.Object.DestroyImmediate(window); window = null;
            }
            EditorApplication.Exit(code);
        }

        private static IEnumerator CheckMenus()
        {
            var stored = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt")).Read();
            string root = Path.GetFullPath("ContentData/Trials/android-level-tool-stage-03/window-selection-" + Guid.NewGuid().ToString("N"));
            new ContentSnapshotStore(root).Publish(stored.Snapshot, null);
            window = ScriptableObject.CreateInstance<LevelEditorWindow>();
            window.position = new Rect(10, 10, 1100, 800); window.ShowUtility(); yield return null;
            // 공개 CreateGUI는 씬 안내 화면이므로 기존 창 메뉴 검사는 기존 편집 UI를 명시적으로 연다.
            Invoke("CreateLegacyEditorGUI");
            Invoke("OpenJsonWorkspace", root); yield return null;
            JsonAuthoringWorkspace workspace = (JsonAuthoringWorkspace)typeof(LevelEditorWindow).GetField("jsonWorkspace", Flags).GetValue(window);
            string id = workspace.Session.Documents.First(document => document.Kind == "level" && (int)document.Data["schemaVersion"] == 5).Id;
            workspace.Session.SelectLevel(id);
            var definitions = workspace.Session.Documents.Where(document => document.Kind == "element").ToDictionary(document => (string)document.Data["definition"]["id"], document => (JObject)document.Data["definition"]);
            workspace.Session.Apply("메뉴 검사 배치", documents =>
            {
                JObject level = documents[id].Data;
                level["elements"] = new JArray(); level["connections"] = new JArray();
                foreach (var cell in level["board"]["cells"]) cell["isActive"] = true;
                LevelDocumentEditing.Place(level, definitions, "obstacle.metal-rod-box", 1, 1, false);
                LevelDocumentEditing.Place(level, definitions, "floor.dust", 2, 2, false);
            });
            workspace.RestoreDisplay(); Invoke("SetLevel", workspace.Level);
            LevelBoardView board = (LevelBoardView)typeof(LevelEditorWindow).GetField("board", Flags).GetValue(window);
            double deadline = EditorApplication.timeSinceStartup + 10;
            while (board == null || window.rootVisualElement.panel == null)
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("기존 EditorWindow 편집 UI 준비 시간 초과");
                yield return null;
                board = (LevelBoardView)typeof(LevelEditorWindow).GetField("board", Flags).GetValue(window);
            }
            board.Brush = LevelBrush.Select; board.Layer = PlacementLayer.Obstacle;
            Invoke("SelectCell", new BoardCoordinate(2, 2)); yield return null;
            string before = workspace.Session.Get(id).Data.ToString();
            LevelContextMenuVerification.Action(window, "요소 복사").Execute(); yield return null;
            Check(workspace.Session.Get(id).Data.ToString() == before, "Editor JSON copy menu leaves document unchanged");
            board.Layer = PlacementLayer.Dust;
            Invoke("SelectCell", new BoardCoordinate(4, 4)); yield return null;
            bool wrongLayerDisabled = false;
            try { LevelContextMenuVerification.Action(window, "요소 붙여넣기"); } catch (InvalidOperationException) { wrongLayerDisabled = true; }
            Check(wrongLayerDisabled && workspace.Session.Get(id).Data.ToString() == before, "Editor JSON different layer disables clipboard paste without editing");
            board.Layer = PlacementLayer.Obstacle;
            workspace.Session.Apply("원본 속성 수정", documents => documents[id].Data["elements"][0]["durability"] = 1);
            workspace.RestoreDisplay(); Invoke("Refresh");
            Invoke("SelectCell", new BoardCoordinate(4, 4)); yield return null;
            LevelContextMenuVerification.Action(window, "요소 붙여넣기").Execute(); yield return null;
            JObject pasted = workspace.Session.Get(id).Data;
            Check(pasted["elements"].Count() == 3 && (int)pasted["elements"][2]["durability"] != 1, "Editor JSON paste uses independent whole body clipboard");
            string freshId = (string)pasted["elements"][2]["instanceId"];
            Check(freshId != (string)pasted["elements"][0]["instanceId"], "Editor JSON paste assigns fresh ID");
            Invoke("UndoJson", false); yield return null;
            Check(workspace.Session.Get(id).Data["elements"].Count() == 2, "Editor JSON paste is one Undo");
            Invoke("UndoJson", true); yield return null;
            Check((string)workspace.Session.Get(id).Data["elements"][2]["instanceId"] == freshId, "Editor JSON paste Redo preserves ID");
            Invoke("SelectCell", new BoardCoordinate(2, 2)); yield return null;
            LevelContextMenuVerification.Action(window, "선택한 층의 요소 삭제").Execute(); yield return null;
            Check(workspace.Session.Get(id).Data["elements"].Count() == 2 && (string)workspace.Session.Get(id).Data["elements"][0]["layer"] == "Dust", "Editor JSON delete child preserves other layer");
            Invoke("UndoJson", false); yield return null;
            Check(workspace.Session.Get(id).Data["elements"].Count() == 3, "Editor JSON delete one Undo restores whole body");
            Invoke("SelectCell", new BoardCoordinate(2, 2)); yield return null;
            LevelContextMenuVerification.Action(window, "요소 잘라내기").Execute(); yield return null;
            Check(workspace.Session.Get(id).Data["elements"].Count() == 2, "Editor JSON cut removes whole selected body");
            Invoke("SelectCell", new BoardCoordinate(6, 6)); yield return null;
            LevelContextMenuVerification.Action(window, "요소 붙여넣기").Execute(); yield return null;
            Check(workspace.Session.Get(id).Data["elements"].Count() == 3, "Editor JSON cut clipboard pastes after removal");
            Invoke("SaveJsonWorkspace"); Invoke("OpenJsonWorkspace", root); yield return null;
            board = (LevelBoardView)typeof(LevelEditorWindow).GetField("board", Flags).GetValue(window);
            board.Brush = LevelBrush.Select; board.Layer = PlacementLayer.Obstacle;
            Invoke("SelectCell", new BoardCoordinate(8, 8)); yield return null;
            bool disabled = false;
            try { LevelContextMenuVerification.Action(window, "요소 붙여넣기"); } catch (InvalidOperationException) { disabled = true; }
            Check(disabled, "Editor JSON new session resets clipboard");
        }

        private static object Invoke(string method, params object[] arguments) => typeof(LevelEditorWindow).GetMethod(method, Flags | BindingFlags.Public).Invoke(window, arguments);
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); results.Add("PASS " + message); }
    }
}

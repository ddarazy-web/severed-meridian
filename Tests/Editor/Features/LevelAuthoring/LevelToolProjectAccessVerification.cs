using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameScreen;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using LevelTool;
using Levels;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class LevelToolProjectAccessVerification
    {
        public static void Run()
        {
            string temporary = Path.Combine(Path.GetTempPath(), "LevelToolProjects-" + Guid.NewGuid().ToString("N"));
            int exit = 0;
            try
            {
                string root = LevelToolProjectAccess.InternalRoot(temporary);
                Check(root == Path.Combine(temporary, "LevelTool", "Projects") && LevelToolProjectAccess.List(root).Length == 0, "internal root and empty project list");
                foreach (string name in new[] { "", " ", ".", "..", "../escape", "a/b", "a\\b", "C:\\escape", "a:b", "a?b", "a\nb", " trailing", "trailing.", "CON", "nul.json" })
                    Reject(() => LevelToolProjectAccess.Resolve(root, name, true), "invalid project name " + name);

                AuthoringToolWorkspace workspace = new AuthoringToolWorkspace();
                Check(LevelToolProjectAccess.OpenDefault(workspace, root, LevelToolDocuments.NewProject()), "create first default project");
                string levelId = workspace.Session.SelectedLevelId;
                string projectId = workspace.Session.CreateSnapshot().Project.Id;
                Dictionary<string, JObject> definitions = LevelToolDocuments.Definitions(workspace.Session);
                string block = definitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Block" && (string)pair.Value["supply"]?["behavior"] == "FixedNormal").Key;
                string obstacle = definitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Obstacle" && LevelToolDocuments.Size(pair.Value) == 2).Key;
                workspace.Session.Apply("일반 블록·2×2 장애물 배치", documents =>
                {
                    MissionDocumentEditing.Set(documents[levelId].Data, 0, "Color", "Type1", 10);
                    LevelDocumentEditing.Place(documents[levelId].Data, definitions, block, 0, 0, false);
                    LevelDocumentEditing.Place(documents[levelId].Data, definitions, obstacle, 3, 3, false);
                });
                JObject edited = workspace.Session.Get(levelId).Data;
                workspace.Save();
                AuthoringToolWorkspace reopened = new AuthoringToolWorkspace();
                Check(LevelToolProjectAccess.OpenDefault(reopened, root, null), "reopen existing default without template");
                Check(reopened.Session.CreateSnapshot().Project.Id == projectId && reopened.Session.SelectedLevelId == levelId &&
                    JToken.DeepEquals(edited, reopened.Session.Get(levelId).Data), "save and reopen preserve IDs, placements and references");
                PuzzlePlayRequest request = JsonPuzzlePlayAdapter.CreateRequest(reopened.Session.CreateSnapshot(), levelId, 37);
                LevelDefinition playable = request.CreateDefinition();
                try
                {
                    Check(playable.Elements.Count == 2 && playable.Elements.Select(item => item.instanceId)
                        .OrderBy(id => id).SequenceEqual(edited["elements"].Select(item => (string)item["instanceId"]).OrderBy(id => id)), "shared game request preserves both edited placements");
                    foreach (ElementPlacementDefinition item in playable.Elements)
                    {
                        JToken expected = edited["elements"].Single(value => (string)value["instanceId"] == item.instanceId);
                        Check(item.definitionId == (string)expected["definitionId"] && item.coordinate.Row == (int)expected["coordinate"]["row"] &&
                            item.coordinate.Column == (int)expected["coordinate"]["column"] && item.durability == (int)expected["durability"], "game request placement data " + item.definitionId);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(playable); }

                reopened.SaveCopy(LevelToolProjectAccess.Resolve(root, "Copy", true));
                Check(reopened.Folder == Path.Combine(root, "Copy") && reopened.Session.CreateSnapshot().Project.Id == projectId, "copy publication preserves document IDs");
                AuthoringToolWorkspace copied = new AuthoringToolWorkspace();
                Check(copied.Open(LevelToolProjectAccess.Resolve(root, "Copy"), "Cancel") && copied.Session.CreateSnapshot().Project.Id == projectId &&
                    JToken.DeepEquals(copied.Session.Get(levelId).Data, edited), "saved copy reopens with matching data and references");
                Check(LevelToolProjectAccess.List(root).SequenceEqual(new[] { "Copy", "Default" }), "internal project list contains direct names");
                string second = LevelToolProjectAccess.Resolve(root, "Second", true);
                Check(workspace.Create(second, LevelToolDocuments.NewProject(), "Discard"), "second project creation");
                Directory.CreateDirectory(Path.Combine(root, "Empty"));
                Directory.CreateDirectory(Path.Combine(root, "Partial"));
                File.WriteAllText(Path.Combine(root, "Partial", "unfinished.json"), "unfinished");
                foreach (string name in new[] { "Default", "Copy", "Empty", "Partial" })
                    Reject(() => LevelToolProjectAccess.Resolve(root, name, true), "duplicate folder protected " + name);

                AuthoringEditSession current = reopened.Session;
                current.SelectCells(new[] { 0, 30 });
                current.Apply("기존 세션 보존", documents => documents[levelId].Data["displayName"] = "보존할 초안");
                current.Apply("Redo 보존", documents => documents[levelId].Data["displayName"] = "다시 실행할 초안");
                current.Undo();
                string state = reopened.ExportState();
                Check(!LevelToolProjectAccess.OpenDefault(reopened, root, null) && reopened.ExportState() == state && current.CanUndo && current.CanRedo,
                    "default entry preserves current dirty session, selection and history");
                Check(!reopened.Open(second, "Cancel") && !reopened.Create(Path.Combine(root, "Cancelled"), LevelToolDocuments.NewProject()) &&
                    !Directory.Exists(Path.Combine(root, "Cancelled")) && reopened.ExportState() == state, "cancel preserves session and avoids project creation");

                string broken = Path.Combine(root, "Broken");
                Directory.CreateDirectory(broken);
                File.WriteAllText(Path.Combine(broken, "project.json"), "{broken");
                Reject(() => reopened.Open(LevelToolProjectAccess.Resolve(root, "Broken"), "Discard"), "corrupt project rejected");
                Check(reopened.ExportState() == state && ReferenceEquals(current, reopened.Session), "failed open preserves current session");
                string blocked = Path.Combine(root, "Blocked");
                File.WriteAllText(blocked, "blocked");
                Reject(() => reopened.SaveCopy(blocked), "copy write failure");
                Check(reopened.ExportState() == state, "write failure preserves source folder and draft");

                string partialRoot = Path.Combine(temporary, "partial");
                Directory.CreateDirectory(Path.Combine(partialRoot, "Default"));
                string partialFile = Path.Combine(partialRoot, "Default", "unfinished.json");
                File.WriteAllText(partialFile, "unfinished");
                Reject(() => LevelToolProjectAccess.OpenDefault(new AuthoringToolWorkspace(), partialRoot, LevelToolDocuments.NewProject()), "partial default rejected");
                Check(File.ReadAllText(partialFile) == "unfinished" && !File.Exists(Path.Combine(partialRoot, "Default", "project.json")), "partial default never overwritten");
                string corruptRoot = Path.Combine(temporary, "corrupt");
                Directory.CreateDirectory(Path.Combine(corruptRoot, "Default"));
                string corruptFile = Path.Combine(corruptRoot, "Default", "project.json");
                File.WriteAllText(corruptFile, "{broken");
                Reject(() => LevelToolProjectAccess.OpenDefault(new AuthoringToolWorkspace(), corruptRoot, null), "corrupt default rejected");
                Check(File.ReadAllText(corruptFile) == "{broken", "corrupt default never overwritten");
                string emptyRoot = Path.Combine(temporary, "empty");
                Directory.CreateDirectory(Path.Combine(emptyRoot, "Default"));
                Check(LevelToolProjectAccess.OpenDefault(new AuthoringToolWorkspace(), emptyRoot, LevelToolDocuments.NewProject()), "empty default folder initialized");
            }
            catch (Exception error) { exit = 1; Debug.LogException(error); }
            finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
            EditorApplication.Exit(exit);
        }

        private static void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); }
            catch (ContentFormatException) { rejected = true; }
            catch (IOException) { rejected = true; }
            Check(rejected, message);
        }

        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("FAIL " + message);
            Debug.Log("PASS " + message);
        }
    }
}

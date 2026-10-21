using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace LevelAuthoring.Editor
{
    public static class ReferencedRecoveryVerification
    {
        public static void Run()
        {
            string owned = Path.GetFullPath("Logs/ReferencedRecovery/" + Guid.NewGuid().ToString("N"));
            string data = Path.Combine(owned, "data"); Directory.CreateDirectory(owned);
            int exit = 1;
            try
            {
                var setup = new AuthoringToolWorkspace(); setup.Create(data, LevelToolDocuments.NewProject());
                string first = setup.Session.SelectedLevelId;
                string second = DocumentEditing.DuplicateLevel(setup.Session, first, 2, "Unread"); setup.Save();
                foreach (string index in Directory.GetFiles(Path.Combine(data, ".authoring", "listings"), "*.json")) File.Delete(index);
                var source = new AuthoringToolWorkspace(); source.OpenListing(data, "Cancel"); source.OpenLevel(first);
                Check(source.Session.GetDocumentInfo(second).Kind == "unknown", "Legacy fixture unexpectedly read the second body");
                source.Session.EditLevel("one", body => body["displayName"] = "Changed");
                source.Session.EditLevel("two", body => body["moveCount"] = -1);
                source.Session.Undo(); source.Session.SelectCells(new[] { 40 });
                string path = Path.Combine(data, source.Directory.Entries.Single(entry => entry.Id == second).RelativePath);
                string state;
                var target = new AuthoringToolWorkspace();
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    state = source.ExportState();
                    Check((int)JObject.Parse(state)["session"]["version"] == 2, "Directory recovery still writes every body as v1");
                    target.RestoreState(state);
                    Check(target.Session.IsDirty && target.Session.SelectedCells.SequenceEqual(new[] { 40 }), "Dirty/selection lost");
                    Check((string)target.Session.Get(first).Data["displayName"] == "Changed", "Draft lost");
                    target.Session.Redo(); Check((int)target.Session.Get(first).Data["moveCount"] == -1, "Unfinished Redo lost");
                    target.Session.Undo(); target.Save();
                    Check(!target.Session.IsDirty, "Restored partial save failed");
                }
                // 복원 뒤 처음 요청하는 원본도 외부 변경을 그대로 받아들이면 안 된다.
                File.AppendAllText(path, " ");
                bool rejected = false;
                try { target.Session.Get(second); } catch (ContentFormatException) { rejected = true; }
                Check(rejected, "Changed source silently replaced a recovery reference");
                Check((string)target.Session.Get(first).Data["displayName"] == "Changed", "Reference failure lost current draft");
                File.Delete(path);
                rejected = false;
                try { target.Session.Get(second); } catch (ContentFormatException) { rejected = true; }
                Check(rejected, "Missing source silently accepted");
                AuthoringEditSession preserved = target.Session;
                // 원본 manifest가 다음 저장 세대로 바뀐 뒤 과거 복구 기록을 강제 적용하지 않는다.
                rejected = false;
                try { target.RestoreState(state); } catch (ContentFormatException) { rejected = true; }
                Check(rejected && ReferenceEquals(target.Session, preserved), "Failed recovery replaced active workspace");
                string reindexed = Path.Combine(data, "reindexed");
                var external = new AuthoringToolWorkspace(); external.Create(reindexed, LevelToolDocuments.NewProject());
                string edited = external.Session.SelectedLevelId;
                external.OpenListing(reindexed, "Discard"); external.OpenLevel(edited);
                external.Session.EditLevel("draft", body => body["displayName"] = "protected draft");
                string beforeExternal = external.ExportState();
                string relative = external.Directory.Entries.Single(entry => entry.Id == edited).RelativePath;
                var files = new JsonContentStore(reindexed);
                ContentFile old = files.Read(relative); old.Document.Data["displayName"] = "external";
                files.Save(old.Document, relative, old.Hash);
                new ContentSnapshotStore(reindexed).RebuildDirectoryIndex(System.Threading.CancellationToken.None);
                rejected = false;
                try { new AuthoringToolWorkspace().RestoreState(beforeExternal); } catch (ContentFormatException) { rejected = true; }
                Check(rejected, "Refreshed index silently replaced the recorded baseline hash");
                string deletionFolder = Path.Combine(data, "deletion");
                var deletion = new AuthoringToolWorkspace(); deletion.Create(deletionFolder, LevelToolDocuments.NewProject());
                string kept = deletion.Session.SelectedLevelId;
                string removed = DocumentEditing.DuplicateLevel(deletion.Session, kept, 2, "Remove"); deletion.Save();
                deletion.OpenListing(deletionFolder, "Discard"); deletion.OpenLevel(kept);
                string removedPath = deletion.Directory.Entries.Single(entry => entry.Id == removed).RelativePath;
                deletion.Session.ApplyToDocuments("remove", new[] { removed }, docs => docs.Remove(removed));
                string deletionState = deletion.ExportState();
                var deletionFiles = new JsonContentStore(deletionFolder);
                ContentFile removedFile = deletionFiles.Read(removedPath); removedFile.Document.Data["displayName"] = "external edit before delete";
                deletionFiles.Save(removedFile.Document, removedPath, removedFile.Hash);
                var restoredDeletion = new AuthoringToolWorkspace(); restoredDeletion.RestoreState(deletionState);
                rejected = false;
                try { restoredDeletion.Save(); } catch (ContentFormatException) { rejected = true; }
                Check(rejected && restoredDeletion.Session.IsDirty, "Restored deletion ignored external changes to removed document");
                string backupFolder = Path.Combine(data, "backup");
                var backup = new AuthoringToolWorkspace(); backup.Create(backupFolder, LevelToolDocuments.NewProject());
                string backupLevel = backup.Session.SelectedLevelId;
                backup.Session.EditLevel("save", body => body["displayName"] = "next generation"); backup.Save();
                backup.OpenListing(backupFolder, "Discard", true); backup.OpenLevel(backupLevel);
                var backupRestored = new AuthoringToolWorkspace(); backupRestored.RestoreState(backup.ExportState());
                Check(backupRestored.IsRecovery, "Backup recovery lost read-only source protection");
                rejected = false;
                try { backupRestored.Save(); } catch (ContentFormatException) { rejected = true; }
                Check(rejected, "Backup recovery allowed original overwrite");
                string discardFolder = Path.Combine(data, "discard");
                var discard = new AuthoringToolWorkspace(); discard.Create(discardFolder, LevelToolDocuments.NewProject());
                string a = discard.Session.SelectedLevelId;
                string b = DocumentEditing.DuplicateLevel(discard.Session, a, 2, "Other"); discard.Save();
                discard.OpenListing(discardFolder, "Discard"); discard.OpenLevel(a);
                string projectId = discard.Directory.Project.Id;
                string originalName = (string)discard.Session.Get(a).Data["displayName"];
                discard.Session.EditLevel("A", body => body["displayName"] = "discard this");
                discard.Session.ApplyToDocuments("B", new[] { b }, docs => docs[b].Data["displayName"] = "keep other");
                discard.Session.ApplyToDocuments("shared", new[] { projectId }, docs => docs[projectId].Data["name"] = "keep shared");
                discard.Session.DiscardLevelChanges(a);
                var afterDiscard = new AuthoringToolWorkspace(); afterDiscard.RestoreState(discard.ExportState());
                Check((string)afterDiscard.Session.Get(a).Data["displayName"] == originalName &&
                    (string)afterDiscard.Session.Get(b).Data["displayName"] == "keep other" &&
                    (string)afterDiscard.Session.Get(projectId).Data["name"] == "keep shared", "v2 scoped discard lost unrelated work");
                int undoCount = 0;
                while (afterDiscard.Session.CanUndo)
                {
                    afterDiscard.Session.Undo(); undoCount++;
                    Check((string)afterDiscard.Session.Get(a).Data["displayName"] == originalName, "v2 Undo revived discarded level edit");
                }
                Check(undoCount == 2 && !afterDiscard.Session.IsDirty, "v2 scoped discard changed remaining history");
                while (afterDiscard.Session.CanRedo)
                {
                    afterDiscard.Session.Redo();
                    Check((string)afterDiscard.Session.Get(a).Data["displayName"] == originalName, "v2 Redo revived discarded level edit");
                }
                Check((string)afterDiscard.Session.Get(b).Data["displayName"] == "keep other" &&
                    (string)afterDiscard.Session.Get(projectId).Data["name"] == "keep shared", "v2 Redo lost unrelated work");
                File.WriteAllText(Path.Combine(owned, "result.txt"), "PASS v2 locked unread source, draft/selection/Undo/Redo, partial save, changed source rejection");
                exit = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(owned, "result.txt"), error.ToString()); UnityEngine.Debug.LogException(error); }
            finally { if (Directory.Exists(data)) Directory.Delete(data, true); EditorApplication.Exit(exit); }
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    }
}

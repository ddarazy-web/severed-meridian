using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using LevelAuthoring.Runtime;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace LevelAuthoring.Editor
{
    public static class ProjectDirectoryVerification
    {
        public static void Run()
        {
            string owned = Path.GetFullPath("Logs/ProjectDirectory/" + Guid.NewGuid().ToString("N"));
            string data = Path.Combine(owned, "data"); Directory.CreateDirectory(owned);
            int exit = 1;
            try
            {
                ContentSnapshot template = LevelToolTestProject.Create();
                ContentDocument project = template.Project;
                var entries = new JArray();
                for (int index = 0; index < 10000; index++)
                    entries.Add(new JObject { ["id"] = "entry-" + index, ["path"] = "levels/" + index + ".json" });
                project.Data["documents"] = entries;
                var files = new JsonContentStore(data); files.Save(project, "project.json", null);
                MethodInfo open = typeof(ContentSnapshotStore).GetMethod("ReadDirectory");
                Check(open != null, "Manifest-only directory API missing");
                object directory = open.Invoke(new ContentSnapshotStore(data), null);
                using var lifetime = (IDisposable)directory;
                var listed = (IEnumerable)directory.GetType().GetProperty("Entries").GetValue(directory);
                Check(listed.Cast<object>().Count() == 10000, "Directory count differs");
                Check(!Directory.Exists(Path.Combine(data, "levels")), "Listing required or created body files");
                MethodInfo browse = typeof(AuthoringToolWorkspace).GetMethod("OpenListing");
                Check(browse != null, "Workspace listing-only open missing");
                var workspace = new AuthoringToolWorkspace();
                Check((bool)browse.Invoke(workspace, new object[] { data, "Cancel", false }) && workspace.Session == null,
                    "Opening a project automatically selected or loaded a level");
                Check(workspace.Folder == data, "Listing-only workspace lost folder");
                ContentDocument level = template.Documents.First(doc => doc.Kind == "level");
                var selected = new ContentDocument("level", "entry-7", level.Data);
                ContentFile written = files.Save(selected, "levels/7.json", null);
                MethodInfo read = directory.GetType().GetMethod("ReadDocument");
                ContentDocument first = (ContentDocument)read.Invoke(directory, new object[] { "entry-7" });
                string original = (string)first.Data["displayName"];
                first.Data["displayName"] = "external query";
                selected.Data["displayName"] = "external disk";
                files.Save(selected, "levels/7.json", written.Hash);
                ContentDocument again = (ContentDocument)read.Invoke(directory, new object[] { "entry-7" });
                Check((string)again.Data["displayName"] == original, "Loaded version changed after external mutation");
                bool rejected = false;
                try { read.Invoke(directory, new object[] { "entry-8" }); }
                catch (TargetInvocationException error) { rejected = error.InnerException is FileNotFoundException; }
                Check(rejected, "Missing selected file was not rejected");
                Check((string)((ContentDocument)read.Invoke(directory, new object[] { "entry-7" })).Data["displayName"] == original,
                    "Failed selection damaged loaded version");
                Check(Directory.GetFiles(Path.Combine(data, "levels"), "*.json").Length == 1, "Unselected bodies were materialized");
                ConstructorInfo sessionConstructor = typeof(AuthoringEditSession).GetConstructor(new[] { directory.GetType(), typeof(string) });
                Check(sessionConstructor != null, "Directory-backed editing session missing");
                var session = (AuthoringEditSession)sessionConstructor.Invoke(new[] { directory, "entry-7" });
                Check(session.DocumentIds.Length == 10001 && !session.IsDirty, "Lazy session directory or save baseline differs");
                session.EditLevel("이름", body => body["displayName"] = "edited");
                Check(session.IsDirty, "Lazy session edit did not become dirty");
                session.Undo(); Check(!session.IsDirty, "Lazy Undo lost baseline");
                session.Redo(); Check((string)session.Get("entry-7").Data["displayName"] == "edited", "Lazy Redo lost edit");
                rejected = false;
                try { session.SelectLevel("entry-8"); }
                catch (FileNotFoundException) { rejected = true; }
                Check(rejected && session.SelectedLevelId == "entry-7", "Failed lazy selection changed active level");
                // 앞선 외부 수정 검사의 파일을 원래 바이트로 돌린 뒤 변경분 저장을 검사한다.
                files.Save(new ContentDocument("level", "entry-7", again.Data), "levels/7.json", files.Read("levels/7.json").Hash);
                session.Save(new ContentSnapshotStore(data));
                Check(!session.IsDirty, "Incremental save did not set baseline");
                using (var savedDirectory = (IDisposable)open.Invoke(new ContentSnapshotStore(data), null))
                {
                    var savedEntries = ((IEnumerable)savedDirectory.GetType().GetProperty("Entries").GetValue(savedDirectory)).Cast<object>().ToArray();
                    object untouched = savedEntries.Single(entry => (string)entry.GetType().GetProperty("Id").GetValue(entry) == "entry-8");
                    Check((string)untouched.GetType().GetProperty("RelativePath").GetValue(untouched) == "levels/8.json", "Unchanged file path rewritten");
                    Check((string)((ContentDocument)read.Invoke(savedDirectory, new object[] { "entry-7" })).Data["displayName"] == "edited", "Incremental save lost edit");
                }
                session.Undo(); Check(session.IsDirty, "Undo across incremental save lost old version");
                session.Redo(); Check(!session.IsDirty, "Redo across incremental save differs from saved version");
                var later = new ContentDocument("level", "entry-8", level.Data); later.Data["levelNumber"] = 2;
                files.Save(later, "levels/8.json", null);
                session.SelectLevel("entry-8");
                session.EditLevel("두 번째", body => body["displayName"] = "later edit");
                session.Save(new ContentSnapshotStore(data));
                Check(!session.IsDirty && (string)session.Get("entry-8").Data["displayName"] == "later edit", "First access after save lost source baseline");
                byte[] beforeFailure = File.ReadAllBytes(Path.Combine(data, "project.json"));
                session.EditLevel("실패 검사", body => body["displayName"] = "pending");
                var interrupted = (ContentSnapshotStore)typeof(StreamingPublicationVerification)
                    .GetMethod("Instrumented", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { data,
                        (Action<string>)(phase => { if (phase == "BeforeProject") throw new InvalidOperationException("injected"); }) });
                rejected = false;
                try { session.Save(interrupted); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && session.IsDirty && beforeFailure.SequenceEqual(File.ReadAllBytes(Path.Combine(data, "project.json"))),
                    "Failed incremental publish changed manifest or dirty state");
                session.Save(new ContentSnapshotStore(data));
                beforeFailure = File.ReadAllBytes(Path.Combine(data, "project.json"));
                session.EditLevel("없는 참조", body => body["catalogId"] = "missing-catalog");
                rejected = false;
                try { session.Save(new ContentSnapshotStore(data)); } catch (ContentFormatException) { rejected = true; }
                Check(rejected && session.IsDirty && beforeFailure.SequenceEqual(File.ReadAllBytes(Path.Combine(data, "project.json"))),
                    "Changed document with missing direct reference was saved");
                session.Undo();
                session.EditLevel("중복 번호", body => body["levelNumber"] = 1);
                rejected = false;
                try { session.Save(new ContentSnapshotStore(data)); } catch (ContentFormatException) { rejected = true; }
                Check(rejected, "Known duplicate level number was saved");
                session.Undo();
                using (var inspect = new ContentSnapshotStore(data).ReadDirectory())
                {
                    string editedPath = inspect.Entries.Single(entry => entry.Id == "entry-8").RelativePath;
                    File.AppendAllText(Path.Combine(data, editedPath), " ");
                    session.EditLevel("외부 변경 충돌", body => body["displayName"] = "conflict");
                    rejected = false;
                    try { session.Save(new ContentSnapshotStore(data)); } catch (ContentFormatException) { rejected = true; }
                    Check(rejected && session.IsDirty && beforeFailure.SequenceEqual(File.ReadAllBytes(Path.Combine(data, "project.json"))),
                        "External change was overwritten during incremental save");
                }
                string indexed = Path.Combine(data, "indexed");
                browse.Invoke(workspace, new object[] { data, "Cancel", false });
                MethodInfo openLevel = typeof(AuthoringToolWorkspace).GetMethod("OpenLevel");
                Check(openLevel != null, "Workspace explicit level editing missing");
                openLevel.Invoke(workspace, new object[] { "entry-7" });
                Check(workspace.Session.SelectedLevelId == "entry-7", "Workspace edit button target differs");
                workspace.Session.EditLevel("현재 초안", body => body["displayName"] = "keep draft");
                rejected = false;
                try { openLevel.Invoke(workspace, new object[] { "entry-8" }); }
                catch (TargetInvocationException error) { rejected = error.InnerException is ContentFormatException; }
                Check(rejected && workspace.Session.SelectedLevelId == "entry-7" && workspace.Session.IsDirty,
                    "Failed workspace level open lost draft or selection");
                var repository = new ContentSnapshotStore(indexed);
                repository.Publish(template, null);
                object indexedDirectory = open.Invoke(repository, null);
                using var indexedLifetime = (IDisposable)indexedDirectory;
                var indexedEntries = ((IEnumerable)indexedDirectory.GetType().GetProperty("Entries").GetValue(indexedDirectory)).Cast<object>().ToArray();
                PropertyInfo infoProperty = indexedEntries[0].GetType().GetProperty("Info");
                Check(infoProperty != null, "Persisted listing metadata missing");
                var levelEntry = indexedEntries.Single(entry => ((ContentDocumentInfo)infoProperty.GetValue(entry))?.Kind == "level");
                var info = (ContentDocumentInfo)infoProperty.GetValue(levelEntry);
                Check(info.Id == level.Id && info.LevelNumber == (int)level.Data["levelNumber"] && info.DisplayName == (string)level.Data["displayName"],
                    "Listing metadata differs from saved level");
                string memberPath = Path.Combine(indexed, (string)levelEntry.GetType().GetProperty("RelativePath").GetValue(levelEntry));
                using (var locked = new FileStream(memberPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                using (var reopened = (IDisposable)open.Invoke(repository, null))
                    Check(((IEnumerable)reopened.GetType().GetProperty("Entries").GetValue(reopened)).Cast<object>()
                        .Any(entry => ((ContentDocumentInfo)infoProperty.GetValue(entry))?.Id == level.Id), "Listing opened locked body");
                File.AppendAllText(memberPath, " ");
                rejected = false;
                try { read.Invoke(indexedDirectory, new object[] { level.Id }); }
                catch (TargetInvocationException error) { rejected = error.InnerException is ContentFormatException; }
                Check(rejected, "Indexed body hash change was accepted");
                string indexFile = Directory.GetFiles(Path.Combine(indexed, ".authoring", "listings"), "*.json").Single();
                File.WriteAllText(indexFile, "{broken");
                using (var locked = new FileStream(memberPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                using (var fallback = (IDisposable)open.Invoke(repository, null))
                {
                    Check(!string.IsNullOrEmpty((string)fallback.GetType().GetProperty("MetadataWarning").GetValue(fallback)), "Broken index lacks notice");
                    Check(((IEnumerable)fallback.GetType().GetProperty("Entries").GetValue(fallback)).Cast<object>()
                        .All(entry => infoProperty.GetValue(entry) == null), "Broken index retained partial metadata");
                }
                MethodInfo rebuild = typeof(ContentSnapshotStore).GetMethod("RebuildDirectoryIndex");
                Check(rebuild != null, "Explicit cancellable directory metadata rebuild missing");
                byte[] previousIndex = File.ReadAllBytes(indexFile);
                using (var cancellation = new CancellationTokenSource())
                {
                    bool cancelled = false;
                    try { rebuild.Invoke(repository, new object[] { cancellation.Token,
                        (Action<int, int>)((done, total) => { if (done == 1) cancellation.Cancel(); }) }); }
                    catch (TargetInvocationException error) { cancelled = error.InnerException is OperationCanceledException; }
                    Check(cancelled && previousIndex.SequenceEqual(File.ReadAllBytes(indexFile)), "Cancelled rebuild replaced existing metadata");
                }
                int completed = 0, expected = 0;
                using (var rebuilt = (ContentProjectDirectory)rebuild.Invoke(repository, new object[] { CancellationToken.None,
                    (Action<int, int>)((done, total) => { completed = done; expected = total; }) }))
                {
                    Check(completed == expected && expected > 0, "Metadata rebuild progress incomplete");
                    Check(rebuilt.MetadataWarning == null && rebuilt.Entries.All(entry => entry.Info != null), "Explicit rebuild left unknown metadata");
                }
                previousIndex = File.ReadAllBytes(indexFile);
                string manifestFile = Path.Combine(indexed, "project.json");
                byte[] previousManifest = File.ReadAllBytes(manifestFile);
                bool conflict = false;
                try { rebuild.Invoke(repository, new object[] { CancellationToken.None,
                    (Action<int, int>)((done, total) => { if (done == total) File.AppendAllText(manifestFile, " "); }) }); }
                catch (TargetInvocationException error) { conflict = error.InnerException is ContentFormatException; }
                Check(conflict && previousIndex.SequenceEqual(File.ReadAllBytes(indexFile)), "Concurrent project change published stale metadata");
                File.WriteAllBytes(manifestFile, previousManifest);
                MethodInfo refreshListing = typeof(AuthoringToolWorkspace).GetMethod("RebuildListing");
                Check(refreshListing != null, "Workspace metadata refresh missing");
                string refreshFolder = Path.Combine(data, "refresh");
                var fixture = new AuthoringToolWorkspace(); fixture.Create(refreshFolder, template);
                DocumentEditing.DuplicateLevel(fixture.Session, level.Id, 2, "아직 열지 않은 레벨"); fixture.Save();
                foreach (string listingFile in Directory.GetFiles(Path.Combine(refreshFolder, ".authoring", "listings"), "*.json")) File.Delete(listingFile);
                var refreshing = new AuthoringToolWorkspace(); refreshing.OpenListing(refreshFolder, "Cancel");
                refreshing.OpenLevel(level.Id);
                refreshing.Session.EditLevel("보존", body => body["displayName"] = "unsaved metadata test");
                long contentVersion = refreshing.Session.ContentVersion;
                string unknownId = refreshing.Session.DocumentIds.First(id => refreshing.Session.GetDocumentInfo(id).Kind == "unknown");
                refreshListing.Invoke(refreshing, new object[] { CancellationToken.None, null });
                Check(refreshing.Session.IsDirty && refreshing.Session.ContentVersion == contentVersion &&
                    refreshing.Session.GetDocumentInfo(level.Id).DisplayName == "unsaved metadata test", "Metadata refresh replaced current draft");
                Check(refreshing.Session.GetDocumentInfo(unknownId).Kind != "unknown", "Metadata refresh did not update unloaded reference");
                refreshing.Session.Undo();
                Check(!refreshing.Session.IsDirty, "Metadata refresh damaged Undo baseline");
                refreshing.Session.Redo();
                Check(refreshing.Session.IsDirty, "Metadata refresh damaged Redo");
                MethodInfo trial = typeof(AuthoringEditSession).GetMethod("CreateTrialSnapshot");
                Check(trial != null, "Selected-level trial snapshot API missing");
                string trialFolder = Path.Combine(data, "trial");
                var trialFixture = LevelToolDataFlowVerification.CreateFixture(trialFolder); trialFixture.Save();
                string trialLevel = trialFixture.Session.SelectedLevelId;
                using (var trialFiles = new ContentSnapshotStore(trialFolder).ReadDirectory())
                    foreach (ProjectDocumentEntry entry in trialFiles.Entries.Where(entry => entry.Info.Kind == "level" && entry.Id != trialLevel))
                        File.Delete(Path.Combine(trialFolder, entry.RelativePath));
                var trialWorkspace = new AuthoringToolWorkspace(); trialWorkspace.OpenListing(trialFolder, "Cancel"); trialWorkspace.OpenLevel(trialLevel);
                var selectedSnapshot = (ContentSnapshot)trial.Invoke(trialWorkspace.Session, new object[] { new[] { trialLevel }, null });
                Check(selectedSnapshot.Documents.Count(doc => doc.Kind == "level") == 1, "Trial snapshot included unselected levels");
                Check(JsonPuzzlePlayAdapter.CreateRequest(selectedSnapshot, trialLevel, 7) != null, "Selected trial cannot create game request");
                rejected = false;
                try { trial.Invoke(trialWorkspace.Session, new object[] { new[] { selectedSnapshot.Project.Id }, null }); }
                catch (TargetInvocationException error) { rejected = error.InnerException is ContentFormatException; }
                Check(rejected, "Project document accepted as a trial level");
                var shared = new SharedTutorialDraft(trialWorkspace.Session);
                shared.Session.ApplyToDocuments("미리보기", new[] { shared.FlowId }, docs => docs[shared.FlowId].Data["steps"][0]["instructions"] = "preview only");
                MethodInfo preview = typeof(SharedTutorialDraft).GetMethod("PreviewLevels");
                Check(preview != null, "Selected shared preview API missing");
                var previewSnapshot = (ContentSnapshot)preview.Invoke(shared, new object[] { trialWorkspace.Session, new[] { trialLevel } });
                Check((string)previewSnapshot.Get(shared.FlowId).Data["steps"][0]["instructions"] == "preview only" &&
                    (string)trialWorkspace.Session.Get(shared.FlowId).Data["steps"][0]["instructions"] != "preview only", "Shared preview changed parent or lost draft");
                trialWorkspace.Session.EditLevel("없는 흐름", body => body["tutorial"]["flowId"] = "missing-flow");
                rejected = false;
                try { trial.Invoke(trialWorkspace.Session, new object[] { new[] { trialLevel }, null }); }
                catch (TargetInvocationException error) { rejected = error.InnerException is ContentFormatException; }
                Check(rejected, "Selected trial bypassed reference validation");
                using (var zip = new MemoryStream())
                {
                    ProjectPackageStore.Export(indexed, zip, CancellationToken.None);
                    zip.Position = 0;
                    string imported = ProjectPackageStore.Import(zip, Path.Combine(data, "imports"), "listing", CancellationToken.None);
                    using var importedDirectory = new ContentSnapshotStore(imported).ReadDirectory();
                    Check(importedDirectory.MetadataWarning == null && importedDirectory.Entries.All(entry => entry.Info != null),
                        "ZIP import did not retain metadata from validation; would need another full read");
                    string lockedPath = Path.Combine(imported, importedDirectory.Entries.First().RelativePath);
                    using var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    using var listedImport = new ContentSnapshotStore(imported).ReadDirectory();
                    Check(listedImport.MetadataWarning == null, "Imported listing reopened a body");
                }
                File.WriteAllText(Path.Combine(owned, "result.txt"), "PASS 10000 manifest entries without bodies; selected read, isolation, failed selection preservation; explicit metadata rebuild, cancellation and manifest conflict");
                exit = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(owned, "result.txt"), error.ToString()); UnityEngine.Debug.LogException(error); }
            finally { if (Directory.Exists(data)) Directory.Delete(data, true); EditorApplication.Exit(exit); }
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    }
}

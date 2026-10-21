using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;
using LevelTool;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelBrowserLazyDisplayVerification
    {
        public static void Run()
        {
            string owned = Path.GetFullPath("Logs/LevelBrowserLazyDisplay/" + Guid.NewGuid().ToString("N"));
            string data = Path.Combine(owned, "data");
            Directory.CreateDirectory(owned);
            GameObject host = null;
            int exit = 1;
            try
            {
                host = new GameObject("isolated-display-test"); host.SetActive(false);
                var screen = host.AddComponent<LevelToolScreen>();
                screen.Workspace.Create(data, LevelToolDocuments.NewProject());
                AuthoringEditSession session = screen.Workspace.Session;
                ContentDocument original = session.Get(session.SelectedLevelId);
                for (int number = 2; number <= 50; number++)
                {
                    string id = "display-level-" + number;
                    var body = original.Data.DeepClone(); body["levelNumber"] = number; body["displayName"] = "Sample " + number;
                    session.ApplyToDocuments("add", new[] { id }, docs => docs.Add(id, new ContentDocument("level", id, (Newtonsoft.Json.Linq.JObject)body)));
                }
                Prepare(screen);
                var cached = (Dictionary<string, ContentDocument>)Field(screen, "displayDocuments");
                Check(cached.Values.Count(doc => doc.Kind == "level") == 1, "Display retained unselected level bodies");
                var index = (LevelBrowserIndex)Field(screen, "levelIndex");
                Check(index.Count == 50 && index.Search("50").Single().Name == "Sample 50", "Metadata index lost levels");
                int reads = (int)Field(screen, "displayDocumentReadCount");
                session.SelectCells(new[] { 40 }); Prepare(screen);
                Check((int)Field(screen, "displayDocumentReadCount") == reads, "Selection reread bodies");
                session.SelectLevel("display-level-50"); Prepare(screen);
                Check((int)Field(screen, "displayDocumentReadCount") == reads + 1, "Opening a level read unrelated bodies");
                Check(cached.Values.Count(doc => doc.Kind == "level") == 1 && cached.ContainsKey("display-level-50"), "Previous level body retained");
                session.EditLevel("rename", body => body["displayName"] = "Updated"); Prepare(screen);
                Check(index.Search("50").Single().Name == "Updated", "Metadata edit not reflected");
                session.Undo(); Prepare(screen);
                Check(index.Search("50").Single().Name == "Sample 50", "Metadata Undo not reflected");
                string listingPath = Path.Combine(data, "listing");
                var files = new JsonContentStore(listingPath);
                ContentDocument project = session.Documents.Single(doc => doc.Kind == "project");
                var entries = new JArray();
                for (int number = 0; number < 10000; number++)
                    entries.Add(new JObject { ["id"] = "unread-" + number, ["path"] = "levels/" + number + ".json" });
                project.Data["documents"] = entries; files.Save(project, "project.json", null);
                screen.Workspace.OpenListing(listingPath, "Discard"); Prepare(screen);
                index = (LevelBrowserIndex)Field(screen, "levelIndex");
                Check(screen.Workspace.Session == null && index.Count == 10000, "Listing-only screen did not show unknown files");
                Check(!Directory.Exists(Path.Combine(listingPath, "levels")), "Listing display touched missing bodies");
                files.Save(new ContentDocument("level", "unread-7", original.Data), "levels/7.json", null);
                screen.Workspace.OpenLevel("unread-7"); Prepare(screen);
                index = (LevelBrowserIndex)Field(screen, "levelIndex");
                Check(index.Count == 10000 && index.Get("unread-7").Number == (int)original.Data["levelNumber"], "Selected metadata was not refreshed");
                cached = (Dictionary<string, ContentDocument>)Field(screen, "displayDocuments");
                Check(cached.Values.Count(doc => doc.Kind == "level") == 1 && cached.Values.All(doc => doc.Kind != "unknown"), "Display loaded unselected unknown files");
                MethodInfo trial = typeof(LevelToolScreen).GetMethod("CreateTrialSnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(trial != null, "Screen still lacks selected-level trial boundary");
                var trialSnapshot = (ContentSnapshot)trial.Invoke(screen, new object[] { null });
                Check(trialSnapshot.Documents.Count(doc => doc.Kind == "level") == 1 && trialSnapshot.DocumentIds.Contains("unread-7"),
                    "Screen trial expanded unrelated levels");
                var status = new Label();
                typeof(LevelToolScreen).GetField("status", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(screen, status);
                screen.Workspace.Session.EditLevel("unsaved", body => body["displayName"] = "Keep draft");
                typeof(LevelToolScreen).GetMethod("ValidateLevel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(screen, new object[] { false });
                Check(status.text.Contains("검사 실패"), "Unread missing file was not reported as validation failure");
                Check(Field(screen, "validatedDocuments") == null, "Failed read retained a completed validation snapshot");
                Check(screen.Workspace.Session.Get("unread-7").Data["displayName"].Value<string>() == "Keep draft" && screen.Workspace.Session.IsDirty,
                    "Failed validation discarded pending edits");
                File.WriteAllText(Path.Combine(owned, "result.txt"), "PASS selected-only cache, all metadata, selection reuse, level switch, rename/Undo");
                exit = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(owned, "result.txt"), error.ToString()); Debug.LogException(error); }
            finally
            {
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (Directory.Exists(data)) Directory.Delete(data, true);
                EditorApplication.Exit(exit);
            }
        }
        private static object Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private static void Prepare(LevelToolScreen screen) => typeof(LevelToolScreen).GetMethod("PrepareDisplay", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, null);
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    }
}

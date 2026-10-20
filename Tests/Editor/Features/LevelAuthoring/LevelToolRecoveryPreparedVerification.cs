using System;
using System.IO;
using System.Reflection;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using LevelAuthoring.Runtime;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class LevelToolRecoveryPreparedVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        public static void Run()
        {
            string root = Path.GetFullPath("Logs/LevelToolRecoveryPerformance/prepared-" + Guid.NewGuid().ToString("N"));
            try
            {
                var source = new AuthoringToolWorkspace(); source.Create(Path.Combine(root, "project"), LevelToolDocuments.NewProject());
                source.Session.EditLevel("이름", level => LevelDocumentEditing.SetName(level, "첫 변경"));
                source.Session.EditLevel("이름", level => LevelDocumentEditing.SetName(level, "둘째 변경"));
                source.Session.Undo(); source.Session.SelectCells(new[] { 1, 2 });
                string expected = source.ExportState();
                var store = new ToolDraftStore(root);
                store.Save(expected, new JObject { ["layer"] = "Block" }, 1);
                MethodInfo read = typeof(ToolDraftStore).GetMethod("ReadPrepared", Flags);
                Check(read != null, "prepared recovery read boundary exists");
                object prepared = read.Invoke(store, null);
                Type type = prepared.GetType();
                JObject view = (JObject)type.GetProperty("View", Flags).GetValue(prepared);
                view["layer"] = "mutated";
                Check((string)((JObject)type.GetProperty("View", Flags).GetValue(prepared))["layer"] == "Block", "view getter isolates mutable JSON");
                var target = new AuthoringToolWorkspace();
                MethodInfo apply = type.GetMethod("ApplyTo", Flags);
                apply.Invoke(prepared, new object[] { target, false });
                Check(target.ExportState() == expected, "prepared apply retains all history and selection");
                source.Session.Redo(); target.Session.Redo();
                Check(source.ExportState() == target.ExportState(), "prepared Redo retains meaning");
                source.Session.Undo(); target.Session.Undo();
                Check(source.ExportState() == target.ExportState(), "prepared Undo retains meaning");
                bool consumed = false;
                try { apply.Invoke(prepared, new object[] { new AuthoringToolWorkspace(), false }); }
                catch (TargetInvocationException error) { consumed = error.InnerException is InvalidOperationException; }
                Check(consumed, "prepared ownership is single use");
                string first = File.ReadAllText(Path.Combine(root, "draft.json"));
                store.Save(expected, new JObject { ["layer"] = "Cover" }, 2);
                File.WriteAllText(Path.Combine(root, "draft.json"), "broken");
                object fallback = read.Invoke(store, null);
                Check(store.ReadWarning != null, "prepared read reports fallback");
                var recovered = new AuthoringToolWorkspace(); apply.Invoke(fallback, new object[] { recovered, false });
                Check(recovered.ExportState() == expected, "previous valid record restored");
                store.Save(expected, new JObject(), 3);
                Check(File.ReadAllText(Path.Combine(root, "draft.json.previous")) == first, "corrupt current never replaces valid previous");
                string latest = File.ReadAllText(Path.Combine(root, "draft.json"));
                store.Save(expected, new JObject { ["layer"] = "stale" }, 2);
                Check(File.ReadAllText(Path.Combine(root, "draft.json")) == latest, "stale sequence cannot overwrite latest");
                // 서로 다른 손상 위치가 모두 같은 기존 기록을 보존해야 한다.
                Action<JObject>[] corruptions = {
                    value => value["version"] = 999,
                    value => value["session"]["current"]["levelId"] = "missing",
                    value => value["session"]["current"]["cells"] = new JArray(81),
                    value => ((JArray)value["session"]["current"]["documents"]).Add(value["session"]["current"]["documents"][0].DeepClone()),
                    value => value["session"]["undo"][0]["cells"] = new JArray(1, 1),
                    value => value["session"]["savedLevelId"] = "missing"
                };
                foreach (Action<JObject> corrupt in corruptions)
                {
                    JObject invalid = JObject.Parse(expected); corrupt(invalid);
                    bool rejected = false;
                    try { store.Save(invalid.ToString(), new JObject(), 4); }
                    catch (Exception) { rejected = true; }
                    Check(rejected && File.ReadAllText(Path.Combine(root, "draft.json")) == latest,
                        "invalid workspace rejected without changing valid checkpoint");
                }
                foreach (JObject invalidView in new[] { new JObject { ["cellSize"] = "bad" },
                    new JObject { ["sharedTutorialDraft"] = "broken" }, new JObject { ["pendingInputs"] = new JArray(1) } })
                {
                    bool rejected = false;
                    try { store.Save(expected, invalidView, 4); }
                    catch (Exception) { rejected = true; }
                    Check(rejected && File.ReadAllText(Path.Combine(root, "draft.json")) == latest,
                        "invalid view/shared draft rejected without changing valid checkpoint");
                }
                object blocked = read.Invoke(store, null);
                var untouched = new AuthoringToolWorkspace(); untouched.RestoreState(expected);
                AuthoringEditSession identity = untouched.Session;
                string projectFile = Path.Combine(source.Folder, "project.json");
                File.Move(projectFile, projectFile + ".test-backup");
                try
                {
                    bool failed = false;
                    try { apply.Invoke(blocked, new object[] { untouched, true }); }
                    catch (TargetInvocationException) { failed = true; }
                    Check(failed && ReferenceEquals(identity, untouched.Session) && untouched.ExportState() == expected,
                        "discard read failure keeps target session and full state");
                }
                finally { File.Move(projectFile + ".test-backup", projectFile); }
                apply.Invoke(blocked, new object[] { untouched, true });
                Check(!untouched.Session.IsDirty && !untouched.Session.CanUndo, "discard restores saved state after recoverable failure");
                // 실제 파일 잠금으로 교체 실패를 만들고 정상 기록과 임시 파일 정리를 검사한다.
                bool lockedFailure = false;
                using (FileStream locked = new FileStream(Path.Combine(root, "draft.json"), FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    try { store.Save(expected, new JObject(), 4); }
                    catch (IOException) { lockedFailure = true; }
                }
                Check(lockedFailure && File.ReadAllText(Path.Combine(root, "draft.json")) == latest &&
                    File.ReadAllText(Path.Combine(root, "draft.json.previous")) == first &&
                    Directory.GetFiles(root, "draft.json.pending-*").Length == 0,
                    "replace failure preserves current and previous and cleans owned temporary");
                var sharedWorkspace = LevelToolDataFlowVerification.CreateFixture(Path.Combine(root, "shared-project"));
                var shared = new LevelAuthoring.Runtime.SharedTutorialDraft(sharedWorkspace.Session);
                TutorialDraftEditing.EditFlow(shared.Session, shared.FlowId, "사본 변경", flow => flow.steps[0].instructions = "보존할 안내");
                string sharedText = shared.ExportState();
                var sharedStore = new ToolDraftStore(Path.Combine(root, "shared-draft"));
                sharedStore.Save(sharedWorkspace.ExportState(), new JObject { ["sharedTutorialDraft"] = sharedText }, 1);
                object sharedPrepared = read.Invoke(sharedStore, null);
                JObject sharedView = (JObject)type.GetProperty("View", Flags).GetValue(sharedPrepared);
                var restoredShared = LevelAuthoring.Runtime.SharedTutorialDraft.Restore((string)sharedView["sharedTutorialDraft"]);
                var sharedTarget = new AuthoringToolWorkspace(); apply.Invoke(sharedPrepared, new object[] { sharedTarget, false });
                Check(restoredShared.ExportState() == sharedText && sharedTarget.ExportState() == sharedWorkspace.ExportState(),
                    "shared draft and parent histories survive prepared recovery independently");
                restoredShared.Apply(sharedTarget.Session);
                Check((string)sharedTarget.Session.Get(shared.FlowId).Data["steps"][0]["instructions"] == "보존할 안내",
                    "restored shared draft still validates parent and applies");
                string matrixFolder = Path.Combine(root, "damage-matrix");
                var matrix = new ToolDraftStore(matrixFolder);
                Check(matrix.Read() == null && matrix.ReadPrevious() == null, "missing records return no recovery");
                Directory.CreateDirectory(matrixFolder);
                string currentPath = Path.Combine(matrixFolder, "draft.json"), previousPath = currentPath + ".previous";
                File.WriteAllText(previousPath, first);
                Check((string)matrix.Read()["workspace"] == expected && matrix.ReadWarning != null,
                    "missing current falls back to valid previous");
                File.WriteAllText(currentPath, first); File.WriteAllText(previousPath, "broken previous");
                Check((string)matrix.Read()["workspace"] == expected, "valid current ignores broken previous");
                File.WriteAllText(currentPath, "broken current");
                bool bothRejected = false;
                try { matrix.Read(); } catch (ContentFormatException) { bothRejected = true; }
                Check(bothRejected && File.ReadAllText(currentPath) == "broken current" && File.ReadAllText(previousPath) == "broken previous",
                    "both corrupt records remain untouched on rejection");
                var ordered = new ToolDraftStore(Path.Combine(root, "reordered"));
                string older = source.ExportState(); source.Session.Redo(); string newer = source.ExportState(); source.Session.Undo();
                using (var release = new System.Threading.ManualResetEventSlim(false))
                {
                    var delayed = System.Threading.Tasks.Task.Run(() => { release.Wait(); ordered.Save(older, new JObject(), 1); });
                    try { ordered.Save(newer, new JObject(), 2); }
                    finally { release.Set(); }
                    delayed.GetAwaiter().GetResult();
                }
                Check((string)ordered.Read()["workspace"] == newer, "delayed older save cannot replace latest redo state");
                Debug.Log("PASS prepared recovery ownership and fallback: " + root); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}

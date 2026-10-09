using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Storage;
using LevelTool;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolStorageUIExercise
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static LevelToolScreen screen;
        private static VisualElement Root => screen.GetComponent<UIDocument>().rootVisualElement;
        internal static async UniTask Run(LevelToolScreen target)
        {
            screen = target;
            string root = Path.GetFullPath("ContentData/Trials/game-authoring-stage-03/tool-ui-" + Guid.NewGuid().ToString("N"));
            var store = new ContentSnapshotStore(root);
            store.Publish(screen.Workspace.Session.CreateSnapshot(), null);
            var previous = screen.Workspace.Session;
            Folder("open", root); Click("cancel");
            Check(ReferenceEquals(previous, screen.Workspace.Session) && previous.IsDirty, "UI cancel folder switch keeps dirty draft");
            Folder("open", root); Click("discard-and-continue"); await Idle();
            Check(screen.Workspace.Folder == root && !screen.Workspace.Session.IsDirty, "UI discard opens selected folder");
            Name("저장 검사"); Click("save"); await Idle();
            string id = screen.Workspace.Session.SelectedLevelId;
            Check((string)store.Read().Snapshot.Get(id).Data["displayName"] == "저장 검사", "UI save writes current JSON");
            Name("버릴 수정"); Click("reload"); Click("cancel");
            Check(screen.Workspace.Session.IsDirty, "UI reload cancel keeps draft");
            Click("reload"); Click("discard-and-continue"); await Idle();
            Check(!screen.Workspace.Session.IsDirty && (string)screen.Workspace.Session.Get(id).Data["displayName"] == "저장 검사", "UI discard reload restores disk");
            Name("저장 후 재열기"); Click("reload"); Click("save-and-continue"); await Idle();
            Check(!screen.Workspace.Session.IsDirty && (string)screen.Workspace.Session.Get(id).Data["displayName"] == "저장 후 재열기", "UI save and reload same folder");
            var disk = store.Read(); store.Publish(disk.Snapshot, disk.Hash);
            Name("외부 충돌 초안"); string state = screen.Workspace.Session.ExportState(); Click("save"); await Idle();
            Check(screen.Workspace.Session.ExportState() == state && Root.Q<Label>("status").text.Contains("외부"), "UI conflict displays cause and keeps draft");
            Folder("save-copy", root + "-copy"); await Idle();
            Check(screen.Workspace.Folder == root + "-copy" && !screen.Workspace.Session.IsDirty, "UI save copy changes target on success");
            Folder("backup", root); await Idle();
            Check(screen.Workspace.IsRecovery, "UI opens protected previous good snapshot");
            Click("save"); await Idle(); Check(Root.Q<Label>("status").text.Contains("복구본"), "UI recovered source cannot overwrite");
            string sourceHash = store.Read().Hash;
            Folder("save-copy", root + "-recovered"); await Idle();
            Check(!screen.Workspace.IsRecovery && store.Read().Hash == sourceHash, "UI backup copy leaves source unchanged");
            Folder("new-project", root + "-new"); await Idle();
            Check(screen.Workspace.Folder == root + "-new" && screen.Workspace.Session.Documents.Count(doc => doc.Kind == "level") == 1,
                "UI new project from existing project");
            screen = null;
        }
        private static void Name(string text)
        {
            TextField field = Root.Query<TextField>().ToList().First(value => value.label == "이름"); field.value = text;
        }
        private static void Folder(string action, string path)
        {
            Click(action); Root.Q<TextField>("folder-path").value = path; Click("choose-folder");
        }
        private static void Click(string name)
        {
            string command = name == "open" ? "Open" : name == "reload" ? "Reload" : name == "save-copy" ? "SaveCopy" : name == "backup" ? "Backup" : name == "new-project" ? "NewProject" : null;
            if (command != null)
            {
                Click("menu-file"); name = "command-" + command;
            }
            Button button = Root.Q<Button>(name) ?? throw new Exception("Missing button " + name);
            typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
        }
        private static async UniTask Idle()
        {
            await UniTask.WaitUntil(() => !(bool)typeof(LevelToolScreen).GetField("busy", Flags).GetValue(screen));
            await UniTask.Yield();
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}

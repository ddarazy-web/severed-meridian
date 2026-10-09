using System;
using System.IO;
using System.Reflection;
using LevelTool;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolRecordsExercise
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static void Run(LevelToolScreen screen)
        {
            var type = typeof(LevelToolScreen);
            string trials = (string)type.GetProperty("BatchDirectory", Flags).GetValue(screen);
            string multi = (string)type.GetProperty("MultiDirectory", Flags).GetValue(screen);
            string recovery = (string)type.GetField("recoveryDirectory", Flags).GetValue(screen);
            Check(!string.IsNullOrEmpty(recovery) && Path.GetFullPath(recovery).StartsWith(
                Path.GetFullPath("Logs/GameAuthoringStage05/advanced-"), StringComparison.OrdinalIgnoreCase), "record test owns isolated recovery directory");
            string repeatId = Guid.NewGuid().ToString("N"), multiId = Guid.NewGuid().ToString("N");
            string repeat = Path.Combine(trials, repeatId), queue = Path.Combine(multi, multiId);
            Header(repeat, "batch.json", repeatId);
            File.WriteAllText(Path.Combine(repeat, "000000.json"), "{}");
            File.WriteAllText(Path.Combine(repeat, "source.context"), "isolated source");
            Header(queue, "queue.json", multiId);
            string level = Path.Combine(queue, "0000"); Directory.CreateDirectory(level);
            File.WriteAllText(Path.Combine(level, "source.context"), "isolated level source");
            string balanceId = Guid.NewGuid().ToString("N"), balance = Path.Combine(level, balanceId);
            Header(balance, "balance.json", balanceId);
            File.WriteAllText(Path.Combine(balance, "source.context"), "isolated balance source");
            string archive = Path.Combine(recovery, "separate-archive.txt"); File.WriteAllText(archive, "preserve");
            string before = screen.Workspace.Session.ExportState();
            type.GetField("inspectorPage", Flags).SetValue(screen, "자동 시험");
            type.GetMethod("Refresh", Flags).Invoke(screen, null);
            VisualElement root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Click(string name)
            {
                Button button = root.Q<Button>(name) ?? throw new Exception("record control missing: " + name);
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
            }
            void Tick()
            {
                var advance = type.GetMethod("AdvanceToolRecords", Flags) ?? throw new Exception("record update missing");
                for (int i = 0; i < 1000; i++) advance.Invoke(screen, null);
            }
            Click("records-count"); Tick();
            Check(File.Exists(Path.Combine(repeat, "source.context")) && File.Exists(Path.Combine(balance, "balance.json")), "count never deletes records");
            Check(root.Q<Label>("records-management-status").text.Contains("1"), "record count displayed");
            Click("records-delete-all"); Tick();
            Check(root.Q<Button>("records-confirm-delete") != null, "delete requires explicit runtime confirmation");
            Click("records-cancel-delete");
            Check(File.Exists(Path.Combine(repeat, "batch.json")) && Directory.Exists(queue), "confirmation cancel preserves records");
            Click("records-delete-all"); Tick();
            type.GetMethod("StartToolBatch", Flags).Invoke(screen, new object[] { false });
            Check(type.GetField("toolBatch", Flags).GetValue(screen) == null, "pending cleanup prevents competing trial start");
            Click("records-confirm-delete"); Tick();
            Check(!Directory.Exists(repeat) && !Directory.Exists(queue), "confirmed cleanup removes JSON sidecars and nested recommendation records");
            Check(File.ReadAllText(archive) == "preserve", "separate archive remains intact");
            Check(screen.Workspace.Session.ExportState() == before, "cleanup preserves authoring document selection and undo history");
            Header(repeat, "batch.json", repeatId);
            File.WriteAllText(Path.Combine(repeat, "unknown.txt"), "preserve");
            Click("records-delete-all"); Tick(); Click("records-confirm-delete"); Tick();
            Check(File.ReadAllText(Path.Combine(repeat, "unknown.txt")) == "preserve" && File.Exists(Path.Combine(repeat, "batch.json")), "unknown file and failed record header preserved");
            Check(root.Q<Label>("records-management-status").text.Contains("실패"), "partial cleanup failure displayed");
            type.GetMethod("StartToolBatch", Flags).Invoke(screen, new object[] { false });
            var active = (AutoPlay.PersistedBotBatch)type.GetField("toolBatch", Flags).GetValue(screen);
            Check(active != null, "record lock test starts a real batch");
            active.Pause(); Click("records-delete-all"); Tick();
            Check(type.GetField("recordCleanup", Flags).GetValue(screen) == null && root.Q<Button>("records-confirm-delete") == null,
                "active or paused batch prevents cleanup confirmation");
            type.GetMethod("DrainTrialRecords", Flags).Invoke(screen, null);
        }
        private static void Header(string folder, string name, string id)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, name), "{\"id\":\"" + id + "\",\"version\":1,\"formatVersion\":1,\"startedUtc\":\"2026-10-09T00:00:00Z\"}");
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}

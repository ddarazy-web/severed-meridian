using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolHistoryExercise
    {
        internal static void Run(LevelToolScreen screen)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Click(string name) => typeof(Clickable).GetMethod("Invoke", flags).Invoke(root.Q<Button>(name).clickable, new object[] { null });
            void Tick() => typeof(LevelToolScreen).GetMethod("AdvanceToolHistory", flags).Invoke(screen, null);
            BotAnalysisReader Reader() => (BotAnalysisReader)typeof(LevelToolScreen).GetField("historyReader", flags).GetValue(screen);
            BotRecordReplay Replay() => (BotRecordReplay)typeof(LevelToolScreen).GetField("historyReplay", flags).GetValue(screen);
            void Open(string path)
            {
                typeof(LevelToolScreen).GetMethod("OpenHistory", flags).Invoke(screen, new object[] { path });
                for (int i = 0; i < 20; i++) Tick();
            }
            string state = screen.Workspace.Session.ExportState();
            var original = Reader(); string path = original.DirectoryPath;
            string headerText = File.ReadAllText(Path.Combine(path, "batch.json"));
            var outcome = root.Q<DropdownField>("history-outcome");
            foreach (string choice in new[] { "성공", "실패", "오류", "중단" })
            {
                outcome.value = choice;
                int expected = original.Games.Count(game => choice == "성공" ? game.Outcome == BotSessionStatus.Won : choice == "실패" ?
                    game.Outcome == BotSessionStatus.MovesExhausted || game.Outcome == BotSessionStatus.Blocked : choice == "오류" ? game.Outcome == BotSessionStatus.Error : game.Outcome == BotSessionStatus.Stopped);
                Check(root.Query<Button>().ToList().Count(button => button.name.StartsWith("history-game-", StringComparison.Ordinal)) == expected,
                    "outcome filter displays recorded cases: " + choice);
            }
            outcome.value = "전체";
            root.Q<TextField>("history-destination").value = path; Click("history-export");
            Check(root.Q<Label>("status").text.Contains("실패") && File.ReadAllText(Path.Combine(path, "batch.json")) == headerText, "archive rejects source overwrite with visible cause");
            string exported = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(path)), "history-ux-" + Guid.NewGuid().ToString("N"));
            root.Q<TextField>("history-destination").value = exported; Click("history-export");
            for (int i = 0; i < 20; i++) Tick();
            Check(File.Exists(Path.Combine(exported, "source.context")) && root.Q<Label>("status").text.Contains("보관 완료"), "UI archive completes with independent source");
            Open(exported);
            Check(!root.Q<Label>("history-case-detail").text.Contains("난수"), "opening another history clears previous selected case");
            Check(root.Q<Label>("history-comparison").text.Contains("같은 규칙/전략"), "archived equivalent record compares on common seeds");
            var header = JObject.Parse(File.ReadAllText(Path.Combine(exported, "batch.json")));
            header["engineVersion"] = "verification-older-rules"; File.WriteAllText(Path.Combine(exported, "batch.json"), header.ToString());
            Open(exported);
            Check(Reader().Error == null && root.Q<Label>("history-comparison").text.Contains("쌍 비교 제외") &&
                root.Q<Label>("history-summary").text.Contains("버전"), "different rule version retains statistics but holds comparison and grade");
            Click("history-game-0");
            Check(Replay() == null && root.Q<Label>("status").text.Contains("규칙"), "incompatible recorded actions cannot replay");
            header["formatVersion"] = 99; File.WriteAllText(Path.Combine(exported, "batch.json"), header.ToString());
            Open(exported);
            Check(root.Q<Label>("status").text.Contains("이력 열기 실패"), "unsupported history reports failure instead of empty success");
            Check(File.ReadAllText(Path.Combine(path, "batch.json")) == headerText && screen.Workspace.Session.ExportState() == state,
                "history failure and version checks preserve source files and authoring state");
            Open(path); Click("history-game-0"); Click("history-replay-all");
            int count = 0; while (Replay().NeedsAdvance && count++ < 50000) Tick();
            Check(Replay().Status == BotReplayStatus.Completed, "original record still replays after failed history operations");
        }
        private static void Check(bool value, string text) { if (!value) throw new Exception("FAIL " + text); Debug.Log("PASS " + text); }
    }
}

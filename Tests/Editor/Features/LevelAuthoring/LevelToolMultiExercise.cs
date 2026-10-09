using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using AutoPlay;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolMultiExercise
    {
        internal static void Run(LevelToolScreen screen)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Click(string name)
            {
                var button = root.Q<Button>(name) ?? throw new Exception("Missing " + name);
                typeof(Clickable).GetMethod("Invoke", flags).Invoke(button.clickable, new object[] { null });
            }
            void Advance() => typeof(LevelToolScreen).GetMethod("AdvanceToolMulti", flags).Invoke(screen, null);
            PersistedMultiLevelTest Run() => (PersistedMultiLevelTest)typeof(LevelToolScreen).GetField("toolMulti", flags).GetValue(screen);
            void Drain(bool savingOnly = false)
            {
                var deadline = DateTime.UtcNow.AddSeconds(60);
                while (savingOnly ? Run().IsSaving : Run().HasWork)
                {
                    if (DateTime.UtcNow > deadline) throw new Exception("multi UI timeout");
                    Advance(); if (Run().IsSaving) Thread.Sleep(1);
                }
            }
            Click("new-level");
            var session = screen.Workspace.Session;
            string first = session.SelectedLevelId;
            session.Apply("short multi trial", docs => {
                docs[first].Data["moveCount"] = 1;
                docs[first].Data["missions"] = new JArray(new JObject { ["kind"] = "Color", ["color"] = "Type1", ["count"] = 99 });
            });
            Click("duplicate"); string second = session.SelectedLevelId;
            root.Q<DropdownField>("inspector-page").value = "자동 시험";
            root.Q<Toggle>("multi-level-" + first).value = true;
            root.Q<Toggle>("multi-level-" + second).value = true;
            root.Q<IntegerField>("multi-samples").value = 1;
            string before = session.ExportState();
            Click("multi-start");
            Check(Run().IsSaving && !root.Q<Button>("batch-new").enabledSelf && !root.Q<Button>("bot-new").enabledSelf, "multi gates other trials during initial write");
            Drain(true); Click("multi-pause"); Drain(true);
            Check(Run().Paused && Run().Current == null, "multi UI pause before first game");
            Click("multi-resume"); Drain();
            Check(Run().PersistenceError == null && Run().Record.entries.All(e => e.status == MultiLevelTestStatus.Completed), "two JSON levels complete sequentially");
            Check(Run().Record.entries.Select(e => e.sourceDocumentId).SequenceEqual(new[] { first, second }), "multi records stable JSON document IDs");
            Check(session.ExportState() == before, "multi trial preserves authoring history and content");
            var completed = Run().Record;
            Click("multi-result-" + completed.id + "-0");
            var reader = (BotAnalysisReader)typeof(LevelToolScreen).GetField("historyReader", flags).GetValue(screen);
            while (!reader.IsDone) reader.Advance();
            Check(reader.Error == null && reader.Games.Count == 2, "multi result uses common result viewer and disk JSON source");
            var source = LevelAuthoring.Storage.AuthoringTrialSource.Decode(reader.SourceContext);
            Check(source.LevelId == first, "result context resolves selected queue level");
            Click("multi-history-refresh");
            int steps = 0;
            while (typeof(LevelToolScreen).GetField("multiScan", flags).GetValue(screen) != null && steps++ < 100) Advance();
            Check(root.Query<Button>("multi-result-" + completed.id + "-0").ToList().Count == 2, "saved multi queue reloads beside current result");

            root.Q<Toggle>("multi-level-" + second).value = false;
            var mode = root.Q<DropdownField>("multi-mode"); mode.value = mode.choices[1];
            Check(!root.Q<IntegerField>("multi-samples").enabledSelf, "balance fixed sample count explained by disabled repeat input");
            Click("multi-start"); Drain(true); Advance(); Drain(true);
            Check(!string.IsNullOrEmpty(Run().Record.entries[0].resultId), "balance session begins with independent source");
            Click("multi-stop"); Drain();
            Click("multi-result-" + Run().Record.id + "-0");
            Check(root.Q<Label>("balance-status").text.Contains("0/100"), "stopped balance shows measured range count without invented recommendation");
            Check(session.ExportState() == before, "balance stop and result lookup preserve edited JSON");
        }
        private static void Check(bool value, string text) { if (!value) throw new Exception("FAIL " + text); Debug.Log("PASS " + text); }
    }
}

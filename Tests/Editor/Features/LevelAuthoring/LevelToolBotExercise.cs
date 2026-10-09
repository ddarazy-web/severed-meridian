using System;
using System.Linq;
using System.Reflection;
using AutoPlay;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolBotExercise
    {
        internal static void Run(LevelToolScreen screen)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Click(string name)
            {
                var button = root.Q<Button>(name) ?? throw new Exception("Missing bot button " + name);
                typeof(Clickable).GetMethod("Invoke", flags).Invoke(button.clickable, new object[] { null });
            }
            Click("new-level");
            var session = screen.Workspace.Session;
            session.Apply("short bot trial", docs => {
                var data = docs[session.SelectedLevelId].Data; data["moveCount"] = 3;
                data["missions"] = new JArray(new JObject { ["kind"] = "Color", ["color"] = "Type1", ["count"] = 99 });
            });
            typeof(LevelToolScreen).GetMethod("Refresh", flags).Invoke(screen, null);
            var page = root.Q<DropdownField>("inspector-page");
            Check(page.choices.Contains("자동 시험"), "runtime bot page exists"); page.value = "자동 시험";
            Check(root.Q<Button>("multi-start") != null, "multiple level trial controls exist");
            string before = session.ExportState();
            Click("bot-new"); Click("bot-step");
            BotPlaySession Bot() => (BotPlaySession)typeof(LevelToolScreen).GetField("toolBot", flags).GetValue(screen);
            void Finish()
            {
                int count = 0;
                while (Bot().NeedsAdvance && count++ < 50000) typeof(LevelToolScreen).GetMethod("AdvanceToolBot", flags).Invoke(screen, null);
                Check(!Bot().NeedsAdvance && Bot().Status != BotSessionStatus.Error, "bot reaches stable boundary: " + Bot().Message);
            }
            Finish(); Check(Bot().Records.Count == 1, "single step ends after one settled action");
            Check(root.Q("bot-trial-board")?.Query<Label>(className: "trial-cell").ToList().Count == Bot().State.Rows * Bot().State.Columns, "bot snapshot renders every runtime cell");
            string first = Bot().Records[0].Choice.Action.First.ToString();
            int originalSeed = Bot().Seed;
            Click("bot-repeat"); Click("bot-step"); Finish();
            Check(Bot().Seed == originalSeed && Bot().Records[0].Choice.Action.First.ToString() == first, "same snapshot seed and strategy reproduce first action");
            Click("bot-run"); Finish();
            Check(Bot().Status == BotSessionStatus.MovesExhausted || Bot().Status == BotSessionStatus.Won || Bot().Status == BotSessionStatus.Blocked, "whole game produces explicit terminal result");
            Check(session.ExportState() == before, "bot runs preserve authoring content selection and history");
            Click("bot-repeat"); Click("bot-run"); Click("bot-stop"); Finish();
            Check(session.ExportState() == before, "stop preserves authoring state");
            Check(root.Q<IntegerField>("batch-count") != null, "batch UI controls exist");
            root.Q<IntegerField>("batch-count").value = 1;
            Click("batch-new");
            PersistedBotBatch Batch() => (PersistedBotBatch)typeof(LevelToolScreen).GetField("toolBatch", flags).GetValue(screen);
            void TickBatch() => typeof(LevelToolScreen).GetMethod("AdvanceToolBatch", flags).Invoke(screen, null);
            int ticks = 0;
            while (Batch().IsSaving && ticks++ < 10000) { TickBatch(); System.Threading.Thread.Sleep(1); }
            Click("batch-pause");
            while (Batch().IsSaving && ticks++ < 20000) { TickBatch(); System.Threading.Thread.Sleep(1); }
            Check(Batch().Record.status == BotBatchStatus.Paused, "batch UI pause checkpoint");
            Click("batch-resume");
            while (Batch().HasWork && ticks++ < 50000) { TickBatch(); if (Batch().IsSaving) System.Threading.Thread.Sleep(1); }
            Check(Batch().Record.status == BotBatchStatus.Completed && !Batch().IsSaving && Batch().Games.Count == 2, "batch UI completes both strategies durably");
            Check(session.ExportState() == before, "batch UI preserves full edit session");
            string directory = (string)typeof(LevelToolScreen).GetProperty("BatchDirectory", flags).GetValue(screen);
            Click("history-refresh");
            void TickHistory() => typeof(LevelToolScreen).GetMethod("AdvanceToolHistory", flags).Invoke(screen, null);
            for (int i = 0; i < 20; i++) TickHistory();
            Click("history-open-" + Batch().Record.id);
            for (int i = 0; i < 20; i++) TickHistory();
            Check(root.Q<Label>("history-summary").text.Contains("정상 종료"), "stored results show shared statistics");
            var filter = root.Q<DropdownField>("history-strategy");
            Check(filter != null, "saved cases expose strategy filter");
            filter.value = "계획";
            Check(root.Q<Button>("history-game-0") == null && root.Q<Button>("history-game-1") != null, "planning filter preserves recorded ordinals");
            filter.value = "전체";
            Click("history-reference");
            Check(root.Q<Label>("history-comparison").text.Contains("같은 정의"), "reference comparison identifies same source");
            Check(root.Q<Label>("history-summary").text.Contains("패배 잔여"), "statistics include remaining mission measurements");
            Click("history-game-0");
            Check(root.Q<Label>("history-case-detail").text.Contains("난수"), "selected case describes stored actions and random consumption");
            Click("history-replay-all");
            BotRecordReplay Replay() => (BotRecordReplay)typeof(LevelToolScreen).GetField("historyReplay", flags).GetValue(screen);
            int replayTicks = 0;
            while (Replay().NeedsAdvance && replayTicks++ < 50000) TickHistory();
            Check(Replay().Status == BotReplayStatus.Completed, "disk source and saved actions replay to exact terminal outcome: " + Replay().Message);
            Check(root.Q("history-trial-board")?.Query<Label>(className: "trial-cell").ToList().Count == Replay().State.Rows * Replay().State.Columns, "record replay renders its own runtime board");
            Check(session.ExportState() == before, "archive and replay preserve full authoring state");
            string context = new AutoPlay.BotBatchStore(directory).ReadSourceContext(Batch().Record.id);
            var restored = LevelAuthoring.Storage.AuthoringTrialSource.Decode(context);
            Check(context == LevelAuthoring.Storage.AuthoringTrialSource.Encode(session.CreateSnapshot(), session.SelectedLevelId), "batch disk context preserves all JSON source documents");
            using (var graph = new LevelAuthoring.Runtime.AuthoringObjectGraph(restored.Snapshot.Documents))
            {
                var level = (Levels.LevelDefinition)graph.Resolve(restored.LevelId);
                Check(Simulation.LevelStateBuilder.Fingerprint(level) == Batch().Record.fingerprint, "disk JSON source rebuilds same runtime fingerprint with catalog");
            }
            LevelToolHistoryExercise.Run(screen);
        }
        internal static bool VerifyArtwork(LevelToolScreen screen)
        {
            var view = screen.GetComponent<UIDocument>().rootVisualElement.Q("history-trial-board");
            Check(view != null, "replay board remains available after async loading");
            string error = view.Query<Label>().ToList().Select(label => label.text).FirstOrDefault(text => text != null && (text.Contains("이미지 로드 실패") || text.Contains("이미지 정의 오류")));
            if (error != null) throw new Exception(error);
            var sprites = view.Query<Image>().ToList();
            if (sprites.Count == 0) return false;
            Check(sprites.All(image => image.sprite != null), "runtime replay board has loaded actual sprite frames");
            Check(view.Query<Label>(className: "trial-cell").ToList().Any(label => string.IsNullOrEmpty(label.text)), "loaded artwork replaces fallback cell text");
            return true;
        }
        private static void Check(bool value, string text) { if (!value) throw new Exception("FAIL " + text); Debug.Log("PASS " + text); }
    }
}

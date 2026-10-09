#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using AutoPlay;
using LevelAuthoring.Documents;
using LevelAuthoring.Runtime;
using Levels;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private TrialBoardView botBoard;
        private BotPlaySession toolBot;
        private AuthoringObjectGraph toolBotGraph;
        private ContentSnapshot toolBotSnapshot;
        private string toolBotLevel, toolBotName;
        private int toolBotSeed = 12345, displayedBotTurns = -1;
        private BotStrategyKind toolBotStrategy;
        private Label toolBotStatus;
        private VisualElement toolBotRecords;

        private void DrawToolBot()
        {
            inspector.Add(new Label("봇은 시작 순간의 레벨 사본을 사용하며 편집 보드를 바꾸지 않습니다. 튜토리얼 안내·아이템·부스터 없이 퍼즐 규칙만 시험합니다. 선택 점수는 사람의 체감 난이도가 아닙니다.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            Number(inspector, "bot-seed", "시작 시드", toolBotSeed, value => { toolBotSeed = value; checkpointPending = true; });
            Choice(inspector, "bot-strategy", "전략", new[] { "Basic", "Planning" }, new[] { "기본", "두 수 계획" }, toolBotStrategy.ToString(),
                value => { toolBotStrategy = (BotStrategyKind)Enum.Parse(typeof(BotStrategyKind), value); checkpointPending = true; });
            Button(inspector, "bot-new", "현재 레벨로 새 봇 시험", () => PrepareToolBot(false));
            Button(inspector, "bot-repeat", "직전 시작 사본·시드로 다시", () => PrepareToolBot(true))
                .tooltip = "편집 내용을 다시 읽지 않습니다. 직전 시험 사본과 시드는 유지하고, 선택한 전략으로 다시 시작합니다.";
            Button(inspector, "bot-step", "한 수 진행", () => { toolBot?.Begin(false); UpdateToolBotControls(); });
            Button(inspector, "bot-run", "한 판 실행", () => { toolBot?.Begin(true); UpdateToolBotControls(); });
            Button(inspector, "bot-stop", "현재 행동 후 중지", () => { toolBot?.RequestStop(); UpdateToolBotControls(); });
            toolBotStatus = new Label { name = "bot-status", style = { whiteSpace = WhiteSpace.Normal } }; inspector.Add(toolBotStatus);
            toolBotRecords = new Foldout { text = "최근 행동과 선택 이유", value = true }; inspector.Add(toolBotRecords);
            botBoard?.Dispose(); botBoard = new TrialBoardView("bot-trial-board", "봇 시험 보드"); boardScroll.Add(botBoard.Root);
            displayedBotTurns = -1; UpdateToolBotControls();
            DrawToolBatch();
            DrawToolMulti();
            DrawToolHistory();
            DrawToolRecords();
        }

        private void PrepareToolBot(bool repeat)
        {
            if (busy || RecordsBusy || BatchActive || MultiActive || toolBot?.NeedsAdvance == true || Session == null) return;
            AuthoringObjectGraph graph = null;
            try
            {
                ContentSnapshot snapshot = repeat ? toolBotSnapshot : sharedTutorialDraft?.Preview(Session) ?? Session.CreateSnapshot();
                if (snapshot == null) throw new InvalidOperationException("먼저 새 봇 시험을 시작하세요.");
                string id = repeat ? toolBotLevel : Session.SelectedLevelId;
                int runSeed = repeat ? toolBot.Seed : toolBotSeed;
                graph = new AuthoringObjectGraph(snapshot.Documents);
                var level = (LevelDefinition)graph.Resolve(id);
                var issues = LevelDefinitionValidator.Validate(level);
                if (issues.Count != 0) throw new ArgumentException(string.Join("\n", issues.Select(issue => issue.Message)));
                var next = new BotPlaySession(level, runSeed, toolBotStrategy);
                DisposeToolBot();
                toolBot = next; toolBotGraph = graph; graph = null;
                toolBotSnapshot = snapshot; toolBotLevel = id;
                toolBotName = (string)snapshot.Get(id).Data["displayName"];
                displayedBotTurns = -1; UpdateToolBotControls();
            }
            catch (Exception error) { graph?.Dispose(); Show("봇 시험 시작 실패: " + error.Message); }
        }

        private void AdvanceToolBot()
        {
            if (busy || playRoot != null || toolBot?.NeedsAdvance != true) return;
            toolBot.Advance();
            UpdateToolBotControls();
        }

        private void UpdateToolBotControls()
        {
            if (inspectorPage != "자동 시험" || toolBotStatus == null) return;
            botBoard?.Show(toolBot?.State, toolBotGraph == null ? null : (LevelDefinition)toolBotGraph.Resolve(toolBotLevel));
            UpdateToolBatchControls();
            UpdateToolMulti();
            bool running = toolBot?.NeedsAdvance == true;
            bool canRun = !BatchActive && !MultiActive && toolBot != null && (toolBot.Status == BotSessionStatus.Preparing || toolBot.Status == BotSessionStatus.Ready || toolBot.Status == BotSessionStatus.Stopped && toolBot.State != null);
            root.Q<Button>("bot-new")?.SetEnabled(!running && !BatchActive && !MultiActive);
            root.Q<Button>("bot-repeat")?.SetEnabled(!running && !BatchActive && !MultiActive && toolBotSnapshot != null);
            root.Q<Button>("bot-step")?.SetEnabled(canRun); root.Q<Button>("bot-run")?.SetEnabled(canRun);
            root.Q<Button>("bot-stop")?.SetEnabled(running);
            toolBotStatus.text = toolBot == null ? "새 시험을 시작하세요." : toolBotName + " · 시드 " + toolBot.Seed + " · " +
                (toolBot.Strategy == BotStrategyKind.Basic ? "기본" : "두 수 계획") + "\n" + toolBot.Message +
                "\n완료 행동 " + toolBot.Records.Count + " · 남은 이동 " + (toolBot.State?.MovesRemaining.ToString() ?? "준비 중");
            int count = toolBot?.Records.Count ?? 0;
            if (displayedBotTurns == count) return;
            displayedBotTurns = count; toolBotRecords.Clear();
            if (toolBot == null) return;
            foreach (var turn in toolBot.Records.Skip(Math.Max(0, count - 20)))
                toolBotRecords.Add(new Label(turn.Turn + ". " + turn.Choice.Action.First + " → " + turn.Choice.Action.Second + "\n" + turn.Choice.Reason)
                    { style = { whiteSpace = WhiteSpace.Normal } });
        }

        private void DisposeToolBot()
        {
            toolBot?.Dispose(); toolBot = null;
            toolBotGraph?.Dispose(); toolBotGraph = null;
            toolBotSnapshot = null; toolBotLevel = null;
        }
    }
}
#endif

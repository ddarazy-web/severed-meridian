#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using System.Linq;
using AutoPlay;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Documents;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using Levels;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private TrialBoardView batchBoard;
        private PersistedBotBatch toolBatch;
        private AuthoringObjectGraph toolBatchGraph;
        private ContentSnapshot batchSnapshot;
        private string batchLevel;
        private int[] batchSeeds;
        private int batchCount = 100, displayedBatchGames = -1;
        private Label batchStatus;
        private VisualElement batchRows;
        private bool BatchActive => toolBatch != null && (toolBatch.HasWork || toolBatch.CanControl);
        private string BatchDirectory => Path.Combine(string.IsNullOrWhiteSpace(recoveryDirectory) ? Path.Combine(Application.persistentDataPath, "LevelTool") : recoveryDirectory, "Trials");

        private void DrawToolBatch()
        {
            var panel = new Foldout { text = "같은 시드로 두 전략 반복 시험", value = true }; inspector.Add(panel);
            panel.Add(new Label("횟수마다 기본·계획 전략을 같은 시드로 실행합니다. 100회는 총 200판입니다. 저장 중에는 다음 판을 기다립니다.") { style = { whiteSpace = WhiteSpace.Normal } });
            Number(panel, "batch-count", "전략별 시험 횟수", batchCount, value => { batchCount = value; checkpointPending = true; });
            Button(panel, "batch-new", "현재 레벨로 반복 시험", () => StartToolBatch(false));
            Button(panel, "batch-repeat", "직전 사본·시드 묶음으로 다시", () => StartToolBatch(true));
            Button(panel, "batch-pause", "일시정지", () => { toolBatch?.Pause(); UpdateToolBatchControls(); });
            Button(panel, "batch-resume", "같은 상태로 재개", () => { toolBatch?.Resume(); UpdateToolBatchControls(); });
            Button(panel, "batch-stop", "반복 시험 중지", () => { toolBatch?.RequestStop(); UpdateToolBatchControls(); });
            batchStatus = new Label { name = "batch-status", style = { whiteSpace = WhiteSpace.Normal } }; panel.Add(batchStatus);
            batchRows = new VisualElement(); panel.Add(batchRows); displayedBatchGames = -1;
            batchBoard?.Dispose(); batchBoard = new TrialBoardView("batch-trial-board", "반복 시험 현재 판"); boardScroll.Add(batchBoard.Root);
            UpdateToolBatchControls();
        }

        private void StartToolBatch(bool repeat)
        {
            if (busy || RecordsBusy || BatchActive || MultiActive || toolBot?.NeedsAdvance == true) return;
            AuthoringObjectGraph graph = null;
            try
            {
                var snapshot = repeat ? batchSnapshot : sharedTutorialDraft?.Preview(Session) ?? Session.CreateSnapshot();
                if (snapshot == null) throw new InvalidOperationException("먼저 새 반복 시험을 시작하세요.");
                string id = repeat ? batchLevel : Session.SelectedLevelId;
                int[] seeds = repeat ? (int[])batchSeeds.Clone() : BotBatchSession.NewSeeds(batchCount, batchSeeds);
                graph = new AuthoringObjectGraph(snapshot.Documents);
                var level = (LevelDefinition)graph.Resolve(id);
                var next = new PersistedBotBatch(level, seeds, new AutoPlay.BotBatchStore(BatchDirectory, AuthoringTrialSource.Encode(snapshot, id)));
                toolBatch?.Dispose(); toolBatchGraph?.Dispose(); DisposeToolBot();
                toolBatch = next; toolBatchGraph = graph; graph = null;
                batchSnapshot = snapshot; batchLevel = id; batchSeeds = seeds;
                displayedBatchGames = -1; UpdateToolBatchControls(); UpdateToolBotControls();
            }
            catch (Exception error) { graph?.Dispose(); Show("반복 시험 시작 실패: " + error.Message); }
        }

        private void AdvanceToolBatch()
        {
            if (busy || toolBatch == null || !toolBatch.HasWork) return;
            toolBatch.Advance(); UpdateToolBatchControls(); UpdateToolBotControls();
        }

        private void UpdateToolBatchControls()
        {
            if (inspectorPage != "자동 시험" || batchStatus == null) return;
            batchBoard?.Show(toolBatch?.Current?.State, toolBatchGraph == null ? null : (LevelDefinition)toolBatchGraph.Resolve(batchLevel));
            bool active = BatchActive, single = toolBot?.NeedsAdvance == true || MultiActive;
            root.Q<Button>("batch-new")?.SetEnabled(!active && !single);
            root.Q<Button>("batch-repeat")?.SetEnabled(!active && !single && batchSnapshot != null);
            root.Q<Button>("batch-pause")?.SetEnabled(toolBatch?.CanControl == true && toolBatch.Record.status == BotBatchStatus.Running);
            root.Q<Button>("batch-resume")?.SetEnabled(toolBatch?.CanControl == true && toolBatch.Record.status == BotBatchStatus.Paused);
            root.Q<Button>("batch-stop")?.SetEnabled(active);
            if (toolBatch == null) { batchStatus.text = "아직 반복 시험 기록이 없습니다."; return; }
            var record = toolBatch.Record;
            batchStatus.text = (toolBatch.IsSaving ? "기록 저장 중" : record.status.ToString()) + " · 저장 확인 " + toolBatch.Games.Count + "/" + record.Total +
                "\n" + record.message + (toolBatch.PersistenceError == null ? "" : "\n저장 실패: " + toolBatch.PersistenceError);
            if (displayedBatchGames == toolBatch.Games.Count) return;
            displayedBatchGames = toolBatch.Games.Count; batchRows.Clear();
            foreach (var game in toolBatch.Games.Skip(Math.Max(0, toolBatch.Games.Count - 20)))
                batchRows.Add(new Label((game.Ordinal + 1) + ". 시드 " + game.Seed + " · " + (game.Strategy == BotStrategyKind.Basic ? "기본" : "계획") +
                    " · " + game.Outcome + " · 사용 이동 " + game.UsedMoves) { style = { whiteSpace = WhiteSpace.Normal } });
        }

        private async UniTask CloseToolBatch()
        {
            var closing = toolBatch; var graph = toolBatchGraph;
            toolBatch = null; toolBatchGraph = null;
            try { if (closing != null) await closing.StopAndDisposeAsync(); }
            finally { graph?.Dispose(); }
        }

        private void OnApplicationQuit() => DrainTrialRecords();

        private void DrainTrialRecords()
        {
            toolMulti?.StopAndDisposeAtShutdown(); toolMulti = null;
            multiGraph?.Dispose(); multiGraph = null;
            toolBatch?.StopAndDisposeAtShutdown(); toolBatch = null;
            toolBatchGraph?.Dispose(); toolBatchGraph = null;
        }
    }
}
#endif

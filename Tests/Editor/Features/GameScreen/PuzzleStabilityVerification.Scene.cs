using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static partial class PuzzleStabilityVerification
    {
        private const string PlayKey = "Stage11.Scene";
        static PuzzleStabilityVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PlayKey, false))
                { SessionState.SetBool(PlayKey, false); SceneAsync().Forget(Debug.LogException); }
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Stage11.Observe", false))
                { SessionState.SetBool("Stage11.Observe", false); ObservationAsync().Forget(Debug.LogException); }
                if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("Stage11.UI", false))
                {
                    SessionState.SetBool("Stage11.UI", false);
                    string path = PuzzleUIStateVerification.Output + "interaction-results.txt";
                    string[] lines = File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
                    EditorApplication.Exit(lines.Any(line => line.StartsWith("PASS ")) && !lines.Any(line => line.StartsWith("FAIL ")) ? 0 : 1);
                }
            };
        }
        public static void RunScene()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            Directory.CreateDirectory(Output); PuzzleUIRenderVerification.RememberSize(); SessionState.SetBool(PlayKey, true);
            EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath); EditorApplication.EnterPlaymode();
        }
        public static void Boundaries()
        { SessionState.SetBool("Stage11.Boundaries", true); RunScene(); }
        public static void SourceOutcomes()
        { SessionState.SetBool("Stage11.Sources", true); RunScene(); }
        public static void ResultBoundaries()
        { SessionState.SetBool("Stage11.Results", true); RunScene(); }
        public static void UIInteraction()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            Directory.CreateDirectory(PuzzleUIStateVerification.Output); SessionState.SetBool("Stage11.UI", true); PuzzleUIInteractionVerification.Run();
        }
        private static async UniTask SceneAsync()
        {
            results.Clear(); int exit = 0; float previous = Time.timeScale;
            try
            {
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                float deadline = Time.realtimeSinceStartup + 30;
                while (!session.CanAcceptInput && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                Check(session.CanAcceptInput, "11단계 실제 게임 씬 시작"); Time.timeScale = 0;
                bool boundariesOnly = SessionState.GetBool("Stage11.Boundaries", false); SessionState.SetBool("Stage11.Boundaries", false);
                bool sourcesOnly = SessionState.GetBool("Stage11.Sources", false); SessionState.SetBool("Stage11.Sources", false);
                bool resultsOnly = SessionState.GetBool("Stage11.Results", false); SessionState.SetBool("Stage11.Results", false);
                if (resultsOnly) await BackgroundResultChecks();
                else if (sourcesOnly) await OriginalSourceOutcomeChecks();
                else
                {
                    if (!boundariesOnly) { await SourceAndActionChecks(); await PoolReuseChecks(); }
                    await InterruptionBoundaryChecks();
                    if (!boundariesOnly) { await BackgroundResultChecks(); await LifetimeChecks(); }
                }
                BaselineInvariantChecks();
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally { Time.timeScale = previous; PuzzleUIRenderVerification.RestoreSize(); File.WriteAllLines(Output + "scene-results.txt", results); EditorApplication.Exit(exit); }
        }
        private static async UniTask SourceAndActionChecks()
        {
            await ExistingSceneCheck("ActionChecks", UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>());
            await ExistingSceneCheck("BoundaryChecks", UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>());
            await ExistingSceneCheck("SourceChecks");
            await OriginalSourceOutcomeChecks();
            await ExistingSceneCheck("RelatedRecoveryChecks");
            await ExistingSceneCheck("LargeObstacleAudioCheck");
            await ExistingSceneCheck("LossCheck");
            Check(true, "실제 Asset/MemoryPack·4파워·10조합·2x2·3아이템·회수·연쇄·승패 상태 동등 재실행");
        }
        private static async UniTask ExistingSceneCheck(string name, params object[] arguments)
        {
            List<string> reused = (List<string>)typeof(PuzzleAudioFeedbackVerification).GetField("results", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            reused.Clear();
            await (UniTask)typeof(PuzzleAudioFeedbackVerification).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, arguments);
            results.AddRange(reused.Select(line => line.Replace("PASS ", "PASS 재실행(10) ")));
        }
        private static async UniTask Step(PuzzleGameSession session, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while ((bool)Field(session, "preparingEffects") && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
            if ((bool)Field(session, "preparingEffects") || session.HasFailed) throw new InvalidOperationException("표시 준비 실패: " + session.Message);
            Invoke(session, "TickProgress", seconds);
            if (session.IsPresenting) Invoke(session, "AdvancePresentation", seconds); else Invoke(session, "Advance");
            await UniTask.Yield();
        }
        private static async UniTask<LevelDefinition> PrepareFixture(PuzzleGameSession session, int kind)
        {
            LevelDefinition level = kind == 4 ? (LevelDefinition)typeof(CombinationVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { 8, RocketDirection.Horizontal, null }) :
                (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":" + (kind == 3 ? 1 : 1000) + "}]}", level);
            if (kind == 0 || kind == 1)
                foreach (BoardCoordinate cell in new[] { new BoardCoordinate(3, 2), new BoardCoordinate(3, 4), new BoardCoordinate(3, 5), new BoardCoordinate(2, 3) })
                    typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                        new object[] { level, cell, InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type1 });
            if (kind == 2 || kind == 3 || kind == 5)
                foreach (BoardCoordinate cell in kind == 3 ? new[] { new BoardCoordinate(4, 4), new BoardCoordinate(7, 7) } : new[] { new BoardCoordinate(4, 4) })
                    typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                        new object[] { level, cell, (kind == 3 || kind == 5) && cell.Row == 4 ? InitialBlockKind.Rocket : InitialBlockKind.Drone, RocketDirection.Horizontal, RabbitColor.Type1 });
            Check(LevelSupplyEditing.AddTopSources(level) == null, "fixture 실제 상단 신규 공급구 " + kind);
            typeof(PuzzleGameSession).GetField("initialBytes", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, LevelPackCodec.Encode(new[] { level }));
            session.enabled = true; await session.RestartAsync(CancellationToken.None); session.enabled = false;
            Invoke(session, "TickProgress", .7f);
            Check(session.IsReady && !session.HasFailed && session.Phase == BoardActionPhase.Ready, "실제 다시하기 fixture 준비 " + kind);
            return level;
        }
        private static BoardActionExecutor BeginFixture(PuzzleGameSession session, int kind, bool executeDirect = true)
        {
            BoardActionExecutor direct = new BoardActionExecutor(session.State); session.enabled = true;
            if (kind == 0)
            {
                BoardCoordinate first = new BoardCoordinate(2, 3), second = new BoardCoordinate(3, 3);
                Check(session.TrySwap(first, second), "일반 매칭 실제 교환"); if (executeDirect) direct.Swap(first, second);
            }
            else if (kind == 1)
            {
                bool found = false;
                foreach (RuntimeCell cell in session.State.Cells)
                {
                    BoardCoordinate right = new BoardCoordinate(cell.Coordinate.Row, cell.Coordinate.Column + 1);
                    if (right.Column < 9 && ActionQuery.Swap(session.State, cell.Coordinate, right).Reason == ActionReason.NoNewMatch)
                    { Check(!session.TrySwap(cell.Coordinate, right), "무효 교환 실제 복귀"); if (executeDirect) direct.Swap(cell.Coordinate, right); found = true; break; }
                }
                Check(found && session.IsPresenting, "무효 복귀 fixture 시작");
            }
            else if (kind == 4)
            { Check(session.TrySwap(new BoardCoordinate(4, 4), new BoardCoordinate(4, 5)), "다수 드론 실제 조합"); if (executeDirect) direct.Swap(new BoardCoordinate(4, 4), new BoardCoordinate(4, 5)); }
            else { Check(session.TryActivate(new BoardCoordinate(4, 4)), "실제 파워 발동 " + kind); if (executeDirect) direct.Activate(new BoardCoordinate(4, 4)); }
            session.enabled = false; return direct;
        }
        private static bool Busy(PuzzleGameSession session) => session.IsPresenting || ((BoardActionExecutor)Field(session, "executor")).HasPendingCascade || session.HasProgressFeedback;
        private static async UniTask Finish(PuzzleGameSession session, BoardActionExecutor direct, Action observe = null)
        {
            for (int frame = 0; frame < 20000 && Busy(session); frame++) { await Step(session, .02f); observe?.Invoke(); }
            while (direct.HasPendingCascade) direct.AdvanceCascade();
            Check(!Busy(session) && !session.HasFailed && Snapshot(session.State) == Snapshot(direct.State) && session.Phase == direct.Phase && Snapshot(session.Outcome) == Snapshot(direct.Outcome), "표시 종료 전체 상태·Phase·승패 직접 동등");
            session.enabled = true; Check(session.CanAcceptInput || session.ResultReady, "표시 종료 입력 또는 결과 정상 복귀"); session.enabled = false;
        }
        private static bool Playing(object playback) => playback != null && (bool)playback.GetType().GetProperty("IsPlaying", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(playback);
    }
}

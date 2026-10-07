using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static partial class PuzzleLevelTransitionVerification
    {
        private const string PlayKey = "Stage13.Scene";
        static PuzzleLevelTransitionVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PlayKey, false))
                { SessionState.EraseBool(PlayKey); SceneAsync().Forget(Debug.LogException); }
            };
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("Stage13.AssetFinish", false))
                    EditorApplication.delayCall += () =>
                    {
                        SessionState.EraseBool("Stage13.AssetFinish");
                        bool clean = !PuzzleEditorLauncher.IsBusy && SessionState.GetInt("Puzzle.EditorLaunch.source", int.MinValue) == int.MinValue;
                        File.AppendAllText(Output + "scene-results.txt", (clean ? "PASS " : "FAIL ") + "Asset 게임 종료 요청·source 정리\n");
                        int exit = clean ? SessionState.GetInt("Stage13.AssetExit", 1) : 1;
                        SessionState.EraseInt("Stage13.AssetExit");
                        EditorApplication.Exit(exit);
                    };
            };
        }

        public static void RunScene()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            Directory.CreateDirectory(Output); PuzzleUIRenderVerification.RememberSize(); SessionState.SetBool(PlayKey, true);
            EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath); EditorApplication.EnterPlaymode();
        }
        public static void RunBoundaries()
        { SessionState.SetBool("Stage13.Boundaries", true); RunScene(); }
        public static void RunLoss()
        { SessionState.SetBool("Stage13.Loss", true); RunScene(); }
        public static void RunRegression()
        { SessionState.SetBool("Stage13.Regression", true); RunScene(); }
        public static void RunProbe()
        { SessionState.SetBool("Stage13.Probe", true); RunScene(); }

        private static async UniTask SceneAsync()
        {
            results.Clear(); int exit = 0; LevelDefinition fixture = null;
            bool asset = SessionState.GetBool("Stage13.AssetScene", false); SessionState.EraseBool("Stage13.AssetScene");
            try
            {
                Application.runInBackground = true;
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                float deadline = Time.realtimeSinceStartup + 45;
                while (!session.CanAcceptInput && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                Check(session.CanAcceptInput, asset ? "실제 Asset launcher 씬 준비" : "실제 MemoryPack 씬 준비");
                if (asset) { await AssetChecks(session); return; }
                int review = SessionState.GetInt("Stage13.Review", -1);
                if (review >= 0) { SessionState.EraseInt("Stage13.Review"); await ReviewChecks(session, review); return; }
                if (SessionState.GetBool("Stage13.Probe", false))
                { SessionState.EraseBool("Stage13.Probe"); await WinAt(session, 1); await CapturePopups("inset-victory", true); return; }
                if (SessionState.GetBool("Stage13.Regression", false))
                { SessionState.EraseBool("Stage13.Regression"); await RegressionChecks(); return; }
                if (SessionState.GetBool("Stage13.Loss", false))
                { SessionState.EraseBool("Stage13.Loss"); await LossChecks(session); return; }
                if (SessionState.GetBool("Stage13.Boundaries", false))
                { SessionState.EraseBool("Stage13.Boundaries"); await BoundaryChecks(session); return; }
                Check(!session.CanAdvanceLevel && !await session.AdvanceLevelAsync(CancellationToken.None), "승리 전 전환 거부");
                fixture = await (UniTask<LevelDefinition>)typeof(PuzzleStabilityVerification)
                    .GetMethod("PrepareFixture", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { session, 3 });
                typeof(PuzzleStabilityVerification).GetMethod("BeginFixture", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { session, 3, false });
                session.enabled = true;
                deadline = Time.realtimeSinceStartup + 45;
                while (!session.ResultReady && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                Check(session.ResultReady && session.Outcome.Kind == BoardOutcomeKind.Won && session.CanAdvanceLevel, "실제 승리 ResultReady 전환 허용");
                PuzzleResultView popup = UnityEngine.Object.FindFirstObjectByType<PuzzleResultView>();
                UnityEngine.UI.Button next = (UnityEngine.UI.Button)typeof(PuzzleResultView).GetField("nextLevel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(popup);
                UnityEngine.UI.Button retry = (UnityEngine.UI.Button)typeof(PuzzleResultView).GetField("retry", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(popup);
                UnityEngine.UI.Text detail = (UnityEngine.UI.Text)typeof(PuzzleResultView).GetField("detail", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(popup);
                Check(next.gameObject.activeInHierarchy && next.interactable && retry.interactable, "실제 승리 팝업 다음·Retry 활성");
                await CapturePopups("victory");
                object state = session.State, outcome = session.Outcome;
                string logicalSession = session.LogicalSessionId;
                FieldInfo bytesField = typeof(PuzzleGameSession).GetField("initialBytes", BindingFlags.NonPublic | BindingFlags.Instance);
                byte[] bytes = (byte[])bytesField.GetValue(session);
                int number = session.State.LevelNumber;
                UniTask<bool> request = session.AdvanceLevelAsync(CancellationToken.None);
                Check(session.IsChangingLevel && !session.CanAcceptInput && !session.CanUseItems && !session.CanAdvanceLevel, "준비 중 보드·아이템·추가 요청 차단");
                Check(popup.gameObject.activeInHierarchy && !next.interactable && !retry.interactable && detail.text.Contains("다음 레벨 준비 중"), "실제 준비 팝업 유지·버튼 잠금·안내");
                Check(!await session.AdvanceLevelAsync(CancellationToken.None), "준비 중 중복 전환 거부");
                await session.RestartAsync(CancellationToken.None);
                Check(ReferenceEquals(session.State, state), "준비 중 Retry 기존 판 유지");
                Check(!await request, "다음 번호 누락 로드 실패 반환");
                Check(!session.IsChangingLevel && session.ResultReady && !session.HasFailed && ReferenceEquals(session.State, state) &&
                    ReferenceEquals(session.Outcome, outcome) && ReferenceEquals(bytesField.GetValue(session), bytes) && session.State.LevelNumber == number,
                    "누락 실패 후 승리·전체 상태·번호·bytes 기준 보존");
                Check(session.Message.Contains("다음 레벨을 불러올 수 없습니다"), "누락 실패 진단 전달");
                Check(session.LogicalSessionId == logicalSession, "Next 준비 실패 기존 논리 문맥 보존");
                Check(detail.text.Contains("다음 레벨을 불러올 수 없습니다") && next.interactable && retry.interactable, "실제 실패 팝업 갱신·재시도 활성");
                await CapturePopups("error");
                using CancellationTokenSource cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                Check(!await session.AdvanceLevelAsync(cancellation.Token) && session.ResultReady && ReferenceEquals(session.State, state), "사전 취소 기존 승리 보존");
                Check(session.LogicalSessionId == logicalSession, "Next 사전 취소 기존 논리 문맥 보존");
                await SuccessChecks(session);
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally
            {
                if (fixture != null) UnityEngine.Object.Destroy(fixture);
                try { PuzzleUIRenderVerification.RestoreSize(); Check(true, "Game View 선택·크기 목록 복원"); }
                catch (Exception error) { results.Add("FAIL cleanup " + error); exit = 1; }
                File.WriteAllLines(Output + "scene-results.txt", results);
                if (asset)
                {
                    SessionState.SetInt("Stage13.AssetExit", exit); SessionState.SetBool("Stage13.AssetFinish", true);
                    EditorApplication.ExitPlaymode();
                }
                else EditorApplication.Exit(exit);
            }
        }
    }
}

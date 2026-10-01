using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static partial class PuzzleSettlementAnimationVerification
    {
        private const string Key = "StageSeven.Verify";
        private const string Output = "Logs/PuzzleSettlementAnimationVerification/";
        private static readonly List<string> results = new List<string>();

        static PuzzleSettlementAnimationVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                { SessionState.SetBool(Key, false); VerifyAsync().Forget(Debug.LogException); }
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key + ".Scene", false))
                { SessionState.SetBool(Key + ".Scene", false); VerifySceneAsync().Forget(Debug.LogException); }
            };
        }

        public static void Run()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Key, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static async UniTask VerifyAsync()
        {
            results.Clear();
            GameObject root = null;
            LevelDefinition level = null;
            int exit = 0;
            try
            {
                root = new GameObject("Settlement verification");
                PuzzleWorldBoard prefab = AssetDatabase.LoadAssetAtPath<PuzzleWorldBoard>(PuzzleGameAssets.Folder + "/PuzzleWorldBoard.prefab");
                PuzzleWorldBoard board = UnityEngine.Object.Instantiate(prefab, root.transform);
                Camera camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform); camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = 5.2f;
                level = (LevelDefinition)typeof(BoardActionVerification).GetMethod("RocketBoard", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                // 기존 블록의 대각선 이동 없이도 제거 뒤 채움 연출이 발생하는 공급을 둔다.
                typeof(SettlementVerification).GetMethod("Source", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { level, new BoardCoordinate(3, 2), SupplyExhaustion.Stop, new[] { new SupplyItem(SupplyKind.FixedNormal, 1) } });
                LevelRuntimeState state = LevelStateBuilder.Build(level, 12345).State;
                GameObject owner = new GameObject("Session"); owner.transform.SetParent(root.transform);
                PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera);
                Set(session, "started", true);
                PuzzleArtwork art = new PuzzleArtwork(); Set(session, "artwork", art);
                await art.PrepareAsync(state, CancellationToken.None);
                BoardActionExecutor executor = new BoardActionExecutor(state);
                Set(session, "executor", executor); Set(session, "ready", true);
                board.Draw(state, art);
                BoardCoordinate a = new BoardCoordinate(2, 3), b = new BoardCoordinate(3, 3);
                Dictionary<SpriteRenderer, Vector3> initialScales = board.GetComponentsInChildren<SpriteRenderer>().ToDictionary(image => image, image => image.transform.localScale);
                Check(session.TrySwap(a, b), "유효 교환 시작");
                Tick(session, .15f);
                Check(session.IsPresenting, "교환 종료 후 제거 재생 유지");
                BoardActionPhase phase = session.Phase;
                Call(session, "Advance");
                Check(session.Phase == phase && !session.CanAcceptInput, "제거 중 다음 연쇄·입력 차단");
                Tick(session, .06f);
                Check(board.GetComponentsInChildren<SpriteRenderer>().Any(image => image.enabled && image.color.a > 0 && image.color.a < 1), "제거 중간 알파");
                Check(initialScales.Any(pair => pair.Key.enabled && pair.Key.color.a > 0 && pair.Key.color.a < 1 &&
                    pair.Key.transform.localScale.x < pair.Value.x && pair.Key.transform.localScale.y < pair.Value.y), "제거 중간 축소");
                Tick(session, .06f);
                Check(!session.IsPresenting, "제거 종료");
                Check(initialScales.All(pair => pair.Key.transform.localScale == pair.Value && pair.Key.color.a == 1), "제거 종료 원래 축척·알파 복원");
                foreach (MatchedBlockChange change in executor.LastApplied.Changes.Where(change => change.IsTransformation))
                    Check(board.OccupantAt(change.Coordinate)?.sprite != null, "생성 파워 표시 보존");
                Call(session, "Advance");
                Check(session.IsPresenting, "정착 계산 후 낙하 재생 유지");
                Check(executor.LastSettlement.Records.Count > 0, "낙하 기록 fixture");
                int round = executor.CascadeRounds;
                Tick(session, .06f);
                Call(session, "Advance");
                Check(executor.CascadeRounds == round && !session.CanAcceptInput, "낙하 중 자동 매칭 차단");
                for (int frame = 0; frame < 1000 && session.IsPresenting; frame++) Tick(session, .02f);
                Check(!session.IsPresenting, "정착 재생 종료");
                Call(session, "ResetPresentation");
                LevelRuntimeState powerState = LevelStateBuilder.Build(level, 12345).State;
                RuntimeCell powerCell = powerState.CellAt(a);
                Check(powerCell.IsActive, "파워 탭 활성 칸 fixture");
                typeof(RuntimeCell).GetProperty("Content").SetValue(powerCell, RuntimeContent.Rocket);
                typeof(RuntimeCell).GetProperty("RocketDirection").SetValue(powerCell, RocketDirection.Horizontal);
                typeof(RuntimeCell).GetProperty("Color").SetValue(powerCell, null);
                executor = new BoardActionExecutor(powerState); Set(session, "executor", executor);
                board.Draw(powerState, art);
                BoardActionExecutor direct = new BoardActionExecutor(powerState); direct.Activate(powerCell.Coordinate);
                Check(session.TryActivate(powerCell.Coordinate) && session.IsPresenting, "파워 탭 제거 재생");
                for (int frame = 0; frame < 20000 && (session.IsPresenting || executor.HasPendingCascade); frame++)
                {
                    if (session.IsPresenting) Tick(session, .02f);
                    else Call(session, "Advance");
                }
                for (int step = 0; step < 1000 && direct.HasPendingCascade; step++) direct.AdvanceCascade();
                Check(!session.IsPresenting && !executor.HasPendingCascade && Snapshot(executor.State) == Snapshot(direct.State), "파워 제거·전체 연쇄 직접 실행기 결과 동일");
                Check(executor.Phase == direct.Phase && Snapshot(executor.Outcome) == Snapshot(direct.Outcome), "파워 연쇄 Phase·승패 결과 동일");
                await VerifyRoutesAsync(session, board, art);
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally
            {
                UnityEngine.Object.Destroy(root);
                if (level != null) UnityEngine.Object.Destroy(level);
                File.WriteAllLines(Output + "results.txt", results);
                if (Application.isBatchMode) EditorApplication.Exit(exit); else EditorApplication.ExitPlaymode();
            }
        }

        private static void Check(bool condition, string message)
        { results.Add((condition ? "PASS " : "FAIL ") + message); if (!condition) throw new Exception(message); }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
        private static void Tick(PuzzleGameSession session, float seconds) => Call(session, "AdvancePresentation", seconds);
        private static string Snapshot(object state) => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { state });
    }
}

using System;
using System.IO;
using System.Collections.Generic;
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
    public static class PuzzleGameplayVerification
    {
        private const string Output = "Logs/PuzzleGameplayVerification/";
        private const string Key = "StageTwo.Verify";
        private static readonly List<string> results = new List<string>();
        private static GameObject root;
        private static PuzzleWorldBoard board;
        private static Camera camera;
        static PuzzleGameplayVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                { SessionState.SetBool(Key, false); VerifyAsync().Forget(Debug.LogException); }
            };
        }
        [MenuItem("Tools/Match/게임 플레이 검증")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("검증 전 현재 씬을 저장해 주세요.");
            SessionState.SetBool(Key, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static async UniTask VerifyAsync()
        {
            results.Clear(); int exitCode = 0;
            try
            {
                root = new GameObject("Gameplay Verification");
                board = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PuzzleGameAssets.Folder + "/PuzzleWorldBoard.prefab"), root.transform).GetComponent<PuzzleWorldBoard>();
                camera = new GameObject("Verification Camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform); camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = (PuzzleWorldBoard.HalfHeight + 0.7f);
                PuzzleGameSession loaded = NewSession();
                await loaded.InitializeAsync(1, 12345, CancellationToken.None);
                Check(loaded.CanAcceptInput && loaded.State.LevelNumber == 1, "MemoryPack 시작 탐색 및 콜드 이미지 로드");
                Check(board.GetComponentsInChildren<SpriteRenderer>().Any(r => r.sprite != null && r.sprite.name.StartsWith("rabbit-")), "실제 일반 블록 스프라이트");
                UnityEngine.Object.Destroy(loaded.gameObject); await UniTask.Yield();
                LevelDefinition rocket = (LevelDefinition)Invoke(typeof(BoardActionVerification), "RocketBoard");
                string original = JsonUtility.ToJson(rocket);
                PuzzleGameSession session = await FromState(LevelStateBuilder.Build(rocket, 12345).State);
                BoardActionExecutor baseline = new BoardActionExecutor(session.State);
                string before = Snapshot(session.State);
                Check(!session.TrySwap(new BoardCoordinate(3, 2), new BoardCoordinate(3, 5)) && Snapshot(session.State) == before, "InvalidSwapPreservesState");
                Present(session);
                int moves = session.State.MovesRemaining;
                BoardCoordinate first = new BoardCoordinate(2, 3), second = new BoardCoordinate(3, 3);
                Check(session.TrySwap(first, second) && session.State.MovesRemaining == moves - 1, "ValidSwapConsumesOneMove");
                Check(!session.TrySwap(first, second), "같은 프레임 중복 행동 차단");
                baseline.Swap(first, second);
                Check(Snapshot(session.State) == Snapshot(baseline.State), "실행기 행동 직후 상태 동일");
                Present(session);
                Check(board.GetComponentsInChildren<SpriteRenderer>().Any(r => r.sprite != null && r.sprite.name.StartsWith("cleaning-rocket")), "ColdPowerSpawnHasArtwork");
                Finish(session, baseline);
                Check(JsonUtility.ToJson(rocket) == original, "원본 레벨 보존");
                UnityEngine.Object.Destroy(session.gameObject); UnityEngine.Object.Destroy(rocket); await UniTask.Yield();
                LevelDefinition win = (LevelDefinition)Invoke(typeof(RecoveryVerification), "PlayFixture");
                session = await FromState(LevelStateBuilder.Build(win, 12345).State);
                baseline = new BoardActionExecutor(session.State);
                Check(session.TryActivate(new BoardCoordinate(BoardDefinition.DefaultRows - 1, 0)), "파워 제자리 발동");
                baseline.Activate(new BoardCoordinate(BoardDefinition.DefaultRows - 1, 0));
                bool sawWinningCascade = false;
                for (int step = 0; step < 1000 && baseline.HasPendingCascade; step++)
                {
                    Advance(session); baseline.AdvanceCascade();
                    if (session.Outcome?.Kind == BoardOutcomeKind.Won && session.Phase != BoardActionPhase.Stopped)
                    { sawWinningCascade = true; Check(!session.CanAcceptInput, "라스트팡 입력 차단"); }
                }
                Check(sawWinningCascade && session.Outcome?.Kind == BoardOutcomeKind.Won && session.Phase == BoardActionPhase.Stopped, "WonFinishesLastPang");
                Check(Snapshot(session.State) == Snapshot(baseline.State), "회수 미션·라스트팡 최종 상태 동일");
                UnityEngine.Object.Destroy(session.gameObject); UnityEngine.Object.Destroy(win); await UniTask.Yield();
                LevelDefinition lose = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
                Invoke(typeof(PowerEffectVerification), "Place", lose, new BoardCoordinate(BoardDefinition.DefaultRows - 1, 0), InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1);
                JsonUtility.FromJsonOverwrite("{\"moveCount\":1,\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", lose);
                session = await FromState(LevelStateBuilder.Build(lose, 12345).State);
                baseline = new BoardActionExecutor(session.State);
                session.TryActivate(new BoardCoordinate(BoardDefinition.DefaultRows - 1, 0)); baseline.Activate(new BoardCoordinate(BoardDefinition.DefaultRows - 1, 0)); Finish(session, baseline);
                Check(session.Outcome?.Kind == BoardOutcomeKind.MovesExhausted && !session.CanAcceptInput, "이동 소진 및 종료 입력 차단");
                UnityEngine.Object.Destroy(session.gameObject); UnityEngine.Object.Destroy(lose); await UniTask.Yield();
                PuzzleGameSession pending = NewSession();
                UniTask loading = pending.InitializeAsync(1, 999, CancellationToken.None);
                Check(loading.Status == UniTaskStatus.Pending, "파괴 시 실제 로드가 진행 중");
                UnityEngine.Object.DestroyImmediate(pending.gameObject); await loading;
                Check(pending == null, "ExitDuringLoad 소유자 파괴 뒤 완료");
                PuzzleGameSession missing = NewSession();
                await missing.InitializeAsync(49, 12345, CancellationToken.None);
                Check(!missing.CanAcceptInput && missing.Message.Contains("실패"), "누락 레벨 입력 차단");
                UnityEngine.Object.Destroy(missing.gameObject);
                await PuzzleBoardInputVerification.VerifyAsync(board, camera);
                LevelDefinition supply = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
                Invoke(typeof(SettlementVerification), "Source", supply, new BoardCoordinate(0, 0), SupplyExhaustion.Stop, new[] { new SupplyItem(SupplyKind.Scrap) });
                LevelStateBuildResult supplied = LevelStateBuilder.Build(supply, 12345);
                Check(supplied.IsBuilt, "공급 전용 고철 검사 레벨 유효");
                using (PuzzleArtwork supplyArt = new PuzzleArtwork())
                {
                    await supplyArt.PrepareAsync(supplied.State, CancellationToken.None);
                    Check(supplyArt.Get("Obstacles/Scrap/scrap-durability-1-v1-256") != null, "초기 배치에 없는 공급 고철 이미지 준비");
                }
                UnityEngine.Object.Destroy(supply);
                LevelDefinition invalidStart = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
                session = NewSession(); Set(session, "started", true);
                bool startRejected = false;
                try
                {
                    UniTask preparation = (UniTask)typeof(PuzzleGameSession).GetMethod("PrepareAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(session, new object[] { invalidStart, 12345, CancellationToken.None });
                    await preparation;
                }
                catch (InvalidOperationException) { startRejected = true; }
                Check(startRejected && !session.CanAcceptInput && session.State == null, "시작 조건 실패 시 실행기/입력 생성 차단");
                UnityEngine.Object.Destroy(session.gameObject); UnityEngine.Object.Destroy(invalidStart); await UniTask.Yield();
                LevelDefinition damage = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
                Invoke(typeof(PowerEffectVerification), "Place", damage, new BoardCoordinate(BoardDefinition.DefaultRows - 1, 0), InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1);
                Invoke(typeof(PowerEffectVerification), "Crate", damage, new BoardCoordinate(BoardDefinition.DefaultRows - 1, 1), 3);
                session = await FromState(LevelStateBuilder.Build(damage, 12345).State);
                baseline = new BoardActionExecutor(session.State);
                Check(session.TryActivate(new BoardCoordinate(BoardDefinition.DefaultRows - 1, 0)), "장애물 피해용 로켓 발동");
                baseline.Activate(new BoardCoordinate(BoardDefinition.DefaultRows - 1, 0));
                Check(session.State.Obstacles[0].Durability < 3 && Snapshot(session.State) == Snapshot(baseline.State), "장애물 내구도와 미션 변화 일치");
                Finish(session, baseline);
                UnityEngine.Object.Destroy(session.gameObject); UnityEngine.Object.Destroy(damage); await UniTask.Yield();
                foreach (int count in new[] { 2, 9 })
                {
                    Dictionary<BoardCoordinate, int> colors = Enumerable.Range(0, count).ToDictionary(i => new BoardCoordinate(i / 5 * 2, i % 5 * 2), i => i % 5);
                    LevelDefinition isolated = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", colors, 20);
                    LevelRuntimeState state = LevelStateBuilder.Build(isolated, 12345).State;
                    session = await FromState(state);
                    BoardActionExecutor running = (BoardActionExecutor)typeof(PuzzleGameSession).GetField("executor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                    typeof(BoardActionExecutor).GetProperty("TurnEffects").GetSetMethod(true).Invoke(running, new[] { Activator.CreateInstance(typeof(TurnEffectContext), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { 0, Array.Empty<MatchedBlockChange>() }, null) });
                    typeof(BoardActionExecutor).GetProperty("Phase").GetSetMethod(true).Invoke(running, new object[] { BoardActionPhase.WaitingForAutomaticMatch });
                    Advance(session);
                    Check(session.Outcome?.Kind == (count == 2 ? BoardOutcomeKind.Blocked : BoardOutcomeKind.Aborted) && !session.CanAcceptInput, "진행 불가/탐색 중단 구분 " + count);
                    UnityEngine.Object.Destroy(session.gameObject); UnityEngine.Object.Destroy(isolated); await UniTask.Yield();
                }
            }
            catch (Exception error) { results.Add("FAIL " + error); exitCode = 1; }
            finally
            {
                if (root != null) UnityEngine.Object.Destroy(root);
                File.WriteAllLines(Output + "results.txt", results);
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
                else EditorApplication.ExitPlaymode();
            }
        }
        private static PuzzleGameSession NewSession()
        {
            GameObject owner = new GameObject("Session"); owner.transform.SetParent(root.transform);
            PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera); return session;
        }
        private static async UniTask<PuzzleGameSession> FromState(LevelRuntimeState state)
        {
            PuzzleGameSession session = NewSession(); Set(session, "started", true);
            PuzzleArtwork art = new PuzzleArtwork(); await art.PrepareAsync(state, CancellationToken.None);
            Set(session, "artwork", art); Set(session, "executor", new BoardActionExecutor(state)); Set(session, "ready", true);
            board.Draw(session.State, art); return session;
        }
        private static void Finish(PuzzleGameSession session, BoardActionExecutor baseline)
        {
            Present(session);
            for (int i = 0; i < 1000 && baseline.HasPendingCascade; i++) { Advance(session); baseline.AdvanceCascade(); }
            Check(!baseline.HasPendingCascade && session.Phase == baseline.Phase && Snapshot(session.State) == Snapshot(baseline.State), "연쇄 종료 및 실행기 상태 일치");
        }
        private static void Advance(PuzzleGameSession session) => typeof(PuzzleGameSession).GetMethod("Advance", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(session, null);
        private static void Present(PuzzleGameSession session) => typeof(PuzzleGameSession).GetMethod("AdvancePresentation", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(session, new object[] { 1f });
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        private static string Snapshot(object state) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", state);
        private static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); results.Add("PASS " + name); }
    }
}

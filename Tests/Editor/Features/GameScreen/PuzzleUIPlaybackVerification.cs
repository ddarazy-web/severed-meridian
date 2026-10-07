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
using UnityEngine;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static class PuzzleUIPlaybackVerification
    {
        private const string Key = "StageFour.Playback";
        private static readonly List<string> results = new List<string>();
        static PuzzleUIPlaybackVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                { SessionState.SetBool(Key, false); VerifyAsync().Forget(Debug.LogException); }
            };
        }

        [MenuItem("Tools/Match/UI 세션 동작 검증")]
        public static void Run()
        { SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode(); }

        private static async UniTask VerifyAsync()
        {
            results.Clear(); GameObject root = new GameObject("UI session verification"); int code = 0;
            try
            {
                // 실행 중인 실제 게임과 독립된 세션으로 규칙·재시작을 검사한다.
                PuzzleWorldBoard board = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PuzzleGameAssets.Folder + "/PuzzleWorldBoard.prefab"), root.transform).GetComponent<PuzzleWorldBoard>();
                Camera camera = new GameObject("VerificationCamera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform); camera.orthographic = true; camera.enabled = false;
                foreach (bool packed in new[] { false, true })
                {
                    GameObject owner = new GameObject("Session"); owner.transform.SetParent(root.transform);
                    PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera);
                    LevelDefinition source = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
                    string original = JsonUtility.ToJson(source);
                    if (packed) await session.InitializeAsync(1, 12345, CancellationToken.None);
                    else await session.InitializeAsync(LevelPackCodec.ReadLevel(LevelPackCodec.Encode(new[] { source }), 1), 12345, CancellationToken.None);
                    Check(session.CanAcceptInput, "준비 " + packed);
                    string initial = Snapshot(session.State);
                    Check(!session.TryUseItem(BoardItem.Hammer, new BoardCoordinate(-1, 0)) && initial == Snapshot(session.State), "잘못된 대상 거절/원자성 " + packed);
                    int subscribers = ((Delegate)typeof(PuzzleGameSession).GetField("Changed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session))?.GetInvocationList().Length ?? 0;
                    BoardCoordinate target = session.State.Cells.First(cell => session.CanSelectItemTarget(BoardItem.Hammer, cell.Coordinate)).Coordinate;
                    BoardActionExecutor baseline = new BoardActionExecutor(session.State);
                    Check(baseline.UseItem(BoardItem.Hammer, target).IsApplied && session.TryUseItem(BoardItem.Hammer, target), "망치 실행 " + packed);
                    Check(session.SetPaused(true), "연쇄 중 일시정지 " + packed);
                    string paused = Snapshot(session.State);
                    for (int i = 0; i < 8; i++) await UniTask.Yield();
                    Check(paused == Snapshot(session.State) && !session.CanAcceptInput && !session.TryUseItem(BoardItem.Shuffle), "pause 상태/명령 차단 " + packed);
                    Check(session.SetPaused(false), "재개 " + packed);
                    for (int i = 0; i < 1500 && baseline.HasPendingCascade; i++) baseline.AdvanceCascade();
                    for (int i = 0; i < 1500 && session.Phase != BoardActionPhase.Ready && session.Phase != BoardActionPhase.Stopped; i++) await UniTask.Yield();
                    Check(Snapshot(session.State) == Snapshot(baseline.State), "아이템/연쇄 기존 실행기 일치 " + packed);
                    for (int i = 0; i < 5; i++)
                    {
                        var previousArt = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                        await session.RestartAsync(CancellationToken.None);
                        Check(initial == Snapshot(session.State) && session.CanAcceptInput, "스냅샷 재현 " + packed + "/" + i);
                        Check(previousArt.AtlasCount == 0, "이전 아틀라스 반환 " + packed + "/" + i);
                    }
                    Check(subscribers == (((Delegate)typeof(PuzzleGameSession).GetField("Changed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session))?.GetInvocationList().Length ?? 0), "재시작 구독 누적 없음 " + packed);
                    Check(JsonUtility.ToJson(source) == original, "원본 보존 " + packed);
                    UniTask restarting = session.RestartAsync(CancellationToken.None);
                    Check(session.IsRestarting && !session.CanUseItems, "재시작 중 입력 차단 " + packed);
                    await session.RestartAsync(CancellationToken.None);
                    await restarting;
                    Check(initial == Snapshot(session.State) && session.CanAcceptInput, "재시작 연타 한 판 유지 " + packed);
                    using (var cancellation = new CancellationTokenSource())
                    {
                        restarting = session.RestartAsync(cancellation.Token);
                        cancellation.Cancel(); await restarting;
                        var cancelledArt = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                        Check(!session.IsReady && !session.IsRestarting && (cancelledArt == null || cancelledArt.AtlasCount == 0), "재시작 취소 자원 반환 " + packed);
                    }
                    await session.RestartAsync(CancellationToken.None);
                    Check(initial == Snapshot(session.State), "취소 후 재시작 원본 유지 " + packed);
                    restarting = session.RestartAsync(CancellationToken.None);
                    UnityEngine.Object.Destroy(owner); await UniTask.Yield(); await restarting;
                    Check(session == null, "재시작 중 소유자 종료 " + packed);
                }
            }
            catch (Exception error) { results.Add("FAIL " + error); code = 1; }
            finally
            {
                UnityEngine.Object.Destroy(root);
                Directory.CreateDirectory(PuzzleUIStateVerification.Output);
                File.WriteAllLines(PuzzleUIStateVerification.Output + "playback-results.txt", results);
                if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.ExitPlaymode();
            }
        }
        private static string Snapshot(LevelRuntimeState state) => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { state });
        private static void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException(name); results.Add("PASS " + name); }
    }
}

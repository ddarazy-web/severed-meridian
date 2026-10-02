using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Levels;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameScreen.Editor
{
    public static partial class PuzzleStabilityVerification
    {
        private static async UniTask OriginalSourceOutcomeChecks()
        {
            LevelDefinition source = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
            string original = JsonUtility.ToJson(source);
            List<string> records = new List<string> { "source,seed,turns,outcome,phase,restarted,originalUnchanged" };
            foreach (PuzzleEditorLevelSource mode in new[] { PuzzleEditorLevelSource.Asset, PuzzleEditorLevelSource.MemoryPack })
            {
                PuzzleEditorLaunchRequest request = PuzzleEditorLaunchRequest.Capture(source, mode, 12345);
                void Load(Scene scene, LoadSceneMode loadMode)
                {
                    if (scene.path != PuzzleGameAssets.ScenePath) return;
                    PuzzleGameSession loaded = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PuzzleGameSession>(true)).Single();
                    loaded.InitializeAsync(request.CreateDefinition(), 12345, CancellationToken.None).Forget(Debug.LogException);
                }
                SceneManager.sceneLoaded += Load;
                try { await EditorSceneManager.LoadSceneAsyncInPlayMode(PuzzleGameAssets.ScenePath, new LoadSceneParameters(LoadSceneMode.Single)); }
                finally { SceneManager.sceneLoaded -= Load; }
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                float deadline = Time.realtimeSinceStartup + 30;
                while (!session.IsReady && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                Check(session.IsReady && !session.HasFailed, mode + " 원본 전체 판 실제 씬 준비");
                session.enabled = false; Invoke(session, "TickProgress", .7f);
                string initial = Snapshot(session.State); int limit = session.State.MovesRemaining + 1, turns = 0;
                BoardActionExecutor direct = new BoardActionExecutor(session.State);
                List<PuzzleFeedbackCueKind> heard = new List<PuzzleFeedbackCueKind>();
                void Played(PuzzleFeedbackCueKind kind) { heard.Add(kind); }
                session.AudioPlayback.Played += Played;
                try
                {
                    while (session.Outcome == null && turns < limit)
                    {
                        ActionCandidate candidate = ActionQuery.Find(session.State).First();
                        session.enabled = true;
                        if (candidate.Kind == QueryActionKind.Activate)
                        { Check(session.TryActivate(candidate.First), mode + " 원본 판 실제 파워 " + turns); direct.Activate(candidate.First); }
                        else
                        { Check(session.TrySwap(candidate.First, candidate.Second.Value), mode + " 원본 판 실제 교환 " + turns); direct.Swap(candidate.First, candidate.Second.Value); }
                        session.enabled = false; await Finish(session, direct); turns++;
                        Check(((BoardActionExecutor)Field(session, "executor")).Turn == direct.Turn, mode + " 연속 행동 턴/생성 시점 동등 " + turns);
                    }
                    Check(session.ResultReady && session.Outcome != null && session.Phase == BoardActionPhase.Stopped, mode + " 원본 판 실제 승패·라스트팡·수집 종료 " + session.Outcome?.Kind);
                    Check(heard.Count(kind => kind == PuzzleFeedbackCueKind.Win || kind == PuzzleFeedbackCueKind.Lose) == 1, mode + " 원본 판 실제 결과음 한 번");
                    BoardOutcomeKind outcome = session.Outcome.Kind;
                    await session.RestartAsync(CancellationToken.None); Invoke(session, "TickProgress", .7f); session.enabled = true;
                    Check(session.CanAcceptInput && session.Outcome == null && !Busy(session) && Snapshot(session.State) == initial, mode + " 원본 판 종료 뒤 같은 데이터/시드 다시하기");
                    Check(JsonUtility.ToJson(source) == original, mode + " 원본 판 종료·다시하기 에셋 불변");
                    records.Add(mode + ",12345," + turns + "," + outcome + ",Stopped,True,True");
                    session.enabled = false;
                }
                finally { session.AudioPlayback.Played -= Played; }
            }
            File.WriteAllLines(Output + "source-outcomes.csv", records);
        }
    }
}

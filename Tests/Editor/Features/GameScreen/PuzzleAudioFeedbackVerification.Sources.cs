using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    public static partial class PuzzleAudioFeedbackVerification
    {
        private static async UniTask SourceChecks()
        {
            float previous = Time.timeScale; Time.timeScale = 0;
            LevelDefinition source = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
            string original = JsonUtility.ToJson(source);
            try
            {
                foreach (PuzzleEditorLevelSource mode in new[] { PuzzleEditorLevelSource.Asset, PuzzleEditorLevelSource.MemoryPack })
                    foreach (bool enabled in new[] { true, false })
                    {
                        PuzzleEditorLaunchRequest request = PuzzleEditorLaunchRequest.Capture(source, mode, 12345);
                        LevelDefinition baseline = request.CreateDefinition();
                        StartingBoardSearch search = new StartingBoardSearch(baseline, 12345);
                        while (!search.IsDone) search.Advance(128);
                        UnityEngine.Object.Destroy(baseline);
                        Check(search.Status == StartingBoardStatus.Success, mode + " 실제 맵 시작 보드 " + enabled);
                        void Load(Scene scene, LoadSceneMode loadMode)
                        {
                            if (scene.path != PuzzleGameAssets.ScenePath) return;
                            PuzzleGameSession loaded = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PuzzleGameSession>(true)).Single();
                            loaded.SoundEnabled = enabled;
                            loaded.InitializeAsync(request.CreateDefinition(), 12345, CancellationToken.None).Forget(Debug.LogException);
                        }
                        SceneManager.sceneLoaded += Load;
                        try { await EditorSceneManager.LoadSceneAsyncInPlayMode(PuzzleGameAssets.ScenePath, new LoadSceneParameters(LoadSceneMode.Single)); }
                        finally { SceneManager.sceneLoaded -= Load; }
                        PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                        float deadline = Time.realtimeSinceStartup + 30;
                        while (!session.IsReady && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                        Check(session.IsReady && !session.HasFailed, mode + " 소유 요청 실제 씬 준비 " + enabled);
                        session.enabled = false; Invoke(session, "TickProgress", .7f);
                        Check(Snapshot(session.State) == Snapshot(new BoardActionExecutor(search.State).State), mode + " 원본 미션 유지 초기 전체 상태 동등 " + enabled);
                        List<PuzzleFeedbackCueKind> heard = new List<PuzzleFeedbackCueKind>();
                        void Played(PuzzleFeedbackCueKind kind) { heard.Add(kind); }
                        session.AudioPlayback.Played += Played;
                        try
                        {
                            ActionCandidate candidate = ActionQuery.Find(session.State).First(action => action.Kind == QueryActionKind.SwapMatch);
                            BoardActionExecutor direct = new BoardActionExecutor(session.State);
                            direct.Swap(candidate.First, candidate.Second.Value);
                            session.enabled = true; Check(session.TrySwap(candidate.First, candidate.Second.Value), mode + " 원본 맵 유효 교환 " + enabled); session.enabled = false;
                            BoardActionExecutor executor = (BoardActionExecutor)typeof(PuzzleGameSession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                            for (int frame = 0; frame < 20000 && (session.IsPresenting || executor.HasPendingCascade || session.HasProgressFeedback); frame++) await Frame(session, .02f);
                            while (direct.HasPendingCascade) direct.AdvanceCascade();
                            Check(!session.HasFailed && !session.IsPresenting && !executor.HasPendingCascade && Snapshot(session.State) == Snapshot(direct.State) &&
                                session.Phase == direct.Phase && Snapshot(session.Outcome) == Snapshot(direct.Outcome), mode + " 소리 on/off 최종 보드·미션·이동·공급·난수·승패 동등 " + enabled);
                            Check(enabled ? heard.Contains(PuzzleFeedbackCueKind.Swap) && heard.Contains(PuzzleFeedbackCueKind.Match) : heard.Count == 0, mode + " 실제 교환·제거 소리 설정 " + enabled);
                            Check(session.AudioPlayback.ClipCount == 14 && session.AudioPlayback.SourceCount == 8, mode + " 재진입 클립·음성 한 세트 " + enabled);
                        }
                        finally { session.AudioPlayback.Played -= Played; session.enabled = true; }
                    }
                Check(JsonUtility.ToJson(source) == original, "소리 on/off Asset·MemoryPack 시험 뒤 원본 레벨 불변");
            }
            finally { Time.timeScale = previous; }
        }

        private static async UniTask RelatedRecoveryChecks()
        {
            PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
            session.SoundEnabled = true;
            List<PuzzleFeedbackCueKind> heard = new List<PuzzleFeedbackCueKind>();
            void Played(PuzzleFeedbackCueKind kind) { heard.Add(kind); }
            session.AudioPlayback.Played += Played;
            float previous = Time.timeScale; Time.timeScale = 0;
            try
            {
                // 기존 실제 회수·3아이템 검사도 같은 재생기를 사용해 규칙 결과를 비교한다.
                await (UniTask)typeof(PuzzleProgressFeedbackVerification).GetMethod("RecoveryAndItemChecks", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                Check(heard.Contains(PuzzleFeedbackCueKind.Landing), "실제 낙하·공급 회수 착지 소리");
                Check(heard.Contains(PuzzleFeedbackCueKind.MissionComplete), "실제 회수 도착의 미션 완료음");
                Check(session.AudioPlayback.ClipCount == 14 && session.AudioPlayback.SourceCount == 8, "회수·3아이템 이후 동일 소리 풀 재사용");
            }
            finally { session.AudioPlayback.Played -= Played; Time.timeScale = previous; }
        }
    }
}

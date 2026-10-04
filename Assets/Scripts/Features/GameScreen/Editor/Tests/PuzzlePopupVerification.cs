using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using PopupUI;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static partial class PuzzlePopupVerification
    {
        private const string Key = "PopupFramework.Stage03.Game";
        private const string Output = "Logs/PopupFramework/";
        private static readonly List<string> results = new List<string>();
        static PuzzlePopupVerification()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                { SessionState.EraseBool(Key); VerifyAsync().Forget(Debug.LogException); }
            };
        }
        public static void RunScene()
        {
            Directory.CreateDirectory(Output); SessionState.SetBool(Key, true);
            PuzzleUIRenderVerification.RememberSize(); PuzzleGameSceneVerification.OpenInteractive();
        }
        public static void RunRestart()
        {
            SessionState.SetBool(Key + ".Restart", true); RunScene();
        }
        public static void RunResult()
        {
            SessionState.SetBool(Key + ".Result", true); RunScene();
        }
        public static void RunRestore()
        {
            SessionState.SetBool(Key + ".Restore", true); RunResult();
        }
        public static void ApplyPrefabConnections()
        {
            results.Clear(); int exit = 0;
            try
            {
                PuzzlePopupAssets.Apply();
                Check(true, "게임 세 팝업 프리팹 catalog Host Binding 연결 저장 씬 저장0");
            }
            catch (Exception error) { exit = 1; results.Add("FAIL " + error); }
            finally
            { Directory.CreateDirectory(Output); File.WriteAllLines(Output + "stage03-prefab-results.txt", results); EditorApplication.Exit(exit); }
        }
        private static void Check(bool value, string name)
        { if (!value) throw new InvalidOperationException(name); results.Add("PASS " + name); }
        private static async UniTask VerifyAsync()
        {
            results.Clear(); int exit = 0;
            try
            {
                await UniTask.Yield();
                PuzzleScreenView screen = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenView>();
                Check(screen != null, "실제 게임 화면 생성");
                PuzzlePopupBinding binding = screen.GetComponent<PuzzlePopupBinding>();
                Check(binding != null && binding.Service != null && screen.GetComponentInChildren<PopupHost>(true) != null, "실제 게임 프리팹 공통 팝업 Binding Host 연결");
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                await UniTask.WaitUntil(() => session.CanAcceptInput || session.HasFailed).Timeout(TimeSpan.FromSeconds(60));
                Check(session.CanAcceptInput, "실제 게임 팝업 시험 준비: " + session.Message);
                if (SessionState.GetBool(Key + ".Result", false))
                {
                    SessionState.EraseBool(Key + ".Result");
                    await ResultChecks(binding, session);
                    if (SessionState.GetBool(Key + ".Restore", false))
                    {
                        SessionState.EraseBool(Key + ".Restore");
                        await RestoreChecks(screen, binding, session);
                    }
                    return;
                }
                if (SessionState.GetBool(Key + ".Restart", false))
                {
                    SessionState.EraseBool(Key + ".Restart");
                    await RestartChecks(binding, session); return;
                }
                PopupHandle description = binding.OpenDescription("미션 설명 시험");
                PopupHandle pause = binding.OpenPause();
                Check(binding.Service.Count == 2 && binding.Service.Top == pause && session.IsPaused, "설명 뒤 일시정지 중첩과 정지 요청");
                binding.Service.Close(description);
                Check(binding.Service.Count == 1 && binding.Service.Top == pause && session.IsPaused, "중간 설명 제거 일시정지 순서 보존");
                binding.Service.Close(pause);
                Check(binding.Service.Count == 0 && !session.IsPaused, "마지막 팝업 종료 자신의 정지 해제");
                await InputAndOwnershipChecks(screen, binding, session);
            }
            catch (Exception error) { exit = 1; results.Add("FAIL " + error); }
            finally
            {
                PuzzleUIRenderVerification.RestoreSize();
                results.Add("UTC " + DateTime.UtcNow.ToString("O")); File.WriteAllLines(Output + "stage03-game-results.txt", results);
                EditorApplication.Exit(exit);
            }
        }
    }
}

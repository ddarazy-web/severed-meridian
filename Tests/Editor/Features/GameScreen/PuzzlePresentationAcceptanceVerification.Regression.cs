using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzlePresentationAcceptanceVerification
    {
        public static void SettlementScene()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            SessionState.SetBool("Stage12.Settlement", true);
            EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath); EditorApplication.EnterPlaymode();
        }

        private static async UniTask SettlementRegressionAsync()
        {
            List<string> readiness = new List<string>();
            try
            {
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                float began = Time.realtimeSinceStartup; int startFrame = Time.frameCount;
                for (int iteration = 0; iteration < 1800 && !session.CanAcceptInput && !session.HasFailed; iteration++) await UniTask.Yield();
                readiness.Add("1800-budget elapsed=" + (Time.realtimeSinceStartup - began) + " actualFrames=" + (Time.frameCount - startFrame) +
                    " ready=" + session.IsReady + " input=" + session.CanAcceptInput + " failed=" + session.HasFailed + " timeScale=" + Time.timeScale +
                    " startRemaining=" + Field(session, "startRemaining") + " message=" + session.Message);
                // 프레임 횟수는 준비 시간의 상한이 아니다. 기존 검사 본문을 바꾸지 않고 실제 준비를 기다린다.
                float deadline = began + 30;
                Application.runInBackground = true;
                while (!session.CanAcceptInput && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.NextFrame();
                readiness.Add("actual-ready elapsed=" + (Time.realtimeSinceStartup - began) + " actualFrames=" + (Time.frameCount - startFrame) +
                    " input=" + session.CanAcceptInput + " failed=" + session.HasFailed + " startRemaining=" + Field(session, "startRemaining"));
                File.WriteAllLines(Output + "settlement-readiness.txt", readiness);
                if (!session.CanAcceptInput || session.HasFailed) throw new InvalidOperationException("실제 준비 실패: " + session.Message);
                await (UniTask)typeof(PuzzleSettlementAnimationVerification).GetMethod("VerifySceneAsync", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            }
            catch (Exception error)
            {
                readiness.Add("FAIL " + error); File.WriteAllLines(Output + "settlement-readiness.txt", readiness);
                File.WriteAllText("Logs/PuzzleSettlementAnimationVerification/scene-results.txt", "FAIL " + error);
                EditorApplication.Exit(1);
            }
        }
    }
}

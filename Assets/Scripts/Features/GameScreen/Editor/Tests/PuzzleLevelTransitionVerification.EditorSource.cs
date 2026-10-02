using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzleLevelTransitionVerification
    {
        private const string SourceAudit = "Stage13.SourceAudit";
        private const string LifecycleOutput = "Logs/PuzzleEditorLaunchVerification/lifecycle-results.txt";

        [InitializeOnLoadMethod]
        private static void RegisterSourceAudit()
        {
            PuzzleEditorLauncher.Finished += (owner, message) =>
            {
                if (!SessionState.GetBool(SourceAudit, false)) return;
                bool cleared = SessionState.GetInt("Puzzle.EditorLaunch.source", int.MinValue) == int.MinValue;
                File.AppendAllText(Output + "source-observations.txt", (cleared ? "PASS " : "FAIL ") + "실행 종료 source SessionState 정리 owner=" + owner + "\n");
            };
            EditorApplication.update += ObserveSourceLifecycle;
        }

        public static void RunEditorLifecycle()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            if (EditorSceneManager.GetActiveScene().path == "") EditorSceneManager.OpenScene("Assets/Scenes/game.unity");
            Directory.CreateDirectory(Output);
            if (File.Exists(LifecycleOutput) && !File.Exists(Output + "lifecycle-before.txt")) File.Copy(LifecycleOutput, Output + "lifecycle-before.txt");
            File.WriteAllText(Output + "source-observations.txt", "");
            SessionState.SetBool(SourceAudit, true);
            SessionState.SetInt(SourceAudit + ".case", -1);
            PuzzleEditorLaunchLifecycleVerification.Run();
        }

        private static void ObserveSourceLifecycle()
        {
            if (!SessionState.GetBool(SourceAudit, false)) return;
            int index = SessionState.GetInt("StageThree.LifecycleTest.case", -1);
            if (EditorApplication.isPlaying && index >= 0 && index != 8 && index != 9 && index != 10 &&
                SessionState.GetInt(SourceAudit + ".case", -1) != index)
            {
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                if (session != null && session.IsReady)
                {
                    SessionState.SetInt(SourceAudit + ".case", index);
                    bool expected = index % 4 >= 2;
                    bool matched = session.LevelAdvanceEnabled == expected && SessionState.GetInt("Puzzle.EditorLaunch.source", -1) == (expected ? 1 : 0);
                    File.AppendAllText(Output + "source-observations.txt", (matched ? "PASS " : "FAIL ") + "실제 에디터 선택 소스 전달 case=" + index + " MemoryPack=" + expected + "\n");
                }
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetInt("StageThree.LifecycleTest.phase", -1) != 0 || !File.Exists(LifecycleOutput)) return;
            string[] reused = File.ReadAllLines(LifecycleOutput);
            if (!reused.Contains("PASS LifecycleSuiteComplete") && !reused.Any(line => line.StartsWith("FAIL "))) return;
            string[] own = File.ReadAllLines(Output + "source-observations.txt");
            int sourceCount = own.Count(line => line.StartsWith("PASS 실제 에디터 선택 소스 전달"));
            string[] combined = reused.Select(line => line.Replace("PASS ", "PASS 기존 lifecycle ")).Concat(own).Concat(new[]
                { (sourceCount == 9 ? "PASS " : "FAIL ") + "소스9사례 관찰 수=" + sourceCount }).ToArray();
            File.WriteAllLines(Output + "editor-lifecycle-results.txt", combined);
            SessionState.EraseBool(SourceAudit); SessionState.EraseInt(SourceAudit + ".case");
            EditorApplication.Exit(combined.Any(line => line.StartsWith("FAIL ")) ? 1 : 0);
        }
    }
}

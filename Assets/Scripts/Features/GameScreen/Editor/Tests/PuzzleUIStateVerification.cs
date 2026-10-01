using System;
using System.IO;
using System.Reflection;
using UnityEditor;

namespace GameScreen.Editor
{
    public static class PuzzleUIStateVerification
    {
        public const string Output = "Logs/PuzzleUIVerification/";

        [MenuItem("Tools/Match/UI 세션 계약 검증")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            int failures = 0;
            using StreamWriter log = new StreamWriter(Output + "contract-results.txt");
            Type type = typeof(PuzzleGameSession);
            foreach (string name in new[] { "IsPaused", "IsRestarting", "CanUseItems" })
            {
                bool found = type.GetProperty(name) != null;
                log.WriteLine((found ? "PASS " : "FAIL ") + name);
                if (!found) failures++;
            }
            foreach (string name in new[] { "SetPaused", "RestartAsync", "CanSelectItemTarget", "TryUseItem" })
            {
                bool found = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public) != null;
                log.WriteLine((found ? "PASS " : "FAIL ") + name);
                if (!found) failures++;
            }
            log.Flush();
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
            if (failures != 0) throw new InvalidOperationException("UI 세션 계약 누락: " + failures);
        }
    }
}

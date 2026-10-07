using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzlePresentationAcceptanceVerification
    {
        private static void FinishGameView(int exit, string resultFile)
        {
            try { PuzzleUIRenderVerification.RestoreSize(); Check(true, "Game View 선택·크기 목록 cleanup 복원 성공"); }
            catch (Exception error) { results.Add("FAIL cleanup " + error); exit = 1; }
            File.WriteAllLines(Output + resultFile, results); EditorApplication.Exit(exit);
        }

        public static void GameViewCleanupFailure()
        {
            results.Clear(); PuzzleUIRenderVerification.RememberSize();
            // 실제 설정은 바꾸지 않고 기대 목록만 훼손해 정리 실패의 결과/종료 전파를 검사한다.
            string saved = SessionState.GetString("StageFour.SizeSnapshot", "");
            SessionState.SetString("StageFour.SizeSnapshot", saved.Replace("\"Entries\":[", "\"Entries\":[\"forced-cleanup-failure\","));
            FinishGameView(0, "cleanup-failure-results.txt");
        }

        public static void GameViewRestoration()
        {
            results.Clear(); int exit = 0;
            Assembly assembly = typeof(EditorWindow).Assembly;
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            object sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { sizesType.GetProperty("currentGroupType").GetValue(sizes) });
            Type viewType = assembly.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(viewType);
            PropertyInfo selected = viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int originalSelected = (int)selected.GetValue(view);
            int originalCustom = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
            string original = GameViewSnapshot(group, originalSelected);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "game-view-before.json", original);
            try
            {
                // 같은 이름의 기존 항목도 이번 실행이 추가한 항목과 구분해 보존한다.
                Type sizeType = assembly.GetType("UnityEditor.GameViewSize"), mode = assembly.GetType("UnityEditor.GameViewSizeType");
                object ownedFixture = Activator.CreateInstance(sizeType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                    new[] { Enum.ToObject(mode, 1), (object)1231, 779, "Stage04 1231x779" }, null);
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { ownedFixture });
                int builtIn = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
                selected.SetValue(view, builtIn + originalCustom);
                string before = GameViewSnapshot(group, (int)selected.GetValue(view));
                File.WriteAllText(Output + "game-view-fixture-before.json", before);
                PuzzleUIRenderVerification.RememberSize();
                PuzzleUIRenderVerification.SetSize(1234, 777);
                PuzzleUIRenderVerification.SetSize(1235, 778);
                PuzzleUIRenderVerification.SetSize(1234, 777);
                PuzzleUIRenderVerification.RestoreSize();
                string after = GameViewSnapshot(group, (int)selected.GetValue(view));
                File.WriteAllText(Output + "game-view-fixture-after.json", after);
                Check(before == after, "Game View 전후 선택·전체 크기 목록 동등 및 기존 Stage04 항목 보존");
                PuzzleUIRenderVerification.RememberSize();
                PuzzleUIRenderVerification.SetSize(1231, 779);
                PuzzleUIRenderVerification.RestoreSize();
                Check(before == GameViewSnapshot(group, (int)selected.GetValue(view)), "기존 크기 재사용·추가0 복원");
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally
            {
                try
                {
                    int builtIn = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
                    int custom = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
                    for (int index = custom - 1; index >= originalCustom; index--)
                        group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { builtIn + index });
                    selected.SetValue(view, originalSelected);
                    string restored = GameViewSnapshot(group, (int)selected.GetValue(view));
                    File.WriteAllText(Output + "game-view-after.json", restored);
                    Check(original == restored, "검사 소유 fixture 제거·원래 Game View 완전 복원");
                }
                catch (Exception error) { results.Add("FAIL cleanup " + error); exit = 1; }
                File.WriteAllLines(Output + "game-view-results.txt", results); EditorApplication.Exit(exit);
            }
        }

        private static string GameViewSnapshot(object group, int selected)
        {
            int builtIn = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
            int custom = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
            string[] entries = Enumerable.Range(0, builtIn + custom).Select(index =>
            {
                object size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { index });
                return index + ":" + size.GetType().GetProperty("width").GetValue(size) + "x" + size.GetType().GetProperty("height").GetValue(size)
                    + ":" + size.GetType().GetProperty("sizeType").GetValue(size) + ":" + size.GetType().GetProperty("baseText").GetValue(size);
            }).ToArray();
            return JsonUtility.ToJson(new GameViewEvidence { Selected = selected, Entries = entries }, true);
        }
        [Serializable] private sealed class GameViewEvidence { public int Selected; public string[] Entries; }
    }
}

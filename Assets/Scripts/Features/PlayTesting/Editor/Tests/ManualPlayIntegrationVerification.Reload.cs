using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class ManualPlayIntegrationVerification
    {
        private const string ReloadKey = "Match.ManualPlayIntegrationVerification.Reload";
        private static int reloadMarker;
        private static int reloadWait;

        public static void Reload()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            folder = "Assets/__ManualPlayReload_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            level = (LevelDefinition)Invoke(typeof(ItemBoosterVerification), "PlayFixture");
            AssetDatabase.CreateAsset(level, folder + "/Reload.asset"); AssetDatabase.SaveAssetIfDirty(level);
            LevelInitialStateWindow.OpenManualLevel(level);
            window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().Single().ActiveSimulationPanel;
            reloadWait = 0; EditorApplication.update += BeforeReload;
        }

        private static void BeforeReload()
        {
            if (window.IsSearching && reloadWait++ < 5000) return;
            EditorApplication.update -= BeforeReload;
            try
            {
                Click(window.rootVisualElement, "manual-start");
                Check(window.Execution != null, "도메인 재로드 전 실제 실행 세션");
                string path = AssetDatabase.GetAssetPath(level);
                File.WriteAllText(Evidence + "/reload-state.json", JsonUtility.ToJson(new Saved { path = path, folder = folder, json = JsonUtility.ToJson(level),
                    guid = AssetDatabase.AssetPathToGUID(path), process = System.Diagnostics.Process.GetCurrentProcess().Id,
                    tab = window.Owner.WorkspaceTab, seed = window.rootVisualElement.Q<IntegerField>("initial-seed").value }));
                File.WriteAllLines(Evidence + "/reload-results.txt", Results);
                reloadMarker = 1; SessionState.SetBool(ReloadKey, true);
                EditorUtility.RequestScriptReload();
            }
            catch (Exception error) { ReloadFailed(error); }
        }

        [InitializeOnLoadMethod]
        private static void ResumeReloadVerification()
        {
            if (SessionState.GetBool(ReloadKey, false)) EditorApplication.update += AfterReload;
        }

        private static void AfterReload()
        {
            window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().SingleOrDefault()?.ActiveSimulationPanel;
            if ((window?.rootVisualElement == null || window.rootVisualElement.Q<Button>("manual-start") == null) && reloadWait++ < 5000) return;
            EditorApplication.update -= AfterReload; SessionState.EraseBool(ReloadKey);
            try
            {
                Results.Clear(); Results.AddRange(File.ReadAllLines(Evidence + "/reload-results.txt"));
                Saved saved = JsonUtility.FromJson<Saved>(File.ReadAllText(Evidence + "/reload-state.json"));
                Check(reloadMarker == 0 && saved.process == System.Diagnostics.Process.GetCurrentProcess().Id, "동일 프로세스 실제 도메인 재로드 확인");
                Check(window != null && window.Execution == null && window.CurrentState == null && !window.CascadeRunning, "도메인 재로드 후 이전 세션·예약 없음");
                Check(window.rootVisualElement.Q<Label>("initial-status").text.Contains("다시 시작"), "도메인 재로드 후 재시작 안내");
                LevelDefinition loaded = AssetDatabase.LoadAssetAtPath<LevelDefinition>(saved.path);
                Check(window.Owner.WorkspaceTab == saved.tab && window.Owner.CurrentLevel == loaded && window.rootVisualElement.Q<IntegerField>("initial-seed").value == saved.seed,
                    "도메인 재로드 후 탭·공통 레벨·시드 복원");
                Check(JsonUtility.ToJson(loaded) == saved.json && AssetDatabase.AssetPathToGUID(saved.path) == saved.guid, "도메인 재로드 정의·GUID 보존");
                window.Owner.Close();
                if (!saved.folder.StartsWith("Assets/__ManualPlayReload_", StringComparison.Ordinal) || saved.folder.Contains("..")) throw new InvalidOperationException("임시 경로 오류");
                Check(AssetDatabase.DeleteAsset(saved.folder), "재로드 소유 임시 에셋 정리");
                File.Move(Evidence + "/reload-state.json", Evidence + "/reload-completed-" + DateTime.UtcNow.Ticks + ".json");
                File.WriteAllLines(Evidence + "/reload-results.txt", Results); EditorApplication.Exit(0);
            }
            catch (Exception error) { ReloadFailed(error); }
        }

        private static void ReloadFailed(Exception error)
        {
            SessionState.EraseBool(ReloadKey); Results.Add("FAIL " + error);
            File.WriteAllLines(Evidence + "/reload-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
}

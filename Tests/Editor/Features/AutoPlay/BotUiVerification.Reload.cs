using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class BotUiVerification
    {
        private const string ReloadKey = "Match.BotUiVerification.Reload";
        private static string ReloadEvidence => SessionState.GetBool(ReloadKey + ".planning", false) ? "Logs/BotPlanningVerification" : Evidence;
        private static int reloadMarker, reloadWait;
        [Serializable] private sealed class ReloadData { public string folder, path, json, disk, guid; public int process; }

        /// <summary>기존 실제 재로드 시나리오를 계획 계산 도중에도 실행한다. 결과 파일은 25단계와 분리한다.</summary>
        public static void PlanningReload()
        {
            SessionState.SetBool(ReloadKey + ".planning", true);
            Reload();
        }

        /// <summary>소유 임시 에셋으로 봇 실행을 시작한 뒤 실제 스크립트 재로드를 요청한다.</summary>
        public static void Reload()
        {
            Directory.CreateDirectory(ReloadEvidence);
            string folder = "Assets/__BotReload_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            level = (LevelDefinition)typeof(ItemBoosterVerification).GetMethod("PlayFixture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            string path = folder + "/Bot.asset";
            AssetDatabase.CreateAsset(level, path); AssetDatabase.SaveAssetIfDirty(level);
            string disk = File.ReadAllText(path);
            JsonUtility.FromJsonOverwrite("{\"levelNumber\":252}", level); EditorUtility.SetDirty(level);
            SessionState.SetString(ReloadKey + ".data", JsonUtility.ToJson(new ReloadData { folder = folder, path = path,
                json = JsonUtility.ToJson(level), disk = disk, guid = AssetDatabase.AssetPathToGUID(path), process = System.Diagnostics.Process.GetCurrentProcess().Id }));
            window = LevelEditorWindow.OpenWorkspace(1, level, true); panel = window.ActiveSimulationPanel;
            panel.rootVisualElement.Q<Foldout>("bot-trial").value = true;
            if (SessionState.GetBool(ReloadKey + ".planning", false)) panel.rootVisualElement.Q<PopupField<string>>("bot-strategy").value = "계획";
            Click(panel.rootVisualElement, "bot-new");
            EditorApplication.update += BeforeReload;
        }

        /// <summary>봇 시작 구성이 끝나면 행동 처리 도중 도메인을 재로드한다.</summary>
        private static void BeforeReload()
        {
            if (Session.NeedsAdvance && reloadWait++ < 5000) return;
            EditorApplication.update -= BeforeReload;
            try
            {
                Check(Session.Status == BotSessionStatus.Ready, "실제 재로드 전 봇 준비");
                Click(panel.rootVisualElement, "bot-run"); ForceTick();
                if (SessionState.GetBool(ReloadKey + ".planning", false))
                    Check(Session.Status == BotSessionStatus.Running && Session.IsPlanning && Session.LastChoice == null, "실제 재로드 전 계획 계산 중·미제출 상태");
                else Check(Session.Status == BotSessionStatus.Running && Session.LastChoice != null, "실제 재로드 전 행동·후속 처리 중");
                File.WriteAllLines(ReloadEvidence + "/reload-results.txt", Results);
                reloadMarker = 1; SessionState.SetBool(ReloadKey, true);
                EditorUtility.RequestScriptReload();
            }
            catch (Exception error) { EndReload(error); }
        }

        /// <summary>이 전용 검증이 요청한 실제 재로드에만 이어서 검사한다.</summary>
        [InitializeOnLoadMethod]
        private static void ResumeReload()
        {
            if (SessionState.GetBool(ReloadKey, false)) EditorApplication.update += AfterReload;
        }

        /// <summary>정적 상태 초기화와 기존 창 복구 뒤 이전 봇 예약이 사라졌는지 확인한다.</summary>
        private static void AfterReload()
        {
            window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().SingleOrDefault();
            panel = window?.ActiveSimulationPanel;
            if (panel?.rootVisualElement?.Q<Button>("bot-new") == null && reloadWait++ < 5000) return;
            EditorApplication.update -= AfterReload;
            try
            {
                Results.Clear(); Results.AddRange(File.ReadAllLines(ReloadEvidence + "/reload-results.txt"));
                ReloadData saved = JsonUtility.FromJson<ReloadData>(SessionState.GetString(ReloadKey + ".data", ""));
                Check(reloadMarker == 0 && saved.process == System.Diagnostics.Process.GetCurrentProcess().Id, "동일 프로세스 실제 도메인 재로드 확인");
                Check(panel != null && Session == null && panel.CurrentState == null && panel.Execution == null && !panel.CascadeRunning,
                    "재로드 뒤 봇·수동 실행기와 이전 보드·예약 없음");
                Check(panel.rootVisualElement.Q<Label>("initial-status").text.Contains("다시 시작"), "재로드 후 재시작 안내 표시");
                Check(!panel.rootVisualElement.Q<Button>("bot-step").enabledInHierarchy && panel.rootVisualElement.Q<Button>("bot-new").enabledInHierarchy,
                    "재로드 후 이전 판 입력 차단·새 시험만 가능");
                if (SessionState.GetBool(ReloadKey + ".planning", false))
                    Check(!panel.rootVisualElement.Q<Button>("bot-repeat").enabledInHierarchy, "계획 재로드 후 과거 비교 조건·실행 예약 폐기");
                level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(saved.path);
                Check(JsonUtility.ToJson(level) == saved.json && EditorUtility.IsDirty(level) && File.ReadAllText(saved.path) == saved.disk &&
                    AssetDatabase.AssetPathToGUID(saved.path) == saved.guid, "재로드 전 미저장 JSON·dirty·디스크·GUID 보존");
                Check(window.CurrentLevel == level && window.WorkspaceTab == 1, "재로드 뒤 레벨과 플레이 탭 선택 보존");
                EndReload(null);
            }
            catch (Exception error) { EndReload(error); }
        }

        /// <param name="error">검사 실패. null이면 성공.</param>
        private static void EndReload(Exception error)
        {
            EditorApplication.update -= BeforeReload; EditorApplication.update -= AfterReload;
            if (error != null) Results.Add("FAIL " + error);
            ReloadData saved = JsonUtility.FromJson<ReloadData>(SessionState.GetString(ReloadKey + ".data", ""));
            if (window != null) { window.SetLevel(null); window.Close(); }
            if (saved != null && saved.folder.StartsWith("Assets/__BotReload_", StringComparison.Ordinal) && !saved.folder.Contains(".."))
            {
                bool removed = AssetDatabase.DeleteAsset(saved.folder);
                Results.Add((removed ? "PASS " : "FAIL ") + "소유 재로드 임시 에셋·메타 정리");
            }
            SessionState.EraseBool(ReloadKey); SessionState.EraseString(ReloadKey + ".data");
            File.WriteAllLines(ReloadEvidence + "/reload-results.txt", Results);
            SessionState.EraseBool(ReloadKey + ".planning");
            EditorApplication.Exit(Results.Any(line => line.StartsWith("FAIL")) ? 1 : 0);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Board;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class BotBatchUiVerification
    {
        private const string ReloadKey = "Match.BatchReloadVerification";
        private static int marker, reloadWait;
        [Serializable] private sealed class ReloadData
        {
            public string folder, path, json, disk, guid, pointer, batchId;
            public bool pointerExisted;
            public int process;
        }

        /// <summary>전용 임시 에셋으로 실제 재로드를 검사한다. 기존 최근 시험 포인터는 끝에서 복원한다.</summary>
        public static void Reload()
        {
            Directory.CreateDirectory("Logs/BotBatchVerification");
            Results.Clear();
            string folder = "Assets/__BatchReload_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            BoardCoordinate[] cells = Enumerable.Range(0, 16).Select(i => new BoardCoordinate(i / 4, i % 4)).ToArray();
            level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { cells });
            JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[],\"moveCount\":3,\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
            string path = folder + "/Batch.asset";
            AssetDatabase.CreateAsset(level, path); AssetDatabase.SaveAssetIfDirty(level);
            string disk = File.ReadAllText(path);
            JsonUtility.FromJsonOverwrite("{\"levelNumber\":272}", level); EditorUtility.SetDirty(level);
            string pointer = Path.Combine(BotBatchStore.DefaultRoot, "latest.txt");
            ReloadData data = new ReloadData { folder = folder, path = path, disk = disk, json = JsonUtility.ToJson(level),
                guid = AssetDatabase.AssetPathToGUID(path), process = System.Diagnostics.Process.GetCurrentProcess().Id,
                pointerExisted = File.Exists(pointer), pointer = File.Exists(pointer) ? File.ReadAllText(pointer) : "" };
            SessionState.SetString(ReloadKey + ".data", JsonUtility.ToJson(data));
            window = LevelEditorWindow.OpenWorkspace(1, level, true); panel = window.ActiveSimulationPanel;
            panel.rootVisualElement.Q<Foldout>("bot-trial").value = true;
            panel.rootVisualElement.Q<IntegerField>("batch-count").value = 2;
            Click(panel.rootVisualElement, "batch-new");
            // 도메인 복구 조건을 고정해 검사한다. 새 시드 UI 자체는 별도 UI 검사에서 확인했다.
            Click(panel.rootVisualElement, "batch-stop");
            BotBatchRecord condition = Batch.Record; condition.seeds = new[] { 771, 772 };
            Click(panel.rootVisualElement, "batch-repeat");
            EditorApplication.update += BeforeBatchReload;
        }

        private static void BeforeBatchReload()
        {
            try
            {
                if (Batch?.Current?.IsPlanning != true || Batch.Record.finished != 1)
                {
                    if (reloadWait++ > 10000 || Batch?.CanContinue != true) throw new InvalidOperationException("재로드 전 계획 탐색 경계에 도달하지 못했습니다.");
                    return;
                }
                EditorApplication.update -= BeforeBatchReload;
                Check(Batch.Record.basicFinished == 1, "실제 재로드 전 완료된 기본 판 1개");
                Click(panel.rootVisualElement, "batch-pause");
                Check(Batch.Record.status == BotBatchStatus.Paused && Batch.Current.IsPlanning, "계획 탐색 중 일시정지 상태에서 재로드 요청");
                ReloadData data = JsonUtility.FromJson<ReloadData>(SessionState.GetString(ReloadKey + ".data", ""));
                data.batchId = Batch.Record.id; SessionState.SetString(ReloadKey + ".data", JsonUtility.ToJson(data));
                File.WriteAllLines("Logs/BotBatchVerification/reload-results.txt", Results);
                marker = 1; SessionState.SetBool(ReloadKey, true); EditorUtility.RequestScriptReload();
            }
            catch (Exception error) { EndBatchReload(error); }
        }

        [InitializeOnLoadMethod]
        private static void ResumeBatchReload()
        {
            if (SessionState.GetBool(ReloadKey, false)) EditorApplication.update += AfterBatchReload;
            else if (!string.IsNullOrEmpty(SessionState.GetString(ReloadKey + ".data", "")))
                EditorApplication.delayCall += () => {
                    window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().SingleOrDefault();
                    EndBatchReload(new InvalidOperationException("준비 도중 재로드된 검사 정리. 정상 재로드 검사는 다시 실행해야 합니다."));
                };
        }

        private static void AfterBatchReload()
        {
            window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().SingleOrDefault(); panel = window?.ActiveSimulationPanel;
            if (panel?.rootVisualElement?.Q<Button>("batch-new") == null && reloadWait++ < 10000) return;
            EditorApplication.update -= AfterBatchReload;
            try
            {
                Results.Clear(); Results.AddRange(File.ReadAllLines("Logs/BotBatchVerification/reload-results.txt"));
                ReloadData data = JsonUtility.FromJson<ReloadData>(SessionState.GetString(ReloadKey + ".data", ""));
                Check(marker == 0 && data.process == System.Diagnostics.Process.GetCurrentProcess().Id, "실제 동일 프로세스 도메인 재로드 확인");
                Check(panel != null && Batch == null && panel.CurrentState == null, "재로드 후 이전 실행 객체와 보드 복원하지 않음");
                BotBatchRecord record = new BotBatchStore().LoadLatest();
                Check(record.id == data.batchId && record.finished == 1 && record.stopped == 1 && record.Unrun == 2 &&
                    record.status == BotBatchStatus.Interrupted, "실제 재로드 후 완료 판 보존·진행 판 중단·미실행 구분");
                Check(panel.rootVisualElement.Q<Button>("batch-repeat").enabledInHierarchy &&
                    !panel.rootVisualElement.Q<Button>("batch-pause").enabledInHierarchy, "과거 판 재개 금지·저장 조건 재시험 제공");
                level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(data.path);
                Check(JsonUtility.ToJson(level) == data.json && EditorUtility.IsDirty(level) && File.ReadAllText(data.path) == data.disk &&
                    AssetDatabase.AssetPathToGUID(data.path) == data.guid, "실제 재로드 후 미저장 값·dirty·디스크·GUID 보존");
                Click(panel.rootVisualElement, "batch-repeat");
                Check(Batch.Record.id != record.id && Batch.Record.seeds.SequenceEqual(record.seeds) && Batch.Record.definitionJson == record.definitionJson,
                    "재로드 후 실제 저장 사본과 시드로 새 묶음 재시험");
                EndBatchReload(null);
            }
            catch (Exception error) { EndBatchReload(error); }
        }

        private static void EndBatchReload(Exception error)
        {
            EditorApplication.update -= BeforeBatchReload; EditorApplication.update -= AfterBatchReload;
            if (error != null) Results.Add("FAIL " + error);
            ReloadData data = JsonUtility.FromJson<ReloadData>(SessionState.GetString(ReloadKey + ".data", ""));
            if (window != null) { window.Close(); window = null; }
            if (data != null)
            {
                if (data.folder.StartsWith("Assets/__BatchReload_", StringComparison.Ordinal) && !data.folder.Contains(".."))
                    Results.Add((AssetDatabase.DeleteAsset(data.folder) ? "PASS " : "FAIL ") + "검사 소유 임시 에셋·메타 정리");
                string pointer = Path.Combine(BotBatchStore.DefaultRoot, "latest.txt");
                if (data.pointerExisted) File.WriteAllText(pointer, data.pointer); else File.Delete(pointer);
            }
            SessionState.EraseBool(ReloadKey); SessionState.EraseString(ReloadKey + ".data");
            File.WriteAllLines("Logs/BotBatchVerification/reload-results.txt", Results);
            EditorApplication.Exit(Results.Any(r => r.StartsWith("FAIL")) ? 1 : 0);
        }
    }
}

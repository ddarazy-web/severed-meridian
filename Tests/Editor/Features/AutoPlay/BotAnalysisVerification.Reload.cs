using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class BotAnalysisVerification
    {
        private const string AnalysisReloadKey = "Match.AnalysisReloadVerification";
        private static int reloadMarker;
        [Serializable] private sealed class AnalysisReloadData
        {
            public string asset, directory, guid, disk, json, record, header;
            public int process;
        }

        public static void Reload()
        {
            Results.Clear(); Directory.CreateDirectory(Evidence);
            uiSequence = BeforeAnalysisReload(); EditorApplication.update += ReloadTick;
        }

        private static void ReloadTick()
        {
            if (EditorApplication.timeSinceStartup < uiNext) return;
            uiNext = EditorApplication.timeSinceStartup + .1;
            try { if (uiSequence.MoveNext()) return; FinishAnalysisReload(null); }
            catch (Exception error) { FinishAnalysisReload(error); }
        }

        private static IEnumerator BeforeAnalysisReload()
        {
            BotAnalysisReader source = Load(File.ReadAllText(Evidence + "/source-path.txt"));
            string folder = "Assets/__AnalysisReload_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            uiLevel = ScriptableObject.CreateInstance<LevelDefinition>();
            JsonUtility.FromJsonOverwrite(source.Record.definitionJson, uiLevel);
            string asset = folder + "/Original.asset";
            AssetDatabase.CreateAsset(uiLevel, asset); AssetDatabase.SaveAssetIfDirty(uiLevel);
            AnalysisReloadData data = new AnalysisReloadData { asset = asset, directory = folder,
                guid = AssetDatabase.AssetPathToGUID(asset), disk = File.ReadAllText(asset),
                process = System.Diagnostics.Process.GetCurrentProcess().Id };
            // 원본 연결 메타데이터 검사에는 사용자 기록 대신 소유한 보관 사본만 수정한다.
            data.record = Path.GetFullPath(Evidence + "/reload-record-" + Guid.NewGuid().ToString("N"));
            using (BotAnalysisExport export = new BotAnalysisExport(source, data.record))
            { while (!export.IsDone) export.Advance(); Check(export.Error == null, "재로드 소유 보관본: " + export.Error); }
            BotBatchRecord metadata = JsonUtility.FromJson<BotBatchRecord>(File.ReadAllText(Path.Combine(data.record, "batch.json")));
            metadata.sourceGuid = data.guid; metadata.sourceName = "Original";
            data.header = JsonUtility.ToJson(metadata); File.WriteAllText(Path.Combine(data.record, "batch.json"), data.header);
            JsonUtility.FromJsonOverwrite("{\"levelNumber\":280}", uiLevel); EditorUtility.SetDirty(uiLevel);
            data.json = JsonUtility.ToJson(uiLevel);
            SessionState.SetString(AnalysisReloadKey, JsonUtility.ToJson(data));
            window = LevelEditorWindow.OpenWorkspace(3, uiLevel, true); yield return null;
            LevelAnalysisPanel panel = (LevelAnalysisPanel)typeof(LevelEditorWindow).GetField("analysisPanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
            panel.OpenRecord(data.record);
            for (int wait = 0; panel.Analysis?.IsDone != true && wait < 400; wait++) yield return null;
            Check(panel.Analysis?.Error == null && panel.Analysis?.Games.Count == 200, "재로드 전 사본 200판 읽기");
            Click("analysis-tab-cases"); yield return null;
            window.rootVisualElement.Q<ListView>("analysis-cases").SetSelection(0); yield return null;
            Click("analysis-replay-start");
            for (int wait = 0; panel.ReplaySession?.Status == BotReplayStatus.Preparing && wait < 400; wait++) yield return null;
            Click("analysis-replay-run"); Click("analysis-replay-pause");
            Check(panel.ReplaySession.Status == BotReplayStatus.Paused, "실제 재로드 전 재생 일시정지");
            File.WriteAllLines(Evidence + "/reload-results.txt", Results);
            EditorApplication.update -= ReloadTick;
            reloadMarker = 1; EditorUtility.RequestScriptReload();
            yield return null;
        }

        [InitializeOnLoadMethod]
        private static void ResumeAnalysisReload()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(AnalysisReloadKey, ""))) return;
            // 창 재구성과 같은 프레임의 delayCall에 기대지 않는다. UI 준비는 반복자의 대기로 확인한다.
            Results.Clear(); Results.AddRange(File.ReadAllLines(Evidence + "/reload-results.txt"));
            File.AppendAllText(Evidence + "/reload-progress.txt", "reload callback " + DateTime.UtcNow.ToString("O") + Environment.NewLine);
            uiSequence = AfterAnalysisReload(); EditorApplication.update += ReloadTick;
        }

        private static IEnumerator AfterAnalysisReload()
        {
            AnalysisReloadData data = JsonUtility.FromJson<AnalysisReloadData>(SessionState.GetString(AnalysisReloadKey, ""));
            for (int wait = 0; wait < 400; wait++)
            {
                window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().SingleOrDefault();
                if (window?.rootVisualElement.Q("analysis-panel") != null) break;
                yield return null;
            }
            Check(reloadMarker == 0 && data.process == System.Diagnostics.Process.GetCurrentProcess().Id, "동일 프로세스의 실제 도메인 재로드");
            Check(window != null && window.WorkspaceTab == 3, "재로드 뒤 결과 탭 유지");
            LevelAnalysisPanel panel = (LevelAnalysisPanel)typeof(LevelEditorWindow).GetField("analysisPanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
            Check(panel.ReplaySession == null && panel.Analysis == null, "재로드 뒤 폐기된 재생 객체 자동 복원 금지");
            uiLevel = AssetDatabase.LoadAssetAtPath<LevelDefinition>(data.asset);
            Check(JsonUtility.ToJson(uiLevel) == data.json && EditorUtility.IsDirty(uiLevel) &&
                File.ReadAllText(data.asset) == data.disk && AssetDatabase.AssetPathToGUID(data.asset) == data.guid, "미저장 원본 값·dirty·디스크·GUID 보존");
            panel.OpenRecord(data.record);
            for (int wait = 0; panel.Analysis?.IsDone != true && wait < 400; wait++) yield return null;
            Check(panel.Analysis?.Games.Count == 200 && panel.Analysis.Error == null, "재로드 후 기록 다시 열기");
            string recommendation = window.rootVisualElement.Q<Label>("analysis-difficulty-title").text;
            Check(recommendation.Contains("보류"), "재로드 후 성공 0판 추천 보류 재계산");
            Check(File.ReadAllText(Path.Combine(data.record, "batch.json")) == data.header, "재로드 조회는 원본 기록 바이트 불변");
            string renamed = data.directory + "/Renamed.asset";
            Check(string.IsNullOrEmpty(AssetDatabase.RenameAsset(data.asset, "Renamed")), "검사 소유 원본 이름 변경");
            panel.SourceChanged(); yield return null;
            Check(AssetDatabase.GUIDToAssetPath(data.guid) == renamed &&
                window.rootVisualElement.Q<Label>("analysis-identity").text.Contains("Renamed.asset"), "GUID로 바뀐 원본 이름 찾기");
            // 삭제 검사는 저장 사본과 소유권을 분리했는지 확인하기 위한 전용 에셋에만 수행한다.
            Check(AssetDatabase.DeleteAsset(renamed), "검사 소유 원본 삭제");
            uiLevel = null; panel.SourceChanged(); yield return null;
            Check(window.rootVisualElement.Q<Label>("analysis-identity").text.Contains("찾을 수 없습니다"), "원본 삭제 안내");
            Check(window.rootVisualElement.Q<Label>("analysis-difficulty-title").text == recommendation,
                "원본 이름 변경·삭제 후에도 저장 사본의 추천 의미 유지");
            Click("analysis-tab-cases"); yield return null;
            window.rootVisualElement.Q<ListView>("analysis-cases").SetSelection(0); yield return null;
            Click("analysis-replay-start");
            for (int wait = 0; panel.ReplaySession?.Status == BotReplayStatus.Preparing && wait < 400; wait++) yield return null;
            Click("analysis-replay-run");
            for (int wait = 0; panel.ReplaySession?.NeedsAdvance == true && wait < 400; wait++) yield return null;
            Check(panel.ReplaySession.Status == BotReplayStatus.Completed, "원본 삭제 후 저장 사본으로 완전 재생");
        }

        private static void FinishAnalysisReload(Exception error)
        {
            EditorApplication.update -= ReloadTick;
            if (error != null) { Results.Add("FAIL " + error); UnityEngine.Debug.LogException(error); }
            AnalysisReloadData data = JsonUtility.FromJson<AnalysisReloadData>(SessionState.GetString(AnalysisReloadKey, ""));
            if (window != null) window.Close();
            if (data != null && data.directory.StartsWith("Assets/__AnalysisReload_", StringComparison.Ordinal) && !data.directory.Contains(".."))
                Results.Add((AssetDatabase.DeleteAsset(data.directory) ? "PASS " : "FAIL ") + "검사 소유 에셋·메타 정리");
            SessionState.EraseString(AnalysisReloadKey);
            File.WriteAllLines(Evidence + "/reload-results.txt", Results);
            EditorApplication.Exit(Results.Any(r => r.StartsWith("FAIL")) ? 1 : 0);
        }
    }
}

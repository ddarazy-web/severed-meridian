using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>실제 Match 창에서 선택·레벨별 추천 분리·제어를 확인한다. 기존 기록 사본만 사용한다.</summary>
    public static class MultiLevelUiVerification
    {
        private const string Evidence = "Logs/MultiLevelVerification";
        private static readonly List<string> results = new List<string>();
        private static readonly List<LevelDefinition> levels = new List<LevelDefinition>();
        private static LevelEditorWindow window;
        private static IEnumerator sequence;
        private static string folder, oldPointer;
        private static bool hadPointer;
        private static double deadline;
        public static void Start() { sequence = Run(); deadline = EditorApplication.timeSinceStartup + 90; EditorApplication.update += Tick; }

        /// <returns>UI 갱신과 기록 조회를 기다리는 단계. 실제 게임은 진행하지 않는다.</returns>
        private static IEnumerator Run()
        {
            string pointer = MultiLevelTestStore.DefaultRoot + "/latest.txt";
            hadPointer = File.Exists(pointer); oldPointer = hadPointer ? File.ReadAllText(pointer) : null;
            string run = File.ReadAllText("Logs/Stage31Workflow/full-run-path.txt").Trim();
            string originalId = File.ReadAllText(run + "/records/latest.txt").Trim();
            string rawRoot = run + "/records/" + originalId;
            string json = File.ReadAllText(rawRoot + "/balance.json");
            MultiLevelTestStore store = new MultiLevelTestStore();
            MultiLevelTestRecord record = new MultiLevelTestRecord { id = Guid.NewGuid().ToString("N"), mode = MultiLevelTestMode.Balance };
            folder = "Assets/__MultiUi_" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            for (int index = 0; index < 2; index++)
            {
                BotMoveBalanceRecord value = JsonUtility.FromJson<BotMoveBalanceRecord>(json);
                value.id = Guid.NewGuid().ToString("N"); value.sourceName = index == 0 ? "검사 A" : "검사 B";
                value.trials = value.trials.Take(index + 1).ToList(); value.status = BotBatchStatus.Interrupted;
                LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>(); JsonUtility.FromJsonOverwrite(value.definitionJson, level);
                AssetDatabase.CreateAsset(level, folder + "/" + value.sourceName + ".asset"); levels.Add(level);
                record.entries.Add(new MultiLevelTestEntry { name = value.sourceName, assetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level)),
                    definitionJson = value.definitionJson, resultId = value.id, status = MultiLevelTestStatus.Interrupted });
                string destination = store.LevelRoot(record, index); new BotMoveBalanceStore(destination).Save(value);
                foreach (BotMoveTrial trial in value.trials)
                {
                    string target = Path.Combine(destination, value.id, "trials", trial.batchId); Directory.CreateDirectory(target);
                    foreach (string path in Directory.GetFiles(Path.Combine(rawRoot, "trials", trial.batchId))) File.Copy(path, Path.Combine(target, Path.GetFileName(path)));
                }
            }
            store.Save(record);
            window = LevelEditorWindow.OpenWorkspace(4, levels[0], true); window.ShowUtility(); window.position = new Rect(20, 20, 1000, 760);
            yield return null; yield return null;
            Check(window.WorkspaceTab == 4 && window.rootVisualElement.Q("multi-level-panel").resolvedStyle.display == DisplayStyle.Flex, "실제 통합창 여러 레벨 시험 탭");
            ListView resultList = window.rootVisualElement.Q<ListView>("multi-results");
            Label detail = window.rootVisualElement.Q<Label>("multi-detail");
            resultList.SetSelection(0);
            while (detail.text.Contains("확인 중")) yield return null;
            Check(detail.text.StartsWith("검사 A") && detail.text.Contains("어려움 : 1회") && detail.text.Contains("쉬움 : 추가 시험 필요"), "첫 레벨의 실측 추천만 표시");
            resultList.SetSelection(1);
            while (detail.text.Contains("확인 중")) yield return null;
            Check(detail.text.StartsWith("검사 B") && detail.text.Contains("쉬움 : 2회") && !detail.text.Contains("검사 A"), "두 번째 레벨 선택 시 추천 분리");
            for (int frame = 0; frame < 3; frame++) yield return null;
            foreach (string name in new[] { "multi-start", "multi-pause", "multi-stop", "multi-read", "multi-mode" })
            {
                VisualElement element = window.rootVisualElement.Q(name);
                Check(element.worldBound.width > 0 && element.worldBound.xMax <= window.rootVisualElement.worldBound.xMax + 1, "최소 창 버튼 접근 " + name);
            }
            typeof(BotAnalysisVerification).GetMethod("CaptureAnalysis", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { "multi-level.png" });
            File.Copy("Logs/BotAnalysisVerification/multi-level.png", Evidence + "/multi-level.png", true);
            ListView choices = window.rootVisualElement.Q<ListView>("multi-level-choices");
            Check(choices.selectionType == SelectionType.Multiple, "다중 선택 목록");
            choices.SetSelection(Enumerable.Range(0, choices.itemsSource.Count).Where(i => levels.Contains((LevelDefinition)choices.itemsSource[i])));
            window.rootVisualElement.Q<PopupField<string>>("multi-mode").index = 0;
            window.rootVisualElement.Q<IntegerField>("multi-samples").value = 1;
            Click("multi-start"); Click("multi-pause");
            Check(!choices.enabledInHierarchy && window.rootVisualElement.Q<Button>("multi-pause").text == "재개", "시작·일시정지와 선택 변경 잠금");
            window.SelectWorkspaceTab(1);
            Check(!window.ActiveSimulationPanel.rootVisualElement.enabledInHierarchy, "다른 탭에서 중복 시험 잠금");
            window.SelectWorkspaceTab(4); Check(window.rootVisualElement.Q<Button>("multi-pause").text == "재개", "탭 복귀 자동 재개 없음");
            Click("multi-pause"); Click("multi-pause"); Click("multi-stop");
            Check(choices.enabledInHierarchy, "중지 후 선택 잠금 해제");
            MultiLevelTestRecord stopped = store.Load();
            Check(stopped.entries.Count == 2 && stopped.entries.All(e => e.status == MultiLevelTestStatus.Stopped && string.IsNullOrEmpty(e.resultId)), "선택 두 레벨만 등록·추가 게임 0판");
            window.Close(); window = null;
            // 앞선 소규모 실제 반복 결과로 현재 횟수의 판단 보류와 기존 상세 화면 연결을 확인한다.
            string repeatManifest = Directory.GetDirectories(Evidence, "run-*").SelectMany(p => Directory.GetFiles(p, "queue.json", SearchOption.AllDirectories))
                .First(p => JsonUtility.FromJson<MultiLevelTestRecord>(File.ReadAllText(p)).mode == MultiLevelTestMode.Repeat);
            MultiLevelTestRecord repeatRecord = JsonUtility.FromJson<MultiLevelTestRecord>(File.ReadAllText(repeatManifest));
            string repeatRoot = Path.GetDirectoryName(repeatManifest); repeatRecord.id = Guid.NewGuid().ToString("N");
            repeatRecord.entries = repeatRecord.entries.Take(1).ToList();
            string repeatTarget = Path.Combine(store.LevelRoot(repeatRecord, 0), repeatRecord.entries[0].resultId); Directory.CreateDirectory(repeatTarget);
            foreach (string path in Directory.GetFiles(Path.Combine(repeatRoot, "0000", repeatRecord.entries[0].resultId)))
                File.Copy(path, Path.Combine(repeatTarget, Path.GetFileName(path)));
            store.Save(repeatRecord); window = LevelEditorWindow.OpenWorkspace(4, levels[0], true); yield return null;
            window.rootVisualElement.Q<ListView>("multi-results").SetSelection(0);
            detail = window.rootVisualElement.Q<Label>("multi-detail");
            while (detail.text.Contains("확인 중")) yield return null;
            Check(detail.text.Contains("판단 보류") && detail.text.Contains("100판"), "반복 시험의 작은 표본은 임의 등급을 만들지 않음");
            yield return null;
            Click("multi-open-repeat"); Check(window.WorkspaceTab == 3, "반복 결과에서 기존 통계·사례 탭으로 연결");
            window.Close(); window = null;
            // 다시 열 때 기존 실측 목록을 복원하며 자동 게임 실행은 하지 않는다.
            store.Save(record); window = LevelEditorWindow.OpenWorkspace(4, levels[0], true); yield return null;
            Check(window.rootVisualElement.Q<ListView>("multi-results").itemsSource.Count == 2 && !window.rootVisualElement.Q<Button>("multi-stop").enabledInHierarchy, "창 재열기 결과 목록 보존·자동 실행 없음");
        }
        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name); Check(button.enabledInHierarchy, "버튼 활성 " + name);
            using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
        private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); results.Add("PASS " + message); }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("소규모 UI 검사 시간 초과");
                if (sequence.MoveNext()) return;
            }
            catch (Exception error) { results.Add("FAIL " + error); }
            EditorApplication.update -= Tick;
            if (window != null) window.Close();
            if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
            string pointer = MultiLevelTestStore.DefaultRoot + "/latest.txt";
            if (hadPointer) File.WriteAllText(pointer, oldPointer); else File.Delete(pointer);
            File.WriteAllLines(Evidence + "/ui-results.txt", results);
            EditorApplication.Exit(results.Any(line => line.StartsWith("FAIL")) ? 1 : 0);
        }
    }
}

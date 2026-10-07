using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class BotDifficultyVerification
    {
        private static LevelEditorWindow difficultyWindow;
        private static LevelDefinition difficultyLevel;
        private static IEnumerator difficultySequence;
        private static double nextUi;

        public static void UI()
        {
            Results.Clear(); difficultySequence = DifficultyUI(); EditorApplication.update += DifficultyTick;
        }

        private static void DifficultyTick()
        {
            if (EditorApplication.timeSinceStartup < nextUi) return;
            nextUi = EditorApplication.timeSinceStartup + .1;
            Exception failure = null;
            try { if (difficultySequence.MoveNext()) return; }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); UnityEngine.Debug.LogException(error); }
            EditorApplication.update -= DifficultyTick;
            if (difficultyWindow != null) difficultyWindow.Close();
            if (difficultyLevel != null) UnityEngine.Object.DestroyImmediate(difficultyLevel);
            File.WriteAllLines(Evidence + "/difficulty-ui-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static void DifficultyClick(string name)
        {
            Button button = difficultyWindow.rootVisualElement.Q<Button>(name);
            Check(button != null && button.enabledInHierarchy, "난이도 화면 버튼 접근 " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }

        private static IEnumerator DifficultyUI()
        {
            string root = File.ReadAllText(Evidence + "/runs-path.txt");
            string[] paths = File.ReadAllLines(Evidence + "/runs-results.txt").Where(line => line.StartsWith("DATA "))
                .Select(line => Path.Combine(root, line.Split(' ').First(word => Guid.TryParseExact(word, "N", out _)))).ToArray();
            Check(paths.Length == 4, "실제 네 시험 묶음 확보");
            BotBatchRecord first = JsonUtility.FromJson<BotBatchRecord>(File.ReadAllText(Path.Combine(paths[0], "batch.json")));
            difficultyLevel = ScriptableObject.CreateInstance<LevelDefinition>(); JsonUtility.FromJsonOverwrite(first.definitionJson, difficultyLevel);
            string original = JsonUtility.ToJson(difficultyLevel); bool dirty = EditorUtility.IsDirty(difficultyLevel);
            difficultyWindow = LevelEditorWindow.OpenWorkspace(3, difficultyLevel, true);
            difficultyWindow.ShowUtility(); difficultyWindow.position = new Rect(10, 10, 1000, 760);
            yield return null;
            typeof(ConnectionGraphVerification).GetField("window", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, difficultyWindow);
            typeof(ConnectionGraphVerification).GetMethod("RaiseWindow", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            LevelAnalysisPanel panel = (LevelAnalysisPanel)typeof(LevelEditorWindow).GetField("analysisPanel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(difficultyWindow);
            VisualElement ui = difficultyWindow.rootVisualElement;
            for (int sample = 0; sample < paths.Length; sample++)
            {
                Stopwatch watch = Stopwatch.StartNew(); panel.OpenRecord(paths[sample]);
                for (int wait = 0; panel.Analysis?.IsDone != true && wait < 500; wait++) yield return null;
                Check(panel.Analysis?.Error == null && panel.Analysis?.IsDone == true, "실제 200판 UI 읽기 " + sample);
                DifficultyClick("analysis-tab-summary"); yield return null;
                Label title = ui.Q<Label>("analysis-difficulty-title");
                Check(title != null && title.text.Contains("예상 난이도"), "실제 성공 기록 등급 표시 " + sample);
                Check(ui.Q<Label>("analysis-difficulty-version").text.Contains(BotDifficultyRules.Version), "현재 평가 버전 표시 " + sample);
                Check(ui.Q<Label>("analysis-difficulty-samples").text.Contains("100쌍"), "같은 시드 정상 표본 표시 " + sample);
                if (sample == 1) Check(ui.Q<Label>("analysis-difficulty-tags").text.Contains("이동 여유 부족"), "실제 이동 여유 태그");
                Foldout evidence = ui.Q<Foldout>("analysis-difficulty-evidence"); evidence.value = true; yield return null;
                Check(evidence.Query<Label>().ToList().Any(label => label.text.Contains("사람의 체감")), "근거 펼치기에서 한계 표시 " + sample);
                Check(ui.Q<Button>("difficulty-cases").worldBound.xMax <= difficultyWindow.position.width, "최소 너비 사례 버튼 접근 " + sample);
                Results.Add("TIME 읽기와 표시 " + sample + " " + watch.Elapsed.TotalMilliseconds.ToString("F1") + "ms (UI 검사 대기 포함)");
                if (sample == 0 || sample == 1)
                {
                    evidence.value = false; yield return null;
                    typeof(BotAnalysisVerification).GetField("window", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, difficultyWindow);
                    string image = "difficulty-success-" + sample + ".png";
                    typeof(BotAnalysisVerification).GetMethod("CaptureAnalysis", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { image });
                    File.Copy("Logs/BotAnalysisVerification/" + image, Evidence + "/" + image, true);
                }
                DifficultyClick("difficulty-cases"); yield return null;
                Check(ui.Q<ListView>("analysis-cases").itemsSource.Count == 100 && panel.ReplaySession == null, "대표 전략 사례만 조회·자동 재생 없음 " + sample);
                DifficultyClick("workspace-tab-0"); yield return null; DifficultyClick("workspace-tab-3"); yield return null;
                Check(panel.Analysis.Record.id == Path.GetFileName(paths[sample]), "탭 이동 후 선택 기록 보존 " + sample);
                if (sample == 0) DifficultyClick("analysis-reference");
            }
            DifficultyClick("analysis-tab-compare"); yield return null;
            Check(ui.Q<Label>("reference-difficulty-title") != null && ui.Q<Label>("selected-difficulty-title") != null, "새 시드 재시험의 양쪽 추천 표시");
            Check(ui.Q<Label>("analysis-comparison-info").text.Contains("다른 시드 묶음"), "새 시드 묶음 차이 안내");
            Check(JsonUtility.ToJson(difficultyLevel) == original && EditorUtility.IsDirty(difficultyLevel) == dirty, "추천 UI 원본·dirty 불변");
            string export = null;
            // 최초 검사에서 완료 파일을 확인하지 못한 이력이 있어 같은 정상 원본의 내보내기를 반복 대조한다.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                export = Path.GetFullPath(Evidence + "/difficulty-ui-export-" + Guid.NewGuid().ToString("N"));
                panel.BeginExport(export);
                for (int wait = 0; !File.Exists(Path.Combine(export, "batch.json")) && wait < 500; wait++) yield return null;
                Check(File.Exists(Path.Combine(export, "batch.json")), "추천 후 기록 내보내기 " + attempt + ": " + ui.Q<Label>("analysis-notice").text);
                Check(Directory.GetFiles(export, "*.json").Length == 201, "200판과 요약 보관 " + attempt);
            }
            BotBatchRecord changed = JsonUtility.FromJson<BotBatchRecord>(File.ReadAllText(Path.Combine(export, "batch.json")));
            changed.planningVersion = "unsupported-difficulty-test";
            File.WriteAllText(Path.Combine(export, "batch.json"), JsonUtility.ToJson(changed));
            panel.OpenRecord(export);
            for (int wait = 0; panel.Analysis?.IsDone != true && wait < 500; wait++) yield return null;
            DifficultyClick("analysis-tab-summary"); yield return null;
            Check(ui.Q<Label>("analysis-difficulty-title").text.Contains("보류") && ui.Q<Label>("analysis-difficulty-hold").text.Contains("버전"), "과거 전략 버전은 통계 조회 유지·추천 보류");
            Check(ui.Q<Label>("analysis-basic") != null, "비호환 평가에서도 기존 통계 보존");
            changed.planningVersion = PlanningSearch.Version;
            LevelDefinition invalid = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                JsonUtility.FromJsonOverwrite(changed.definitionJson, invalid);
                JsonUtility.FromJsonOverwrite("{\"moveCount\":0}", invalid);
                changed.definitionJson = JsonUtility.ToJson(invalid); changed.fingerprint = LevelStateBuilder.Fingerprint(invalid);
            }
            finally { UnityEngine.Object.DestroyImmediate(invalid); }
            File.WriteAllText(Path.Combine(export, "batch.json"), JsonUtility.ToJson(changed));
            panel.OpenRecord(export);
            for (int wait = 0; panel.Analysis?.IsDone != true && wait < 500; wait++) yield return null;
            yield return null;
            Check(ui.Q<Label>("analysis-difficulty-title").text.Contains("보류"), "지문이 일치해도 잘못된 레벨 정의는 추천 보류");
            File.WriteAllText(Path.Combine(export, "000000.json"), "{broken");
            panel.OpenRecord(export);
            for (int wait = 0; panel.Analysis?.IsDone != true && wait < 500; wait++) yield return null;
            yield return null;
            Check(ui.Q<Label>("analysis-difficulty-title")?.text.Contains("보류") == true, "손상 기록에서 이전 추천 제거·보류 표시");
            panel.OpenRecord(Path.Combine(root, "does-not-exist")); yield return null;
            Check(ui.Query<Label>().ToList().Any(label => label.text != null && label.text.Contains("난이도 판단 보류")), "누락된 기록은 판단 보류 안내");
        }
    }
}

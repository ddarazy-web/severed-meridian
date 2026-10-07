using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    /// <summary>31단계 실제 UI 20,000판 시험. 실제 실행을 건너뛰거나 결과를 합성하지 않는다.</summary>
    public static class Stage31BalanceVerification
    {
        private const string Evidence = "Logs/Stage31Workflow";
        private static readonly List<string> Results = new List<string>();
        private static LevelEditorWindow window;
        private static LevelDefinition level;
        private static BotMoveBalancePanel balance;
        private static IEnumerator sequence;
        private static Stopwatch watch;
        private static double nextSample;
        private static string original, runDirectory;
        private static int previousCompleted = -1;

        /// <summary>전용 Unity 프로세스에서 호출한다. 완료/오류 시 증거를 보존하고 해당 프로세스만 종료한다.</summary>
        public static void Full()
        {
            Directory.CreateDirectory(Evidence);
            runDirectory = Evidence + "/full-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(runDirectory);
            File.WriteAllText(Evidence + "/full-run-path.txt", runDirectory);
            File.WriteAllText(Evidence + "/full-process.txt", Process.GetCurrentProcess().Id.ToString());
            File.WriteAllText(runDirectory + "/performance.csv", "seconds,completedMoves,finishedGames,workingSetBytes,managedBytes\n");
            watch = Stopwatch.StartNew(); sequence = Run(); EditorApplication.update += Tick;
        }

        /// <param name="pass">검사 결과.</param><param name="message">증거 이름.</param>
        private static void Check(bool pass, string message)
        {
            if (!pass) throw new InvalidOperationException(message);
            Results.Add("PASS " + message);
        }

        private static IEnumerator Run()
        {
            level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { Enumerable.Range(0, 16).Select(i => new BoardCoordinate(i / 4, i % 4)).ToArray() });
            JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[],\"moveCount\":20,\"missions\":[{\"kind\":0,\"color\":0,\"count\":3}]}", level);
            for (int c = 0; c < 4; c++)
                typeof(SettlementVerification).GetMethod("Source", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { level, new BoardCoordinate(0, c), SupplyExhaustion.Random, new[] { new SupplyItem(SupplyKind.RandomNormal, 1) } });
            string folder = "Assets/__Stage31_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            AssetDatabase.CreateAsset(level, folder + "/기본수집_전체시험.asset"); AssetDatabase.SaveAssetIfDirty(level);
            File.WriteAllText(runDirectory + "/source-asset.txt", AssetDatabase.GetAssetPath(level));
            original = JsonUtility.ToJson(level); File.WriteAllText(runDirectory + "/source.json", original);
            Check(LevelDefinitionValidator.Validate(level).Count == 0, "대표 4×4 기본 수집·정상 공급 정의 검사");
            window = LevelEditorWindow.OpenWorkspace(1, level, true); window.ShowUtility(); window.position = new Rect(20, 20, 1160, 900);
            balance = (BotMoveBalancePanel)typeof(LevelInitialStatePanel).GetField("balancePanel", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(window.ActiveSimulationPanel);
            balance.Store = new BotMoveBalanceStore(runDirectory + "/records"); balance.Root.value = true;
            yield return null; yield return null;
            Button start = window.rootVisualElement.Q<Button>("balance-start");
            Check(start != null && start.enabledInHierarchy, "실제 밸런스 시작 버튼 활성");
            using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = start; start.SendEvent(evt); }
            Check(balance.Session?.NeedsAdvance == true, "실제 UI로 새 시드 전체 시험 시작");
            File.WriteAllText(runDirectory + "/initial-balance.json", JsonUtility.ToJson(balance.Session.Record, true));
            while (balance.Session.CanContinue) yield return null;
            BotMoveBalanceRecord record = balance.Session.Record;
            Check(record.status == BotBatchStatus.Completed, "전체 시험 상태: " + record.status + " · " + record.message);
            Check(record.trials.Count == 100 && record.trials.Select(t => t.moves).SequenceEqual(Enumerable.Range(1, 100)), "실제 1~100회 누락·중복 없음");
            Check(record.trials.All(t => t.normalPerStrategy == 100), "각 횟수 실제 두 전략 100판씩 완료");
            Check(JsonUtility.ToJson(level) == original && !EditorUtility.IsDirty(level), "원본 JSON·dirty 보존");
            Check(window.rootVisualElement.Q<Label>("balance-progress").text.Contains("전체 완료"), "실제 화면 전체 완료 표시");
            for (int grade = 0; grade < 4; grade++)
            {
                string ranges = BotMoveRecommendations.Ranges(record.trials, grade);
                Check(window.rootVisualElement.Q<Label>("balance-recommendations").text.Contains(BotMoveRecommendations.Titles[grade] + " : " +
                    (ranges.Length == 0 ? "추천 없음" : ranges)), "실제 화면 추천 " + BotMoveRecommendations.Titles[grade]);
            }
            Stopwatch read = Stopwatch.StartNew(); BotMoveBalanceRecord reloaded = balance.Store.Load(); read.Stop();
            Check(reloaded.status == BotBatchStatus.Completed && reloaded.trials.Count == 100, "전체 요약 재조회");
            File.WriteAllText(runDirectory + "/timing.txt", $"FullSeconds={watch.Elapsed.TotalSeconds.ToString(CultureInfo.InvariantCulture)}\nSummaryReadMilliseconds={read.Elapsed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)}");
            window.rootVisualElement.Q<Foldout>("balance-detail").value = true;
            yield return null; yield return null;
            typeof(BotAnalysisVerification).GetMethod("CaptureAnalysis", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { "stage31-full.png" });
            File.Copy("Logs/BotAnalysisVerification/stage31-full.png", runDirectory + "/full-ui.png", true);
        }

        /// <summary>제품 실행은 기존 UI가 담당한다. 이 코드는 상태를 관찰하고 비용·완료 증거만 기록한다.</summary>
        private static void Tick()
        {
            Exception failure = null;
            try
            {
                int completed = balance?.Session?.Record.trials.Count ?? 0;
                if (watch.Elapsed.TotalSeconds >= nextSample || completed != previousCompleted)
                {
                    previousCompleted = completed; nextSample = watch.Elapsed.TotalSeconds + 10;
                    int games = completed * 200 + (balance?.Session?.Current?.Record.finished ?? 0);
                    File.WriteAllText(runDirectory + "/progress.txt", $"{DateTime.UtcNow:O}\n{completed}/100 moves; {games}/20000 games\n" + (balance?.Session?.Record.message ?? "준비 중"));
                    File.AppendAllText(runDirectory + "/performance.csv", string.Join(",", watch.Elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture),
                        completed, games, Process.GetCurrentProcess().WorkingSet64, GC.GetTotalMemory(false)) + "\n");
                }
                if (sequence.MoveNext()) return;
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            EditorApplication.update -= Tick;
            if (window != null) window.Close();
            // 대표 에셋은 재시작·선택 적용 검증을 위해 남긴다. 최종 감사 때 소유 경로만 정리한다.
            File.WriteAllLines(runDirectory + "/results.txt", Results);
            File.WriteAllText(runDirectory + "/terminal.txt", failure == null ? "completed" : "failed");
            if (failure != null) UnityEngine.Debug.LogException(failure);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}

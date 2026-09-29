using System;
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>범위 계산·보관·실제 봇 400판과 UI 수명주기를 검사한다. 합성 결과는 실측과 구분한다.</summary>
    public static class BotMoveBalanceVerification
    {
        private const string Evidence = "Logs/BalanceVerification";
        private static readonly List<string> Results = new List<string>();
        private static LevelEditorWindow window;
        private static LevelInitialStatePanel panel;
        private static LevelDefinition level;
        private static BotMoveBalancePanel balance;
        private static IEnumerator sequence;
        private static string original;
        private static double deadline, nextProgress;

        public static void Start()
        {
            Directory.CreateDirectory(Evidence);
            sequence = Run(); deadline = EditorApplication.timeSinceStartup + 1200;
            EditorApplication.update += Tick;
        }

        private static void Check(bool pass, string name)
        {
            if (!pass) throw new InvalidOperationException(name);
            Results.Add("PASS " + name);
        }

        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            Check(button != null && button.enabledInHierarchy, "버튼 접근 " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }

        private static IEnumerator Run()
        {
            Check(BotMoveRecommendations.Grade(BotDifficultyGrade.Easy) == 0 &&
                BotMoveRecommendations.Grade(BotDifficultyGrade.Normal) == 1 && BotMoveRecommendations.Grade(BotDifficultyGrade.Hard) == 2 &&
                BotMoveRecommendations.Grade(BotDifficultyGrade.VeryHard) == 3 && BotMoveRecommendations.Grade(null) == -1, "공통 4등급 변환·보류");
            List<BotMoveTrial> synthetic = new List<BotMoveTrial> {
                new BotMoveTrial { moves = 30, grade = 0, normalPerStrategy = 100 },
                new BotMoveTrial { moves = 32, grade = 0, normalPerStrategy = 100 },
                new BotMoveTrial { moves = 31, grade = 0, normalPerStrategy = 100 },
                new BotMoveTrial { moves = 28, grade = 0, normalPerStrategy = 100 },
                new BotMoveTrial { moves = 29, grade = 1, normalPerStrategy = 100 },
                new BotMoveTrial { moves = 33, grade = 0, normalPerStrategy = 99 } };
            Check(BotMoveRecommendations.Ranges(synthetic, 0) == "28회, 30~32회", "합성 표본: 빈칸·다른 등급·미완료를 범위로 연결하지 않음");
            Check(BotMoveRecommendations.Ranges(synthetic, 3) == "", "없는 난이도 수치를 만들지 않음");
            // 전체 범위의 종료 표시와 저장 형식은 합성 값으로 검사한다. 아래 실제 판 수에 합산하지 않는다.
            BotMoveBalanceStore syntheticStore = new BotMoveBalanceStore(Evidence + "/synthetic-" + Guid.NewGuid().ToString("N"));
            BotMoveBalanceRecord full = new BotMoveBalanceRecord { id = Guid.NewGuid().ToString("N"), seeds = Enumerable.Range(1, 100).ToArray(),
                status = BotBatchStatus.Completed, trials = Enumerable.Range(1, 100).Select(n => new BotMoveTrial {
                    moves = n, normalPerStrategy = 100, grade = n <= 25 ? 3 : n <= 50 ? 2 : n <= 75 ? 1 : 0 }).ToList() };
            syntheticStore.Save(full);
            Check(syntheticStore.Load().trials.Count == 100 && BotMoveRecommendations.Ranges(full.trials, 0) == "76~100회" &&
                BotMoveRecommendations.Ranges(full.trials, 3) == "1~25회", "합성 100구간 완료 저장과 양 끝 범위");
            full.trials.RemoveAt(99); syntheticStore.Save(full);
            bool rejected = false;
            try { syntheticStore.Load(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "합성 99구간을 전체 완료로 저장한 손상 기록 거부");
            level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { Enumerable.Range(0, 16).Select(i => new BoardCoordinate(i / 4, i % 4)).ToArray() });
            level.name = "이동 횟수 검증용 수집 레벨";
            JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[],\"moveCount\":20,\"missions\":[{\"kind\":0,\"color\":0,\"count\":3}]}", level);
            for (int c = 0; c < 4; c++)
                typeof(SettlementVerification).GetMethod("Source", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { level, new BoardCoordinate(0, c), SupplyExhaustion.Random, new[] { new SupplyItem(SupplyKind.RandomNormal, 1) } });
            original = JsonUtility.ToJson(level);
            window = LevelEditorWindow.OpenWorkspace(1, level, true); window.ShowUtility(); window.position = new Rect(20, 20, 1160, 900);
            panel = window.ActiveSimulationPanel;
            balance = (BotMoveBalancePanel)typeof(LevelInitialStatePanel).GetField("balancePanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
            balance.Store = new BotMoveBalanceStore(Evidence + "/actual-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(Evidence + "/actual-root.txt", balance.Store.Root);
            balance.Root.value = true;
            yield return null; yield return null;
            Click("balance-start");
            Check(balance.CanContinue && balance.Session.Record.seeds.Length == 100, "실제 UI 시작·100시드");
            Check(!window.rootVisualElement.Q("bot-buttons").enabledInHierarchy && !window.rootVisualElement.Q("batch-buttons").enabledInHierarchy,
                "단일·반복 봇 중복 실행 잠금");
            Click("balance-pause");
            int count = balance.Session.Record.trials.Count;
            yield return null; yield return null;
            Check(balance.Session.Record.status == BotBatchStatus.Paused && balance.Session.Record.trials.Count == count, "일시정지 상태 유지");
            panel.SetVisible(false); panel.SetVisible(true);
            Check(balance.Session.Record.status == BotBatchStatus.Paused, "탭 복귀 자동 재개 금지");
            Click("balance-pause");
            while (balance.Session.Record.trials.Count < 2 && balance.Session.NeedsAdvance) yield return null;
            Check(balance.Session.Record.trials.Count >= 2, "실제 이동 1회·2회 각각 200판 완료: " + balance.Session.Record.message);
            Click("balance-pause");
            BotMoveBalanceRecord actual = balance.Session.Record;
            Check(actual.status == BotBatchStatus.Paused, "완료 직후 일시정지 요약 저장 성공: " + actual.message);
            Check(actual.trials.Take(2).All(t => t.normalPerStrategy == 100), "횟수마다 두 전략 각 100판 정상 표본");
            foreach (BotMoveTrial trial in actual.trials.Take(2))
            {
                string directory = Path.Combine(balance.Store.Root, actual.id, "trials", trial.batchId);
                BotBatchRecord batch = JsonUtility.FromJson<BotBatchRecord>(File.ReadAllText(Path.Combine(directory, "batch.json")));
                Check(batch.seeds.SequenceEqual(actual.seeds) && batch.status == BotBatchStatus.Completed, "이동 " + trial.moves + "회 같은 시드·완료 기록");
                var games = Directory.GetFiles(directory, "*.json").Where(p => Path.GetFileName(p) != "batch.json")
                    .Select(p => JsonUtility.FromJson<BotBatchGame>(File.ReadAllText(p))).ToArray();
                Check(games.Length == 200 && games.Count(g => g.strategy == BotStrategyKind.Basic && g.outcome == BotSessionStatus.Won) == trial.basicWon &&
                    games.Count(g => g.strategy == BotStrategyKind.Planning && g.outcome == BotSessionStatus.Won) == trial.planningWon, "실제 판 파일과 추천 성공 수 독립 대조");
                Results.Add($"DATA 실제 이동 {trial.moves}회 · 기본 {trial.basicWon}/100 · 계획 {trial.planningWon}/100 · 등급 {trial.grade}");
            }
            Check(JsonUtility.ToJson(level) == original, "원본 이동 횟수·전체 데이터 보존");
            Check(window.rootVisualElement.Q<Label>("balance-progress").text.Contains("중간 결과"), "100개 횟수 전에는 전체 완료로 표시하지 않음");
            window.rootVisualElement.Q<Foldout>("balance-detail").value = true;
            yield return null; yield return null;
            window.position = new Rect(20, 20, 780, 860);
            yield return null; yield return null;
            foreach (string name in new[] { "balance-start", "balance-pause", "balance-stop", "balance-recommendations" })
            {
                VisualElement control = window.rootVisualElement.Q(name);
                Check(control.worldBound.yMax <= window.rootVisualElement.worldBound.yMax && control.worldBound.xMax <= window.rootVisualElement.worldBound.xMax,
                    "좁은 창 필수 조작과 추천 노출 " + name);
            }
            typeof(BotAnalysisVerification).GetMethod("CaptureAnalysis", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { "move-balance.png" });
            File.Copy("Logs/BotAnalysisVerification/move-balance.png", Evidence + "/actual-ui.png", true);
            Click("balance-stop");
            Check(!balance.CanContinue && balance.Session.Record.status == BotBatchStatus.Stopped, "일시정지 후 중지");
            BotMoveBalanceRecord loaded = balance.Store.Load();
            Check(loaded.trials.Count == actual.trials.Count && loaded.trials[0].batchId == actual.trials[0].batchId, "중지 후 완료한 횟수 결과 보관·다시 읽기");
            int[] firstSeeds = actual.seeds;
            Click("balance-start");
            Check(!balance.Session.Record.seeds.SequenceEqual(firstSeeds), "새 시험은 새로운 시드 묶음");
            balance.Session.SetPaused(true);
            Check(balance.Store.Load().status == BotBatchStatus.Interrupted, "저장된 실행 중 기록은 재열람 시 중단으로 구분");
            balance.Dispose();
            Check(balance.Store.Load().status == BotBatchStatus.Interrupted, "창 수명 종료 결과 보존");
        }

        private static void Tick()
        {
            Exception failure = null;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("실제 밸런스 검증 시간 초과");
                if (EditorApplication.timeSinceStartup > nextProgress)
                { nextProgress = EditorApplication.timeSinceStartup + 5; File.WriteAllText(Evidence + "/progress.txt", balance?.Session?.Record.message ?? "준비 중"); }
                if (sequence.MoveNext()) return;
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            EditorApplication.update -= Tick; balance?.Dispose(); if (window != null) window.Close();
            if (level != null) UnityEngine.Object.DestroyImmediate(level);
            File.WriteAllLines(Evidence + "/results.txt", Results);
            if (failure != null) UnityEngine.Debug.LogException(failure);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}

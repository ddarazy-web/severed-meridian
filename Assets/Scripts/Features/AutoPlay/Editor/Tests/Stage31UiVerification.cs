using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>기존 200판 사본을 실제 UI에서 조회한다. 전체 시험을 새로 돌리지 않는다.</summary>
    public static class Stage31UiVerification
    {
        private const string Evidence = "Logs/Stage31Workflow", Key = "Match.Stage31.SmallReload";
        private static readonly List<string> Results = new List<string>();
        private static LevelEditorWindow window;
        private static BotMoveBalancePanel balance;
        private static BotMoveBalanceRecord record;
        private static IEnumerator sequence;
        private static State state;
        private static int marker;
        private static int reloadFrames;
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] private sealed class State
        {
            public string asset, pointer, id, header, originalHeader;
            public bool hadPointer;
            public int process;
        }

        public static void Start()
        {
            try
            {
                string run = File.ReadAllText(Evidence + "/full-run-path.txt").Trim();
                string root = run + "/records", id = File.ReadAllText(root + "/latest.txt").Trim();
                record = JsonUtility.FromJson<BotMoveBalanceRecord>(File.ReadAllText(root + "/" + id + "/balance.json"));
                record.id = Guid.NewGuid().ToString("N"); record.trials = record.trials.Take(1).ToList(); record.status = BotBatchStatus.Interrupted;
                string pointer = BotMoveBalanceStore.DefaultRoot + "/latest.txt";
                state = new State { asset = File.ReadAllText(run + "/source-asset.txt").Trim(), id = record.id,
                    pointer = File.Exists(pointer) ? File.ReadAllText(pointer) : "", hadPointer = File.Exists(pointer), process = Process.GetCurrentProcess().Id };
                string destination = BotMoveBalanceStore.DefaultRoot + "/" + record.id + "/trials/" + record.trials[0].batchId;
                Directory.CreateDirectory(destination);
                foreach (string path in Directory.GetFiles(root + "/" + id + "/trials/" + record.trials[0].batchId))
                    File.Copy(path, destination + "/" + Path.GetFileName(path));
                new BotMoveBalanceStore().Save(record);
                state.header = BotMoveBalanceStore.DefaultRoot + "/" + record.id + "/balance.json";
                state.originalHeader = File.ReadAllText(state.header);
                Persist(); sequence = Run(); EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error, true); }
        }

        private static void Open()
        {
            if (window != null) window.Close();
            window = LevelEditorWindow.OpenWorkspace(1, AssetDatabase.LoadAssetAtPath<LevelDefinition>(state.asset), true);
            window.ShowUtility(); window.position = new Rect(20, 20, 780, 860);
            balance = (BotMoveBalancePanel)typeof(LevelInitialStatePanel).GetField("balancePanel", Hidden).GetValue(window.ActiveSimulationPanel);
            balance.Root.value = true;
        }

        private static bool Reading => typeof(BotMoveBalancePanel).GetField("loading", Hidden).GetValue(balance) != null;
        private static string Recommendation => window.rootVisualElement.Q<Label>("balance-recommendations").text;
        private static string Progress => window.rootVisualElement.Q<Label>("balance-progress").text;

        private static IEnumerator AwaitRead()
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (Reading)
            {
                if (watch.Elapsed.TotalSeconds > 60) throw new TimeoutException("200판 UI 조회가 완료되지 않았습니다.");
                yield return null;
            }
            Results.Add("DATA 200판 UI 분할 조회 대기 " + watch.ElapsedMilliseconds + "ms");
            yield return null;
        }

        private static IEnumerator Run()
        {
            foreach (LevelEditorWindow restored in Resources.FindObjectsOfTypeAll<LevelEditorWindow>()) restored.Close();
            Open();
            Check(Reading && Recommendation.Contains("검증을 마친 뒤"), "원시 판 확인 전 추천 숨김");
            IEnumerator read = AwaitRead(); while (read.MoveNext()) yield return read.Current;
            Check(Progress.Contains("중간 결과") && Recommendation.Contains("어려움 : 1회"), "기존 실측 200판의 중간 추천 표시");
            Check(Recommendation.Contains("보통 : 추가 시험 필요") && !Recommendation.Contains("전체 완료"), "없는 등급을 중간 추천 없음으로 단정하지 않음");
            Check(File.ReadAllText(state.header) == state.originalHeader, "UI 조회 후 저장 요약 바이트 보존");
            window.rootVisualElement.Q<Foldout>("balance-detail").value = true;
            for (int frame = 0; frame < 3; frame++) yield return null;
            foreach (string name in new[] { "balance-start", "balance-pause", "balance-stop", "balance-folder", "balance-recommendations" })
            {
                VisualElement element = window.rootVisualElement.Q(name);
                Check(element.worldBound.width > 0 && element.worldBound.xMax <= window.rootVisualElement.worldBound.xMax + 1 &&
                    element.worldBound.yMax <= window.rootVisualElement.worldBound.yMax + 1, "최소 창 조작·추천 접근 " + name);
            }
            Button help = window.rootVisualElement.Q<Button>("manual-help-difficulty.html-move-balance");
            Check(help != null && help.worldBound.width >= 20 && help.tooltip.Contains("사용 설명서") &&
                File.ReadAllText("Docs/MoonRabbitJunkyard/Manual/difficulty.html").Contains("id=\"move-balance\""), "실제 도움말 아이콘·툴팁·문단 경로");
            Click("balance-folder");
            Check(Directory.Exists(Path.GetDirectoryName(state.header)), "실제 결과 폴더 버튼의 경로 존재");
            typeof(BotAnalysisVerification).GetMethod("CaptureAnalysis", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { "stage31-small-ui.png" });
            File.Copy("Logs/BotAnalysisVerification/stage31-small-ui.png", Evidence + "/small-ui.png", true);

            window.Close(); window = null;
            string gamePath = Path.GetDirectoryName(state.header) + "/trials/" + record.trials[0].batchId + "/000000.json";
            string game = File.ReadAllText(gamePath); File.WriteAllText(gamePath, "{broken");
            Open(); read = AwaitRead(); while (read.MoveNext()) yield return read.Current;
            Check(Progress.Contains("읽지 못했습니다") && Recommendation.Contains("추천을 표시하지 않습니다") &&
                window.rootVisualElement.Q<Label>("balance-rows").text == "", "손상 조회는 이전 추천·상세를 지우고 오류 표시");
            window.Close(); window = null; File.WriteAllText(gamePath, game);
            record.rulesVersion = "unsupported-evaluation"; new BotMoveBalanceStore().Save(record);
            Open(); read = AwaitRead(); while (read.MoveNext()) yield return read.Current;
            Check(Recommendation.Contains("다른 기록") && !Recommendation.Contains("어려움 : 1회"), "평가 버전 불일치 추천 차단");
            window.Close(); window = null; record.rulesVersion = BotMoveRecommendations.Version; new BotMoveBalanceStore().Save(record);
            Open(); read = AwaitRead(); while (read.MoveNext()) yield return read.Current;

            Click("balance-start"); Click("balance-pause");
            Check(balance.CanContinue && balance.Session.Record.status == BotBatchStatus.Paused, "새 시험을 즉시 일시정지·장시간 실행 없음");
            Check(!window.rootVisualElement.Q("bot-buttons").enabledInHierarchy && !window.rootVisualElement.Q("batch-buttons").enabledInHierarchy,
                "일시정지 중 다른 봇 중복 실행 차단");
            window.SelectWorkspaceTab(0); window.SelectWorkspaceTab(1);
            Check(balance.Session.Record.status == BotBatchStatus.Paused, "실제 탭 복귀 시 자동 재개 없음");
            Click("balance-pause"); Check(balance.Session.NeedsAdvance, "명시적 재개");
            Click("balance-pause"); Click("balance-stop"); Check(!balance.CanContinue, "일시정지 후 중지");
            window.Close(); window = null;
            new BotMoveBalanceStore().Save(record); Open(); read = AwaitRead(); while (read.MoveNext()) yield return read.Current;
            Check(Recommendation.Contains("어려움 : 1회") && balance.Session == null, "창 다시 열기에서 기존 완료 구간 보존·자동 게임 재개 없음");
            state.originalHeader = File.ReadAllText(state.header); Persist(); marker = 1;
            SessionState.SetBool(Key, true); EditorApplication.update -= Tick; sequence = null;
            EditorUtility.RequestScriptReload();
        }

        [InitializeOnLoadMethod]
        private static void AfterReload()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update += ResumeReload;
        }

        /// <summary>에셋 갱신과 창 복원이 끝난 뒤 실제 재로드 검사를 이어간다.</summary>
        private static void ResumeReload()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || reloadFrames++ < 10) return;
            EditorApplication.update -= ResumeReload;
                try
                {
                    state = JsonUtility.FromJson<State>(File.ReadAllText(Evidence + "/small-ui-state.json"));
                    Results.Clear(); Results.AddRange(File.ReadAllLines(Evidence + "/small-ui-results.txt"));
                    Check(marker == 0 && state.process == Process.GetCurrentProcess().Id, "실제 동일 프로세스 도메인 재로드");
                    Persist();
                    window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().FirstOrDefault();
                    Open(); sequence = VerifyReopened(false); EditorApplication.update += Tick;
                }
                catch (Exception error) { Finish(error, true); }
        }

        /// <summary>기존 사본으로 미완료 수명주기 검사만 다시 수행하며 새 판을 시작하지 않는다.</summary>
        public static void ReloadOnly()
        {
            try
            {
                state = JsonUtility.FromJson<State>(File.ReadAllText(Evidence + "/small-ui-state.json"));
                state.process = Process.GetCurrentProcess().Id;
                Results.AddRange(File.ReadAllLines(Evidence + "/small-ui-results.txt").Where(line => !line.StartsWith("UNVERIFIED")));
                File.WriteAllText(BotMoveBalanceStore.DefaultRoot + "/latest.txt", state.id);
                Open(); state.originalHeader = File.ReadAllText(state.header); Persist();
                marker = 1; SessionState.SetBool(Key, true); EditorUtility.RequestScriptReload();
            }
            catch (Exception error) { Finish(error, true); }
        }

        public static void Restart()
        {
            try
            {
                state = JsonUtility.FromJson<State>(File.ReadAllText(Evidence + "/small-ui-state.json"));
                Results.AddRange(File.ReadAllLines(Evidence + "/small-ui-results.txt"));
                Check(state.process != Process.GetCurrentProcess().Id, "별도 Editor 프로세스로 재시작");
                foreach (LevelEditorWindow restored in Resources.FindObjectsOfTypeAll<LevelEditorWindow>()) restored.Close();
                Open(); sequence = VerifyReopened(true); EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error, true); }
        }

        /// <summary>게임 진행 없이 제어 응답 시간과 실행 중 창 종료의 중단 저장만 확인한다.</summary>
        public static void ControlOnly()
        {
            try
            {
                state = JsonUtility.FromJson<State>(File.ReadAllText(Evidence + "/small-ui-state.json"));
                Results.AddRange(File.ReadAllLines(Evidence + "/small-ui-results.txt"));
                Open();
                BotMoveBalanceStore store = new BotMoveBalanceStore(Evidence + "/control-" + Guid.NewGuid().ToString("N"));
                balance.Store = store;
                Click("balance-start");
                int[] firstSeeds = balance.Session.Record.seeds.ToArray();
                Stopwatch watch = Stopwatch.StartNew();
                Click("balance-pause");
                Check(balance.Session.Record.status == BotBatchStatus.Paused, "응답 측정 일시정지 상태");
                Results.Add("DATA 일시정지 응답 " + watch.Elapsed.TotalMilliseconds + "ms");
                watch.Restart(); Click("balance-stop");
                Check(!balance.CanContinue, "응답 측정 중지 상태");
                Results.Add("DATA 중지 응답 " + watch.Elapsed.TotalMilliseconds + "ms");
                Click("balance-start");
                Check(!firstSeeds.SequenceEqual(balance.Session.Record.seeds), "새 시험은 새 시드 묶음");
                Check(balance.Session.Current == null && balance.Session.Record.trials.Count == 0, "제어 검사의 추가 게임 0판");
                window.Close(); window = null;
                Check(store.Load().status == BotBatchStatus.Interrupted, "실행 중 창 닫기는 중단 기록 저장");
                Finish(null, false);
            }
            catch (Exception error) { Finish(error, false); }
        }

        private static IEnumerator VerifyReopened(bool restart)
        {
            IEnumerator read = AwaitRead(); while (read.MoveNext()) yield return read.Current;
            Check(balance.Session == null && Recommendation.Contains("어려움 : 1회") && Progress.Contains("중간 결과"),
                (restart ? "재시작" : "재로드") + " 후 완료 구간·중간 상태 보존·자동 실행 없음");
            Check(File.ReadAllText(state.header) == state.originalHeader, "수명주기 후 요약 원본 바이트 보존");
            SessionState.EraseBool(Key); Finish(null, restart);
        }

        private static void Tick()
        {
            try { sequence?.MoveNext(); }
            catch (Exception error) { Finish(error, true); }
        }
        private static void Persist()
        {
            File.WriteAllText(Evidence + "/small-ui-state.json", JsonUtility.ToJson(state, true));
            File.WriteAllLines(Evidence + "/small-ui-results.txt", Results);
        }
        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            Check(button != null && button.enabledInHierarchy, "실제 버튼 접근 " + name);
            using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
        private static void Check(bool pass, string message)
        {
            if (!pass) throw new InvalidOperationException(message);
            Results.Add("PASS " + message);
        }
        private static void Finish(Exception error, bool restore)
        {
            EditorApplication.update -= Tick;
            if (window != null) window.Close();
            if (error != null) Results.Add("FAIL " + error);
            if (restore && state != null)
            {
                string pointer = BotMoveBalanceStore.DefaultRoot + "/latest.txt";
                if (state.hadPointer) File.WriteAllText(pointer, state.pointer); else File.Delete(pointer);
                SessionState.EraseBool(Key);
            }
            File.WriteAllLines(Evidence + "/small-ui-results.txt", Results);
            EditorApplication.Exit(Results.Any(line => line.StartsWith("FAIL ")) ? 1 : 0);
        }
    }
}

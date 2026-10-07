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
    public static partial class BotBatchUiVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static LevelEditorWindow window;
        private static LevelInitialStatePanel panel;
        private static LevelDefinition level;
        private static IEnumerator sequence;
        private static double deadline;
        private static BotBatchSession Batch => (BotBatchSession)typeof(LevelInitialStatePanel).GetField("batchSession", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);

        public static void Start()
        {
            Directory.CreateDirectory("Logs/BotBatchVerification");
            BoardCoordinate[] cells = Enumerable.Range(0, 16).Select(i => new BoardCoordinate(i / 4, i % 4)).ToArray();
            level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { cells });
            JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[],\"moveCount\":3,\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
            window = LevelEditorWindow.OpenWorkspace(1, level, true); panel = window.ActiveSimulationPanel;
            typeof(LevelInitialStatePanel).GetField("batchStore", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(panel,
                new BotBatchStore("Library/Match/AutoPlayUiVerification/" + Guid.NewGuid().ToString("N")));
            sequence = Run(); deadline = EditorApplication.timeSinceStartup + 180; EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            Exception failure = null;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("반복 시험 UI 검증 시간 초과");
                if (sequence.MoveNext()) return;
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            EditorApplication.update -= Tick;
            if (window != null) window.Close();
            if (level != null) UnityEngine.Object.DestroyImmediate(level);
            File.WriteAllLines("Logs/BotBatchVerification/ui-results.txt", Results);
            if (failure != null) Debug.LogException(failure);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static IEnumerator Run()
        {
            yield return null;
            window.position = new Rect(20, 20, 760, 860);
            VisualElement root = panel.rootVisualElement;
            root.Q<Foldout>("bot-trial").value = true;
            IntegerField count = root.Q<IntegerField>("batch-count");
            Check(count.value == 100, "실제 화면 기본값 전략별 100회");
            count.value = 0;
            Check(!root.Q<Button>("batch-new").enabledInHierarchy, "횟수 0 입력 실행 차단");
            count.value = 1;
            string original = JsonUtility.ToJson(level); bool dirty = EditorUtility.IsDirty(level);
            Click(root, "batch-new");
            BotBatchSession first = Batch;
            Check(first != null && first.Record.Total == 2 && !root.Q<Button>("bot-new").enabledInHierarchy,
                "반복 시작 즉시 총 횟수 반영·한 판 입력 잠금");
            while (Batch.Current?.State == null && Batch.NeedsAdvance) yield return null;
            Click(root, "batch-pause");
            Check(Batch.Record.status == BotBatchStatus.Paused && root.Q<Button>("batch-stop").enabledInHierarchy &&
                root.Q<Button>("batch-pause").text == "재개", "일시정지 중 재개·중지 제공");
            int recorded = Batch.Record.Recorded;
            for (int i = 0; i < 10; i++) yield return null;
            Check(Batch.Record.Recorded == recorded, "일시정지 뒤 Editor 갱신이 다음 판을 실행하지 않음");
            Check(panel.CurrentState == Batch.Current.State && root.Q("initial-cell-0-0") != null,
                "화면 갱신 간격 사이의 일시정지도 실제 보드 표시");
            typeof(ConnectionGraphVerification).GetField("window", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, window);
            typeof(ConnectionGraphVerification).GetMethod("RaiseWindow", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            yield return null; yield return null;
            foreach (string name in new[] { "batch-new", "batch-repeat", "batch-pause", "batch-stop", "batch-count", "manual-help-play.html-batch" })
            {
                VisualElement control = root.Q(name);
                Check(control != null && control.worldBound.width > 10 && control.worldBound.height > 10 &&
                    control.worldBound.xMin >= root.worldBound.xMin && control.worldBound.xMax <= root.worldBound.xMax &&
                    control.worldBound.yMin >= root.worldBound.yMin && control.worldBound.yMax <= root.worldBound.yMax,
                    "760×860 필수 조작·도움말 영역 노출 " + name);
            }
            Texture2D capture = new Texture2D((int)window.position.width, (int)window.position.height, TextureFormat.RGB24, false);
            capture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(window.position.position, capture.width, capture.height)); capture.Apply();
            File.WriteAllBytes("Logs/BotBatchVerification/batch-ui.png", capture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(capture);
            Click(root, "batch-pause");
            panel.SetVisible(false);
            Check(Batch.Record.status == BotBatchStatus.Paused, "탭 이탈 자동 일시정지");
            panel.SetVisible(true);
            Check(Batch.Record.status == BotBatchStatus.Paused, "탭 복귀 자동 재개 금지");
            JsonUtility.FromJsonOverwrite("{\"levelNumber\":271}", level);
            panel.SetLevel(null); panel.SetLevel(level);
            Check(ReferenceEquals(first, Batch) && root.Q<Label>("batch-summary").text.Contains("다른 저장 사본"),
                "원본·레벨 변경에도 사본 유지와 과거 조건 표시");
            Click(root, "batch-pause");
            while (Batch.NeedsAdvance) yield return null;
            Check(Batch.Record.status == BotBatchStatus.Completed && Batch.Record.finished == 2 &&
                root.Q<ProgressBar>("batch-progress").value == 100, "실제 두 판 완료·진행률 100 표시");
            int[] seeds = Batch.Record.seeds.ToArray(); string fingerprint = Batch.Record.fingerprint;
            Click(root, "batch-repeat");
            Check(!ReferenceEquals(first, Batch) && Batch.Record.seeds.SequenceEqual(seeds) && Batch.Record.fingerprint == fingerprint,
                "현재 편집 내용과 다른 이전 사본·시드의 실제 재시험");
            Click(root, "batch-pause"); Click(root, "batch-stop");
            Check(Batch.Record.status == BotBatchStatus.Stopped && root.Q<ProgressBar>("batch-progress").value < 100,
                "일시정지 후 중지·일부 실행 표시");
            JsonUtility.FromJsonOverwrite(original, level);
            Check(JsonUtility.ToJson(level) == original && EditorUtility.IsDirty(level) == dirty, "UI 실행이 원본 JSON·dirty를 변경하지 않음");
            Click(root, "bot-manual");
            Check(Batch == null && root.Q("manual-toolbar").enabledInHierarchy, "수동 시험 전환은 반복 소유권 해제");
            Click(root, "batch-new"); first = Batch;
            window.Close(); window = null;
            Check(!first.CanContinue && first.Record.status == BotBatchStatus.Interrupted && first.Current == null,
                "창 종료는 기록 보존·실행 객체 해제");
        }

        private static void Click(VisualElement root, string name)
        {
            Button button = root.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("버튼 사용 불가 " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }
        private static void Check(bool pass, string description)
        {
            if (!pass) throw new InvalidOperationException(description);
            Results.Add("PASS " + description);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>편집 사본부터 수동 조작·새 반복 시험·분석까지 동일한 실제 작업창에서 연결 검수한다.</summary>
    public static partial class LevelWorkflowVerification
    {
        private const string Evidence = "Logs/LevelWorkflowVerification";
        private static readonly List<string> Results = new List<string>();
        private static LevelEditorWindow window;
        private static LevelDefinition level;
        private static LevelInitialStatePanel play;
        private static string folder, recordRoot;
        private static IEnumerator sequence;
        private static double next;
        private static int scenario;
        private static string resultOverride;
        private static string CaseName => new[] { "basic", "flow", "connections" }[scenario];
        private static BotBatchSession Batch => (BotBatchSession)typeof(LevelInitialStatePanel).GetField("batchSession", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(play);

        public static void Basic() => Start(0);
        public static void Flow() => Start(1);
        public static void Connections() => Start(2);

        /// <summary>완료한 반복 시험은 다시 실행하지 않고 결과 화면의 실패한 연결만 재현한다.</summary>
        public static void InspectBasicResult()
        {
            Results.Clear(); resultOverride = "basic-result-check";
            string path = File.ReadAllText(Evidence + "/basic-record.txt");
            BotBatchRecord record = JsonUtility.FromJson<BotBatchRecord>(File.ReadAllText(Path.Combine(path, "batch.json")));
            level = ScriptableObject.CreateInstance<LevelDefinition>(); JsonUtility.FromJsonOverwrite(record.definitionJson, level);
            window = LevelEditorWindow.OpenWorkspace(3, level, true); window.ShowUtility(); window.position = new Rect(20, 20, 1100, 850);
            sequence = InspectResult(path); EditorApplication.update += Tick;
        }

        private static IEnumerator InspectResult(string path)
        {
            yield return null;
            LevelAnalysisPanel panel = (LevelAnalysisPanel)typeof(LevelEditorWindow).GetField("analysisPanel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
            panel.OpenRecord(path);
            while (panel.Analysis?.IsDone != true) yield return null;
            Click("difficulty-cases"); yield return null;
            ListView cases = window.rootVisualElement.Q<ListView>("analysis-cases");
            Results.Add("STATE index=" + cases.selectedIndex + " notice=" + window.rootVisualElement.Q<Label>("analysis-notice").text + " case=" + window.rootVisualElement.Q<Label>("analysis-case-info").text);
            Check(window.rootVisualElement.Q<Button>("analysis-replay-start").enabledInHierarchy, "사례 이동 직후 재생 준비 가능");
            Click("analysis-replay-start");
            while (panel.ReplaySession?.Status == BotReplayStatus.Preparing) yield return null;
            Click("analysis-replay-run");
            while (panel.ReplaySession?.NeedsAdvance == true) yield return null;
            Check(panel.ReplaySession?.Status == BotReplayStatus.Completed, "신규 실제 사례 재생 완료");
        }

        private static void Start(int kind)
        {
            scenario = kind;
            Directory.CreateDirectory(Evidence); Results.Clear();
            string sourceRoot = File.ReadAllText("Logs/BotDifficultyVerification/runs-path.txt");
            BotBatchRecord source = JsonUtility.FromJson<BotBatchRecord>(File.ReadAllText(Path.Combine(sourceRoot, "a4f6287c12c24cadbd8c2d40df041ec6", "batch.json")));
            folder = "Assets/__LevelWorkflow_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            level = ScriptableObject.CreateInstance<LevelDefinition>(); JsonUtility.FromJsonOverwrite(source.definitionJson, level);
            if (scenario != 0)
            {
                UnityEngine.Object.DestroyImmediate(level);
                level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { Enumerable.Range(0, 25).Select(i => new BoardCoordinate(i / 5, i % 5)).ToArray() });
                JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[],\"moveCount\":6,\"missions\":[{\"kind\":0,\"color\":0,\"count\":6}]}", level);
                for (int column = 0; column < 5; column++)
                    typeof(SettlementVerification).GetMethod("Source", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                        new object[] { level, new BoardCoordinate(0, column), SupplyExhaustion.Random,
                            new[] { new SupplyItem(column == 2 ? SupplyKind.RandomPower : SupplyKind.RandomNormal, 1) } });
                if (scenario == 1)
                    Check(LevelFlowEditing.SetPortal(level, new BoardCoordinate(4, 0), null) == null, "검사 소유 통로 입구 준비");
                else
                {
                    LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Generator, RequiredCharge = 3 }, new[] { new BoardCoordinate(2, 0) });
                    LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 6 }, new[] { new BoardCoordinate(3, 4) });
                    JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":1,\"count\":1}]}", level);
                }
            }
            AssetDatabase.CreateAsset(level, folder + "/Workflow.asset"); AssetDatabase.SaveAssetIfDirty(level);
            recordRoot = Path.GetFullPath(Evidence + "/" + CaseName + "-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(Evidence + "/" + CaseName + "-root.txt", recordRoot);
            window = LevelEditorWindow.OpenWorkspace(0, level, true); window.ShowUtility(); window.position = new Rect(20, 20, 1100, 850);
            sequence = Run(); EditorApplication.update += Tick;
        }

        private static void Check(bool value, string description)
        {
            Results.Add((value ? "PASS " : "FAIL ") + description);
            if (!value) throw new InvalidOperationException(description);
        }

        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            Check(button != null && button.enabledInHierarchy, "작업창 버튼 " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + .1;
            Exception failure = null;
            try { if (sequence.MoveNext()) return; }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); Debug.LogException(error); }
            EditorApplication.update -= Tick;
            if (window != null) window.Close();
            // 검증이 만든 고유 에셋 폴더만 제거한다. 사용자 레벨과 기록은 건드리지 않는다.
            if (folder != null && folder.StartsWith("Assets/__LevelWorkflow_", StringComparison.Ordinal))
                Results.Add((AssetDatabase.DeleteAsset(folder) ? "PASS " : "FAIL ") + "소유 임시 에셋 정리");
            File.WriteAllLines(Evidence + "/" + (resultOverride ?? CaseName) + "-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static IEnumerator Run()
        {
            yield return null;
            typeof(ConnectionGraphVerification).GetField("window", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, window);
            typeof(ConnectionGraphVerification).GetMethod("RaiseWindow", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            if (scenario != 0)
            {
                BoardCoordinate origin = scenario == 1 ? new BoardCoordinate(4, 0) : new BoardCoordinate(2, 0);
                typeof(LevelEditorWindow).GetMethod("SelectCell", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { origin });
                yield return null;
                LevelConnectionGraph graph = window.rootVisualElement.Q<LevelConnectionGraph>();
                string portName = scenario == 1 ? "portal-entrance-4-0" : "connection-port-0-1";
                VisualElement port = graph.Q(portName);
                Check(port != null, "대표 레벨 실제 연결점 " + portName);
                using (PointerDownEvent evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = port.worldBound.center }))
                { evt.target = port; port.SendEvent(evt); }
                yield return null;
                Vector2 end = scenario == 1 ? graph.LocalToWorld(new Vector2(4 * 40 + 20, 2 * 40 + 20)) : graph.Q("connection-port-1-0").worldBound.center;
                using (PointerMoveEvent evt = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, button = 0, mousePosition = end }))
                { evt.target = graph; graph.SendEvent(evt); }
                yield return null;
                using (PointerUpEvent evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = end }))
                { evt.target = graph; graph.SendEvent(evt); }
                yield return null;
                Check(scenario == 1 ? level.Flow.Portals[0].HasExit : level.Connections.Count == 1, "대표 레벨 드래그 연결 반영");
                // 통로가 만든 합류 후보의 순서를 명시한다. 임의의 충돌을 무시하지 않는다.
                foreach (BoardCoordinate mergeCell in Enumerable.Range(0, 25).Select(i => new BoardCoordinate(i / 5, i % 5)))
                {
                    List<BoardCoordinate> sources = LevelFlowRules.Sources(level, mergeCell);
                    if (sources.Count > 1) Check(LevelFlowEditing.SetMerge(level, mergeCell, sources) == null, "합류 순서 지정 " + mergeCell);
                }
                Check(LevelDefinitionValidator.Validate(level).Count == 0, "연결·공급 대표 정의 정합성: " + string.Join(" / ", LevelDefinitionValidator.Validate(level)));
            }
            Click("inspector-tab-1"); yield return null;
            IntegerField moves = window.rootVisualElement.Query<IntegerField>().ToList().First(field => field.bindingPath == "moveCount");
            moves.value = 7;
            for (int i = 0; i < 5; i++) yield return null;
            Check(level.MoveCount == 7 && EditorUtility.IsDirty(level), "실제 인스펙터 편집·dirty 반영: 값=" + level.MoveCount + " dirty=" + EditorUtility.IsDirty(level));
            using (KeyDownEvent save = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.S, modifiers = EventModifiers.Control }))
            { save.target = window.rootVisualElement; window.rootVisualElement.SendEvent(save); }
            yield return null;
            Check(!EditorUtility.IsDirty(level) && File.ReadAllText(AssetDatabase.GetAssetPath(level)).Contains("moveCount: 7"), "Ctrl+S 후 디스크와 화면 값 일치");
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level));
            window.rootVisualElement.Q<UnityEditor.UIElements.ToolbarMenu>("workspace-menu-level").menu.MenuItems()
                .OfType<DropdownMenuAction>().First(item => item.name == "이름 변경…").Execute(); yield return null;
            string displayName = new[] { "기본 수집 통합 검수", "통로와 파워 공급 통합 검수", "발전기와 상자 통합 검수" }[scenario];
            window.rootVisualElement.Q("level-name-panel").Q<TextField>().value = displayName;
            Click("confirm-level-name"); yield return null;
            Check(level.name == displayName && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level)) == guid, "이름 변경 후 GUID 보존");
            string original = JsonUtility.ToJson(level), disk = File.ReadAllText(AssetDatabase.GetAssetPath(level));
            Click("play-level"); yield return null; play = window.ActiveSimulationPanel;
            typeof(LevelInitialStatePanel).GetField("batchStore", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(play, new BotBatchStore(recordRoot));
            for (int i = 0; play.IsSearching && i < 1000; i++) yield return null;
            Click("manual-start"); yield return null;
            for (int i = 0; (play.IsSearching || play.CascadeRunning) && i < 1000; i++) yield return null;
            if (play.Execution == null) { Click("manual-start"); yield return null; }
            Check(play.Execution != null, "저장 레벨의 수동 플레이 시작");
            BoardCoordinate cell = play.CurrentState.Cells.First(c => c.Content == RuntimeContent.Normal).Coordinate;
            Click("item-Hammer"); Click($"initial-cell-{cell.Row}-{cell.Column}");
            for (int i = 0; play.CascadeRunning && i < 1000; i++) yield return null;
            Check(!play.CascadeRunning && play.Execution.ItemUses.Count == 1, "수동 조작과 후속 처리 완료");
            Click("manual-restart");
            for (int i = 0; play.IsSearching && i < 1000; i++) yield return null;
            Check(JsonUtility.ToJson(level) == original && File.ReadAllText(AssetDatabase.GetAssetPath(level)) == disk, "수동 시험·재시작 원본 보존");
            play.rootVisualElement.Q<Foldout>("bot-trial").value = true;
            play.rootVisualElement.Q<IntegerField>("batch-count").value = 100;
            Click("batch-new"); yield return null;
            Check(Batch != null && Batch.Record.Total == 200, "실제 UI로 신규 100시드×두 전략 시작");
            Click("batch-pause"); int count = Batch.Record.Recorded;
            yield return null; yield return null;
            Check(Batch.Record.status == BotBatchStatus.Paused && Batch.Record.Recorded == count, "일시정지 상태 보존");
            Click("workspace-tab-0"); yield return null; Click("workspace-tab-1"); yield return null;
            Check(Batch.Record.status == BotBatchStatus.Paused, "탭 복귀 후 명시적 재개 대기");
            Click("batch-pause");
            while (Batch.NeedsAdvance)
            { File.WriteAllText(Evidence + "/" + CaseName + "-progress.txt", Batch.Record.finished + "/200 " + Batch.Record.status); yield return null; }
            Check(Batch.Record.status == BotBatchStatus.Completed && Batch.Record.finished == 200, "UI 신규 200판 완료: " + Batch.Record.message);
            string record = Path.Combine(recordRoot, Batch.Record.id); File.WriteAllText(Evidence + "/" + CaseName + "-record.txt", record);
            Click("workspace-tab-3"); yield return null;
            LevelAnalysisPanel analysis = (LevelAnalysisPanel)typeof(LevelEditorWindow).GetField("analysisPanel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
            analysis.OpenRecord(record);
            for (int i = 0; analysis.Analysis?.IsDone != true && i < 1000; i++) yield return null;
            Check(analysis.Analysis?.Error == null && analysis.Analysis?.Games.Count == 200, "같은 창에서 신규 결과 읽기");
            Check(window.rootVisualElement.Q<Label>("analysis-identity").text.Contains("같은 정의"), "편집 원본과 시험 정의 일치 안내");
            Check(window.rootVisualElement.Q<Label>("analysis-difficulty-title") != null, "신규 기록 난이도 또는 판단 보류 표시");
            typeof(BotAnalysisVerification).GetField("window", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, window);
            typeof(BotAnalysisVerification).GetMethod("CaptureAnalysis", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "workflow-basic.png" });
            File.Copy("Logs/BotAnalysisVerification/workflow-basic.png", Evidence + "/" + CaseName + "-result.png", true);
            Click("difficulty-cases"); yield return null;
            Click("analysis-replay-start");
            for (int i = 0; analysis.ReplaySession?.Status == BotReplayStatus.Preparing && i < 1000; i++) yield return null;
            Click("analysis-replay-run");
            for (int i = 0; analysis.ReplaySession?.NeedsAdvance == true && i < 1000; i++) yield return null;
            Check(analysis.ReplaySession?.Status == BotReplayStatus.Completed, "신규 기록 사례 재생 완료");
            Check(JsonUtility.ToJson(level) == original && !EditorUtility.IsDirty(level), "전체 흐름 후 편집 원본·dirty 보존");
        }
    }
}

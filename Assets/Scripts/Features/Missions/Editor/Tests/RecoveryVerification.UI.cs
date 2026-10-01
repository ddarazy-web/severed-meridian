using Board;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Simulation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class RecoveryVerification
    {
        private static LevelInitialStatePanel window;
        private static LevelDefinition fixture;
        private static IEnumerator sequence;
        private static double nextTick;
        private static string folder;
        [Serializable] private sealed class Saved { public string folder, path, json, guid, replay; public int process; }
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static LevelDefinition PlayFixture()
        {
            LevelDefinition level = Make(2); Place(level, C(7, 0));
            string error = LevelFlowEditing.SetPath(level, Enumerable.Range(0, BoardDefinition.DefaultRows).Select(row => C(row, 0)).ToArray());
            if (error != null) throw new InvalidOperationException(error);
            Invoke(typeof(PowerEffectVerification), "Place", level, C(8, 0), InitialBlockKind.Rocket, RocketDirection.Vertical, RabbitColor.Type1);
            Invoke(typeof(PowerEffectVerification), "Place", level, C(6, 0), InitialBlockKind.Drone, RocketDirection.Horizontal, RabbitColor.Type1);
            Invoke(typeof(SettlementVerification), "Source", level, C(0, 0), SupplyExhaustion.Stop,
                new[] { new SupplyItem(SupplyKind.Recovery), new SupplyItem(SupplyKind.Bomb) });
            return level;
        }
        private static void Finish(BoardActionExecutor executor)
        {
            int steps = 0;
            while (executor.HasPendingCascade && steps++ < 500)
            {
                CascadeStepResult step = executor.AdvanceCascade();
                if (!step.IsApplied) throw new InvalidOperationException(step.Message);
            }
            if (executor.HasPendingCascade) throw new InvalidOperationException("회수 연쇄 한도");
        }
        private static string Replay(LevelDefinition level)
        {
            BoardActionExecutor executor = new BoardActionExecutor(Build(level));
            BoardActionResult action = executor.Activate(C(8, 0));
            if (!action.IsApplied) throw new InvalidOperationException(action.Message);
            Finish(executor);
            if (executor.State.Recoveries.Count != 2 || executor.State.Missions[0].Progress != 2)
                throw new InvalidOperationException("재현 회수 수량 불일치");
            if (!executor.TurnEffects.Targeting.Any(r => r.Event == TargetingEvent.Landed)) throw new InvalidOperationException("재현 드론 착탄 기록 없음");
            return BoardActionExecutor.Version + RecoveryRules.Version + SettlementResolution.Version +
                Snapshot(action) + Snapshot(executor.State) + Snapshot(executor.TurnEffects) + Snapshot(executor.CascadeHistory);
        }
        public static void Start()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            if (File.Exists(Evidence + "/state.json")) throw new InvalidOperationException("이전 회수 검증 Restart 필요");
            folder = "Assets/__RecoveryVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                fixture = PlayFixture(); AssetDatabase.CreateAsset(fixture, folder + "/Recovery.asset"); AssetDatabase.SaveAssets();
                Check(LevelDefinitionValidator.Validate(fixture).Count == 0 && new StartConditionReport(Build(fixture)).IsSatisfied, "실제 회수 시험 정합성·시작 조건");
                Check(EditorApplication.ExecuteMenuItem("Match/초기 보드 확인"), "Match 메뉴 진입");
                window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().Single().ActiveSimulationPanel;
                window.Owner.position = new Rect(10, 10, 1000, 780); window.Owner.Focus();
                sequence = UI(); EditorApplication.update += Tick;
            }
            catch (Exception error) { FinishUI(error); }
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.4;
            try
            {
                if (sequence.MoveNext()) return;
                File.WriteAllText(Evidence + "/state.json", JsonUtility.ToJson(new Saved { folder = folder, path = AssetDatabase.GetAssetPath(fixture), json = JsonUtility.ToJson(fixture),
                    guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(fixture)), replay = Replay(fixture), process = System.Diagnostics.Process.GetCurrentProcess().Id }, true));
                FinishUI(null);
            }
            catch (Exception error) { FinishUI(error); }
        }
        private static void FinishUI(Exception error)
        {
            EditorApplication.update -= Tick;
            if (error != null) { Results.Add("FAIL " + error); Debug.LogException(error); if (folder != null) AssetDatabase.DeleteAsset(folder); }
            File.WriteAllLines(Evidence + "/ui-results.txt", Results);
            if (window != null) window.Owner.Close(); EditorApplication.Exit(error == null ? 0 : 1);
        }
        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("버튼 사용 불가 " + name);
            using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled(); submit.target = button; button.SendEvent(submit);
        }
        private static IEnumerator UI()
        {
            yield return null;
            typeof(SettlementVerification).GetField("window", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, window);
            Invoke(typeof(SettlementVerification), "RaiseVerificationWindow"); yield return null;
            VisualElement root = window.rootVisualElement;
            root.Q<IntegerField>("initial-seed").value = 12345;
            root.Q<PopupField<string>>("initial-mode").value = "시작 조건 구성";
            root.Q<ObjectField>("initial-level").value = fixture; Click("initial-build");
            for (int wait = 0; (window.IsSearching || window.CurrentState == null) && wait < 100; wait++) yield return null;
            string json = JsonUtility.ToJson(fixture), file = File.ReadAllText(AssetDatabase.GetAssetPath(fixture)), initial = Snapshot(window.CurrentState);
            Check(root.Q<Button>("initial-cell-7-0").text.Contains("회수") && root.Q<Button>("initial-cell-8-0").text.Contains("도착"), "부품과 도착 바닥 보드 표시");
            Capture("initial-wide.png");
            BoardActionExecutor manual = new BoardActionExecutor(window.CurrentState);
            root.Q<Toggle>("execution-mode").value = true;
            Click("initial-cell-8-0"); Click("execution-activate"); yield return null;
            Check(window.Execution.LastApplied.IsApplied && window.Execution.State.CellAt(C(7, 0)).Content == RuntimeContent.Recovery, "실제 UI 로켓 관통·부품 불파괴");
            manual.Activate(C(8, 0)); Finish(manual);
            Click("execution-settle"); yield return null;
            Check(window.Execution.State.Recoveries.Count == 2 && root.Q<Label>("initial-overview").text.Contains("회수 부품"), "실제 낙하·고정 공급·회수 기록");
            Capture("settled-wide.png");
            Click("execution-cascade"); for (int wait = 0; window.CascadeRunning && wait < 500; wait++) yield return null;
            Check(!window.CascadeRunning && Snapshot(window.Execution.State) == Snapshot(manual.State) &&
                Snapshot(window.Execution.TurnEffects) == Snapshot(manual.TurnEffects) && Snapshot(window.Execution.CascadeHistory) == Snapshot(manual.CascadeHistory), "단계·끝까지 보드·난수·회수·공급·문맥·이력 일치");
            Check(root.Q<Label>("initial-details").text.Contains("회수 누적 2") && root.Q<Label>("initial-details").text.Contains("남은 목표 0"), "연쇄 후 실제 회수·남은 목표 표시");
            Check(window.Execution.State.MovesRemaining == 19 && window.Execution.State.Supply.Sources[0].ItemIndex == 2, "한 수 소모·고정 목록 커서");
            window.Owner.position = new Rect(10, 10, 680, 780); yield return null; Capture("complete-narrow.png");
            Check(root.Q<Button>("execution-cascade").worldBound.xMax <= 680, "680폭 버튼 접근");
            root.Q<ScrollView>("initial-board-scroll").scrollOffset = new Vector2(90, 40); yield return null; Capture("complete-scrolled.png");
            Check(Snapshot(window.Execution.State) == Snapshot(manual.State), "표시·스크롤 상태와 난수 무변경");
            root.Q<ScrollView>("initial-board-scroll").scrollOffset = Vector2.zero; window.Owner.position = new Rect(10, 10, 1000, 780); yield return null;
            Check(JsonUtility.ToJson(fixture) == json && File.ReadAllText(AssetDatabase.GetAssetPath(fixture)) == file && !EditorUtility.IsDirty(fixture) && Snapshot(window.CurrentState) == initial, "원본 JSON·파일·dirty·초기 사본 보존");
            Click("execution-reset"); yield return null;
            Check(window.Execution == null && Snapshot(window.CurrentState) == initial, "동일 시드 재시작·회수 및 커서 초기화");
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-8-0"); Click("execution-activate"); Click("execution-cascade");
            root.Q<ObjectField>("initial-level").value = null; yield return null;
            Check(window.Execution == null && !window.CascadeRunning, "레벨 전환시 연쇄 실행 해제");
            window.Owner.Close(); yield return null;
            Check(window == null || !window.CascadeRunning, "창 종료 후 실행 수명 해제");
        }
        private static void Capture(string name)
        {
            Rect rect = window.Owner.position; Texture2D texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            texture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, (int)rect.width, (int)rect.height)); texture.Apply();
            File.WriteAllBytes(Evidence + "/" + name, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
        public static void Restart()
        {
            Results.Clear();
            try
            {
                Saved saved = JsonUtility.FromJson<Saved>(File.ReadAllText(Evidence + "/state.json"));
                Check(saved.process != System.Diagnostics.Process.GetCurrentProcess().Id, "독립 Unity 프로세스");
                LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(saved.path);
                Check(JsonUtility.ToJson(level) == saved.json && AssetDatabase.AssetPathToGUID(saved.path) == saved.guid, "원본 JSON·GUID 재로드");
                Check(Replay(level) == saved.replay, "행동·보드·회수·미션·공급·난수·문맥·연쇄 독립 재현");
                if (!saved.folder.StartsWith("Assets/__RecoveryVerification_", StringComparison.Ordinal) || saved.folder.Contains("..")) throw new InvalidOperationException("임시 경로 오류");
                Check(AssetDatabase.DeleteAsset(saved.folder), "소유 임시 에셋 정리");
                File.Move(Evidence + "/state.json", Evidence + "/completed-" + DateTime.UtcNow.Ticks + ".json");
                File.WriteAllLines(Evidence + "/restart-results.txt", Results); EditorApplication.Exit(0);
            }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/restart-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}

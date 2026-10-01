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
    public static partial class FixedObstacleVerification
    {
        private static LevelInitialStatePanel window;
        private static LevelDefinition[] levels;
        private static string folder;
        private static IEnumerator sequence;
        private static double nextTick;
        [Serializable] private sealed class Saved
        { public string folder; public int process; public string[] paths, json, guids, snapshots; }

        private static string Replay(LevelDefinition level)
        {
            string text = "";
            foreach (int seed in new[] { 12345, 7 })
                foreach (bool reverse in new[] { false, true })
                {
                    BoardActionExecutor executor = new BoardActionExecutor(Build(level, seed));
                    BoardActionResult action = executor.Swap(reverse ? C(4, 5) : C(4, 4), reverse ? C(4, 4) : C(4, 5));
                    if (!action.IsApplied) throw new InvalidOperationException(action.Message); Finish(executor);
                    text += BoardActionExecutor.Version + PowerEffectResolution.Version + MatchResolution.Version + SettlementResolution.Version + Snapshot(action) + Snapshot(executor.State) + ContextSnapshot(executor) + Snapshot(executor.CascadeHistory);
                    if (executor.Phase == BoardActionPhase.Ready)
                    {
                        ActionCandidate next = ActionQuery.Find(executor.State).First();
                        BoardActionResult second = next.Second.HasValue ? executor.Swap(next.First, next.Second.Value) : executor.Activate(next.First);
                        if (!second.IsApplied) throw new InvalidOperationException(second.Message); Finish(executor);
                        text += Snapshot(second) + Snapshot(executor.State) + ContextSnapshot(executor) + Snapshot(executor.CascadeHistory);
                    }
                }
            return text;
        }
        private static string ContextSnapshot(BoardActionExecutor executor)
        {
            // 프로퍼티 스냅샷에서 빠지는 턴 보호·피해·유입 기록도 독립 프로세스로 비교한다.
            TurnEffectContext context = executor.TurnEffects;
            return Snapshot(context) + string.Join(";", ((IEnumerable)typeof(TurnEffectContext).GetField("hitCells", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context)).Cast<object>().Select(x => x.ToString()).OrderBy(x => x)) + string.Join(";", executor.State.Cells.Select(c =>
                context.IsProtected(c.Coordinate) + "," + context.HasFired(c.Coordinate) + "," + context.LastArrival(c.Coordinate) + "," + context.HasDamagedWeb(c.Coordinate) + "," + context.HasDamagedDust(c.Coordinate))) +
                string.Join(";", Enumerable.Range(0, executor.State.Obstacles.Count).Select(context.HasDamaged));
        }
        public static void Start()
        {
            Directory.CreateDirectory(Evidence);
            if (File.Exists(Evidence + "/state.json")) throw new InvalidOperationException("이전 검증 Restart 필요");
            Results.Clear(); folder = "Assets/__FixedObstacleVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                levels = new[] { CreateFixture(1), CreateFixture(6), CreateFixture(8) };
                for (int i = 0; i < levels.Length; i++) AssetDatabase.CreateAsset(levels[i], folder + "/Pair" + i + ".asset");
                AssetDatabase.SaveAssets();
                Check(EditorApplication.ExecuteMenuItem("Match/초기 보드 확인"), "Match 메뉴 진입");
                window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().Single().ActiveSimulationPanel; window.Owner.position = new Rect(10, 10, 1000, 780); window.Owner.Focus();
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
                string[] paths = levels.Select(AssetDatabase.GetAssetPath).ToArray();
                foreach (LevelDefinition level in levels) AssetDatabase.SaveAssetIfDirty(level);
                File.WriteAllText(Evidence + "/state.json", JsonUtility.ToJson(new Saved { folder = folder, process = System.Diagnostics.Process.GetCurrentProcess().Id,
                    paths = paths, json = levels.Select(JsonUtility.ToJson).ToArray(), guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray(), snapshots = levels.Select(Replay).ToArray() }, true));
                Check(true, "고정장애물3종×2시드×양방향 및 가능한 다음 수 재현 기준 저장"); FinishUI(null);
            }
            catch (Exception error) { FinishUI(error); }
        }
        private static void FinishUI(Exception error)
        {
            EditorApplication.update -= Tick;
            if (error != null) { Results.Add("FAIL " + error); Debug.LogException(error); AssetDatabase.DeleteAsset(folder); }
            File.WriteAllLines(Evidence + "/ui-results.txt", Results);
            if (window != null) window.Owner.Close(); EditorApplication.Exit(error == null ? 0 : 1);
        }
        public static void Restart()
        {
            Results.Clear(); Saved saved = JsonUtility.FromJson<Saved>(File.ReadAllText(Evidence + "/state.json"));
            Check(saved.process != System.Diagnostics.Process.GetCurrentProcess().Id, "독립 Unity 프로세스 확인");
            for (int i = 0; i < saved.paths.Length; i++)
            {
                LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(saved.paths[i]);
                Check(JsonUtility.ToJson(level) == saved.json[i] && AssetDatabase.AssetPathToGUID(saved.paths[i]) == saved.guids[i], "원본 JSON/GUID 재로드 " + i);
                Check(Replay(level) == saved.snapshots[i], "조합/방향/색/예약/착탄/미션/전체 상태 독립 재현 " + i);
            }
            if (!saved.folder.StartsWith("Assets/__FixedObstacleVerification_", StringComparison.Ordinal) || saved.folder.Contains("..")) throw new InvalidOperationException("정리 경로 오류");
            Check(AssetDatabase.DeleteAsset(saved.folder), "소유 임시 에셋 정리"); File.WriteAllLines(Evidence + "/restart-results.txt", Results);
            File.Move(Evidence + "/state.json", Evidence + "/completed-" + DateTime.UtcNow.Ticks + ".json");
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
            Invoke(typeof(SettlementVerification), "RaiseVerificationWindow", null); yield return null;
            VisualElement root = window.rootVisualElement;
            root.Q<IntegerField>("initial-seed").value = 12345; root.Q<PopupField<string>>("initial-mode").value = "시작 조건 구성";
            for (int i = 0; i < levels.Length; i++)
            {
                root.Q<ObjectField>("initial-level").value = levels[i]; Click("initial-build"); yield return null;
                string json = JsonUtility.ToJson(levels[i]), file = File.ReadAllText(AssetDatabase.GetAssetPath(levels[i])), initial = Snapshot(window.CurrentState);
                Check(root.Q<Button>("initial-cell-8-6").text.Contains("토3") && root.Q<Button>("initial-cell-8-6").ClassListContains("rabbit-2") && root.Q<Button>("initial-cell-7-7").text.Contains("#2") && root.Q<Button>("initial-cell-8-8").text.Contains("#2"), "자물쇠 색/번호 표식 및 폐가전 동일 본체 표시 " + i);
                root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-4-4"); Click("initial-cell-4-5"); Click("execution-swap"); yield return null;
                Check(window.Execution.LastApplied.IsApplied && window.Execution.TurnEffects.Combination.Kind == new[] { PowerCombinationKind.RocketBomb, PowerCombinationKind.MagnetRocket, PowerCombinationKind.MagnetDrone }[i] && root.Q<Label>("initial-details").text.Contains("중심"), "실제 UI 조합/중심 표시 " + i);
                if (i > 0) Check(root.Q<Label>("initial-details").text.Contains("선택 색") && root.Q<Label>("initial-overview").text.Contains("조합 변환 기록"), "실제 UI 색/변환 기록 " + i);
                if (i >= 0) { Capture("pair" + i + "-wide.png"); window.Owner.position = new Rect(10, 10, 680, 780); yield return null; Capture("pair" + i + "-narrow.png");
                    Check(root.Q<Button>("execution-cascade").worldBound.xMax <= 680, "680폭 실행 버튼 접근 " + i); window.Owner.position = new Rect(10, 10, 1000, 780); }
                BoardActionExecutor manual = new BoardActionExecutor(window.CurrentState); manual.Swap(C(4, 4), C(4, 5)); Finish(manual);
                Click("execution-cascade"); for (int wait = 0; window.CascadeRunning && wait < 500; wait++) yield return null;
                Check(root.Q<Label>("initial-details").text.Contains("금고 제거") && root.Q<Label>("initial-details").text.Contains("폐가전 제거") && root.Q<Label>("initial-overview").text.Contains("본체 공유 내구도"), "본체/실제 미션 표시 " + i);
                Capture("pair" + i + "-settled.png");
                Check(!window.CascadeRunning && Snapshot(window.Execution.State) == Snapshot(manual.State) && Snapshot(window.Execution.TurnEffects) == Snapshot(manual.TurnEffects), "단계/끝까지 상태·조합·예약 일치 " + i);
                Check(JsonUtility.ToJson(levels[i]) == json && File.ReadAllText(AssetDatabase.GetAssetPath(levels[i])) == file && !EditorUtility.IsDirty(levels[i]) && Snapshot(window.CurrentState) == initial, "원본/dirty/시작 스냅샷 보존 " + i);
            }
            root.Q<ObjectField>("initial-level").value = levels[0]; Click("initial-build"); yield return null;
            string original = JsonUtility.ToJson(levels[0]); root.Q<Toggle>("execution-mode").value = true;
            Click("initial-cell-4-4"); Click("initial-cell-4-5"); Click("execution-swap"); Click("execution-reset"); yield return null;
            Check(window.Execution == null && window.CurrentState != null, "조합 후 동일 시드 초기화");
            foreach (string change in new[] { "seed", "mode", "level", "source", "close" })
            {
                root.Q<IntegerField>("initial-seed").value = 12345; root.Q<PopupField<string>>("initial-mode").value = "시작 조건 구성"; root.Q<ObjectField>("initial-level").value = levels[0];
                Click("initial-build"); yield return null; root.Q<Toggle>("execution-mode").value = true;
                Click("initial-cell-4-4"); Click("initial-cell-4-5"); Click("execution-swap"); Click("execution-cascade");
                if (change == "seed") root.Q<IntegerField>("initial-seed").value = 7;
                if (change == "mode") root.Q<PopupField<string>>("initial-mode").value = "원시 후보";
                if (change == "level") root.Q<ObjectField>("initial-level").value = null;
                if (change == "source") { JsonUtility.FromJsonOverwrite("{\"moveCount\":19}", levels[0]); Invoke(typeof(LevelInitialStatePanel), "CheckInput", window); JsonUtility.FromJsonOverwrite(original, levels[0]); }
                if (change == "close") window.Owner.Close();
                Check(window == null || (window.Execution == null && !window.CascadeRunning), "조합 실행/예약 소유자 해제 " + change);
            }
        }
        private static void Capture(string name)
        {
            Rect rect = window.Owner.position; Texture2D texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            texture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, (int)rect.width, (int)rect.height)); texture.Apply();
            File.WriteAllBytes(Evidence + "/" + name, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}


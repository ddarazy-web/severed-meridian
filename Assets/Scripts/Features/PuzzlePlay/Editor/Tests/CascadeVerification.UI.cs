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
    public static partial class CascadeVerification
    {
        private static LevelInitialStatePanel window;
        private static LevelDefinition chain, random, lastMove;
        private static string folder;
        private static IEnumerator sequence;
        private static double nextTick;
        [Serializable] private sealed class Saved
        { public string folder; public int process; public string[] paths, json, guids, snapshots; }

        public static void Start()
        {
            Directory.CreateDirectory(Evidence);
            if (File.Exists(Evidence + "/state.json")) throw new InvalidOperationException("이전 검증 Restart 필요");
            Results.Clear(); folder = "Assets/__CascadeVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                DataChecks();
                chain = ChainBoard(); AssetDatabase.CreateAsset(chain, folder + "/Chain.asset");
                random = ChainBoard(); LevelSupplyEditing.SetItems(random, 0, new[] { new SupplyItem(SupplyKind.RandomNormal, 9) }); AssetDatabase.CreateAsset(random, folder + "/Random.asset");
                lastMove = ChainBoard(); JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", lastMove); AssetDatabase.CreateAsset(lastMove, folder + "/LastMove.asset");
                Check(EditorApplication.ExecuteMenuItem("Match/초기 보드 확인"), "Match 메뉴 진입");
                window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().Single().ActiveSimulationPanel; window.Owner.position = new Rect(10, 10, 1000, 780); window.Owner.Focus();
                sequence = UI(); EditorApplication.update += Tick;
            }
            catch (Exception error) { FinishUI(error); }
        }

        private static string Replay(LevelDefinition level)
        {
            StartingBoardSearch start = StartingBoardBuilder.Build(level, 12345);
            if (start.State == null) throw new InvalidOperationException(start.Message);
            BoardActionExecutor executor = new BoardActionExecutor(start.State);
            BoardActionResult action = executor.Activate(C(2, 0));
            if (!action.IsApplied) throw new InvalidOperationException(action.Message);
            Finish(executor); string first = Snapshot(action) + "\n" + Context(executor) + "\n" + Snapshot(executor.CascadeHistory);
            if (executor.Phase == BoardActionPhase.Ready)
            {
                BoardActionResult next = executor.Activate(C(1, 3));
                if (!next.IsApplied) throw new InvalidOperationException(next.Message);
                Finish(executor); return first + "\n다음 수\n" + Snapshot(next) + "\n" + Context(executor) + "\n" + Snapshot(executor.CascadeHistory);
            }
            return first;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.4;
            try
            {
                if (sequence.MoveNext()) return;
                string[] paths = AssetDatabase.FindAssets("t:LevelDefinition", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
                foreach (string path in paths) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path));
                File.WriteAllText(Evidence + "/state.json", JsonUtility.ToJson(new Saved
                {
                    folder = folder, process = System.Diagnostics.Process.GetCurrentProcess().Id, paths = paths,
                    json = paths.Select(p => JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<LevelDefinition>(p))).ToArray(),
                    guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray(), snapshots = paths.Select(p => Replay(AssetDatabase.LoadAssetAtPath<LevelDefinition>(p))).ToArray()
                }, true)); FinishUI(null);
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
                Check(level != null && JsonUtility.ToJson(level) == saved.json[i] && AssetDatabase.AssetPathToGUID(saved.paths[i]) == saved.guids[i], "원본 JSON/GUID 재로드 " + saved.paths[i]);
                Check(Replay(level) == saved.snapshots[i], "여러 턴/도착/보호/피해/공급/난수/결과 독립 재현 " + saved.paths[i]);
            }
            if (!saved.folder.StartsWith("Assets/__CascadeVerification_", StringComparison.Ordinal) || saved.folder.Contains("..")) throw new InvalidOperationException("정리 경로 오류");
            Check(AssetDatabase.DeleteAsset(saved.folder), "소유 검증 에셋 정리");
            File.WriteAllLines(Evidence + "/restart-results.txt", Results);
            File.Move(Evidence + "/state.json", Evidence + "/completed-" + DateTime.UtcNow.Ticks + ".json");
        }

        private static IEnumerator UI()
        {
            yield return null;
            // 기존 화면 검증의 현재 프로세스 창 표시만 재사용한다.
            typeof(SettlementVerification).GetField("window", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, window);
            Invoke(typeof(SettlementVerification), "RaiseVerificationWindow", null); yield return null;
            VisualElement root = window.rootVisualElement;
            root.Q<ObjectField>("initial-level").value = chain; root.Q<IntegerField>("initial-seed").value = 12345;
            root.Q<PopupField<string>>("initial-mode").value = "시작 조건 구성"; Click("initial-build"); yield return null;
            string json = JsonUtility.ToJson(chain), file = File.ReadAllText(AssetDatabase.GetAssetPath(chain)), initial = Snapshot(window.CurrentState);
            root.Q<Toggle>("execution-mode").value = true;
            Check(!root.Q<Button>("execution-automatic").enabledInHierarchy && !root.Q<Button>("execution-cascade").enabledInHierarchy, "첫 행동 전 자동 단계/끝까지 비활성");
            Click("initial-cell-2-0"); Click("execution-activate"); Click("execution-settle"); yield return null;
            Check(window.Execution.Phase == BoardActionPhase.WaitingForAutomaticMatch && root.Q<Button>("execution-automatic").enabledInHierarchy, "정착 후 자동 매칭 버튼 활성");
            Click("execution-automatic"); yield return null;
            Check(window.Execution.CascadeRounds == 1 && window.Execution.Phase == BoardActionPhase.WaitingForFall && window.Execution.State.Obstacles[0].Durability == 2, "UI 자동 매칭 1단계/상자 피해"); Capture("step-wide.png");
            string stopped = Context(window.Execution);
            Click("execution-cascade"); Check(window.CascadeRunning, "연쇄 끝까지 예약"); Click("execution-cascade");
            Check(!window.CascadeRunning && Context(window.Execution) == stopped, "연쇄 실행 중지/현 단계 보존");
            Click("execution-cascade"); for (int i = 0; window.CascadeRunning && i < 30; i++) yield return null;
            Check(!window.CascadeRunning && window.Execution.Phase == BoardActionPhase.Ready && window.Execution.CascadeRounds == 3 && window.Execution.State.MovesRemaining == 19, "UI 연쇄 끝까지/다음 행동 대기");
            Check(root.Q<Label>("initial-overview").text.Contains("연쇄 3") && root.Q<Label>("initial-boundary").text.Contains("다음 행동"), "회차/난수/결과 요약 표시"); Capture("stable-wide.png");
            Check(JsonUtility.ToJson(chain) == json && File.ReadAllText(AssetDatabase.GetAssetPath(chain)) == file && !EditorUtility.IsDirty(chain) && Snapshot(window.CurrentState) == initial, "연쇄 원본/파일/dirty/시작 상태 보존");
            window.Owner.position = new Rect(10, 10, 680, 480); yield return null;
            Check(root.Q<Button>("execution-cascade").worldBound.xMax <= root.worldBound.xMax && root.Q<Button>("execution-reset").worldBound.yMax < root.Q<ScrollView>("initial-board-scroll").worldBound.yMin && root.Q<ScrollView>("initial-inspector").worldBound.height > 100, "680 폭 실행 버튼/보드/결과 접근"); Capture("stable-narrow.png");
            Click("initial-cell-1-1"); Check(root.Q<Label>("initial-details").text.Contains("내구도 2"), "칸 상세가 초기 상태 아닌 현재 내구도 표시");
            Click("execution-cancel"); Click("initial-cell-1-3");
            Click("execution-activate"); Click("execution-cascade"); for (int i = 0; window.CascadeRunning && i < 30; i++) yield return null;
            Check(window.Execution.Turn == 2 && window.Execution.State.MovesRemaining == 18 && window.Execution.State.Obstacles[0].Durability == 1, "UI 두 번째 수/피해 기록 초기화"); Capture("second-turn.png");
            Click("execution-reset"); yield return null;
            Check(window.Execution == null && Snapshot(window.CurrentState) == initial, "연쇄 후 같은 시드 초기화");
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-2-0"); Click("execution-activate"); Click("execution-cascade");
            JsonUtility.FromJsonOverwrite("{\"moveCount\":21}", chain); yield return null;
            Check(window.Execution == null && !window.CascadeRunning && window.CurrentState == null && root.Q<Button>("execution-cascade").text == "연쇄 끝까지", "실행 중 원본 변경 예약/상태 폐기/버튼 복원"); JsonUtility.FromJsonOverwrite(json, chain);
            root.Q<ObjectField>("initial-level").value = lastMove; Click("initial-build"); yield return null;
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-2-0"); Click("execution-activate"); Click("execution-cascade");
            for (int i = 0; window.CascadeRunning && i < 30; i++) yield return null;
            Check(window.Execution.CascadeRounds == 3 && window.Execution.LastCascadeStep.Reason == CascadeStepReason.MovesExhausted && root.Q<Label>("initial-boundary").text.Contains("승패 판정 미지원"), "마지막 수 전체 연쇄/승패 미판정 UI");
            root.Q<ObjectField>("initial-level").value = chain; Click("initial-build"); yield return null;
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-2-0"); Click("execution-activate"); Click("execution-cascade"); root.Q<IntegerField>("initial-seed").value = 7;
            Check(!window.CascadeRunning && window.Execution == null, "시드 변경 예약 해제");
            Click("initial-build"); yield return null; root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-2-0"); Click("execution-activate"); Click("execution-cascade");
            root.Q<PopupField<string>>("initial-mode").value = "원시 후보";
            Check(!window.CascadeRunning && window.Execution == null, "모드 변경 예약 해제");
            root.Q<PopupField<string>>("initial-mode").value = "시작 조건 구성"; Click("initial-build"); yield return null;
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-2-0"); Click("execution-activate"); Click("execution-cascade"); window.Owner.Close(); yield return null;
            Check(window == null || (!window.CascadeRunning && window.Execution == null), "창 종료 예약 실행/상태 해제"); window = null;
        }

        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("버튼 없음/비활성 " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }
        private static void Capture(string name)
        {
            Rect rect = window.Owner.position; Texture2D texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            texture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, (int)rect.width, (int)rect.height)); texture.Apply();
            File.WriteAllBytes(Evidence + "/" + name, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}

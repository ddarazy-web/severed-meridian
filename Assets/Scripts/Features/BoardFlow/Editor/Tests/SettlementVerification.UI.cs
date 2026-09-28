using System;
using System.Collections;
using System.IO;
using System.Linq;
using Simulation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class SettlementVerification
    {
        private static LevelInitialStatePanel window;
        private static IEnumerator sequence;
        private static double nextTick;
        private static string folder;
        private static LevelDefinition falling, rejected, matchesLevel;
        [Serializable] private sealed class Saved
        { public string folder; public int process; public string[] paths, json, guids, snapshots; }

        public static void Start()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            if (File.Exists(Evidence + "/state.json")) throw new InvalidOperationException("이전 검증 Restart 필요");
            folder = "Assets/__SettlementVerification_" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                DataChecks();
                falling = FallingBoard(); AssetDatabase.CreateAsset(falling, folder + "/Falling.asset");
                rejected = FallingBoard(); LevelSupplyEditing.SetItems(rejected, 0, new[] { new SupplyItem(SupplyKind.Rocket), new SupplyItem(SupplyKind.Recovery) });
                AssetDatabase.CreateAsset(rejected, folder + "/Rejected.asset");
                matchesLevel = FallingBoard(); LevelSupplyEditing.SetItems(matchesLevel, 0, new[] { new SupplyItem(SupplyKind.FixedNormal, 3) });
                AssetDatabase.CreateAsset(matchesLevel, folder + "/Matches.asset");
                LevelDefinition random = FallingBoard(); LevelSupplyEditing.SetItems(random, 0, new[] { new SupplyItem(SupplyKind.RandomNormal, 20) });
                AssetDatabase.CreateAsset(random, folder + "/Random.asset");
                Check(EditorApplication.ExecuteMenuItem("Match/초기 보드 확인"), "Match 메뉴 진입");
                window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().Single().ActiveSimulationPanel; window.Owner.position = new Rect(10, 10, 1000, 780); window.Owner.Focus();
                sequence = UI(); EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error); }
        }
        private static string Replay(LevelDefinition level)
        {
            StartingBoardSearch start = StartingBoardBuilder.Build(level, 12345);
            if (start.State == null) throw new InvalidOperationException(start.Message);
            BoardActionExecutor executor = new BoardActionExecutor(start.State);
            BoardActionResult action = executor.Swap(C(3, 3), C(2, 3)); SettlementResult result = executor.Settle();
            string protectedCells = string.Join(";", executor.State.Cells.Where(c => executor.TurnEffects.IsProtected(c.Coordinate)).Select(c => c.Coordinate));
            string damaged = string.Join(";", Enumerable.Range(0, executor.State.Obstacles.Count).Where(i => executor.TurnEffects.HasDamaged(i)));
            return Snapshot(action) + "\n" + Snapshot(result) + "\n" + Snapshot(executor.State) + "\n" + executor.Phase + "\n보호 " + protectedCells + "\n피해 " + damaged;
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
                }, true)); Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }
        private static void Finish(Exception error)
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
                Check(Replay(level) == saved.snapshots[i], "정착 전체 상태/공급/난수/보호/순서 독립 재현 " + saved.paths[i]);
            }
            if (!saved.folder.StartsWith("Assets/__SettlementVerification_", StringComparison.Ordinal) || saved.folder.Contains("..")) throw new InvalidOperationException("정리 경로 오류");
            Check(AssetDatabase.DeleteAsset(saved.folder), "소유 검증 에셋 정리");
            File.WriteAllLines(Evidence + "/restart-results.txt", Results);
            File.Move(Evidence + "/state.json", Evidence + "/completed-" + DateTime.UtcNow.Ticks + ".json");
        }
        private static IEnumerator UI()
        {
            yield return null; VisualElement root = window.rootVisualElement;
            RaiseVerificationWindow(); yield return null;
            root.Q<ObjectField>("initial-level").value = falling; root.Q<IntegerField>("initial-seed").value = 12345;
            root.Q<PopupField<string>>("initial-mode").value = "시작 조건 구성"; yield return null;
            Click("initial-build"); yield return null;
            string originalJson = JsonUtility.ToJson(falling), originalFile = File.ReadAllText(AssetDatabase.GetAssetPath(falling)), startState = Snapshot(window.CurrentState);
            bool dirty = EditorUtility.IsDirty(falling);
            root.Q<Toggle>("execution-mode").value = true; yield return null;
            Check(!root.Q<Button>("execution-settle").enabledInHierarchy, "효과 전 정착 버튼 비활성");
            Click("initial-cell-3-3"); Click("initial-cell-2-3"); Click("execution-swap"); yield return null;
            Check(root.Q<Button>("execution-settle").enabledInHierarchy && window.Execution.State.MovesRemaining == 0, "낙하 대기/마지막 수 정착 버튼 활성"); Capture("before-wide.png");
            string applied = Snapshot(window.Execution.LastApplied); TurnEffectContext previousContext = window.Execution.TurnEffects;
            Click("execution-settle"); yield return null;
            Check(window.Execution.Phase == BoardActionPhase.WaitingForAutomaticMatch && window.Execution.TurnEffects.IsProtected(C(9, 3)) && !window.Execution.TurnEffects.IsProtected(C(8, 3)), "UI 정착/보호 이동/공급 보호 구분");
            Check(previousContext.IsProtected(C(3, 3)) && !previousContext.IsProtected(C(9, 3)) && previousContext.HasFired(C(2, 3)) && !window.Execution.TurnEffects.HasFired(C(2, 3)), "턴 기록 독립 복사/소모 파워 좌표 정리");
            Check(!root.Q<Button>("execution-settle").enabledInHierarchy && !root.Q<Button>("execution-swap").enabledInHierarchy && !root.Q<Button>("execution-activate").enabledInHierarchy, "정착 후 모든 실행 입력 차단");
            Check(root.Q<Label>("initial-boundary").text.Contains("자동 매칭 대기") && root.Q<Label>("initial-overview").text.Contains("목록 0:0 → 1:0") && root.Q<Label>("initial-details").text.Contains("남은 이동 0"), "정착 결과/커서/수 표시");
            Check(Snapshot(window.Execution.LastApplied) == applied && Snapshot(window.CurrentState) == startState && JsonUtility.ToJson(falling) == originalJson && File.ReadAllText(AssetDatabase.GetAssetPath(falling)) == originalFile && EditorUtility.IsDirty(falling) == dirty, "UI 원본/파일/dirty/시작 상태/직전 행동 보존");
            string settled = Snapshot(window.Execution.State), result = Snapshot(window.Execution.LastSettlement); Capture("after-wide.png");
            window.Owner.position = new Rect(10, 10, 680, 480); yield return null; yield return null;
            Check(root.Q<Button>("execution-settle").worldBound.xMax <= 680 && root.Q<Button>("execution-reset").worldBound.xMax <= 680 && root.Q<ScrollView>("initial-inspector").resolvedStyle.width >= 200, "좁은 창 정착/초기화/결과 접근");
            root.Q<ScrollView>("initial-board-scroll").ScrollTo(root.Q<Button>("initial-cell-9-3")); yield return null; Capture("after-narrow.png");
            Click("execution-reset"); yield return null; Check(window.Execution == null && Snapshot(window.CurrentState) == startState, "정착 초기화 시작 상태 복원");
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-3-3"); Click("initial-cell-2-3"); Click("execution-swap"); Click("execution-settle"); yield return null;
            Check(Snapshot(window.Execution.State) == settled && Snapshot(window.Execution.LastSettlement) == result, "같은 시드 교환+효과+정착 재현");
            root.Q<ObjectField>("initial-level").value = matchesLevel; Click("initial-build"); yield return null;
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-3-3"); Click("initial-cell-2-3"); Click("execution-swap"); Click("execution-settle"); yield return null;
            string matchedState = Snapshot(window.Execution.State); Click("settled-match-0"); yield return null;
            Check(root.Query<Button>(className: "query-highlight").ToList().Count == 3 && Snapshot(window.Execution.State) == matchedState && window.Execution.State.Cells.Count(c => c.Content == RuntimeContent.Normal) == 3, "정착 후 매칭 조회/강조는 제거·난수 변경 없음"); Capture("matches.png");
            root.Q<ObjectField>("initial-level").value = falling; Click("initial-build"); yield return null;
            Click("execution-reset"); yield return null; root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-3-3"); Click("initial-cell-2-3"); Click("execution-swap");
            JsonUtility.FromJsonOverwrite("{\"moveCount\":33}", falling); Click("execution-settle"); yield return null;
            Check(window.Execution == null && window.CurrentState == null, "원본 변경 직후 정착 버튼 오래된 입력 차단"); JsonUtility.FromJsonOverwrite(originalJson, falling);
            root.Q<ObjectField>("initial-level").value = rejected; Click("initial-build"); yield return null;
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-3-3"); Click("initial-cell-2-3"); Click("execution-swap"); yield return null;
            string before = Snapshot(window.Execution.State), beforeAction = Snapshot(window.Execution.LastApplied); TurnEffectContext context = window.Execution.TurnEffects;
            Click("execution-settle"); yield return null;
            Check(window.Execution.Phase == BoardActionPhase.WaitingForFall && Snapshot(window.Execution.State) == before && Snapshot(window.Execution.LastApplied) == beforeAction && window.Execution.TurnEffects == context && window.Execution.LastSettlement == null && root.Q<Label>("execution-info").text.Contains("미지원"), "UI 미지원 공급 정착 거절/효과 직후 상태 보존"); Capture("rejected.png");
            root.Q<IntegerField>("initial-seed").value = 55; yield return null; Check(window.Execution == null, "시드 변경 정착 상태 폐기");
            Click("initial-build"); yield return null; root.Q<Toggle>("execution-mode").value = true;
            root.Q<PopupField<string>>("initial-mode").value = "원시 후보"; yield return null; Check(window.Execution == null, "모드 변경 정착 상태 폐기");
            window.Owner.Close(); yield return null; Check(window == null || window.Execution == null, "창 종료 정착 상태 해제"); window = null;
        }
        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("버튼 없음 " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }
        private static void Capture(string name)
        {
            Rect rect = window.Owner.position; Texture2D texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            texture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, (int)rect.width, (int)rect.height)); texture.Apply();
            File.WriteAllBytes(Evidence + "/" + name, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }

#if UNITY_EDITOR_WIN
        private delegate bool WindowVisitor(IntPtr windowHandle, IntPtr value);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool EnumWindows(WindowVisitor visitor, IntPtr value);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
#endif
        private static void RaiseVerificationWindow()
        {
            // 화면 픽셀 캡처에 다른 앱이 섞이지 않도록 이 검증 프로세스의 창만 잠시 위에 표시한다.
#if UNITY_EDITOR_WIN
            uint processId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id; int raised = 0;
            EnumWindows((handle, value) =>
            {
                GetWindowThreadProcessId(handle, out uint owner);
                if (owner == processId && IsWindowVisible(handle) && SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x53)) raised++;
                return true;
            }, IntPtr.Zero);
            Check(raised > 0, "Unity 검증 창 표시");
#endif
            window.Owner.Focus(); window.Owner.Repaint();
        }
    }
}

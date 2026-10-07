using System;
using System.Collections;
using System.Collections.Generic;
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
    public static partial class BoardActionVerification
    {
        private static LevelInitialStatePanel window;
        private static IEnumerator sequence;
        private static double nextTick;
        private static string folder;
        private static LevelDefinition rocket, mixed;
        [Serializable] private sealed class Saved
        { public string folder; public int process; public string[] paths, json, guids, snapshots; }

        public static void Start()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            if (File.Exists(Evidence + "/state.json")) throw new InvalidOperationException("이전 Restart 필요");
            folder = "Assets/__BoardActionVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                DataChecks();
                rocket = RocketBoard(); AssetDatabase.CreateAsset(rocket, folder + "/Rocket.asset");
                LevelDefinition drone = Pattern(new[] { C(3, 3), C(3, 4), C(4, 3), C(4, 4) }, C(4, 4), C(4, 5)); AssetDatabase.CreateAsset(drone, folder + "/Drone.asset");
                LevelDefinition random = ScriptableObject.CreateInstance<LevelDefinition>();
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":12}]}", random); AssetDatabase.CreateAsset(random, folder + "/Random.asset");
                Dictionary<Board.BoardCoordinate, int> cross = Enumerable.Range(1, 5).ToDictionary(i => C(3, i), i => 0);
                foreach (int i in Enumerable.Range(1, 5)) cross[C(i, 3)] = 0;
                AssetDatabase.CreateAsset(Make(cross), folder + "/Tie.asset");
                typeof(LevelInitialStateVerification).GetField("folder", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, folder);
                mixed = (LevelDefinition)typeof(LevelInitialStateVerification).GetMethod("Mixed", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                LevelRuntimeState source = Build(mixed); BoardActionExecutor copy = new BoardActionExecutor(source);
                Check(Snapshot(source) == Snapshot(copy.State), "복합 본체/공급 커서/미션 전체 복사 값 동일");
                string snapshot = Snapshot(source);
                Set(copy.State.Obstacles[0], "Durability", 1); Set(copy.State.Obstacles[5], "Charge", 1);
                Set(copy.State.Supply.Sources[0], "ItemIndex", 1); Set(copy.State.Supply.Sources[0], "ItemConsumed", 1);
                Set(copy.State.Supply, "ScrapGenerated", 1); Set(copy.State.Missions[0], "Progress", 1);
                Check(Snapshot(source) == snapshot, "복사본 본체/충전/공급/미션 변경은 원본 독립");
                Check(EditorApplication.ExecuteMenuItem("Match/초기 보드 확인"), "Match 메뉴 진입");
                window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().Single().ActiveSimulationPanel; window.Owner.position = new Rect(10, 10, 1000, 780); window.Owner.Focus();
                sequence = UI(); EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error); }
        }
        private static string Replay(LevelDefinition level)
        {
            if (Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(level)) == "Tie")
            {
                LevelRuntimeState state = Build(level); string decisions = Snapshot(Resolve(state, C(3, 3), C(3, 3)));
                return decisions + "\n" + Snapshot(state);
            }
            StartingBoardSearch start = StartingBoardBuilder.Build(level, 12345);
            if (start.State == null) throw new InvalidOperationException(start.Message);
            ActionCandidate action = ActionQuery.Find(start.State).First(a => a.Kind == QueryActionKind.SwapMatch);
            BoardActionExecutor executor = new BoardActionExecutor(start.State);
            BoardActionResult result = executor.Swap(action.First, action.Second.Value);
            return Snapshot(result) + "\n" + Snapshot(executor.State) + "\n" + executor.Phase;
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.4;
            try
            {
                if (sequence.MoveNext()) return;
                string[] paths = AssetDatabase.FindAssets("t:LevelDefinition", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
                foreach (string path in paths) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path));
                File.WriteAllText(Evidence + "/state.json", JsonUtility.ToJson(new Saved
                {
                    folder = folder, process = System.Diagnostics.Process.GetCurrentProcess().Id, paths = paths,
                    json = paths.Select(p => JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<LevelDefinition>(p))).ToArray(),
                    guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray(), snapshots = paths.Select(p => Replay(AssetDatabase.LoadAssetAtPath<LevelDefinition>(p))).ToArray()
                }, true));
                Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            if (error != null) { Results.Add("FAIL " + error); Debug.LogException(error); AssetDatabase.DeleteAsset(folder); }
            File.WriteAllLines(Evidence + "/ui-results.txt", Results);
            if (window != null) window.Owner.Close();
            EditorApplication.Exit(error == null ? 0 : 1);
        }
        public static void Restart()
        {
            Results.Clear(); PriorityChecks(); Saved saved = JsonUtility.FromJson<Saved>(File.ReadAllText(Evidence + "/state.json"));
            Check(saved.process != System.Diagnostics.Process.GetCurrentProcess().Id, "독립 Unity 프로세스 확인");
            for (int i = 0; i < saved.paths.Length; i++)
            {
                LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(saved.paths[i]);
                Check(level != null && JsonUtility.ToJson(level) == saved.json[i] && AssetDatabase.AssetPathToGUID(saved.paths[i]) == saved.guids[i], "원본 JSON/GUID 재로드 " + saved.paths[i]);
                Check(Replay(level) == saved.snapshots[i], "실행/선택/난수/전체 상태 독립 재현 " + saved.paths[i]);
            }
            if (!saved.folder.StartsWith("Assets/__BoardActionVerification_", StringComparison.Ordinal) || saved.folder.Contains("..")) throw new InvalidOperationException("정리 경로 오류");
            Check(AssetDatabase.DeleteAsset(saved.folder), "소유 검증 에셋 정리");
            File.WriteAllLines(Evidence + "/restart-results.txt", Results);
            File.Move(Evidence + "/state.json", Evidence + "/completed-" + DateTime.UtcNow.Ticks + ".json");
        }

        private static IEnumerator UI()
        {
            yield return null;
            VisualElement root = window.rootVisualElement;
            root.Q<ObjectField>("initial-level").value = rocket; root.Q<IntegerField>("initial-seed").value = 12345;
            root.Q<PopupField<string>>("initial-mode").value = "원시 후보"; yield return null;
            Click("initial-build"); yield return null;
            Check(!root.Q<Toggle>("execution-mode").enabledInHierarchy && window.Execution == null, "원시 후보에서 실행 시험 차단");
            root.Q<PopupField<string>>("initial-mode").value = "시작 조건 구성"; yield return null;
            Click("initial-build"); yield return null;
            Check(root.Q<Toggle>("execution-mode").enabledInHierarchy, "시작 성공 후 실행 시험 활성화");
            string originalJson = JsonUtility.ToJson(rocket), originalFile = File.ReadAllText(AssetDatabase.GetAssetPath(rocket)), source = Snapshot(window.CurrentState);
            bool dirty = EditorUtility.IsDirty(rocket);
            root.Q<Toggle>("execution-mode").value = true; yield return null;
            Click("initial-cell-2-3"); yield return null;
            Check(!root.Q<Button>("execution-swap").enabledInHierarchy, "한 칸 선택만으로 실행 불가");
            Click("initial-cell-2-3"); yield return null;
            Check(!root.Q<Button>("execution-cancel").enabledInHierarchy, "같은 칸 재선택 취소");
            Click("initial-cell-3-2"); Click("initial-cell-3-5"); yield return null;
            string before = Snapshot(window.Execution.State); Click("execution-swap"); yield return null;
            Check(Snapshot(window.Execution.State) == before && root.Q<Label>("execution-info").text.Contains("인접"), "UI 무효 입력 거절/무변경");
            Click("execution-cancel"); yield return null;
            Check(root.Query<Button>(className: "execution-input").ToList().Count == 0, "명시적 선택 취소 강조 제거");
            Click("initial-cell-2-3"); Click("initial-cell-3-3"); yield return null;
            Check(root.Q<Label>("execution-info").text.Contains("출발") && root.Q<Label>("execution-info").text.Contains("도착"), "입력 방향 표시");
            Capture("before-wide.png"); Click("execution-swap"); yield return null;
            Check(window.Execution.Phase == BoardActionPhase.WaitingForFall && window.Execution.State.CellAt(C(3, 3)).Content == RuntimeContent.Rocket && window.Execution.State.MovesRemaining == 19, "UI 실제 교환/생성/한 수 차감");
            Check(root.Query<Button>(className: "execution-created").ToList().Count == 1 && root.Query<Button>(className: "execution-removed").ToList().Count == 3, "생성/제거 칸 구분 강조");
            Check(!root.Q<Button>("execution-swap").enabledInHierarchy && root.Q<Label>("initial-boundary").text.Contains("낙하 대기"), "낙하 대기 입력 차단/안내");
            Check(Snapshot(window.CurrentState) == source && JsonUtility.ToJson(rocket) == originalJson && File.ReadAllText(AssetDatabase.GetAssetPath(rocket)) == originalFile && EditorUtility.IsDirty(rocket) == dirty, "실행 UI 원본/파일/dirty/조회 스냅샷 보존");
            string result = Snapshot(window.Execution.LastApplied), state = Snapshot(window.Execution.State);
            window.Owner.Repaint(); yield return null;
            Check(Snapshot(window.Execution.State) == state && Snapshot(window.Execution.LastApplied) == result, "재표시 실행/난수 중복 없음");
            Capture("after-wide.png");
            window.Owner.position = new Rect(10, 10, 680, 480); yield return null; yield return null;
            Check(root.Q<Button>("execution-reset").worldBound.xMax <= 680 && root.Q<ScrollView>("initial-inspector").resolvedStyle.width >= 200, "좁은 창 실행/초기화/상세 접근");
            ScrollView board = root.Q<ScrollView>("initial-board-scroll"); Button last = root.Q<Button>("initial-cell-8-8"); board.ScrollTo(last); yield return null;
            Check(board.contentViewport.worldBound.Overlaps(last.worldBound), "실행 후 좁은 창 끝칸 접근"); Capture("after-narrow.png");
            Click("execution-reset"); yield return null;
            Check(window.Execution == null && Snapshot(window.CurrentState) == source && root.Query<Button>(className: "execution-created").ToList().Count == 0, "같은 시드 초기화 상태/선택/결과 복원");
            root.Q<Toggle>("execution-mode").value = true; yield return null;
            Click("initial-cell-2-3"); Click("initial-cell-3-3"); Click("execution-swap"); yield return null;
            Check(Snapshot(window.Execution.LastApplied) == result && Snapshot(window.Execution.State) == state, "같은 시드 UI 재실행 동일");
            root.Q<Toggle>("execution-mode").value = false; yield return null;
            Check(window.Execution == null && Snapshot(window.CurrentState) == source, "실행 시험 종료 조회 복귀");
            root.Q<Toggle>("execution-mode").value = true; yield return null;
            root.Q<IntegerField>("initial-seed").value = 55; yield return null;
            Check(window.Execution == null && window.CurrentState == null, "시드 변경 실행 폐기");
            Click("initial-build"); yield return null;
            root.Q<Toggle>("execution-mode").value = true; yield return null;
            Click("initial-cell-2-3"); Click("initial-cell-3-3");
            JsonUtility.FromJsonOverwrite("{\"moveCount\":33}", rocket); Click("execution-swap"); yield return null;
            Check(window.Execution == null && window.CurrentState == null, "원본 변경 직후 실행 버튼도 오래된 입력 차단");
            JsonUtility.FromJsonOverwrite(originalJson, rocket);
            root.Q<ObjectField>("initial-level").value = mixed; yield return null;
            Click("initial-build"); yield return null;
            int ticks = 0; while (window.IsSearching && ticks++ < 60) yield return null;
            root.Q<Toggle>("execution-mode").value = true; yield return null;
            Click("initial-cell-1-0"); Click("initial-cell-1-1"); Click("execution-swap"); yield return null;
            Check(root.Q<Label>("execution-info").text.Contains("이번 실행 시험") && window.Execution.Phase == BoardActionPhase.Ready, "복합 장애물 실행 미지원 안내"); Capture("unsupported.png");
            root.Q<PopupField<string>>("initial-mode").value = "원시 후보"; yield return null;
            Check(window.Execution == null, "구성 모드 변경 실행 폐기");
            root.Q<ObjectField>("initial-level").value = rocket; root.Q<PopupField<string>>("initial-mode").value = "시작 조건 구성"; yield return null;
            Click("initial-build"); yield return null; root.Q<Toggle>("execution-mode").value = true; yield return null;
            BoardActionExecutor held = window.Execution; string heldState = Snapshot(held.State);
            window.Owner.Close(); yield return null;
            Check((window == null || window.Execution == null) && Snapshot(held.State) == heldState, "창 종료 실행 소유 해제/지연 적용 없음"); window = null;
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
    }
}

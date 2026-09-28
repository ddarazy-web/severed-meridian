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
    public static partial class PowerEffectVerification
    {
        private static LevelInitialStatePanel window;
        private static IEnumerator sequence;
        private static double nextTick;
        private static string folder;
        private static LevelDefinition protection, bomb, rejected;
        [Serializable] private sealed class Saved
        { public string folder; public int process; public string[] paths, json, guids, snapshots; }

        public static void Start()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            if (File.Exists(Evidence + "/state.json")) throw new InvalidOperationException("이전 검증 Restart 필요");
            folder = "Assets/__PowerEffectVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                DataChecks();
                protection = ProtectionBoard(); AssetDatabase.CreateAsset(protection, folder + "/Protection.asset");
                bomb = Make(); Place(bomb, C(3, 3), InitialBlockKind.Bomb); AssetDatabase.CreateAsset(bomb, folder + "/Bomb.asset");
                rejected = ProtectionBoard(); Place(rejected, C(6, 9), InitialBlockKind.Drone); AssetDatabase.CreateAsset(rejected, folder + "/Rejected.asset");
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
            BoardActionResult result = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(level)) == "Bomb" ? executor.Activate(C(3, 3)) : executor.Swap(C(3, 3), C(2, 3));
            return Snapshot(result) + "\n" + Snapshot(executor.State) + "\n" + executor.Phase;
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
            Results.Clear(); Saved saved = JsonUtility.FromJson<Saved>(File.ReadAllText(Evidence + "/state.json"));
            Check(saved.process != System.Diagnostics.Process.GetCurrentProcess().Id, "독립 Unity 프로세스 확인");
            for (int i = 0; i < saved.paths.Length; i++)
            {
                LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(saved.paths[i]);
                Check(level != null && JsonUtility.ToJson(level) == saved.json[i] && AssetDatabase.AssetPathToGUID(saved.paths[i]) == saved.guids[i], "원본 JSON/GUID 재로드 " + saved.paths[i]);
                Check(Replay(level) == saved.snapshots[i], "파워/피해/거절/난수/전체 상태 독립 재현 " + saved.paths[i]);
            }
            if (!saved.folder.StartsWith("Assets/__PowerEffectVerification_", StringComparison.Ordinal) || saved.folder.Contains("..")) throw new InvalidOperationException("정리 경로 오류");
            Check(AssetDatabase.DeleteAsset(saved.folder), "소유 검증 에셋 정리");
            File.WriteAllLines(Evidence + "/restart-results.txt", Results);
            File.Move(Evidence + "/state.json", Evidence + "/completed-" + DateTime.UtcNow.Ticks + ".json");
        }
        private static IEnumerator UI()
        {
            yield return null;
            VisualElement root = window.rootVisualElement;
            root.Q<ObjectField>("initial-level").value = protection; root.Q<IntegerField>("initial-seed").value = 12345;
            root.Q<PopupField<string>>("initial-mode").value = "시작 조건 구성"; yield return null;
            Click("initial-build"); yield return null;
            Check(root.Q<Toggle>("execution-mode").enabledInHierarchy, "시작 조건 구성 성공");
            string json = JsonUtility.ToJson(protection), file = File.ReadAllText(AssetDatabase.GetAssetPath(protection)), source = Snapshot(window.CurrentState);
            bool dirty = EditorUtility.IsDirty(protection);
            root.Q<Toggle>("execution-mode").value = true; yield return null;
            Click("initial-cell-3-3"); yield return null;
            Check(root.Q<Button>("execution-activate").enabledInHierarchy && !root.Q<Button>("execution-swap").enabledInHierarchy, "한 칸 선택 제자리 발동");
            Click("initial-cell-2-3"); yield return null;
            Check(!root.Q<Button>("execution-activate").enabledInHierarchy && root.Q<Button>("execution-swap").enabledInHierarchy, "두 칸 선택 교환 발동");
            Capture("before-wide.png"); Click("execution-swap"); yield return null;
            Check(window.Execution.LastApplied.IsApplied && window.Execution.State.CellAt(C(3, 3)).Content == RuntimeContent.Rocket && window.Execution.State.Obstacles[0].Durability == 2, "UI 교환/신규 파워 보호/상자 피해");
            Check(root.Q<Label>("initial-overview").text.Contains("3 → 2") && root.Q<Label>("initial-overview").text.Contains("보호") && root.Query<Button>(className: "execution-created").ToList().Count == 1, "피해 전후/보호/생성 강조 표시");
            Check(!root.Q<Button>("execution-activate").enabledInHierarchy && !root.Q<Button>("execution-swap").enabledInHierarchy && root.Q<Label>("initial-boundary").text.Contains("낙하 대기"), "효과 완료 낙하 대기/입력 차단");
            Check(JsonUtility.ToJson(protection) == json && File.ReadAllText(AssetDatabase.GetAssetPath(protection)) == file && EditorUtility.IsDirty(protection) == dirty && Snapshot(window.CurrentState) == source, "실행 UI 원본/dirty/파일/시작 상태 보존");
            string result = Snapshot(window.Execution.LastApplied); Capture("after-wide.png");
            window.Owner.position = new Rect(10, 10, 680, 480); yield return null; yield return null;
            Check(root.Q<Button>("execution-reset").worldBound.xMax <= 680 && root.Q<ScrollView>("initial-inspector").resolvedStyle.width >= 200, "좁은 창 버튼/결과 접근");
            ScrollView inspector = root.Q<ScrollView>("initial-inspector"); inspector.ScrollTo(root.Q<Label>("initial-overview")); yield return null; Capture("after-narrow.png");
            Click("execution-reset"); yield return null;
            Check(window.Execution == null && Snapshot(window.CurrentState) == source, "동일 시드 초기화");
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-3-3"); Click("initial-cell-2-3"); Click("execution-swap"); yield return null;
            Check(Snapshot(window.Execution.LastApplied) == result, "동일 시드 효과 결과 재현");
            root.Q<ObjectField>("initial-level").value = bomb; yield return null;
            Check(window.Execution == null, "레벨 변경 실행 폐기"); Click("initial-build"); yield return null;
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-3-3"); Click("execution-activate"); yield return null;
            Check(window.Execution.LastApplied.IsActivation && window.Execution.LastApplied.Effects.Count(e => e.Response == DamageResponse.Remove) == 8, "UI 달폭탄 제자리 3x3 발동");
            Click("execution-reset"); yield return null; root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-3-3");
            string bombJson = JsonUtility.ToJson(bomb); JsonUtility.FromJsonOverwrite("{\"moveCount\":33}", bomb); Click("execution-activate"); yield return null;
            Check(window.Execution == null && window.CurrentState == null, "원본 변경 직후 제자리 버튼도 오래된 입력 차단"); JsonUtility.FromJsonOverwrite(bombJson, bomb);
            root.Q<ObjectField>("initial-level").value = rejected; Click("initial-build"); yield return null;
            root.Q<Toggle>("execution-mode").value = true; string unchanged = Snapshot(window.Execution.State);
            Click("initial-cell-3-3"); Click("initial-cell-2-3"); Click("execution-swap"); yield return null;
            Check(window.Execution.Phase == BoardActionPhase.Ready && Snapshot(window.Execution.State) == unchanged && root.Q<Label>("execution-info").text.Contains("전체 취소"), "UI 늦은 미지원 연쇄 거절/보존"); Capture("rejected.png");
            root.Q<IntegerField>("initial-seed").value = 55; yield return null; Check(window.Execution == null, "시드 변경 실행 폐기");
            Click("initial-build"); yield return null; root.Q<Toggle>("execution-mode").value = true;
            root.Q<PopupField<string>>("initial-mode").value = "원시 후보"; yield return null; Check(window.Execution == null, "모드 변경 실행 폐기");
            window.Owner.Close(); yield return null; Check(window == null || window.Execution == null, "창 종료 실행 해제"); window = null;
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

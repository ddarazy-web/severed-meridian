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
    public static partial class LevelInitialStateVerification
    {
        private static LevelInitialStateWindow window;
        private static IEnumerator sequence;
        private static double nextTick;
        [Serializable] private sealed class SavedState
        { public string folder; public int process; public int seed; public string[] paths, values, snapshots, guids; }

        public static void Start()
        {
            Directory.CreateDirectory(Evidence);
            if (File.Exists(Evidence + "/state.json")) throw new InvalidOperationException("기존 Restart를 먼저 완료하세요.");
            folder = "Assets/__LevelInitialStateVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                DataChecks();
                Check(EditorApplication.ExecuteMenuItem("Match/초기 보드 확인"), "Match 메뉴로 확인 창 열기");
                window = Resources.FindObjectsOfTypeAll<LevelInitialStateWindow>().Single();
                window.position = new Rect(10, 10, 1000, 780); window.Focus();
                sequence = RunUI(); EditorApplication.update += Tick;
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.4;
            try
            {
                if (sequence.MoveNext()) return;
                string[] paths = AssetDatabase.FindAssets("t:LevelDefinition", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
                foreach (string path in paths) { LevelDefinition definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition); }
                File.WriteAllText(Evidence + "/state.json", JsonUtility.ToJson(new SavedState
                {
                    folder = folder, process = System.Diagnostics.Process.GetCurrentProcess().Id, seed = 12345, paths = paths,
                    values = paths.Select(path => JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path))).ToArray(),
                    snapshots = paths.Select(path => Snapshot(Build(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path)))).ToArray(),
                    guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray()
                }, true));
                Finish(0);
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void Fail(Exception exception)
        { Results.Add("FAIL " + exception); Debug.LogException(exception); if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder); Finish(1); }

        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            File.WriteAllLines(Evidence + "/ui-results.txt", Results);
            if (window != null) window.Close();
            EditorApplication.Exit(code);
        }

        public static void Restart()
        {
            SavedState saved = JsonUtility.FromJson<SavedState>(File.ReadAllText(Evidence + "/state.json"));
            Check(saved.process != System.Diagnostics.Process.GetCurrentProcess().Id, "독립 Unity 프로세스에서 재현");
            for (int i = 0; i < saved.paths.Length; i++)
            {
                LevelDefinition definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(saved.paths[i]);
                Check(definition != null && JsonUtility.ToJson(definition) == saved.values[i] && AssetDatabase.AssetPathToGUID(saved.paths[i]) == saved.guids[i], "원본 값/GUID 재로딩 " + saved.paths[i]);
                Check(Snapshot(Build(definition, saved.seed)) == saved.snapshots[i], "같은 입력/시드 전체 상태 재현 " + saved.paths[i]);
            }
            if (!saved.folder.StartsWith("Assets/__LevelInitialStateVerification_", StringComparison.Ordinal) || saved.folder.Contains("..")) throw new InvalidOperationException("정리 경로 오류");
            Check(AssetDatabase.DeleteAsset(saved.folder), "소유 검증 에셋 정리");
            File.WriteAllLines(Evidence + "/restart-results.txt", Results);
            File.Move(Evidence + "/state.json", Evidence + "/completed-" + DateTime.UtcNow.Ticks + ".json");
        }

        private static IEnumerator RunUI()
        {
            yield return null;
            Check(window.rootVisualElement.panel != null, "실제 UI 패널 생성");
            window.rootVisualElement.Q<ObjectField>("initial-level").value = mixed;
            window.rootVisualElement.Q<IntegerField>("initial-seed").value = 12345;
            Int(mixed, "moveCount", 37); yield return null;
            string json = JsonUtility.ToJson(mixed), file = File.ReadAllText(AssetDatabase.GetAssetPath(mixed));
            bool dirty = EditorUtility.IsDirty(mixed);
            Click("initial-build"); yield return null;
            Check(window.CurrentState != null && window.CurrentState.MovesRemaining == 37, "미저장 정의 UI 구성");
            Check(window.rootVisualElement.Query<Button>().ToList().Count(button => button.name.StartsWith("initial-cell-")) == 100, "100칸 읽기 전용 보드 표시");
            string snapshot = Snapshot(window.CurrentState);
            Click("initial-cell-5-5"); yield return null;
            Check(window.rootVisualElement.Q<Label>("initial-details").text.Contains("generator → target"), "선택 본체 연결 속성 표시");
            Click("initial-cell-0-7"); yield return null;
            Check(window.rootVisualElement.Q<Label>("initial-details").text.Contains("1번 공급: 청소로켓 ×2"), "공급 목록 순서 읽기 표시");
            Click("initial-build"); yield return null;
            Check(Snapshot(window.CurrentState) == snapshot, "같은 시드 UI 재구성 재현");
            Check(JsonUtility.ToJson(mixed) == json && EditorUtility.IsDirty(mixed) == dirty && File.ReadAllText(AssetDatabase.GetAssetPath(mixed)) == file, "확인 화면이 원본 JSON/dirty/파일 보존");
            UnityEngine.Random.State global = UnityEngine.Random.state;
            int seed = window.CurrentState.Random.Seed;
            Click("initial-new-seed");
            // 다른 Editor 프레임의 난수 소비를 버튼의 부작용으로 오인하지 않도록 호출 직후 비교한다.
            Check(JsonUtility.ToJson(UnityEngine.Random.state) == JsonUtility.ToJson(global), "새 시드 버튼 전역 난수 보존");
            yield return null;
            Check(window.CurrentState != null && window.CurrentState.Random.Seed != seed && window.CurrentState.Random.Seed == window.rootVisualElement.Q<IntegerField>("initial-seed").value, "새 시드 UI 표시/실사용 일치: " + window.rootVisualElement.Q<Label>("initial-status").text);
            LevelRuntimeState held = window.CurrentState; string heldSnapshot = Snapshot(held);
            Int(mixed, "moveCount", 38); yield return null; yield return null;
            Check(window.CurrentState == null && Snapshot(held) == heldSnapshot, "외부 수정 시 이전 화면 제거·기존 상태 독립");
            Click("initial-build"); yield return null;
            Check(window.CurrentState.MovesRemaining == 38, "수정된 메모리로 다시 구성");
            window.rootVisualElement.Q<IntegerField>("initial-seed").value = 12345;
            Check(window.CurrentState == null, "시드 입력 변경 시 이전 후보 제거");
            Click("initial-build"); yield return null;
            snapshot = Snapshot(window.CurrentState);
            window.Repaint(); yield return null; yield return null;
            Check(Snapshot(window.CurrentState) == snapshot, "조회/repaint 난수·상태 불변");
            Capture("wide.png");
            window.position = new Rect(10, 10, 680, 480); window.Focus(); yield return null; yield return null;
            Check(window.rootVisualElement.Q<Button>("initial-new-seed").worldBound.xMax <= 680 && window.rootVisualElement.Q<ScrollView>("initial-inspector").resolvedStyle.width >= 200, "좁은 창 입력과 속성 접근");
            Check(window.rootVisualElement.Q<Label>("initial-boundary").text == new StartConditionReport(window.CurrentState).Message + " · 실제 플레이 미지원", "실제 시작 검사 결과/플레이 미지원 안내");
            Click("initial-cell-0-0"); yield return null;
            Capture("narrow.png");
            ScrollView board = window.rootVisualElement.Q<ScrollView>("initial-board-scroll");
            Check(board.horizontalScroller.highValue > 0 && board.verticalScroller.highValue > 0, "좁은 창 양방향 스크롤 범위");
            Button lastCell = window.rootVisualElement.Q<Button>("initial-cell-9-9"); board.ScrollTo(lastCell);
            yield return null;
            Check(board.contentViewport.worldBound.Overlaps(lastCell.worldBound), "좁은 창 마지막 칸까지 스크롤 접근");
            Capture("narrow-end.png");
            Int(mixed, "obstacles.Array.data[0].durability", 99); yield return null; yield return null;
            Click("initial-build"); yield return null;
            Check(window.CurrentState == null && window.rootVisualElement.Q<Label>("initial-status").text.Contains("obstacles.Array.data[0]"), "실패 시 이전 성공 제거와 필드 진단");
            Capture("invalid.png");
            window.rootVisualElement.Q<ObjectField>("initial-level").value = null; Click("initial-build"); yield return null;
            Check(window.CurrentState == null && window.rootVisualElement.Q<Label>("initial-status").text.Contains("MissingLevel"), "레벨 해제/미선택 진단");
            JsonUtility.FromJsonOverwrite(mixedJson, mixed);
            window.rootVisualElement.Q<ObjectField>("initial-level").value = mixed; Click("initial-build"); yield return null;
            Check(window.CurrentState != null, "실패 후 정상 레벨 재구성");
            // 기존 에셋 편집/복제/Undo 경로와 새 읽기 전용 창의 상호작용을 검증한다.
            string before = JsonUtility.ToJson(mixed);
            using (SerializedObject edit = new SerializedObject(mixed)) { edit.FindProperty("moveCount").intValue = 43; edit.ApplyModifiedProperties(); }
            yield return null; yield return null;
            Check(window.CurrentState == null, "기존 Inspector 편집 무효화");
            Undo.PerformUndo(); yield return null;
            Check(JsonUtility.ToJson(mixed) == before, "확인 창 구성 뒤 기존 Undo 정상");
            Undo.PerformRedo(); yield return null;
            Check(mixed.MoveCount == 43, "기존 Redo 정상");
            LevelDefinition copy = LevelAssetOperations.DuplicateAtPath(mixed, folder + "/Copy.asset", 81002);
            Check(copy != null && copy.MoveCount == 43 && mixed.LevelNumber == 81001, "확인 창 이후 기존 복제 독립");
            Check(Build(copy).MovesRemaining == 43, "복제본 초기 후보 구성");
            JsonUtility.FromJsonOverwrite(mixedJson, mixed);
            window.rootVisualElement.Q<ObjectField>("initial-level").value = copy; yield return null;
            Check(window.CurrentState == null, "다른 레벨 선택 시 이전 후보 제거");
            Click("initial-build"); yield return null;
            Check(window.CurrentState.LevelNumber == 81002, "다른 레벨 구성 대상 일치");
            File.WriteAllText(Evidence + "/performance.txt", "마지막 UI 구성: " + window.LastBuildMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms\nUnity " + Application.unityVersion + " / " + Environment.Version);
        }

        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("버튼 없음: " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }

        private static void Capture(string name)
        {
            Rect rect = window.position;
            Texture2D texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            texture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, (int)rect.width, (int)rect.height)); texture.Apply();
            File.WriteAllBytes(Evidence + "/" + name, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}

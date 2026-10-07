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
    public static partial class StartingBoardVerification
    {
        private static LevelInitialStatePanel window;
        private static IEnumerator sequence;
        private static double nextTick;
        private static string folder;
        private static LevelDefinition mixed, random, invalid;
        [Serializable] private sealed class Saved
        { public string folder; public int process; public string[] paths, json, guids, snapshots; }

        public static void Start()
        {
            Directory.CreateDirectory(Evidence);
            if (File.Exists(Evidence + "/state.json")) throw new InvalidOperationException("이전 검증 Restart 필요");
            Results.Clear();
            folder = "Assets/__StartingBoardVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                Patterns(); Actions(); Starts(); Edges();
                random = Definition(); AssetDatabase.CreateAsset(random, folder + "/Random.asset");
                invalid = Definition("{\"moveCount\":0}"); AssetDatabase.CreateAsset(invalid, folder + "/Invalid.asset");
                // 8단계의 복합 정답 에셋 생성 절차를 그대로 재사용한다.
                typeof(LevelInitialStateVerification).GetField("folder", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, folder);
                mixed = (LevelDefinition)typeof(LevelInitialStateVerification).GetMethod("Mixed", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                LevelRuntimeState raw = Build(mixed);
                StartingBoardSearch result = StartingBoardBuilder.Build(mixed, 12345);
                Check(result.Status == StartingBoardStatus.Success, "복합/부분 고정/비정형 보드 탐색 성공");
                foreach (RuntimeCell cell in raw.Cells)
                {
                    RuntimeCell actual = result.State.CellAt(cell.Coordinate);
                    bool fixedColor = mixed.InitialBlocks.Any(block => block.Kind == InitialBlockKind.FixedNormal && block.Coordinate.Equals(cell.Coordinate));
                    if (cell.Content == RuntimeContent.Normal && !fixedColor) Set(actual, "Color", cell.Color);
                    Check(Snapshot(cell) == Snapshot(actual), "변수 색 외 셀 필드 보존 " + cell.Coordinate);
                }
                Check(Snapshot(raw.Obstacles) == Snapshot(result.State.Obstacles) && Snapshot(raw.Missions) == Snapshot(result.State.Missions) &&
                    Snapshot(raw.Flow) == Snapshot(result.State.Flow) && Snapshot(raw.Connections) == Snapshot(result.State.Connections) &&
                    Snapshot(raw.Supply) == Snapshot(result.State.Supply), "복합 본체/미션/흐름/연결/공급 전체 보존");
                Check(EditorApplication.ExecuteMenuItem("Match/초기 보드 확인"), "Match 메뉴 진입");
                window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().Single().ActiveSimulationPanel;
                window.Owner.position = new Rect(10, 10, 1000, 780); window.Owner.Focus();
                sequence = UI(); EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.4;
            try
            {
                if (sequence.MoveNext()) return;
                string[] paths = AssetDatabase.FindAssets("t:LevelDefinition", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(s => s).ToArray();
                foreach (string path in paths) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path));
                File.WriteAllText(Evidence + "/state.json", JsonUtility.ToJson(new Saved
                {
                    folder = folder, process = System.Diagnostics.Process.GetCurrentProcess().Id, paths = paths,
                    json = paths.Select(p => JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<LevelDefinition>(p))).ToArray(),
                    guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray(),
                    snapshots = paths.Select(p => SearchSnapshot(StartingBoardBuilder.Build(AssetDatabase.LoadAssetAtPath<LevelDefinition>(p), 12345))).ToArray()
                }, true));
                Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }
        private static string SearchSnapshot(StartingBoardSearch search) => search.Status + ":" + search.Attempts + ":" + search.RandomDrawCount + ":" + search.DefinitionFingerprint + ":" + Snapshot(search.State);
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
            Results.Clear();
            Saved saved = JsonUtility.FromJson<Saved>(File.ReadAllText(Evidence + "/state.json"));
            Check(saved.process != System.Diagnostics.Process.GetCurrentProcess().Id, "독립 프로세스 확인");
            for (int i = 0; i < saved.paths.Length; i++)
            {
                LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(saved.paths[i]);
                Check(level != null && JsonUtility.ToJson(level) == saved.json[i] && AssetDatabase.AssetPathToGUID(saved.paths[i]) == saved.guids[i], "원본/GUID 재로드 " + saved.paths[i]);
                Check(SearchSnapshot(StartingBoardBuilder.Build(level, 12345)) == saved.snapshots[i], "검색 결과/이유/시도/난수 독립 재현 " + saved.paths[i]);
            }
            if (!saved.folder.StartsWith("Assets/__StartingBoardVerification_", StringComparison.Ordinal) || saved.folder.Contains("..")) throw new InvalidOperationException("정리 경로 오류");
            Check(AssetDatabase.DeleteAsset(saved.folder), "검증 소유 에셋 정리");
            File.WriteAllLines(Evidence + "/restart-results.txt", Results);
            File.Move(Evidence + "/state.json", Evidence + "/completed-" + DateTime.UtcNow.Ticks + ".json");
        }

        private static IEnumerator UI()
        {
            yield return null;
            VisualElement root = window.rootVisualElement;
            root.Q<PopupField<string>>("initial-mode").value = "원시 후보";
            root.Q<ObjectField>("initial-level").value = random;
            root.Q<IntegerField>("initial-seed").value = 12345; yield return null;
            string json = JsonUtility.ToJson(random); bool dirty = EditorUtility.IsDirty(random);
            Click("initial-build"); yield return null;
            Check(Snapshot(window.CurrentState) == Snapshot(Build(random)), "원시 후보 API 동일 결과 UI");
            string snapshot = Snapshot(window.CurrentState);
            Click("query-match-0"); yield return null;
            Check(root.Query<Button>(className: "query-highlight").ToList().Count > 0 && Snapshot(window.CurrentState) == snapshot, "매칭 목록 강조/상태 보존");
            Click("query-action-0"); yield return null;
            Check(root.Query<Button>(className: "query-highlight").ToList().Count == 2 && Snapshot(window.CurrentState) == snapshot, "행동 목록 강조/조회만 수행");
            Capture("raw-wide.png");
            root.Q<PopupField<string>>("initial-mode").value = "시작 조건 구성"; yield return null;
            Check(window.CurrentState == null && root.Q<Label>("initial-boundary").text.Contains("미검사"), "모드 변경 이전 성공 폐기");
            Click("initial-build"); yield return null;
            int ticks = 0;
            while (window.IsSearching && ticks++ < 60) yield return null;
            Check(window.LastSearch?.Status == StartingBoardStatus.Success && new StartConditionReport(window.CurrentState).IsSatisfied, "UI 시작 조건 구성 성공");
            Check(root.Q<Label>("initial-boundary").text == "시작 조건 통과 · 실제 플레이 미지원", "통과/미지원 안내 일치");
            Check(root.Q<Label>("initial-status").text.Contains("100000"), "탐색 횟수/상한 표시");
            Capture("success-wide.png");
            snapshot = Snapshot(window.CurrentState);
            Click("initial-build"); yield return null;
            ticks = 0; while (window.IsSearching && ticks++ < 60) yield return null;
            Check(Snapshot(window.CurrentState) == snapshot, "같은 시드 UI 재현");
            Check(JsonUtility.ToJson(random) == json && EditorUtility.IsDirty(random) == dirty, "UI 원본 JSON/dirty 보존");
            window.Owner.position = new Rect(10, 10, 680, 480); yield return null; yield return null;
            ScrollView board = root.Q<ScrollView>("initial-board-scroll");
            Button last = root.Q<Button>("initial-cell-8-8"); board.ScrollTo(last); yield return null;
            Check(board.contentViewport.worldBound.Overlaps(last.worldBound) && root.Q<Button>("initial-new-seed").worldBound.xMax <= 680, "좁은 창 끝칸/입력 접근");
            Capture("success-narrow.png");
            root.Q<IntegerField>("initial-seed").value = 7; yield return null;
            Check(window.CurrentState == null && window.LastSearch == null, "시드 변경 결과 폐기");
            Click("initial-build");
            // 같은 이벤트 안에서 검색 시작 직후 입력을 바꾸어 오래된 결과 적용을 검증한다.
            root.Q<IntegerField>("initial-seed").value = 8; yield return null; yield return null;
            Check(!window.IsSearching && window.CurrentState == null, "검색 중 시드 변경 취소");
            Click("initial-build");
            JsonUtility.FromJsonOverwrite("{\"moveCount\":37}", random); yield return null; yield return null;
            Check(!window.IsSearching && window.CurrentState == null, "검색 중 미저장 정의 변경 취소");
            JsonUtility.FromJsonOverwrite(json, random);
            root.Q<ObjectField>("initial-level").value = invalid; yield return null;
            Click("initial-build"); yield return null;
            Check(window.LastSearch?.Status == StartingBoardStatus.DefinitionError && window.CurrentState == null, "정의 오류 UI 분류");
            Capture("invalid.png");
            LevelDefinition fixedLevel = Definition(@"{""initialBlocks"":[{""coordinate"":{""row"":0,""column"":0},""kind"":1,""fixedColor"":0},{""coordinate"":{""row"":0,""column"":1},""kind"":1,""fixedColor"":0},{""coordinate"":{""row"":0,""column"":2},""kind"":1,""fixedColor"":0}]}");
            AssetDatabase.CreateAsset(fixedLevel, folder + "/FixedFailure.asset");
            root.Q<ObjectField>("initial-level").value = fixedLevel; yield return null;
            Click("initial-build"); yield return null;
            Check(window.LastSearch?.Status == StartingBoardStatus.FixedMatch && root.Q<Label>("initial-boundary").text.Contains("고정 조건 위반"), "고정 위반 UI 실패 진단");
            Capture("fixed-failure.png");
            root.Q<ObjectField>("initial-level").value = random; yield return null;
            Click("initial-build");
            typeof(LevelInitialStatePanel).GetField("search", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window, new StartingBoardSearch(random, 12345, 0));
            yield return null;
            Check(window.LastSearch?.Status == StartingBoardStatus.LimitReached && window.CurrentState == null && root.Q<Label>("initial-boundary").text.Contains("이 결과만으로 해결 불가능을 뜻하지 않습니다"), "탐색 한도 UI는 불가능 단정하지 않음");
            Capture("limit.png");
            root.Q<PopupField<string>>("initial-mode").value = "원시 후보"; yield return null;
            Check(window.LastSearch == null && root.Q<Label>("initial-boundary").text.Contains("미검사"), "실패 뒤 목적 변경 진단 초기화");
            root.Q<PopupField<string>>("initial-mode").value = "시작 조건 구성"; yield return null;
            Click("initial-build");
            root.Q<ObjectField>("initial-level").value = mixed; yield return null;
            Check(!window.IsSearching && window.CurrentState == null, "검색 중 레벨 변경 취소");
            Click("initial-new-seed"); yield return null;
            ticks = 0; while (window.IsSearching && ticks++ < 60) yield return null;
            Check(window.LastSearch?.Status == StartingBoardStatus.Success && window.CurrentState.Random.Seed == root.Q<IntegerField>("initial-seed").value, "시작 모드 새 시드 버튼 표시/실사용 일치");
            root.Q<ObjectField>("initial-level").value = random; yield return null;
            Click("initial-build"); window.Owner.Close(); yield return null;
            Check(window == null || !window.IsSearching, "검색 중 창 종료 취소");
            window = null;
        }
        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("버튼 없음 " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }
        private static void Capture(string name)
        {
            Rect rect = window.Owner.position;
            Texture2D texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            texture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, (int)rect.width, (int)rect.height)); texture.Apply();
            File.WriteAllBytes(Evidence + "/" + name, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}

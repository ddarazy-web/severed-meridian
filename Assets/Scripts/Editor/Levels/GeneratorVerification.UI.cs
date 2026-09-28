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
    public static partial class GeneratorVerification
    {
        private static LevelInitialStateWindow window;
        private static LevelDefinition fixture;
        private static IEnumerator sequence;
        private static double nextTick;
        private static string folder;
        [Serializable] private sealed class Saved { public string folder, path, json, guid, replay; public int process; }

        private static LevelDefinition PlayFixture()
        {
            LevelDefinition level = Multiple();
            Invoke(typeof(PowerEffectVerification), "Place", null, level, C(3, 4), InitialBlockKind.Rocket, RocketDirection.Vertical, RabbitColor.Type1);
            string error = LevelSupplyEditing.PlaceSources(level, new[] { C(0, 4) });
            if (error != null) throw new InvalidOperationException(error);
            int index = LevelSupplyRules.FindSource(level, C(0, 4));
            error = LevelSupplyEditing.SetSourceProperty(level, new[] { index }, "mode", (int)SupplyMode.Fixed);
            if (error != null) throw new InvalidOperationException(error);
            SupplyItem item = JsonUtility.FromJson<SupplyItem>("{\"kind\":" + (int)SupplyKind.Rocket + ",\"count\":30,\"direction\":" + (int)RocketDirection.Vertical + "}");
            error = LevelSupplyEditing.SetItems(level, index, new[] { item });
            if (error != null) throw new InvalidOperationException(error);
            return level;
        }
        private static void Finish(BoardActionExecutor executor)
        {
            int steps = 0;
            while (executor.HasPendingCascade && steps++ < 500) executor.AdvanceCascade();
            if (executor.HasPendingCascade || executor.Phase != BoardActionPhase.Ready) throw new InvalidOperationException("연쇄 완료 실패 " + executor.Phase);
        }
        private static string Replay(LevelDefinition level)
        {
            BoardActionExecutor executor = new BoardActionExecutor(Build(level)); string text = "";
            for (int turn = 1; turn <= 3; turn++)
            {
                BoardActionResult action = executor.Activate(ChargeRocket(executor.State));
                if (!action.IsApplied) throw new InvalidOperationException(action.Message);
                Finish(executor);
                text += Snapshot(action) + Snapshot(executor.State) + Snapshot(executor.TurnEffects) + Snapshot(executor.CascadeHistory);
            }
            if (executor.State.Obstacles[0].Charge != 3 || GeneratorRules.ActiveConnections(executor.State).Count != 0)
                throw new InvalidOperationException("재현 완충/연결 제거 실패");
            return BoardActionExecutor.Version + PowerEffectResolution.Version + SettlementResolution.Version + text;
        }
        private static Board.BoardCoordinate ChargeRocket(LevelRuntimeState state) => state.Cells.First(c => c.Coordinate.Column == 4 && c.Coordinate.Row < 4 &&
            c.Content == RuntimeContent.Rocket && c.RocketDirection == RocketDirection.Vertical).Coordinate;
        public static void Start()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            if (File.Exists(Evidence + "/state.json")) throw new InvalidOperationException("이전 발전기 검증 Restart 필요");
            folder = "Assets/__GeneratorVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                fixture = PlayFixture(); AssetDatabase.CreateAsset(fixture, folder + "/Generator.asset"); AssetDatabase.SaveAssets();
                Check(LevelDefinitionValidator.Validate(fixture).Count == 0 && new StartConditionReport(Build(fixture)).IsSatisfied, "플레이 시험 레벨 정합성/시작 조건");
                Check(EditorApplication.ExecuteMenuItem("Match/초기 보드 확인"), "Match 메뉴 진입");
                window = Resources.FindObjectsOfTypeAll<LevelInitialStateWindow>().Single(); window.position = new Rect(10, 10, 1000, 780); window.Focus();
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
            if (window != null) window.Close(); EditorApplication.Exit(error == null ? 0 : 1);
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
            root.Q<ObjectField>("initial-level").value = fixture; Click("initial-build"); yield return null;
            string json = JsonUtility.ToJson(fixture), file = File.ReadAllText(AssetDatabase.GetAssetPath(fixture)), initial = Snapshot(window.CurrentState);
            Check(root.Q<Button>("initial-cell-4-4").text.Contains("0/3") && root.Q<VisualElement>("generator-overlay") != null, "초기 충전값/표시등 오버레이");
            Check(root.Q<VisualElement>("generator-overlay").pickingMode == PickingMode.Ignore, "전선 입력 통과");
            Capture("initial-wide.png");
            BoardActionExecutor manual = new BoardActionExecutor(window.CurrentState);
            root.Q<Toggle>("execution-mode").value = true;
            for (int turn = 1; turn <= 3; turn++)
            {
                Board.BoardCoordinate rocket = ChargeRocket(window.Execution.State);
                Click("initial-cell-" + rocket.Row + "-" + rocket.Column); Click("execution-activate"); yield return null;
                Check(window.Execution.LastApplied.IsApplied && window.Execution.State.Obstacles[0].Charge == turn, "실제 UI 충전 " + turn);
                if (turn < 3) Check(root.Q<Button>("initial-cell-4-4").text.Contains(turn + "/3"), "충전 표시 갱신 " + turn);
                if (turn == 3) Check(!window.Execution.State.Cells.Any(c => c.ObstacleIndex.HasValue), "실제 UI 완충 즉시 전체 제거");
                Capture("turn" + turn + "-effect.png");
                manual.Activate(ChargeRocket(manual.State)); Finish(manual);
                Click("execution-cascade"); for (int wait = 0; window.CascadeRunning && wait < 500; wait++) yield return null;
                Check(!window.CascadeRunning && Snapshot(window.Execution.State) == Snapshot(manual.State) && Snapshot(window.Execution.TurnEffects) == Snapshot(manual.TurnEffects), "단계/끝까지 상태·충전·기록 일치 " + turn);
                Check(root.Q<Label>("initial-overview").text.Contains("발전기 충전·연결 기록"), "발전기 진단 기록 " + turn);
                if (turn == 1)
                {
                    window.position = new Rect(10, 10, 680, 780); yield return null; Capture("charged-narrow.png");
                    Check(root.Q<Button>("execution-cascade").worldBound.xMax <= 680, "680폭 버튼 접근");
                    root.Q<ScrollView>("initial-board-scroll").scrollOffset = new Vector2(90, 40); yield return null; Capture("charged-scrolled.png");
                    root.Q<ScrollView>("initial-board-scroll").scrollOffset = Vector2.zero; window.position = new Rect(10, 10, 1000, 780); yield return null;
                }
            }
            Check(GeneratorRules.ActiveConnections(window.Execution.State).Count == 0 && window.Execution.State.Missions.All(m => m.Remaining == 0), "완충 후 연결/미션 완료");
            Capture("complete-wide.png");
            Check(JsonUtility.ToJson(fixture) == json && File.ReadAllText(AssetDatabase.GetAssetPath(fixture)) == file && !EditorUtility.IsDirty(fixture) && Snapshot(window.CurrentState) == initial, "원본/파일/dirty/시작 상태 보존");
            Click("execution-reset"); yield return null;
            Check(window.Execution == null && Snapshot(window.CurrentState) == initial && root.Q<Button>("initial-cell-4-4").text.Contains("0/3"), "동일 시드 초기화/충전등 복구");
            LevelDefinition maximum = PlayFixture();
            using (SerializedObject data = new SerializedObject(maximum))
            { data.FindProperty("obstacles.Array.data[0].requiredCharge").intValue = 5; data.ApplyModifiedPropertiesWithoutUndo(); }
            root.Q<ObjectField>("initial-level").value = maximum; Click("initial-build"); yield return null;
            Button maximumCell = root.Q<Button>("initial-cell-4-4");
            Capture("max-charge-wide.png");
            Check(maximumCell.text.Contains("0/5") && window.CurrentState.Obstacles[0].Definition.RequiredCharge == 5, "최대 충전값 표시/표시등5개 캡처");
            Vector2 measured = maximumCell.MeasureTextSize(maximumCell.text, maximumCell.contentRect.width, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
            Check(measured.y <= maximumCell.contentRect.height + 1, "최대 충전 표시가 칸 높이 안에 들어감");
            root.Q<ObjectField>("initial-level").value = null; yield return null;
            Check(root.Q<VisualElement>("generator-overlay") == null && window.Execution == null, "레벨 변경시 전선/실행 수명 해제");
            UnityEngine.Object.DestroyImmediate(maximum);
        }
        private static void Capture(string name)
        {
            Rect rect = window.position; Texture2D texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            texture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, (int)rect.width, (int)rect.height)); texture.Apply();
            File.WriteAllBytes(Evidence + "/" + name, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
        public static void Restart()
        {
            Results.Clear(); Saved saved = JsonUtility.FromJson<Saved>(File.ReadAllText(Evidence + "/state.json"));
            Check(saved.process != System.Diagnostics.Process.GetCurrentProcess().Id, "독립 Unity 프로세스");
            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(saved.path);
            Check(JsonUtility.ToJson(level) == saved.json && AssetDatabase.AssetPathToGUID(saved.path) == saved.guid, "원본 JSON/GUID 재로드");
            Check(Replay(level) == saved.replay, "세 수 충전/연결 제거/미션/보드/난수/문맥/연쇄 독립 재현");
            if (!saved.folder.StartsWith("Assets/__GeneratorVerification_", StringComparison.Ordinal) || saved.folder.Contains("..")) throw new InvalidOperationException("임시 경로 오류");
            Check(AssetDatabase.DeleteAsset(saved.folder), "소유 임시 에셋 정리");
            File.WriteAllLines(Evidence + "/restart-results.txt", Results);
            File.Move(Evidence + "/state.json", Evidence + "/completed-" + DateTime.UtcNow.Ticks + ".json");
        }
    }
}

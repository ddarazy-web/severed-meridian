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
    public static partial class TargetPowerVerification
    {
        private static LevelInitialStatePanel window;
        private static LevelDefinition uiLevel;
        private static string folder;
        private static IEnumerator sequence;
        private static double nextTick;
        [Serializable] private sealed class Saved
        { public string folder, path, json, guid, replay; public int process; }

        private static LevelDefinition ReplayBoard()
        {
            LevelDefinition level = Make(); Mission(level, MissionKind.Color, 100); Mission(level, MissionKind.Crate, 2);
            Place(level, C(4, 4), InitialBlockKind.Drone); Place(level, C(4, 5), InitialBlockKind.Drone);
            Place(level, C(8, 0), InitialBlockKind.Bomb); Place(level, C(8, 8), InitialBlockKind.Magnet);
            Place(level, C(7, 7), InitialBlockKind.Rocket);
            Invoke(typeof(PowerEffectVerification), "Crate", null, level, C(2, 2), 2);
            Invoke(typeof(PowerEffectVerification), "Crate", null, level, C(2, 7), 1);
            return level;
        }

        private static string Replay(LevelDefinition level)
        {
            StartingBoardSearch initial = StartingBoardBuilder.Build(level, 12345);
            if (initial.State == null) throw new InvalidOperationException(initial.Message);
            BoardActionExecutor executor = new BoardActionExecutor(initial.State);
            string result = "";
            for (int turn = 0; turn < 4; turn++)
            {
                BoardActionResult action;
                if (turn == 0) action = executor.Activate(C(4, 4));
                else
                {
                    ActionCandidate candidate = ActionQuery.Find(executor.State).Where(a => a.Kind != QueryActionKind.SwapCombination)
                        .OrderBy(a => a.Kind == QueryActionKind.Activate && a.FirstContent == RuntimeContent.Magnet ? 0 : a.Kind == QueryActionKind.Activate ? 1 : 2).First();
                    action = candidate.Second.HasValue ? executor.Swap(candidate.First, candidate.Second.Value) : executor.Activate(candidate.First);
                }
                if (!action.IsApplied) throw new InvalidOperationException(action.Message);
                int step = 0;
                while (executor.HasPendingCascade && step++ < 500)
                    if (!executor.AdvanceCascade().IsApplied) throw new InvalidOperationException("재현 연쇄 실패");
                if (executor.HasPendingCascade) throw new InvalidOperationException("재현 연쇄 한도");
                result += Snapshot(action) + "\n" + Snapshot(executor.State) + "\n" + Snapshot(executor.TurnEffects) + "\n" + Snapshot(executor.CascadeHistory) + "\n" +
                    string.Join(";", executor.State.Cells.Select(c => executor.TurnEffects.IsProtected(c.Coordinate) + "," + executor.TurnEffects.LastArrival(c.Coordinate) + "," + executor.TurnEffects.HasFired(c.Coordinate)));
                if (turn < 3 && executor.Phase != BoardActionPhase.Ready) throw new InvalidOperationException("재현용 4턴 전에 종료 " + turn);
            }
            return result;
        }

        public static void Start()
        {
            Directory.CreateDirectory(Evidence);
            if (File.Exists(Evidence + "/state.json")) throw new InvalidOperationException("이전 검증 Restart 필요");
            Results.Clear(); folder = "Assets/__TargetPowerVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                DataChecks(); uiLevel = ReplayBoard(); AssetDatabase.CreateAsset(uiLevel, folder + "/Replay.asset"); AssetDatabase.SaveAssets();
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
                AssetDatabase.SaveAssetIfDirty(uiLevel); string path = AssetDatabase.GetAssetPath(uiLevel);
                File.WriteAllText(Evidence + "/state.json", JsonUtility.ToJson(new Saved { folder = folder, path = path,
                    json = JsonUtility.ToJson(uiLevel), guid = AssetDatabase.AssetPathToGUID(path), replay = Replay(uiLevel), process = System.Diagnostics.Process.GetCurrentProcess().Id }, true));
                Check(true, "4턴 상태/미션/선택 색/드론 예약/착탄/난수 재현 기준 저장"); FinishUI(null);
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
            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(saved.path);
            Check(level != null && JsonUtility.ToJson(level) == saved.json && AssetDatabase.AssetPathToGUID(saved.path) == saved.guid, "원본 JSON/GUID 재로드 보존");
            Check(Replay(level) == saved.replay, "4턴 전체 상태/미션/색/목표/예약/착탄/난수 독립 재현");
            if (!saved.folder.StartsWith("Assets/__TargetPowerVerification_", StringComparison.Ordinal) || saved.folder.Contains("..")) throw new InvalidOperationException("정리 경로 오류");
            Check(AssetDatabase.DeleteAsset(saved.folder), "소유 임시 에셋 정리");
            File.WriteAllLines(Evidence + "/restart-results.txt", Results);
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
            root.Q<ObjectField>("initial-level").value = uiLevel; root.Q<IntegerField>("initial-seed").value = 12345;
            root.Q<PopupField<string>>("initial-mode").value = "시작 조건 구성"; Click("initial-build"); yield return null;
            string json = JsonUtility.ToJson(uiLevel), file = File.ReadAllText(AssetDatabase.GetAssetPath(uiLevel)), initial = Snapshot(window.CurrentState);
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-4-4"); Click("execution-activate"); yield return null;
            Check(window.Execution.LastApplied.IsApplied && window.Execution.TurnEffects.Targeting.Count(r => r.Event == TargetingEvent.Landed) == 2, "실제 UI 드론 연쇄·2착탄");
            Check(root.Q<Label>("initial-overview").text.Contains("예약") && root.Q<Label>("initial-details").text.Contains("실제 미션 진행"), "실제/예상 진행과 드론 목표 기록 표시");
            Capture("drone-wide.png"); window.Owner.position = new Rect(10, 10, 680, 780); yield return null; Capture("drone-narrow.png");
            Check(root.Q<Button>("execution-cascade").worldBound.xMax <= window.Owner.position.width, "680폭 연쇄 버튼 화면 안 배치");
            BoardActionExecutor manual = new BoardActionExecutor(window.CurrentState); manual.Activate(C(4, 4));
            while (manual.HasPendingCascade) { if (!manual.AdvanceCascade().IsApplied) throw new InvalidOperationException("단계 연쇄 실패"); }
            Click("execution-cascade"); for (int i = 0; window.CascadeRunning && i < 500; i++) yield return null;
            Check(!window.CascadeRunning && Snapshot(window.Execution.State) == Snapshot(manual.State) && Snapshot(window.Execution.TurnEffects) == Snapshot(manual.TurnEffects), "단계별/끝까지 실행 상태·미션·기록 일치");
            Check(JsonUtility.ToJson(uiLevel) == json && File.ReadAllText(AssetDatabase.GetAssetPath(uiLevel)) == file && !EditorUtility.IsDirty(uiLevel) && Snapshot(window.CurrentState) == initial, "UI 원본/파일/dirty/시작 상태 보존");

            Click("execution-reset"); yield return null; root.Q<Toggle>("execution-mode").value = true;
            Check(Snapshot(window.Execution.State) == initial, "동일 시드 초기화");
            Click("initial-cell-8-8"); Click("execution-activate"); yield return null;
            Check(window.Execution.TurnEffects.Targeting.Any(r => r.Event == TargetingEvent.ColorSelected) && root.Q<Label>("initial-overview").text.Contains("자석 선택 색"), "UI 자석 선택 색 표시");
            window.Owner.position = new Rect(10, 10, 1000, 780); yield return null; Capture("magnet-wide.png");
            Click("execution-cascade"); root.Q<IntegerField>("initial-seed").value = 7;
            Check(window.Execution == null && !window.CascadeRunning, "시드 변경 실행/예약 소유자 해제");
            root.Q<IntegerField>("initial-seed").value = 12345; Click("initial-build"); yield return null;
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-4-4"); Click("execution-activate"); Click("execution-cascade");
            root.Q<PopupField<string>>("initial-mode").value = "원시 후보";
            Check(window.Execution == null && !window.CascadeRunning, "모드 변경 실행 해제");
            root.Q<PopupField<string>>("initial-mode").value = "시작 조건 구성"; Click("initial-build"); yield return null;
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-4-4"); Click("execution-activate"); Click("execution-cascade");
            root.Q<ObjectField>("initial-level").value = null;
            Check(window.Execution == null && !window.CascadeRunning, "레벨 변경 실행 해제");
            root.Q<ObjectField>("initial-level").value = uiLevel; Click("initial-build"); yield return null;
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-4-4"); Click("execution-activate"); Click("execution-cascade");
            JsonUtility.FromJsonOverwrite("{\"moveCount\":19}", uiLevel); Invoke(typeof(LevelInitialStatePanel), "CheckInput", window);
            Check(window.Execution == null && !window.CascadeRunning, "원본 변경 실행 해제"); JsonUtility.FromJsonOverwrite(json, uiLevel);
            Click("initial-build"); yield return null; root.Q<Toggle>("execution-mode").value = true;
            Click("initial-cell-4-4"); Click("execution-activate"); Click("execution-cascade"); window.Owner.Close(); yield return null;
            Check(window == null || (!window.CascadeRunning && window.Execution == null), "창 닫기 실행 해제");
        }

        private static void Capture(string name)
        {
            Rect rect = window.Owner.position; Texture2D texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            texture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, (int)rect.width, (int)rect.height)); texture.Apply();
            File.WriteAllBytes(Evidence + "/" + name, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}

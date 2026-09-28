using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Simulation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class RoundEndVerification
    {
        private static LevelInitialStateWindow window;
        private static LevelDefinition fixture;
        private static IEnumerator sequence;
        private static double nextTick;
        private static string folder;
        [Serializable] private sealed class Saved { public string folder, path, json, guid, replay; public int process; }
        private static LevelDefinition PlayFixture() => (LevelDefinition)Invoke(typeof(RecoveryVerification), "PlayFixture");
        private static string Replay(LevelDefinition level)
        {
            BoardActionExecutor executor = new BoardActionExecutor(Build(level));
            BoardActionResult action = executor.Activate(C(9, 0));
            if (!action.IsApplied) throw new InvalidOperationException(action.Message);
            Finish(executor);
            if (executor.State.Recoveries.Count != 2 || executor.State.Missions[0].Progress != 2)
                throw new InvalidOperationException("재현 회수 수량 불일치");
            if (!executor.WinningTurnEffects.Targeting.Any(r => r.Event == TargetingEvent.Landed)) throw new InvalidOperationException("재현 드론 착탄 기록 없음");
            return BoardActionExecutor.Version + RecoveryRules.Version + SettlementResolution.Version +
                Snapshot(action) + Snapshot(executor.State) + Snapshot(executor.TurnEffects) + Snapshot(executor.CascadeHistory) + Snapshot(executor.Outcome);
        }
        public static void Start()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            if (File.Exists(Evidence + "/state.json")) throw new InvalidOperationException("이전 회수 검증 Restart 필요");
            folder = "Assets/__RoundEndVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                fixture = PlayFixture(); AssetDatabase.CreateAsset(fixture, folder + "/Recovery.asset"); AssetDatabase.SaveAssets();
                Check(LevelDefinitionValidator.Validate(fixture).Count == 0 && new StartConditionReport(Build(fixture)).IsSatisfied, "실제 회수 시험 정합성·시작 조건");
                Check(EditorApplication.ExecuteMenuItem("Match/초기 보드 확인"), "Match 메뉴 진입");
                window = Resources.FindObjectsOfTypeAll<LevelInitialStateWindow>().Single();
                window.position = new Rect(10, 10, 1000, 780); window.Focus();
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
            Invoke(typeof(SettlementVerification), "RaiseVerificationWindow"); yield return null;
            VisualElement root = window.rootVisualElement;
            root.Q<IntegerField>("initial-seed").value = 12345;
            root.Q<PopupField<string>>("initial-mode").value = "시작 조건 구성";
            root.Q<ObjectField>("initial-level").value = fixture; Click("initial-build");
            for (int wait = 0; (window.IsSearching || window.CurrentState == null) && wait < 100; wait++) yield return null;
            string json = JsonUtility.ToJson(fixture), file = File.ReadAllText(AssetDatabase.GetAssetPath(fixture)), initial = Snapshot(window.CurrentState);
            Check(root.Q<Button>("initial-cell-8-0").text.Contains("회수") && root.Q<Button>("initial-cell-9-0").text.Contains("도착"), "부품과 도착 바닥 보드 표시");
            Capture("initial-wide.png");
            BoardActionExecutor manual = new BoardActionExecutor(window.CurrentState);
            root.Q<Toggle>("execution-mode").value = true;
            Click("initial-cell-9-0"); Click("execution-activate"); yield return null;
            Check(window.Execution.LastApplied.IsApplied && window.Execution.State.CellAt(C(8, 0)).Content == RuntimeContent.Recovery, "실제 UI 로켓 관통·부품 불파괴");
            manual.Activate(C(9, 0)); Finish(manual);
            Click("execution-settle"); yield return null;
            Check(window.Execution.State.Recoveries.Count == 2 && root.Q<Label>("initial-overview").text.Contains("회수 부품"), "실제 낙하·고정 공급·회수 기록");
            Capture("settled-wide.png");
            Click("execution-cascade"); for (int wait = 0; window.CascadeRunning && wait < 500; wait++) yield return null;
            Check(!window.CascadeRunning && Snapshot(window.Execution.State) == Snapshot(manual.State) &&
                Snapshot(window.Execution.TurnEffects) == Snapshot(manual.TurnEffects) && Snapshot(window.Execution.CascadeHistory) == Snapshot(manual.CascadeHistory), "단계·끝까지 보드·난수·회수·공급·문맥·이력 일치");
            Check(root.Q<Label>("initial-details").text.Contains("회수 누적 2") && root.Q<Label>("initial-details").text.Contains("남은 목표 0"), "연쇄 후 실제 회수·남은 목표 표시");
            Check(window.Execution.State.MovesRemaining == 19 && window.Execution.State.Supply.Sources[0].ItemIndex == 2, "한 수 소모·고정 목록 커서");
            Check(window.Execution.Outcome?.Kind == BoardOutcomeKind.Won && root.Q<Label>("initial-details").text.Contains("성공"), "실제 UI 라스트팡 완료·성공 결과 표시");
            Check(root.Q<Button>("execution-skip-last-pang").style.display == DisplayStyle.None && !root.Q<Button>("execution-activate").enabledInHierarchy, "결과 후 건너뛰기 숨김·행동 차단");
            window.position = new Rect(10, 10, 680, 780); yield return null; Capture("complete-narrow.png");
            Check(root.Q<Button>("execution-cascade").worldBound.xMax <= 680, "680폭 버튼 접근");
            root.Q<ScrollView>("initial-board-scroll").scrollOffset = new Vector2(90, 40); yield return null; Capture("complete-scrolled.png");
            Check(Snapshot(window.Execution.State) == Snapshot(manual.State), "표시·스크롤 상태와 난수 무변경");
            root.Q<ScrollView>("initial-board-scroll").scrollOffset = Vector2.zero; window.position = new Rect(10, 10, 1000, 780); yield return null;
            Check(JsonUtility.ToJson(fixture) == json && File.ReadAllText(AssetDatabase.GetAssetPath(fixture)) == file && !EditorUtility.IsDirty(fixture) && Snapshot(window.CurrentState) == initial, "원본 JSON·파일·dirty·초기 사본 보존");
            Click("execution-reset"); yield return null;
            Check(window.Execution == null && Snapshot(window.CurrentState) == initial, "동일 시드 재시작·회수 및 커서 초기화");
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-9-0"); Click("execution-activate");
            for (int step = 0; window.Execution.Outcome == null && step < 100; step++)
            {
                Click(window.Execution.Phase == BoardActionPhase.WaitingForFall ? "execution-settle" : "execution-automatic");
                yield return null;
            }
            Check(window.Execution.IsLastPang && root.Q<Button>("execution-skip-last-pang").enabledInHierarchy, "성공 확정 직후 실제 건너뛰기 버튼 제공");
            Capture("last-pang-wide.png");
            string won = Snapshot(window.Execution.Outcome), atSkip = Snapshot(window.Execution.State);
            Click("execution-skip-last-pang"); yield return null;
            Check(!window.Execution.HasPendingCascade && Snapshot(window.Execution.Outcome) == won && Snapshot(window.Execution.State) == atSkip && root.Q<Label>("initial-details").text.Contains("건너뛰기"), "실제 건너뛰기 성공·난수 보존 및 결과 표시");
            Click("execution-reset"); yield return null;
            foreach (int moves in new[] { 1, 20 })
            {
                LevelDefinition ending = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", new Dictionary<BoardCoordinate, int>
                { [C(9, 0)] = 0, [C(9, 1)] = 1, [C(9, 2)] = 0, [C(8, 1)] = 0 }, moves);
                root.Q<ObjectField>("initial-level").value = ending; Click("initial-build");
                for (int wait = 0; (window.IsSearching || window.CurrentState == null) && wait < 100; wait++) yield return null;
                root.Q<Toggle>("execution-mode").value = true;
                Click("initial-cell-8-1"); Click("initial-cell-9-1"); Click("execution-swap"); Click("execution-cascade");
                for (int wait = 0; window.CascadeRunning && wait < 100; wait++) yield return null;
                Check(window.Execution.Outcome?.Kind == (moves == 1 ? BoardOutcomeKind.MovesExhausted : BoardOutcomeKind.Blocked) &&
                    root.Q<Label>("initial-details").text.Contains(moves == 1 ? "이동 수 소진" : "진행 불가"), "실제 UI 종료 이유 표시 " + moves);
                if (moves != 1) Check(root.Q<Label>("initial-details").text.Contains("자동 재배치 진행 불가") && root.Q<Label>("initial-details").text.Contains("시도"), "실제 UI 재배치 사유·시도 표시");
                Capture(moves == 1 ? "moves-exhausted.png" : "blocked.png");
                root.Q<ObjectField>("initial-level").value = null; UnityEngine.Object.DestroyImmediate(ending); yield return null;
            }
            root.Q<ObjectField>("initial-level").value = fixture; Click("initial-build");
            for (int wait = 0; (window.IsSearching || window.CurrentState == null) && wait < 100; wait++) yield return null;
            root.Q<Toggle>("execution-mode").value = true;
            // 원본이 아닌 실행 사본의 손상 데이터를 주입해 실제 입력 경로의 오류 표시를 검증한다.
            Set(window.Execution.State.CellAt(C(0, 0)), "Content", RuntimeContent.Obstacle);
            string invalid = Snapshot(window.Execution.State);
            Click("initial-cell-9-0"); Click("execution-activate"); yield return null;
            Check(window.Execution.Outcome?.Kind == BoardOutcomeKind.Aborted && Snapshot(window.Execution.State) == invalid &&
                root.Q<Label>("initial-details").text.Contains("실행 중단") && !root.Q<Button>("execution-activate").enabledInHierarchy, "손상 실행 사본은 실제 UI 오류 종료·입력 차단·원자성");
            Capture("aborted.png");
            Click("execution-reset"); yield return null;
            root.Q<Toggle>("execution-mode").value = true; Click("initial-cell-9-0"); Click("execution-activate"); Click("execution-cascade");
            root.Q<ObjectField>("initial-level").value = null; yield return null;
            Check(window.Execution == null && !window.CascadeRunning, "레벨 전환시 연쇄 실행 해제");
            window.Close(); yield return null;
            Check(window == null || !window.CascadeRunning, "창 종료 후 실행 수명 해제");
        }
        private static void Capture(string name)
        {
            Rect rect = window.position; Texture2D texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
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
                if (!saved.folder.StartsWith("Assets/__RoundEndVerification_", StringComparison.Ordinal) || saved.folder.Contains("..")) throw new InvalidOperationException("임시 경로 오류");
                Check(AssetDatabase.DeleteAsset(saved.folder), "소유 임시 에셋 정리");
                File.Move(Evidence + "/state.json", Evidence + "/completed-" + DateTime.UtcNow.Ticks + ".json");
                File.WriteAllLines(Evidence + "/restart-results.txt", Results); EditorApplication.Exit(0);
            }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/restart-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}


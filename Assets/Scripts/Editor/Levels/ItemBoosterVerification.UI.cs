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
    public static partial class ItemBoosterVerification
    {
        private static LevelInitialStateWindow window;
        private static LevelDefinition fixture;
        private static IEnumerator sequence;
        private static double nextTick;
        private static string folder;
        [Serializable] private sealed class Saved { public string folder, path, json, guid, replay; public int process; }
        private static LevelDefinition PlayFixture()
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
            Invoke(typeof(PowerEffectVerification), "Place", level, C(9, 9), InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1);
            return level;
        }
        private static readonly StartBooster[] Selected = { StartBooster.Rocket, StartBooster.Bomb, StartBooster.Magnet };
        private static string Replay(LevelDefinition level)
        {
            BoardActionExecutor executor = new BoardActionExecutor(Build(level), Selected);
            BoardCoordinate target = executor.State.Cells.First(c => c.Content == RuntimeContent.Normal).Coordinate;
            if (!executor.UseItem(BoardItem.Hammer, target).IsApplied) throw new InvalidOperationException("재현 망치 사용 실패");
            Finish(executor);
            if (!executor.UseItem(BoardItem.Swap, C(5, 5), C(5, 6)).IsApplied) throw new InvalidOperationException("재현 자리 바꾸기 실패");
            Finish(executor);
            if (!executor.UseItem(BoardItem.Shuffle).IsApplied) throw new InvalidOperationException("재현 섞기 실패");
            Finish(executor);
            return BoardActionExecutor.Version + PowerEffectResolution.Version + Snapshot(executor.State) + Snapshot(executor.TurnEffects) +
                Snapshot(executor.CascadeHistory) + Snapshot(executor.ItemUses) + Snapshot(executor.BoosterPlacements) + Snapshot(executor.PendingBoosters);
        }
        public static void Start()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            if (File.Exists(Evidence + "/state.json")) throw new InvalidOperationException("이전 아이템 검증 Restart 필요");
            folder = "Assets/__ItemBoosterVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                fixture = PlayFixture(); AssetDatabase.CreateAsset(fixture, folder + "/Items.asset"); AssetDatabase.SaveAssets();
                Check(LevelDefinitionValidator.Validate(fixture).Count == 0 && new StartConditionReport(Build(fixture)).IsSatisfied, "실제 아이템 시험 정합성·시작 조건");
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
            foreach (StartBooster booster in Selected) root.Q<Toggle>("booster-" + booster).value = true;
            Capture("selection-wide.png");
            root.Q<Toggle>("execution-mode").value = true; yield return null;
            Check(window.Execution.BoosterPlacements.Count == 3 && Selected.All(b => !root.Q<Toggle>("booster-" + b).enabledInHierarchy), "실제 부스터 선택·배치·시작 후 선택 잠금");
            Check(root.Q<Label>("booster-status").text.Contains("대기 없음"), "실제 부스터 배치 상태 표시");
            BoardActionExecutor manual = new BoardActionExecutor(window.CurrentState, Selected);
            BoardCoordinate target = manual.State.Cells.First(c => c.Content == RuntimeContent.Normal).Coordinate;
            string untouched = Snapshot(window.Execution.State);
            Click("item-Hammer"); yield return null;
            Check(!root.Q<Button>("item-Swap").enabledInHierarchy && !root.Q<Button>("item-Shuffle").enabledInHierarchy && !root.Q<Button>("execution-activate").enabledInHierarchy, "아이템 선택 중 다른 아이템·파워 입력 차단");
            Click("item-cancel"); yield return null;
            Check(Snapshot(window.Execution.State) == untouched && window.Execution.ItemUses.Count == 0, "취소는 보드·난수·사용 기록 무변경");
            Click("item-Hammer"); Click("item-Hammer");
            Check(Snapshot(window.Execution.State) == untouched && root.Q<Button>("item-Swap").enabledInHierarchy, "같은 아이템 버튼 재선택 취소");
            Click("item-Hammer"); Click($"initial-cell-{target.Row}-{target.Column}"); yield return null;
            Check(window.Execution.ItemUses.Count == 1 && window.Execution.State.MovesRemaining == 20 && !root.Q<Button>("item-Hammer").enabledInHierarchy, "실제 망치 대상 클릭 즉시 사용·후속 처리 입력 차단");
            manual.UseItem(BoardItem.Hammer, target); Finish(manual);
            Click("execution-cascade"); for (int wait = 0; window.CascadeRunning && wait < 500; wait++) yield return null;
            Check(Snapshot(window.Execution.State) == Snapshot(manual.State) && Snapshot(window.Execution.TurnEffects) == Snapshot(manual.TurnEffects) &&
                Snapshot(window.Execution.CascadeHistory) == Snapshot(manual.CascadeHistory), "아이템 단계·끝까지 실행 상태·난수·문맥·이력 일치");
            Click("item-Hammer");
            BoardCoordinate empty = window.Execution.State.Cells.First(c => c.IsActive && c.Content == RuntimeContent.Empty).Coordinate;
            string rejected = Snapshot(window.Execution.State);
            Click($"initial-cell-{empty.Row}-{empty.Column}");
            Check(Snapshot(window.Execution.State) == rejected && window.Execution.ItemUses.Count == 1 && !root.Q<Button>("item-Swap").enabledInHierarchy && root.Q<Button>("item-cancel").enabledInHierarchy,
                "실제 빈칸 선택은 무소모·아이템 선택 유지");
            Click("item-cancel");
            Click("item-Swap"); Click("initial-cell-8-8"); yield return null;
            Check(root.Q<Button>("initial-cell-8-8").ClassListContains("execution-input") && root.Query<Button>().ToList().Any(b => b.ClassListContains("query-highlight")), "자리 바꾸기 첫 칸과 가능 이웃 강조");
            Click("initial-cell-8-8");
            Check(!root.Q<Button>("initial-cell-8-8").ClassListContains("execution-input") && !root.Q<Button>("item-Hammer").enabledInHierarchy, "첫 칸 재선택은 첫 선택만 취소");
            Click("initial-cell-8-8"); Click("initial-cell-5-5");
            Check(root.Q<Button>("initial-cell-5-5").ClassListContains("execution-input"), "먼 이동 가능 칸은 첫 선택 변경");
            typeof(LevelInitialStateWindow).GetMethod("OnLostFocus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
            Check(root.Q<Button>("item-Hammer").enabledInHierarchy && !root.Q<Button>("initial-cell-5-5").ClassListContains("execution-input"), "창 비활성화 대상 선택 해제");
            Click("item-Swap"); Click("initial-cell-5-5"); Click("initial-cell-5-6");
            Check(window.Execution.ItemUses.Count == 2 && window.Execution.ItemUses.Last().Item == BoardItem.Swap, "실제 두 칸 지정 자리 바꾸기 실행");
            Click("execution-cascade"); for (int wait = 0; window.CascadeRunning && wait < 500; wait++) yield return null;
            Click("item-Shuffle"); yield return null;
            Check(root.Q<VisualElement>("item-shuffle-confirmation").style.display == DisplayStyle.Flex, "실제 섞기 사용 확인 표시");
            string beforeShuffle = Snapshot(window.Execution.State);
            Click("item-shuffle-cancel"); Check(Snapshot(window.Execution.State) == beforeShuffle, "섞기 확인 취소 무소모");
            Click("item-Shuffle"); Click("item-shuffle-confirm"); Click("execution-cascade");
            for (int wait = 0; window.CascadeRunning && wait < 500; wait++) yield return null;
            Check(window.Execution.ItemUses.Count == 3 && window.Execution.State.MovesRemaining == 20, "확인 후 섞기 사용·이동 무소모");
            Check(BoardActionExecutor.Version + PowerEffectResolution.Version + Snapshot(window.Execution.State) + Snapshot(window.Execution.TurnEffects) +
                Snapshot(window.Execution.CascadeHistory) + Snapshot(window.Execution.ItemUses) + Snapshot(window.Execution.BoosterPlacements) + Snapshot(window.Execution.PendingBoosters) == Replay(fixture),
                "세 아이템 실제 UI 결과와 독립 재현 시나리오 일치");
            window.position = new Rect(10, 10, 680, 780); yield return null; Capture("items-narrow.png");
            Check(root.Q<Button>("item-Shuffle").worldBound.xMax <= 680, "680폭 아이템 접근");
            Check(Selected.All(b => root.Q<Toggle>("booster-" + b).Q<Label>().resolvedStyle.height <= 24), "680폭 부스터 이름 한 줄 표시");
            root.Q<ScrollView>("initial-board-scroll").scrollOffset = new Vector2(80, 40); yield return null; Capture("items-scrolled.png");
            Check(JsonUtility.ToJson(fixture) == json && File.ReadAllText(AssetDatabase.GetAssetPath(fixture)) == file && !EditorUtility.IsDirty(fixture) && Snapshot(window.CurrentState) == initial, "원본 JSON·파일·dirty·초기 사본 보존");
            Click("item-Hammer"); Click("execution-reset"); yield return null;
            Check(window.Execution == null && Selected.All(b => !root.Q<Toggle>("booster-" + b).value), "재시작시 아이템 선택·부스터 선택 초기화");
            LevelDefinition waitingLevel = PlayFixture();
            Invoke(typeof(PowerEffectVerification), "Place", waitingLevel, C(9, 8), InitialBlockKind.Rocket, RocketDirection.Vertical, RabbitColor.Type1);
            BoardCoordinate[] coveredCells = Build(waitingLevel).Cells.Where(c => c.Content == RuntimeContent.Normal).Select(c => c.Coordinate).ToArray();
            LevelObstacleEditing.Apply(waitingLevel, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 1 }, coveredCells);
            Check(LevelDefinitionValidator.Validate(waitingLevel).Count == 0 && new StartConditionReport(Build(waitingLevel)).IsSatisfied, "부스터 대기 UI 시험의 유효 교환·시작 조건");
            AssetDatabase.CreateAsset(waitingLevel, folder + "/Waiting.asset"); AssetDatabase.SaveAssets();
            root.Q<ObjectField>("initial-level").value = waitingLevel; Click("initial-build");
            for (int wait = 0; (window.IsSearching || window.CurrentState == null) && wait < 100; wait++) yield return null;
            foreach (StartBooster booster in Selected) root.Q<Toggle>("booster-" + booster).value = true;
            root.Q<Toggle>("execution-mode").value = true; yield return null;
            Check(window.Execution.PendingBoosters.Count == 3 && window.Execution.BoosterPlacements.Count == 0 &&
                root.Q<Label>("booster-status").text.Contains("청소로켓") && root.Q<Label>("booster-status").text.Contains("무지개 자석"), "실제 후보 부족 부스터 세 종류 대기 표시");
            Click("item-Hammer"); Click("initial-cell-0-0"); Click("execution-cascade");
            for (int wait = 0; window.CascadeRunning && wait < 500; wait++) yield return null;
            Check(window.Execution.BoosterPlacements.Count == 1 && window.Execution.PendingBoosters.Count == 2 &&
                root.Q<Label>("booster-status").text.Contains("대기 달 폭탄, 무지개 자석"), "실제 덮개 제거 후 첫 부스터 배치·남은 대기 표시 갱신");
            yield return null; Capture("pending-boosters.png");
            Click("execution-reset"); yield return null;
            root.Q<Toggle>("execution-mode").value = true; Click("item-Swap"); root.Q<ObjectField>("initial-level").value = null; yield return null;
            Check(window.Execution == null && root.Q<Button>("item-cancel").style.display == DisplayStyle.None, "레벨 전환 대상 선택 해제");
            window.Close(); yield return null;
            Check(window == null || !window.CascadeRunning, "창 종료 실행 수명 해제");
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
                Check(Replay(level) == saved.replay, "아이템·부스터·보드·미션·난수·문맥·연쇄 독립 재현");
                if (!saved.folder.StartsWith("Assets/__ItemBoosterVerification_", StringComparison.Ordinal) || saved.folder.Contains("..")) throw new InvalidOperationException("임시 경로 오류");
                Check(AssetDatabase.DeleteAsset(saved.folder), "소유 임시 에셋 정리");
                File.Move(Evidence + "/state.json", Evidence + "/completed-" + DateTime.UtcNow.Ticks + ".json");
                File.WriteAllLines(Evidence + "/restart-results.txt", Results); EditorApplication.Exit(0);
            }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/restart-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}



using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Levels;
using Lobby;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;

namespace LevelAuthoring.Editor
{
    public static partial class LobbyUiVerification
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Action startupProbe;
        private static bool verifyRelease;
        private static bool rejectLevelPack;
        private static bool verifyDataFailure;
        private static bool verifyProgress;
        public static void RunRelease() { verifyRelease = true; RunStartup(); }
        public static void RunDataFailure() { verifyDataFailure = verifyRelease = true; RunStartup(); }
        public static void RunPlayerProgress() { verifyProgress = verifyRelease = true; RunStartup(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallStartupProbe() => startupProbe?.Invoke();
        public static void RunStartup()
        {
            bool enabled = EditorSettings.enterPlayModeOptionsEnabled;
            EnterPlayModeOptions options = EditorSettings.enterPlayModeOptions;
            var startScene = EditorSceneManager.playModeStartScene;
            var reads = new List<string>();
            var originalTransform = Addressables.InternalIdTransformFunc;
            startupProbe = () => Addressables.InternalIdTransformFunc = location =>
            {
                reads.Add(location.InternalId);
                if (rejectLevelPack && location.PrimaryKey == LevelPackCodec.Address(1)) return "Assets/__MissingLobbyStage02Pack.bytes";
                return originalTransform == null ? location.InternalId : originalTransform(location);
            };
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Lobby.unity");
            EditorApplication.playModeStateChanged += Started;
            EditorApplication.EnterPlaymode();

            async void Started(PlayModeStateChange state)
            {
                if (state != PlayModeStateChange.EnteredPlayMode) return;
                EditorApplication.playModeStateChanged -= Started;
                int exit = 1;
                var result = new List<string>();
                var savedPreferences = new Dictionary<string, int?>();
                LevelDefinition tutorialSource = null;
                try
                {
                    double deadline = EditorApplication.timeSinceStartup + 30;
                    LobbyScreen screen = null;
                    while (EditorApplication.timeSinceStartup < deadline)
                    {
                        screen = UnityEngine.Object.FindFirstObjectByType<LobbyScreen>();
                        if (screen != null && screen.Snapshot != null) break;
                        await UniTask.Yield();
                    }
                    Check(screen != null && screen.Snapshot != null && screen.LastReadyMilliseconds > 0, "Lobby ready timeout");
                    await UniTask.DelayFrame(3);
                    Check(UnityEngine.Object.FindObjectsByType<GameScreen.PuzzleGameSession>(FindObjectsSortMode.None).Length == 0, "Game started before selection");
                    string[] dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Lobby.unity", true);
                    Check(!dependencies.Any(path => path.Contains("Atlas") || path.EndsWith(".mp3") || path.EndsWith(".wav") || path.Contains("Assets/Prefabs/Game") || path.Contains("LevelTool")), "Forbidden lobby dependency");
                    Check(reads.Count > 0, "No actual resource reads observed");
                    Check(!reads.Any(path => path.Contains("levels-") || path.Contains("element-content") || path.Contains("Atlas") || path.EndsWith(".wav") || path.EndsWith(".mp3")), "Game data or artwork read by lobby");
                    result.Add("PASS lobby ready ms=" + screen.LastReadyMilliseconds + "; levels=" + screen.Snapshot.Levels.Count);
                    result.Add("PASS no game session or game dependencies before selection");
                    result.AddRange(reads.Select(path => "READ " + path));
                    if (verifyProgress)
                    {
                        tutorialSource = LevelPackCodec.ReadLevel(File.ReadAllBytes("Assets/Data/LevelPacks/levels-000001.bytes"), 1);
                        var keys = new List<string> { "MoonRabbit.Tutorial.Completed.1" };
                        if (!string.IsNullOrEmpty(tutorialSource.Tutorial.completionId)) keys.Add("MoonRabbit.Tutorial.Identity." + tutorialSource.Tutorial.completionId);
                        foreach (int previous in tutorialSource.Tutorial.previousLevelNumbers ?? new List<int>()) keys.Add("MoonRabbit.Tutorial.Completed." + previous);
                        foreach (string key in keys.Distinct()) savedPreferences[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
                        Directory.CreateDirectory("Logs/LobbyStage02");
                        File.WriteAllText("Logs/LobbyStage02/player-progress-backup.json", Newtonsoft.Json.JsonConvert.SerializeObject(savedPreferences));
                        foreach (string key in savedPreferences.Keys) PlayerPrefs.DeleteKey(key);
                        PlayerPrefs.Save();
                    }
                    if (verifyDataFailure)
                    {
                        MemoryBoundary("failure-before");
                        rejectLevelPack = true;
                        screen.GetComponentInChildren<LobbyLevelCell>().GetComponentInChildren<Button>().onClick.Invoke();
                        deadline = EditorApplication.timeSinceStartup + 30;
                        while (EditorApplication.timeSinceStartup < deadline)
                        {
                            var flow = UnityEngine.Object.FindFirstObjectByType<GameScreen.Navigation.LobbyGameFlow>();
                            if (flow != null && !flow.IsBusy) break;
                            await UniTask.Yield();
                        }
                        Check(UnityEngine.Object.FindObjectsByType<GameScreen.PuzzleGameSession>(FindObjectsSortMode.None).Length == 0, "Failed data load retained a game session");
                        Check(Field<Text>(screen, "status").text.Contains("준비하지 못"), "Failed data load lacks retry guidance");
                        Check(UnityEngine.SceneManagement.SceneManager.sceneCount == 1, "Failed data load retained a scene");
                        result.Add("PASS real game pack read failure returns to selectable lobby with no game scene/session");
                        MemoryBoundary("failure-after");
                        rejectLevelPack = false;
                        if (gameMeasurements)
                        {
                            MemoryBoundary("entry-cancel-before");
                            screen.GetComponentInChildren<LobbyLevelCell>().GetComponentInChildren<Button>().onClick.Invoke();
                            Field<Button>(screen, "retry").onClick.Invoke();
                            using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
                            await UniTask.WaitUntil(() => !UnityEngine.Object.FindFirstObjectByType<GameScreen.Navigation.LobbyGameFlow>().IsBusy, cancellationToken: timeout.Token);
                            Check(UnityEngine.Object.FindObjectsByType<GameScreen.PuzzleGameSession>(FindObjectsSortMode.None).Length == 0, "Cancelled entry retained session");
                            MemoryBoundary("entry-cancel-after");
                        }
                    }
                    if (verifyRelease)
                    {
                      for (int round = 0; round < (verifyProgress ? 2 : verifyDataFailure ? 1 : 10); round++)
                      {
                        int selectedIndex = verifyProgress || verifyDataFailure || gameMeasurements && round < 4 ? 0 : round % 3 == 0 ? 0 : round % 3 == 1 ? 1 : 14;
                        var activeList = screen.GetComponentInChildren<LobbyLevelList>();
                        activeList.RestoreScroll(selectedIndex * Field<float>(activeList, "rowHeight"));
                        float savedOffset = screen.ScrollOffset;
                        var cell = activeList.GetComponentsInChildren<LobbyLevelCell>().Single(item => item.BoundIndex == selectedIndex).GetComponentInChildren<Button>();
                        using var measurement = gameMeasurements ? new GameMeasurement(round, selectedIndex + 1, reads) : null;
                        cell.onClick.Invoke();
                        deadline = EditorApplication.timeSinceStartup + 30;
                        GameScreen.PuzzleGameSession game = null;
                        while (EditorApplication.timeSinceStartup < deadline)
                        {
                            game = UnityEngine.Object.FindFirstObjectByType<GameScreen.PuzzleGameSession>();
                            var flow = UnityEngine.Object.FindFirstObjectByType<GameScreen.Navigation.LobbyGameFlow>();
                            if (game != null && game.IsReady && flow != null && !flow.IsBusy) break;
                            await UniTask.Yield();
                        }
                        Check(game != null && game.IsReady, "Game entry failed");
                        Check(game.State.LevelNumber == selectedIndex + 1, "Selected level changed after lobby reconstruction");
                        await UniTask.DelayFrame(5);
                        Check(UnityEngine.Object.FindObjectsByType<LobbyScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                            "Lobby UI retained after successful game entry");
                        Check(UnityEngine.Object.FindObjectsByType<LobbyLevelCell>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                            "Lobby cells retained after successful game entry");
                        result.Add("PASS lobby screen and pooled cells released during game");
                        if (measurement != null)
                        {
                            using var interactionTimeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(20));
                            await UniTask.WaitUntil(() => game.InitialInteractionMilliseconds >= 0, cancellationToken: interactionTimeout.Token);
                            measurement.Ready(game);
                            if (verifyDataFailure) await MeasureSessionBoundaries(game);
                        }
                        if (verifyProgress)
                        {
                            await WaitForStableGame(game);
                            if (round == 0)
                            {
                                Check(game.TutorialState != null && game.TutorialState.State != Tutorial.TutorialProgressState.Completed, "Fresh player tutorial did not start");
                                foreach (var step in Tutorial.TutorialFlowResolver.Resolve(tutorialSource).steps)
                                {
                                    bool accepted = step.kind == Tutorial.TutorialStepKind.Description ? game.TryAdvanceTutorial() :
                                        step.kind == Tutorial.TutorialStepKind.Item ? game.TryUseItem(step.item, step.first) : game.TrySwap(step.first, step.second);
                                    Check(accepted, "Published tutorial action rejected: " + step.kind);
                                    await WaitForStableGame(game);
                                }
                                Check(game.TutorialState != null && game.TutorialState.State == Tutorial.TutorialProgressState.Completed, "Published tutorial did not complete");
                                string completionKey = string.IsNullOrEmpty(tutorialSource.Tutorial.completionId) ? "MoonRabbit.Tutorial.Completed.1" : "MoonRabbit.Tutorial.Identity." + tutorialSource.Tutorial.completionId;
                                Check(PlayerPrefs.GetInt(completionKey, 0) == 1, "Published tutorial completion not persisted");
                                result.Add("PASS actual published tutorial actions completed and persisted");
                            }
                            else
                            {
                                Check(game.TutorialState == null, "Completed player tutorial ran again on reentry");
                                result.Add("PASS recorded tutorial skipped on fresh lobby reentry");
                            }
                        }
                        measurement?.Returning();
                        game.RequestLobby();
                        deadline = EditorApplication.timeSinceStartup + 30;
                        while (EditorApplication.timeSinceStartup < deadline)
                        {
                            screen = UnityEngine.Object.FindFirstObjectByType<LobbyScreen>();
                            var flow = UnityEngine.Object.FindFirstObjectByType<GameScreen.Navigation.LobbyGameFlow>();
                            if (screen != null && screen.Snapshot != null && flow != null && !flow.IsBusy && UnityEngine.Object.FindObjectsByType<GameScreen.PuzzleGameSession>(FindObjectsSortMode.None).Length == 0) break;
                            await UniTask.Yield();
                        }
                        Check(screen != null && screen.Snapshot != null && UnityEngine.Object.FindObjectsByType<GameScreen.PuzzleGameSession>(FindObjectsSortMode.None).Length == 0, "Lobby return failed");
                        Check(Mathf.Abs(screen.ScrollOffset - savedOffset) < 1, "Lobby scroll position not restored");
                        measurement?.LobbyReady(screen);
                        var operationCount = Addressables.ResourceManager.GetType().GetProperty("OperationCacheCount", Private);
                        deadline = EditorApplication.timeSinceStartup + 5;
                        int operations;
                        do
                        {
                            await UniTask.Yield();
                            operations = (int)operationCount.GetValue(Addressables.ResourceManager);
                        } while (operations != 0 && EditorApplication.timeSinceStartup < deadline);
                        Check(operations == 0, "Addressables operations retained after return: " + operations);
                        result.Add("PASS reconstructed lobby and released game round=" + round);
                        result.Add("PASS selected=" + (selectedIndex + 1) + "; scroll=" + savedOffset + "; operation cache=" + operations);
                        measurement?.Finished();
                        if (verifyDataFailure) MemoryBoundary("boundaries-returned-drained");
                      }
                    }
                    exit = 0;
                }
                catch (Exception error) { result.Add("FAIL " + error); }
                finally
                {
                    Directory.CreateDirectory("Logs/LobbyStage02");
                    File.WriteAllLines(gameMeasurements ? Path.Combine(measurementOutput, "result.txt") : "Logs/LobbyStage02/startup.txt", result);
                    foreach (var preference in savedPreferences)
                    {
                        if (preference.Value.HasValue) PlayerPrefs.SetInt(preference.Key, preference.Value.Value);
                        else PlayerPrefs.DeleteKey(preference.Key);
                    }
                    if (savedPreferences.Count > 0)
                    {
                        PlayerPrefs.Save();
                        File.AppendAllText("Logs/LobbyStage02/startup.txt", "PASS original player preference keys restored\n");
                    }
                    if (tutorialSource != null) UnityEngine.Object.Destroy(tutorialSource);
                    Addressables.InternalIdTransformFunc = originalTransform;
                    startupProbe = null;
                    verifyRelease = false;
                    verifyDataFailure = rejectLevelPack = false;
                    verifyProgress = false;
                    gameMeasurements = false;
                    EditorSettings.enterPlayModeOptionsEnabled = enabled;
                    EditorSettings.enterPlayModeOptions = options;
                    EditorSceneManager.playModeStartScene = startScene;
                    EditorApplication.Exit(exit);
                }
            }
        }

        private static async UniTask WaitForStableGame(GameScreen.PuzzleGameSession game)
        {
            using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(35));
            await UniTask.Yield();
            await UniTask.WaitUntil(() => game.HasFailed || game.IsReady && !game.IsStartingFeedback && !game.IsPresenting && !game.HasProgressFeedback &&
                (game.Phase == Simulation.BoardActionPhase.Ready || game.Phase == Simulation.BoardActionPhase.Stopped), cancellationToken: timeout.Token);
            Check(!game.HasFailed, game.Message);
        }

        public static void Run()
        {
            EditorApplication.playModeStateChanged += BeginInPlayMode;
            EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");
            EditorApplication.EnterPlaymode();
        }

        private static async void BeginInPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= BeginInPlayMode;
            string root = "Logs/LobbyStage01/ui-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(root);
            var results = new List<string>();
            Keyboard keyboard = null;
            var originalInputRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
            var originalBackground = InputSystem.settings.backgroundBehavior;
            bool originalRunInBackground = Application.runInBackground;
            int exit = 1;
            try
            {
                Check(EditorSettings.enterPlayModeOptionsEnabled && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0,
                    "This live verification requires the project's existing no-domain-reload Play Mode setting");
                await UniTask.DelayFrame(4);
                var screen = UnityEngine.Object.FindFirstObjectByType<LobbyScreen>();
                double deadline = EditorApplication.timeSinceStartup + 20;
                while (Field<LobbyIndexSnapshot>(screen, "snapshot") == null && EditorApplication.timeSinceStartup < deadline) await UniTask.Yield();
                var original = Field<LobbyIndexSnapshot>(screen, "snapshot");
                Check(original != null, "Actual lobby index failed to load");
                LobbyLevelList list = Field<LobbyLevelList>(screen, "list");
                ScrollRect scroll = list.GetComponent<ScrollRect>();
                float rowHeight = Field<float>(list, "rowHeight");
                foreach (int total in new[] { 0, 1, 15, 1000, 10000 })
                {
                    var entries = Enumerable.Range(0, total).Select(i => new LevelLobbyEntry { LevelId = "fixture-" + i, LevelNumber = i + 1, DisplayName = "긴 한글 이름의 시험 레벨입니다. 버튼 영역에 겹치지 않아야 합니다." }).ToArray();
                    var snapshot = Snapshot(entries);
                    string selected = null;
                    int clicks = 0;
                    list.SetItems(snapshot.Levels, item => { selected = item.Id; clicks++; });
                    await UniTask.DelayFrame(2);
                    int expected = Math.Min(total, Mathf.CeilToInt(scroll.viewport.rect.height / rowHeight) + 4);
                    Check(list.CellCount == expected, "Cell bound " + total);
                    var before = list.GetComponentsInChildren<LobbyLevelCell>().Select(cell => cell.GetInstanceID()).OrderBy(id => id).ToArray();
                    for (int step = 0; step <= 50; step++)
                    {
                        scroll.content.anchoredPosition = new Vector2(0, Mathf.Max(0, total * rowHeight - scroll.viewport.rect.height) * step / 50f);
                        scroll.onValueChanged.Invoke(Vector2.zero);
                    }
                    var after = list.GetComponentsInChildren<LobbyLevelCell>();
                    Check(before.SequenceEqual(after.Select(cell => cell.GetInstanceID()).OrderBy(id => id)), "Cells recreated during scroll");
                    if (total > 0)
                    {
                        var last = after.Single(cell => cell.BoundIndex == total - 1);
                        var button = last.GetComponentInChildren<Button>();
                        button.onClick.Invoke();
                        Check(clicks == 1 && selected == "fixture-" + (total - 1), "Wrong recycled selection");
                        var pointer = new PointerEventData(EventSystem.current)
                        {
                            button = PointerEventData.InputButton.Left,
                            position = RectTransformUtility.WorldToScreenPoint(null, scroll.viewport.position)
                        };
                        pointer.pressPosition = pointer.position;
                        var handler = ExecuteEvents.GetEventHandler<IDragHandler>(button.gameObject);
                        ExecuteEvents.Execute(handler, pointer, ExecuteEvents.initializePotentialDrag);
                        ExecuteEvents.Execute(handler, pointer, ExecuteEvents.beginDragHandler);
                        if (total >= 1000)
                        {
                            float beforeDrag = scroll.content.anchoredPosition.y;
                            pointer.position += new Vector2(0, 100);
                            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.dragHandler);
                            Check(Mathf.Abs(scroll.content.anchoredPosition.y - beforeDrag) > 1, "Drag on row button did not scroll content");
                        }
                        ExecuteEvents.Execute(handler, pointer, ExecuteEvents.endDragHandler);
                        button.onClick.Invoke();
                        Check(clicks == 1, "Drag selected row");
                        await UniTask.DelayFrame(1);
                        ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                        Check(clicks == 2, "Keyboard submit after drag failed");
                        ExecuteEvents.Execute(handler, pointer, ExecuteEvents.initializePotentialDrag);
                        button.onClick.Invoke();
                        Check(clicks == 3, "Click after drag failed");
                    }
                    results.Add($"PASS total={total}, cells={list.CellCount}, stable instances, selected ID and drag suppression");
                }
                Vector2 originalSize = scroll.viewport.sizeDelta;
                foreach (float delta in new[] { 240f, -160f, 0f })
                {
                    scroll.viewport.sizeDelta = originalSize + new Vector2(0, delta);
                    scroll.onValueChanged.Invoke(Vector2.zero);
                    Check(list.CellCount == Mathf.CeilToInt(scroll.viewport.rect.height / rowHeight) + 4, "Scroll event swallowed viewport resize");
                    var bound = list.GetComponentsInChildren<LobbyLevelCell>().Select(cell => cell.BoundIndex).ToArray();
                    Check(bound.Distinct().Count() == list.CellCount, "Resize produced duplicate row bindings");
                    await UniTask.DelayFrame(1);
                }
                results.Add("PASS viewport grow/shrink before list LateUpdate adjusts pool and unique bindings");
                // 기존 입력 검사처럼 실제 PlayerLoop에서 합성 키를 소비하게 한다.
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Application.runInBackground = true;
                keyboard = InputSystem.AddDevice<Keyboard>("LobbyVerificationKeyboard");
                keyboard.MakeCurrent();
                EventSystem.current.SetSelectedGameObject(null);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.End));
                for (int frame = 0; frame < 3; frame++)
                {
                    await UniTask.DelayFrame(1);
                    results.Add($"KEY frame={Time.frameCount} current={Keyboard.current?.deviceId} expected={keyboard.deviceId} pressed={keyboard.endKey.isPressed} edge={keyboard.endKey.wasPressedThisFrame} list={list.isActiveAndEnabled} focus={EventSystem.current.currentSelectedGameObject?.name} first={Field<int>(list, "first")}");
                }
                Check(EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.GetComponentInParent<LobbyLevelCell>().BoundIndex == 9999, "End key did not reach last row");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await UniTask.DelayFrame(2);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Home)); await UniTask.DelayFrame(3);
                Check(EventSystem.current.currentSelectedGameObject.GetComponentInParent<LobbyLevelCell>().BoundIndex == 0, "Home key did not return to first row");
                results.Add("PASS keyboard End/Home focuses correct rows and scrolls to visibility");
                Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Lobby/MoonRabbitLobby-Regular.ttf");
                string required = string.Concat(original.Levels.Select(level => level.Name)) + "달토끼 고물상레벨 목록을 불러오는 중등록된 없습니다플레이할 선택하세요다시 시도데이터 갱신 후 주세요게임 연결은 다음 단계입니다 ·…0123456789";
                string missing = new string(required.Where(ch => !char.IsWhiteSpace(ch) && !font.HasCharacter(ch)).Distinct().ToArray());
                Check(missing.Length == 0, "Missing lobby glyphs: " + missing);
                string[] dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Lobby.unity", true);
                Check(!dependencies.Any(path => path.Contains("Atlas") || path.EndsWith(".mp3") || path.EndsWith(".wav") || path.Contains("Assets/Prefabs/Game") || path.Contains("LevelTool")), "Forbidden lobby dependency");
                results.Add("PASS authored scene dependencies and registered name/UI glyph coverage");
                MethodInfo reload = typeof(LobbyScreen).GetMethod("Reload", Private);
                var manager = Addressables.ResourceManager;
                PropertyInfo cacheCount = manager.GetType().GetProperty("OperationCacheCount", Private);
                int baseline = (int)cacheCount.GetValue(manager);
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    reload.Invoke(screen, null);
                    reload.Invoke(screen, null);
                    screen.enabled = false;
                    string disabledText = Field<Text>(screen, "status").text;
                    await UniTask.DelayFrame(4);
                    Check(Field<Text>(screen, "status").text == disabledText, "Cancelled load updated disabled UI");
                    screen.enabled = true;
                    deadline = EditorApplication.timeSinceStartup + 10;
                    while (Field<LobbyIndexSnapshot>(screen, "snapshot") == null && EditorApplication.timeSinceStartup < deadline) await UniTask.Yield();
                    if (Field<LobbyIndexSnapshot>(screen, "snapshot")?.Levels.Count != original.Levels.Count)
                    {
                        results.Add($"Reload diagnostic attempt={attempt} active={screen.isActiveAndEnabled} status={Field<Text>(screen, "status").text}");
                        try { var probe = await LobbyIndexLoader.LoadAsync(); results.Add("Direct loader count=" + probe.Levels.Count); }
                        catch (Exception error) { results.Add("Direct loader error=" + error); }
                        Check(false, "Reload after cancellation failed");
                    }
                    await UniTask.DelayFrame(2);
                    Check((int)cacheCount.GetValue(manager) <= baseline, "Addressables operations accumulated after reload");
                }
                int events = 0;
                screen.LevelSelected += (id, number, generation) =>
                {
                    Check(id == original.Levels[0].Id && number == original.Levels[0].Number && generation == original.Generation, "Screen selection metadata mismatch");
                    events++;
                };
                list.GetComponentsInChildren<LobbyLevelCell>().Single(cell => cell.BoundIndex == 0).GetComponentInChildren<Button>().onClick.Invoke();
                Check(events == 1, "Selection event duplicated after reload");
                reload.Invoke(screen, null);
                Text remainingStatus = Field<Text>(screen, "status");
                string destroyedText = remainingStatus.text;
                UnityEngine.Object.DestroyImmediate(screen);
                await UniTask.DelayFrame(4);
                deadline = EditorApplication.timeSinceStartup + 10;
                while ((int)cacheCount.GetValue(manager) > baseline && EditorApplication.timeSinceStartup < deadline)
                {
                    Check(remainingStatus.text == destroyedText, "Destroyed screen load changed UI");
                    await UniTask.Yield();
                }
                Check(remainingStatus.text == destroyedText && (int)cacheCount.GetValue(manager) <= baseline, "Destroyed screen load changed UI or retained operations");
                results.Add("PASS repeated reload/cancel/reenable, ID/number/generation selection, destruction and operation cache baseline");
                exit = 0;
            }
            catch (Exception error) { results.Add("FAIL " + error); Debug.LogException(error); }
            finally
            {
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = originalInputRouting;
                InputSystem.settings.backgroundBehavior = originalBackground;
                Application.runInBackground = originalRunInBackground;
                File.WriteAllLines(Path.Combine(root, "result.txt"), results);
                EditorApplication.Exit(exit);
            }
        }
        private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, Private).GetValue(instance);
        private static LobbyIndexSnapshot Snapshot(LevelLobbyEntry[] entries) => (LobbyIndexSnapshot)Activator.CreateInstance(typeof(LobbyIndexSnapshot), Private, null, new object[] { "fixture", new LevelLobbyIndex { Entries = entries } }, null);
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    }
}

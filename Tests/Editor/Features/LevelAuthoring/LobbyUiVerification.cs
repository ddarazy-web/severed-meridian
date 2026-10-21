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
    public static class LobbyUiVerification
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
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

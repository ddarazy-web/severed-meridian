using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using AutoPlay;
using Cysharp.Threading.Tasks;
using GameScreen;
using LevelAuthoring.Editing;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolMobileTrialsVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const string Output = "Logs/AndroidLevelToolStage05";
        private static readonly Vector2Int[] Sizes = { new Vector2Int(360, 780), new Vector2Int(780, 360), new Vector2Int(975, 800), new Vector2Int(1280, 800) };
        private static readonly StringBuilder Evidence = new StringBuilder();
        private static LevelToolScreen screen;
        private static PanelSettings panel;
        private static RenderTexture texture, previousTexture;
        private static PanelScaleMode previousScaleMode;
        private static float previousScale;
        private static bool previousEnabled, started, recordsOnly, gameOnly;
        private static EnterPlayModeOptions previousOptions;
        private static string recoveryFolder, expectedState;
        private static Rect safeBounds;
        private static int exitCode;
        private static CancellationTokenSource lifetime;
        private static VisualElement Root => screen.GetComponent<UIDocument>().rootVisualElement;

        public static void Run() => Begin(false, false);
        public static void RunRecordBack() => Begin(true, false);
        public static void RunGameReturn() => Begin(false, true);

        private static void Begin(bool onlyRecordBack, bool onlyGameReturn)
        {
            recordsOnly = onlyRecordBack; gameOnly = onlyGameReturn; started = false; exitCode = 0; Evidence.Clear();
            Evidence.AppendLine("Editor Play Mode mobile UI pointer/scroll and simulated common Back/pause paths. Physical Android OS Back, touch, rotation and suspension remain unverified.");
            Directory.CreateDirectory(Output);
            recoveryFolder = Path.GetFullPath(Output + "/trials-" + Guid.NewGuid().ToString("N"));
            previousEnabled = EditorSettings.enterPlayModeOptionsEnabled; previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            LevelToolRecoveryIsolation.Configure(recoveryFolder);
            lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Changed;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (lifetime?.IsCancellationRequested == true && !started)
            {
                started = true; exitCode = 1;
                Evidence.AppendLine("FAIL mobile trial screen startup timeout");
                Debug.LogError("FAIL mobile trial screen startup timeout");
                EditorApplication.ExitPlaymode(); return;
            }
            if (started || !EditorApplication.isPlaying) return;
            screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
            if (screen == null || Get<bool>("busy")) return;
            started = true; Exercise().Forget();
        }

        private static async UniTask Exercise()
        {
            try
            {
                LevelToolRecoveryIsolation.Verify(screen, recoveryFolder);
                panel = screen.GetComponent<UIDocument>().panelSettings;
                previousTexture = panel.targetTexture; previousScaleMode = panel.scaleMode; previousScale = panel.scale;
                panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1;
                Check(screen.Workspace.Create(Path.Combine(recoveryFolder, "Project"), LevelToolTestProject.Create()), "isolated project created");
                var session = screen.Workspace.Session;
                string id = session.SelectedLevelId;
                session.Apply("모바일 시험 설정", docs => {
                    docs[id].Data["moveCount"] = 1;
                    docs[id].Data["missions"] = new JArray(new JObject { ["kind"] = "Color", ["color"] = "Type1", ["count"] = 999 });
                    // 시험판의 9×9 표시는 유지하고 계획 탐색 분기만 중앙 3×3으로 제한한다.
                    for (int cell = 0; cell < 81; cell++)
                        docs[id].Data["board"]["cells"][cell]["isActive"] = cell / 9 >= 3 && cell / 9 <= 5 && cell % 9 >= 3 && cell % 9 <= 5;
                    docs[id].Data["elementSupply"]["sources"] = new JArray();
                    SupplyDocumentEditing.PlaceSources(docs[id].Data, new[] { 30, 31, 32 }, false);
                });
                session.Apply("모바일 시험 Redo", docs => docs[id].Data["displayName"] = "모바일 시험 이후 다시 실행");
                session.Undo(); session.SelectCells(new[] { 40, 41 }); Call("Refresh"); expectedState = session.ExportState();
                if (gameOnly) await GameReturn();
                else if (recordsOnly)
                {
                    await Viewport(Sizes[0]); await OpenPage("workspace-records"); await RecordBack();
                }
                else
                {
                    for (int index = 0; index < Sizes.Length; index++)
                    {
                        await Viewport(Sizes[index]); await OpenPage("workspace-test");
                        await Click("bot-new"); await Reveal("bot-run"); PointerClick(Root.Q<Button>("bot-run")); await EarlyClick("bot-stop");
                        await UniTask.WaitUntil(() => !Get<BotPlaySession>("toolBot").NeedsAdvance, cancellationToken: lifetime.Token);
                        Preserved("single bot stop " + Sizes[index]);
                        Root.Q<IntegerField>("batch-count").value = 1;
                        await Reveal("batch-new"); PointerClick(Root.Q<Button>("batch-new"));
                        await UniTask.WaitUntil(() => Get<PersistedBotBatch>("toolBatch").CanControl, cancellationToken: lifetime.Token);
                        await EarlyClick("batch-pause");
                        await UniTask.WaitUntil(() => !Get<PersistedBotBatch>("toolBatch").IsSaving, cancellationToken: lifetime.Token);
                        Check(Get<PersistedBotBatch>("toolBatch").Record.status == BotBatchStatus.Paused, "batch pause checkpoint " + Sizes[index]);
                        Call("OnApplicationPause", true); Call("OnApplicationPause", false); Preserved("paused batch interruption");
                        await Viewport(Sizes[(index + 1) % Sizes.Length]); Preserved("paused batch rotation");
                        await Viewport(Sizes[index]); await Click("batch-resume");
                        await UniTask.WaitUntil(() => !Get<PersistedBotBatch>("toolBatch").HasWork, cancellationToken: lifetime.Token);
                        var batch = Get<PersistedBotBatch>("toolBatch");
                        Check(batch.Record.status == BotBatchStatus.Completed && batch.Games.Count == 2 && batch.PersistenceError == null, "batch complete two strategies " + Sizes[index]);
                        await Reveal("batch-new"); PointerClick(Root.Q<Button>("batch-new"));
                        await UniTask.WaitUntil(() => Get<PersistedBotBatch>("toolBatch").CanControl, cancellationToken: lifetime.Token);
                        await EarlyClick("batch-stop");
                        await UniTask.WaitUntil(() => !Get<PersistedBotBatch>("toolBatch").HasWork, cancellationToken: lifetime.Token);
                        Check(Get<PersistedBotBatch>("toolBatch").PersistenceError == null, "batch stop durably saves without error");
                        await Click("history-refresh");
                        // 스캔 중 목록이 매번 다시 그려지므로 완료 후 화면에 남은 버튼을 터치한다.
                        await UniTask.WaitUntil(() => Get<object>("historyScan") == null, cancellationToken: lifetime.Token);
                        Check(Root.Q<Button>("history-open-" + batch.Record.id) != null, "completed history scan contains selected batch");
                        await Click("history-open-" + batch.Record.id);
                        await UniTask.WaitUntil(() => Get<BotAnalysisReader>("historyReader").IsDone, cancellationToken: lifetime.Token);
                        Check(Get<BotAnalysisReader>("historyReader").Error == null, "saved result validated");
                        await ReadSummary();
                        Check(Root.Q<Label>("history-summary").text.Contains("정상 종료"), "mobile result statistics accessible");
                        await Click("history-next"); await Click("history-previous"); await Click("history-game-0");
                        await Click("history-replay-all"); await Click("history-replay-pause"); await Click("history-replay-close");
                        Check(Get<BotRecordReplay>("historyReplay") == null, "mobile replay closes");
                        Root.Q<IntegerField>("multi-samples").value = 1;
                        Root.Q<Toggle>("multi-level-" + id).value = true;
                        await Reveal("multi-start"); PointerClick(Root.Q<Button>("multi-start"));
                        await UniTask.WaitUntil(() => Get<PersistedMultiLevelTest>("toolMulti").Current != null, cancellationToken: lifetime.Token);
                        await EarlyClick("multi-stop");
                        await UniTask.WaitUntil(() => !Get<PersistedMultiLevelTest>("toolMulti").HasWork, cancellationToken: lifetime.Token);
                        var multi = Get<PersistedMultiLevelTest>("toolMulti");
                        Check(multi.PersistenceError == null && !string.IsNullOrEmpty(multi.Record.entries[0].resultId), "multi stop writes independent result");
                        await Click("multi-result-" + multi.Record.id + "-0");
                        await UniTask.WaitUntil(() => Get<BotAnalysisReader>("historyReader").IsDone, cancellationToken: lifetime.Token);
                        Check(Get<BotAnalysisReader>("historyReader").Error == null, "stopped multi result accessible");
                        Preserved("mobile trial results " + Sizes[index]);
                        await OpenPage("workspace-records"); await RecordBack();
                    }
                    await GameReturn();
                }
                Preserved("final project level selection and complete undo/redo history");
            }
            catch (Exception error) { exitCode = 1; Evidence.AppendLine("FAIL " + error); Debug.LogException(error); }
            finally { EditorApplication.ExitPlaymode(); }
        }

        private static async UniTask RecordBack()
        {
            // 각 실행이 소유한 격리 폴더에만 검사할 기록을 둔다. 삭제 확인은 승인하지 않는다.
            string record = Guid.NewGuid().ToString("N");
            string path = Path.Combine(recoveryFolder, "Trials", record); Directory.CreateDirectory(path);
            string header = Path.Combine(path, "batch.json");
            File.WriteAllText(header, "{\"id\":\"" + record + "\",\"version\":1,\"formatVersion\":1,\"seeds\":[12345],\"startedUtc\":\"2026-10-11T00:00:00Z\"}");
            Root.Q<Foldout>("record-management").value = true;
            await Click("records-count");
            await UniTask.WaitUntil(() => !GetProperty<bool>("RecordsBusy"), cancellationToken: lifetime.Token);
            await Click("records-delete-all");
            await UniTask.WaitUntil(() => Root.Q<Button>("records-cancel-delete") != null, cancellationToken: lifetime.Token);
            await Click("records-cancel-delete"); Check(!GetProperty<bool>("RecordsBusy"), "explicit record cancel unlocks trials");
            await Click("records-delete-all");
            await UniTask.WaitUntil(() => Root.Q<Button>("records-confirm-delete") != null, cancellationToken: lifetime.Token);
            await Reveal("records-confirm-delete"); Call("HandleAndroidBack"); await Layout();
            Check(Root.Q("modal") == null && !GetProperty<bool>("RecordsBusy") && Root.Q("editor").enabledInHierarchy, "record confirmation Back unlocks editor and trials");
            Check(File.Exists(header), "record Back preserves files"); Preserved("record Back preserves selection/history");
            await Click("records-count");
            await UniTask.WaitUntil(() => !GetProperty<bool>("RecordsBusy"), cancellationToken: lifetime.Token);
            Check(Root.Q<Label>("records-management-status").text.Contains("반복"), "record count works again after Back");
        }

        private static async UniTask GameReturn()
        {
            Camera background = screen.GetComponentInChildren<Camera>(true);
            Check(background != null && background.enabled && background.cullingMask == 0,
                "editor has an active background-only camera");
            await Viewport(Sizes[0]); await Click("play");
            await UniTask.WaitUntil(() => screen.TestSession?.HasFailed == true ||
                screen.TestSession != null && screen.TestSession.IsReady && !screen.TestSession.IsPresenting && !screen.TestSession.HasProgressFeedback ||
                !Get<bool>("busy") && Get<GameObject>("playRoot") == null, cancellationToken: lifetime.Token);
            var game = screen.TestSession;
            Check(game != null && game.IsReady && !game.HasFailed, "representative real game ready: " + Root.Q<Label>("status").text);
            Check(!background.enabled && game.BoardCamera.isActiveAndEnabled,
                "game trial renders with its camera without duplicate background rendering");
            string state = screen.Workspace.Session.ExportState();
            Call("HandleAndroidBack"); Call("HandleAndroidBack");
            Get<PuzzlePopupBinding>("playPopups").Host.TryCancelInput(); await Layout();
            Check(screen.TestSession == game && game.IsPaused, "same-frame repeated game Back opens one pause without tool exit");
            await Viewport(Sizes[1]); Preserved("game rotation preserves authoring");
            Call("OnApplicationPause", true); Call("OnApplicationPause", false);
            Check(screen.TestSession == game && screen.Workspace.Session.ExportState() == state, "game interruption retains same test and authoring session");
            Call("HandleAndroidBack"); Call("HandleAndroidBack"); await Layout();
            Check(screen.TestSession == game && !game.IsPaused, "same-frame repeated popup Back resumes existing game once");
            await Reveal("return-to-editor");
            Check(Root.Q<Button>("return-to-editor").worldBound.height >= 48, "return hit target at least 48 UI units");
            await Click("return-to-editor"); await Layout();
            Check(screen.TestSession == null && Root.Q("editor").resolvedStyle.display == DisplayStyle.Flex, "pointer return restores editor");
            Check(background.enabled, "pointer return restores the editor background camera");
            Preserved("actual game pointer return");
        }

        private static async UniTask ReadSummary()
        {
            Label label = Root.Q<Label>("history-summary");
            ScrollView scroll = label.GetFirstAncestorOfType<ScrollView>();
            for (VisualElement parent = label.parent; parent != null; parent = parent.parent)
                if (parent is Foldout foldout) foldout.value = true;
            await Layout();
            float top = scroll.contentContainer.WorldToLocal(label.worldBound.min).y;
            scroll.scrollOffset = new Vector2(0, top); await Layout();
            Rect viewport = scroll.contentViewport.worldBound;
            Check(label.worldBound.yMin >= viewport.yMin - 2 && label.worldBound.yMin < viewport.yMax && label.worldBound.xMin >= viewport.xMin - 2 && label.worldBound.xMax <= viewport.xMax + 2,
                "long result first line visible without horizontal clipping");
            scroll.scrollOffset = new Vector2(0, Mathf.Max(top, top + label.worldBound.height - viewport.height)); await Layout();
            Check(label.worldBound.yMax <= viewport.yMax + 2 && label.worldBound.yMax > viewport.yMin, "long result last line accessible by scrolling");
            Within(scroll.contentViewport, safeBounds, "result scroll viewport safe bounds");
        }

        private static async UniTask Viewport(Vector2Int size)
        {
            if (texture != null) { panel.targetTexture = null; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            texture = new RenderTexture(size.x, size.y, 0); texture.Create(); panel.targetTexture = texture;
            await UniTask.WaitUntil(() => Mathf.Abs(Root.layout.width - size.x) < 2 && Mathf.Abs(Root.layout.height - size.y) < 2, cancellationToken: lifetime.Token);
            Rect safe = new Rect(12, 18, size.x - 36, size.y - 42);
            safeBounds = new Rect(safe.xMin, size.y - safe.yMax, safe.width, safe.height);
            Call("ApplyMobileViewport", new Vector2(size.x, size.y), safe, default(Rect)); await Layout();
            Check(Root.ClassListContains(safe.width >= 940 ? "mobile-wide" : safe.height >= safe.width ? "mobile-portrait" : "mobile-landscape"), "safe usable viewport layout " + size);
        }

        private static async UniTask OpenPage(string name)
        {
            if (Get<string>("mobilePanel") != "more") await Click("mobile-more");
            await Click(name); if (Get<string>("mobilePanel") != "properties") await Click("mobile-properties");
        }

        private static async UniTask Click(string name) { await Reveal(name); PointerClick(Root.Q<Button>(name)); await UniTask.Yield(); }

        private static async UniTask EarlyClick(string name)
        {
            Button button = Root.Q<Button>(name);
            button.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(button);
            // 짧은 시험이 끝나기 전에 조작하고 스크롤 기하 갱신에 필요한 한 프레임만 기다린다.
            await UniTask.Yield(); Within(button, safeBounds, name);
            Within(button, button.GetFirstAncestorOfType<ScrollView>().contentViewport.worldBound, "early scroll viewport " + name);
            PointerClick(button);
        }

        private static async UniTask Reveal(string name)
        {
            VisualElement element = Root.Q(name) ?? throw new Exception("Missing mobile trial control " + name);
            // 접기와 스크롤은 실제 화면의 기존 컨테이너를 통해 처리한다.
            for (VisualElement parent = element.parent; parent != null; parent = parent.parent)
                if (parent is Foldout foldout) foldout.value = true;
            await Layout();
            ScrollView scroll = element.GetFirstAncestorOfType<ScrollView>();
            scroll?.ScrollTo(element); await Layout();
            Within(element, safeBounds, name);
            if (scroll != null) Within(element, scroll.contentViewport.worldBound, "scroll viewport " + name);
        }

        private static void PointerClick(Button button)
        {
            Check(button != null && button.enabledInHierarchy, "pointer enabled " + button?.name);
            Event source = new Event { type = EventType.MouseDown, button = 0, mousePosition = button.worldBound.center };
            using (PointerDownEvent evt = PointerDownEvent.GetPooled(source))
            {
                // 플랫폼 입력 백엔드만 대체하며 실제 터치 버튼 콜백을 통과한다.
                typeof(PointerDownEvent).GetProperty("pointerType", Flags).SetValue(evt, UnityEngine.UIElements.PointerType.touch);
                typeof(PointerDownEvent).GetProperty("pointerId", Flags).SetValue(evt, 1);
                typeof(PointerDownEvent).GetProperty("isPrimary", Flags).SetValue(evt, true);
                evt.target = button; button.SendEvent(evt);
            }
            source.type = EventType.MouseUp;
            using (PointerUpEvent evt = PointerUpEvent.GetPooled(source))
            {
                typeof(PointerUpEvent).GetProperty("pointerType", Flags).SetValue(evt, UnityEngine.UIElements.PointerType.touch);
                typeof(PointerUpEvent).GetProperty("pointerId", Flags).SetValue(evt, 1);
                typeof(PointerUpEvent).GetProperty("isPrimary", Flags).SetValue(evt, true);
                evt.target = button; button.SendEvent(evt);
            }
        }

        private static async UniTask Layout() => await UniTask.Delay(150, ignoreTimeScale: true, cancellationToken: lifetime.Token);
        private static T Get<T>(string name) => (T)typeof(LevelToolScreen).GetField(name, Flags).GetValue(screen);
        private static T GetProperty<T>(string name) => (T)typeof(LevelToolScreen).GetProperty(name, Flags).GetValue(screen);
        private static object Call(string name, params object[] arguments) => typeof(LevelToolScreen).GetMethod(name, Flags).Invoke(screen, arguments);
        private static void Preserved(string context) => Check(screen.Workspace.Session.ExportState() == expectedState, context);
        private static void Within(VisualElement element, Rect bounds, string name)
        {
            Rect rect = element.worldBound;
            Check(element.resolvedStyle.display != DisplayStyle.None && rect.width > 0 && rect.height > 0 && !float.IsNaN(rect.width) && rect.xMin >= bounds.xMin - 2 && rect.yMin >= bounds.yMin - 2 && rect.xMax <= bounds.xMax + 2 && rect.yMax <= bounds.yMax + 2, "visible " + name + " " + rect);
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("FAIL " + message);
            Evidence.AppendLine("PASS " + message); Debug.Log("PASS " + message);
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Changed;
            if (panel != null) { panel.targetTexture = previousTexture; panel.scaleMode = previousScaleMode; panel.scale = previousScale; }
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); texture = null; }
            lifetime?.Dispose(); lifetime = null;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnabled; EditorSettings.enterPlayModeOptions = previousOptions;
            File.WriteAllText(Output + (recordsOnly ? "/record-back-verification.txt" : gameOnly ? "/game-return-verification.txt" : "/mobile-trials-verification.txt"), Evidence.ToString());
            EditorApplication.Exit(exitCode);
        }
    }
}

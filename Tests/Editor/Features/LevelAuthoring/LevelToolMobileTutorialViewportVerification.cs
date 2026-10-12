using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameScreen;
using LevelAuthoring.Runtime;
using LevelTool;
using Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolMobileTutorialViewportVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string Output = "Logs/AndroidLevelToolStage05/mobile-tutorial-viewport-verification.txt";
        private static readonly StringBuilder Evidence = new StringBuilder();
        private static LevelToolScreen screen;
        private static CancellationTokenSource lifetime;
        private static EnterPlayModeOptions previousOptions;
        private static bool previousEnabled, started, restored;
        private static int exit;
        private static string folder;
        private static Type renderVerification;
        private static PanelSettings panel;
        private static PanelScaleMode previousScaleMode;
        private static float previousScale;
        private static VisualElement Root => screen.GetComponent<UIDocument>().rootVisualElement;

        public static void Run()
        {
            Evidence.Clear(); started = restored = false; exit = 0;
            Evidence.AppendLine("Editor actual Game View Screen dimensions, UGUI layout/raycast and replayed pointer backend. Physical Android touch, OS rotation, Safe Area and suspension remain unverified.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            folder = Path.GetFullPath("Logs/AndroidLevelToolStage05/tutorial-viewport-" + Guid.NewGuid().ToString("N"));
            previousEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
            renderVerification = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("GameScreen.Editor.PuzzleUIRenderVerification")).FirstOrDefault(type => type != null)
                ?? throw new InvalidOperationException("Existing PuzzleUIRenderVerification must be linked for Game View restoration.");
            try
            {
                Render("RememberSize");
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
                EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
                LevelToolRecoveryIsolation.Configure(Path.Combine(folder, "Recovery"));
                lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(4));
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += Changed;
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error) { Fail(error); Changed(PlayModeStateChange.EnteredEditMode); }
        }

        private static void Tick()
        {
            if (started) return;
            if (lifetime.IsCancellationRequested)
            {
                started = true; Fail(new TimeoutException("Tutorial viewport startup timeout"));
                EditorApplication.ExitPlaymode(); return;
            }
            if (!EditorApplication.isPlaying) return;
            screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
            if (screen == null || (bool)Get("busy")) return;
            started = true; Exercise().Forget();
        }

        private static async UniTask Exercise()
        {
            try
            {
                LevelToolRecoveryIsolation.Verify(screen, Path.Combine(folder, "Recovery"));
                panel = screen.GetComponent<UIDocument>().panelSettings;
                Check(panel.targetTexture == null, "tool and UGUI use actual Game View rather than UITK texture");
                previousScaleMode = panel.scaleMode; previousScale = panel.scale;
                panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1;
                Check(screen.Workspace.Create(Path.Combine(folder, "Project"), LevelToolDocuments.NewProject()), "isolated tutorial project");
                var parent = screen.Workspace.Session;
                // 기존 SharedDraftGame과 같은 공통 샘플/공유 초안 준비를 재사용한다.
                TutorialDraftEditing.AddSampleBoard(parent, "two");
                string flow = TutorialDraftEditing.CreateFlow(parent, "모바일 화면 두 행동");
                var draft = new SharedTutorialDraft(parent);
                TutorialDraftEditing.EditFlow(draft.Session, flow, "viewport instructions", value => value.steps[0].instructions = "화면 좌표로 두 번 교환");
                TutorialDraftEditing.EditFlow(draft.Session, flow, "viewport redo", value => value.steps[0].instructions = "복귀 후 Redo");
                draft.Session.Undo(); parent.SelectCells(new[] { 40, 41 });
                Set("sharedTutorialDraft", draft); Set("inspectorPage", "튜토리얼"); Call("Refresh");
                string parentBefore = parent.ExportState(), draftBefore = draft.ExportState();
                string progress = "MoonRabbit.Tutorial.Completed." + parent.Get(parent.SelectedLevelId).Data["levelNumber"];
                string identity = "MoonRabbit.Tutorial.Identity." + parent.Get(parent.SelectedLevelId).Data["tutorial"]["completionId"];
                bool hadProgress = PlayerPrefs.HasKey(progress), hadIdentity = PlayerPrefs.HasKey(identity);
                int progressValue = PlayerPrefs.GetInt(progress), identityValue = PlayerPrefs.GetInt(identity);
                foreach (Vector2Int size in new[] { new Vector2Int(360, 780), new Vector2Int(780, 360) })
                {
                    Render("SetSize", size.x, size.y);
                    await UniTask.WaitUntil(() => Screen.width == size.x && Screen.height == size.y &&
                        Mathf.Abs(Root.layout.width - size.x) < 2 && Mathf.Abs(Root.layout.height - size.y) < 2, cancellationToken: lifetime.Token);
                    Call("ApplyMobileViewport", new Vector2(size.x, size.y), new Rect(0, 0, size.x, size.y), default(Rect));
                    Check(Screen.width == size.x && Screen.height == size.y, "actual Screen viewport " + size);
                    await screen.BeginPlay();
                    PuzzleGameSession game = screen.TestSession;
                    Check(game != null, "shared draft tutorial game starts " + size);
                    await Stable(game);
                    Check(game.TutorialState?.Instructions == "화면 좌표로 두 번 교환", "unapplied draft instructions used " + size);
                    var input = game.GetComponent<PuzzleBoardInput>();
                    TutorialOverlayView overlay = UnityEngine.Object.FindFirstObjectByType<TutorialOverlayView>();
                    Check(input != null && overlay != null, "real board input and tutorial overlay linked");
                    int actions = 0;
                    while (game.TutorialState.State != TutorialProgressState.Completed && actions < 3)
                    {
                        await UniTask.Yield(); await UniTask.Yield(); Canvas.ForceUpdateCanvases();
                        var guidance = game.TutorialState;
                        Check(guidance.First.HasValue && guidance.Second.HasValue && overlay.Content.gameObject.activeInHierarchy, "visible actionable tutorial guidance");
                        Vector2 first = game.BoardCamera.WorldToScreenPoint(game.TutorialCellWorldPosition(guidance.First.Value));
                        Vector2 second = game.BoardCamera.WorldToScreenPoint(game.TutorialCellWorldPosition(guidance.Second.Value));
                        Check(game.BoardCamera.pixelRect.Contains(first) && game.BoardCamera.pixelRect.Contains(second) &&
                            input.TryGetCoordinate(first, out var a) && a.Equals(guidance.First.Value) &&
                            input.TryGetCoordinate(second, out var b) && b.Equals(guidance.Second.Value), "tutorial screen coordinates round-trip inside actual camera viewport");
                        Rect bubble = ScreenRect(overlay.Bubble);
                        Check(bubble.xMin >= -2 && bubble.yMin >= -2 && bubble.xMax <= Screen.width + 2 && bubble.yMax <= Screen.height + 2 &&
                            !bubble.Contains(first) && !bubble.Contains(second), "tutorial bubble visible without covering action centers " + size);
                        // 물리 입력 백엔드만 재생한다. 제품 좌표 변환, UGUI 차단, 스와프 판정은 그대로 통과한다.
                        typeof(PuzzleBoardInput).GetMethod("BeginPointer", Flags).Invoke(input, new object[] { 7, first });
                        typeof(PuzzleBoardInput).GetMethod("UpdatePointer", Flags).Invoke(input, new object[] { 7, second });
                        typeof(PuzzleBoardInput).GetMethod("EndPointer", Flags).Invoke(input, new object[] { 7, second });
                        actions++; await Stable(game);
                    }
                    Check(actions == 2 && game.TutorialState.State == TutorialProgressState.Completed, "two replayed screen swipes complete shared fixed-supply tutorial " + size);
                    Button back = Root.Q<Button>("return-to-editor");
                    Check(back != null && back.enabledInHierarchy && back.worldBound.height >= 48 && Root.worldBound.Contains(back.worldBound.center), "mobile pointer return control reachable " + size);
                    PointerClick(back); await UniTask.Yield(); await UniTask.Yield();
                    Check(screen.TestSession == null && ReferenceEquals(parent, screen.Workspace.Session) && parent.ExportState() == parentBefore && draft.ExportState() == draftBefore,
                        "pointer return preserves parent/draft selection and history " + size);
                    Check(PlayerPrefs.HasKey(progress) == hadProgress && PlayerPrefs.GetInt(progress) == progressValue &&
                        PlayerPrefs.HasKey(identity) == hadIdentity && PlayerPrefs.GetInt(identity) == identityValue, "tutorial replay leaves official progress unchanged");
                    draft.Session.Redo(); Check((string)draft.Session.Get(flow).Data["steps"][0]["instructions"] == "복귀 후 Redo", "shared draft redo survives mobile game return");
                    draft.Session.Undo(); Check(draft.ExportState() == draftBefore, "shared draft undo restores complete state");
                }
                Check(parent.ExportState() == parentBefore, "authoring source remains unchanged across both real Screen viewports");
            }
            catch (Exception error) { Fail(error); }
            finally
            {
                if (screen != null) screen.ReturnToEditor();
                RestoreGameView();
                EditorApplication.ExitPlaymode();
            }
        }

        private static async UniTask Stable(PuzzleGameSession game)
        {
            await UniTask.Yield();
            await UniTask.WaitUntil(() => game.HasFailed || game.IsReady && !game.IsPresenting && !game.HasProgressFeedback &&
                (game.Phase == Simulation.BoardActionPhase.Ready || game.Phase == Simulation.BoardActionPhase.Stopped), cancellationToken: lifetime.Token);
            Check(!game.HasFailed, "tutorial game stable: " + game.Message);
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 low = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 high = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
        }

        private static void PointerClick(Button button)
        {
            foreach (EventType type in new[] { EventType.MouseDown, EventType.MouseUp })
            {
                var source = new Event { type = type, button = 0, mousePosition = button.worldBound.center };
                using EventBase evt = type == EventType.MouseDown ? (EventBase)PointerDownEvent.GetPooled(source) : PointerUpEvent.GetPooled(source);
                evt.GetType().GetProperty("pointerType", Flags).SetValue(evt, UnityEngine.UIElements.PointerType.touch);
                evt.GetType().GetProperty("pointerId", Flags).SetValue(evt, 1);
                evt.GetType().GetProperty("isPrimary", Flags).SetValue(evt, true);
                evt.target = button; button.SendEvent(evt);
            }
        }

        private static object Get(string name) => typeof(LevelToolScreen).GetField(name, Flags).GetValue(screen);
        private static void Set(string name, object value) => typeof(LevelToolScreen).GetField(name, Flags).SetValue(screen, value);
        private static object Call(string name, params object[] args) => typeof(LevelToolScreen).GetMethod(name, Flags).Invoke(screen, args);
        private static void Render(string name, params object[] args) => renderVerification.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Evidence.AppendLine("PASS " + message); Debug.Log("PASS " + message); }
        private static void Fail(Exception error) { exit = 1; Evidence.AppendLine("FAIL " + error); Debug.LogException(error); }
        private static void RestoreGameView()
        {
            if (restored) return;
            restored = true;
            try { Render("RestoreSize"); Evidence.AppendLine("PASS original Game View sizes and selection restored"); }
            catch (Exception error) { Fail(error); }
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Changed;
            RestoreGameView(); lifetime?.Dispose(); lifetime = null;
            if (panel != null) { panel.scaleMode = previousScaleMode; panel.scale = previousScale; }
            EditorSettings.enterPlayModeOptionsEnabled = previousEnabled; EditorSettings.enterPlayModeOptions = previousOptions;
            File.WriteAllText(Output, Evidence.ToString()); EditorApplication.Exit(exit);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using LevelAuthoring.Runtime;
using Tutorial;
using Newtonsoft.Json.Linq;
using LevelTool;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolSaveUxVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static LevelToolScreen screen;
        private static string folder, projectFile;
        private static byte[] original;
        private static bool priorEnabled;
        private static EnterPlayModeOptions priorOptions;
        private static int phase;
        private static double deadline;
        private static PanelSettings panel;
        private static RenderTexture texture, previousTexture;
        private static PanelScaleMode previousScaleMode;
        private static float previousScale;
        private static double nextFrame;
        private static int viewport;
        private static readonly Vector2Int[] Sizes = { new Vector2Int(1280, 800), new Vector2Int(360, 780), new Vector2Int(412, 915), new Vector2Int(800, 1280) };
        private static VisualElement Root => screen.GetComponent<UIDocument>().rootVisualElement;

        public static void Run()
        {
            folder = Path.GetFullPath("Logs/LevelToolManualSave/ux-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            priorEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            priorOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            LevelToolRecoveryIsolation.Configure(folder);
            phase = 0; deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Save UX timeout");
                if (!EditorApplication.isPlaying) return;
                screen ??= UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                if (screen == null || (bool)Get("busy")) return;
                if (phase >= 4 && phase < 20) { RenderCheck(); return; }
                if (phase == 0)
                {
                    LevelToolRecoveryIsolation.Verify(screen, folder);
                    screen.Workspace.Create(Path.Combine(folder, "긴 한글 저장 위치 확인용 제작 데이터 프로젝트"), LevelToolTestProject.Create());
                    projectFile = Path.Combine(screen.Workspace.Folder, "project.json");
                    original = File.ReadAllBytes(projectFile);
                    screen.Workspace.Session.EditLevel("UX 검사", level => LevelDocumentEditing.SetName(level, "저장 상태 검사"));
                    Call("Refresh");
                    File.AppendAllText(projectFile, "\n");
                    Command("Save"); phase = 1; return;
                }
                if (phase == 1)
                {
                    Check(screen.Workspace.Session.IsDirty, "failed save retains dirty document");
                    Check(Root.Q<Label>("workspace-status").text.Contains("저장 실패"), "save failure is visible in persistent status");
                    Call("Refresh"); Call("UpdateManualSaveStatus");
                    Check(Root.Q<Label>("workspace-status").text.Contains("저장 실패"), "Refresh does not erase save failure");
                    Check(Root.Q<Button>("save-status-details") != null, "touch-accessible save details entry exists");
                    Click("save-status-details");
                    Check(!string.IsNullOrEmpty(Root.Q<Label>("save-details-error")?.text), "save details expose complete failure reason");
                    Check(Root.Q<Label>("save-details-path").text.Contains(screen.Workspace.Folder), "save details expose project path");
                    Click("close-save-details");
                    Check(Root.Q<Button>("save").text.Contains("다시 저장"), "failed save offers retry");
                    Call("RefreshCommandStates");
                    Check(Root.Q<Button>("save").tooltip.Contains("저장 실패 원인"), "command refresh preserves save error tooltip");
                    File.WriteAllBytes(projectFile, original);
                    Command("Save");
                    Check(Root.Q<Label>("workspace-status").text.Contains("저장 중"), "retry shows saving before completion");
                    Check(!Root.Q<Button>("save").enabledInHierarchy, "saving disables duplicate request");
                    phase = 2; return;
                }
                if (phase == 2)
                {
                    Check(!screen.Workspace.Session.IsDirty, "successful retry marks clean");
                    Check(Root.Q<Label>("workspace-status").text.Contains("저장됨") && !Root.Q<Label>("workspace-status").text.Contains("실패"), "successful retry clears failure state");
                    Check(Root.Q<Button>("save").text == "저장", "save label returns to normal");
                    Check(!File.Exists(Path.Combine(folder, "draft.json")), "save status never adds automatic recovery");
                    new ToolDraftStore(folder).Save(screen.Workspace.ExportState(), new JObject(), 1);
                    screen.Workspace.Session.EditLevel("확인 문구 검사", level => LevelDocumentEditing.SetName(level, "저장하지 않은 변경"));
                    Call("Refresh"); Command("Reload");
                    Check(Root.Q<Button>("save-and-continue").text == "저장 후 다시 읽기", "reload confirmation names actual operation");
                    Click("cancel");
                    Check(screen.Workspace.Session.IsDirty, "reload cancel keeps dirty data");
                    Call("WithDirtyFor", "프로젝트 열기", new Func<string, bool>(_ => throw new IOException("선택한 프로젝트를 읽을 수 없습니다. 현재 작업은 유지됩니다.")));
                    Click("save-and-continue"); phase = 20; return;
                }
                if (phase == 20)
                {
                    Call("Refresh");
                    Check(Root.Q<Label>("workspace-status").text.Contains("파일 작업 실패"), "transition failure persists without pretending JSON save failed");
                    Check(screen.Workspace.Session.IsDirty, "failed transition preserves unsaved session");
                    Click("save-status-details");
                    Check(Root.Q<Label>("save-details-file-error").text.Contains("프로젝트를 읽을 수 없습니다"), "transition details retain actual failure");
                    Click("close-save-details");
                    Command("LegacyRecovery"); phase = 3; return;
                }
                Click("restore-draft");
                Check(Root.Q<Button>("save-and-leave").text == "저장 후 복원", "legacy restore confirmation does not say exit");
                Click("cancel");
                Check(screen.Workspace.Session.IsDirty, "restore cancel keeps current data");
                TutorialDraftEditing.EditLevel(screen.Workspace.Session, "화면 검사 준비", level =>
                    level.Tutorial.steps.Add(new TutorialStepDefinition { authoringId = "ux-step", kind = TutorialStepKind.Swap, instructions = "원본 안내" }));
                TutorialDraftEditing.CreateFlow(screen.Workspace.Session, "공유 사본 안내 검사");
                panel = screen.GetComponent<UIDocument>().panelSettings;
                previousTexture = panel.targetTexture; previousScale = panel.scale; previousScaleMode = panel.scaleMode;
                panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1;
                viewport = 0; phase = 4;
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        private static void RenderCheck()
        {
            Vector2Int size = Sizes[viewport];
            if (phase == 4)
            {
                texture = new RenderTexture(size.x, size.y, 0); texture.Create(); panel.targetTexture = texture;
                nextFrame = EditorApplication.timeSinceStartup + .6; phase = 5; return;
            }
            if (EditorApplication.timeSinceStartup < nextFrame) return;
            if (phase == 5)
            {
                if (Mathf.Abs(Root.layout.width - size.x) > 2 || Mathf.Abs(Root.layout.height - size.y) > 2) return;
                if (viewport > 0) Call("ApplyMobileViewport", new Vector2(size.x, size.y), new Rect(0, 0, size.x, size.y), default(Rect));
                typeof(LevelToolScreen).GetField("manualSaveFailure", Flags).SetValue(screen,
                    "저장할 수 없습니다. 다른 프로그램에서 제작 JSON을 변경했습니다. 현재 편집 내용을 유지한 채 원본 변경 여부를 확인하고 다시 저장하세요. 긴 한글 오류 안내가 모든 화면에서 읽히는지 확인합니다.");
                Call("UpdateManualSaveStatus");
                nextFrame = EditorApplication.timeSinceStartup + .6; phase = 6; return;
            }
            if (phase == 6)
            {
                var button = Root.Q<Button>("save-status-details");
                Rect bounds = button.worldBound;
                Check(bounds.xMin >= 0 && bounds.xMax <= size.x + 1 && bounds.yMin >= 0 && bounds.yMax <= size.y + 1, "save details visible " + size);
                if (viewport > 0) Check(bounds.height >= 44, "save details touch height " + size);
                Capture("status-" + size.x);
                Click("save-status-details");
                nextFrame = EditorApplication.timeSinceStartup + .6; phase = 7; return;
            }
            if (phase == 7)
            {
                var dialog = Root.Q(className: "dialog");
                Check(dialog.worldBound.xMin >= -1 && dialog.worldBound.xMax <= size.x + 1 && dialog.worldBound.yMin >= -1 && dialog.worldBound.yMax <= size.y + 1, "save dialog fits " + size);
                Capture("details-" + size.x);
                Click("close-save-details");
                typeof(LevelToolScreen).GetField("manualSaveFailure", Flags).SetValue(screen, null);
                typeof(LevelToolScreen).GetField("manualFileFailure", Flags).SetValue(screen, null);
                var draft = new SharedTutorialDraft(screen.Workspace.Session);
                TutorialDraftEditing.EditFlowSteps(draft.Session, draft.FlowId, "미적용 사본", level => level.Tutorial.steps[0].instructions = "아직 적용하거나 저장하지 않은 안내");
                typeof(LevelToolScreen).GetField("sharedTutorialDraft", Flags).SetValue(screen, draft);
                Call("UpdateManualSaveStatus"); Click("save-status-details");
                nextFrame = EditorApplication.timeSinceStartup + .6; phase = 8; return;
            }
            if (phase == 8)
            {
                Check(Root.Q<Label>("save-details-policy").text.Contains("원본에 적용"), "shared policy explains required action " + size);
                Check(Root.Query<Label>().ToList().Any(label => label.text.Contains("원본 적용만으로는 파일에 저장되지 않습니다")), "shared apply and save are distinguished " + size);
                Capture("shared-details-" + size.x); Click("close-save-details");
                screen.RequestLeave(() => throw new Exception("Unexpected leave during render check"));
                nextFrame = EditorApplication.timeSinceStartup + .6; phase = 9; return;
            }
            if (phase == 9)
            {
                Check(Root.Q<Button>("apply-flow-and-leave") != null, "shared leave offers apply choice " + size);
                Capture("shared-leave-" + size.x); Click("cancel");
                typeof(LevelToolScreen).GetField("sharedTutorialDraft", Flags).SetValue(screen, null);
                // 복구 읽기 자체는 별도 회귀에서 검사하며 여기서는 안내 상태의 렌더링만 구성한다.
                typeof(AuthoringToolWorkspace).GetProperty("IsRecovery").SetValue(screen.Workspace, true);
                Call("UpdateManualSaveStatus"); Click("save-status-details");
                nextFrame = EditorApplication.timeSinceStartup + .6; phase = 10; return;
            }
            if (phase == 10)
            {
                Check(Root.Q<Label>("save-details-policy").text.Contains("사본 저장"), "recovery policy does not direct to disabled save " + size);
                Check(Root.Query<Label>().ToList().Any(label => label.text.Contains("기존 원본과 복구 기록은 유지됩니다")), "recovery copy guidance " + size);
                Capture("recovery-details-" + size.x); Click("close-save-details");
                typeof(AuthoringToolWorkspace).GetProperty("IsRecovery").SetValue(screen.Workspace, false);
                Call("UpdateManualSaveStatus");
                Command("Reload"); nextFrame = EditorApplication.timeSinceStartup + .6; phase = 11; return;
            }
            if (phase == 11)
            {
                Capture("reload-confirm-" + size.x); Click("cancel");
                panel.targetTexture = null; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); texture = null;
                if (++viewport < Sizes.Length) { phase = 4; return; }
                Debug.Log("PASS manual save UX and rendered viewports: " + folder); Finish(0);
            }
        }

        private static void Capture(string name)
        {
            RenderTexture prior = RenderTexture.active;
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = prior; UnityEngine.Object.DestroyImmediate(pixels); }
        }

        private static object Get(string name) => typeof(LevelToolScreen).GetField(name, Flags).GetValue(screen);
        private static void Call(string name, params object[] arguments) => typeof(LevelToolScreen).GetMethod(name, Flags).Invoke(screen, arguments);
        private static void Command(string name) => Call("TryExecuteCommand", Enum.Parse(typeof(LevelToolScreen).GetNestedType("ToolCommandId", BindingFlags.NonPublic), name));
        private static void Click(string name)
        {
            var button = Root.Q<Button>(name) ?? throw new Exception("Missing button " + name);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
        private static void Check(bool value, string label) { if (!value) throw new Exception("FAIL " + label); Debug.Log("PASS " + label); }
        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            if (panel != null) { panel.targetTexture = previousTexture; panel.scale = previousScale; panel.scaleMode = previousScaleMode; }
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            if (screen != null) typeof(LevelToolScreen).GetField("busy", Flags).SetValue(screen, true);
            EditorSettings.enterPlayModeOptionsEnabled = priorEnabled; EditorSettings.enterPlayModeOptions = priorOptions;
            EditorApplication.Exit(code);
        }
    }
}

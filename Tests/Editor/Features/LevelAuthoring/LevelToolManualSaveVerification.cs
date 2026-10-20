using System;
using System.IO;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using Tutorial;
using LevelTool;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolManualSaveVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static string folder;
        private static LevelToolScreen screen;
        private static bool previousEnabled;
        private static EnterPlayModeOptions previousOptions;
        private static int phase;
        private static double next, deadline;
        private static bool left;
        private static IntegerField invalidInput;
        private static bool pendingTransition;
        public static void RunPendingTransition() { pendingTransition = true; Run(); }
        private static bool sharedPending;
        private static string sharedFlowId;
        public static void RunSharedPending() { sharedPending = true; Run(); }
        private static bool existingRecovery;
        public static void RunExistingRecovery() { existingRecovery = true; Run(); }

        public static void Run()
        {
            folder = Path.GetFullPath("Logs/LevelToolManualSave/check-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            if (existingRecovery)
            {
                File.WriteAllText(Path.Combine(folder, "draft.json"), "invalid legacy fixture");
                File.WriteAllText(Path.Combine(folder, "draft.json.previous"), "previous legacy fixture");
            }
            previousEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
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
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Manual-save timeout");
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
                if (phase >= 200) { TickSharedPending(); return; }
                if (phase >= 100) { TickPendingTransition(); return; }
                if (phase == 0)
                {
                    screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                    if (screen == null || (bool)Get("busy")) return;
                    LevelToolRecoveryIsolation.Verify(screen, folder);
                    Check(Get("modal") == null && screen.Workspace.Session == null, "startup does not automatically read recovery files");
                    screen.Workspace.Create(Path.Combine(folder, "project"), LevelToolDocuments.NewProject());
                    if (sharedPending)
                    {
                        TutorialDraftEditing.EditLevel(screen.Workspace.Session, "준비", level =>
                            level.Tutorial.steps.Add(new TutorialStepDefinition { authoringId = "step", kind = TutorialStepKind.Swap, instructions = "원본" }));
                        sharedFlowId = TutorialDraftEditing.CreateFlow(screen.Workspace.Session, "공유 검사");
                        screen.Workspace.Save();
                        typeof(LevelToolScreen).GetField("sharedTutorialDraft", Flags).SetValue(screen, new SharedTutorialDraft(screen.Workspace.Session));
                        typeof(LevelToolScreen).GetField("inspectorPage", Flags).SetValue(screen, "튜토리얼");
                        Call("Refresh"); phase = 200; next = EditorApplication.timeSinceStartup + 0.3; return;
                    }
                    if (pendingTransition)
                    {
                        typeof(LevelToolScreen).GetField("levelSettingsVisible", Flags).SetValue(screen, true);
                        Call("Refresh"); phase = 100; next = EditorApplication.timeSinceStartup + 0.3; return;
                    }
                    for (int i = 0; i < 10; i++)
                    {
                        int number = i;
                        screen.Workspace.Session.EditLevel("메모리 편집", level => LevelDocumentEditing.SetName(level, "편집 " + number));
                    }
                    screen.Workspace.Session.Undo(); screen.Workspace.Session.Redo();
                    Call("Refresh"); phase = 1; next = EditorApplication.timeSinceStartup + 3; return;
                }
                if (phase == 1)
                {
                    CheckRecoveryUntouched("editing and idle do not write checkpoint");
                    Call("OnApplicationFocus", false); Call("OnApplicationPause", true);
                    CheckRecoveryUntouched("focus and pause do not write checkpoint");
                    Check(((Label)Get("folderLabel")).text.Contains("저장하지 않음"), "dirty status visible");
                    Command("Save"); phase = 2; return;
                }
                if ((bool)Get("busy")) return;
                if (phase == 2)
                {
                    var session = screen.Workspace.Session;
                    Check(!session.IsDirty && session.CanUndo, "manual save is clean and preserves Undo");
                    session.Undo(); Check(session.IsDirty, "Undo after save marks dirty");
                    session.Redo(); Check(!session.IsDirty, "Redo to saved state is clean");
                    session.Undo(); Call("Refresh");
                    left = false; screen.RequestLeave(() => left = true);
                    Check(Get("modal") != null && !left, "dirty exit waits for choice");
                    Call("CloseModal");
                    Check(session.IsDirty && !left, "cancel exit preserves changes");
                    screen.RequestLeave(() => left = true);
                    Click("discard-and-leave"); phase = 3; return;
                }
                if (phase == 3)
                {
                    Check(left && !screen.Workspace.Session.IsDirty, "discard exit completes without saving");
                    invalidInput = new IntegerField("검사 숫자") { name = "manual-invalid-number", value = 4, isDelayed = true };
                    ((VisualElement)Get("editor")).Add(invalidInput);
                    invalidInput.Focus(); phase = 4; next = EditorApplication.timeSinceStartup + 0.3; return;
                }
                if (phase == 4)
                {
                    var text = invalidInput.Q(className: "unity-base-field__input").Q<TextElement>();
                    ((INotifyValueChanged<string>)text).SetValueWithoutNotify("잘못된 숫자");
                    Call("UpdateManualSaveStatus");
                    Check(((Label)Get("folderLabel")).text.Contains("입력 확인 필요"), "invalid pending input marks dirty");
                    left = false; screen.ExitRequested += () => left = true;
                    Command("Exit"); phase = 5; next = EditorApplication.timeSinceStartup + 0.3; return;
                }
                if (phase == 5)
                {
                    Check(Get("modal") != null && !left, "invalid input still permits exit choices");
                    Click("cancel"); phase = 6; next = EditorApplication.timeSinceStartup + 0.3; return;
                }
                if (phase == 6)
                {
                    Check(invalidInput.text == "잘못된 숫자" && !left, "cancel retains invalid raw input");
                    Command("Exit"); Click("save-and-leave");
                    phase = 7; next = EditorApplication.timeSinceStartup + 0.3; return;
                }
                if (phase == 7)
                {
                    Check(!left && invalidInput.text == "잘못된 숫자", "invalid save prevents exit and preserves input");
                    // 실제 종료 메뉴는 에디터 Play Mode도 끝내므로, 최종 버리기는 관찰 가능한 계속 동작을 사용한다.
                    screen.RequestLeave(() => left = true); Click("discard-and-leave"); phase = 8; return;
                }
                Check(left, "invalid input can explicitly be discarded");
                CheckRecoveryUntouched("save and exit do not write checkpoint");
                if (phase == 8)
                {
                    screen.Workspace.Session.EditLevel("마지막 편집", level => LevelDocumentEditing.SetName(level, "메모리 유지"));
                    if (existingRecovery)
                    {
                        Command("LegacyRecovery"); phase = 9; next = EditorApplication.timeSinceStartup + 0.3; return;
                    }
                }
                if (phase == 9)
                {
                    Check(Get("modal") != null, "explicit corrupt recovery displays error dialog");
                    Click("cancel");
                    Check(screen.Workspace.Session.IsDirty, "cancel corrupt recovery preserves current dirty session");
                    CheckRecoveryUntouched("corrupt recovery check preserves both original files");
                }
                screen.enabled = false;
                CheckRecoveryUntouched("disable does not write checkpoint");
                Check(screen.Workspace.Session.CanUndo && screen.Workspace.Session.IsDirty, "memory history remains available");
                Debug.Log("PASS manual-save and exit protection: " + folder); Finish(0);
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static object Get(string name) => typeof(LevelToolScreen).GetField(name, Flags).GetValue(screen);
        private static void CheckRecoveryUntouched(string label)
        {
            Check(existingRecovery ? File.ReadAllText(Path.Combine(folder, "draft.json")) == "invalid legacy fixture" &&
                File.ReadAllText(Path.Combine(folder, "draft.json.previous")) == "previous legacy fixture" :
                !File.Exists(Path.Combine(folder, "draft.json")) && !File.Exists(Path.Combine(folder, "draft.json.previous")), label);
        }
        private static void TickSharedPending()
        {
            var root = (VisualElement)Get("root");
            if (phase == 200)
            {
                root.Q<TextField>("tutorial-instructions").Focus(); phase = 201;
                next = EditorApplication.timeSinceStartup + 0.3; return;
            }
            if (phase == 201)
            {
                var input = root.Q<TextField>("tutorial-instructions");
                ((INotifyValueChanged<string>)input.Q(className: "unity-base-field__input").Q<TextElement>()).SetValueWithoutNotify("공유 미확정 한글");
                Check(!((SharedTutorialDraft)Get("sharedTutorialDraft")).IsChanged, "shared raw input is initially uncommitted");
                left = false; screen.RequestLeave(() => left = true); Click("cancel");
                phase = 202; next = EditorApplication.timeSinceStartup + 0.3; return;
            }
            if (phase == 202)
            {
                Check(root.Q<TextField>("tutorial-instructions").text == "공유 미확정 한글" && !left, "shared exit cancel preserves raw input");
                screen.RequestLeave(() => left = true); Click("apply-flow-and-leave");
                phase = 203; next = EditorApplication.timeSinceStartup + 0.5; return;
            }
            if ((bool)Get("busy") || (bool)Get("committingCommandInput")) return;
            if (phase == 204)
            {
                root.Q<TextField>("tutorial-instructions").Focus(); phase = 205;
                next = EditorApplication.timeSinceStartup + 0.3; return;
            }
            if (phase == 205)
            {
                var input = root.Q<TextField>("tutorial-instructions");
                ((INotifyValueChanged<string>)input.Q(className: "unity-base-field__input").Q<TextElement>()).SetValueWithoutNotify("적용 버튼 마지막 한글");
                Call("ApplySharedTutorialDraft"); phase = 206;
                next = EditorApplication.timeSinceStartup + 0.5; return;
            }
            if (phase == 206)
            {
                Check(Get("sharedTutorialDraft") == null && (string)screen.Workspace.Session.Get(sharedFlowId).Data["steps"][0]["instructions"] == "적용 버튼 마지막 한글",
                    "normal shared apply includes pending last input");
                Finish(0); return;
            }
            Check(Get("sharedTutorialDraft") == null && (string)screen.Workspace.Session.Get(sharedFlowId).Data["steps"][0]["instructions"] == "공유 미확정 한글",
                "shared apply before exit commits last raw text");
            Check(root.Q<Button>("save-and-leave") != null && !left, "shared apply still requires parent save choice");
            Click("cancel");
            Check(screen.Workspace.Session.IsDirty && !File.Exists(Path.Combine(folder, "draft.json")), "shared apply stays in memory until save");
            typeof(LevelToolScreen).GetField("sharedTutorialDraft", Flags).SetValue(screen, new SharedTutorialDraft(screen.Workspace.Session));
            Call("Refresh"); phase = 204; next = EditorApplication.timeSinceStartup + 0.3;
        }
        private static void TickPendingTransition()
        {
            var root = (VisualElement)Get("root");
            if (phase == 100)
            {
                root.Q<TextField>("level-name").Focus(); phase = 101;
                next = EditorApplication.timeSinceStartup + 0.3; return;
            }
            if (phase == 101)
            {
                var input = root.Q<TextField>("level-name");
                ((INotifyValueChanged<string>)input.Q(className: "unity-base-field__input").Q<TextElement>()).SetValueWithoutNotify("실패 후에도 남을 한글");
                Call("WithDirty", new Func<string, bool>(choice => screen.Workspace.Open(Path.Combine(folder, "missing"), choice)));
                Click("discard-and-continue"); phase = 102;
                next = EditorApplication.timeSinceStartup + 0.5; return;
            }
            if ((bool)Get("busy")) return;
            if (phase == 102)
            {
                Check(root.Q<TextField>("level-name").text == "실패 후에도 남을 한글", "failed transition preserves uncommitted Korean");
                Check(!screen.Workspace.Session.IsDirty, "failed transition does not silently commit raw input");
                Check(!File.Exists(Path.Combine(folder, "draft.json")), "failed transition does not create recovery");
                var moves = root.Q<IntegerField>("level-moves");
                ((INotifyValueChanged<string>)moves.Q(className: "unity-base-field__input").Q<TextElement>()).SetValueWithoutNotify("11");
                Command("Save"); phase = 103; next = EditorApplication.timeSinceStartup + 0.5; return;
            }
            if ((bool)Get("commandAwaitingInput") || (bool)Get("committingCommandInput")) return;
            if (phase == 103)
            {
                Check(root.Q<TextField>("level-name").value == "실패 후에도 남을 한글" && root.Q<IntegerField>("level-moves").value == 11,
                    "save commits multiple pending fields without losing Korean or number");
                Check(!screen.Workspace.Session.IsDirty, "multiple pending fields saved cleanly");
                root.Q<TextField>("level-name").Focus(); phase = 104; next = EditorApplication.timeSinceStartup + 0.3; return;
            }
            if (phase == 104)
            {
                var input = root.Q<TextField>("level-name");
                ((INotifyValueChanged<string>)input.Q(className: "unity-base-field__input").Q<TextElement>()).SetValueWithoutNotify("중단 전 미확정 입력");
                screen.enabled = false; screen.enabled = true;
                phase = 105; next = EditorApplication.timeSinceStartup + 0.5; return;
            }
            if (phase == 105)
            {
                Check(root.Q<TextField>("level-name").text == "중단 전 미확정 입력" && !screen.Workspace.Session.IsDirty,
                    "disable and reenable preserve raw input without committing");
                File.WriteAllText(Path.Combine(folder, "draft.json"), "corrupt fixture");
                Command("LegacyRecovery"); phase = 106; next = EditorApplication.timeSinceStartup + 0.5; return;
            }
            if (phase == 106)
            {
                Check(Get("modal") != null, "explicit corrupt record check opens error dialog");
                Click("cancel"); phase = 107; next = EditorApplication.timeSinceStartup + 0.3; return;
            }
            Check(root.Q<TextField>("level-name").text == "중단 전 미확정 입력" && !screen.Workspace.Session.IsDirty,
                "cancel explicit recovery retains uncommitted raw input");
            Finish(0);
        }
        private static void Call(string name, params object[] args) => typeof(LevelToolScreen).GetMethod(name, Flags).Invoke(screen, args);
        private static void Command(string name) => Call("TryExecuteCommand", Enum.Parse(typeof(LevelToolScreen).GetNestedType("ToolCommandId", BindingFlags.NonPublic), name));
        private static void Click(string name)
        {
            var button = ((VisualElement)Get("root")).Q<Button>(name);
            Check(button != null, "button " + name);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
        private static void Check(bool value, string label) { if (!value) throw new Exception("FAIL " + label); Debug.Log("PASS " + label); }
        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            if (screen != null) typeof(LevelToolScreen).GetField("busy", Flags).SetValue(screen, true);
            EditorSettings.enterPlayModeOptionsEnabled = previousEnabled; EditorSettings.enterPlayModeOptions = previousOptions;
            EditorApplication.Exit(code);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolInputVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static bool previousEnabled, done;
        private static EnterPlayModeOptions previousOptions;
        private static double deadline;
        private static int stage, exit;
        private static string recoveryFolder, folder, levelId;
        private static string lifecycleState;
        private static string previousClipboard;
        private static LevelToolScreen screen;
        private static VisualElement Root => screen.GetComponent<UIDocument>().rootVisualElement;
        private static AuthoringEditSession Session => screen.Workspace.Session;
        private static bool Busy => (bool)typeof(LevelToolScreen).GetField("busy", Flags).GetValue(screen) ||
            (bool)typeof(LevelToolScreen).GetField("commandAwaitingInput", Flags).GetValue(screen);
        private const string FinalName = "지연 입력 끝글자힣";

        public static void Run()
        {
            previousClipboard = GUIUtility.systemCopyBuffer;
            previousEnabled = EditorSettings.enterPlayModeOptionsEnabled; previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            string run = Path.GetFullPath("Logs/LevelToolUIStage01/input-" + Guid.NewGuid().ToString("N"));
            recoveryFolder = Path.Combine(run, "recovery"); folder = Path.Combine(run, "workspace");
            LevelToolRecoveryIsolation.Configure(recoveryFolder);
            stage = 0; exit = 0; done = false; deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Tick; EditorApplication.playModeStateChanged += State;
            EditorApplication.EnterPlaymode();
        }
        private static void State(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= State;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnabled; EditorSettings.enterPlayModeOptions = previousOptions;
            GUIUtility.systemCopyBuffer = previousClipboard;
            EditorApplication.Exit(exit);
        }
        private static void Tick()
        {
            try
            {
                if (done) return;
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Input verification timed out at stage " + stage);
                if (!EditorApplication.isPlaying) return;
                screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                if (screen == null || Busy) return;
                switch (stage)
                {
                    case 0:
                        LevelToolRecoveryIsolation.Verify(screen, recoveryFolder);
                        Call("CloseModal");
                        screen.Workspace.Create(folder, LevelToolDocuments.NewProject(), "Discard");
                        levelId = Session.SelectedLevelId;
                        Session.Apply("게임 시험 미션 준비", docs => MissionDocumentEditing.Set(docs[levelId].Data, 0, "Color", "Type1", 10));
                        screen.Workspace.Save();
                        Call("SelectWorkspacePage", "보드 편집"); Click("inspector-level");
                        Root.Q<TextField>("level-name").Focus(); stage++; break;
                    case 1:
                        TextField name = Root.Q<TextField>("level-name");
                        SetRaw(name, FinalName);
                        Check(name.isDelayed && name.value != FinalName && name.text == FinalName, "actual TextField contains uncommitted final character");
                        Send(KeyCode.S, EventModifiers.Control, true); Send(KeyCode.S, EventModifiers.Control, false);
                        stage++; break;
                    case 2:
                        Check((string)Read()["displayName"] == FinalName && !Session.IsDirty, "Ctrl+S persists final delayed character before blur");
                        Check(InsideFocus(Root.Q<TextField>("level-name")), "save restores editing field focus");
                        Root.Q<IntegerField>("level-moves").Focus(); stage++; break;
                    case 3:
                        IntegerField moves = Root.Q<IntegerField>("level-moves"); SetRaw(moves, "37");
                        Check(moves.isDelayed && moves.value != 37 && moves.text == "37", "actual IntegerField contains an uncommitted value");
                        Send(KeyCode.S, EventModifiers.Control, true); Send(KeyCode.S, EventModifiers.Control, false);
                        stage++; break;
                    case 4:
                        Check((int)Read()["moveCount"] == 37 && !Session.IsDirty, "Ctrl+S persists delayed integer");
                        Root.Q<IntegerField>("level-moves").Focus(); stage++; break;
                    case 5:
                        IntegerField invalid = Root.Q<IntegerField>("level-moves"); SetRaw(invalid, "-");
                        string before = Session.ExportState();
                        Send(KeyCode.S, EventModifiers.Control, true); Send(KeyCode.S, EventModifiers.Control, false);
                        Check(!Busy && invalid.text == "-" && Session.ExportState() == before && (int)Read()["moveCount"] == 37,
                            "invalid integer keeps raw input and does not save");
                        Check(Root.Q<Label>("status").text.Contains("정수"), "invalid integer explains why save is deferred");
                        VerifyTextRouting(invalid);
                        SetRaw(invalid, "37"); Root.Q("board").Focus(); stage++; break;
                    case 6:
                        Session.Apply("first", docs => docs[levelId].Data["displayName"] = "first");
                        Session.Apply("second", docs => docs[levelId].Data["displayName"] = "second");
                        Call("Refresh"); Root.Q("board").Focus(); stage++; break;
                    case 7:
                        Send(KeyCode.Z, EventModifiers.Control, true); Send(KeyCode.Z, EventModifiers.Control, true);
                        Check((string)Session.Get(levelId).Data["displayName"] == "first", "held Ctrl+Z executes exactly once");
                        Send(KeyCode.Z, EventModifiers.Control, false);
                        Send(KeyCode.Z, EventModifiers.Control | EventModifiers.Shift, true); Send(KeyCode.Z, EventModifiers.Control | EventModifiers.Shift, false);
                        Check((string)Session.Get(levelId).Data["displayName"] == "second", "Ctrl+Shift+Z redoes the same command");
                        Click("undo"); Check((string)Session.Get(levelId).Data["displayName"] == "first", "header undo executes once");
                        Click("menu-edit"); Click("command-Redo");
                        Check((string)Session.Get(levelId).Data["displayName"] == "second", "menu redo executes once");
                        Root.Q("board").Focus();
                        Send(KeyCode.Z, EventModifiers.Control, true); Send(KeyCode.Z, EventModifiers.Control, false);
                        Send(KeyCode.Y, EventModifiers.Control, true); Send(KeyCode.Y, EventModifiers.Control, false);
                        Check((string)Session.Get(levelId).Data["displayName"] == "second", "Ctrl+Y redoes the same command");
                        Root.Q<TextField>("level-name").Focus(); stage++; break;
                    case 8:
                        Send(KeyCode.F1, EventModifiers.None, true); Send(KeyCode.F1, EventModifiers.None, false);
                        Check(Root.Q("shortcut-search") != null, "F1 opens searchable help from text input");
                        string state = Session.ExportState();
                        Send(KeyCode.F5, EventModifiers.None, true); Send(KeyCode.F5, EventModifiers.None, false);
                        Send(KeyCode.S, EventModifiers.Control, true); Send(KeyCode.S, EventModifiers.Control, false);
                        Check(!Busy && Session.ExportState() == state && Root.Q("modal") != null && screen.TestSession == null, "modal blocks background save and play");
                        Send(KeyCode.Escape, EventModifiers.None, true); Send(KeyCode.Escape, EventModifiers.None, false);
                        Check(Root.Q("modal") == null && InsideFocus(Root.Q<TextField>("level-name")), "Esc closes only help and restores field focus");
                        Root.Q("board").Focus(); stage++; break;
                    case 9:
                        VerifySharedAndPicking();
                        Call("UnregisterShortcuts"); Call("RegisterShortcuts"); Call("RegisterShortcuts");
                        Root.Q("board").Focus(); stage++; break;
                    case 10:
                        Send(KeyCode.Z, EventModifiers.Control, true); Send(KeyCode.Z, EventModifiers.Control, false);
                        Check((string)Session.Get(levelId).Data["displayName"] == "first", "repeated shortcut registration retains exactly one undo handler");
                        AuthoringToolWorkspace external = new AuthoringToolWorkspace(); external.Open(folder, "Cancel");
                        external.Session.Apply("외부 변경", docs => docs[levelId].Data["moveCount"] = 38); external.Save();
                        Send(KeyCode.S, EventModifiers.Control, true); Send(KeyCode.S, EventModifiers.Control, false);
                        stage++; break;
                    case 11:
                        Check(Session.IsDirty && (int)Read()["moveCount"] == 38 && (string)Session.Get(levelId).Data["displayName"] == "first",
                            "external write conflict preserves local draft and external snapshot");
                        Check(!Root.Q<Label>("status").text.Contains("완료되었습니다"), "failed save never reports success");
                        Root.Q("board").Focus();
                        Send(KeyCode.O, EventModifiers.Control, true); Send(KeyCode.O, EventModifiers.Control, false);
                        Check(Root.Q("folder-path") != null, "Ctrl+O opens folder selection");
                        Send(KeyCode.Escape, EventModifiers.None, true); Send(KeyCode.Escape, EventModifiers.None, false);
                        Send(KeyCode.S, EventModifiers.Control | EventModifiers.Shift, true); Send(KeyCode.S, EventModifiers.Control | EventModifiers.Shift, false);
                        Check(Root.Q("folder-path") != null, "Ctrl+Shift+S opens copy destination");
                        Send(KeyCode.Escape, EventModifiers.None, true); Send(KeyCode.Escape, EventModifiers.None, false);
                        Root.Q("board").Focus();
                        Send(KeyCode.Space, EventModifiers.Shift, true); Send(KeyCode.Space, EventModifiers.Shift, false);
                        Check((bool)Get("boardFocus"), "Shift+Space expands board");
                        Send(KeyCode.Space, EventModifiers.Shift, true); Send(KeyCode.Space, EventModifiers.Shift, false);
                        Check(!(bool)Get("boardFocus"), "Shift+Space restores panels");
                        lifecycleState = Session.ExportState();
                        screen.enabled = false; screen.enabled = true;
                        stage++; break;
                    case 12:
                        Check(Session.ExportState() == lifecycleState && Root.Q("modal") == null, "actual component reenable preserves draft without recovery prompt");
                        Root.Q("board").Focus();
                        Send(KeyCode.F1, EventModifiers.None, true); Send(KeyCode.F1, EventModifiers.None, false);
                        Check(Root.Query<VisualElement>("modal").ToList().Count == 1, "reenabled component opens help exactly once");
                        Send(KeyCode.Escape, EventModifiers.None, true); Send(KeyCode.Escape, EventModifiers.None, false);
                        Root.Q("board").Focus();
                        Send(KeyCode.F5, EventModifiers.None, true); Send(KeyCode.F5, EventModifiers.None, false);
                        stage++; break;
                    case 13:
                        Check(screen.TestSession != null && screen.TestSession.IsReady, "F5 starts actual game test once: " + Root.Q<Label>("status").text);
                        screen.ReturnToEditor(); stage++; break;
                    case 14:
                        Check(Session.ExportState() == lifecycleState && InsideFocus(Root.Q("board")), "game return preserves draft and restores board focus");
                        TextField clipboardField = new TextField { name = "clipboard-field", value = "복사 붙여넣기 검사" };
                        Root.Add(clipboardField); Root.Add(new TextField { name = "tab-target" });
                        clipboardField.Focus(); stage++; break;
                    case 15:
                        Root.Q<TextField>("clipboard-field").SelectAll();
                        Send(KeyCode.C, EventModifiers.Control, true); Send(KeyCode.C, EventModifiers.Control, false);
                        stage++; break;
                    case 16:
                        Check(GUIUtility.systemCopyBuffer == "복사 붙여넣기 검사", "native text Ctrl+C copies selected Korean text");
                        Root.Q<TextField>("clipboard-field").value = "";
                        Root.Q<TextField>("clipboard-field").SelectRange(0, 0);
                        Send(KeyCode.V, EventModifiers.Control, true); Send(KeyCode.V, EventModifiers.Control, false);
                        stage++; break;
                    case 17:
                        Check(Root.Q<TextField>("clipboard-field").value == "복사 붙여넣기 검사", "native text Ctrl+V pastes without board edits");
                        Send(KeyCode.Tab, EventModifiers.None, true); Send(KeyCode.Tab, EventModifiers.None, false);
                        stage++; break;
                    case 18:
                        Check(InsideFocus(Root.Q("tab-target")) && Session.ExportState() == lifecycleState, "Tab preserves native focus navigation and authoring state");
                        Send(KeyCode.Tab, EventModifiers.Shift, true); Send(KeyCode.Tab, EventModifiers.Shift, false);
                        stage++; break;
                    case 19:
                        Check(InsideFocus(Root.Q("clipboard-field")), "Shift+Tab returns to previous input");
                        Root.Q("clipboard-field").RemoveFromHierarchy(); Root.Q("tab-target").RemoveFromHierarchy();
                        folder += "-pointer"; screen.Workspace.SaveCopy(folder); Call("Refresh"); Click("inspector-level");
                        Root.Q<TextField>("level-name").Focus(); stage++; break;
                    case 20:
                        SetRaw(Root.Q("level-name"), "버튼 입력 확정"); PointerClick("save"); stage++; break;
                    case 21:
                        Check(Read()["displayName"].ToString() == "버튼 입력 확정", "pointer header Save commits delayed input");
                        Root.Q<TextField>("level-name").Focus(); stage++; break;
                    case 22:
                        SetRaw(Root.Q("level-name"), "메뉴 입력 확정"); PointerClick("menu-file"); stage++; break;
                    case 23:
                        PointerClick("command-Save"); stage++; break;
                    case 24:
                        Check(Read()["displayName"].ToString() == "메뉴 입력 확정", "pointer menu Save commits delayed input");
                        Root.Q<IntegerField>("level-moves").Focus(); stage++; break;
                    case 25:
                        SetRaw(Root.Q("level-moves"), "-"); PointerClick("save"); PointerClick("menu-file"); stage++; break;
                    case 26:
                        Check(Root.Q("modal") == null && Root.Q<IntegerField>("level-moves").text == "-", "invalid numeric input blocks pointer save and menu without losing raw input");
                        Debug.Log("PASS real Play Mode delayed text/integer saves, invalid input retention, command parity, shared history and modal boundaries");
                        Debug.Log("MANUAL NOT VERIFIED: physical Korean IME composition, host Editor consumed keys and native OS window controls.");
                        done = true; EditorApplication.ExitPlaymode(); break;
                }
            }
            catch (Exception error)
            {
                exit = 1; done = true; Debug.LogException(error);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); else State(PlayModeStateChange.EnteredEditMode);
            }
        }
        private static void VerifyTextRouting(VisualElement input)
        {
            string before = Session.ExportState();
            foreach (KeyCode key in new[] { KeyCode.C, KeyCode.V, KeyCode.X, KeyCode.A, KeyCode.Z, KeyCode.Y, KeyCode.Delete, KeyCode.B, KeyCode.E, KeyCode.Space })
            {
                foreach (EventModifiers modifiers in new[] { EventModifiers.None, EventModifiers.Control })
                {
                    using (KeyDownEvent evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = key, modifiers = modifiers }))
                    {
                        evt.target = input; Call("OnShortcutKeyDown", evt);
                        Check(!evt.isPropagationStopped && Session.ExportState() == before, "text owns " + modifiers + "+" + key);
                    }
                }
            }
        }
        private static void VerifySharedAndPicking()
        {
            string before = Session.ExportState();
            string beforeData = Session.Get(levelId).Data.ToString();
            Set("tutorialPick", new JObject { ["cells"] = new JArray() });
            Send(KeyCode.F1, EventModifiers.None, true); Send(KeyCode.F1, EventModifiers.None, false);
            Send(KeyCode.Escape, EventModifiers.None, true); Send(KeyCode.Escape, EventModifiers.None, false);
            Check(Get("tutorialPick") != null && Session.ExportState() == before, "first Esc closes modal and preserves pending tutorial pick");
            Send(KeyCode.Escape, EventModifiers.None, true); Send(KeyCode.Escape, EventModifiers.None, false);
            Check(Get("tutorialPick") == null && Session.ExportState() == before, "second Esc cancels only pending tutorial pick");
            string flowId = TutorialDraftEditing.CreateFlow(Session, "공유 단축키 검사");
            SharedTutorialDraft draft = new SharedTutorialDraft(Session);
            string parent = Session.ExportState();
            draft.Session.Apply("공유 사본 수정", docs => docs[flowId].Data["displayName"] = "changed");
            Set("sharedTutorialDraft", draft); Call("Refresh"); Root.Q("board").Focus();
            Type commands = typeof(LevelToolScreen).GetNestedType("ToolCommandId", BindingFlags.NonPublic);
            object[] args = { Enum.Parse(commands, "Save"), null };
            Check(!(bool)typeof(LevelToolScreen).GetMethod("CanExecuteCommand", Flags).Invoke(screen, args), "shared draft forbids saving parent");
            Send(KeyCode.Z, EventModifiers.Control, true); Send(KeyCode.Z, EventModifiers.Control, false);
            Check(!draft.IsChanged && Session.ExportState() == parent, "Undo uses shared draft history and leaves parent untouched");
            Set("sharedTutorialDraft", null); Session.Undo(); Call("Refresh");
            Check(Session.Get(levelId).Data.ToString() == beforeData, "shared verification restores parent document");
        }
        private static JObject Read() => new ContentSnapshotStore(folder).Read().Snapshot.Get(levelId).Data;
        private static void SetRaw(VisualElement field, string text)
        {
            TextElement input = field.Q<TextElement>("unity-text-input") ?? field.Query<TextElement>().ToList().Last();
            input.text = text;
        }
        private static bool InsideFocus(VisualElement field)
        { VisualElement focus = Root.focusController.focusedElement as VisualElement; return field == focus || (focus != null && field.Contains(focus)); }
        private static void Send(KeyCode key, EventModifiers modifiers, bool down)
        {
            VisualElement target = Root.focusController.focusedElement as VisualElement ?? Root;
            // 공개 focusedElement는 복합 필드로 재지정된다. 실제 키 입력 대상은 내부 TextElement다.
            if (target is TextField || target is IntegerField)
                target = target.Q<TextElement>("unity-text-input") ?? target.Query<TextElement>().ToList().Last();
            Event source = new Event { type = down ? EventType.KeyDown : EventType.KeyUp, keyCode = key, modifiers = modifiers, character = key == KeyCode.Tab ? '\t' : '\0' };
            if (down) { using (KeyDownEvent evt = KeyDownEvent.GetPooled(source)) { evt.target = target; target.SendEvent(evt); } }
            else { using (KeyUpEvent evt = KeyUpEvent.GetPooled(source)) { evt.target = target; target.SendEvent(evt); } }
        }
        private static void Click(string name)
        {
            Button button = Root.Q<Button>(name) ?? throw new Exception("Missing button " + name);
            Check(button.enabledInHierarchy, "enabled " + name);
            typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
        }
        private static void PointerClick(string name)
        {
            Button button = Root.Q<Button>(name) ?? throw new Exception("Missing pointer button " + name);
            Event source = new Event { type = EventType.MouseDown, button = 0, mousePosition = button.worldBound.center };
            using (PointerDownEvent evt = PointerDownEvent.GetPooled(source)) { evt.target = button; button.SendEvent(evt); }
            source.type = EventType.MouseUp;
            using (PointerUpEvent evt = PointerUpEvent.GetPooled(source)) { evt.target = button; button.SendEvent(evt); }
        }
        private static object Call(string name, params object[] arguments) => typeof(LevelToolScreen).GetMethod(name, Flags).Invoke(screen, arguments);
        private static object Get(string name) => typeof(LevelToolScreen).GetField(name, Flags).GetValue(screen);
        private static void Set(string name, object value) => typeof(LevelToolScreen).GetField(name, Flags).SetValue(screen, value);
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}


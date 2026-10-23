#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private enum ToolCommandId { Open, NewProject, ImportProject, ExportProject, RecentProjects, Save, SaveCopy, Reload, Backup, LegacyRecovery, Exit, Undo, Redo, Copy, Cut, Paste, Delete, BoardFocus, Play, Validate, ReplayTutorial, Help, Cancel, Windowed, Fullscreen, Manual }
        private sealed class ToolCommand
        {
            internal readonly ToolCommandId Id;
            internal readonly string Label, Gesture, Description, Scope;
            internal ToolCommand(ToolCommandId id, string label, string gesture, string description, string scope)
            { Id = id; Label = label; Gesture = gesture; Description = description; Scope = scope; }
        }
        private static readonly ToolCommand[] ToolCommands =
        {
            new ToolCommand(ToolCommandId.Open, "작업 폴더 열기", "Ctrl+O", "미저장 내용을 확인한 뒤 JSON 작업 폴더를 엽니다.", "제작 화면"),
            new ToolCommand(ToolCommandId.ImportProject, "프로젝트 ZIP 가져오기", "", "검사한 ZIP을 새 내부 프로젝트로 등록합니다. 기존 프로젝트는 보존합니다.", "제작 화면"),
            new ToolCommand(ToolCommandId.ExportProject, "저장 후 ZIP 내보내기", "", "JSON 저장을 마친 후 정상 저장본만 ZIP으로 전송합니다. ZIP 위치 선택을 취소해도 JSON 저장은 유지됩니다.", "제작 화면"),
            new ToolCommand(ToolCommandId.RecentProjects, "최근 프로젝트", "", "최근 성공적으로 연 프로젝트를 선택합니다.", "제작 화면"),
            new ToolCommand(ToolCommandId.NewProject, "새 프로젝트", "", "새 폴더에 프로젝트를 만듭니다.", "제작 화면"),
            new ToolCommand(ToolCommandId.Save, "저장", "Ctrl+S", "현재 입력을 확정하고 작업 폴더에 저장합니다. 조합 중이거나 숫자가 잘못되면 보류합니다.", "제작 화면 · 입력 필드"),
            new ToolCommand(ToolCommandId.SaveCopy, "사본 저장", "Ctrl+Shift+S", "입력을 확정한 뒤 다른 JSON 폴더에 사본을 저장합니다.", "제작 화면 · 입력 필드"),
            new ToolCommand(ToolCommandId.Reload, "다시 읽기", "", "미저장 내용을 확인하고 작업 폴더를 다시 읽습니다.", "제작 화면"),
            new ToolCommand(ToolCommandId.Backup, "이전 정상본", "", "선택한 폴더의 이전 정상본을 엽니다.", "제작 화면"),
            new ToolCommand(ToolCommandId.LegacyRecovery, "과거 복구 기록 확인", "", "이전 버전의 복구 기록을 직접 확인합니다. 현재 작업과 원본 파일은 선택 전까지 유지됩니다.", "제작 화면"),
            new ToolCommand(ToolCommandId.Exit, "도구 종료", "", "미저장 내용을 확인한 뒤 도구를 종료합니다.", "제작 화면"),
            new ToolCommand(ToolCommandId.Undo, "실행 취소", "Ctrl+Z", "활성 제작 세션의 변경을 되돌립니다. 글 입력 중에는 필드의 기본 실행 취소가 우선합니다.", "제작 화면 · 공유 사본"),
            new ToolCommand(ToolCommandId.Redo, "다시 실행", "Ctrl+Y / Ctrl+Shift+Z", "활성 제작 세션의 변경을 다시 적용합니다. 글 입력 중에는 필드의 기본 동작이 우선합니다.", "제작 화면 · 공유 사본"),
            new ToolCommand(ToolCommandId.Copy, "선택 요소 복사", "Ctrl+C", "현재 층의 선택을 같은 세션의 클립보드에 복사합니다. 2×2 본체는 통째로 복사합니다.", "보드 선택"),
            new ToolCommand(ToolCommandId.Cut, "선택 요소 잘라내기", "Ctrl+X", "선택의 사본을 확보한 다음 현재 층에서 삭제합니다.", "보드 선택"),
            new ToolCommand(ToolCommandId.Paste, "선택 칸에 붙여넣기", "Ctrl+V", "첫 선택 칸을 기준으로 배치합니다. 경계·충돌·참조 오류가 있으면 전체를 취소합니다.", "같은 편집 세션"),
            new ToolCommand(ToolCommandId.Delete, "선택 층 삭제", "Delete", "현재 층의 선택 요소를 삭제합니다. 다른 층은 유지합니다.", "보드 선택"),
            new ToolCommand(ToolCommandId.BoardFocus, "보드 영역 넓게 / 복원", "Shift+Space", "좌우 패널을 접거나 복원합니다. 키는 보드에 포커스가 있을 때 작동합니다.", "보드 포커스"),
            new ToolCommand(ToolCommandId.Windowed, "16:10 창 모드", "", "저장된 창 크기와 위치로 돌아갑니다. 크기 정보가 없으면 1280×800으로 열며 테두리를 끌어 크기를 조절할 수 있습니다.", "Windows 레벨툴 실행 파일"),
            new ToolCommand(ToolCommandId.Fullscreen, "창 모드 전환", "Alt+Enter / F11", "모니터를 가득 채우는 전체 화면과 16:10 창을 전환합니다. 창 크기 정보가 없으면 1280×800으로 돌아갑니다.", "Windows 레벨툴 전체 · 시험 중에도 가능"),
            new ToolCommand(ToolCommandId.Play, "게임 시험", "F5", "현재 자료를 검사하고 게임 시험을 시작합니다.", "제작 화면 · 공유 사본"),
            new ToolCommand(ToolCommandId.Validate, "레벨 검사", "", "선택한 레벨의 현재 자료를 검사합니다.", "제작 화면"),
            new ToolCommand(ToolCommandId.ReplayTutorial, "튜토리얼 재생 검사", "", "튜토리얼 재생을 포함하여 검사합니다.", "제작 화면"),
            new ToolCommand(ToolCommandId.Help, "단축키 안내", "F1", "명령과 적용 범위, 입력 주의점을 검색합니다.", "제작 화면"),
            new ToolCommand(ToolCommandId.Manual, "레벨툴 사용 설명서", "", "현재 화면의 JSON 저장, ZIP 전달, Android 터치 조작을 스크린샷과 함께 봅니다.", "제작 화면"),
            new ToolCommand(ToolCommandId.Cancel, "진행 중 조작 취소", "Esc", "맨 위 메뉴·모달, 대상 지정, 현재 도구 순서로 한 단계만 취소합니다.", "현재 입력 문맥")
        };
        private readonly Dictionary<Button, ToolCommandId> commandButtons = new Dictionary<Button, ToolCommandId>();
        private bool commandMenuOpen;
        private bool commandAwaitingInput;
        private FocusedInputState commandInputFocus;

        private bool CanExecuteCommand(ToolCommandId id, out string reason)
        {
            reason = null;
            if (id == ToolCommandId.Windowed || id == ToolCommandId.Fullscreen)
            {
                if (!LevelToolWindowController.Supported) reason = "Windows 레벨툴 실행 파일에서 사용합니다. Unity 에디터 창은 변경하지 않습니다.";
                else if (windowController?.Ready != true) reason = windowController?.Error ?? "창을 준비하고 있습니다.";
                return reason == null;
            }
            if (id == ToolCommandId.Cancel && modal != null) return true;
            if (id == ToolCommandId.Cancel && fileTransferCancellation != null) return true;
            if (modal != null && !commandMenuOpen) reason = "열린 창을 닫은 뒤 실행하세요.";
            else if (busy || playRoot != null || commandAwaitingInput) reason = "진행 중인 처리가 끝난 뒤 실행하세요.";
            else if (id == ToolCommandId.Cancel)
            {
                if (tutorialPick == null && flowTool == null && !wireDrawing && brush == null && moveBody == null && boardInput?.HasPendingGesture != true)
                    reason = "취소할 조작이 없습니다.";
            }
            else if (id == ToolCommandId.Help || id == ToolCommandId.Manual) return true;
            else if (tutorialPick != null) reason = "칸 지정을 확정하거나 취소하세요.";
            else if (sharedTutorialDraft != null && (id == ToolCommandId.Open || id == ToolCommandId.NewProject ||
                id == ToolCommandId.ImportProject || id == ToolCommandId.ExportProject || id == ToolCommandId.RecentProjects ||
                id == ToolCommandId.Save || id == ToolCommandId.SaveCopy || id == ToolCommandId.Reload || id == ToolCommandId.Backup || id == ToolCommandId.LegacyRecovery))
                reason = "공유 원본 사본을 적용하거나 버린 뒤 파일 작업을 해 주세요.";
            else if (id == ToolCommandId.Open || id == ToolCommandId.NewProject || id == ToolCommandId.Backup || id == ToolCommandId.LegacyRecovery || id == ToolCommandId.Exit || id == ToolCommandId.ImportProject || id == ToolCommandId.RecentProjects) return true;
            else if (id == ToolCommandId.Reload && Workspace.Folder != null) return true;
            else if (Session == null) reason = Workspace.Directory != null ? "목록의 편집 버튼으로 레벨을 여세요." : "먼저 작업 폴더를 여세요.";
            else if (Session.SelectedLevelId == null && (id == ToolCommandId.Play || id == ToolCommandId.Validate ||
                id == ToolCommandId.ReplayTutorial || id == ToolCommandId.Copy || id == ToolCommandId.Cut ||
                id == ToolCommandId.Paste || id == ToolCommandId.Delete || id == ToolCommandId.BoardFocus))
                reason = "먼저 레벨을 만들거나 목록의 편집 버튼으로 열어 주세요.";
            else if (id == ToolCommandId.Copy || id == ToolCommandId.Cut || id == ToolCommandId.Paste || id == ToolCommandId.Delete)
            {
                if (sharedTutorialDraft != null || inspectorPage != "기본") reason = "보드 편집의 기본 배치 화면에서 사용하세요.";
                else if (Session.SelectedCells.Length == 0) reason = "먼저 대상 칸을 선택하세요.";
                else if (id == ToolCommandId.Paste && !ElementClipboard.HasClipboard) reason = "먼저 요소를 복사하세요.";
                else if (id == ToolCommandId.Paste && ElementClipboard.ClipboardLayer != layer) reason = "복사한 요소의 층으로 전환한 뒤 붙여넣으세요.";
                else if (id != ToolCommandId.Paste && SelectedBoardInstanceIds(out reason).Length == 0)
                    reason ??= "현재 층의 요소를 선택하세요.";
            }
            else if ((id == ToolCommandId.Save || id == ToolCommandId.ExportProject) && Workspace.IsRecovery) reason = "복구본은 사본 저장으로 다른 폴더에 저장하세요.";
            else if (id == ToolCommandId.Undo && !(sharedTutorialDraft?.Session ?? Session).CanUndo) reason = "실행 취소할 변경이 없습니다.";
            else if (id == ToolCommandId.Redo && !(sharedTutorialDraft?.Session ?? Session).CanRedo) reason = "다시 실행할 변경이 없습니다.";
            else if (id == ToolCommandId.Play && (RecordsBusy || BatchActive || MultiActive)) reason = "진행 중인 시험과 기록 처리를 먼저 끝내세요.";
            return reason == null;
        }

        private bool TryExecuteCommand(ToolCommandId id)
        {
            if (tutorialWork != null && (id == ToolCommandId.Save || id == ToolCommandId.SaveCopy || id == ToolCommandId.Open ||
                id == ToolCommandId.NewProject || id == ToolCommandId.ImportProject || id == ToolCommandId.ExportProject ||
                id == ToolCommandId.Reload || id == ToolCommandId.RecentProjects || id == ToolCommandId.Backup ||
                id == ToolCommandId.LegacyRecovery || id == ToolCommandId.Exit) && GuardTutorialWorkExit(() => TryExecuteCommand(id))) return false;
            if (id == ToolCommandId.Undo && tutorialWork != null && sharedTutorialDraft == null && !Session.CanUndoLevelWork(tutorialWork))
            { Show("튜토리얼 작성 시작 이전 변경은 전체 취소 후 되돌릴 수 있습니다."); return false; }
            if (!CanExecuteCommand(id, out string reason)) { Show(reason); return false; }
            if (id == ToolCommandId.Cancel)
            {
                if (trialLoadingModal != null) CancelTrialLoading();
                else if (fileTransferCancellation != null) fileTransferCancellation.Cancel();
                else if (modal != null) { purposeDraft = null; CloseModal(); }
                else if (tutorialPick != null) CancelTutorialPick();
                else { CancelPendingInput(); brush = null; Refresh(); Show("진행 중 조작을 취소했습니다."); }
                return true;
            }
            if (commandMenuOpen) CloseModal();
            CancelBoardTouch();
            if (id == ToolCommandId.Windowed || id == ToolCommandId.Fullscreen)
            {
                if (id == ToolCommandId.Fullscreen) windowController.ToggleFullscreen();
                else windowController.SetMode(LevelToolWindowMode.Windowed);
                return true;
            }
            if (!IsNavigationCommand(id) && !CommitCommandInput()) return false;
            if (!IsNavigationCommand(id) && committingCommandInput)
            {
                commandAwaitingInput = true;
                RefreshCommandStates();
                ExecuteAfterInputCommit(id).Forget();
                return true;
            }
            if (!CanExecuteCommand(id, out reason)) { Show(reason); return false; }
            switch (id)
            {
                case ToolCommandId.Open: ChooseFolder(path => WithDirty(choice => Workspace.OpenListing(path, choice))); break;
                case ToolCommandId.NewProject: NewProject(); break;
                case ToolCommandId.Save: Storage(() => Workspace.Save(), true).Forget(); break;
                case ToolCommandId.ImportProject: ImportProject(); break;
                case ToolCommandId.ExportProject: ExportProject(); break;
                case ToolCommandId.RecentProjects: OpenRecentProjects(); break;
                case ToolCommandId.SaveCopy: ChooseFolder(path => Storage(() => Workspace.SaveCopy(path), true).Forget(), true); break;
                case ToolCommandId.Reload: WithDirtyFor("다시 읽기", choice => Workspace.ReloadListing(choice)); break;
                case ToolCommandId.Backup: ChooseFolder(path => WithDirtyFor("이전 정상본 열기", choice => Workspace.OpenListing(path, choice, true))); break;
                case ToolCommandId.LegacyRecovery: ReadLegacyRecovery().Forget(); break;
                case ToolCommandId.Exit: ExitTool(); break;
                case ToolCommandId.Undo: Edit(() => (sharedTutorialDraft?.Session ?? Session).Undo()); break;
                case ToolCommandId.Redo: Edit(() => (sharedTutorialDraft?.Session ?? Session).Redo()); break;
                case ToolCommandId.Copy: Edit(() => ElementClipboard.Copy(layer)); break;
                case ToolCommandId.Cut: Edit(() => ElementClipboard.Cut(layer)); break;
                case ToolCommandId.Delete: Edit(() => ElementClipboard.Delete(layer)); break;
                case ToolCommandId.Paste:
                    Edit(() => { int cell = Session.SelectedCells[0]; ElementClipboard.Paste(cell / 9, cell % 9); }); break;
                case ToolCommandId.BoardFocus: ToggleBoardFocus(); break;
                case ToolCommandId.Play: BeginPlay().Forget(); break;
                case ToolCommandId.Validate: ValidateLevel(false); break;
                case ToolCommandId.ReplayTutorial: ValidateLevel(true); break;
                case ToolCommandId.Help: OpenShortcutHelp(); break;
                case ToolCommandId.Manual: OpenManual(); break;
            }
            RefreshCommandStates();
            return true;
        }

        private async UniTask ExecuteAfterInputCommit(ToolCommandId id)
        {
            int revision = inputCommitRevision;
            await UniTask.WaitUntil(() => !committingCommandInput || this == null || !isActiveAndEnabled);
            commandAwaitingInput = false;
            if (this != null && isActiveAndEnabled && revision == inputCommitRevision) TryExecuteCommand(id);
        }

        private Button CommandButton(VisualElement parent, ToolCommandId id, string name)
        {
            ToolCommand definition = Array.Find(ToolCommands, command => command.Id == id);
            Button button = Button(parent, name, definition.Label, () => TryExecuteCommand(id));
            button.RegisterCallback<PointerDownEvent>(PrepareCommandPointer, TrickleDown.TrickleDown);
            commandButtons[button] = id;
            button.tooltip = definition.Description + (definition.Gesture.Length == 0 ? "" : " (" + definition.Gesture + ")");
            return button;
        }

        private void PrepareCommandPointer(PointerDownEvent evt)
        {
            if (evt.button != 0 || modal != null) return;
            commandInputFocus = CaptureFocusedInput();
            if (!(evt.currentTarget is Button button) || !commandButtons.TryGetValue(button, out ToolCommandId id) || IsNavigationCommand(id))
            {
                // 메뉴와 이탈 동작은 잘못된 입력도 버리거나 취소할 수 있어야 한다.
                SuspendMobilePanelInput();
                return;
            }
            if (!CommitCommandInput()) { commandInputFocus = null; evt.StopImmediatePropagation(); evt.PreventDefault(); }
        }

        private static bool IsNavigationCommand(ToolCommandId id) => id == ToolCommandId.Open ||
            id == ToolCommandId.NewProject || id == ToolCommandId.ImportProject || id == ToolCommandId.RecentProjects ||
            id == ToolCommandId.Reload || id == ToolCommandId.Backup || id == ToolCommandId.LegacyRecovery || id == ToolCommandId.Exit || id == ToolCommandId.BoardFocus;

        private void RefreshCommandStates()
        {
            List<Button> removed = new List<Button>();
            foreach (KeyValuePair<Button, ToolCommandId> pair in commandButtons)
            {
                if (root != null && !root.Contains(pair.Key)) { removed.Add(pair.Key); continue; }
                ToolCommand definition = Array.Find(ToolCommands, command => command.Id == pair.Value);
                bool allowed = CanExecuteCommand(pair.Value, out string reason);
                pair.Key.SetEnabled(allowed);
                pair.Key.tooltip = definition.Description + (definition.Gesture.Length == 0 ? "" : " (" + definition.Gesture + ")") +
                    (pair.Value == ToolCommandId.Save && folderLabel != null ? "\n" + folderLabel.tooltip : "") +
                    (allowed ? "" : "\n사용 불가: " + reason);
            }
            foreach (Button button in removed) commandButtons.Remove(button);
        }

        private void BuildCommandMenus(VisualElement menuBar)
        {
            commandButtons.Clear();
            string[] names = { "file", "edit", "view", "test", "help" };
            string[] labels = { "파일", "편집", "보기", "시험", "도움말" };
            ToolCommandId[][] groups =
            {
                new[] { ToolCommandId.Open, ToolCommandId.NewProject, ToolCommandId.ImportProject, ToolCommandId.ExportProject, ToolCommandId.RecentProjects, ToolCommandId.Save, ToolCommandId.SaveCopy, ToolCommandId.Reload, ToolCommandId.Backup, ToolCommandId.LegacyRecovery, ToolCommandId.Exit },
                new[] { ToolCommandId.Undo, ToolCommandId.Redo, ToolCommandId.Copy, ToolCommandId.Cut, ToolCommandId.Paste, ToolCommandId.Delete, ToolCommandId.Cancel },
                new[] { ToolCommandId.BoardFocus, ToolCommandId.Windowed, ToolCommandId.Fullscreen },
                new[] { ToolCommandId.Play, ToolCommandId.Validate, ToolCommandId.ReplayTutorial },
                new[] { ToolCommandId.Manual, ToolCommandId.Help }
            };
            for (int index = 0; index < names.Length; index++)
            {
                int group = index;
                Button menuButton = Button(menuBar, "menu-" + names[index], labels[index], () =>
                {
                    if (busy || playRoot != null) return;
                    VisualElement panel = OpenModal(labels[group]);
                    panel.name = "command-menu"; panel.AddToClassList("command-menu"); commandMenuOpen = true;
                    modal.style.backgroundColor = UnityEngine.Color.clear;
                    VisualElement source = menuBar.Q("menu-" + names[group]);
                    UnityEngine.Vector2 position = root.WorldToLocal(new UnityEngine.Vector2(source.worldBound.xMin, source.worldBound.yMax));
                    if (!mobileViewportActive)
                    {
                        panel.style.position = Position.Absolute; panel.style.width = 350;
                        panel.style.left = UnityEngine.Mathf.Max(0, UnityEngine.Mathf.Min(position.x, root.layout.width - 350));
                        panel.style.top = UnityEngine.Mathf.Max(0, position.y);
                    }
                    modal.RegisterCallback<PointerDownEvent>(evt => { if (evt.target == modal) { CloseModal(); evt.StopPropagation(); } });
                    foreach (ToolCommandId id in groups[group])
                    {
                        if (UsesInternalProjects && (id == ToolCommandId.Windowed || id == ToolCommandId.Fullscreen)) continue;
                        ToolCommand definition = Array.Find(ToolCommands, command => command.Id == id);
                        Button item = CommandButton(panel, id, "command-" + id);
                        item.text = ""; item.AddToClassList("command-menu-item");
                        item.Add(new Label(definition.Label) { pickingMode = PickingMode.Ignore });
                        Label gesture = new Label(definition.Gesture) { pickingMode = PickingMode.Ignore };
                        gesture.AddToClassList("command-gesture"); item.Add(gesture);
                    }
                    Button(panel, "close-menu", "닫기 (Esc)", CloseModal);
                    RefreshCommandStates();
                });
                menuButton.RegisterCallback<PointerDownEvent>(PrepareCommandPointer, TrickleDown.TrickleDown);
            }
        }
    }
}
#endif

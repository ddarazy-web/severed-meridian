#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private FocusedInputState modalInputFocus;
        private VisualElement modalPreviousFocus;

        private async UniTask Storage(Action action, bool saving = false)
        {
            if (busy) return;
            if (sharedTutorialDraft != null) { Show("공유 원본 사본을 적용하거나 버린 뒤 파일 작업을 해 주세요."); return; }
            var previousSession = Session;
            FocusedInputState previousInput = commandInputFocus ?? CaptureFocusedInput();
            commandInputFocus = null;
            var pendingInputs = CaptureRecoveryInputs();
            if (HasPendingAuthoringInput()) SuspendMobilePanelInput();

            busy = true; editor.SetEnabled(false); Show("파일 처리 중…");
            RefreshCommandStates();
            if (saving) BeginManualSave();
            try
            {
                await UniTask.RunOnThreadPool(action);
                manualFileFailure = null;
                if (saving) CompleteManualSave();
                if (!ReferenceEquals(previousSession, Session)) CancelPendingInput();
                Show("파일 처리가 완료되었습니다.");
            }
            catch (Exception error)
            {
                if (saving) CompleteManualSave(error);
                else { manualSaveOwner = Session; manualFileFailure = error.Message; }
                Show(error.Message);
            }
            finally
            {
                busy = false;
                if (this != null && editor != null)
                {
                    editor.SetEnabled(modal == null); Refresh();
                    RestoreMobilePanelInputs();
                    if (!ReferenceEquals(previousSession, Session)) RememberInternalProject();
                    if (ReferenceEquals(previousSession, Session) && modal == null)
                    {
                        RestoreRecoveryInputs(pendingInputs);
                        RestoreFocusedInput(previousInput);
                        UpdateManualSaveStatus();
                    }
                }
            }
        }

        private void WithDirty(Func<string, bool> action) => WithDirtyFor("프로젝트 열기", action);

        private void WithDirtyFor(string operation, Func<string, bool> action)
        {
            if (!HasUnsavedWork) { Storage(() => action("Cancel")).Forget(); return; }
            VisualElement panel = OpenModal(operation + " 전 현재 작업 확인");
            panel.Add(new Label("현재 작업에 저장하지 않은 변경이 있습니다. 저장하거나 버린 뒤 선택한 작업을 진행합니다. 취소하면 입력 중인 내용까지 유지합니다.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            Button(panel, "save-and-continue", "저장 후 " + operation, () => { CloseModal(); ContinueAfterManualInput(() => Storage(() => action("Save")).Forget()).Forget(); });
            Button(panel, "discard-and-continue", "버리고 " + operation, () => { CloseModal(); Storage(() => action("Discard")).Forget(); });
            Button(panel, "cancel", "취소", CloseModal);
        }

        private void NewProject()
        {
            if (busy) return;
            ChooseFolder(path =>
            {
                try
                {
                    var template = LevelToolDocuments.NewProject();
                    WithDirtyFor("새 프로젝트 만들기", choice => Workspace.Create(path, template, choice));
                }
                catch (Exception error) { Show(error.Message); }
            }, true);
        }

        private VisualElement OpenModal(string title)
        {
            CancelBoardTouch();
            CloseModal();
            modalInputFocus = commandInputFocus ?? CaptureFocusedInput(); commandInputFocus = null;
            modalPreviousFocus = root.focusController?.focusedElement as VisualElement;
            pendingInputDirty = HasPendingAuthoringInput();
            SuspendMobilePanelInput();
            modal = new VisualElement { name = "modal" }; modal.AddToClassList("modal"); root.Add(modal);
            editor?.SetEnabled(false);
            VisualElement panel = new VisualElement { focusable = true }; panel.AddToClassList("dialog"); modal.Add(panel);
            panel.schedule.Execute(() =>
            {
                if (modal == null || !modal.Contains(panel)) return;
                VisualElement first = panel.Q<TextField>();
                if (first == null) first = panel.Q<Button>();
                (first ?? panel).Focus();
            });
            VisualElement content = panel;
            if (mobileViewportActive)
            {
                mobileDialogFrame = panel;
                mobileDialogScroll = new ScrollView { name = "mobile-dialog-scroll" };
                mobileDialogScroll.style.flexGrow = 1; panel.Add(mobileDialogScroll);
                content = mobileDialogScroll.contentContainer;
                ApplyMobileViewport(mobileScreenPixels, mobileSafePixels, mobileKeyboardPixels);
            }
            content.Add(new Label(title)); panel.schedule.Execute(ApplyToolSkin); return content;
        }
        private void CloseModal()
        {
            if (modal == null) return;
            CancelToolRecordConfirmation();
            modal.RemoveFromHierarchy(); modal = null; commandMenuOpen = false;
            mobileDialogFrame = null; mobileDialogScroll = null;
            editor?.SetEnabled(!busy);
            if (!busy)
            {
                RestoreMobilePanelInputs();
                if (modalInputFocus != null) RestoreFocusedInput(modalInputFocus);
                else if (modalPreviousFocus != null && root.Contains(modalPreviousFocus)) modalPreviousFocus.Focus();
            }
            modalInputFocus = null; modalPreviousFocus = null;
            RefreshCommandStates();
        }

        private bool UsesInternalProjects => Application.platform == RuntimePlatform.Android;
        private string InternalProjectsRoot => LevelToolProjectAccess.InternalRoot(string.IsNullOrEmpty(recoveryDirectory)
            ? Application.persistentDataPath : recoveryDirectory);

        private void ChooseInternalProject(Action<string> selected, bool create)
        {
            VisualElement panel = OpenModal(create ? "새 내부 프로젝트" : "내부 프로젝트 열기");
            panel.Add(new Label("프로젝트는 이 앱의 내부 저장 공간에 JSON으로 저장됩니다."));
            TextField name = new TextField("프로젝트 이름") { name = "project-name" };
            panel.Add(name);
            Label error = new Label(); panel.Add(error);
            ScrollView projects = new ScrollView(); projects.style.height = 260; panel.Add(projects);
            try
            {
                foreach (string project in LevelToolProjectAccess.List(InternalProjectsRoot))
                    Button(projects, "internal-project", project, () => name.value = project);
            }
            catch (Exception failure) { error.text = failure.Message; }
            Button(panel, "choose-project", create ? "이 이름으로 만들기" : "선택한 프로젝트 열기", () =>
            {
                try
                {
                    string folder = LevelToolProjectAccess.Resolve(InternalProjectsRoot, name.value, create);
                    CloseModal(); selected(folder);
                }
                catch (Exception failure) { error.text = failure.Message; }
            });
            Button(panel, "cancel", "취소", CloseModal);
        }

        private void ChooseFolder(Action<string> selected, bool create = false)
        {
            if (busy) return;
            if (UsesInternalProjects) { ChooseInternalProject(selected, create); return; }
            VisualElement panel = OpenModal("작업 폴더 선택");
            TextField path = new TextField("폴더 경로") { name = "folder-path", value = Workspace.Folder ?? Application.persistentDataPath };
            panel.Add(path);
            Label error = new Label(); panel.Add(error);
            ScrollView folders = new ScrollView(); folders.style.height = 260; panel.Add(folders);
            VisualElement owner = modal;
            int navigation = 0;
            async UniTask Browse(string directory)
            {
                int revision = ++navigation;
                try
                {
                    path.value = Path.GetFullPath(directory); folders.Clear(); error.text = "";
                    string target = path.value;
                    string[] entries = await UniTask.RunOnThreadPool(() => LevelToolFolderAccess.List(target));
                    if (this == null || owner != modal || revision != navigation) return;
                    foreach (string child in entries)
                        Button(folders, "folder", child, () => Browse(child).Forget());
                }
                catch (Exception failure) { if (owner == modal && revision == navigation) error.text = failure.Message; }
            }
            Button(panel, "browse", "경로 보기", () => Browse(path.value).Forget());
            Button(panel, "choose-folder", "이 폴더 선택", () =>
            {
                try { string value = Path.GetFullPath(path.value); CloseModal(); selected(value); }
                catch (Exception failure) { error.text = failure.Message; }
            });
            panel.Add(new Label("새 프로젝트·사본 저장: 아직 없는 하위 폴더 경로를 입력해도 됩니다."));
            Button(panel, "cancel", "취소", CloseModal);
            Browse(path.value).Forget();
        }
    }
}
#endif

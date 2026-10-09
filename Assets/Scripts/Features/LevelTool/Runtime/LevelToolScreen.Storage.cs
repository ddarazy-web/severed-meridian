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

        private async UniTask Storage(Action action)
        {
            if (busy) return;
            if (sharedTutorialDraft != null) { Show("공유 원본 사본을 적용하거나 버린 뒤 파일 작업을 해 주세요."); return; }
            var previousSession = Session;
            FocusedInputState previousInput = commandInputFocus ?? CaptureFocusedInput();
            commandInputFocus = null;
            busy = true; editor.SetEnabled(false); Show("파일 처리 중…");
            RefreshCommandStates();
            try
            {
                await UniTask.RunOnThreadPool(action);
                if (!ReferenceEquals(previousSession, Session)) CancelPendingInput();
                Show("파일 처리가 완료되었습니다.");
            }
            catch (Exception error) { Show(error.Message); }
            finally
            {
                busy = false;
                if (this != null && editor != null)
                {
                    editor.SetEnabled(modal == null); Refresh();
                    if (ReferenceEquals(previousSession, Session) && modal == null) RestoreFocusedInput(previousInput);
                }
            }
        }

        private void WithDirty(Func<string, bool> action)
        {
            if (Session?.IsDirty != true) { Storage(() => action("Cancel")).Forget(); return; }
            VisualElement panel = OpenModal("현재 수정 내용이 저장되지 않았습니다.");
            panel.Add(new Label("저장 후 계속하거나, 버리고 다른 자료를 열 수 있습니다. 취소하면 현재 편집을 유지합니다."));
            Button(panel, "save-and-continue", "저장 후 계속", () => { CloseModal(); Storage(() => action("Save")).Forget(); });
            Button(panel, "discard-and-continue", "버리고 계속", () => { CloseModal(); Storage(() => action("Discard")).Forget(); });
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
                    WithDirty(choice => Workspace.Create(path, template, choice));
                }
                catch (Exception error) { Show(error.Message); }
            });
        }

        private VisualElement OpenModal(string title)
        {
            CloseModal();
            modalInputFocus = commandInputFocus ?? CaptureFocusedInput(); commandInputFocus = null;
            modalPreviousFocus = root.focusController?.focusedElement as VisualElement;
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
            panel.Add(new Label(title)); panel.schedule.Execute(ApplyToolSkin); return panel;
        }
        private void CloseModal()
        {
            if (modal == null) return;
            modal.RemoveFromHierarchy(); modal = null; commandMenuOpen = false;
            editor?.SetEnabled(!busy);
            if (!busy)
            {
                if (modalInputFocus != null) RestoreFocusedInput(modalInputFocus);
                else if (modalPreviousFocus != null && root.Contains(modalPreviousFocus)) modalPreviousFocus.Focus();
            }
            modalInputFocus = null; modalPreviousFocus = null;
            RefreshCommandStates();
        }

        private void ChooseFolder(Action<string> selected)
        {
            if (busy) return;
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

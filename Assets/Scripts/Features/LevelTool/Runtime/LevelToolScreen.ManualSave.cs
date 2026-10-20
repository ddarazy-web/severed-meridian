#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Globalization;
using System.Linq;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private JArray suspendedInputs;
        private bool pendingInputDirty;
        private bool manualSaveInProgress;
        private string manualSaveFailure;
        private string manualFileFailure;
        private AuthoringEditSession manualSaveOwner;

        private void BeginManualSave()
        {
            manualSaveOwner = Session;
            manualSaveInProgress = true;
            UpdateManualSaveStatus();
        }

        private void CompleteManualSave(Exception failure = null)
        {
            manualSaveOwner = Session;
            manualSaveInProgress = false;
            manualSaveFailure = failure?.Message;
            if (failure == null) manualFileFailure = null;
            UpdateManualSaveStatus();
        }

        private bool HasPendingAuthoringInput() => editor != null && editor.Query<VisualElement>().ToList().Any(IsPendingAuthoringInput);
        private static bool IsPendingAuthoringInput(VisualElement field)
        {
            if (field is TextField text) return text.text != text.value;
            if (field is IntegerField integer) return !int.TryParse(integer.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) || i != integer.value;
            if (field is LongField wide) return !long.TryParse(wide.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l) || l != wide.value;
            if (field is FloatField single) return !float.TryParse(single.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) || float.IsNaN(f) || float.IsInfinity(f) || f != single.value;
            if (field is DoubleField number) return !double.TryParse(number.text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) || double.IsNaN(d) || double.IsInfinity(d) || d != number.value;
            return false;
        }
        private bool HasUnsavedWork => Session?.IsDirty == true || sharedTutorialDraft?.IsChanged == true ||
            (modal != null ? pendingInputDirty : HasPendingAuthoringInput());

        private void OnManualInput(InputEvent evt)
        {
            if (editor != null && evt.target is VisualElement field && editor.Contains(field))
                root.schedule.Execute(UpdateManualSaveStatus);
        }

        private void UpdateManualSaveStatus()
        {
            if (folderLabel == null || Session == null) return;
            if (!ReferenceEquals(manualSaveOwner, Session))
            {
                manualSaveOwner = Session;
                manualSaveFailure = null; manualFileFailure = null; manualSaveInProgress = false;
            }
            if (modal == null) pendingInputDirty = HasPendingAuthoringInput();
            bool dirty = Session.IsDirty || pendingInputDirty || sharedTutorialDraft?.IsChanged == true;
            string state = manualSaveInProgress ? "저장 중" : manualSaveFailure != null ? "저장 실패" : manualFileFailure != null ? "파일 작업 실패" :
                busy ? "처리 중" : pendingInputDirty ? "입력 확인 필요" : Workspace.IsRecovery ? "사본 저장 필요" :
                sharedTutorialDraft?.IsChanged == true ? "공유 사본 미적용" : dirty ? "저장하지 않음" : "저장됨";
            folderLabel.text = (dirty ? "* " : "") + state;
            string saveInstruction = Workspace.IsRecovery ? "파일 메뉴의 사본 저장으로 새 폴더에 저장하세요." :
                sharedTutorialDraft != null ? "공유 사본을 원본에 적용한 뒤 프로젝트를 저장하세요." :
                mobileViewportActive ? "저장 버튼으로 저장하세요." : "저장 버튼 또는 Ctrl+S로 저장하세요.";
            folderLabel.tooltip = folderLabel.text + (Workspace.IsRecovery ? " · 복구본은 사본 저장 필요" : "") +
                (sharedTutorialDraft != null ? " · 공유 원본 사본" + (sharedTutorialDraft.IsChanged ? " 미적용" : "") : "") +
                (Workspace.Folder != null ? "\n" + Workspace.Folder : "") + (manualSaveFailure == null ? "" : "\n저장 실패 원인: " + manualSaveFailure) +
                (manualFileFailure == null ? "" : "\n파일 작업 실패 원인: " + manualFileFailure) +
                "\n자동 저장하지 않습니다. " + saveInstruction + " 강제 종료 시 미저장 변경은 사라집니다.";
            if (levelTitle != null) levelTitle.text = (dirty ? "* " : "") + levelTitle.text.TrimStart('*', ' ');
            var save = root.Q<Button>("save");
            if (save != null)
            {
                save.text = manualSaveInProgress ? "저장 중…" : manualSaveFailure != null ? "다시 저장" : dirty ? "저장 *" : "저장";
                save.style.minWidth = mobileViewportActive ? 0 : save.text.Length * 14 + 40;
                if (mobileViewportActive)
                {
                    save.style.paddingLeft = 4;
                    var icon = save.Q("tool-icon");
                    if (icon != null) icon.style.display = DisplayStyle.None;
                }
                bool allowed = CanExecuteCommand(ToolCommandId.Save, out string reason);
                save.tooltip = folderLabel.tooltip + (allowed ? "" : "\n사용 불가: " + reason);
            }
            var details = root.Q<Button>("save-status-details");
            if (details != null) details.text = manualSaveFailure != null || manualFileFailure != null ? "실패 원인" : "저장 안내";
        }

        private void OpenSaveDetails()
        {
            if (busy) return;
            UpdateManualSaveStatus();
            VisualElement panel = OpenModal("저장 상태 안내");
            VisualElement content = panel;
            if (!mobileViewportActive)
            {
                var scroll = new ScrollView { name = "save-details-scroll" };
                scroll.style.maxHeight = 420; panel.Add(scroll); content = scroll.contentContainer;
            }
            content.Add(new Label(folderLabel.text) { name = "save-details-state" });
            string instruction = Workspace.IsRecovery ? "파일 메뉴의 사본 저장을 사용하세요." :
                sharedTutorialDraft != null ? "공유 사본을 원본에 적용한 뒤 프로젝트를 저장하세요." :
                mobileViewportActive ? "저장 버튼으로 저장하세요." : "저장 버튼 또는 Ctrl+S로 저장하세요.";
            content.Add(new Label("변경 내용은 메모리에만 있습니다. " + instruction +
                " 강제 종료하면 마지막 저장 이후의 변경은 사라질 수 있습니다.") { name = "save-details-policy" });
            if (pendingInputDirty) content.Add(new Label("입력 중인 내용이 있습니다. 글자 조합을 마치고 올바른 숫자를 입력한 뒤 저장하세요."));
            if (sharedTutorialDraft != null) content.Add(new Label("공유 사본을 원본에 적용한 뒤 프로젝트를 저장하세요. 원본 적용만으로는 파일에 저장되지 않습니다."));
            if (Workspace.IsRecovery) content.Add(new Label("과거 복구본은 파일 메뉴의 사본 저장으로 새 폴더에 저장하세요. 기존 원본과 복구 기록은 유지됩니다."));
            if (manualSaveFailure != null) content.Add(new Label("최근 저장 실패\n" + manualSaveFailure + "\n원인을 해결한 뒤 다시 저장하세요.") { name = "save-details-error" });
            if (manualFileFailure != null) content.Add(new Label("최근 파일 작업 실패\n" + manualFileFailure +
                "\n파일 작업이 완료되지 않았습니다. 현재 편집 내용과 저장 위치를 확인한 뒤 다시 시도하세요.") { name = "save-details-file-error" });
            content.Add(new Label("저장 위치\n" + (Workspace.Folder ?? "아직 작업 폴더를 열지 않았습니다.")) { name = "save-details-path" });
            foreach (var label in content.Query<Label>().ToList())
            {
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.flexShrink = 0;
                label.style.marginBottom = 12;
            }
            Button(panel, "close-save-details", "닫고 편집 계속", CloseModal);
        }

        private async UniTask ContinueAfterManualInput(Action action)
        {
            var session = Session;
            do
            {
                if (!CommitCommandInput()) { UpdateManualSaveStatus(); return; }
                if (committingCommandInput)
                    await UniTask.WaitUntil(() => !committingCommandInput || this == null || !isActiveAndEnabled);
                if (this == null || !isActiveAndEnabled || !ReferenceEquals(Session, session)) return;
            } while (HasPendingAuthoringInput());
            action();
        }

        private void DiscardUncommittedInputs()
        {
            // 숨긴 입력의 Blur 가드를 해제한 뒤 값은 그대로 두고 미확정 문자열만 버린다.
            foreach (var pending in mobilePendingInputs.Values.ToArray()) pending.Restore(true);
            mobilePendingInputs.Clear();
            if (editor != null)
            {
                foreach (var field in editor.Query<TextField>().ToList()) field.SetValueWithoutNotify(field.value);
                foreach (var field in editor.Query<IntegerField>().ToList()) field.SetValueWithoutNotify(field.value);
                foreach (var field in editor.Query<LongField>().ToList()) field.SetValueWithoutNotify(field.value);
                foreach (var field in editor.Query<FloatField>().ToList()) field.SetValueWithoutNotify(field.value);
                foreach (var field in editor.Query<DoubleField>().ToList()) field.SetValueWithoutNotify(field.value);
            }
            pendingInputDirty = false;
        }
    }
}
#endif

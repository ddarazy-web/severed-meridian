#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private readonly HashSet<KeyCode> heldShortcutKeys = new HashSet<KeyCode>();
        private bool committingCommandInput;
        private bool commandInputRefreshPending;
        private int inputCommitRevision;

        private void RegisterShortcuts()
        {
            UnregisterShortcuts();
            root.RegisterCallback<KeyDownEvent>(OnShortcutKeyDown, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyUpEvent>(OnShortcutKeyUp, TrickleDown.TrickleDown);
            root.RegisterCallback<FocusOutEvent>(OnShortcutFocusOut);
        }
        private void UnregisterShortcuts()
        {
            root?.UnregisterCallback<KeyDownEvent>(OnShortcutKeyDown, TrickleDown.TrickleDown);
            root?.UnregisterCallback<KeyUpEvent>(OnShortcutKeyUp, TrickleDown.TrickleDown);
            root?.UnregisterCallback<FocusOutEvent>(OnShortcutFocusOut);
            heldShortcutKeys.Clear();
        }
        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            heldShortcutKeys.Clear();
            CancelFlowTool(); wireDrawing = false; wireRoute.Clear();
        }
        private void OnShortcutKeyUp(KeyUpEvent evt) => heldShortcutKeys.Remove(evt.keyCode);
        private void OnShortcutFocusOut(FocusOutEvent evt)
        {
            if (evt.relatedTarget is VisualElement next && root.Contains(next)) return;
            // 모달로 포커스가 옮겨지는 프레임은 제외하고 패널 밖으로 떠난 키 상태만 정리한다.
            root.schedule.Execute(() =>
            {
                VisualElement focused = root?.focusController?.focusedElement as VisualElement;
                if (focused == null || !root.Contains(focused)) heldShortcutKeys.Clear();
            }).StartingIn(50);
        }

        private void OnShortcutKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Tab || evt.altKey || evt.commandKey) return;
            ToolCommandId? command = null;
            if (evt.keyCode == KeyCode.Escape) command = ToolCommandId.Cancel;
            else if (evt.ctrlKey && evt.keyCode == KeyCode.O && !evt.shiftKey) command = ToolCommandId.Open;
            else if (evt.ctrlKey && evt.keyCode == KeyCode.S) command = evt.shiftKey ? ToolCommandId.SaveCopy : ToolCommandId.Save;
            else if (evt.ctrlKey && evt.keyCode == KeyCode.Z) command = evt.shiftKey ? ToolCommandId.Redo : ToolCommandId.Undo;
            else if (evt.ctrlKey && evt.keyCode == KeyCode.Y && !evt.shiftKey) command = ToolCommandId.Redo;
            else if (!evt.ctrlKey && !evt.shiftKey && evt.keyCode == KeyCode.F1) command = ToolCommandId.Help;
            else if (!evt.ctrlKey && !evt.shiftKey && evt.keyCode == KeyCode.F5) command = ToolCommandId.Play;
            else if (!evt.ctrlKey && evt.shiftKey && evt.keyCode == KeyCode.Space) command = ToolCommandId.BoardFocus;

            // 최상위 창이 배경 제작 명령을 소유한다. Tab과 기본 필드 편집은 UI Toolkit에 맡긴다.
            if (modal != null)
            {
                if (command == null) return;
                VisualElement modalFocus = root?.focusController?.focusedElement as VisualElement ?? evt.target as VisualElement;
                if ((command == ToolCommandId.Undo || command == ToolCommandId.Redo) && FindCommandInput(modalFocus) != null) return;
                if (heldShortcutKeys.Add(evt.keyCode) && command == ToolCommandId.Cancel) TryExecuteCommand(ToolCommandId.Cancel);
                evt.StopImmediatePropagation(); evt.PreventDefault(); return;
            }
            VisualElement focused = root?.focusController?.focusedElement as VisualElement ?? evt.target as VisualElement;
            VisualElement input = FindCommandInput(focused);
            if (!string.IsNullOrEmpty(Input.compositionString))
            {
                if (command == ToolCommandId.Save || command == ToolCommandId.SaveCopy)
                { Show("글자 조합을 마친 뒤 저장하세요. 입력은 그대로 유지됩니다."); evt.StopImmediatePropagation(); evt.PreventDefault(); }
                return;
            }
            if (input != null)
            {
                if (command != ToolCommandId.Save && command != ToolCommandId.SaveCopy && command != ToolCommandId.Help) return;
            }
            if (tutorialPick != null && input == null && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter))
            {
                if (heldShortcutKeys.Add(evt.keyCode) && !busy) ConfirmTutorialPick();
                evt.StopImmediatePropagation(); evt.PreventDefault(); return;
            }
            if (command == null) return;
            if (command == ToolCommandId.BoardFocus && (board == null || focused == null || (focused != board && !board.Contains(focused)))) return;
            if (heldShortcutKeys.Add(evt.keyCode)) TryExecuteCommand(command.Value);
            evt.StopImmediatePropagation(); evt.PreventDefault();
        }

        private static VisualElement FindCommandInput(VisualElement focused)
        {
            for (VisualElement current = focused; current != null; current = current.parent)
                if (current is TextField || current is IntegerField || current is FloatField || current is DoubleField || current is LongField)
                    return current;
            return null;
        }

        private bool CommitCommandInput()
        {
            if (committingCommandInput) return true;
            if (!string.IsNullOrEmpty(Input.compositionString))
            { Show("글자 조합을 마친 뒤 실행하세요. 입력은 그대로 유지됩니다."); return false; }
            VisualElement focused = root?.focusController?.focusedElement as VisualElement;
            VisualElement input = FindCommandInput(focused);
            if (input == null) return true;
            int integer = 0;
            long longValue = 0;
            float floatValue = 0;
            double doubleValue = 0;
            if (input is IntegerField number && !int.TryParse(number.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
            { Show("올바른 정수를 입력한 뒤 실행하세요. 입력은 그대로 유지됩니다."); return false; }
            if ((input is LongField longField && !long.TryParse(longField.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out longValue)) ||
                (input is FloatField floatField && (!float.TryParse(floatField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue) || float.IsNaN(floatValue) || float.IsInfinity(floatValue))) ||
                (input is DoubleField doubleField && (!double.TryParse(doubleField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleValue) || double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))))
            { Show("올바른 숫자를 입력한 뒤 실행하세요. 입력은 그대로 유지됩니다."); return false; }
            FocusedInputState snapshot = CaptureFocusedInput();
            bool changed = input is TextField pendingText && pendingText.value != pendingText.text ||
                input is IntegerField pendingInteger && pendingInteger.value != integer ||
                input is LongField pendingLong && pendingLong.value != longValue ||
                input is FloatField pendingFloat && pendingFloat.value != floatValue ||
                input is DoubleField pendingDouble && pendingDouble.value != doubleValue;
            if (!changed) return true;
            committingCommandInput = true;
            try
            {
                // 지연 필드는 Blur 시점보다 먼저 값을 확정해야 저장이 마지막 글자를 놓치지 않는다.
                if (input is TextField text) text.value = text.text;
                else if (input is IntegerField numeric) numeric.value = integer;
                else if (input is LongField longNumeric) longNumeric.value = longValue;
                else if (input is FloatField floatNumeric) floatNumeric.value = floatValue;
                else if (input is DoubleField doubleNumeric) doubleNumeric.value = doubleValue;
                focused?.Blur();
            }
            catch { committingCommandInput = false; throw; }
            FinishCommandInputCommit(snapshot, ++inputCommitRevision).Forget();
            return true;
        }

        private async UniTask FinishCommandInputCommit(FocusedInputState snapshot, int revision)
        {
            // KeyDown 처리 안에서 발생한 Change/Blur 이벤트는 큐에 들어간다.
            // 해당 이벤트와 세션 편집이 끝나기 전에는 저장 스레드를 시작하지 않는다.
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            if (this == null || !isActiveAndEnabled || revision != inputCommitRevision) return;
            committingCommandInput = false;
            if (commandInputRefreshPending) { commandInputRefreshPending = false; Refresh(); }
            RestoreFocusedInput(snapshot);
        }
    }
}
#endif

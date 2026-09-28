using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        private Action cancelMergeDrag;

        private void BuildMergeList(VisualElement parent, BoardCoordinate cell, List<BoardCoordinate> ordered)
        {
            LevelDefinition owner = level; string snapshot = JsonUtility.ToJson(level);
            bool Current() => level == owner && selected.HasValue && selected.Value.Equals(cell) && JsonUtility.ToJson(level) == snapshot;
            void Apply(List<BoardCoordinate> order)
            {
                if (!Current()) return;
                operation.text = LevelFlowEditing.SetMerge(level, cell, order) ?? "합류 우선순위를 변경했습니다. Undo 한 번으로 복구합니다.";
                Refresh();
            }
            parent.Add(new Label("합류 우선순위 · 핸들을 드래그 (1번 우선)"));
            var list = new ListView(ordered, 30, () => new VisualElement(), (row, index) =>
            {
                row.Clear(); row.style.flexDirection = FlexDirection.Row;
                var label = new Label($"{index + 1}. {ordered[index]}" + (level.Flow.Portals.Any(p => p.Entrance.Equals(ordered[index])) ? " 통로" : ""));
                label.style.flexGrow = 1; row.Add(label);
                foreach (int delta in new[] { -1, 1 })
                {
                    int destination = index + delta;
                    var button = new Button(() =>
                    {
                        var changed = ordered.ToList(); (changed[index], changed[destination]) = (changed[destination], changed[index]); Apply(changed);
                    }) { text = delta < 0 ? "↑" : "↓", name = "merge-" + index + "-" + delta };
                    button.SetEnabled(destination >= 0 && destination < ordered.Count); row.Add(button);
                }
            }) { name = "merge-order", reorderable = ordered.Count > 1, reorderMode = ListViewReorderMode.Animated, selectionType = SelectionType.Single };
            list.style.height = Math.Max(34, Math.Min(5, ordered.Count) * 30 + 4);
            bool cancelled = false, pointerDown = false;
            Action cancel = () =>
            {
                cancelled = true;
                if (!pointerDown) return;
                pointerDown = false;
                list.schedule.Execute(() => { if (list.panel != null) Refresh(); });
            };
            cancelMergeDrag = cancel;
            list.RegisterCallback<AttachToPanelEvent>(evt => { if (evt.target == list) cancelMergeDrag = cancel; });
            list.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target != list) return;
                cancelled = true; if (cancelMergeDrag == cancel) cancelMergeDrag = null;
            });
            list.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0) { cancelled = false; pointerDown = true; } }, TrickleDown.TrickleDown);
            list.RegisterCallback<PointerUpEvent>(_ => pointerDown = false);
            list.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape) { cancel(); evt.StopPropagation(); } }, TrickleDown.TrickleDown);
            // Unity의 드롭 정리가 끝난 뒤 저장/화면 갱신한다.
            list.itemIndexChanged += (_, _) => list.schedule.Execute(() => { if (!cancelled && list.panel != null) Apply(ordered.ToList()); });
            list.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.StopPropagation();
                evt.menu.AppendAction("합류 우선순위 초기화", _ =>
                {
                    if (!Current()) return;
                    operation.text = LevelFlowEditing.RemoveMerge(level, cell) ?? "합류 우선순위를 초기화했습니다.";
                    Refresh();
                });
            }));
            parent.Add(list);
        }
    }
}

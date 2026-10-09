#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private string navigationPage;

        private string WorkspacePage => inspectorPage == "자동 시험" ? "검사·봇 시험" : inspectorPage == "시험 기록" ? "시험 기록" :
            inspectorPage.StartsWith("튜토리얼", StringComparison.Ordinal) ? "튜토리얼" :
            inspectorPage == "공급" || inspectorPage == "흐름" || inspectorPage == "연결" ? "흐름·공급" : "보드 편집";

        private void SelectWorkspacePage(string page)
        {
            if (busy || modal != null || playRoot != null) return;
            if (sharedTutorialDraft != null && page != "튜토리얼") { Show("공유 원본 사본을 적용하거나 버린 뒤 다른 작업으로 이동하세요."); return; }
            root.focusController?.focusedElement?.Blur();
            CancelPendingInput(); brush = null;
            inspectorPage = page == "흐름·공급" ? "흐름" : page == "튜토리얼" ? "튜토리얼" :
                page == "검사·봇 시험" ? "자동 시험" : page == "시험 기록" ? "시험 기록" : "기본";
            Refresh();
        }

        private void RefreshNavigation()
        {
            string page = WorkspacePage;
            string[] labels = { "보드 편집", "흐름·공급", "튜토리얼", "검사·봇 시험", "시험 기록" };
            string[] ids = { "edit", "flow", "tutorial", "test", "records" };
            for (int i = 0; i < ids.Length; i++)
            {
                var tab = root.Q<Button>("workspace-" + ids[i]);
                tab.EnableInClassList("selected", page == labels[i]);
                tab.SetEnabled(!busy && (sharedTutorialDraft == null || labels[i] == "튜토리얼"));
            }
            if (navigationPage != page)
            {
                navigationPage = page; pageTools.Clear();
                string[] choices = page == "보드 편집" ? new[] { "기본", "모양" } : page == "흐름·공급" ? new[] { "흐름", "공급", "연결" } :
                    page == "튜토리얼" ? new[] { "튜토리얼", "튜토리얼 공급" } : Array.Empty<string>();
                foreach (string value in choices)
                    Button(pageTools, "page-" + value, value == "기본" ? "배치·생성구" : value, () =>
                    {
                        if (sharedTutorialDraft != null && value != "튜토리얼") { Show("공유 사본 편집을 먼저 마쳐 주세요."); return; }
                        root.focusController?.focusedElement?.Blur(); CancelPendingInput(); brush = null; inspectorPage = value; Refresh();
                    });
                if (choices.Length == 0) pageTools.Add(new Label(page == "시험 기록" ? "저장된 시험을 조회·재생·보관합니다." : "현재 편집본의 사본으로 봇 시험을 실행합니다."));
            }
            foreach (var button in pageTools.Query<Button>().ToList()) button.EnableInClassList("selected", button.name == "page-" + inspectorPage);
            levelTitle.text = Session == null ? "레벨 제작 도구" : "레벨 " + Session.Get(Session.SelectedLevelId).Data["levelNumber"] + "  ·  " + Session.Get(Session.SelectedLevelId).Data["displayName"];
            levelTitle.tooltip = levelTitle.text;
            UpdatePanelVisibility();
        }

        private sealed class FocusedInputState
        {
            public VisualElement Element;
            public int[] Path;
            public Type Type;
            public int Cursor, Selection;
        }

        private FocusedInputState CaptureFocusedInput()
        {
            var focused = root?.focusController?.focusedElement as VisualElement;
            if (focused == null || !root.Contains(focused)) return null;
            // 내부 입력 요소 대신 필드 단위로 기억하여 재구성 뒤에도 같은 위치를 찾는다.
            VisualElement element = focused;
            for (VisualElement ancestor = focused; ancestor != null && ancestor != root; ancestor = ancestor.parent)
                if (ancestor is TextField || ancestor is IntegerField || ancestor is FloatField) { element = ancestor; break; }
            var path = new List<int>();
            for (VisualElement current = element; current != root && current.parent != null; current = current.parent)
                path.Insert(0, current.parent.hierarchy.IndexOf(current));
            return new FocusedInputState { Element = element, Path = path.ToArray(), Type = element.GetType(),
                Cursor = element is TextField text ? text.cursorIndex : 0, Selection = element is TextField selected ? selected.selectIndex : 0 };
        }

        private void RestoreFocusedInput(FocusedInputState state)
        {
            if (state == null || modal != null || root == null) return;
            VisualElement element = state.Element;
            if (!root.Contains(element))
            {
                element = root;
                foreach (int index in state.Path)
                {
                    if (index >= element.hierarchy.childCount) return;
                    element = element.hierarchy[index];
                }
            }
            if (element.GetType() != state.Type || !element.enabledInHierarchy) return;
            element.Focus();
            if (element is TextField text) text.SelectRange(Math.Min(state.Cursor, text.value.Length), Math.Min(state.Selection, text.value.Length));
        }
    }
}
#endif

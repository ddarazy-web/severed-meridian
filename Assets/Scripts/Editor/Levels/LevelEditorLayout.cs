using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        private int toolPage;
        private int inspectorPage;
        private Foldout validationDrawer;
        private VisualElement selectionPage;
        private VisualElement settingsPage;
        private VisualElement inspectorTabs;

        private Toggle PanelToggle(string label, string panelName, string name)
        {
            Toggle toggle = new Toggle(label) { name = name, value = true, tooltip = label + " 패널 표시/숨기기" };
            toggle.RegisterValueChangedCallback(evt =>
            {
                board?.CancelStroke();
                rootVisualElement.Q(panelName).style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            return toggle;
        }

        private VisualElement CreateToolTabs()
        {
            VisualElement tabs = new VisualElement();
            tabs.AddToClassList("tab-bar");
            string[] labels = { "배치", "흐름", "목록" };
            for (int i = 0; i < labels.Length; i++)
            {
                int page = i;
                Button tab = new Button(() =>
                {
                    board.CancelStroke();
                    toolPage = page;
                    board.Brush = page == 1 ? LevelBrush.Flow : LevelBrush.Select;
                    if (page == 1) flowTool = FlowTool.Select;
                    operation.text = page == 0 ? "배치할 종류를 선택하고 보드를 클릭하거나 드래그하세요." :
                        page == 1 ? "흐름 도구를 선택하세요. 장치 연결은 보드에서 발전기를 선택해 시작합니다." :
                        "배치된 항목을 클릭하면 해당 위치와 속성을 확인할 수 있습니다.";
                    Refresh();
                }) { text = labels[i], name = "tool-tab-" + i };
                tab.AddToClassList("tab");
                tab.EnableInClassList("active", toolPage == i);
                tabs.Add(tab);
            }
            return tabs;
        }

        private void ArrangeInspector()
        {
            settingsPage = new VisualElement { name = "level-settings-page" };
            // 기존 바인딩과 필드 인스턴스를 유지한 채 화면에서만 분리한다.
            while (properties.childCount > 0) settingsPage.Add(properties[0]);
            selectionPage = new VisualElement { name = "selection-page" };
            selectionPage.Add(selectedProperties);
            selectionPage.Add(flowProperties);
            inspectorTabs = new VisualElement();
            inspectorTabs.AddToClassList("tab-bar");
            string[] labels = { "선택 속성", "레벨 설정" };
            for (int i = 0; i < labels.Length; i++)
            {
                int page = i;
                Button tab = new Button(() => ShowInspectorPage(page)) { text = labels[i], name = "inspector-tab-" + i };
                tab.AddToClassList("tab");
                inspectorTabs.Add(tab);
            }
            properties.Add(inspectorTabs);
            properties.Add(selectionPage);
            properties.Add(settingsPage);
            ShowInspectorPage(inspectorPage);
        }

        private void ShowInspectorPage(int page)
        {
            inspectorPage = page;
            if (selectionPage == null || settingsPage == null || inspectorTabs == null) return;
            selectionPage.style.display = page == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            settingsPage.style.display = page == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < inspectorTabs.childCount; i++)
                inspectorTabs[i].EnableInClassList("active", i == page);
        }
    }
}

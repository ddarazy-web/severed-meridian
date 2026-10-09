#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private VisualElement leftPanel, rightPanel, levelPanel, materialPanel, workspaceTabs, pageTools, rightTabs;
        private ScrollView validationPanel;
        private Label levelTitle;
        private bool materialsVisible, levelSettingsVisible, boardFocus;
        private bool autoFitBoard = true;
        private IVisualElementScheduledItem boardFitSchedule;

        private void BuildEditorLayout()
        {
            editor = new VisualElement { name = "editor" }; editor.AddToClassList("editor-shell"); root.Add(editor);
            var menuBar = new VisualElement { name = "menu-bar" }; menuBar.AddToClassList("menu-bar"); editor.Add(menuBar);
            BuildCommandMenus(menuBar);
            var header = new VisualElement { name = "header" }; header.AddToClassList("tool-header"); editor.Add(header);
            var identity = new VisualElement(); identity.AddToClassList("workspace-identity"); header.Add(identity);
            levelTitle = new Label("레벨 제작 도구") { name = "level-title" }; levelTitle.AddToClassList("level-title"); identity.Add(levelTitle);
            folderLabel = new Label { name = "workspace-status" }; identity.Add(folderLabel);
            toolbar = new VisualElement { name = "toolbar" }; toolbar.AddToClassList("toolbar"); header.Add(toolbar);
            CommandButton(toolbar, ToolCommandId.Save, "save");
            CommandButton(toolbar, ToolCommandId.Undo, "undo");
            CommandButton(toolbar, ToolCommandId.Redo, "redo");
            CommandButton(toolbar, ToolCommandId.Play, "play");
            workspaceTabs = new VisualElement { name = "workspace-tabs" }; workspaceTabs.AddToClassList("workspace-tabs"); editor.Add(workspaceTabs);
            string[] ids = { "edit", "flow", "tutorial", "test", "records" };
            string[] labels = { "보드 편집", "흐름·공급", "튜토리얼", "검사·봇 시험", "시험 기록" };
            for (int i = 0; i < ids.Length; i++)
            {
                string page = labels[i]; Button(workspaceTabs, "workspace-" + ids[i], page, () => SelectWorkspacePage(page));
            }
            pageTools = new VisualElement { name = "page-tools" }; pageTools.AddToClassList("page-tools"); editor.Add(pageTools);
            DrawTrialSource();
            body = new VisualElement { name = "workspace-body" }; body.AddToClassList("body"); editor.Add(body);
            leftPanel = new VisualElement { name = "left-panel" }; leftPanel.AddToClassList("sidebar"); body.Add(leftPanel);
            var leftTabs = new VisualElement(); leftTabs.AddToClassList("panel-tabs"); leftPanel.Add(leftTabs);
            Button(leftTabs, "sidebar-levels", "레벨 목록", () => { materialsVisible = false; UpdatePanelVisibility(); });
            Button(leftTabs, "sidebar-materials", "소재", () => { materialsVisible = true; UpdatePanelVisibility(); });
            levelPanel = new VisualElement(); levelPanel.AddToClassList("panel-content"); leftPanel.Add(levelPanel);
            var levelActions = new VisualElement(); levelActions.AddToClassList("panel-tabs"); levelPanel.Add(levelActions);
            Button(levelActions, "new-level", "새 레벨", () => AddLevel(false));
            Button(levelActions, "duplicate", "복제", () => AddLevel(true));
            levelList = new ScrollView { name = "levels" }; levelList.style.flexGrow = 1; levelPanel.Add(levelList);
            materialPanel = new VisualElement(); materialPanel.AddToClassList("panel-content"); leftPanel.Add(materialPanel);
            var layers = new DropdownField("편집 층", new List<string> { "Block", "Obstacle", "Cover", "Dust" }, 0) { name = "edit-layer" };
            layers.SetValueWithoutNotify(layer);
            layers.tooltip = "Block: 일반·파워 블록 / Obstacle: 장애물 / Cover: 덮개 / Dust: 바닥";
            layers.RegisterValueChangedCallback(e => { layer = e.newValue; brush = null; moveBody = null; Refresh(); }); materialPanel.Add(layers);
            Button(materialPanel, "select", "선택 도구", () => { brush = null; moveBody = null; Refresh(); Show("셀 선택 · Shift+클릭으로 사각 영역 선택"); });
            palette = new ScrollView { name = "palette" }; palette.style.flexGrow = 1; materialPanel.Add(palette);
            boardScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { name = "board-scroll" }; boardScroll.AddToClassList("board-viewport"); body.Add(boardScroll);
            board = new VisualElement { name = "board", focusable = true }; board.AddToClassList("board"); boardScroll.Add(board);
            boardScroll.contentContainer.style.alignItems = Align.Center;
            boardScroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                boardFitSchedule?.Pause();
                boardFitSchedule = boardScroll.schedule.Execute(FitBoardToViewport).StartingIn(1);
            });
            rightPanel = new VisualElement { name = "right-panel" }; rightPanel.AddToClassList("sidebar"); body.Add(rightPanel);
            rightTabs = new VisualElement(); rightTabs.AddToClassList("panel-tabs"); rightPanel.Add(rightTabs);
            Button(rightTabs, "inspector-selection", "선택 속성", () => { levelSettingsVisible = false; UpdatePanelVisibility(); });
            Button(rightTabs, "inspector-level", "레벨 설정", () => { levelSettingsVisible = true; UpdatePanelVisibility(); });
            properties = new ScrollView { name = "properties" }; properties.style.flexGrow = 1; rightPanel.Add(properties); inspector = properties.contentContainer;
            validationPanel = new ScrollView { name = "validation-panel" }; validationPanel.AddToClassList("validation-panel"); editor.Add(validationPanel);
            status = new Label("왼쪽에서 레벨을 선택하고, 소재 탭에서 배치할 블록을 고르세요.") { name = "status" }; status.AddToClassList("status"); editor.Add(status);
            UpdatePanelVisibility();
        }

        private void UpdatePanelVisibility()
        {
            if (leftPanel == null) return;
            leftPanel.style.display = rightPanel.style.display = boardFocus ? DisplayStyle.None : DisplayStyle.Flex;
            levelPanel.style.display = materialsVisible ? DisplayStyle.None : DisplayStyle.Flex;
            materialPanel.style.display = materialsVisible ? DisplayStyle.Flex : DisplayStyle.None;
            root.Q("sidebar-levels").EnableInClassList("selected", !materialsVisible);
            root.Q("sidebar-materials").EnableInClassList("selected", materialsVisible);
            root.Q("inspector-selection").EnableInClassList("selected", !levelSettingsVisible);
            root.Q("inspector-level").EnableInClassList("selected", levelSettingsVisible);
            rightTabs.style.display = inspectorPage == "기본" ? DisplayStyle.Flex : DisplayStyle.None;
            VisualElement settings = inspector.Q("level-settings"), selection = inspector.Q("selection-properties");
            if (settings != null) settings.style.display = levelSettingsVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (selection != null) selection.style.display = levelSettingsVisible ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void ToggleBoardFocus()
        {
            boardFocus = !boardFocus; UpdatePanelVisibility();
            Show(boardFocus ? "보드 영역을 넓혔습니다. Shift+Space 또는 보기 메뉴로 패널을 복원하세요." : "좌우 편집 패널을 복원했습니다.");
        }

        private void FitBoardToViewport()
        {
            if (!autoFitBoard || Session == null || inspectorPage == "자동 시험" || inspectorPage == "시험 기록") return;
            boardScroll.contentContainer.style.minWidth = boardScroll.contentViewport.resolvedStyle.width;
            // 스크롤바 출현에 따라 맞춤 크기가 왕복하지 않도록 바깥 뷰포트에서 여유를 예약한다.
            float available = Mathf.Min(boardScroll.resolvedStyle.width, boardScroll.resolvedStyle.height) - 52;
            if (float.IsNaN(available) || available <= 0) return;
            float fitted = Mathf.Clamp(Mathf.Floor(available / 9), 32, 72);
            if (Mathf.Abs(cellSize - fitted) < .5f) return;
            cellSize = fitted; board.Clear(); DrawBoard(); DrawTutorialPreview(); DrawTutorialPicking();
        }
    }
}
#endif

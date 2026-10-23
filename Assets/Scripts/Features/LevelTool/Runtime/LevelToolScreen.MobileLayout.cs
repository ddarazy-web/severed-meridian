#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        // 205/290 단위 패널과 보드 작업 폭 400 단위, 간격을 함께 확보하는 기준이다.
        private const float MobileWideWidth = 940;
        private VisualElement mobileNavigation;
        private ScrollView mobileMorePanel;
        private Label mobileToolStatus;
        private string mobilePanel;
        private bool mobileWide, mobilePortrait;
        private float mobileWidth, mobileHeight;
        private float mobilePanelHeight;

        private void InitializeMobileLayout()
        {
            if (Application.platform != RuntimePlatform.Android) return;
            root.RegisterCallback<GeometryChangedEvent>(OnMobileLayoutGeometryChanged);
            ApplyMobileLayout(root.contentRect.width, root.contentRect.height);
        }

        private void DisposeMobileLayout()
        {
            root?.UnregisterCallback<GeometryChangedEvent>(OnMobileLayoutGeometryChanged);
            body?.UnregisterCallback<GeometryChangedEvent>(OnMobileBodyGeometryChanged);
            mobileNavigation = null;
            mobileMorePanel = null;
            mobileToolStatus = null;
            mobilePendingInputs.Clear();
        }

        private void OnMobileLayoutGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyMobileLayout(root.contentRect.width, root.contentRect.height);
        }

        private void OnMobileBodyGeometryChanged(GeometryChangedEvent evt)
        {
            if (mobileNavigation != null) ApplyMobileLayout(mobileWidth, mobileHeight);
        }

        // 폭/높이는 Safe Area와 패널 배율을 이미 적용한 루트 콘텐츠의 UI 단위이다.
        // 레이아웃만 바꾸므로 미확정 입력이나 편집 세션을 다시 만들지 않는다.
        private void ApplyMobileLayout(float width, float height)
        {
            if (root == null || body == null || width <= 0 || height <= 0 ||
                float.IsNaN(width) || float.IsNaN(height)) return;
            if (mobileNavigation == null)
            {
                root.AddToClassList("mobile-level-tool");
                mobileToolStatus = new Label { name = "mobile-tool-status" };
                mobileToolStatus.AddToClassList("mobile-tool-status");
                editor.Insert(editor.IndexOf(body), mobileToolStatus);
                mobileNavigation = new VisualElement { name = "mobile-navigation" };
                mobileNavigation.AddToClassList("mobile-navigation");
                editor.Insert(editor.IndexOf(status), mobileNavigation);
                Button(mobileNavigation, "mobile-levels", "레벨", () => SelectMobilePanel("levels"));
                Button(mobileNavigation, "mobile-materials", "소재", () => SelectMobilePanel("materials"));
                Button(mobileNavigation, "mobile-properties", "속성", () => SelectMobilePanel("properties"));
                Button(mobileNavigation, "mobile-more", "더보기", () => SelectMobilePanel("more"));
                // 패널 이동 버튼이 지연 입력의 포커스를 빼앗아 문서 수정을 확정하지 않게 한다.
                foreach (Button button in mobileNavigation.Query<Button>().ToList())
                {
                    bool pressed = false;
                    button.focusable = false;
                    button.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (evt.button != 0) return;
                        SuspendMobilePanelInput();
                        pressed = true; evt.PreventDefault();
                    }, TrickleDown.TrickleDown);
                    // 기본 클릭 처리가 취소된 경우에도 패널 전환은 손을 뗄 때 한 번 실행한다.
                    button.RegisterCallback<PointerUpEvent>(evt =>
                    {
                        if (evt.button != 0 || !pressed) return;
                        pressed = false;
                        evt.PreventDefault(); evt.StopImmediatePropagation();
                        SelectMobilePanel(button.name.Substring("mobile-".Length));
                    }, TrickleDown.TrickleDown);
                    button.RegisterCallback<PointerLeaveEvent>(_ => { pressed = false; RestoreMobilePanelInputs(); });
                    button.RegisterCallback<PointerCancelEvent>(_ => { pressed = false; RestoreMobilePanelInputs(); });
                }
                foreach (Button button in toolbar.Query<Button>().ToList())
                {
                    button.style.minWidth = 0;
                    button.text = button.name == "undo" ? "취소" : button.name == "redo" ? "다시" :
                        button.name == "play" ? "시험" : button.text;
                }
                ScrollView leftContent = new ScrollView { name = "mobile-left-content" };
                leftContent.style.flexGrow = 1;
                leftContent.contentContainer.style.flexGrow = 1;
                leftPanel.Add(leftContent);
                leftContent.Add(levelPanel);
                leftContent.Add(materialPanel);
                mobileMorePanel = new ScrollView { name = "mobile-more-panel" };
                mobileMorePanel.AddToClassList("sidebar");
                body.Add(mobileMorePanel);
                // 최초 생성 때만 명령 묶음을 이동한다. 회전 중에는 필드를 제거하지 않는다.
                mobileMorePanel.Add(root.Q("menu-bar"));
                VisualElement trialControls = root.Q("trial-source").parent;
                trialControls.AddToClassList("mobile-trial-controls");
                mobileMorePanel.Add(trialControls);
                mobileMorePanel.Add(root.Q("trial-pack-status"));
                mobileMorePanel.Add(workspaceTabs);
                mobileMorePanel.Add(pageTools);
                body.RegisterCallback<GeometryChangedEvent>(OnMobileBodyGeometryChanged);
            }
            if (mobileWide && width < MobileWideWidth)
            {
                VisualElement focused = root.focusController?.focusedElement as VisualElement;
                if (focused != null && rightPanel.Contains(focused)) mobilePanel = "properties";
                else if (focused != null && leftPanel.Contains(focused)) mobilePanel = materialsVisible ? "materials" : "levels";
                else if (focused != null && mobileMorePanel.Contains(focused)) mobilePanel = "more";
            }
            mobileWidth = width; mobileHeight = height;
            mobileWide = width >= MobileWideWidth;
            mobilePortrait = height >= width;
            RefreshBoardToolStatus();
            root.EnableInClassList("mobile-wide", mobileWide);
            root.EnableInClassList("mobile-portrait", !mobileWide && mobilePortrait);
            root.EnableInClassList("mobile-landscape", !mobileWide && !mobilePortrait);

            // 창 변경 시 선택된 패널과 기존 요소를 유지하고 기하 정보만 갱신한다.
            float panelWidth = Mathf.Min(320, width * .48f);
            float panelHeight = mobilePanel == "materials" ? Mathf.Min(520, height * .65f) : Mathf.Min(280, height * .40f);
            float bodyHeight = body.resolvedStyle.height;
            if (!float.IsNaN(bodyHeight) && bodyHeight > 0) panelHeight = Mathf.Min(panelHeight, bodyHeight * (mobilePanel == "materials" ? .80f : .60f));
            mobilePanelHeight = panelHeight;
            foreach (VisualElement panel in new VisualElement[] { leftPanel, rightPanel, mobileMorePanel })
            {
                panel.style.position = mobileWide && panel != mobileMorePanel ? Position.Relative : Position.Absolute;
                panel.style.left = StyleKeyword.Auto;
                panel.style.right = mobileWide && panel != mobileMorePanel ? new StyleLength(StyleKeyword.Auto) : new StyleLength(0f);
                panel.style.top = mobileWide && panel != mobileMorePanel || mobilePortrait ? new StyleLength(StyleKeyword.Auto) : new StyleLength(0f);
                panel.style.bottom = mobileWide && panel != mobileMorePanel ? new StyleLength(StyleKeyword.Auto) : new StyleLength(0f);
                panel.style.width = mobileWide && panel != mobileMorePanel ? new StyleLength(StyleKeyword.Null) :
                    new StyleLength(mobilePortrait ? width : panelWidth);
                panel.style.height = mobileWide && panel != mobileMorePanel ? new StyleLength(StyleKeyword.Auto) :
                    mobilePortrait ? new StyleLength(panelHeight) : new StyleLength(StyleKeyword.Auto);
                panel.style.marginLeft = panel.style.marginRight = mobileWide && panel != mobileMorePanel ?
                    new StyleLength(StyleKeyword.Null) : new StyleLength(0f);
            }
            UpdateMobilePanelVisibility();
        }

        private void SelectMobilePanel(string panel)
        {
            CancelBoardTouch();
            SuspendMobilePanelInput();
            mobilePanel = mobilePanel == panel ? null : panel;
            if (panel == "levels") materialsVisible = false;
            if (panel == "materials") materialsVisible = true;
            boardFocus = false;
            ApplyMobileLayout(mobileWidth, mobileHeight);
            UpdatePanelVisibility();
            root.schedule.Execute(RestoreMobilePanelInputs);
        }

        private void UpdateMobilePanelVisibility()
        {
            bool editing = Workspace.Folder != null && levelIndex.Count > 0;
            mobileNavigation.style.display = editing ? DisplayStyle.Flex : DisplayStyle.None;
            mobileToolStatus.style.display = editing ? DisplayStyle.Flex : DisplayStyle.None;
            // 가상 목록은 높이가 정해진 뷰포트가 필요하다. 소재 화면의 바깥 스크롤은 유지한다.
            ScrollView leftContent = leftPanel.Q<ScrollView>("mobile-left-content");
            if (leftContent != null) leftContent.contentContainer.style.height = materialsVisible ?
                new StyleLength(StyleKeyword.Auto) : new StyleLength(Length.Percent(100));
            bool left = mobilePanel == "levels" || mobilePanel == "materials";
            leftPanel.style.display = !boardFocus && (mobileWide || left) ? DisplayStyle.Flex : DisplayStyle.None;
            rightPanel.style.display = !boardFocus && (mobileWide || mobilePanel == "properties") ? DisplayStyle.Flex : DisplayStyle.None;
            mobileMorePanel.style.display = !boardFocus && mobilePanel == "more" ? DisplayStyle.Flex : DisplayStyle.None;
            // 좁은 화면에서도 보드와 열린 패널이 각각 사용 가능한 영역을 갖는다.
            bool compactPanel = !mobileWide && !boardFocus && mobilePanel != null;
            boardScroll.style.marginBottom = compactPanel && mobilePortrait ? mobilePanelHeight : 0;
            boardScroll.style.marginRight = compactPanel && !mobilePortrait ? Mathf.Min(320, mobileWidth * .48f) : 0;
            foreach (Button button in mobileNavigation.Query<Button>().ToList())
                button.EnableInClassList("selected", button.name == "mobile-" + mobilePanel);
            string tool = moveBody != null ? "이동" : brush != null ? "소재 " + brush : "선택";
            mobileToolStatus.text = "층 " + layer + " · 도구 " + tool;
        }
    }
}
#endif

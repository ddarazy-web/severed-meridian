using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        [SerializeField] private int workspaceTab;
        [SerializeField] private LevelInitialStatePanel playPanel;
        [SerializeField] private LevelInitialStatePanel diagnosticPanel;
        private LevelAnalysisPanel analysisPanel;
        private VisualElement editorRoot;
        private ObjectField workspaceLevel;
        private Button[] workspaceTabs;
        private VisualElement workspaceContent;
        private ScrollView workspaceViewport;
        internal LevelInitialStatePanel ActiveSimulationPanel => workspaceTab == 1 ? playPanel : diagnosticPanel;
        internal int WorkspaceTab => workspaceTab;

        [MenuItem("Match/통합 작업창", false, 0)]
        public static void OpenWorkspaceMenu() => OpenWorkspace(0);

        internal static LevelEditorWindow OpenWorkspace(int tab, LevelDefinition target = null, bool replaceLevel = false)
        {
            LevelEditorWindow window = GetWindow<LevelEditorWindow>("Match");
            window.Show();
            if (window.editorRoot == null) window.CreateGUI();
            if (replaceLevel) window.SetLevel(target);
            else if (window.CurrentLevel == null && Selection.activeObject is LevelDefinition selectedLevel) window.SetLevel(selectedLevel);
            window.SelectWorkspaceTab(tab);
            window.Focus();
            return window;
        }

        public void CreateGUI()
        {
            // Unity의 코드 재컴파일 뒤에도 이 메서드가 다시 호출된다. 이전 패널의 예약 작업과
            // 바인딩을 먼저 해제한다. 직렬화된 레벨·탭 선택은 유지하지만 UI 객체는 재사용하지 않는다.
            playPanel?.Dispose(); diagnosticPanel?.Dispose(); analysisPanel?.Dispose();
            board?.CancelStroke(); properties?.Unbind(); data?.Dispose(); data = null;
            titleContent = new GUIContent("Match");
            rootVisualElement.Clear();
            // CreateGUI 재호출 시 중복 저장을 방지한다. 창 내부의 키 입력만 받는다.
            rootVisualElement.UnregisterCallback<KeyDownEvent>(HandleSaveShortcut, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<KeyDownEvent>(HandleSaveShortcut, TrickleDown.TrickleDown);
            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Features/LevelEditor/Editor/Styles/LevelEditor.uss");
            if (style != null && !rootVisualElement.styleSheets.Contains(style)) rootVisualElement.styleSheets.Add(style);
            rootVisualElement.AddToClassList("match-workspace");
            // 도킹된 창은 Unity가 minSize를 보장하지 않는다. 이때도 버튼을 잘라내지 않고
            // 전체 작업 영역에 접근하도록 바깥 스크롤을 둔다. 충분히 크면 스크롤은 나타나지 않는다.
            ScrollView viewport = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { name = "workspace-viewport" };
            workspaceViewport = viewport;
            viewport.style.flexGrow = 1;
            viewport.contentContainer.style.flexGrow = 1;
            workspaceContent = new VisualElement { name = "workspace-content" };
            workspaceContent.style.flexGrow = 1;
            // 콘텐츠의 자연 높이가 긴 Inspector를 따라 무한히 늘어나지 않게 실제 뷰포트에
            // 맞춘다. 최소 크기보다 좁을 때만 minWidth/minHeight가 우선되어 바깥 스크롤이 생긴다.
            viewport.RegisterCallback<GeometryChangedEvent>(_ => FitWorkspaceViewport());
            viewport.Add(workspaceContent);
            rootVisualElement.Add(viewport);
            Toolbar header = new Toolbar { name = "workspace-header" };
            header.Add(new Label("MATCH"));
            workspaceLevel = new ObjectField("레벨") { name = "workspace-level", objectType = typeof(LevelDefinition), allowSceneObjects = false, value = level };
            workspaceLevel.style.flexGrow = 1;
            workspaceLevel.style.flexShrink = 1; workspaceLevel.style.minWidth = 0;
            workspaceLevel.labelElement.style.minWidth = 28; workspaceLevel.labelElement.style.width = 28;
            VisualElement levelInput = workspaceLevel.Q(className: "unity-base-field__input");
            levelInput.style.minWidth = 0; levelInput.style.flexShrink = 1;
            workspaceLevel.RegisterValueChangedCallback(evt => SetLevel(evt.newValue as LevelDefinition));
            header.Add(workspaceLevel);
            header.Add(new ToolbarButton(() => ShowLevelNamePanel(false)) { text = "이름 변경", name = "rename-level" });
            header.Add(new ToolbarButton(OpenManual) { text = "사용 설명서", name = "open-level-manual" });
            workspaceContent.Add(header);
            VisualElement tabs = new VisualElement { name = "workspace-tabs" };
            string[] labels = { "레벨 편집", "플레이 테스트", "초기 보드·진단", "결과·이력" };
            workspaceTabs = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int tab = i;
                workspaceTabs[i] = new Button(() => SelectWorkspaceTab(tab)) { text = labels[i], name = "workspace-tab-" + i };
                tabs.Add(workspaceTabs[i]);
            }
            workspaceContent.Add(tabs);
            Button recommend = new Button(ShowShapeRecommendations) { text = "맵 모양 목록", name = "show-shape-recommendations", tooltip = "저장한 레벨의 모양을 등록하거나, 등록 목록에서 골라 새 레벨로 만듭니다." };
            tabs.Add(recommend);
            recommendationPanel = new VisualElement { name = "shape-recommendations" };
            recommendationPanel.style.maxHeight = 380;
            recommendationPanel.style.flexShrink = 0;
            recommendationPanel.style.display = DisplayStyle.None;
            workspaceContent.Add(recommendationPanel);
            levelNamePanel = new VisualElement { name = "level-name-panel" };
            levelNamePanel.style.display = DisplayStyle.None;
            levelNamePanel.style.flexShrink = 0;
            workspaceContent.Add(levelNamePanel);
            editorRoot = new VisualElement { name = "editor-panel" };
            editorRoot.AddToClassList("workspace-panel"); workspaceContent.Add(editorRoot);
            // 패널은 창과 같은 수명을 가지며 탭 이동으로 다시 만들지 않는다.
            playPanel ??= new LevelInitialStatePanel(); diagnosticPanel ??= new LevelInitialStatePanel();
            playPanel.Initialize(this, level, true); diagnosticPanel.Initialize(this, level, false);
            playPanel.rootVisualElement.AddToClassList("workspace-panel");
            diagnosticPanel.rootVisualElement.AddToClassList("workspace-panel");
            workspaceContent.Add(playPanel.rootVisualElement); workspaceContent.Add(diagnosticPanel.rootVisualElement);
            analysisPanel = new LevelAnalysisPanel(() => level, () => playPanel.IsAnalysisBlocked || diagnosticPanel.IsAnalysisBlocked);
            analysisPanel.Root.AddToClassList("workspace-panel"); workspaceContent.Add(analysisPanel.Root);
            CreateEditorGUI();
            SelectWorkspaceTab(workspaceTab);
            LevelEditorHelp.Apply(rootVisualElement);
        }

        internal void SelectWorkspaceTab(int tab)
        {
            // 탭 전환은 새로운 시뮬레이션을 만드는 작업이 아니다. 입력 중인 편집을 반영하고
            // 숨겨진 패널의 자동 진행만 멈춘다. SetVisible이 복귀 시 재개할 진행 상태를 관리한다.
            if (workspaceTabs == null) return;
            board?.CancelStroke(); data?.ApplyModifiedProperties();
            workspaceTab = Mathf.Clamp(tab, 0, 3);
            // 편집: 좌우 도구와 보드, 플레이: 보드와 조작 버튼, 진단: 보드와 상세 정보가 기준이다.
            // 사용자가 넓혀 둔 창은 줄이지 않고, 전환한 탭에 부족한 축만 늘린다.
            Vector2 required = workspaceTab switch
            {
                0 => new Vector2(1040, 780),
                1 => new Vector2(760, 860),
                3 => new Vector2(1000, 760),
                _ => new Vector2(800, 900)
            };
            minSize = required;
            workspaceContent.style.minWidth = required.x;
            workspaceContent.style.minHeight = required.y;
            // CreateGUI도 이 경로를 사용한다. Unity가 창을 연결하는 도중 position을 바꾸면
            // HostView 초기화와 충돌하므로 다음 UI 갱신에 현재 탭의 최소 크기를 적용한다.
            rootVisualElement.schedule.Execute(() =>
            {
                FitWorkspaceViewport();
                if (docked || maximized) return;
                Rect bounds = position;
                if (bounds.width < minSize.x || bounds.height < minSize.y)
                {
                    bounds.width = Mathf.Max(bounds.width, minSize.x);
                    bounds.height = Mathf.Max(bounds.height, minSize.y);
                    position = bounds;
                }
            });
            editorRoot.style.display = workspaceTab == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            playPanel.SetVisible(workspaceTab == 1); diagnosticPanel.SetVisible(workspaceTab == 2);
            analysisPanel.SetVisible(workspaceTab == 3);
            for (int i = 0; i < workspaceTabs.Length; i++) workspaceTabs[i].EnableInClassList("workspace-tab-active", i == workspaceTab);
        }

        /// <summary>창에 맞는 콘텐츠 크기를 정하고, 도킹 등으로 최소 크기보다 좁을 때만 바깥 스크롤을 표시한다.</summary>
        private void FitWorkspaceViewport()
        {
            if (workspaceViewport == null || workspaceContent == null) return;
            Rect area = workspaceViewport.layout;
            workspaceContent.style.width = area.width;
            workspaceContent.style.height = area.height;
            // Auto만 쓰면 세로 막대가 가로 공간을 차지하고 가로 막대가 다시 세로 공간을
            // 차지하여, 딱 맞는 창에서도 두 막대가 서로를 계속 유지하는 문제가 있다.
            bool cramped = area.width + 1 < minSize.x || area.height + 1 < workspaceContent.style.minHeight.value.value;
            ScrollerVisibility visibility = cramped ? ScrollerVisibility.Auto : ScrollerVisibility.Hidden;
            workspaceViewport.horizontalScrollerVisibility = visibility;
            workspaceViewport.verticalScrollerVisibility = visibility;
        }
    }
}

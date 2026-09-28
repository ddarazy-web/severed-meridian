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
        private VisualElement editorRoot;
        private ObjectField workspaceLevel;
        private Button[] workspaceTabs;
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
            playPanel?.Dispose(); diagnosticPanel?.Dispose();
            board?.CancelStroke(); properties?.Unbind(); data?.Dispose(); data = null;
            titleContent = new GUIContent("Match");
            rootVisualElement.Clear();
            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Features/LevelEditor/Editor/Styles/LevelEditor.uss");
            if (style != null && !rootVisualElement.styleSheets.Contains(style)) rootVisualElement.styleSheets.Add(style);
            rootVisualElement.AddToClassList("match-workspace");
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
            rootVisualElement.Add(header);
            VisualElement tabs = new VisualElement { name = "workspace-tabs" };
            string[] labels = { "레벨 편집", "플레이 테스트", "초기 보드·진단" };
            workspaceTabs = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int tab = i;
                workspaceTabs[i] = new Button(() => SelectWorkspaceTab(tab)) { text = labels[i], name = "workspace-tab-" + i };
                tabs.Add(workspaceTabs[i]);
            }
            rootVisualElement.Add(tabs);
            Button recommend = new Button(ShowShapeRecommendations) { text = "맵 모양 목록", name = "show-shape-recommendations", tooltip = "저장한 레벨의 모양을 등록하거나, 등록 목록에서 골라 새 레벨로 만듭니다." };
            tabs.Add(recommend);
            recommendationPanel = new VisualElement { name = "shape-recommendations" };
            recommendationPanel.style.maxHeight = 380;
            recommendationPanel.style.flexShrink = 0;
            recommendationPanel.style.display = DisplayStyle.None;
            rootVisualElement.Add(recommendationPanel);
            levelNamePanel = new VisualElement { name = "level-name-panel" };
            levelNamePanel.style.display = DisplayStyle.None;
            levelNamePanel.style.flexShrink = 0;
            rootVisualElement.Add(levelNamePanel);
            editorRoot = new VisualElement { name = "editor-panel" };
            editorRoot.AddToClassList("workspace-panel"); rootVisualElement.Add(editorRoot);
            // 패널은 창과 같은 수명을 가지며 탭 이동으로 다시 만들지 않는다.
            playPanel ??= new LevelInitialStatePanel(); diagnosticPanel ??= new LevelInitialStatePanel();
            playPanel.Initialize(this, level, true); diagnosticPanel.Initialize(this, level, false);
            playPanel.rootVisualElement.AddToClassList("workspace-panel");
            diagnosticPanel.rootVisualElement.AddToClassList("workspace-panel");
            rootVisualElement.Add(playPanel.rootVisualElement); rootVisualElement.Add(diagnosticPanel.rootVisualElement);
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
            workspaceTab = Mathf.Clamp(tab, 0, 2);
            editorRoot.style.display = workspaceTab == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            playPanel.SetVisible(workspaceTab == 1); diagnosticPanel.SetVisible(workspaceTab == 2);
            for (int i = 0; i < workspaceTabs.Length; i++) workspaceTabs[i].EnableInClassList("workspace-tab-active", i == workspaceTab);
        }
    }
}

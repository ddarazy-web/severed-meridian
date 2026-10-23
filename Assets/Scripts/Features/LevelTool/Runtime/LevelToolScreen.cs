#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    [RequireComponent(typeof(UIDocument))]
    public sealed partial class LevelToolScreen : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField] private StyleSheet styles;
        public AuthoringToolWorkspace Workspace { get; } = new AuthoringToolWorkspace();
        private VisualElement root, editor, toolbar, body, board, inspector, modal;
        private ScrollView levelList, palette, boardScroll, properties;
        private Label status, folderLabel;
        private string layer = "Block", brush, color = "Type1", direction = "Horizontal";
        private string inspectorPage = "기본";
        private int anchor = -1, seed = 12345;
        private float cellSize = 52;
        private string moveBody;
        private bool busy;
        private Camera backgroundCamera;
        private LevelToolWindowController windowController;
        private Presentation.LevelToolBoardView boardView;
        private Presentation.LevelToolPropertiesPanel propertiesPanel;
        private string paletteKey, boardKey;
        private Dictionary<string, JObject> definitions = new Dictionary<string, JObject>();
        private AuthoringEditSession Session => Workspace.Session;

        private void OnEnable()
        {
            backgroundCamera = GetComponentInChildren<Camera>(true);
            InitializeMobileViewport();
            if (LevelToolWindowController.Supported) windowController ??= new LevelToolWindowController();
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            modal = null; commandMenuOpen = false;
            tutorialWorkBar = null;
            if (styles != null && !root.styleSheets.Contains(styles)) root.styleSheets.Add(styles);
            if (font != null) root.style.unityFont = font;
            root.AddToClassList("level-tool");
            navigationPage = null;
            displaySession = null;
            propertiesKey = null;
            paletteKey = boardKey = null;
            BuildEditorLayout();
            boardView = new Presentation.LevelToolBoardView(board, Show, SelectConnectionVertex, () => busy);
            supplyPanel = new Presentation.LevelToolSupplyPanel(() => Session, Change, supplyState);
            propertiesPanel = new Presentation.LevelToolPropertiesPanel(() => Session, Change,
                () => new Presentation.LevelToolPropertiesOptions
                {
                    Layer = layer, Brush = brush, Color = color, Direction = direction,
                    CellSize = cellSize, Seed = seed, TutorialMode = tutorialMode
                }, new Presentation.LevelToolPropertiesActions
                {
                    SetColor = value => color = value, SetDirection = value => direction = value,
                    Zoom = value => ZoomBoard(value, boardScroll.contentViewport.worldBound.center),
                    Fit = () => { autoFitBoard = true; FitBoardToViewport(); },
                    BeginMove = id => { moveBody = id; brush = null; Show("장애물이 이동할 기준 칸을 클릭하세요."); },
                    SetSeed = value => seed = value, SetTutorialMode = value => tutorialMode = value,
                    LoadMissionImage = (image, kind, tint) => LoadMissionPreview(image, kind, tint).Forget()
                });
            tutorialPanel = new Presentation.LevelToolTutorialPanel(new Presentation.LevelToolTutorialActions
            {
                Session = () => Session, Draft = () => sharedTutorialDraft, Edit = Edit, Refresh = RefreshProperties,
                Preview = () => tutorialPreview, SetPreview = value => tutorialPreview = value, Layer = () => layer,
                WorkActive = () => tutorialWork != null,
                Pick = BeginTutorialPick,
                BeginSharedDraft = () => Edit(() => { sharedTutorialDraft = new LevelAuthoring.Runtime.SharedTutorialDraft(Session); tutorialStep = 0; CancelPendingInput(); brush = null; }),
                ApplySharedDraft = ApplySharedTutorialDraft,
                DiscardSharedDraft = () => { sharedTutorialDraft = null; Refresh(); }
            }, tutorialState);
            editor.SetEnabled(!busy);
            RegisterShortcuts();
            root.UnregisterCallback<InputEvent>(OnManualInput, TrickleDown.TrickleDown);
            root.RegisterCallback<InputEvent>(OnManualInput, TrickleDown.TrickleDown);
            Refresh();
            if (tutorialWork != null && purposeDraft != null && tutorialPick == null) OpenPurposeSettings();
            if (tutorialPick != null && directPickReady) OpenDirectPickSettings();
            EnableRecovery();
            if (suspendedInputs != null)
            {
                JArray inputs = suspendedInputs; suspendedInputs = null;
                var session = Session;
                root.schedule.Execute(() =>
                {
                    if (isActiveAndEnabled && ReferenceEquals(Session, session))
                    { RestoreRecoveryInputs(inputs); UpdateManualSaveStatus(); }
                }).ExecuteLater(50);
            }
            LoadToolSkin().Forget();
        }

        private void Show(string text) { if (status != null) { status.text = text; status.tooltip = text; } }
        private static Button Button(VisualElement parent, string name, string text, Action action)
        {
            Button value = new Button(action) { name = name, text = text }; parent.Add(value); return value;
        }
        private void Edit(Action action)
        {
            if (busy || Session == null) return;
            string previousLevel = Session.SelectedLevelId;
            try
            {
                action();
                forceProperties = true;
                if (Session.SelectedLevelId != previousLevel) CancelPendingInput();
                Refresh();
            }
            catch (Exception error) { Refresh(); Show(error.Message); }
        }
        private void CancelPendingInput()
        {
            CancelBoardTouch();
            tutorialPick = null;
            purposeDraft = null;
            directPickStep = null; directPickReady = false;
            CancelFlowTool(); wireDrawing = false; wireRoute.Clear();
            connectionGenerator = null; connectionTarget = null; selectedConnection = 0;
            moveBody = null; anchor = -1;
        }
        private void Change(string label, Action<JObject> action) => Edit(() => Session.EditLevel(label, action));

        private void ConfirmDeleteLevel()
        {
            if (GuardTutorialWorkExit(ConfirmDeleteLevel)) return;
            if (Session == null || busy || modal != null || sharedTutorialDraft != null || tutorialPick != null) return;
            if (Session.DocumentIdsOfKind("level").Length <= 1)
            { Show("마지막 레벨은 삭제할 수 없습니다. 레벨을 하나 이상 남겨 주세요."); return; }
            var session = Session;
            string id = session.SelectedLevelId;
            var info = session.GetDocumentInfo(id);
            VisualElement panel = OpenModal("레벨 삭제");
            panel.Add(new Label("현재 편집 중인 레벨 " + info.LevelNumber + " · " + info.DisplayName + "을 삭제할까요?")
                { style = { whiteSpace = WhiteSpace.Normal } });
            panel.Add(new Label("저장할 때 작업 폴더에 반영됩니다. 실행 취소(Ctrl+Z)로 복구할 수 있습니다.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            Button(panel, "cancel-delete-level", "취소", CloseModal);
            Button(panel, "confirm-delete-level", "삭제", () =>
            {
                CloseModal();
                ContinueAfterManualInput(() => Edit(() =>
                {
                    if (!ReferenceEquals(Session, session) || session.SelectedLevelId != id) return;
                    session.DeleteSelectedLevel();
                    Show("레벨을 삭제했습니다. 저장하면 반영되며 실행 취소로 복구할 수 있습니다.");
                })).Forget();
            });
        }

        private void Refresh()
        {
            if (root == null) return;
            if (committingCommandInput) { commandInputRefreshPending = true; return; }
            DrawIncomingWorkspace();
            UpdatePackStatus();

            folderLabel.text = (Session == null ? Workspace.Directory != null ? "목록 열림 · 편집할 레벨을 선택하세요" : "작업 폴더 없음" : Session.IsDirty ? "● 미저장" : "저장됨") +
                (Workspace.IsRecovery ? "  [복구본 · 사본 저장 필요]" : "");
            if (Session?.RequiresProjectValidation == true) folderLabel.text += " · 전체 프로젝트 검사 필요";
            foreach (string name in new[] { "save", "save-copy", "reload", "undo", "redo", "play", "validate-level", "replay-tutorial" })
                root.Q<Button>(name)?.SetEnabled(Session != null && !busy);
            root.Q<Button>("new-project")?.SetEnabled(!busy);
            bool shared = sharedTutorialDraft != null;
            root.Q("board-touch-erase")?.SetEnabled(Session != null && !busy && !shared);
            root.Q("delete-level")?.SetEnabled(Session != null && !busy && !shared && tutorialPick == null &&
                Session.DocumentIdsOfKind("level").Length > 1);
            root.Q("select")?.SetEnabled(Session != null && !busy);
            foreach (string name in new[] { "open", "new-project", "backup" })
                root.Q(name)?.SetEnabled(!busy && !shared);
            foreach (string name in new[] { "save", "save-copy", "reload", "new-level", "duplicate", "edit-layer" })
                root.Q(name)?.SetEnabled(Session != null && !busy && !shared);
            levelList.SetEnabled(!shared); palette.SetEnabled(!shared);
            if (shared) folderLabel.text += "  [공유 원본 사본" + (sharedTutorialDraft.IsChanged ? " · 미적용]" : "]");
            if (Workspace.Folder != null) folderLabel.text += "\n" + Workspace.Folder;
            folderLabel.tooltip = folderLabel.text;
            var focus = committingCommandInput ? null : CaptureFocusedInput();
            Vector2 levelOffset = levelList.scrollOffset, paletteOffset = palette.scrollOffset, propertyOffset = properties.scrollOffset, boardOffset = boardScroll.scrollOffset;
            inspector = properties.contentContainer;
            try { PrepareDisplay(); }
            catch (Exception error)
            {
                // 잘못된 참조를 고치기 전까지 이전 레벨의 입력을 현재 자료에 적용하지 않도록 비운다.
                displaySession = null; inspector.Clear();
                supplyPanel.Clear(); propertiesPanel.Clear(); tutorialPanel.Clear();
                Show(error.Message); return;
            }
            board.style.display = (inspectorPage == "자동 시험" || inspectorPage == "시험 기록") ? DisplayStyle.None : DisplayStyle.Flex;
            RefreshNavigation(); RefreshCommandStates(); RefreshCreationGuide(); RefreshTutorialWorkBar();
            RefreshStartScreen();
            if (Session?.SelectedLevelId == null) { RefreshLevelBrowser(); palette.Clear(); paletteKey = null; inspector.Clear(); boardView.Clear(); ApplyToolSkin(); return; }
            try
            {
                RefreshLevelBrowser();
                RefreshMaterialPalette();
                string nextBoardState = Workspace.Folder + "|" + Session.SelectedLevelId + "|" + boardRevision +
                    "|" + string.Join(",", Session.SelectedCells) + "|" + layer + "|" + inspectorPage + "|" +
                    flowTool + "|" + flowFirst + "|" + string.Join(",", flowRoute) + "|" + wireDrawing + "|" + string.Join(",", wireRoute) + "|" + connectionGenerator + "|" + connectionTarget + "|" + selectedConnection +
                    "|" + definitionRevision;
                string nextBoard = nextBoardState + "|" + cellSize;
                // 자동 맞춤은 별도 예약 콜백에서 그린다. 이전 키와 같아도 실제 기하가 다르면 동기화한다.
                if (nextBoard != boardKey || board.style.width.value.value != cellSize * 9)
                {
                    boardKey = nextBoard; DrawBoard();
                }
                string nextProperties = nextBoardState + "|" + propertiesRevision + "|" + selectedShape + "|" + shared + "|" + (tutorialPick != null);
                if (forceProperties || propertiesKey != nextProperties)
                {
                    bool retainedTutorial = inspectorPage == "튜토리얼" && inspector.Contains(tutorialPanel.Root) &&
                        tutorialPick == null && tutorialPanel.TryRefreshInstructions();
                    if (retainedTutorial) RefreshTutorialSummaryInstructions();
                    if (!retainedTutorial)
                    {
                        inspector.Clear();
                        supplyPanel.Clear(); propertiesPanel.Clear(); tutorialPanel.Clear();
                        botBoard?.Root.RemoveFromHierarchy(); botBoard?.Dispose(); botBoard = null;
                        batchBoard?.Root.RemoveFromHierarchy(); batchBoard?.Dispose(); batchBoard = null;
                        multiBoard?.Root.RemoveFromHierarchy(); multiBoard?.Dispose(); multiBoard = null;
                        historyBoard?.Root.RemoveFromHierarchy(); historyBoard?.Dispose(); historyBoard = null;
                        DrawProperties();
                    }
                    propertiesKey = nextProperties; forceProperties = false;
                }
                inspector.Q<Slider>("board-zoom")?.SetValueWithoutNotify(cellSize);
                DrawTutorialPreview();
            }
            catch (Exception error) { Show(error.Message); }
            DrawValidationResults();
            DrawTutorialPicking();
            UpdatePanelVisibility();
            ApplyToolSkin();
            levelList.scrollOffset = levelOffset; palette.scrollOffset = paletteOffset;
            properties.scrollOffset = propertyOffset; boardScroll.scrollOffset = boardOffset;
            RestoreFocusedInput(focus);
            boardScroll.schedule.Execute(FitBoardToViewport);
            UpdateManualSaveStatus();
        }
    }
}
#endif

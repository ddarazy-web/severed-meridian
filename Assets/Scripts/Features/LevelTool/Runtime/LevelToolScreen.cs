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
        private LevelToolWindowController windowController;
        private string levelListKey, paletteKey, boardKey;
        private Dictionary<string, JObject> definitions = new Dictionary<string, JObject>();
        private AuthoringEditSession Session => Workspace.Session;

        private void OnEnable()
        {
            if (LevelToolWindowController.Supported) windowController ??= new LevelToolWindowController();
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            modal = null; commandMenuOpen = false;
            if (styles != null && !root.styleSheets.Contains(styles)) root.styleSheets.Add(styles);
            if (font != null) root.style.unityFont = font;
            root.AddToClassList("level-tool");
            navigationPage = null;
            levelListKey = paletteKey = boardKey = null;
            BuildEditorLayout();
            RegisterShortcuts();
            Refresh();
            EnableRecovery().Forget();
            LoadToolSkin().Forget();
        }

        private void Show(string text) { if (status != null) status.text = text; }
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
                if (Session.SelectedLevelId != previousLevel) CancelPendingInput();
                Refresh();
            }
            catch (Exception error) { Refresh(); Show(error.Message); }
        }
        private void CancelPendingInput()
        {
            tutorialPick = null;
            CancelFlowTool(); wireDrawing = false; wireRoute.Clear();
            connectionGenerator = null; connectionTarget = null; selectedConnection = 0;
            moveBody = null; anchor = -1;
        }
        private void Change(string label, Action<JObject> action) => Edit(() =>
            Session.Apply(label, docs =>
            {
                JObject level = docs[Session.SelectedLevelId].Data;
                if ((int)level["schemaVersion"] != 5) throw new InvalidOperationException("먼저 ‘편집 형식으로 전환’을 눌러 주세요. 이전 형식의 원본은 저장 전까지 바뀌지 않습니다.");
                action(level);
            }));

        private void AddLevel(bool duplicate)
        {
            Edit(() =>
            {
                int number = Session.Documents.Where(doc => doc.Kind == "level").Max(doc => (int)doc.Data["levelNumber"]) + 1;
                if (duplicate) DocumentEditing.DuplicateLevel(Session, Session.SelectedLevelId, number, "레벨 " + number + " 사본");
                else
                {
                    JObject value = LevelToolDocuments.NewLevel();
                    value["catalogId"] = Session.Get(Session.SelectedLevelId).Data["catalogId"].DeepClone();
                    DocumentEditing.AddLevel(Session, value, number, "레벨 " + number);
                }
            });
        }

        private void Refresh()
        {
            if (root == null) return;
            if (committingCommandInput) { commandInputRefreshPending = true; return; }
            DrawIncomingWorkspace();
            UpdatePackStatus();
            checkpointPending = true;
            folderLabel.text = (Session == null ? "작업 폴더 없음" : Session.IsDirty ? "● 미저장" : "저장됨") +
                (Workspace.IsRecovery ? "  [복구본 · 사본 저장 필요]" : "");
            foreach (string name in new[] { "save", "save-copy", "reload", "undo", "redo", "play", "validate-level", "replay-tutorial" })
                root.Q<Button>(name)?.SetEnabled(Session != null && !busy);
            root.Q<Button>("new-project")?.SetEnabled(!busy);
            bool shared = sharedTutorialDraft != null;
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
            inspector = properties.contentContainer; inspector.Clear();
            botBoard?.Root.RemoveFromHierarchy(); botBoard?.Dispose(); botBoard = null;
            batchBoard?.Root.RemoveFromHierarchy(); batchBoard?.Dispose(); batchBoard = null;
            multiBoard?.Root.RemoveFromHierarchy(); multiBoard?.Dispose(); multiBoard = null;
            historyBoard?.Root.RemoveFromHierarchy(); historyBoard?.Dispose(); historyBoard = null;
            board.style.display = (inspectorPage == "자동 시험" || inspectorPage == "시험 기록") ? DisplayStyle.None : DisplayStyle.Flex;
            RefreshNavigation(); RefreshCommandStates();
            if (Session == null) { levelList.Clear(); palette.Clear(); board.Clear(); return; }
            try
            {
                definitions = LevelToolDocuments.Definitions(Session);
                var levels = Session.Documents.Where(doc => doc.Kind == "level").OrderBy(doc => (int)doc.Data["levelNumber"]).ToArray();
                string nextLevels = string.Join("|", levels.Select(doc => doc.Id + ":" + doc.Data["levelNumber"] + ":" + doc.Data["displayName"]));
                if (nextLevels != levelListKey)
                {
                    levelListKey = nextLevels; levelList.Clear();
                    foreach (var document in levels)
                    {
                        string id = document.Id;
                        Button(levelList, id, document.Data["levelNumber"] + " · " + document.Data["displayName"],
                            () => Edit(() => { Session.SelectLevel(id); anchor = -1; moveBody = null; }));
                    }
                }
                foreach (var item in levelList.Query<Button>().ToList()) item.EnableInClassList("selected", item.name == Session.SelectedLevelId);
                var materials = definitions.Where(pair => LevelToolDocuments.Layer(pair.Value) == layer).ToArray();
                string nextPalette = layer + string.Join("|", materials.Select(pair => pair.Key + ":" + pair.Value["displayName"]));
                if (nextPalette != paletteKey)
                {
                    paletteKey = nextPalette; palette.Clear();
                    foreach (var pair in materials)
                    {
                        string id = pair.Key;
                        Button item = Button(palette, id, (string)pair.Value["displayName"], () => { brush = id; moveBody = null; Refresh(); Show("빈 칸 클릭으로 배치, 기존 칸은 더블클릭으로 교체합니다."); });
                        item.tooltip = id;
                    }
                }
                foreach (var item in palette.Query<Button>().ToList()) item.EnableInClassList("selected", item.name == brush);
                string nextBoard = Workspace.Folder + "|" + Session.SelectedLevelId + "|" + Session.Get(Session.SelectedLevelId).Data.ToString(Newtonsoft.Json.Formatting.None) +
                    "|" + string.Join(",", Session.SelectedCells) + "|" + layer + "|" + inspectorPage + "|" + cellSize + "|" +
                    flowTool + "|" + flowFirst + "|" + string.Join(",", flowRoute) + "|" + wireDrawing + "|" + string.Join(",", wireRoute) + "|" + connectionGenerator + "|" + connectionTarget + "|" + selectedConnection +
                    "|" + string.Join("|", Session.Documents.Where(doc => doc.Kind != "level").Select(doc => doc.Data.ToString(Newtonsoft.Json.Formatting.None)));
                if (nextBoard != boardKey)
                {
                    boardKey = nextBoard; board.Clear(); DrawBoard();
                }
                DrawProperties(); DrawTutorialPreview();
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
        }
    }
}
#endif

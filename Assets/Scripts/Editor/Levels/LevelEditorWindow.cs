using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow : EditorWindow
    {
        [SerializeField] private LevelDefinition level;
        private SerializedObject data;
        private LevelBoardView board;
        private ObjectField assetField;
        private VisualElement properties;
        private VisualElement selectedProperties;
        private VisualElement tools;
        private ScrollView boardScroll;
        private ScrollView issues;
        private Label state;
        private Label result;
        private Label operation;
        private BoardCoordinate? selected;
        private bool refreshQueued;

        public LevelDefinition CurrentLevel => level;

        [MenuItem("Match/레벨 에디터")]
        public static void Open()
        {
            GetWindow<LevelEditorWindow>("레벨 에디터").Show();
        }

        public static void OpenLevel(LevelDefinition target)
        {
            LevelEditorWindow window = GetWindow<LevelEditorWindow>("레벨 에디터");
            window.Show();
            window.SetLevel(target);
        }

        private void OnEnable()
        {
            minSize = new Vector2(680, 480);
            Undo.undoRedoPerformed += ExternalChange;
            Undo.postprocessModifications += OnModifications;
            ObjectChangeEvents.changesPublished += OnObjectChanges;
            EditorApplication.projectChanged += ExternalChange;
        }

        private void OnDisable()
        {
            board?.CancelStroke();
            Undo.undoRedoPerformed -= ExternalChange;
            Undo.postprocessModifications -= OnModifications;
            ObjectChangeEvents.changesPublished -= OnObjectChanges;
            EditorApplication.projectChanged -= ExternalChange;
            EditorApplication.update -= RefreshDeferred;
            refreshQueued = false;
            properties?.Unbind();
            data?.Dispose();
            data = null;
        }

        private void OnLostFocus() => board?.CancelStroke();

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("match-editor");
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Editor/Levels/LevelEditor.uss");
            if (sheet != null && !rootVisualElement.styleSheets.Contains(sheet)) rootVisualElement.styleSheets.Add(sheet);
            Label title = new Label("MATCH  /  레벨 에디터");
            title.AddToClassList("editor-title");
            VisualElement titleBar = new VisualElement();
            titleBar.AddToClassList("editor-header");
            titleBar.Add(title);
            titleBar.Add(PanelToggle("도구", "tool-scroll", "toggle-tools"));
            titleBar.Add(PanelToggle("속성", "inspector-scroll", "toggle-inspector"));
            rootVisualElement.Add(titleBar);
            Toolbar bar = new Toolbar();
            bar.AddToClassList("editor-toolbar");
            assetField = new ObjectField("레벨") { objectType = typeof(LevelDefinition), allowSceneObjects = false, name = "level-asset" };
            assetField.style.flexGrow = 1;
            assetField.RegisterValueChangedCallback(evt => SetLevel(evt.newValue as LevelDefinition));
            bar.Add(assetField);
            bar.Add(new ToolbarButton(() =>
            {
                board.CancelStroke();
                LevelAssetOperations.CreateLevelAsset();
                SetLevel(Selection.activeObject as LevelDefinition);
            }) { text = "새 레벨", name = "new-level" });
            bar.Add(new ToolbarButton(Save) { text = "저장", name = "save-level" });
            bar.Add(new ToolbarButton(ShowDuplicatePanel) { text = "복제", name = "duplicate-level" });
            bar.Add(new ToolbarButton(Validate) { text = "검사", name = "validate-level" });
            rootVisualElement.Add(bar);
            duplicatePanel = new VisualElement { name = "duplicate-panel" };
            duplicatePanel.style.display = DisplayStyle.None;
            duplicatePanel.style.flexShrink = 0;
            rootVisualElement.Add(duplicatePanel);
            state = new Label { name = "asset-state" };
            rootVisualElement.Add(state);

            VisualElement workspace = new VisualElement { name = "workspace" };
            workspace.style.flexDirection = FlexDirection.Row;
            workspace.style.flexGrow = 1;
            workspace.style.minHeight = 0;
            rootVisualElement.Add(workspace);
            ScrollView toolScroll = new ScrollView();
            toolScroll.name = "tool-scroll";
            toolScroll.style.width = 192;
            toolScroll.style.flexShrink = 0;
            tools = toolScroll.contentContainer;
            tools.AddToClassList("side-content");
            workspace.Add(toolScroll);

            boardScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { name = "board-scroll" };
            boardScroll.style.flexGrow = 1;
            boardScroll.style.minWidth = 80;
            boardScroll.contentContainer.style.minWidth = 454;
            boardScroll.contentContainer.style.alignItems = Align.Center;
            workspace.Add(boardScroll);
            VisualElement grid = new VisualElement();
            grid.AddToClassList("board-grid");
            grid.style.width = 430;
            grid.style.flexShrink = 0;
            VisualElement headers = new VisualElement();
            headers.style.flexDirection = FlexDirection.Row;
            headers.style.marginLeft = 30;
            for (int c = 1; c <= 10; c++)
            {
                Label header = new Label(c.ToString());
                header.style.width = LevelBoardView.CellSize;
                header.style.height = 26;
                header.style.unityTextAlign = TextAnchor.MiddleCenter;
                headers.Add(header);
            }
            grid.Add(headers);
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            VisualElement rowLabels = new VisualElement();
            rowLabels.style.width = 30;
            rowLabels.style.flexShrink = 0;
            for (int r = 1; r <= 10; r++)
            {
                Label label = new Label(r.ToString());
                label.style.height = LevelBoardView.CellSize;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                rowLabels.Add(label);
            }
            row.Add(rowLabels);
            board = new LevelBoardView();
            board.AddManipulator(new ContextualMenuManipulator(BuildBoardContextMenu));
            board.Selected += SelectCell;
            board.PlacementSelected += SelectPlacements;
            board.SourceSelected += SelectSource;
            board.Cancelled += () => { cancelSupplyDrag?.Invoke(); cancelMergeDrag?.Invoke(); };
            board.Committed += ApplyStroke;
            board.MoveCommitted += (index, destination) =>
            {
                LevelObstacleEditing.Move(level, index, destination, out string message);
                operation.text = message;
                selected = destination;
                Refresh();
            };
            SetupFlowOverlay();
            row.Add(board);
            grid.Add(row);
            boardScroll.Add(grid);

            ScrollView inspectorScroll = new ScrollView { name = "inspector-scroll" };
            inspectorScroll.style.width = 256;
            inspectorScroll.style.flexShrink = 0;
            properties = inspectorScroll.contentContainer;
            properties.AddToClassList("side-content");
            workspace.Add(inspectorScroll);
            operation = new Label("선택 도구로 칸을 선택하거나 도구를 골라 칠하세요.") { name = "operation-status" };
            operation.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(operation);
            result = new Label("검사 전입니다.") { name = "validation-status" };
            validationDrawer = new Foldout { text = "구조 검사", value = false, name = "validation-drawer" };
            validationDrawer.Add(result);
            rootVisualElement.Add(validationDrawer);
            issues = new ScrollView { name = "validation-issues" };
            issues.style.height = 100;
            issues.style.flexShrink = 0;
            validationDrawer.Add(issues);
            LevelDefinition current = level;
            level = null;
            SetLevel(current);
        }

        public void SetLevel(LevelDefinition target)
        {
            board?.CancelStroke();
            properties?.Unbind();
            data?.Dispose();
            data = null;
            level = target;
            selected = null;
            selectedSources.Clear();
            selectedPlacements.Clear();
            duplicateSource = null;
            if (duplicatePanel != null) duplicatePanel.style.display = DisplayStyle.None;
            selectedSupplyItem = -1;
            expandedUsedGroups.Clear();
            flowTool = FlowTool.None;
            flowListExpanded = false;
            toolPage = 0;
            inspectorPage = 0;
            if (board == null) return;
            assetField.SetValueWithoutNotify(level);
            board.Brush = LevelBrush.Select;
            board.Color = RabbitColor.Type1;
            board.Layer = PlacementLayer.Block;
            operation.text = level == null ? "레벨을 선택하거나 새 레벨을 만드세요." : "선택 도구로 칸을 선택하거나 도구를 골라 칠하세요.";
            Refresh();
        }

        private UndoPropertyModification[] OnModifications(UndoPropertyModification[] modifications)
        {
            if (modifications.Any(change => change.currentValue.target is LevelDefinition))
                ExternalChange();
            return modifications;
        }

        private void OnObjectChanges(ref ObjectChangeEventStream stream)
        {
            // 배열 크기 변경도 감지한다. 화면의 바인딩 갱신 여부에 데이터 감지를 의존시키지 않는다.
            for (int i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) != ObjectChangeKind.ChangeAssetObjectProperties) continue;
                stream.GetChangeAssetObjectPropertiesEvent(i, out ChangeAssetObjectPropertiesEventArgs change);
                if (AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(change.guid.ToString())) != null)
                {
                    ExternalChange();
                    return;
                }
            }
        }

        private void ExternalChange()
        {
            board?.CancelStroke();
            InvalidateResults();
            if (refreshQueued) return;
            refreshQueued = true;
            // Inspector의 표시 상태와 무관하게 다음 Editor 업데이트에서 한 번만 갱신한다.
            EditorApplication.update += RefreshDeferred;
        }

        private void RefreshDeferred()
        {
            EditorApplication.update -= RefreshDeferred;
            refreshQueued = false;
            if (this != null && board != null) Refresh();
        }

        private void Refresh()
        {
            EditorApplication.update -= RefreshDeferred;
            refreshQueued = false;
            properties.Unbind();
            properties.Clear();
            tools.Clear();
            data?.Dispose();
            data = null;
            assetField.SetValueWithoutNotify(level);
            selectedSources.RemoveWhere(cell => LevelSupplyRules.FindSource(level, cell) < 0);
            board.SourceSelection = selectedSources;
            RefreshPlacementSelection();
            board.Display(level, selected);
            flowOverlay.Display(level, board.Brush == LevelBrush.Flow ? flowTool : FlowTool.None);
            connectionGraph.Display(level, selected, board.Brush == LevelBrush.Select || (board.Brush == LevelBrush.Flow && flowTool == FlowTool.Select));
            flowOverlay.DisplayMerge(selected, board.Brush == LevelBrush.Select || (board.Brush == LevelBrush.Flow && flowTool == FlowTool.Select));
            state.text = level == null ? "레벨을 선택하거나 새로 만드세요." :
                AssetDatabase.GetAssetPath(level) + (EditorUtility.IsDirty(level) ? "  • 저장 안 됨" : "  • 저장됨");
            rootVisualElement.Q<ToolbarButton>("save-level").SetEnabled(level != null);
            rootVisualElement.Q<ToolbarButton>("validate-level").SetEnabled(level != null);
            InvalidateResults();
            if (level == null)
            {
                selected = null;
                properties.Add(new Label("선택한 레벨이 없습니다."));
                return;
            }
            bool editable = LevelBoardEditing.CanEdit(level);
            BuildToolPanel(editable);
            if (!editable)
                properties.Add(new HelpBox("저장 형식·보드 구조 오류로 칠할 수 없습니다. 검사 후 기존 Inspector에서 원본을 확인하세요.", HelpBoxMessageType.Warning));
            if (level.SchemaVersion >= 1 && level.SchemaVersion < LevelDefinition.CurrentSchemaVersion)
            {
                properties.Add(new HelpBox("이전 버전: 읽기 전용입니다. 편집하려면 명시적으로 전환하세요.", HelpBoxMessageType.Info));
                properties.Add(new Button(() =>
                {
                    board.CancelStroke();
                    LevelSchemaUpgrade.Upgrade(level, out string message);
                    operation.text = message;
                    Refresh();
                }) { text = "5단계 형식으로 전환", name = "upgrade-level" });
            }
            properties.Add(new Button(() => { board.CancelStroke(); Selection.activeObject = level; EditorGUIUtility.PingObject(level); })
                { text = "기존 Inspector에서 확인", name = "show-inspector" });
            data = new SerializedObject(level);
            foreach (string fieldName in new[] { "levelNumber", "moveCount", "colors" })
            {
                PropertyField field = new PropertyField(data.FindProperty(fieldName), fieldName == "levelNumber" ? "레벨 번호" : fieldName == "moveCount" ? "이동 횟수" : "사용 종류");
                field.SetEnabled(level.SchemaVersion == LevelDefinition.CurrentSchemaVersion);
                properties.Add(field);
            }
            BuildMissionSettings();
            selectedProperties = new VisualElement { name = "selected-cell-properties" };
            selectedProperties.tooltip = "우클릭: 설정 및 공급 목록 복사·붙여넣기";
            selectedProperties.AddManipulator(new ContextualMenuManipulator(BuildSelectionContextMenu));
            properties.Add(selectedProperties);
            flowProperties = new VisualElement { name = "flow-selected-properties" };
            flowProperties.AddManipulator(new ContextualMenuManipulator(BuildSelectionContextMenu));
            properties.Add(flowProperties);
            BuildSelectedProperties();
            properties.Bind(data);
            // 입력 도중 갱신으로 필드가 재생성되지 않도록 Enter 또는 포커스 이동 때 숫자를 확정한다.
            foreach (IntegerField field in properties.Query<IntegerField>().ToList())
                field.isDelayed = true;
            ArrangeInspector();
        }

        private void SelectCell(BoardCoordinate coordinate)
        {
            selectedPlacements.Clear();
            if (board.Brush == LevelBrush.Select && LevelCommonEditing.TrySelect(level, board.Layer, coordinate, out PlacementSelection target)) selectedPlacements.Add(target);
            RefreshPlacementSelection();
            selected = coordinate;
            board.Display(level, selected);
            connectionGraph.Display(level, selected, board.Brush == LevelBrush.Select || (board.Brush == LevelBrush.Flow && flowTool == FlowTool.Select));
            flowOverlay.DisplayMerge(selected, board.Brush == LevelBrush.Select || (board.Brush == LevelBrush.Flow && flowTool == FlowTool.Select));
            if (data == null) return;
            selectedProperties.Unbind();
            BuildSelectedProperties();
            selectedProperties.Bind(data);
            ShowInspectorPage(0);
        }

        private void BuildSelectedProperties()
        {
            BuildFlowProperties();
            selectedProperties.Clear();
            if (BuildSupplyProperties()) return;
            if (BuildCommonProperties()) return;
            if (!selected.HasValue || level == null)
            {
                selectedProperties.Add(new HelpBox("보드에서 칸을 선택하면 속성이 표시됩니다. 왼쪽에서 편집할 층과 도구를 선택하세요.", HelpBoxMessageType.Info));
                return;
            }
            BoardCoordinate coordinate = selected.Value;
            selectedProperties.Add(new Label($"{coordinate.Row + 1}행 {coordinate.Column + 1}열 · {LayerNames[(int)board.Layer]}")
                { tooltip = "선택 " + coordinate + " / 편집 층: " + LayerNames[(int)board.Layer] });
            if (!LevelBoardEditing.CanEdit(level)) return;
            if (board.Layer == PlacementLayer.Block && LevelSupplyRules.HasRecovery(level, coordinate))
            {
                selectedProperties.Add(new Label("회수 부품 · 도착 바닥으로 회수"));
                selectedProperties.Add(SupplyButton("회수 부품 삭제", "delete-recovery", () => LevelSupplyEditing.PlaceRecovery(level, new[] { coordinate }, true)));
                return;
            }
            Toggle active = new Toggle("칸 활성") { name = "selected-active" };
            active.SetValueWithoutNotify(level.Board.Cells[coordinate.Row * 10 + coordinate.Column].IsActive);
            active.RegisterValueChangedCallback(evt => ApplyStroke(evt.newValue ? LevelBrush.Activate : LevelBrush.Deactivate,
                RabbitColor.Type1, new[] { coordinate }));
            selectedProperties.Add(active);
            if (board.Layer != PlacementLayer.Block)
            {
                BuildPlacementProperties(coordinate);
                return;
            }
            int index = LevelBoardEditing.FindBlock(level, coordinate);
            if (index == -2)
            {
                selectedProperties.Add(new HelpBox("중복 배치입니다. 기존 Inspector의 초기 배치 목록에서 수정하세요.", HelpBoxMessageType.Warning));
                return;
            }
            if (index >= 0 && !LevelPlacementRules.IsNormal(level.InitialBlocks[index].Kind))
            {
                BuildPlacementProperties(coordinate);
                return;
            }
            List<string> choices = new List<string> { "빈칸", "무작위 ?" };
            List<RabbitColor> colors = level.Colors == null ? new List<RabbitColor>() : level.Colors
                .Where(color => Enum.IsDefined(typeof(RabbitColor), color)).Distinct().ToList();
            choices.AddRange(colors.Select(color => "고정 " + ((int)color + 1)));
            string current = "빈칸";
            if (index >= 0)
            {
                InitialBlockDefinition block = level.InitialBlocks[index];
                current = block.Kind == InitialBlockKind.RandomNormal ? "무작위 ?" :
                    block.FixedColor.HasValue ? "고정 " + ((int)block.FixedColor.Value + 1) : "잘못된 유형";
            }
            if (!choices.Contains(current)) choices.Add(current);
            PopupField<string> kind = new PopupField<string>("일반 블록", choices, current) { name = "selected-block" };
            kind.RegisterValueChangedCallback(evt =>
            {
                int choice = choices.IndexOf(evt.newValue);
                if (choice == 0) ApplyStroke(LevelBrush.Erase, RabbitColor.Type1, new[] { coordinate });
                else if (choice == 1) ApplyStroke(LevelBrush.Random, RabbitColor.Type1, new[] { coordinate });
                else if (choice - 2 < colors.Count) ApplyStroke(LevelBrush.Fixed, colors[choice - 2], new[] { coordinate });
            });
            selectedProperties.Add(kind);
            if (!level.Board.Cells[coordinate.Row * 10 + coordinate.Column].IsActive)
                selectedProperties.Add(new Label("비활성 칸은 기존 블록 지우기만 가능합니다."));
        }

        private void ApplyStroke(LevelBrush brush, RabbitColor color, IReadOnlyCollection<BoardCoordinate> coordinates)
        {
            board.CancelStroke();
            if (brush == LevelBrush.Source || brush == LevelBrush.SourceErase)
                operation.text = LevelSupplyEditing.PlaceSources(level, coordinates, brush == LevelBrush.SourceErase) ?? "생성구 편집 완료";
            else if (brush == LevelBrush.Recovery || brush == LevelBrush.RecoveryErase)
                operation.text = LevelSupplyEditing.PlaceRecovery(level, coordinates, brush == LevelBrush.RecoveryErase) ?? "회수 부품 편집 완료";
            else if (brush == LevelBrush.Placement)
                operation.text = LevelObstacleEditing.Apply(level, board.Placement, coordinates).ToString();
            else if (brush == LevelBrush.Fixed || brush == LevelBrush.Random || brush == LevelBrush.Erase)
                operation.text = LevelObstacleEditing.Apply(level, new PlacementBrush
                {
                    Layer = PlacementLayer.Block, Kind = brush == LevelBrush.Fixed ? (int)InitialBlockKind.FixedNormal : (int)InitialBlockKind.RandomNormal,
                    Color = color, Erase = brush == LevelBrush.Erase
                }, coordinates).ToString();
            else
            {
                int changed = LevelBoardEditing.Apply(level, brush, color, coordinates);
                operation.text = changed == 0 ? "변경 없음. 보드 구조와 현재 상태를 확인하세요." : $"{changed}칸 변경 · 실행 취소 한 번으로 복구할 수 있습니다.";
            }
            Refresh();
        }

        private void Save()
        {
            board.CancelStroke();
            if (level == null) return;
            AssetDatabase.SaveAssetIfDirty(level);
            state.text = AssetDatabase.GetAssetPath(level) + "  • 저장됨";
        }

        private void Validate()
        {
            board.CancelStroke();
            if (level == null) return;
            List<LevelValidationIssue> found = LevelDefinitionValidator.Validate(level);
            found.AddRange(LevelAssetOperations.FindNumberConflicts(level));
            issues.Clear();
            validationDrawer.text = found.Count == 0 ? "구조 검사 · 오류 없음" : $"구조 검사 · 오류 {found.Count}개";
            validationDrawer.value = true;
            result.text = (found.Count == 0 ? "구조·공급 수량 오류 없음 (플레이 가능 판정 아님)" : $"구조 오류 {found.Count}개") + " · 실제 낙하/공급 실행/도달 가능성/시작 매칭/난이도 검사 미지원";
            board.SetErrors(found.Where(issue => issue.Coordinate.HasValue).Select(issue => issue.Coordinate.Value));
            foreach (LevelValidationIssue issue in found)
            {
                LevelDefinition issueOwner = level;
                string issueSnapshot = JsonUtility.ToJson(level);
                Button item = new Button(() =>
                {
                    board.CancelStroke();
                    if (level != issueOwner || JsonUtility.ToJson(level) != issueSnapshot) { Refresh(); return; }
                    if (issue.Coordinate.HasValue && issue.Coordinate.Value.Row >= 0 && issue.Coordinate.Value.Row < 10 &&
                        issue.Coordinate.Value.Column >= 0 && issue.Coordinate.Value.Column < 10)
                    {
                        if (issue.PropertyPath.StartsWith("obstacles")) board.Layer = PlacementLayer.Obstacle;
                        else if (issue.PropertyPath.StartsWith("covers")) board.Layer = PlacementLayer.Cover;
                        else if (issue.PropertyPath.StartsWith("dust")) board.Layer = PlacementLayer.Dust;
                        else board.Layer = PlacementLayer.Block;
                        board.Brush = LevelBrush.Select;
                        if (issue.PropertyPath.StartsWith("supply.sources"))
                        {
                            board.Brush = LevelBrush.SourceSelect;
                            selectedSources.Clear();
                            if (LevelSupplyRules.FindSource(level, issue.Coordinate.Value) >= 0) selectedSources.Add(issue.Coordinate.Value);
                            selectedSupplyItem = -1;
                            int start = issue.PropertyPath.LastIndexOf('[', issue.PropertyPath.Length - 1);
                            int end = issue.PropertyPath.LastIndexOf(']');
                            if (issue.PropertyPath.Contains(".items.") && start >= 0 && end > start &&
                                int.TryParse(issue.PropertyPath.Substring(start + 1, end - start - 1), out int index)) selectedSupplyItem = index;
                        }
                        Refresh();
                        Validate();
                        SelectCell(issue.Coordinate.Value);
                        boardScroll.ScrollTo(board.CellAt(issue.Coordinate.Value));
                        if (issue.PropertyPath.StartsWith("flow") || issue.PropertyPath.StartsWith("connections"))
                        {
                            board.Brush = LevelBrush.Flow;
                            toolPage = 1;
                            flowTool = FlowTool.Select;
                            flowOverlay.Display(level, flowTool);
                            tools.Clear();
                            BuildToolPanel(LevelBoardEditing.CanEdit(level));
                            HighlightFlowIssue(issue);
                            BuildFlowProperties();
                        }
                    }
                    else
                    {
                        operation.text = issue.ToString();
                        if (issue.PropertyPath.StartsWith("missions"))
                        {
                            ShowInspectorPage(1);
                            Foldout missions = rootVisualElement.Q<Foldout>("mission-settings");
                            if (missions != null) { missions.value = true; rootVisualElement.Q<ScrollView>("inspector-scroll").ScrollTo(missions); }
                        }
                        else if (issue.PropertyPath.StartsWith("supply"))
                        {
                            ShowInspectorPage(1);
                            Foldout maintenance = rootVisualElement.Q<Foldout>("supply-maintenance");
                            if (maintenance != null) { maintenance.value = true; rootVisualElement.Q<ScrollView>("inspector-scroll").ScrollTo(maintenance); }
                        }
                    }
                }) { text = issue.ToString() };
                item.style.whiteSpace = WhiteSpace.Normal;
                issues.Add(item);
            }
        }

        private void InvalidateResults()
        {
            if (issues == null) return;
            issues.Clear();
            validationDrawer.text = "구조 검사 · 검사 필요";
            result.text = "검사 전이거나 데이터가 변경되었습니다. 다시 검사하세요.";
            board.SetErrors(Array.Empty<BoardCoordinate>());
        }
    }
}



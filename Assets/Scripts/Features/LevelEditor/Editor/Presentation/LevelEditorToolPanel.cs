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
        private readonly HashSet<string> expandedUsedGroups = new HashSet<string>();

        private sealed class ToolOption
        {
            public string Label;
            public LevelBrush Brush;
            public PlacementLayer Layer;
            public int Kind;
            public RabbitColor Color;

            public ToolOption(string label, LevelBrush brush, PlacementLayer layer, int kind = 0, RabbitColor color = RabbitColor.Type1)
            { Label = label; Brush = brush; Layer = layer; Kind = kind; Color = color; }
        }

        private void BuildToolPanel(bool editable)
        {
            VisualElement host = tools;
            VisualElement placementPage = new VisualElement { name = "placement-page" };
            VisualElement flowPage = new VisualElement { name = "flow-page" };
            VisualElement usedPage = new VisualElement { name = "used-page" };
            host.Add(CreateToolTabs());
            host.Add(placementPage);
            host.Add(flowPage);
            host.Add(usedPage);
            tools = placementPage;
            PopupField<string> layer = new PopupField<string>("편집 층", LayerNames, (int)board.Layer) { name = "placement-layer", tooltip = "선택하거나 지울 층" };
            layer.AddToClassList("compact-menu");
            layer.RegisterValueChangedCallback(evt =>
            {
                board.CancelStroke();
                board.Layer = (PlacementLayer)LayerNames.IndexOf(evt.newValue);
                board.Brush = LevelBrush.Select;
                Refresh();
            });
            tools.Add(layer);
            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            Button select = new Button(() => { board.CancelStroke(); board.Brush = LevelBrush.Select; Refresh(); })
                { text = board.Brush == LevelBrush.Select ? "● 선택" : "선택", name = "tool-Select" };
            select.style.flexGrow = 1;
            actions.Add(select);
            Button erase = new Button(() =>
            {
                board.CancelStroke();
                board.Brush = board.Layer == PlacementLayer.Block ? LevelBrush.Erase : LevelBrush.Placement;
                board.Placement = new PlacementBrush { Layer = board.Layer, Erase = true };
                operation.text = LayerNames[(int)board.Layer] + " 층 지우기";
                Refresh();
            }) { text = "층 지우기", name = "erase-layer" };
            erase.SetEnabled(editable);
            erase.style.flexGrow = 1;
            actions.Add(erase);
            tools.Add(actions);

            BuildElementCatalogTools(editable);

            AddToolMenu("보드", "menu-board", new List<ToolOption>
            {
                new ToolOption("칸 활성화", LevelBrush.Activate, board.Layer),
                new ToolOption("칸 비활성화", LevelBrush.Deactivate, board.Layer)
            }, editable);
            List<ToolOption> normal = new List<ToolOption> { new ToolOption("무작위 ?", LevelBrush.Random, PlacementLayer.Block) };
            if (level.Colors != null)
                foreach (RabbitColor color in level.Colors.Where(value => Enum.IsDefined(typeof(RabbitColor), value)).Distinct())
                    normal.Add(new ToolOption("고정 " + ((int)color + 1), LevelBrush.Fixed, PlacementLayer.Block, color: color));
            AddToolMenu("일반 블록", "menu-normal", normal, editable);
            AddToolMenu("파워 블록", "menu-power", Enum.GetValues(typeof(InitialBlockKind)).Cast<InitialBlockKind>()
                .Where(kind => !LevelPlacementRules.IsNormal(kind)).Select(kind =>
                    new ToolOption(LevelPlacementRules.Name(kind), LevelBrush.Placement, PlacementLayer.Block, (int)kind)).ToList(), editable);
            List<ToolOption> obstacles = Enum.GetValues(typeof(ObstacleKind)).Cast<ObstacleKind>()
                .Where(kind => kind != ObstacleKind.Generator && (kind != ObstacleKind.ColorLock || normal.Count > 1))
                .Select(kind => new ToolOption(LevelPlacementRules.Name(kind), LevelBrush.Placement, PlacementLayer.Obstacle, (int)kind)).ToList();
            AddToolMenu("장애물", "menu-obstacle", obstacles, editable);
            AddToolMenu("덮개", "menu-cover", new List<ToolOption>
            {
                new ToolOption("거미줄", LevelBrush.Placement, PlacementLayer.Cover, (int)CoverKind.Web),
                new ToolOption("우주 곰팡이", LevelBrush.Placement, PlacementLayer.Cover, (int)CoverKind.Mold)
            }, editable);
            AddToolMenu("바닥", "menu-floor", new List<ToolOption> { new ToolOption("먼지", LevelBrush.Placement, PlacementLayer.Dust) }, editable);
            AddToolMenu("장치", "menu-device", new List<ToolOption>
                { new ToolOption("고장 난 발전기", LevelBrush.Placement, PlacementLayer.Obstacle, (int)ObstacleKind.Generator) }, editable);
            AddToolMenu("생성·회수", "menu-supply", new List<ToolOption>
            {
                new ToolOption("생성구 선택", LevelBrush.SourceSelect, PlacementLayer.Block),
                new ToolOption("생성구 배치", LevelBrush.Source, PlacementLayer.Block),
                new ToolOption("생성구 삭제", LevelBrush.SourceErase, PlacementLayer.Block),
                new ToolOption("회수 부품", LevelBrush.Recovery, PlacementLayer.Block),
                new ToolOption("부품 삭제", LevelBrush.RecoveryErase, PlacementLayer.Block)
            }, editable && LevelSupplyEditing.CanEdit(level));
            tools = flowPage;
            tools.Add(new Label("중력 · 경로 · 장치 연결") { name = "flow-heading" });
            BuildFlowMenus(editable);
            BuildConnectionList();
            tools = usedPage;
            BuildUsedPlacements();
            BuildSourceList();
            tools = host;
            placementPage.style.display = toolPage == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            flowPage.style.display = toolPage == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            usedPage.style.display = toolPage == 2 ? DisplayStyle.Flex : DisplayStyle.None;
            Foldout legend = new Foldout { text = "표시 안내", value = false };
            legend.Add(new Label("· 빈칸   × 비활성\n? 무작위   ! 오류\n안쪽 윤곽: 덮개\n우측 아래 숫자: 먼지"));
            tools.Add(legend);
        }

        private void AddToolMenu(string title, string name, List<ToolOption> options, bool editable)
        {
            List<string> choices = new List<string> { "선택…" };
            choices.AddRange(options.Select(option => option.Label));
            int selectedOption = options.FindIndex(option => option.Brush == board.Brush && option.Layer == board.Layer &&
                (option.Brush != LevelBrush.Fixed || option.Color == board.Color) &&
                (option.Brush != LevelBrush.Placement || (!board.Placement.Erase && board.Placement.Layer == option.Layer && board.Placement.Kind == option.Kind)));
            PopupField<string> menu = new PopupField<string>(title, choices, selectedOption + 1) { name = name, tooltip = title + " 배치 도구" };
            menu.AddToClassList("compact-menu");
            menu.SetEnabled(editable);
            menu.RegisterValueChangedCallback(evt =>
            {
                board.CancelStroke();
                int index = choices.IndexOf(evt.newValue) - 1;
                if (index < 0) { board.Brush = LevelBrush.Select; Refresh(); return; }
                ToolOption option = options[index];
                toolPage = 0;
                board.Layer = option.Layer;
                board.Brush = option.Brush;
                board.Color = option.Color;
                RabbitColor color = level.Colors?.FirstOrDefault(value => Enum.IsDefined(typeof(RabbitColor), value)) ?? RabbitColor.Type1;
                board.Placement = new PlacementBrush { Layer = option.Layer, Kind = option.Kind, Durability = 1, RequiredCharge = 3, Color = color };
                operation.text = title + " → " + option.Label + (option.Brush == LevelBrush.Placement && board.Placement.Size == 2
                    ? " · 네 칸 미리보기 후 클릭" : " · 클릭/드래그") +
                    (option.Brush == LevelBrush.Placement || option.Brush == LevelBrush.Fixed || option.Brush == LevelBrush.Random
                        ? (option.Layer == PlacementLayer.Block || option.Layer == PlacementLayer.Obstacle
                            ? " · 더블클릭으로 블록↔장애물 교체" : " · 더블클릭으로 같은 층 교체") : "");
                Refresh();
            });
            tools.Add(menu);
            if (name == "menu-normal")
            {
                // 기존 드롭다운은 유지하고 같은 선택 경로를 사용하는 그림 버튼만 보조로 둔다.
                // 레벨에서 허용한 색만 노출하며 클릭은 위의 기존 브러시 선택 처리를 거친다.
                VisualElement rabbits = new VisualElement { name = "normal-rabbit-palette" };
                rabbits.style.flexDirection = FlexDirection.Row;
                rabbits.style.flexWrap = Wrap.Wrap;
                foreach (ToolOption option in options.Where(value => value.Brush == LevelBrush.Fixed))
                {
                    Texture2D texture = RabbitBlockArtwork.Get(option.Color);
                    Button rabbit = new Button(() => menu.value = option.Label)
                        { name = "normal-rabbit-" + (int)option.Color, tooltip = option.Label + " 달토끼 배치", text = texture == null ? ((int)option.Color + 1).ToString() : "" };
                    rabbit.style.width = rabbit.style.height = 34;
                    rabbit.style.flexShrink = 0;
                    rabbit.style.backgroundImage = texture;
                    rabbit.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                    if (board.Brush == LevelBrush.Fixed && board.Color == option.Color)
                    {
                        rabbit.style.borderLeftColor = rabbit.style.borderRightColor = Color.white;
                        rabbit.style.borderTopColor = rabbit.style.borderBottomColor = Color.white;
                    }
                    rabbit.SetEnabled(editable);
                    rabbits.Add(rabbit);
                }
                tools.Add(rabbits);
            }
        }

        private void BuildUsedPlacements()
        {
            VisualElement used = new VisualElement { name = "used-obstacles" };
            used.style.marginTop = 12;
            used.style.borderTopWidth = 1;
            used.style.borderTopColor = (Color)new Color32(105, 110, 120, 255);
            Label title = new Label("현재 사용 중인 장애물");
            LevelEditorHelp.Link(title, "배치된 장애물을 종류별로 모아 보여줍니다. 위치를 누르면 해당 칸을 선택합니다.", "obstacles.html#used");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginTop = 5;
            used.Add(title);
            tools.Add(used);
            if (level.SchemaVersion == LevelDefinition.CurrentSchemaVersion)
            {
                used.Add(new Label($"{level.Elements.Count}개 ID 배치 · 블록/장애물/덮개/먼지") { name = "used-count" });
                foreach (IGrouping<(PlacementLayer Layer, string Id), ElementPlacementDefinition> group in level.Elements
                    .GroupBy(item => (Layer: item.layer, Id: item.definitionId)).OrderBy(group => group.Key.Layer).ThenBy(group => group.Key.Id))
                {
                    string display;
                    try { display = level.CreateElementCatalog().Get(new Elements.ElementId(group.Key.Id)).DisplayName; }
                    catch (Exception error) { display = group.Key.Id + " / " + error.Message; }
                    AddUsedGroup(used, group.Key.Layer, 0, display, group.Select(item => (item.coordinate,
                        "내구 " + item.durability + " · 충전 " + item.requiredCharge)), group.Key.Id);
                }
                return;
            }
            int total = (level.Obstacles?.Count ?? 0) + (level.Covers?.Count ?? 0) + (level.Dust?.Count ?? 0);
            used.Add(new Label(total == 0 ? "아직 배치된 장애물이 없습니다." : $"{total}개 배치 · 덮개/먼지/장치 포함") { name = "used-count" });
            if (level.Obstacles != null)
                foreach (IGrouping<ObstacleKind, ObstaclePlacementDefinition> group in level.Obstacles.GroupBy(item => item.Kind).OrderBy(group => (int)group.Key))
                    AddUsedGroup(used, PlacementLayer.Obstacle, (int)group.Key, LevelPlacementRules.Name(group.Key),
                        group.Select(item => (item.Coordinate, item.Kind == ObstacleKind.Generator ? "충전 " + item.RequiredCharge : "내구 " + item.Durability)));
            if (level.Covers != null)
                foreach (IGrouping<CoverKind, CoverPlacementDefinition> group in level.Covers.GroupBy(item => item.Kind).OrderBy(group => (int)group.Key))
                    AddUsedGroup(used, PlacementLayer.Cover, (int)group.Key, group.Key == CoverKind.Web ? "거미줄" : group.Key == CoverKind.Mold ? "우주 곰팡이" : "잘못된 덮개",
                        group.Select(item => (item.Coordinate, "내구 " + item.Durability)));
            if (level.Dust != null && level.Dust.Count > 0)
                AddUsedGroup(used, PlacementLayer.Dust, 0, "먼지", level.Dust.Select(item => (item.Coordinate, "내구 " + item.Durability)));
        }

        private void AddUsedGroup(VisualElement parent, PlacementLayer layer, int kind, string title,
            IEnumerable<(BoardCoordinate Coordinate, string Value)> placements, string definitionId = null)
        {
            List<(BoardCoordinate Coordinate, string Value)> items = placements.OrderBy(item => item.Coordinate.Row).ThenBy(item => item.Coordinate.Column).ToList();
            string key = layer + "-" + (definitionId ?? kind.ToString());
            Foldout group = new Foldout { text = $"{title} · {items.Count}개", name = "used-" + key, value = expandedUsedGroups.Contains(key) };
            group.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) expandedUsedGroups.Add(key); else expandedUsedGroups.Remove(key);
            });
            LevelDefinition owner = level;
            string source = JsonUtility.ToJson(level);
            for (int i = 0; i < items.Count; i++)
            {
                BoardCoordinate coordinate = items[i].Coordinate;
                bool inBoard = coordinate.Row >= 0 && coordinate.Row < BoardDefinition.DefaultRows && coordinate.Column >= 0 && coordinate.Column < BoardDefinition.DefaultColumns;
                Button item = new Button(() =>
                {
                    board.CancelStroke();
                    if (level != owner || JsonUtility.ToJson(level) != source) { Refresh(); return; }
                    board.Layer = layer;
                    board.Brush = LevelBrush.Select;
                    selected = inBoard ? coordinate : null;
                    inspectorPage = 0;
                    Refresh();
                    if (inBoard) boardScroll.ScrollTo(board.CellAt(coordinate));
                    operation.text = title + " " + coordinate + (inBoard ? " 선택" : " · 보드 밖 기록입니다. 기존 Inspector에서 수정하세요.");
                }) { text = coordinate + " · " + items[i].Value, name = "used-item-" + key + "-" + i };
                item.style.whiteSpace = WhiteSpace.Normal;
                group.Add(item);
            }
            parent.Add(group);
        }
    }
}

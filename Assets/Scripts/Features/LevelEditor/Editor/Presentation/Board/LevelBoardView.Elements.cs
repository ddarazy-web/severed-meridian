using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Elements;
using Elements.Editor;
using Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelBoardView
    {
        private static Sprite ElementArtwork(ElementDefinition definition, ElementPlacementDefinition placement)
        {
            if (definition.Layer != null) return definition.Layer.Behavior == ElementLayerBehavior.NormalConsumption
                ? LevelBoardArtwork.Dust(placement.durability)
                : LevelBoardArtwork.Cover(definition.Layer.Behavior == ElementLayerBehavior.CoverRemoval ? CoverKind.Mold : CoverKind.Web, placement.durability);
            if (definition.ReactionBehavior.HasValue)
            {
                // 기존 아트의 의미만 연결한다. 실제 크기·수치·실행 정의는 배치 원본에서 유지한다.
                ObstacleKind kind = definition.ReactionBehavior == ElementReactionBehavior.GeneratorCharge ? ObstacleKind.Generator :
                    definition.RemovalMissionProfile.Kind switch
                    {
                        MissionKind.Crate => ObstacleKind.Crate, MissionKind.Scrap => ObstacleKind.Scrap,
                        MissionKind.Safe => ObstacleKind.Safe, MissionKind.ColorLock => ObstacleKind.ColorLock,
                        MissionKind.Appliance => ObstacleKind.Appliance, _ => throw new ArgumentException("본체 표시 미션이 없습니다.")
                    };
                return LevelBoardArtwork.Obstacle(kind, placement.color, placement.requiredCharge, placement.durability);
            }
            if (definition.Supply?.Behavior == ElementSupplyBehavior.FixedNormal) return LevelBoardArtwork.Rabbit(placement.color);
            if (definition.Supply?.Behavior == ElementSupplyBehavior.Recovery) return LevelBoardArtwork.Recovery;
            return definition.Supply?.Content switch
            {
                RuntimeContent.Rocket => LevelBoardArtwork.Block(InitialBlockKind.Rocket, placement.rocketDirection),
                RuntimeContent.Bomb => LevelBoardArtwork.Block(InitialBlockKind.Bomb, placement.rocketDirection),
                RuntimeContent.Drone => LevelBoardArtwork.Block(InitialBlockKind.Drone, placement.rocketDirection),
                RuntimeContent.Magnet => LevelBoardArtwork.Block(InitialBlockKind.Magnet, placement.rocketDirection), _ => null
            };
        }
        private void RedrawElements()
        {
            bodies.Clear(); supplyMarks.Clear();
            Dictionary<PlacementLayer, Dictionary<BoardCoordinate, int>> occupied = Enum.GetValues(typeof(PlacementLayer)).Cast<PlacementLayer>()
                .ToDictionary(layer => layer, layer => new Dictionary<BoardCoordinate, int>());
            Dictionary<int, ElementDefinition> definitions = new Dictionary<int, ElementDefinition>();
            HashSet<BoardCoordinate> invalid = new HashSet<BoardCoordinate>();
            string catalogError = null;
            try
            {
                ElementCatalog catalog = level.CreateElementCatalog();
                for (int i = 0; i < level.Elements.Count; i++)
                {
                    ElementPlacementDefinition item = level.Elements[i];
                    try
                    {
                        ElementDefinition definition = catalog.Get(new ElementId(item.definitionId));
                        PackedElementDefinition.FromDefinition(definition).ToDefinition();
                        if (ElementCatalogViewModel.LayerOf(definition) != item.layer) throw new ArgumentException("정의 층이 다릅니다.");
                        definitions.Add(i, definition);
                        foreach (BoardCoordinate coordinate in LevelPlacementRules.Footprint(item.coordinate, ElementPlacementEditing.Size(definition)))
                            if (!occupied[item.layer].TryAdd(coordinate, i)) { invalid.Add(coordinate); occupied[item.layer][coordinate] = -2; }
                    }
                    catch (Exception) { if (item != null) invalid.Add(item.coordinate); }
                }
            }
            catch (Exception error) { catalogError = error.Message; }
            int selectedBody = selected.HasValue && occupied[PlacementLayer.Obstacle].TryGetValue(selected.Value, out int selectedIndex) ? selectedIndex : -1;
            HashSet<BoardCoordinate> selectedArea = selectedBody >= 0 && Layer == PlacementLayer.Obstacle
                ? new HashSet<BoardCoordinate>(LevelPlacementRules.Footprint(level.Elements[selectedBody].coordinate, ElementPlacementEditing.Size(definitions[selectedBody])))
                : new HashSet<BoardCoordinate>();
            int previewSize = Brush == LevelBrush.Move && definitions.TryGetValue(moveIndex, out ElementDefinition moving) ? ElementPlacementEditing.Size(moving)
                : Brush == LevelBrush.Placement ? Placement.Size : 1;
            HashSet<BoardCoordinate> preview = hover.HasValue ? new HashSet<BoardCoordinate>(LevelPlacementRules.Footprint(hover.Value, previewSize)) : new HashSet<BoardCoordinate>();
            string placementError = Brush == LevelBrush.Placement && hover.HasValue ? ElementPlacementEditing.PlacementError(level, Placement, hover.Value) : null;
            for (int i = 0; i < cells.Length; i++)
            {
                BoardCoordinate coordinate = new BoardCoordinate(i / BoardDefinition.DefaultColumns, i % BoardDefinition.DefaultColumns);
                Label cell = cells[i]; bool active = level.Board != null && level.Board.TryGetCell(coordinate, out CellDefinition boardCell) && boardCell.IsActive;
                bool selectedCell = selected.HasValue && selected.Value.Equals(coordinate) || selectedArea.Contains(coordinate) || PlacementSelection.Contains(coordinate) || Brush == LevelBrush.SourceSelect && SourceSelection.Contains(coordinate);
                bool highlighted = visited.Contains(coordinate) || preview.Contains(coordinate) && (Brush == LevelBrush.Placement || Brush == LevelBrush.Move);
                bool conflict = invalid.Contains(coordinate) || errors.Contains(coordinate) || catalogError != null;
                cell.Clear(); cell.text = ""; cell.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                cell.style.backgroundColor = (Color)(active ? new Color32(66, 72, 84, 255) : new Color32(35, 38, 45, 255));
                cell.style.opacity = active ? 1 : 0.55f;
                Color border = conflict || highlighted && placementError != null ? UnityEngine.Color.red : highlighted ? UnityEngine.Color.yellow : selectedCell ? UnityEngine.Color.white : new Color32(24, 27, 32, 255);
                cell.style.borderLeftColor = cell.style.borderRightColor = cell.style.borderTopColor = cell.style.borderBottomColor = border;
                cell.style.borderLeftWidth = cell.style.borderRightWidth = cell.style.borderTopWidth = cell.style.borderBottomWidth = selectedCell || highlighted ? 3 : 1;
                SetFloor(cell, coordinate, active);
                SetArtworkLayer(cell, "board-arrival-art", active && level.Flow != null && level.Flow.Arrivals.Contains(coordinate) ? LevelBoardArtwork.Arrival : null);
                string badge = active ? "·" : "×"; string tooltip = coordinate + (catalogError == null ? "" : " / " + catalogError);
                foreach (PlacementLayer layer in new[] { PlacementLayer.Dust, PlacementLayer.Block, PlacementLayer.Obstacle, PlacementLayer.Cover })
                {
                    if (!occupied[layer].TryGetValue(coordinate, out int index) || index < 0) continue;
                    ElementPlacementDefinition item = level.Elements[index]; ElementDefinition definition = definitions[index];
                    tooltip += " / " + definition.DisplayName + " [" + item.definitionId + "]";
                    int size = ElementPlacementEditing.Size(definition);
                    if (layer == PlacementLayer.Obstacle && size > 1) { badge = ""; continue; }
                    Sprite artwork = ElementArtwork(definition, item);
                    string layerName = layer == PlacementLayer.Dust ? "board-dust-art" : layer == PlacementLayer.Cover ? "board-cover-art" : "board-content-art";
                    SetArtworkLayer(cell, layerName, active ? artwork : null);
                    if (layer == PlacementLayer.Block || layer == PlacementLayer.Obstacle) badge = artwork == null ? definition.Supply?.Behavior == ElementSupplyBehavior.RandomNormal ? "?" : definition.DisplayName : "";
                    if (definition.Placement != null && (layer == PlacementLayer.Dust || layer == PlacementLayer.Cover || artwork == null)) badge += item.durability;
                    if (layerName == "board-content-art")
                    {
                        VisualElement content = cell.Q<VisualElement>(layerName); float scale = BoardArtworkLayout.ContentScale(artwork);
                        content.style.scale = new Scale(Vector3.one * scale);
                        content.style.translate = new Translate(Length.Percent(100 * scale * BoardArtworkLayout.ContentOffsetX(artwork)),
                            Length.Percent(-100 * scale * BoardArtworkLayout.ContentOffsetY(artwork)));
                    }
                }
                if (active && level.Flow?.Portals != null)
                    for (int pair = 0; pair < level.Flow.Portals.Count; pair++)
                    {
                        FlowPortal portal = level.Flow.Portals[pair];
                        if (portal.Entrance.Equals(coordinate) || portal.HasExit && portal.Exit.Equals(coordinate))
                            SetArtworkLayer(cell, "board-portal-art", LevelBoardArtwork.Portal(pair, portal.HasExit && portal.Exit.Equals(coordinate)));
                    }
                Label label = new Label(badge + (conflict ? "!" : "")) { name = "board-art-badge", pickingMode = PickingMode.Ignore };
                label.style.position = Position.Absolute; label.style.left = label.style.top = 1; label.style.color = UnityEngine.Color.white;
                label.style.unityTextOutlineWidth = 1; label.style.unityTextOutlineColor = UnityEngine.Color.black;
                cell.Add(label); cell.tooltip = tooltip;
                if (LevelSupplyRules.FindSource(level, coordinate) != -1)
                {
                    Label sourceMark = new Label("생") { name = "supply-mark-" + i, pickingMode = PickingMode.Ignore };
                    sourceMark.style.position = Position.Absolute;
                    sourceMark.style.left = coordinate.Column * CellSize + 1; sourceMark.style.top = coordinate.Row * CellSize;
                    sourceMark.style.fontSize = 9; sourceMark.style.color = (Color)new Color32(135, 234, 206, 255);
                    supplyMarks.Add(sourceMark);
                }
            }
            foreach (KeyValuePair<int, ElementDefinition> entry in definitions.Where(entry => level.Elements[entry.Key].layer == PlacementLayer.Obstacle && ElementPlacementEditing.Size(entry.Value) > 1))
            {
                ElementPlacementDefinition item = level.Elements[entry.Key]; int size = ElementPlacementEditing.Size(entry.Value);
                Label body = new Label(entry.Value.DisplayName + "\n" + (entry.Value.ChargePlacement != null ? item.requiredCharge : item.durability))
                    { name = "element-body-" + entry.Key, pickingMode = PickingMode.Ignore, tooltip = item.definitionId + " / " + item.instanceId };
                body.style.position = Position.Absolute;
                float extent = size == 2 ? BoardArtworkLayout.LargeObstacleSize : size - 0.1f; float inset = CellSize * (size - extent) * 0.5f;
                body.style.left = item.coordinate.Column * CellSize + inset; body.style.top = item.coordinate.Row * CellSize + inset;
                body.style.width = body.style.height = CellSize * extent;
                body.style.unityTextAlign = TextAnchor.MiddleCenter; body.style.color = UnityEngine.Color.white;
                Sprite artwork = ElementArtwork(entry.Value, item);
                if (artwork != null) { body.text = ""; body.style.backgroundImage = new StyleBackground(artwork); body.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain); }
                bodies.Add(body);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    /// <summary>신형 배치 원본을 정의 프로필로 검사하고 기존 Undo 경계에서 편집한다.</summary>
    public static class ElementPlacementEditing
    {
        public static int Size(ElementDefinition definition) => definition.Placement?.Size ?? definition.ChargePlacement?.Size ?? 1;
        public static PlacementBrush ForDefinition(LevelDefinition level, ElementId id)
        {
            ElementDefinition definition = level.CreateElementCatalog().Get(id);
            PackedElementDefinition.FromDefinition(definition).ToDefinition();
            PlacementLayer? layer = ElementCatalogViewModel.LayerOf(definition);
            if (!layer.HasValue) throw new InvalidOperationException("직접 배치할 수 없는 공급 정의입니다.");
            return new PlacementBrush { DefinitionId = id.Value, DefinitionSize = Size(definition), Layer = layer.Value,
                Durability = definition.Placement?.MaxDurability ?? 0, RequiredCharge = definition.ChargePlacement?.MinRequiredCharge ?? 0,
                Color = level.Colors.FirstOrDefault() };
        }
        private static ElementDefinition Definition(ElementCatalog catalog, ElementPlacementDefinition item) => catalog.Get(new ElementId(item.definitionId));
        public static int Find(LevelDefinition level, PlacementLayer layer, BoardCoordinate coordinate) =>
            Find(level.Elements, level.CreateElementCatalog(), layer, coordinate);
        private static int Find(IReadOnlyList<ElementPlacementDefinition> items, ElementCatalog catalog, PlacementLayer layer, BoardCoordinate coordinate)
        {
            int found = -1;
            for (int i = 0; i < items.Count; i++)
                if (items[i].layer == layer && LevelPlacementRules.Footprint(items[i].coordinate, Size(Definition(catalog, items[i]))).Contains(coordinate))
                { if (found >= 0) return -2; found = i; }
            return found;
        }
        private static ElementDefinition BrushDefinition(ElementCatalog catalog, PlacementBrush brush)
        {
            if (!string.IsNullOrEmpty(brush.DefinitionId)) return catalog.Get(new ElementId(brush.DefinitionId));
            // 기존 팔레트 입력도 신형 목록에만 기록한다.
            ElementId id = brush.Layer switch
            {
                PlacementLayer.Obstacle => LegacyElementMap.Get((ObstacleKind)brush.Kind),
                PlacementLayer.Cover => LegacyElementMap.Get((CoverKind)brush.Kind),
                PlacementLayer.Dust => LegacyElementDefinitions.GetDust().Id,
                _ => LegacyElementMap.Get((InitialBlockKind)brush.Kind switch
                {
                    InitialBlockKind.RandomNormal => SupplyKind.RandomNormal, InitialBlockKind.FixedNormal => SupplyKind.FixedNormal,
                    InitialBlockKind.Rocket => SupplyKind.Rocket, InitialBlockKind.Bomb => SupplyKind.Bomb,
                    InitialBlockKind.Drone => SupplyKind.Drone, InitialBlockKind.Magnet => SupplyKind.Magnet,
                    _ => throw new ArgumentException("등록되지 않은 블록입니다.")
                })
            };
            return catalog.Get(id);
        }
        public static string PlacementError(LevelDefinition level, PlacementBrush brush, BoardCoordinate coordinate)
        {
            if (!LevelBoardEditing.CanEdit(level)) return "편집할 수 없는 보드입니다.";
            try { return Error(level, level.Elements, level.CreateElementCatalog(), brush, coordinate); }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is KeyNotFoundException)
            { return error.Message; }
        }
        private static string Error(LevelDefinition level, IReadOnlyList<ElementPlacementDefinition> items, ElementCatalog catalog,
            PlacementBrush brush, BoardCoordinate coordinate, int moving = -1)
        {
            int index = Find(items, catalog, brush.Layer, coordinate);
            if (index == -2) return "중복 배치는 먼저 수정하세요.";
            if (index >= 0 && !string.IsNullOrEmpty(items[index].instanceId) && items.Count(item => item.instanceId == items[index].instanceId) > 1)
                return "중복 본체 ID는 편집할 수 없습니다.";
            if (index >= 0 && LevelPlacementRules.Footprint(items[index].coordinate, Size(Definition(catalog, items[index])))
                .Any(cell => Find(items, catalog, brush.Layer, cell) != index)) return "본체 일부가 중복 점유되어 있습니다.";
            if (brush.Erase) return null;
            ElementDefinition definition = BrushDefinition(catalog, brush);
            PackedElementDefinition.FromDefinition(definition).ToDefinition();
            if (ElementCatalogViewModel.LayerOf(definition) != brush.Layer) return "정의와 편집 층이 다릅니다.";
            if (definition.Placement != null && (brush.Durability < 1 || brush.Durability > definition.Placement.MaxDurability))
                return $"내구도는 1~{definition.Placement.MaxDurability}입니다.";
            if (definition.ChargePlacement != null && (brush.RequiredCharge < definition.ChargePlacement.MinRequiredCharge ||
                brush.RequiredCharge > definition.ChargePlacement.MaxRequiredCharge)) return "필요 충전량이 정의 범위 밖입니다.";
            bool colored = definition.ColorMatchPolicy?.RequiresMatchingColor == true || definition.Supply?.Behavior == ElementSupplyBehavior.FixedNormal;
            if (colored && (!Enum.IsDefined(typeof(RabbitColor), brush.Color) || !level.Colors.Contains(brush.Color))) return "레벨 사용 색을 선택하세요.";
            if (definition.Supply?.Content == Simulation.RuntimeContent.Rocket && !Enum.IsDefined(typeof(RocketDirection), brush.Direction)) return "로켓 방향이 잘못됐습니다.";
            if (moving < 0 && index >= 0 && !brush.ReplaceExisting && (items[index].definitionId != definition.Id.Value || Size(definition) > 1))
                return "더블클릭으로 교체하거나 속성에서 수정하세요.";
            if (moving < 0 && index >= 0 && brush.ReplaceExisting) coordinate = items[index].coordinate;
            if (brush.Layer == PlacementLayer.Obstacle && level.Flow?.Walls != null &&
                level.Flow.Walls.Any(wall => LevelFlowRules.InternalWall(wall, coordinate, Size(definition)))) return "본체 내부에 벽이 있습니다.";
            foreach (BoardCoordinate cell in LevelPlacementRules.Footprint(coordinate, Size(definition)))
            {
                string cellError = LevelPlacementRules.CellError(level, cell);
                if (cellError != null) return cellError;
                foreach (PlacementLayer layer in Enum.GetValues(typeof(PlacementLayer)))
                {
                    int occupied = Find(items, catalog, layer, cell);
                    if (occupied == -2) return "중복 점유 칸은 편집할 수 없습니다.";
                    if (occupied < 0 || occupied == moving || moving < 0 && occupied == index && layer == brush.Layer) continue;
                    ElementDefinition other = Definition(catalog, items[occupied]);
                    if (brush.Layer == layer) return "다른 요소가 점유합니다.";
                    if (brush.Layer == PlacementLayer.Dust || layer == PlacementLayer.Dust) continue;
                    if (brush.Layer == PlacementLayer.Obstacle && !brush.ReplaceExisting) return "블록 또는 덮개가 점유합니다.";
                    if (layer == PlacementLayer.Obstacle && (brush.Layer != PlacementLayer.Block || !brush.ReplaceExisting)) return "장애물이 점유합니다.";
                    if (brush.Layer == PlacementLayer.Block && layer == PlacementLayer.Cover &&
                        (other.Layer?.Behavior == ElementLayerBehavior.CoverRemoval || definition.Supply?.Behavior == ElementSupplyBehavior.Recovery)) return "해당 덮개와 겹칠 수 없습니다.";
                    if (brush.Layer == PlacementLayer.Cover && layer == PlacementLayer.Block && other.Supply?.Behavior == ElementSupplyBehavior.Recovery) return "회수 부품과 겹칠 수 없습니다.";
                }
            }
            return null;
        }
        public static PlacementEditResult Apply(LevelDefinition level, PlacementBrush brush, IEnumerable<BoardCoordinate> coordinates)
        {
            PlacementEditResult result = new PlacementEditResult();
            BoardCoordinate[] targets = coordinates.Distinct().ToArray();
            if (!LevelBoardEditing.CanEdit(level)) { result.Skipped = targets.Length; result.Reasons.Add("편집할 수 없는 보드입니다."); return result; }
            try
            {
                ElementCatalog catalog = level.CreateElementCatalog();
                if (!brush.Erase && (Size(BrushDefinition(catalog, brush)) > 1 || brush.ReplaceExisting) && targets.Length != 1)
                { result.Skipped = targets.Length; result.Reasons.Add("본체 교체·다칸 배치는 한 칸씩 처리하세요."); return result; }
                List<ElementPlacementDefinition> items = level.Elements.Select(item => JsonUtility.FromJson<ElementPlacementDefinition>(JsonUtility.ToJson(item))).ToList();
                HashSet<string> removed = new HashSet<string>();
                foreach (BoardCoordinate cell in targets)
                {
                    string error = Error(level, items, catalog, brush, cell);
                    if (error != null) { result.Skipped++; result.Reasons.Add(error); continue; }
                    int index = Find(items, catalog, brush.Layer, cell);
                    if (brush.Erase)
                    {
                        if (index >= 0) { removed.Add(items[index].instanceId); items.RemoveAt(index); result.Changed++; }
                        continue;
                    }
                    ElementDefinition definition = BrushDefinition(catalog, brush);
                    BoardCoordinate origin = index >= 0 && brush.ReplaceExisting ? items[index].coordinate : cell;
                    ElementPlacementDefinition existing = index >= 0 ? items[index] : null;
                    ElementPlacementDefinition replacement = new ElementPlacementDefinition { definitionId = definition.Id.Value,
                        instanceId = existing != null && existing.definitionId == definition.Id.Value ? existing.instanceId : Guid.NewGuid().ToString("N"),
                        layer = brush.Layer, coordinate = origin, durability = definition.Placement != null ? brush.Durability : 0,
                        requiredCharge = definition.ChargePlacement != null ? brush.RequiredCharge : 0,
                        hasColor = definition.ColorMatchPolicy?.RequiresMatchingColor == true || definition.Supply?.Behavior == ElementSupplyBehavior.FixedNormal,
                        color = brush.Color, rocketDirection = brush.Direction };
                    if (existing != null && JsonUtility.ToJson(existing) == JsonUtility.ToJson(replacement)) continue;
                    HashSet<BoardCoordinate> footprint = new HashSet<BoardCoordinate>(LevelPlacementRules.Footprint(origin, Size(definition)));
                    for (int i = items.Count - 1; i >= 0; i--)
                    {
                        bool remove = i == index || brush.ReplaceExisting &&
                            (brush.Layer == PlacementLayer.Obstacle && (items[i].layer == PlacementLayer.Block || items[i].layer == PlacementLayer.Cover) && footprint.Contains(items[i].coordinate) ||
                             brush.Layer == PlacementLayer.Block && items[i].layer == PlacementLayer.Obstacle &&
                             LevelPlacementRules.Footprint(items[i].coordinate, Size(Definition(catalog, items[i]))).Contains(cell));
                        if (!remove) continue;
                        if (items[i].instanceId != replacement.instanceId) removed.Add(items[i].instanceId);
                        items.RemoveAt(i);
                    }
                    if (index >= 0 && index <= items.Count) items.Insert(index, replacement); else items.Add(replacement);
                    result.Changed++;
                }
                if (result.Changed == 0) return result;
                using SerializedObject data = new SerializedObject(level);
                SerializedProperty list = data.FindProperty("elements"); list.arraySize = items.Count;
                for (int i = 0; i < items.Count; i++) Write(list.GetArrayElementAtIndex(i), items[i]);
                SerializedProperty connections = data.FindProperty("connections");
                for (int i = connections.arraySize - 1; i >= 0; i--)
                    if (removed.Contains(connections.GetArrayElementAtIndex(i).FindPropertyRelative("generatorId").stringValue) ||
                        removed.Contains(connections.GetArrayElementAtIndex(i).FindPropertyRelative("targetId").stringValue)) connections.DeleteArrayElementAtIndex(i);
                LevelObstacleEditing.Commit(data, "요소 ID 배치 편집");
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is KeyNotFoundException)
            { result.Changed = 0; result.Skipped = targets.Length; result.Reasons.Add(error.Message); }
            return result;
        }
        private static void Write(SerializedProperty item, ElementPlacementDefinition value)
        {
            item.FindPropertyRelative("definitionId").stringValue = value.definitionId;
            item.FindPropertyRelative("instanceId").stringValue = value.instanceId;
            item.FindPropertyRelative("layer").intValue = (int)value.layer;
            item.FindPropertyRelative("coordinate.row").intValue = value.coordinate.Row;
            item.FindPropertyRelative("coordinate.column").intValue = value.coordinate.Column;
            item.FindPropertyRelative("durability").intValue = value.durability;
            item.FindPropertyRelative("requiredCharge").intValue = value.requiredCharge;
            item.FindPropertyRelative("hasColor").boolValue = value.hasColor;
            item.FindPropertyRelative("color").intValue = (int)value.color;
            item.FindPropertyRelative("rocketDirection").intValue = (int)value.rocketDirection;
        }
        public static bool Move(LevelDefinition level, int index, BoardCoordinate destination, out string message)
        {
            if (!LevelBoardEditing.CanEdit(level) || index < 0 || index >= level.Elements.Count || level.Elements[index].layer != PlacementLayer.Obstacle)
            { message = "이동할 본체를 선택하세요."; return false; }
            ElementPlacementDefinition item = level.Elements[index];
            PlacementBrush brush = ForDefinition(level, new ElementId(item.definitionId));
            brush.Durability = item.durability; brush.RequiredCharge = item.requiredCharge; brush.Color = item.color;
            message = Error(level, level.Elements, level.CreateElementCatalog(), brush, destination, index);
            if (message != null) return false;
            if (item.coordinate.Equals(destination)) { message = "변경 없음: 원래 위치입니다."; return false; }
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty value = data.FindProperty("elements").GetArrayElementAtIndex(index);
            value.FindPropertyRelative("coordinate.row").intValue = destination.Row;
            value.FindPropertyRelative("coordinate.column").intValue = destination.Column;
            LevelObstacleEditing.Commit(data, "요소 본체 이동"); message = "본체 ID를 유지하며 이동했습니다."; return true;
        }
    }
}

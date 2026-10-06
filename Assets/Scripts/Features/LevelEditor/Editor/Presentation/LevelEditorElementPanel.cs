using System;
using System.Linq;
using Board;
using Elements;
using Elements.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        private void BuildElementCatalogTools(bool editable)
        {
            if (level.SchemaVersion == LevelDefinition.LegacySchemaVersion)
            {
                tools.Add(new Button(() =>
                {
                    try
                    {
                        ElementPlacementDefinition[] preview = LevelElementMigration.Preview(level);
                        operation.text = $"ID 변환 미리보기: 배치 {preview.Length}개 · 원본 변경 없음";
                    }
                    catch (Exception error) { operation.text = error.Message; }
                }) { text = "ID 변환 미리보기", name = "preview-elements" });
                tools.Add(new Button(() =>
                {
                    board.CancelStroke();
                    try { LevelElementMigration.Apply(level); operation.text = "선택 레벨을 ID 형식으로 변환했습니다. Undo로 복원할 수 있습니다."; }
                    catch (Exception error) { operation.text = error.Message; }
                    Refresh();
                }) { text = "이 레벨에 ID 변환 적용", name = "apply-elements" });
                return;
            }
            if (level.SchemaVersion != LevelDefinition.CurrentSchemaVersion) return;
            UnityEditor.UIElements.ObjectField catalogField = new UnityEditor.UIElements.ObjectField("요소 카탈로그")
                { objectType = typeof(ElementCatalogAsset), name = "element-catalog-asset" };
            catalogField.SetValueWithoutNotify(level.ElementCatalog);
            LevelDefinition source = level;
            catalogField.RegisterValueChangedCallback(evt =>
            {
                if (level != source) return;
                board.CancelStroke();
                using SerializedObject edit = new SerializedObject(level);
                edit.FindProperty("elementCatalog").objectReferenceValue = evt.newValue;
                LevelObstacleEditing.Commit(edit, "레벨 요소 카탈로그 지정"); Refresh();
            });
            tools.Add(catalogField);
            try
            {
                if (catalogOwner != level)
                {
                    elementCatalogModel?.Dispose();
                    elementCatalogModel = new ElementCatalogViewModel(level.CreateElementCatalog()); catalogOwner = level;
                }
                else elementCatalogModel.SetCatalog(level.CreateElementCatalog());
                elementCatalogModel.SetLayer(board.Layer);
                elementCatalogView = new ElementCatalogView(elementCatalogModel, definition =>
                {
                    board.CancelStroke();
                    board.Placement = ElementPlacementEditing.ForDefinition(level, definition.Id);
                    board.Brush = LevelBrush.Placement; board.Layer = board.Placement.Layer;
                    operation.text = definition.DisplayName + " 배치 · 더블클릭으로 교체";
                    Refresh();
                });
                elementCatalogView.SetEnabled(editable); tools.Add(elementCatalogView);
            }
            catch (Exception error) { tools.Add(new HelpBox(error.Message, HelpBoxMessageType.Error)); }
        }

        private void BuildElementProperties(BoardCoordinate coordinate)
        {
            try
            {
                int index = ElementPlacementEditing.Find(level, board.Layer, coordinate);
                if (index < 0) { selectedProperties.Add(new Label(index == -2 ? "중복 배치입니다." : "이 층에 요소가 없습니다.")); return; }
                ElementPlacementDefinition value = level.Elements[index];
                ElementDefinition definition = level.CreateElementCatalog().Get(new ElementId(value.definitionId));
                SerializedProperty item = data.FindProperty($"elements.Array.data[{index}]");
                selectedProperties.Add(new Label(definition.DisplayName + "\n" + value.definitionId + "\n기준 " + value.coordinate));
                if (value.layer == PlacementLayer.Obstacle) selectedProperties.Add(new Label("본체 " + value.instanceId));
                if (definition.Placement != null) AddNumberProperty(item, "durability", "내구도", 1, definition.Placement.MaxDurability);
                if (definition.ChargePlacement != null) AddNumberProperty(item, "requiredCharge", "필요 충전량",
                    definition.ChargePlacement.MinRequiredCharge, definition.ChargePlacement.MaxRequiredCharge);
                if (value.hasColor)
                {
                    int[] colors = level.Colors.Select(color => (int)color).Distinct().ToArray();
                    AddChoiceProperty(item, "color", "색", colors.Select(color => "달토끼 " + (color + 1)).ToList(), colors);
                }
                if (definition.Supply?.Content == Simulation.RuntimeContent.Rocket)
                    AddChoiceProperty(item, "rocketDirection", "제거 방향", new System.Collections.Generic.List<string> { "가로 ↔", "세로 ↕" }, new[] { 0, 1 });
                string snapshot = JsonUtility.ToJson(level); LevelDefinition owner = level;
                if (value.layer == PlacementLayer.Obstacle && ElementPlacementEditing.Size(definition) > 1)
                    selectedProperties.Add(new Button(() =>
                    { if (level == owner && JsonUtility.ToJson(level) == snapshot) board.BeginMove(index); }) { text = "본체 이동", name = "move-obstacle" });
                selectedProperties.Add(new Button(() =>
                {
                    if (level != owner || JsonUtility.ToJson(level) != snapshot) { Refresh(); return; }
                    board.CancelStroke();
                    operation.text = ElementPlacementEditing.Apply(level, new PlacementBrush { Layer = value.layer, Erase = true }, new[] { coordinate }).ToString();
                    Refresh();
                }) { text = "선택 요소 삭제", name = "delete-placement" });
            }
            catch (Exception error) { selectedProperties.Add(new HelpBox(error.Message, HelpBoxMessageType.Error)); }
        }
    }
}

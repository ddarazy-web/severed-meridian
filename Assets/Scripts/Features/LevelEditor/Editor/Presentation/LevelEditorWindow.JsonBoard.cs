using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Elements;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        private bool TryApplyJsonStroke(LevelBrush brush, RabbitColor color, IReadOnlyCollection<BoardCoordinate> cells)
        {
            if (!IsJsonMode || level.SchemaVersion != 5 ||
                brush != LevelBrush.Placement && brush != LevelBrush.Fixed && brush != LevelBrush.Random && brush != LevelBrush.Erase) return false;
            var placement = brush == LevelBrush.Placement ? board.Placement : new PlacementBrush
            {
                Layer = PlacementLayer.Block, Kind = brush == LevelBrush.Fixed ? (int)InitialBlockKind.FixedNormal : (int)InitialBlockKind.RandomNormal,
                Color = color, Erase = brush == LevelBrush.Erase
            };
            JsonAction(() => operation.text = ApplyJsonPlacement(placement, cells).ToString());
            Refresh(); return true;
        }
        private PlacementEditResult ApplyJsonPlacement(PlacementBrush brush, IReadOnlyCollection<BoardCoordinate> cells)
        {
            var result = new PlacementEditResult();
            var definitions = JsonDefinitions();
            string id = brush.Erase ? null : Elements.Editor.ElementPlacementEditing.BrushDefinition(level.CreateElementCatalog(), brush).Id.Value;
            jsonWorkspace.Session.Apply("보드 배치", documents =>
            {
                JObject document = documents[jsonWorkspace.Session.SelectedLevelId].Data;
                foreach (BoardCoordinate cell in cells)
                {
                    JObject before = (JObject)document.DeepClone();
                    try
                    {
                        if (brush.Erase) LevelDocumentEditing.Erase(document, definitions, brush.Layer.ToString(), cell.Row, cell.Column);
                        else LevelDocumentEditing.Place(document, definitions, id, cell.Row, cell.Column, brush.ReplaceExisting,
                            brush.Durability, brush.RequiredCharge, brush.Color.ToString(), brush.Direction.ToString());
                        if (!JToken.DeepEquals(before, document)) result.Changed++;
                    }
                    catch (ContentFormatException error) { result.Skipped++; result.Reasons.Add(error.Message); }
                }
            });
            jsonWorkspace.RestoreDisplay(); PersistJsonState();
            return result;
        }
        private Dictionary<string, JObject> JsonDefinitions()
        {
            // 기본 내장 정의도 현재 카탈로그의 실제 값을 사용한다. JSON 원본에 자동 추가하지 않는다.
            var result = new Dictionary<string, JObject>(StringComparer.Ordinal);
            ElementDefinitionAsset temporary = CreateInstance<ElementDefinitionAsset>();
            try
            {
                foreach (ElementDefinition definition in level.CreateElementCatalog().Definitions)
                {
                    JsonUtility.FromJsonOverwrite("{\"definition\":" + JsonUtility.ToJson(PackedElementDefinition.FromDefinition(definition)) + "}", temporary);
                    result.Add(definition.Id.Value, (JObject)UnityAuthoringCodec.WriteDraft(temporary, "element", "display-definition", _ => null, _ => null).Data["definition"]);
                }
            }
            finally { DestroyImmediate(temporary); }
            return result;
        }
        private void MoveJsonPlacement(int index, BoardCoordinate destination)
        {
            var definitions = JsonDefinitions();
            string id = level.Elements[index].instanceId;
            jsonWorkspace.Session.Apply("본체 이동", documents => LevelDocumentEditing.Move(
                documents[jsonWorkspace.Session.SelectedLevelId].Data, definitions, id, destination.Row, destination.Column));
            jsonWorkspace.RestoreDisplay(); PersistJsonState();
            operation.text = "본체를 이동했습니다.";
        }
    }
}

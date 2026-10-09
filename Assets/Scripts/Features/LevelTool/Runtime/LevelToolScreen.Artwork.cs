#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Elements;
using GameScreen;
using LevelAuthoring.Runtime;
using Levels;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private PuzzleArtwork artwork;
        private string artworkKey;
        private int artworkRevision;

        private async UniTask DrawArtwork(JObject level)
        {
            int revision = ++artworkRevision;
            try
            {
                string catalogId = (string)level["catalogId"];
                string visualId = catalogId == null ? null : (string)Session.Get(catalogId).Data["visualId"];
                string key = Workspace.Folder + "|" + Session.SelectedLevelId + "|" +
                    (visualId == null ? "builtin" : Session.Get(visualId).Data.ToString()) + "|" +
                    Session.Documents.First(doc => doc.Kind == "project").Data["resources"];
                if (artwork == null || artworkKey != key)
                {
                    ElementVisualCatalog catalog = LegacyElementVisuals.Catalog;
                    if (visualId != null)
                    {
                        Dictionary<string, string> paths = Session.Documents.First(doc => doc.Kind == "project").Data["resources"]
                            .ToDictionary(item => (string)item["id"], item => (string)item["path"]);
                        List<ScriptableObject> owned = new List<ScriptableObject>();
                        try
                        {
                            ElementVisualCatalogAsset asset = (ElementVisualCatalogAsset)UnityAuthoringCodec.ReadDraft(Session.Get(visualId),
                                typeof(ElementVisualCatalogAsset), _ => null, id => paths[id], owned.Add);
                            catalog = asset.CreateCatalog();
                        }
                        finally { foreach (ScriptableObject value in owned) Destroy(value); }
                    }
                    artwork?.Dispose(); artwork = new PuzzleArtwork(catalog); artworkKey = key;
                }
                PuzzleArtwork owner = artwork;
                List<(JObject item, ElementVisualFrame frame)> frames = new List<(JObject, ElementVisualFrame)>();
                foreach (JObject item in ((JArray)level["elements"]).OfType<JObject>())
                {
                    string id = (string)item["definitionId"];
                    try
                    {
                        JObject definition = definitions[id];
                        if ((string)definition["supply"]?["behavior"] == "RandomNormal") { Fallback(item, "?", "게임 시작 때 색이 정해지는 일반 블록"); continue; }
                        ElementVisualFrame frame = owner.Visuals.Resolver.Resolve(new ElementId(id), new ElementVisualState(
                            (int)Enum.Parse(typeof(RabbitColor), (string)item["color"]), (int)item["durability"], 0,
                            (int)item["requiredCharge"], (int)Enum.Parse(typeof(RocketDirection), (string)item["rocketDirection"]), 0, LevelToolDocuments.Size(definition)));
                        frames.Add((item, frame));
                    }
                    catch (Exception error) { Fallback(item, "!", id + "\n" + error.Message); }
                }
                try { await owner.PrepareEffectsAsync(frames.Select(value => value.frame.Path), this.GetCancellationTokenOnDestroy()); }
                catch (Exception error) { if (revision == artworkRevision) Show("이미지 로드 실패: " + error.Message); }
                if (this == null || revision != artworkRevision) return;
                foreach (var value in frames.OrderBy(value => value.frame.Order))
                {
                    try
                    {
                        Sprite sprite = owner.GetVisual(value.frame);
                        ElementVisualFrame frame = value.frame;
                        float logicalSize = LevelToolDocuments.Size(definitions[(string)value.item["definitionId"]]);
                        float side = cellSize * frame.Size;
                        float width = side * sprite.rect.width / Mathf.Max(sprite.rect.width, sprite.rect.height);
                        float height = side * sprite.rect.height / Mathf.Max(sprite.rect.width, sprite.rect.height);
                        Image image = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                        image.style.position = Position.Absolute;
                        image.style.left = ((int)value.item["coordinate"]["column"] + logicalSize / 2) * cellSize + frame.Offset.x * side - width * frame.Pivot.x;
                        image.style.top = ((int)value.item["coordinate"]["row"] + logicalSize / 2) * cellSize - frame.Offset.y * side - height * (1 - frame.Pivot.y);
                        image.style.width = width; image.style.height = height;
                        image.style.rotate = new Rotate(new Angle(-frame.Angle));
                        board.Add(image);
                    }
                    catch (Exception error) { Fallback(value.item, "!", error.Message); }
                }
                foreach (JToken source in level["elementSupply"]["sources"])
                {
                    int row = (int)source["coordinate"]["row"], column = (int)source["coordinate"]["column"];
                    Label marker = new Label("↓") { name = "source-" + (row * 9 + column), pickingMode = PickingMode.Ignore };
                    marker.style.position = Position.Absolute; marker.style.left = (column + .75f) * cellSize; marker.style.top = row * cellSize;
                    marker.style.color = new Color(.5f, 1f, .85f); marker.style.backgroundColor = new Color(.05f, .12f, .18f, .85f);
                    board.Add(marker);
                }
                board.Q("flow-overlay")?.BringToFront();
                board.Q("connection-overlay")?.BringToFront();
                board.Q("tutorial-preview-overlay")?.BringToFront();
                board.Q("tutorial-pick-overlay")?.BringToFront();
            }
            catch (Exception error) { if (revision == artworkRevision) Show("이미지 표시 실패: " + error.Message); }
        }

        private void Fallback(JObject item, string text, string reason)
        {
            int cell = (int)item["coordinate"]["row"] * 9 + (int)item["coordinate"]["column"];
            Label tile = board.Q<Label>("cell-" + cell);
            if (tile != null) { tile.text = text; tile.tooltip += "\n" + reason; }
        }
    }
}
#endif

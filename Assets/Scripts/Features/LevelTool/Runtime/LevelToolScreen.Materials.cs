#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private string materialCategory = "전체", materialQuery = "", materialIconKey;
        private readonly List<string> recentMaterials = new List<string>();

        private void BuildMaterialFilters()
        {
            var category = new DropdownField("분류", new List<string> { "전체", "일반 블록", "파워 블록", "장애물", "덮개", "바닥", "최근 사용" }, 0)
                { name = "material-category" };
            category.SetValueWithoutNotify(materialCategory);
            category.RegisterValueChangedCallback(evt => { materialCategory = evt.newValue; RefreshMaterialPalette(); });
            materialPanel.Add(category);
            var search = new TextField("검색") { name = "material-search", value = materialQuery };
            search.tooltip = "이름 또는 ID로 찾습니다. 검색·분류는 제작 데이터를 변경하지 않습니다.";
            search.RegisterValueChangedCallback(evt => { materialQuery = evt.newValue; RefreshMaterialPalette(); });
            materialPanel.Add(search);
        }

        private static string LayerLabel(string value) => value == "Block" ? "블록" : value == "Obstacle" ? "장애물" : value == "Cover" ? "덮개" : "바닥";
        private static string MaterialCategory(JObject definition)
        {
            string targetLayer = LevelToolDocuments.Layer(definition);
            return targetLayer == "Block" ? (string)definition["supply"]?["behavior"] == "Power" ? "파워 블록" : "일반 블록" : LayerLabel(targetLayer);
        }

        private void RefreshMaterialPalette()
        {
            if (palette == null || Session?.SelectedLevelId == null) return;
            string key = Workspace.Folder + "|" + Session.SelectedLevelId + "|" + definitionRevision + "|" + materialCategory + "|" + materialQuery + "|" + color + "|" + string.Join(",", recentMaterials);
            if (paletteKey != key)
            {
                paletteKey = key; materialIconKey = null; palette.Clear();
                string query = materialQuery.Trim();
                IEnumerable<KeyValuePair<string, JObject>> choices = definitions.Where(pair => LevelToolDocuments.Layer(pair.Value) != null &&
                    (materialCategory == "전체" || materialCategory == "최근 사용" && recentMaterials.Contains(pair.Key) || MaterialCategory(pair.Value) == materialCategory) &&
                    (query.Length == 0 || pair.Key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        ((string)pair.Value["displayName"] ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
                if (materialCategory == "최근 사용") choices = choices.OrderBy(pair => recentMaterials.IndexOf(pair.Key));
                foreach (var pair in choices)
                {
                    string id = pair.Key;
                    Button item = Button(palette, id, "", () => SelectMaterial(id)); item.AddToClassList("material-item");
                    item.Add(new Image { name = "material-image", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore });
                    item.Add(new Label((string)pair.Value["displayName"]) { pickingMode = PickingMode.Ignore });
                    item.tooltip = LayerLabel(LevelToolDocuments.Layer(pair.Value)) + " 배치 · " + id;
                }
                if (palette.Query<Button>().ToList().Count == 0) palette.Add(new Label("해당 소재가 없습니다. 분류나 검색어를 바꿔 주세요."));
            }
            foreach (var item in palette.Query<Button>().ToList()) item.EnableInClassList("selected", item.name == brush);
            if (materialsVisible) EnsureMaterialPreviews();
        }

        private void SelectMaterial(string id)
        {
            if (!definitions.TryGetValue(id, out JObject definition)) return;
            CancelBoardTouch(); boardTouchTool = "tap"; brush = id; moveBody = null;
            layer = LevelToolDocuments.Layer(definition);
            root.Q<DropdownField>("edit-layer").SetValueWithoutNotify(layer);
            boardTouchReplace = true;
            root.Q<Toggle>("board-touch-replace").SetValueWithoutNotify(true);
            recentMaterials.Remove(id); recentMaterials.Insert(0, id);
            if (recentMaterials.Count > 20) recentMaterials.RemoveAt(20);
            if (mobileNavigation != null && !mobileWide) mobilePanel = null;
            UpdateBoardTouchHint(); Refresh();
            Show(LayerLabel(layer) + " 배치 중 · " + (string)definition["displayName"] + " · 같은 층은 교체하고 다른 층은 유지합니다.");
        }

        private void EnsureMaterialPreviews()
        {
            if (paletteKey == null || materialIconKey == paletteKey || boardView == null || Session?.SelectedLevelId == null) return;
            materialIconKey = paletteKey;
            LoadMaterialPreviews(paletteKey).Forget();
        }

        private async UniTask LoadMaterialPreviews(string key)
        {
            try
            {
                // 보드가 현재 프로젝트의 시각 카탈로그를 준비한 다음 같은 아틀라스를 사용한다.
                await UniTask.Yield();
                if (this == null || !isActiveAndEnabled || key != paletteKey) return;
                var items = palette.Query<Button>().ToList();
                var selected = items.ToDictionary(item => item.name, item => definitions[item.name]);
                var sprites = await boardView.PrepareMaterialPreviews(selected, color);
                if (this == null || !isActiveAndEnabled || key != paletteKey) return;
                foreach (Button item in items)
                    if (sprites.TryGetValue(item.name, out Sprite sprite)) item.Q<Image>("material-image").sprite = sprite;
                    else item.tooltip += " · 미리보기 이미지 없음";
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (this != null && isActiveAndEnabled) Show("소재 미리보기: " + error.Message); }
        }

        private async UniTask LoadMissionPreview(Image image, string kind, string tint)
        {
            object request = new object(); image.userData = request;
            try
            {
                await UniTask.Yield();
                if (this == null || !isActiveAndEnabled || image.panel == null || image.userData != request) return;
                Sprite sprite = await boardView.PrepareMissionPreview(kind, tint);
                if (this != null && isActiveAndEnabled && image.panel != null && image.userData == request) image.sprite = sprite;
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (this != null && isActiveAndEnabled) Show("미션 미리보기: " + error.Message); }
        }

        private void RefreshBoardToolStatus()
        {
            string action = moveBody != null ? "이동" : boardTouchTool == "erase" ? "지우기" : brush != null ? "배치" : boardTouchTool == "area" ? "영역 선택" : boardTouchTool == "pan" ? "화면 이동" : "선택";
            string name = brush != null && definitions.TryGetValue(brush, out JObject definition) ? " · " + (string)definition["displayName"] : "";
            string level = mobileNavigation != null && !mobilePortrait && !mobileWide ? levelTitle.text.Split('·')[0].Trim() + " · " : "";
            if (currentToolLabel != null) { currentToolLabel.text = level + WorkspacePage + " · " + LayerLabel(layer) + " " + action + name; currentToolLabel.tooltip = currentToolLabel.text; }
            if (boardZoomLabel != null) { boardZoomLabel.text = Mathf.RoundToInt(cellSize / 52f * 100) + "%"; boardZoomLabel.tooltip = "보드만 확대 · Ctrl+휠 / 두 손가락 · 화면 맞춤으로 복원"; }
            root?.Q("board-touch-select")?.EnableInClassList("selected", brush == null && boardTouchTool == "tap");
            root?.Q("board-touch-materials")?.EnableInClassList("selected", brush != null);
            root?.Q("board-touch-erase")?.EnableInClassList("selected", boardTouchTool == "erase");
        }
    }
}
#endif

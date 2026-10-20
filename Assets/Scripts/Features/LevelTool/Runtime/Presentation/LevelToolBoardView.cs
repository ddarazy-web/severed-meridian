#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool.Presentation
{
    // 표시 전용 입력이다. 편집 중인 경로 목록의 소유권은 화면에 남긴다.
    public sealed class LevelToolBoardOverlayState
    {
        public string Page;
        public int FlowFirst = -1, SelectedConnection = -1;
        public int[] FlowRoute = Array.Empty<int>(), WireRoute = Array.Empty<int>();
        public bool WireDrawing, Mobile;
    }

    public sealed partial class LevelToolBoardView : IDisposable
    {
        private readonly Action<string> show;
        private readonly Action<int> selectVertex;
        private readonly Func<bool> blocked;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private bool disposed;
        private AuthoringEditSession displayedSession;
        private string displayedLevelId, displayedFolder, displayedLayer;
        private long displayedVersion;
        private IReadOnlyDictionary<string, JObject> displayedDefinitions;
        private JObject displayedLevel;
        private float displayedCellSize;
        public VisualElement Root { get; }

        public LevelToolBoardView(VisualElement root, Action<string> show, Action<int> selectVertex, Func<bool> blocked)
        { Root = root; this.show = show; this.selectVertex = selectVertex; this.blocked = blocked; }

        public void Refresh(AuthoringEditSession session, string folder, IReadOnlyDictionary<string, JObject> definitions,
            string layer, float cellSize, LevelToolBoardOverlayState overlay)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LevelToolBoardView));
            bool reset = !ReferenceEquals(displayedSession, session) || displayedLevelId != session.SelectedLevelId || displayedFolder != folder;
            if (reset) Clear();
            long version = session.GetDocumentVersion(session.SelectedLevelId);
            JObject level = reset || displayedLevel == null || displayedVersion != version
                ? LevelToolDocuments.UpgradePreview(session.Get(session.SelectedLevelId).Data) : displayedLevel;
            bool contentChanged = reset || displayedLevel == null || displayedLayer != layer || !ReferenceEquals(displayedDefinitions, definitions) ||
                new[] { "board", "elements", "flow", "connections", "elementSupply", "catalogId" }
                    .Any(key => !JToken.DeepEquals(level[key], displayedLevel[key]));
            bool tilesChanged = contentChanged || displayedLayer != layer;
            float previousSize = displayedCellSize;
            displayedSession = session; displayedLevelId = session.SelectedLevelId; displayedFolder = folder;
            displayedVersion = version; displayedDefinitions = definitions; displayedLevel = level;
            displayedLayer = layer; displayedCellSize = cellSize;
            Root.style.width = cellSize * 9; Root.style.height = cellSize * 9;
            int[] selected = session.SelectedCells;
            for (int index = 0; index < 81; index++)
            {
                bool active = (bool)level["board"]["cells"][index]["isActive"];
                Label tile = Root.Q<Label>("cell-" + index);
                if (tile == null)
                {
                    tile = new Label { name = "cell-" + index }; tile.AddToClassList("cell"); Root.Insert(index, tile);
                }
                tile.style.width = cellSize; tile.style.height = cellSize;
                tile.EnableInClassList("selected", selected.Contains(index));
                if (!tilesChanged) continue;
                tile.EnableInClassList("inactive", !active);
                JObject item = level["elements"].OfType<JObject>().FirstOrDefault(value =>
                {
                    if ((string)value["layer"] != layer) return false;
                    int row = (int)value["coordinate"]["row"], column = (int)value["coordinate"]["column"];
                    int size = definitions.TryGetValue((string)value["definitionId"], out JObject definition) ? LevelToolDocuments.Size(definition) : 1;
                    return index / 9 >= row && index / 9 < row + size && index % 9 >= column && index % 9 < column + size;
                });
                tile.text = active ? "·" : "×";
                tile.tooltip = "행 " + (index / 9 + 1) + " · 열 " + (index % 9 + 1) + (item == null ? "" : "\n" + item["definitionId"]);
            }
            Root.Q("flow-overlay")?.RemoveFromHierarchy();
            Root.Q("connection-overlay")?.RemoveFromHierarchy();
            DrawFlowOverlay(level, cellSize, overlay);
            DrawConnectionOverlay(level, cellSize, overlay);
            if (contentChanged)
            {
                foreach (VisualElement element in Root.Children().Where(child => child.ClassListContains("board-artwork")).ToArray()) element.RemoveFromHierarchy();
                DrawArtwork(level, session, folder, definitions, cellSize).Forget();
            }
            else if (previousSize > 0 && previousSize != cellSize)
            {
                float scale = cellSize / previousSize;
                foreach (VisualElement element in Root.Children().Where(child => child.ClassListContains("board-artwork")))
                {
                    element.style.left = element.style.left.value.value * scale;
                    element.style.top = element.style.top.value.value * scale;
                    if (element is Image)
                    {
                        element.style.width = element.style.width.value.value * scale;
                        element.style.height = element.style.height.value.value * scale;
                    }
                }
            }
        }

        public void Clear()
        {
            artworkRevision++;
            Root.Clear();
            displayedLevel = null; displayedSession = null; displayedDefinitions = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; artworkRevision++;
            lifetime.Cancel(); lifetime.Dispose(); artwork?.Dispose(); artwork = null;
            Root.Clear();
            displayedLevel = null; displayedSession = null; displayedDefinitions = null;
        }
    }
}
#endif

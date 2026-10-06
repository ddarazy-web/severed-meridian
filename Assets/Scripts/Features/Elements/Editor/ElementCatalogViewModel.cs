using System;
using System.Collections.Generic;
using System.Linq;
using Levels;

namespace Elements.Editor
{
    /// <summary>원본 에셋과 독립된 카탈로그 조회·선택 상태.</summary>
    public sealed class ElementCatalogViewModel : IDisposable
    {
        private ElementCatalog catalog;
        private ElementId selected;
        private string search = "";
        private PlacementLayer? layer;
        private bool disposed;
        public event Action Changed;
        public string Search => search;
        public IReadOnlyList<ElementDefinition> Visible { get; private set; } = Array.Empty<ElementDefinition>();
        public ElementDefinition SelectedDefinition { get; private set; }
        public string SelectionError { get; private set; }
        public bool CanPlace => !disposed && SelectedDefinition != null && LayerOf(SelectedDefinition).HasValue &&
            SelectionError == null && Visible.Any(item => item.Id == selected);

        public ElementCatalogViewModel(ElementCatalog catalog) { SetCatalog(catalog); }
        public void SetCatalog(ElementCatalog value)
        {
            if (disposed) return;
            catalog = value ?? throw new ArgumentNullException(nameof(value));
            Refresh();
        }
        public void Select(ElementId value) { if (disposed) return; selected = value; Refresh(); }
        public void SetSearch(string value) { if (disposed) return; search = value ?? ""; Refresh(); }
        public void SetLayer(PlacementLayer? value) { if (disposed) return; layer = value; Refresh(); }
        private void Refresh()
        {
            SelectedDefinition = catalog.Definitions.FirstOrDefault(item => item.Id == selected);
            SelectionError = null;
            if (SelectedDefinition != null)
                try { PackedElementDefinition.FromDefinition(SelectedDefinition).ToDefinition(); }
                catch (ArgumentException error) { SelectionError = error.Message; }
                catch (InvalidOperationException error) { SelectionError = error.Message; }
            Visible = Array.AsReadOnly(catalog.Definitions.Where(item => (!layer.HasValue || LayerOf(item) == layer) &&
                (item.Id.Value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 item.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(item => item.DisplayName, StringComparer.Ordinal).ThenBy(item => item.Id.Value, StringComparer.Ordinal).ToArray());
            Changed?.Invoke();
        }
        public static PlacementLayer? LayerOf(ElementDefinition definition)
        {
            if (definition.ReactionBehavior.HasValue) return PlacementLayer.Obstacle;
            if (definition.Layer != null) return definition.Layer.Behavior == ElementLayerBehavior.NormalConsumption ? PlacementLayer.Dust : PlacementLayer.Cover;
            if (definition.Supply != null && definition.Supply.Behavior != ElementSupplyBehavior.Obstacle &&
                definition.Supply.Behavior != ElementSupplyBehavior.RandomPower) return PlacementLayer.Block;
            return null;
        }
        public void Dispose()
        {
            disposed = true; Changed = null; SelectedDefinition = null; SelectionError = null; Visible = Array.Empty<ElementDefinition>();
        }
    }
}

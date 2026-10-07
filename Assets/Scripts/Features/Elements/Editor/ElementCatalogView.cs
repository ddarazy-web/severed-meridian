using System;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Elements.Editor
{
    /// <summary>카탈로그 상태를 표시하고 선택 명령만 전달하는 화면.</summary>
    public sealed class ElementCatalogView : VisualElement, IDisposable
    {
        private readonly ElementCatalogViewModel model;
        private readonly ListView entries = new ListView();
        private readonly Label selection = new Label();
        private readonly Button place;
        public ElementCatalogView(ElementCatalogViewModel model, Action<ElementDefinition> selected)
        {
            this.model = model;
            name = "element-catalog-view";
            ToolbarSearchField search = new ToolbarSearchField { name = "element-catalog-search" };
            search.SetValueWithoutNotify(model.Search);
            search.RegisterValueChangedCallback(evt => model.SetSearch(evt.newValue));
            entries.name = "element-catalog-entries";
            entries.fixedItemHeight = 24;
            entries.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            entries.selectionType = SelectionType.None;
            entries.style.height = 240;
            entries.makeItem = () =>
            {
                Button entry = new Button();
                entry.clicked += () => { if (entry.userData is ElementId id) model.Select(id); };
                return entry;
            };
            entries.bindItem = (row, index) =>
            {
                ElementDefinition definition = (ElementDefinition)entries.itemsSource[index];
                Button entry = (Button)row;
                entry.text = definition.DisplayName; entry.tooltip = definition.Id.Value;
                entry.name = "definition-" + definition.Id.Value; entry.userData = definition.Id;
            };
            entries.unbindItem = (row, index) => { row.name = ""; row.userData = null; };
            Add(search); Add(entries); Add(selection);
            place = new Button(() => { if (model.CanPlace) selected(model.SelectedDefinition); })
                { text = "선택 정의 배치", name = "element-catalog-place" };
            Add(place); model.Changed += Render; Render();
        }
        private void Render()
        {
            entries.itemsSource = model.Visible.ToList();
            selection.text = model.SelectedDefinition == null ? "정의를 선택하세요." : model.SelectedDefinition.DisplayName + "\n" + model.SelectedDefinition.Id.Value +
                (model.SelectionError == null ? "" : "\n" + model.SelectionError);
            place.SetEnabled(model.CanPlace);
        }
        public void Dispose() { model.Changed -= Render; }
    }
}
